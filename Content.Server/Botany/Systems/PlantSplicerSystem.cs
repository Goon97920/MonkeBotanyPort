using System.Linq;
using System.Text;
using Content.Server.Power.EntitySystems;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Botany.Systems;

/// <summary>
/// Monkestation's seed splicer (machines/splicer.dm).
/// <para>Splice: averages two seeds' stats into a hybrid that keeps the first seed's traits,
/// combines both seeds' chemicals and harvests either parent's produce.</para>
/// <para>Infuse: the beaker's reagents change the first seed's stats; 100u or more of a reagent
/// adds it as a chemical gene (3-25%). Every 10u of liquid does 1 infusion damage and the seed
/// is destroyed at 100. Infusing certain reagents unlocks infusion mutations.</para>
/// </summary>
public sealed partial class PlantSplicerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantSplicerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<PlantSplicerComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<PlantSplicerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<PlantSplicerComponent, PlantSplicerSpliceMessage>(OnSplice);
        SubscribeLocalEvent<PlantSplicerComponent, PlantSplicerInfuseMessage>(OnInfuse);
    }

    private void OnInserted(Entity<PlantSplicerComponent> ent, ref EntInsertedIntoContainerMessage args) => UpdateUi(ent);
    private void OnRemoved(Entity<PlantSplicerComponent> ent, ref EntRemovedFromContainerMessage args) => UpdateUi(ent);
    private void OnUiOpened(Entity<PlantSplicerComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    private Entity<SeedComponent>? GetSeed(EntityUid machine, string slot)
    {
        return _itemSlots.GetItemOrNull(machine, slot) is { } item && TryComp<SeedComponent>(item, out var seed) && seed.Genome != null
            ? (item, seed)
            : null;
    }

    #region Splice

    private void OnSplice(Entity<PlantSplicerComponent> ent, ref PlantSplicerSpliceMessage args)
    {
        if (!_power.IsPowered(ent.Owner)
            || GetSeed(ent.Owner, ent.Comp.FirstSeedSlotId) is not { } first
            || GetSeed(ent.Owner, ent.Comp.SecondSeedSlotId) is not { } second)
            return;

        var genome = Splice(first.Comp.Genome!, second.Comp.Genome!);
        QueueDel(first);
        QueueDel(second);

        var seed = _botany.SpawnSeed(genome, Transform(ent).Coordinates);
        if (seed != null)
            _hands.TryPickupAnyHand(args.Actor, seed.Value);

        _popup.PopupEntity(Loc.GetString("plant-splicer-spliced", ("name", _botany.GetPlantName(genome))), ent, args.Actor);
        UpdateUi(ent);
    }

    public PlantGenome Splice(PlantGenome a, PlantGenome b)
    {
        var genome = new PlantGenome
        {
            Species = a.Species,
            HarvestAge = 120,
        };

        genome.SetStat(PlantStat.Potency, (a.Potency + b.Potency) * 0.5f);
        genome.SetStat(PlantStat.Yield, (a.Yield + b.Yield) * 0.5f);
        genome.SetStat(PlantStat.Production, (a.Production + b.Production) * 0.5f);
        genome.SetStat(PlantStat.Lifespan, (a.Lifespan + b.Lifespan) * 0.5f);
        genome.SetStat(PlantStat.Endurance, (a.Endurance + b.Endurance) * 0.5f);
        genome.SetStat(PlantStat.WeedChance, (a.WeedChance + b.WeedChance) * 0.5f);
        genome.SetStat(PlantStat.WeedRate, (a.WeedRate + b.WeedRate) * 0.5f);
        genome.SetStat(PlantStat.Maturation, (a.Maturation + b.Maturation) * 0.5f);

        // Chemicals: the first seed's, plus the second's. Shared reagents add up between different plants.
        var nameA = _botany.GetPlantName(a);
        var nameB = _botany.GetPlantName(b);
        foreach (var chem in a.Chemicals)
        {
            genome.Chemicals.Add(new PlantChemicalGene(chem.Reagent, chem.Rate));
        }

        foreach (var chem in b.Chemicals)
        {
            var existing = genome.GetChemical(chem.Reagent);
            if (existing == null)
                genome.Chemicals.Add(new PlantChemicalGene(chem.Reagent, chem.Rate));
            else if (nameA != nameB)
                existing.Rate += chem.Rate;
        }

        // Products: each parent's produce, or its whole list if it was already spliced.
        genome.SplicedProducts.AddRange(GetProducts(a));
        foreach (var product in GetProducts(b))
        {
            if (!genome.SplicedProducts.Contains(product))
                genome.SplicedProducts.Add(product);
        }

        // Traits: fresh copies of the first seed's traits.
        foreach (var trait in a.Traits)
        {
            if (!_proto.TryIndex(trait.Trait, out var proto) || !_botany.CanAddTrait(genome, trait.Trait))
                continue;
            genome.Traits.Add(new PlantTraitGene(trait.Trait, proto.Flags));
        }

        genome.CustomName = SpliceName(nameA, nameB);
        return genome;
    }

    private IEnumerable<EntProtoId> GetProducts(PlantGenome genome)
    {
        if (genome.IsSpliced)
            return genome.SplicedProducts;
        if (_proto.TryIndex(genome.Species, out var species))
            return species.ProductPrototypes;
        return Array.Empty<EntProtoId>();
    }

    /// <summary>Monkestation: the first ~70% of the first name plus the last ~60% of the second.</summary>
    public static string SpliceName(string a, string b)
    {
        var aEnd = Math.Clamp((int) MathF.Round(a.Length * 0.70f + 1), 0, a.Length);
        var bStart = Math.Clamp((int) MathF.Round(b.Length * 0.40f), 0, b.Length);
        var part1 = a[..aEnd];
        var part2 = b[bStart..];
        if (part1.Length > 49)
            part1 = part1[..49];
        if (part2.Length > 50)
            part2 = part2[^50..];
        return part1 + part2;
    }

    #endregion

    #region Infuse

    private void OnInfuse(Entity<PlantSplicerComponent> ent, ref PlantSplicerInfuseMessage args)
    {
        if (!_power.IsPowered(ent.Owner)
            || GetSeed(ent.Owner, ent.Comp.FirstSeedSlotId) is not { } seed
            || _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.BeakerSlotId) is not { } beaker
            || !_solution.TryGetFitsInDispenser(beaker, out var beakerSoln, out var beakerSolution))
            return;

        var genome = seed.Comp.Genome!;
        if (beakerSolution.Volume <= 0)
        {
            _popup.PopupEntity(Loc.GetString("plant-splicer-beaker-empty"), ent, args.Actor);
            return;
        }

        var damage = beakerSolution.Volume.Float() * ent.Comp.DamagePerUnit;
        genome.InfusionDamage += MathF.Min(damage, 100 - genome.InfusionDamage);
        if (genome.InfusionDamage >= 100)
        {
            _popup.PopupEntity(Loc.GetString("plant-splicer-seed-destroyed", ("seed", seed.Owner)), ent, args.Actor, PopupType.MediumCaution);
            QueueDel(seed);
            _solution.RemoveAllSolution(beakerSoln.Value);
            UpdateUi(ent);
            return;
        }

        // Stat changes. Monkestation merges each reagent's rolls into one list, so a later
        // reagent's roll for a stat replaces an earlier one rather than adding to it.
        var changes = new Dictionary<PlantStat, float>();
        foreach (var reagent in beakerSolution.Contents)
        {
            if (!ent.Comp.Infusions.TryGetValue(reagent.Reagent.Prototype, out var profile))
                continue;

            var units = reagent.Quantity.Float();
            foreach (var (stat, range) in profile)
            {
                changes[stat] = units * range.Roll(_random);
            }
        }

        foreach (var (stat, change) in changes)
        {
            genome.AdjustStat(stat, change);
        }

        // Chemical genes from 100u+ of an obtainable reagent.
        var pool = _botany.GetStrangeReagentPool();
        var infused = new List<ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype>>();
        foreach (var reagent in beakerSolution.Contents)
        {
            ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> id = reagent.Reagent.Prototype;
            var name = _botany.GetReagentName(id);
            if (!pool.Contains(id))
            {
                _popup.PopupEntity(Loc.GetString("plant-splicer-cannot-infuse", ("reagent", name)), ent, args.Actor);
                continue;
            }

            if (reagent.Quantity.Float() < ent.Comp.InfusionGeneVolume)
                continue;

            var rate = _random.Next(3, 26) / 100f;
            var existing = genome.GetChemical(id);
            if (existing != null)
                existing.Rate += rate;
            else
                genome.Chemicals.Add(new PlantChemicalGene(id, rate));

            infused.Add(id);
        }

        // Infusion mutations unlocked by the reagents that were infused.
        foreach (var mutationId in genome.PendingInfusions.ToList())
        {
            if (!_proto.TryIndex(mutationId, out var mutation))
                continue;
            if (!mutation.InfusionReagents.Any(r => infused.Contains(r)))
                continue;

            genome.PendingInfusions.Remove(mutationId);
            if (!genome.Mutations.Contains(mutationId))
                genome.Mutations.Add(mutationId);
        }

        _solution.RemoveAllSolution(beakerSoln.Value);
        Dirty(seed);
        _popup.PopupEntity(Loc.GetString("plant-splicer-infused", ("seed", seed.Owner), ("count", infused.Count)), ent, args.Actor);
        UpdateUi(ent);
    }

    #endregion

    private string StatsLine(PlantGenome genome)
    {
        return Loc.GetString("plant-splicer-stats",
            ("potency", BotanySystem.FormatStat(genome.Potency)),
            ("yield", BotanySystem.FormatStat(genome.Yield)),
            ("endurance", BotanySystem.FormatStat(genome.Endurance)),
            ("lifespan", BotanySystem.FormatStat(genome.Lifespan)),
            ("maturation", BotanySystem.FormatStat(genome.Maturation)),
            ("production", BotanySystem.FormatStat(genome.Production)));
    }

    private void UpdateUi(Entity<PlantSplicerComponent> ent)
    {
        var state = new PlantSplicerState();
        if (GetSeed(ent.Owner, ent.Comp.FirstSeedSlotId) is { } first)
        {
            state.FirstSeed = Name(first);
            state.FirstSeedStats = StatsLine(first.Comp.Genome!);
            state.InfusionDamage = first.Comp.Genome!.InfusionDamage;
        }

        if (GetSeed(ent.Owner, ent.Comp.SecondSeedSlotId) is { } second)
        {
            state.SecondSeed = Name(second);
            state.SecondSeedStats = StatsLine(second.Comp.Genome!);
        }

        if (_itemSlots.GetItemOrNull(ent.Owner, ent.Comp.BeakerSlotId) is { } beaker
            && _solution.TryGetFitsInDispenser(beaker, out _, out var solution))
        {
            var sb = new StringBuilder();
            foreach (var reagent in solution.Contents)
            {
                var line = Loc.GetString("plant-analyzer-reagent-line",
                    ("amount", reagent.Quantity),
                    ("reagent", _botany.GetReagentName(reagent.Reagent.Prototype)));
                if (reagent.Quantity.Float() >= ent.Comp.InfusionGeneVolume)
                    line += " " + Loc.GetString("plant-splicer-will-infuse");
                sb.AppendLine(line);
            }

            state.Beaker = sb.Length > 0 ? sb.ToString().TrimEnd() : Loc.GetString("plant-splicer-beaker-empty");
            state.PotentialDamage = solution.Volume.Float() * ent.Comp.DamagePerUnit;
        }

        _ui.SetUiState(ent.Owner, PlantSplicerUiKey.Key, state);
    }
}
