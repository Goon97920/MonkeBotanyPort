using System.Globalization;
using System.Linq;
using System.Text;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.Systems;

/// <summary>
/// Shared botany helpers: naming, gene compatibility checks and analyzer text.
/// The simulation itself runs on the server.
/// </summary>
public abstract partial class SharedBotanySystem : EntitySystem
{
    [Dependency] protected IPrototypeManager Proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SeedComponent, ExaminedEvent>(OnSeedExamined);
        SubscribeLocalEvent<ProduceComponent, ExaminedEvent>(OnProduceExamined);
        SubscribeLocalEvent<PlantGeneDiskComponent, ExaminedEvent>(OnDiskExamined);
        SubscribeLocalEvent<PlantGraftComponent, ExaminedEvent>(OnGraftExamined);
        SubscribeLocalEvent<PlantTrayComponent, ExaminedEvent>(OnTrayExamined);
    }

    private void OnTrayExamined(Entity<PlantTrayComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var tray = ent.Comp;
        using (args.PushGroup(nameof(PlantTrayComponent)))
        {
            if (tray.Genome is { } genome)
            {
                args.PushMarkup(Loc.GetString("plant-component-something-already-growing-message", ("seedName", GetPlantName(genome))));
                switch (tray.State)
                {
                    case PlantState.Dead:
                        args.PushMarkup(Loc.GetString("plant-component-dead-plant-matter-message"));
                        break;
                    case PlantState.Harvestable:
                        args.PushMarkup(Loc.GetString("tray-component-harvestable"));
                        break;
                }
            }
            else
            {
                args.PushMarkup(Loc.GetString("tray-component-nothing-planted-message"));
            }

            args.PushMarkup(Loc.GetString("tray-component-water-level-message", ("waterLevel", (int) tray.WaterPercent)));
            args.PushMarkup(Loc.GetString("tray-component-nutrient-level-message", ("nutritionLevel", FormatStat(tray.Nutrients))));
            if (tray.SelfSustain >= 100)
                args.PushMarkup(Loc.GetString("tray-component-self-sustaining"));
            if (tray.Weeds >= 5)
                args.PushMarkup(Loc.GetString("tray-component-weed-high-level-warning"));
            if (tray.Pests >= 5)
                args.PushMarkup(Loc.GetString("tray-component-pest-high-level-warning"));
        }
    }

    private void OnSeedExamined(Entity<SeedComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Genome is not { } genome)
            return;

        using (args.PushGroup(nameof(SeedComponent)))
        {
            args.PushMarkup(Loc.GetString("seed-component-description", ("seedName", GetPlantName(genome))));
            args.PushMarkup(Loc.GetString("seed-component-plant-yield-text", ("seedYield", FormatStat(genome.Yield))));
            args.PushMarkup(Loc.GetString("seed-component-plant-potency-text", ("seedPotency", FormatStat(genome.Potency))));
        }
    }

    private void OnProduceExamined(Entity<ProduceComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Genome is not { } genome)
            return;

        using (args.PushGroup(nameof(ProduceComponent)))
        {
            foreach (var gene in genome.Traits)
            {
                if (Proto.TryIndex(gene.Trait, out var trait) && trait.Description is { } desc)
                    args.PushMarkup(Loc.GetString(desc));
            }
        }
    }

    private void OnDiskExamined(Entity<PlantGeneDiskComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(ent.Comp.Gene is { } gene
            ? Loc.GetString("plant-disk-examine-gene", ("gene", GetDiskGeneName(gene)))
            : Loc.GetString("plant-disk-examine-empty"));

        if (ent.Comp.Gene?.Stat == PlantStat.Potency)
            args.PushMarkup(Loc.GetString("plant-disk-examine-potency-note"));

        args.PushMarkup(Loc.GetString(ent.Comp.ReadOnly ? "plant-disk-examine-protected" : "plant-disk-examine-unprotected"));
    }

    private void OnGraftExamined(Entity<PlantGraftComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Trait is not { } trait)
            return;

        args.PushMarkup(Loc.GetString("plant-graft-examine", ("trait", GetTraitGeneName(trait)), ("plant", ent.Comp.ParentName)));
    }

    #region Naming

    public string GetPlantName(PlantGenome genome)
    {
        var name = genome.CustomName;
        if (name == null)
        {
            name = Proto.TryIndex(genome.Species, out var species)
                ? Loc.GetString(species.Name)
                : genome.Species.Id;
        }

        return genome.Experimental ? Loc.GetString("plant-experimental-name", ("name", name)) : name;
    }

    public string GetReagentName(ProtoId<ReagentPrototype> reagent)
    {
        return Proto.TryIndex(reagent, out var proto) ? proto.LocalizedName : reagent.Id;
    }

    /// <summary>Monkestation format: "Fragile Nutriment production 20%".</summary>
    public string GetChemicalGeneName(PlantChemicalGene gene)
    {
        var prefix = (gene.Flags & PlantGeneFlags.Removable) == 0 ? Loc.GetString("plant-gene-fragile") + " " : string.Empty;
        return Loc.GetString("plant-gene-chemical",
            ("prefix", prefix),
            ("reagent", GetReagentName(gene.Reagent)),
            ("rate", FormatStat(gene.Rate * 100)));
    }

    /// <summary>Monkestation format: "Immutable Dank Vesicles", "Essential Fungal Vitality".</summary>
    public string GetTraitGeneName(PlantTraitGene gene)
    {
        var name = Proto.TryIndex(gene.Trait, out var trait) ? Loc.GetString(trait.Name) : gene.Trait.Id;
        if ((gene.Flags & PlantGeneFlags.Removable) != 0)
            return name;

        var prefix = (gene.Flags & PlantGeneFlags.Graftable) == 0
            ? Loc.GetString("plant-gene-immutable")
            : Loc.GetString("plant-gene-essential");
        return $"{prefix} {name}";
    }

    public string GetStatName(PlantStat stat) => Loc.GetString($"plant-stat-{stat.ToString().ToLowerInvariant()}");

    public string GetDiskGeneName(PlantDiskGene gene)
    {
        if (gene.Stat is { } stat)
            return $"{GetStatName(stat)} {FormatStat(gene.StatValue)}";
        if (gene.Chemical is { } chem)
            return GetChemicalGeneName(chem);
        if (gene.Trait is { } trait)
            return GetTraitGeneName(trait);
        return Loc.GetString("plant-gene-unknown");
    }

    public static string FormatStat(float value)
    {
        if (float.IsPositiveInfinity(value) || value >= PlantGenome.MaxStat)
            return "∞";
        if (MathF.Abs(value) >= 1e7f)
            return value.ToString("0.###e0", CultureInfo.InvariantCulture);
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    #endregion

    #region Gene compatibility

    /// <summary>
    /// Whether a trait can be added to the genome (Monkestation can_add): not on a sample, not
    /// blacklisted for the species, not already present and not sharing a group with another trait.
    /// </summary>
    public bool CanAddTrait(PlantGenome genome, ProtoId<PlantTraitPrototype> traitId)
    {
        if (!Proto.TryIndex(traitId, out var trait))
            return false;

        if (trait.SpeciesBlacklist.Contains(genome.Species))
            return false;

        foreach (var existing in genome.Traits)
        {
            if (existing.Trait == traitId)
                return false;

            if (trait.Groups.Count == 0 || !Proto.TryIndex(existing.Trait, out var other))
                continue;

            if (trait.Groups.Any(g => other.Groups.Contains(g)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Whether a chemical gene can be inserted: either the plant doesn't make the reagent yet, or
    /// the new gene's rate is higher (it then upgrades the existing gene).
    /// </summary>
    public bool CanAddChemical(PlantGenome genome, PlantChemicalGene gene)
    {
        var existing = genome.GetChemical(gene.Reagent);
        return existing == null || existing.Rate < gene.Rate;
    }

    /// <summary>Adds a chemical gene, or raises the rate of an existing gene for the same reagent.</summary>
    public void AddOrUpgradeChemical(PlantGenome genome, PlantChemicalGene gene)
    {
        var existing = genome.GetChemical(gene.Reagent);
        if (existing == null)
        {
            genome.Chemicals.Add(gene.Clone());
            return;
        }

        if (gene.Rate > existing.Rate)
            existing.Rate = gene.Rate;
    }

    public bool IsPerennial(PlantGenome genome) => AnyTrait(genome, t => t.Perennial);

    public bool IsSeedless(PlantGenome genome) => AnyTrait(genome, t => t.Seedless);

    public bool AnyTrait(PlantGenome genome, Func<PlantTraitPrototype, bool> predicate)
    {
        foreach (var gene in genome.Traits)
        {
            if (Proto.TryIndex(gene.Trait, out var trait) && predicate(trait))
                return true;
        }

        return false;
    }

    public IEnumerable<PlantTraitPrototype> GetTraits(PlantGenome genome)
    {
        foreach (var gene in genome.Traits)
        {
            if (Proto.TryIndex(gene.Trait, out var trait))
                yield return trait;
        }
    }

    #endregion

    #region Report text

    /// <summary>Monkestation get_analyzer_text_traits: stats and traits.</summary>
    public void AppendStatsReport(StringBuilder sb, PlantGenome genome)
    {
        sb.AppendLine(Loc.GetString("plant-analyzer-stats-header"));
        foreach (var stat in Enum.GetValues<PlantStat>())
        {
            sb.AppendLine(Loc.GetString("plant-analyzer-stat-line",
                ("stat", GetStatName(stat)),
                ("value", FormatStat(genome.GetStat(stat)))));
        }

        sb.AppendLine(Loc.GetString("plant-analyzer-harvest-age", ("age", genome.HarvestAge)));

        if (genome.Traits.Count > 0)
        {
            sb.AppendLine(Loc.GetString("plant-analyzer-traits-header"));
            foreach (var trait in genome.Traits)
            {
                sb.AppendLine(Loc.GetString("plant-analyzer-list-line", ("text", GetTraitGeneName(trait))));
            }
        }

        if (genome.TraitProgress.Count > 0)
        {
            sb.AppendLine(Loc.GetString("plant-analyzer-training-header"));
            foreach (var (traitId, progress) in genome.TraitProgress)
            {
                var name = Proto.TryIndex(traitId, out var t) ? Loc.GetString(t.Name) : traitId.Id;
                sb.AppendLine(Loc.GetString("plant-analyzer-training-line", ("trait", name), ("progress", FormatStat(progress))));
            }
        }

        if (genome.IsSpliced)
            sb.AppendLine(Loc.GetString("plant-analyzer-spliced"));
    }

    /// <summary>Monkestation get_analyzer_text_chem: chemical genes plus volume info.</summary>
    public void AppendChemicalsReport(StringBuilder sb, PlantGenome genome)
    {
        sb.AppendLine(Loc.GetString("plant-analyzer-chems-header"));
        if (genome.Chemicals.Count == 0)
            sb.AppendLine(Loc.GetString("plant-analyzer-no-chems"));

        foreach (var chem in genome.Chemicals)
        {
            sb.AppendLine(Loc.GetString("plant-analyzer-list-line", ("text", GetChemicalGeneName(chem))));
        }

        var (baseVolume, multiplier) = GetVolumeModifiers(genome, 100f);
        sb.AppendLine(Loc.GetString("plant-analyzer-volume", ("volume", FormatStat(baseVolume * multiplier))));
    }

    /// <summary>
    /// The produce's base volume (overridden by volume traits) and the volume multiplier (densified).
    /// </summary>
    public (float BaseVolume, float Multiplier) GetVolumeModifiers(PlantGenome genome, float defaultVolume)
    {
        var baseVolume = defaultVolume;
        var multiplier = 1f;
        foreach (var trait in GetTraits(genome))
        {
            if (trait.BaseVolume is { } v)
                baseVolume = v;
            multiplier *= trait.VolumeMultiplier;
        }

        return (baseVolume, multiplier);
    }

    #endregion
}
