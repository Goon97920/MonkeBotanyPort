using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Botany.Components;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Botany;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Botany.Systems;

/// <summary>
/// Hydroponics trays. Ported from Monkestation's plant_growing and growth_information components.
/// Every cycle (10 s) the tray's reagents apply their effects, the plant drinks, heals or takes
/// damage, grows, and 2.5% of the nutrient solution is used up.
/// </summary>
public sealed partial class PlantTraySystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private PlantTraitSystem _traits = default!;
    [Dependency] private PointLightSystem _light = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    public static readonly ProtoId<ReagentPrototype> Water = "Water";

    /// <summary>Monkestation refuses to harvest onto a tile this crowded.</summary>
    private const int MaxItemsOnTile = 50;
    private const int MaxItemsNearby = 100;

    private readonly List<EntityEffect> _trayEffects = new();
    private readonly List<EntityEffect> _plantEffects = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantTrayComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PlantTrayComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PlantTrayComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<PlantTrayComponent, SolutionTransferAttemptEvent>(OnTransferAttempt);
        SubscribeLocalEvent<PlantTrayComponent, SolutionTransferredEvent>(OnSolutionTransferred);
        SubscribeLocalEvent<PlantTrayComponent, PlantSpadeDoAfterEvent>(OnSpadeDoAfter);
    }

    private void OnMapInit(Entity<PlantTrayComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextWork = _timing.CurTime + ent.Comp.WorkInterval;
        UpdateNutrients(ent);
        UpdateAppearance(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PlantTrayComponent>();
        while (query.MoveNext(out var uid, out var tray))
        {
            if (now < tray.NextWork)
                continue;

            tray.NextWork = now + tray.WorkInterval;
            ProcessTray((uid, tray));
        }
    }

    #region Cycle

    /// <summary>One Monkestation plant_growing/process tick.</summary>
    public void ProcessTray(Entity<PlantTrayComponent> ent)
    {
        var tray = ent.Comp;
        tray.WorkCycle++;

        AbsorbSoilWater(ent);
        var reagents = GetSoilReagents(ent);

        // Grower effects: toxicity, pests, weeds, self-sustenance.
        foreach (var (proto, quantity) in reagents)
        {
            SplitEffects(proto);
            if (_trayEffects.Count > 0)
                _effects.ApplyEffects(ent, _trayEffects.ToArray(), quantity);
        }

        if (tray.Genome is { } genome)
        {
            UpdateTrayGlow(ent, genome);
            ProcessPlant(ent, genome, reagents);
        }

        if (tray.WorkCycle >= 2)
            tray.WorkCycle = 0;

        // Use up 2.5% of the nutrient solution (at least 1u) while there is more than 5u.
        if (_solution.ResolveSolution(ent.Owner, tray.SoilSolutionName, ref tray.SoilSolution, out var soil)
            && soil.Volume > 5)
        {
            var remove = FixedPoint2.Max(FixedPoint2.New(1), FixedPoint2.New(MathF.Round(soil.Volume.Float() * 0.025f, 2)));
            _solution.SplitSolution(tray.SoilSolution.Value, remove);
        }

        UpdateNutrients(ent);
        UpdateAppearance(ent);
        Dirty(ent);
    }

    private void ProcessPlant(Entity<PlantTrayComponent> ent, PlantGenome genome, List<(ReagentPrototype Proto, float Quantity)> reagents)
    {
        var tray = ent.Comp;

        if (tray.WorkCycle >= 2)
        {
            AdjustWater(ent, -_random.Next(1, 7));
            if (tray.WaterPercent <= 0 || tray.Weeds >= PlantTrayComponent.MaxWeeds)
            {
                AdjustHealth(ent, -_random.Next(0, 3));
                return;
            }

            if (tray.Nutrients <= 5)
                AdjustHealth(ent, -_random.Next(0, 3));
        }

        if (tray.WaterPercent >= 10)
            AdjustHealth(ent, _random.Next(1, 3));

        // growth_information/process_growth
        tray.GrowthCycle++;
        var growthMult = MathF.Pow(1.01f, -genome.Maturation);
        var mature = tray.Age > MathF.Max(genome.Maturation, genome.Production)
                     && tray.GrowthCycle >= genome.HarvestAge * growthMult;
        if (!mature)
            tray.Age++;

        if (tray.Age > genome.Lifespan + tray.RepeatedHarvestValue)
            AdjustHealth(ent, -_random.Next(1, 6));

        foreach (var (proto, quantity) in reagents)
        {
            SplitEffects(proto);
            if (_plantEffects.Count > 0)
                _effects.ApplyEffects(ent, _plantEffects.ToArray(), quantity);
        }

        UpdateGrowthState(ent);

        // Monkestation never actually grows weeds from weed rate/chance: the signal is sent to the
        // seed, which doesn't listen for it. Weeds only come from reagents like sugar and honey.
    }

    private void SplitEffects(ReagentPrototype proto)
    {
        _trayEffects.Clear();
        _plantEffects.Clear();
        foreach (var effect in proto.PlantMetabolisms)
        {
            if (effect is IPlantTrayLevelEffect)
                _trayEffects.Add(effect);
            else
                _plantEffects.Add(effect);
        }
    }

    private List<(ReagentPrototype Proto, float Quantity)> GetSoilReagents(Entity<PlantTrayComponent> ent)
    {
        var list = new List<(ReagentPrototype, float)>();
        if (!_solution.ResolveSolution(ent.Owner, ent.Comp.SoilSolutionName, ref ent.Comp.SoilSolution, out var soil))
            return list;

        foreach (var reagent in soil.Contents)
        {
            if (_proto.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out var proto) && proto.PlantMetabolisms.Count > 0)
                list.Add((proto, reagent.Quantity.Float()));
        }

        return list;
    }

    /// <summary>
    /// Monkestation update_growth_information: growth percent, harvestable at 100%, dead at 0 health.
    /// </summary>
    public void UpdateGrowthState(Entity<PlantTrayComponent> ent)
    {
        var tray = ent.Comp;
        if (tray.Genome is not { } genome)
        {
            tray.State = PlantState.None;
            tray.GrowthPercent = 0;
            return;
        }

        if (tray.State == PlantState.Dead)
            return;

        var needed = MathF.Round(genome.HarvestAge * MathF.Pow(1.01f, -genome.Maturation));
        tray.GrowthPercent = needed <= 0 ? 100f : MathF.Round(tray.GrowthCycle / needed * 100f);

        tray.State = tray.GrowthPercent >= 100 ? PlantState.Harvestable : PlantState.Growing;
        if (tray.Health <= 0)
            tray.State = PlantState.Dead;
    }

    #endregion

    #region Adjustments

    public void AdjustHealth(Entity<PlantTrayComponent> ent, float amount)
    {
        var tray = ent.Comp;
        if (tray.Genome is not { } genome || tray.State == PlantState.Dead)
            return;

        tray.Health = Math.Clamp(tray.Health + amount, 0, genome.Endurance * 3);
        if (tray.Health <= 0)
            tray.State = PlantState.Dead;
    }

    /// <summary>Adjusts water by an amount of units (Monkestation trays hold 200).</summary>
    public void AdjustWater(Entity<PlantTrayComponent> ent, float units)
    {
        var tray = ent.Comp;
        tray.WaterPercent = Math.Clamp(tray.WaterPercent + units / tray.MaxWater * 100f, 0f, 100f);
    }

    public void AdjustTrayAttribute(Entity<PlantTrayComponent> ent, PlantTrayAttribute attribute, float amount)
    {
        var tray = ent.Comp;
        switch (attribute)
        {
            case PlantTrayAttribute.Toxicity:
                tray.Toxicity = MathF.Max(0, tray.Toxicity + amount);
                break;
            case PlantTrayAttribute.Pests:
                tray.Pests = Math.Clamp(tray.Pests + amount, 0, PlantTrayComponent.MaxPests);
                break;
            case PlantTrayAttribute.Weeds:
                tray.Weeds = Math.Clamp(tray.Weeds + amount, 0, PlantTrayComponent.MaxWeeds);
                break;
            case PlantTrayAttribute.SelfSustain:
                tray.SelfSustain = Math.Clamp(tray.SelfSustain + amount, 0, 100);
                break;
        }
    }

    public void SpeedUp(Entity<PlantTrayComponent> ent, TimeSpan time)
    {
        ent.Comp.NextWork -= time;
    }

    #endregion

    #region Water and nutrients

    /// <summary>
    /// Monkestation trays keep water separately from nutrients: water poured in fills the water
    /// level directly, and only the rest of the solution goes into the tray.
    /// </summary>
    private void OnTransferAttempt(Entity<PlantTrayComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (args.To != ent.Owner || args.CancelReason != null)
            return;

        if (!_solution.TryGetDrainableSolution(args.From, out var source, out var sourceSolution))
            return;

        var wanted = (100f - ent.Comp.WaterPercent) / 100f * ent.Comp.MaxWater;
        if (wanted <= 0)
            return;

        var water = sourceSolution.GetTotalPrototypeQuantity(Water).Float();
        var moved = MathF.Min(water, MathF.Floor(wanted));
        if (moved <= 0)
            return;

        _solution.RemoveReagent(source.Value, Water, FixedPoint2.New(moved));
        AdjustWater(ent, moved);
        _audio.PlayPvs(ent.Comp.WateringSound, ent);
        UpdateAppearance(ent);
        Dirty(ent);
    }

    private void OnSolutionTransferred(Entity<PlantTrayComponent> ent, ref SolutionTransferredEvent args)
    {
        _audio.PlayPvs(ent.Comp.WateringSound, ent);
        AbsorbSoilWater(ent);
        UpdateNutrients(ent);
        UpdateAppearance(ent);
        Dirty(ent);
    }

    /// <summary>Moves water that ended up in the soil (sprays, leftovers) into the water level.</summary>
    private void AbsorbSoilWater(Entity<PlantTrayComponent> ent)
    {
        var tray = ent.Comp;
        if (!_solution.ResolveSolution(ent.Owner, tray.SoilSolutionName, ref tray.SoilSolution, out var soil))
            return;

        var water = soil.GetTotalPrototypeQuantity(Water).Float();
        var wanted = (100f - tray.WaterPercent) / 100f * tray.MaxWater;
        var moved = MathF.Min(water, wanted);
        if (moved <= 0)
            return;

        _solution.RemoveReagent(tray.SoilSolution.Value, Water, FixedPoint2.New(moved));
        AdjustWater(ent, moved);
    }

    private void UpdateNutrients(Entity<PlantTrayComponent> ent)
    {
        var tray = ent.Comp;
        tray.Nutrients = _solution.ResolveSolution(ent.Owner, tray.SoilSolutionName, ref tray.SoilSolution, out var soil)
            ? soil.Volume.Float()
            : 0;
    }

    #endregion

    #region Interaction

    private void OnInteractUsing(Entity<PlantTrayComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;

        if (TryComp<SeedComponent>(used, out var seed))
        {
            args.Handled = TryPlant(ent, (used, seed), args.User);
            return;
        }

        if (TryComp<PlantGraftComponent>(used, out var graft))
        {
            args.Handled = TryApplyGraft(ent, (used, graft), args.User);
            return;
        }

        if (HasComp<BotanyCultivatorComponent>(used))
        {
            args.Handled = true;
            ent.Comp.Weeds = MathF.Max(0, ent.Comp.Weeds - 10);
            _popup.PopupEntity(Loc.GetString("plant-cultivator-weeds-removed", ("tray", ent.Owner)), ent, args.User);
            UpdateAppearance(ent);
            Dirty(ent);
            return;
        }

        if (TryComp<BotanySpadeComponent>(used, out var spade))
        {
            if (ent.Comp.Genome == null)
                return;

            args.Handled = true;
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, spade.Delay, new PlantSpadeDoAfterEvent(), ent, ent, used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }

        if (TryComp<BotanySecateursComponent>(used, out var secateurs))
        {
            args.Handled = TryCutGraft(ent, secateurs, args.User);
            return;
        }

        if (HasComp<BotanyHarvestBagComponent>(used) && TryComp<StorageComponent>(used, out var storage))
        {
            if (ent.Comp.State != PlantState.Harvestable)
                return;

            args.Handled = true;
            var items = TryHarvest(ent, args.User);
            foreach (var item in items)
            {
                if (!TerminatingOrDeleted(item))
                    _storage.Insert(used, item, out _, user: args.User, storageComp: storage, playSound: false);
            }
        }
    }

    private void OnInteractHand(Entity<PlantTrayComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Genome == null)
            return;

        if (ent.Comp.State == PlantState.Dead)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("plant-remove-dead", ("tray", ent.Owner)), ent, args.User);
            RemovePlant(ent);
            return;
        }

        if (ent.Comp.State != PlantState.Harvestable)
            return;

        args.Handled = true;
        TryHarvest(ent, args.User);
    }

    private void OnSpadeDoAfter(Entity<PlantTrayComponent> ent, ref PlantSpadeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.Genome == null)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("plant-spade-removed", ("tray", ent.Owner)), ent, args.User);
        RemovePlant(ent);
    }

    public bool TryPlant(Entity<PlantTrayComponent> ent, Entity<SeedComponent> seed, EntityUid user)
    {
        if (ent.Comp.Genome != null)
        {
            _popup.PopupEntity(Loc.GetString("plant-holder-component-already-seeded-message", ("name", ent.Owner)), ent, user);
            return true;
        }

        if (seed.Comp.Sample || seed.Comp.Genome is not { } genome)
        {
            _popup.PopupEntity(Loc.GetString("plant-cannot-plant-sample"), ent, user);
            return true;
        }

        Plant(ent, genome.Clone());
        _popup.PopupEntity(Loc.GetString("plant-holder-component-plant-success-message", ("seedName", _botany.GetPlantName(genome))), ent, user);

        if (_proto.TryIndex(genome.Species, out var species) && species.PlantLogImpact is { } impact)
            _adminLog.Add(LogType.Botany, impact, $"{ToPrettyString(user):player} planted {_botany.GetPlantName(genome):seed} at Pos:{Transform(ent).Coordinates}.");

        QueueDel(seed);
        return true;
    }

    public void Plant(Entity<PlantTrayComponent> ent, PlantGenome genome)
    {
        var tray = ent.Comp;
        tray.Genome = genome;
        tray.Health = genome.Endurance * 3;
        tray.GrowthCycle = 0;
        tray.Age = 0;
        tray.RepeatedHarvestValue = 0;
        tray.State = PlantState.Growing;
        UpdateGrowthState(ent);
        UpdateTrayGlow(ent, genome);
        UpdateAppearance(ent);
        Dirty(ent);
    }

    public void RemovePlant(Entity<PlantTrayComponent> ent)
    {
        var tray = ent.Comp;
        tray.Genome = null;
        tray.State = PlantState.None;
        tray.GrowthCycle = 0;
        tray.Age = 0;
        tray.Health = 0;
        tray.GrowthPercent = 0;
        tray.RepeatedHarvestValue = 0;
        if (TryComp<PointLightComponent>(ent, out var light))
            _light.SetEnabled(ent, false, light);
        UpdateAppearance(ent);
        Dirty(ent);
    }

    /// <summary>Harvests a ready plant. Perennials regrow, others are removed.</summary>
    public List<EntityUid> TryHarvest(Entity<PlantTrayComponent> ent, EntityUid user)
    {
        var tray = ent.Comp;
        if (tray.Genome is not { } genome || tray.State != PlantState.Harvestable)
            return new List<EntityUid>();

        if (!PreHarvestChecks(ent, user))
            return new List<EntityUid>();

        var coords = Transform(user).Coordinates;
        var items = _botany.Harvest(genome, coords);
        var name = _botany.GetPlantName(genome);

        _popup.PopupEntity(Loc.GetString("botany-harvest-success-message", ("name", name)), user, user, PopupType.Medium);
        if (_proto.TryIndex(genome.Species, out var species) && species.HarvestLogImpact is { } impact)
            _adminLog.Add(LogType.Botany, impact, $"{ToPrettyString(user):player} harvested {items.Count} of {name:seed} at Pos:{coords}.");

        if (_botany.IsPerennial(genome))
        {
            tray.GrowthCycle = 0;
            tray.RepeatedHarvestValue += genome.Lifespan * 0.05f;
            tray.State = PlantState.Growing;
            UpdateGrowthState(ent);
            UpdateAppearance(ent);
            Dirty(ent);
        }
        else
        {
            RemovePlant(ent);
        }

        var ev = new PlantHarvestedEvent(user, items);
        RaiseLocalEvent(ent, ref ev);
        return items;
    }

    private bool PreHarvestChecks(Entity<PlantTrayComponent> ent, EntityUid user)
    {
        var nearby = _lookup.GetEntitiesInRange(ent.Owner, 1.5f, LookupFlags.Dynamic | LookupFlags.Sundries);
        if (nearby.Count >= MaxItemsNearby)
        {
            _popup.PopupEntity(Loc.GetString("plant-harvest-too-crowded"), ent, user, PopupType.SmallCaution);
            return false;
        }

        var onTile = _lookup.GetEntitiesInRange(user, 0.5f, LookupFlags.Dynamic | LookupFlags.Sundries);
        if (onTile.Count >= MaxItemsOnTile)
        {
            _popup.PopupEntity(Loc.GetString("plant-harvest-too-crowded"), ent, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    /// <summary>Secateurs: cut a graft off a harvestable plant (once per plant).</summary>
    private bool TryCutGraft(Entity<PlantTrayComponent> ent, BotanySecateursComponent secateurs, EntityUid user)
    {
        var tray = ent.Comp;
        if (tray.Genome is not { } genome || tray.State != PlantState.Harvestable)
            return false;

        if (genome.Grafted)
        {
            _popup.PopupEntity(Loc.GetString("plant-graft-already-taken"), ent, user);
            return true;
        }

        var traitId = _proto.TryIndex(genome.Species, out var species) && species.GraftTrait is { } g ? g : BotanySystem.PerennialTrait;
        var flags = _proto.TryIndex(traitId, out var trait) ? trait.Flags : PlantGeneFlags.Removable | PlantGeneFlags.Graftable;

        var name = _botany.GetPlantName(genome);
        var graftEnt = Spawn(secateurs.GraftPrototype, Transform(ent).Coordinates);
        var graft = EnsureComp<PlantGraftComponent>(graftEnt);
        graft.Trait = new PlantTraitGene(traitId, flags);
        graft.ParentName = name;
        graft.Lifespan = genome.Lifespan;
        graft.Endurance = genome.Endurance;
        graft.Production = genome.Production;
        graft.WeedRate = genome.WeedRate;
        graft.WeedChance = genome.WeedChance;
        graft.Yield = genome.Yield;
        Dirty(graftEnt, graft);
        _botany.SetGraftName(graftEnt, name);

        genome.Grafted = true;
        AdjustHealth(ent, -5);
        _popup.PopupEntity(Loc.GetString("plant-graft-cut", ("plant", name)), ent, user);
        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Monkestation apply_graft: adds the graft's trait if possible and moves each stat two thirds of
    /// the way towards the graft's value, never lowering it.
    /// </summary>
    private bool TryApplyGraft(Entity<PlantTrayComponent> ent, Entity<PlantGraftComponent> graft, EntityUid user)
    {
        var tray = ent.Comp;
        if (tray.Genome is not { } genome)
            return false;

        var trait = graft.Comp.Trait;
        var added = false;
        if (trait != null && _botany.CanAddTrait(genome, trait.Trait))
        {
            genome.Traits.Add(trait.Clone());
            added = true;
        }

        static float Pull(float current, float target) =>
            MathF.Round(MathF.Max(current, current + 2f / 3f * (target - current)), MidpointRounding.AwayFromZero);

        genome.SetStat(PlantStat.Lifespan, Pull(genome.Lifespan, graft.Comp.Lifespan));
        genome.SetStat(PlantStat.Endurance, Pull(genome.Endurance, graft.Comp.Endurance));
        genome.SetStat(PlantStat.Production, Pull(genome.Production, graft.Comp.Production));
        genome.SetStat(PlantStat.WeedRate, Pull(genome.WeedRate, graft.Comp.WeedRate));
        genome.SetStat(PlantStat.WeedChance, Pull(genome.WeedChance, graft.Comp.WeedChance));
        genome.SetStat(PlantStat.Yield, Pull(genome.Yield, graft.Comp.Yield));

        var traitName = trait != null ? _botany.GetTraitGeneName(trait) : string.Empty;
        var plant = _botany.GetPlantName(genome);
        _popup.PopupEntity(Loc.GetString(added ? "plant-graft-applied" : "plant-graft-applied-no-trait",
            ("plant", plant), ("trait", traitName)), ent, user);

        QueueDel(graft);
        UpdateGrowthState(ent);
        Dirty(ent);
        return true;
    }

    #endregion

    #region Visuals

    private void UpdateTrayGlow(Entity<PlantTrayComponent> ent, PlantGenome genome)
    {
        PlantTraitPrototype? glow = null;
        foreach (var trait in _botany.GetTraits(genome))
        {
            if (trait.GlowColor != null)
                glow = trait;
        }

        if (glow != null)
        {
            _traits.ApplyGlow(ent, glow, genome);
        }
        else if (TryComp<PointLightComponent>(ent, out var light))
        {
            _light.SetEnabled(ent, false, light);
        }
    }

    public void UpdateAppearance(Entity<PlantTrayComponent> ent)
    {
        var tray = ent.Comp;
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        var rsi = string.Empty;
        var state = string.Empty;
        if (tray.Genome is { } genome && _proto.TryIndex(genome.Species, out var species))
        {
            rsi = species.PlantRsi.ToString();
            switch (tray.State)
            {
                case PlantState.Dead:
                    state = "dead";
                    break;
                case PlantState.Harvestable:
                    state = species.HasHarvestSprite ? "harvest" : $"stage-{species.GrowthStages}";
                    break;
                default:
                    var stage = (int) MathF.Ceiling(tray.GrowthPercent / 100f * species.GrowthStages);
                    state = $"stage-{Math.Clamp(stage, 1, species.GrowthStages)}";
                    break;
            }
        }

        _appearance.SetData(ent, PlantTrayVisuals.PlantRsi, rsi, appearance);
        _appearance.SetData(ent, PlantTrayVisuals.PlantState, state, appearance);

        if (!tray.DrawWarnings)
            return;

        var planted = tray.Genome != null && tray.State != PlantState.Dead;
        var endurance = tray.Genome?.Endurance ?? 0;
        _appearance.SetData(ent, PlantTrayVisuals.HealthLight, planted && tray.Health < endurance * 0.9f, appearance);
        _appearance.SetData(ent, PlantTrayVisuals.WaterLight, tray.WaterPercent <= 10, appearance);
        _appearance.SetData(ent, PlantTrayVisuals.NutritionLight, tray.Nutrients <= 5, appearance);
        _appearance.SetData(ent, PlantTrayVisuals.AlertLight, tray.Weeds >= 5 || tray.Pests >= 5, appearance);
        _appearance.SetData(ent, PlantTrayVisuals.HarvestLight, tray.State == PlantState.Harvestable, appearance);
    }

    #endregion
}

/// <summary>Raised on a tray after it is harvested.</summary>
[ByRefEvent]
public readonly record struct PlantHarvestedEvent(EntityUid User, List<EntityUid> Items);
