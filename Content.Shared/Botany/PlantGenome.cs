using System.Linq;
using Content.Shared.Botany.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Botany;

/// <summary>
/// Mutability flags of a gene. A trait with none of these is shown as "Immutable".
/// Mirrors Monkestation's PLANT_GENE_REMOVABLE / MUTATABLE / GRAFTABLE.
/// </summary>
[Flags, Serializable, NetSerializable]
public enum PlantGeneFlags : byte
{
    None = 0,
    /// <summary>Can be removed with the DNA manipulator.</summary>
    Removable = 1 << 0,
    /// <summary>Can show up randomly on strange seeds.</summary>
    Mutatable = 1 << 1,
    /// <summary>Can be carried by grafts.</summary>
    Graftable = 1 << 2,
}

/// <summary>
/// The core plant stats ("core genes").
/// </summary>
[Serializable, NetSerializable]
public enum PlantStat : byte
{
    Potency,
    Yield,
    Production,
    Endurance,
    Lifespan,
    WeedRate,
    WeedChance,
    Maturation,
}

/// <summary>
/// A reagent the plant produces, as a fraction of its volume (scaled by potency).
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class PlantChemicalGene
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>Production rate. 0.2 means 20%.</summary>
    [DataField]
    public float Rate = 0.04f;

    [DataField]
    public PlantGeneFlags Flags = PlantGeneFlags.Removable;

    public PlantChemicalGene() { }

    public PlantChemicalGene(ProtoId<ReagentPrototype> reagent, float rate, PlantGeneFlags flags = PlantGeneFlags.Removable)
    {
        Reagent = reagent;
        Rate = rate;
        Flags = flags;
    }

    public PlantChemicalGene Clone() => new(Reagent, Rate, Flags);
}

/// <summary>
/// A trait carried by the plant.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class PlantTraitGene
{
    [DataField(required: true)]
    public ProtoId<PlantTraitPrototype> Trait;

    [DataField]
    public PlantGeneFlags Flags;

    public PlantTraitGene() { }

    public PlantTraitGene(ProtoId<PlantTraitPrototype> trait, PlantGeneFlags flags)
    {
        Trait = trait;
        Flags = flags;
    }

    public PlantTraitGene Clone() => new(Trait, Flags);
}

/// <summary>
/// The full genetic makeup of a plant. Seeds, growing plants and produce all carry a copy.
/// Ported from Monkestation's <c>/obj/item/seeds</c> datum.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class PlantGenome
{
    // Monkestation clamps (code/__DEFINES/botany.dm). The maxes are effectively unbounded.
    public const float MaxStat = 1e31f;
    public const float MinEndurance = 10f;
    public const float MinLifespan = 10f;
    public const float MaxWeedRate = 10f;
    public const float MaxWeedChance = 67f;
    public const float FungalMinYield = 1f;

    /// <summary>Trait whose plants have a minimum yield of 1.</summary>
    public static readonly ProtoId<PlantTraitPrototype> FungalTrait = "FungalVitality";

    [DataField(required: true)]
    public ProtoId<PlantSpeciesPrototype> Species;

    /// <summary>Overrides the species name (spliced plants, renamed plants).</summary>
    [DataField]
    public string? CustomName;

    /// <summary>Set once the DNA manipulator has edited this genome.</summary>
    [DataField]
    public bool Experimental;

    [DataField] public float Lifespan = 25;
    [DataField] public float Endurance = 15;
    [DataField] public float Maturation = 25;
    [DataField] public float Production = 25;
    [DataField] public float Yield = 30;
    [DataField] public float Potency = 10;
    [DataField] public float WeedRate = 1;
    [DataField] public float WeedChance = 5;

    [DataField]
    public int HarvestAge = 120;

    [DataField]
    public List<PlantChemicalGene> Chemicals = new();

    [DataField]
    public List<PlantTraitGene> Traits = new();

    /// <summary>Mutations that can currently trigger on harvest.</summary>
    [DataField]
    public List<ProtoId<PlantMutationPrototype>> Mutations = new();

    /// <summary>Infusion mutations not yet unlocked.</summary>
    [DataField]
    public List<ProtoId<PlantMutationPrototype>> PendingInfusions = new();

    /// <summary>Progress (0-100) towards traits being trained by tray reagents.</summary>
    [DataField]
    public Dictionary<ProtoId<PlantTraitPrototype>, float> TraitProgress = new();

    /// <summary>When non-empty this is a spliced seed and harvests cycle through these products.</summary>
    [DataField]
    public List<EntProtoId> SplicedProducts = new();

    /// <summary>Whether a graft has already been cut from this plant.</summary>
    [DataField]
    public bool Grafted;

    /// <summary>Accumulated splicer infusion damage. The seed is destroyed at 100.</summary>
    [DataField]
    public float InfusionDamage;

    public bool IsSpliced => SplicedProducts.Count > 0;

    public bool HasTrait(ProtoId<PlantTraitPrototype> trait)
    {
        foreach (var t in Traits)
        {
            if (t.Trait == trait)
                return true;
        }

        return false;
    }

    public PlantChemicalGene? GetChemical(ProtoId<ReagentPrototype> reagent)
    {
        foreach (var c in Chemicals)
        {
            if (c.Reagent == reagent)
                return c;
        }

        return null;
    }

    public float GetStat(PlantStat stat) => stat switch
    {
        PlantStat.Potency => Potency,
        PlantStat.Yield => Yield,
        PlantStat.Production => Production,
        PlantStat.Endurance => Endurance,
        PlantStat.Lifespan => Lifespan,
        PlantStat.WeedRate => WeedRate,
        PlantStat.WeedChance => WeedChance,
        PlantStat.Maturation => Maturation,
        _ => 0,
    };

    /// <summary>
    /// Sets a stat, applying Monkestation's clamps.
    /// </summary>
    public void SetStat(PlantStat stat, float value)
    {
        if (float.IsNaN(value))
            return;

        switch (stat)
        {
            case PlantStat.Potency:
                Potency = Math.Clamp(value, 0, MaxStat);
                break;
            case PlantStat.Yield:
                var min = HasTrait(FungalTrait) ? FungalMinYield : 0;
                Yield = Math.Clamp(value, min, MaxStat);
                break;
            case PlantStat.Production:
                Production = Math.Clamp(value, 1, MaxStat);
                break;
            case PlantStat.Endurance:
                Endurance = Math.Clamp(value, MinEndurance, MaxStat);
                break;
            case PlantStat.Lifespan:
                Lifespan = Math.Clamp(value, MinLifespan, MaxStat);
                break;
            case PlantStat.WeedRate:
                WeedRate = Math.Clamp(value, 0, MaxWeedRate);
                break;
            case PlantStat.WeedChance:
                WeedChance = Math.Clamp(value, 0, MaxWeedChance);
                break;
            case PlantStat.Maturation:
                // Monkestation leaves maturation unclamped.
                Maturation = Math.Clamp(value, -MaxStat, MaxStat);
                break;
        }
    }

    public void AdjustStat(PlantStat stat, float amount)
    {
        if (amount == 0)
            return;

        SetStat(stat, GetStat(stat) + amount);
    }

    /// <summary>
    /// Deep copy, used whenever seeds are copied (harvest, seed extraction, produce).
    /// </summary>
    public PlantGenome Clone()
    {
        return new PlantGenome
        {
            Species = Species,
            CustomName = CustomName,
            Experimental = Experimental,
            Lifespan = Lifespan,
            Endurance = Endurance,
            Maturation = Maturation,
            Production = Production,
            Yield = Yield,
            Potency = Potency,
            WeedRate = WeedRate,
            WeedChance = WeedChance,
            HarvestAge = HarvestAge,
            Chemicals = Chemicals.Select(c => c.Clone()).ToList(),
            Traits = Traits.Select(t => t.Clone()).ToList(),
            Mutations = new List<ProtoId<PlantMutationPrototype>>(Mutations),
            PendingInfusions = new List<ProtoId<PlantMutationPrototype>>(PendingInfusions),
            TraitProgress = new Dictionary<ProtoId<PlantTraitPrototype>, float>(TraitProgress),
            SplicedProducts = new List<EntProtoId>(SplicedProducts),
            Grafted = Grafted,
            InfusionDamage = InfusionDamage,
        };
    }
}

/// <summary>
/// What a plant data disk can hold: one core stat, chemical or trait gene.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class PlantDiskGene
{
    /// <summary>Set when the disk holds a core stat gene.</summary>
    [DataField]
    public PlantStat? Stat;

    [DataField]
    public float StatValue;

    [DataField]
    public PlantChemicalGene? Chemical;

    [DataField]
    public PlantTraitGene? Trait;

    public PlantDiskGene Clone() => new()
    {
        Stat = Stat,
        StatValue = StatValue,
        Chemical = Chemical?.Clone(),
        Trait = Trait?.Clone(),
    };
}
