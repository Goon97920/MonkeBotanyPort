using Content.Shared.Atmos.Prototypes;
using System.Linq;
using Content.Server.Botany.Components;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Prototypes;
using Content.Shared.Botany.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Kitchen.Components;
using Content.Shared.Random.Helpers;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Botany.Systems;

/// <summary>
/// Server side of botany: creating genomes (including strange seeds), harvesting, mutations and
/// filling produce with chemicals. Ported from Monkestation's seeds.dm and grown.dm.
/// </summary>
public sealed partial class BotanySystem : SharedBotanySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PlantTraitSystem _traits = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private TransformSystem _transform = default!;

    /// <summary>Fraction of harvested items that are seeds (Monkestation seed_harvest_ratio).</summary>
    public const float SeedHarvestRatio = 0.2f;

    /// <summary>Monkestation default potency of produce that wasn't grown from a seed.</summary>
    public const float DefaultProducePotency = 50f;

    public static readonly ProtoId<ReagentPrototype> Nutriment = "Nutriment";
    public static readonly ProtoId<ReagentPrototype> Vitamin = "Vitamin";
    public static readonly ProtoId<ReagentPrototype> Wine = "Wine";

    public static readonly ProtoId<PlantTraitPrototype> PerennialTrait = "PerennialGrowth";

    /// <summary>Plant types; strange seeds may get one at random.</summary>
    private static readonly ProtoId<PlantTraitPrototype>[] PlantTypes = ["WeedAdaptation", "FungalVitality", "AlienProperties"];

    private List<ProtoId<ReagentPrototype>>? _strangeReagentPool;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SeedComponent, MapInitEvent>(OnSeedMapInit);
        SubscribeLocalEvent<ProduceComponent, MapInitEvent>(OnProduceMapInit);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ReactionPrototype>() || args.WasModified<ReagentPrototype>() || args.WasModified<GasPrototype>())
            _strangeReagentPool = null;
    }

    private void OnSeedMapInit(Entity<SeedComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Genome != null)
            return;

        ent.Comp.Genome = CreateGenome(ent.Comp.Species);
        if (ent.Comp.Genome == null)
            return;

        if (ent.Comp.RandomizeGenome)
            RandomizeGenome(ent.Comp.Genome, ent.Comp.RandomReagentBlacklist);

        Dirty(ent);
        UpdateSeedName(ent);
    }

    private void OnProduceMapInit(Entity<ProduceComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Genome != null || ent.Comp.Species is not { } species)
            return;

        // Monkestation: map-spawned produce gets its species' default seed at 50 potency.
        ent.Comp.Genome = CreateGenome(species);
        ent.Comp.Genome?.SetStat(PlantStat.Potency, DefaultProducePotency);
        Dirty(ent);
    }

    #region Genomes

    /// <summary>Builds a fresh genome with the species' default stats, chemicals and traits.</summary>
    public PlantGenome? CreateGenome(ProtoId<PlantSpeciesPrototype> speciesId)
    {
        if (!Proto.TryIndex(speciesId, out var species))
        {
            Log.Error($"Unknown plant species {speciesId}");
            return null;
        }

        var genome = new PlantGenome
        {
            Species = speciesId,
            Lifespan = species.Lifespan,
            Endurance = species.Endurance,
            Maturation = species.Maturation,
            Production = species.Production,
            Yield = species.Yield,
            Potency = species.Potency,
            WeedRate = species.WeedRate,
            WeedChance = species.WeedChance,
            HarvestAge = species.HarvestAge,
            Mutations = new List<ProtoId<PlantMutationPrototype>>(species.Mutations),
            PendingInfusions = new List<ProtoId<PlantMutationPrototype>>(species.InfusionMutations),
        };

        foreach (var traitId in species.Traits)
        {
            if (!Proto.TryIndex(traitId, out var trait))
            {
                Log.Error($"Plant species {speciesId} has unknown trait {traitId}");
                continue;
            }

            var flags = species.TraitFlags.TryGetValue(traitId, out var f) ? f : trait.Flags;
            genome.Traits.Add(new PlantTraitGene(traitId, flags));
        }

        foreach (var (reagent, rate) in species.Chemicals)
        {
            genome.Chemicals.Add(new PlantChemicalGene(reagent, rate));
        }

        return genome;
    }

    /// <summary>
    /// Strange seeds (Monkestation /obj/item/seeds/random): random stats, a 60% chance of 1-3 random
    /// chemicals, a 50% chance of 1-2 random traits and a 35% chance of a plant type.
    /// </summary>
    public void RandomizeGenome(PlantGenome genome, IReadOnlyCollection<ProtoId<ReagentPrototype>> reagentBlacklist)
    {
        genome.SetStat(PlantStat.Lifespan, _random.Next(25, 61));
        genome.SetStat(PlantStat.Endurance, _random.Next(15, 36));
        genome.SetStat(PlantStat.Production, _random.Next(2, 11));
        genome.SetStat(PlantStat.Yield, _random.Next(1, 11));
        genome.SetStat(PlantStat.Potency, _random.Next(10, 36));
        genome.SetStat(PlantStat.WeedRate, _random.Next(1, 11));
        genome.SetStat(PlantStat.WeedChance, _random.Next(5, 101));
        genome.Maturation = _random.Next(6, 13);

        if (_random.Prob(0.6f))
            AddRandomChemicals(genome, 1, 3, reagentBlacklist);
        if (_random.Prob(0.5f))
            AddRandomTraits(genome, 1, 2);
        if (_random.Prob(0.35f))
            AddRandomPlantType(genome);
    }

    public void AddRandomChemicals(PlantGenome genome, int min, int max, IReadOnlyCollection<ProtoId<ReagentPrototype>> blacklist)
    {
        var pool = GetStrangeReagentPool();
        if (pool.Count == 0)
            return;

        var count = _random.Next(min, max + 1);
        for (var i = 0; i < count; i++)
        {
            ProtoId<ReagentPrototype> reagent;
            var tries = 0;
            do
            {
                reagent = _random.Pick(pool);
            } while (blacklist.Contains(reagent) && ++tries < 100);

            if (blacklist.Contains(reagent))
                continue;

            var rate = _random.Next(4, 16) * 0.01f;
            AddOrUpgradeChemical(genome, new PlantChemicalGene(reagent, rate));
        }
    }

    public void AddRandomTraits(PlantGenome genome, int min, int max)
    {
        // Like Monkestation, pick from every trait and only keep mutatable ones,
        // which makes immutable traits "eat" some of the rolls.
        var all = Proto.EnumeratePrototypes<PlantTraitPrototype>().ToList();
        if (all.Count == 0)
            return;

        var count = _random.Next(min, max + 1);
        for (var i = 0; i < count; i++)
        {
            var trait = _random.Pick(all);
            if ((trait.Flags & PlantGeneFlags.Mutatable) == 0 || !trait.RandomPool)
                continue;
            if (!CanAddTrait(genome, trait.ID))
                continue;

            genome.Traits.Add(new PlantTraitGene(trait.ID, trait.Flags));
        }
    }

    public void AddRandomPlantType(PlantGenome genome)
    {
        var type = _random.Pick(PlantTypes);
        if (!Proto.TryIndex(type, out var trait) || !CanAddTrait(genome, type))
            return;

        genome.Traits.Add(new PlantTraitGene(type, trait.Flags));
    }

    /// <summary>
    /// Reagents strange seeds can roll: every reagent produced by, or used in, a chemical reaction,
    /// plus the reagent form of every gas (gas condensers make those).
    /// This keeps admin-only and otherwise unobtainable reagents out of the pool.
    /// </summary>
    public IReadOnlyList<ProtoId<ReagentPrototype>> GetStrangeReagentPool()
    {
        if (_strangeReagentPool != null)
            return _strangeReagentPool;

        var set = new HashSet<ProtoId<ReagentPrototype>>();
        foreach (var reaction in Proto.EnumeratePrototypes<ReactionPrototype>())
        {
            foreach (var product in reaction.Products.Keys)
                set.Add(product);
            foreach (var reactant in reaction.Reactants.Keys)
                set.Add(reactant);
        }

        foreach (var gas in Proto.EnumeratePrototypes<GasPrototype>())
        {
            if (gas.Reagent is { } reagent)
                set.Add(reagent);
        }

        set.RemoveWhere(id => !Proto.TryIndex(id, out var r) || r.Abstract);
        _strangeReagentPool = set.OrderBy(r => r.Id).ToList();
        return _strangeReagentPool;
    }

    public void SetDiskName(EntityUid disk, PlantDiskGene gene)
    {
        _metaData.SetEntityName(disk, Loc.GetString("plant-disk-name", ("gene", GetDiskGeneName(gene))));
    }

    public void SetGraftName(EntityUid graft, string plantName)
    {
        _metaData.SetEntityName(graft, Loc.GetString("plant-graft-name", ("plant", plantName)));
    }

    public void UpdateSeedName(Entity<SeedComponent> ent)
    {
        if (ent.Comp.Genome is not { } genome || !Proto.TryIndex(genome.Species, out var species))
            return;

        _metaData.SetEntityName(ent, Loc.GetString("botany-seed-packet-name",
            ("seedName", GetPlantName(genome)),
            ("seedNoun", Loc.GetString(species.Noun))));
    }

    #endregion

    #region Seeds and produce

    /// <summary>
    /// Spawns a seed packet carrying a copy of the genome.
    /// </summary>
    public EntityUid? SpawnSeed(PlantGenome genome, EntityCoordinates coords, EntityUid? user = null)
    {
        EntProtoId packet;
        if (genome.IsSpliced)
            packet = SplicedSeedPrototype;
        else if (Proto.TryIndex(genome.Species, out var species))
            packet = species.PacketPrototype;
        else
            return null;

        var seed = Spawn(packet, coords);
        var comp = EnsureComp<SeedComponent>(seed);
        comp.Species = genome.Species;
        comp.Genome = genome.Clone();
        comp.RandomizeGenome = false;
        Dirty(seed, comp);
        UpdateSeedName((seed, comp));

        if (user != null)
            _hands.TryPickupAnyHand(user.Value, seed);

        return seed;
    }

    public static readonly EntProtoId SplicedSeedPrototype = "SplicedSeeds";

    /// <summary>Spawns a fresh, default seed of a species (used for mutated seeds).</summary>
    public EntityUid? SpawnDefaultSeed(ProtoId<PlantSpeciesPrototype> speciesId, EntityCoordinates coords)
    {
        var genome = CreateGenome(speciesId);
        return genome == null ? null : SpawnSeed(genome, coords);
    }

    /// <summary>
    /// Spawns one produce item grown from a genome and fills it with chemicals.
    /// </summary>
    public EntityUid SpawnProduce(EntProtoId product, PlantGenome genome, EntityCoordinates coords)
    {
        var entity = Spawn(product, coords);
        var produce = EnsureComp<ProduceComponent>(entity);
        produce.Species = genome.Species;
        produce.Genome = genome.Clone();
        Dirty(entity, produce);

        if (genome.CustomName != null)
            _metaData.SetEntityName(entity, genome.CustomName);

        _traits.ApplyProduceTraits((entity, produce));
        FillProduceChemicals((entity, produce));
        _appearance.SetData(entity, ProduceVisuals.Potency, genome.Potency);
        _transform.SetLocalRotation(entity, _random.NextAngle());
        return entity;
    }

    /// <summary>Spawns default produce of a species (used for mutated produce).</summary>
    public EntityUid? SpawnDefaultProduce(ProtoId<PlantSpeciesPrototype> speciesId, EntityCoordinates coords)
    {
        if (!Proto.TryIndex(speciesId, out var species) || species.ProductPrototypes.Count == 0)
            return null;

        var genome = CreateGenome(speciesId);
        if (genome == null)
            return null;

        genome.SetStat(PlantStat.Potency, DefaultProducePotency);
        return SpawnProduce(_random.Pick(species.ProductPrototypes), genome, coords);
    }

    /// <summary>
    /// Monkestation prepare_result: each chemical gets volume * potency/100 * rate units, scaled down
    /// so the total fits in the produce, with at least 1u of each. Then juicing, distilling,
    /// heating and cooling are applied.
    /// </summary>
    public void FillProduceChemicals(Entity<ProduceComponent> ent)
    {
        if (ent.Comp.Genome is not { } genome)
            return;

        // Mobs (killer tomatoes) and the like may not have a food solution.
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.TargetSolution, out var solutionEnt, out _)
            || solutionEnt is not { } soln)
            return;

        var (baseVolume, multiplier) = GetVolumeModifiers(genome, ent.Comp.BaseVolume);
        var volume = Math.Max(1f, baseVolume * multiplier);

        _solution.RemoveAllSolution(soln);
        soln.Comp.Solution.CanReact = !AnyTrait(genome, t => t.NoReact);

        var potencyRate = genome.Potency / 100f;
        var total = 0f;
        foreach (var chem in genome.Chemicals)
        {
            total += chem.Rate * potencyRate;
        }

        var fitting = total > 0 ? MathF.Min(1f / total, 1f) : 1f;
        Proto.TryIndex(genome.Species, out var species);
        var amounts = new List<(ProtoId<ReagentPrototype> Reagent, float Amount)>();
        var sum = 0f;
        foreach (var chem in genome.Chemicals)
        {
            var amount = MathF.Max(1f, MathF.Round(volume * potencyRate * chem.Rate * fitting, MidpointRounding.AwayFromZero));

            // Cooking reagents never drop below SS14's old amounts.
            if (species != null && species.CookingMinimums.TryGetValue(chem.Reagent, out var minimum))
                amount = MathF.Max(amount, minimum.GetAmount(genome.Potency));

            amounts.Add((chem.Reagent, amount));
            sum += amount;
        }

        // The "at least 1u" rule can push the total slightly past the volume; make room for it.
        _solution.SetCapacity(soln, FixedPoint2.New(Math.Max(volume, sum)));
        foreach (var (reagent, amount) in amounts)
        {
            _solution.TryAddReagent(soln, reagent, FixedPoint2.New(amount), out _);
        }

        ApplyContentTraits(ent, soln, genome);
        _solution.UpdateChemicals(soln, needsReactionsProcessing: soln.Comp.Solution.CanReact);
    }

    private void ApplyContentTraits(Entity<ProduceComponent> ent, Entity<Content.Shared.Chemistry.Components.SolutionComponent> soln, PlantGenome genome)
    {
        var solution = soln.Comp.Solution;

        // Auto-juicing: nutriment and vitamin become the produce's juice (one entry per juice type).
        if (AnyTrait(genome, t => t.Juicing)
            && TryComp<ExtractableComponent>(ent, out var extractable)
            && extractable.JuiceSolution is { } juice
            && juice.Contents.Count > 0)
        {
            var nutriment = solution.GetTotalPrototypeQuantity(Nutriment);
            solution.RemoveReagent(Nutriment, FixedPoint2.MaxValue);
            solution.RemoveReagent(Vitamin, FixedPoint2.MaxValue);
            if (nutriment > 0)
            {
                foreach (var reagent in juice.Contents)
                {
                    solution.AddReagent(reagent.Reagent.Prototype, nutriment);
                }
            }
        }
        // Auto-distilling: nutriment and vitamin ferment into wine.
        else if (AnyTrait(genome, t => t.Distilling))
        {
            var fermentable = solution.GetTotalPrototypeQuantity(Nutriment) + solution.GetTotalPrototypeQuantity(Vitamin);
            solution.RemoveReagent(Nutriment, FixedPoint2.MaxValue);
            solution.RemoveReagent(Vitamin, FixedPoint2.MaxValue);
            if (fermentable > 0)
                solution.AddReagent(Wine, fermentable);
        }

        // Exo/endothermic: burn nutriment to change the temperature of the rest.
        var heat = 0f;
        var cool = 0f;
        foreach (var trait in GetTraits(genome))
        {
            heat = MathF.Max(heat, trait.HeatPerNutriment);
            cool = MathF.Max(cool, trait.CoolPerNutriment);
        }

        if (heat > 0 || cool > 0)
        {
            var nutriment = solution.GetTotalPrototypeQuantity(Nutriment).Float();
            solution.RemoveReagent(Nutriment, FixedPoint2.MaxValue);
            var temp = heat > 0
                ? MathF.Min(1000f, solution.Temperature + nutriment * heat)
                : MathF.Max(3f, solution.Temperature - nutriment * cool);
            solution.Temperature = temp;
        }
    }

    /// <summary>
    /// Turns produce (or anything with a genome) into seeds, Monkestation seedify:
    /// rand(1,4) * multiplier + 1 copies. The whole batch spawns at exactly <paramref name="coords"/>,
    /// so it forms one pile.
    /// </summary>
    public List<EntityUid> Seedify(PlantGenome genome, EntityCoordinates coords, int multiplier)
    {
        return SeedifyExact(genome, coords, _random.Next(1, 5) * Math.Max(1, multiplier));
    }

    /// <summary>
    /// Monkestation seedify with a fixed t_max: t_max + 1 seeds (the portable seed extractor uses 1).
    /// </summary>
    public List<EntityUid> SeedifyExact(PlantGenome genome, EntityCoordinates coords, int tMax)
    {
        var result = new List<EntityUid>();
        for (var i = 0; i <= tMax; i++)
        {
            if (SpawnSeed(genome, coords) is { } seed)
                result.Add(seed);
        }

        return result;
    }

    #endregion

    #region Harvest

    public bool HasViableMutations(PlantGenome genome) => GetViableMutations(genome).Any();

    public IEnumerable<PlantMutationPrototype> GetViableMutations(PlantGenome genome)
    {
        foreach (var id in genome.Mutations)
        {
            if (Proto.TryIndex(id, out var mutation) && mutation.IsViable(genome))
                yield return mutation;
        }
    }

    private PlantMutationPrototype? PickViableMutation(PlantGenome genome)
    {
        var viable = GetViableMutations(genome).ToList();
        return viable.Count == 0 ? null : _random.Pick(viable);
    }

    /// <summary>Monkestation qp_sigmoid: (max * x) / (midpoint + |x|).</summary>
    public static float QpSigmoid(float midpoint, float max, float x) => max * x / (midpoint + MathF.Abs(x));

    /// <summary>
    /// Number of items a harvest gives: yield * 0.1 below 100 yield, then approaching 20.
    /// At least one for any yield of 1 or more.
    /// </summary>
    public static int GetHarvestAmount(float yield)
    {
        int amount;
        if (yield >= 100f)
            amount = (int)MathF.Floor(QpSigmoid(100f, 20f, yield));
        else
            amount = (int)MathF.Floor(yield * 0.1f);

        // Unlike Monkestation, any plant with a yield of 1 or more harvests at least one thing.
        return yield >= 1f ? Math.Max(amount, 1) : amount;
    }

    /// <summary>
    /// Harvests a genome (Monkestation seeds/harvest). Seeds are 20% of the items; while a mutation
    /// is viable, 65% of seeds and 10% of produce come out as the mutated species.
    /// </summary>
    public List<EntityUid> Harvest(PlantGenome genome, EntityCoordinates coords)
    {
        if (genome.IsSpliced)
            return HarvestSpliced(genome, coords);

        var result = new List<EntityUid>();
        if (!Proto.TryIndex(genome.Species, out var species))
            return result;

        var seedless = IsSeedless(genome);
        var yield = genome.Yield;
        var harvestAmount = GetHarvestAmount(yield);
        var maxSeeds = 0;
        if (!seedless)
        {
            maxSeeds = (int)MathF.Floor(harvestAmount * SeedHarvestRatio);
            if (yield < 100f && yield > 0 && maxSeeds == 0 && _random.Prob(0.5f))
                maxSeeds = 1;
        }

        var harvested = 0;
        var seeds = 0;
        var seedCoords = coords;
        while (harvested < harvestAmount)
        {
            while (seeds < maxSeeds)
            {
                EntityUid? seed = null;
                if (_random.Prob(0.65f) && PickViableMutation(genome) is { } mutation)
                    seed = SpawnDefaultSeed(mutation.Result, seedCoords);
                else
                    seed = SpawnSeed(genome, seedCoords);

                if (seed != null)
                    result.Add(seed.Value);
                harvested++;
                seeds++;
            }

            EntityUid? produce = null;
            if (_random.Prob(0.1f) && PickViableMutation(genome) is { } produceMutation)
                produce = SpawnDefaultProduce(produceMutation.Result, coords);
            else if (species.ProductPrototypes.Count > 0)
                produce = SpawnProduce(_random.Pick(species.ProductPrototypes), genome, coords);

            if (produce != null)
                result.Add(produce.Value);
            harvested++;
        }

        return result;
    }

    /// <summary>Spliced harvest: cycles through the parents' products, ~10% seeds.</summary>
    private List<EntityUid> HarvestSpliced(PlantGenome genome, EntityCoordinates coords)
    {
        var result = new List<EntityUid>();
        var seedless = IsSeedless(genome);
        var amount = genome.Yield;
        if (amount >= 10)
            amount = 10 + MathF.Log(1.02f) * (genome.Yield - 1);

        var count = 0;
        while (count < amount)
        {
            if (!seedless && _random.Prob(0.1f))
            {
                if (SpawnSeed(genome, coords) is { } seed)
                    result.Add(seed);
            }
            else
            {
                result.Add(SpawnProduce(_random.Pick(genome.SplicedProducts), genome, coords));
            }

            count++;
        }

        return result;
    }

    #endregion

    /// <summary>
    /// Adds a trait gained by training (tray reagents), applying its stat penalty.
    /// </summary>
    public void TrainTrait(PlantGenome genome, ProtoId<PlantTraitPrototype> traitId, float amount)
    {
        genome.TraitProgress.TryGetValue(traitId, out var progress);
        if (progress >= 100 || genome.HasTrait(traitId))
        {
            genome.TraitProgress.Remove(traitId);
            return;
        }

        progress = MathF.Min(progress + amount, 100f);
        if (progress < 100f)
        {
            genome.TraitProgress[traitId] = progress;
            return;
        }

        genome.TraitProgress.Remove(traitId);
        if (!Proto.TryIndex(traitId, out var trait) || !CanAddTrait(genome, traitId))
            return;

        genome.Traits.Add(new PlantTraitGene(traitId, trait.Flags));
        // Monkestation process_stats: adjust_yield(yield * 0.5). Yes, this raises it.
        if (trait.HalvesYieldOnGain)
            genome.AdjustStat(PlantStat.Yield, genome.Yield * 0.5f);
    }
}
