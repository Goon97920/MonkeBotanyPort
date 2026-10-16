using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Botany.Prototypes;

/// <summary>
/// A plant species. Holds the default genome that freshly made seeds of this species start with,
/// plus everything needed to grow and harvest it (sprites, produce, seed packet).
/// Ported from Monkestation's <c>/obj/item/seeds</c> definitions.
/// </summary>
[Prototype("plantSpecies")]
public sealed partial class PlantSpeciesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Display name of the plant, e.g. "watermelon".</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Noun used for the seed packet, e.g. "seeds" or "spores".</summary>
    [DataField]
    public LocId Noun = "seeds-noun-seeds";

    /// <summary>Seed packet entity spawned when seeds of this species are made.</summary>
    [DataField(required: true)]
    public EntProtoId PacketPrototype;

    /// <summary>Produce spawned on harvest (one is picked at random per item).</summary>
    [DataField(required: true)]
    public List<EntProtoId> ProductPrototypes = new();

    /// <summary>RSI holding <c>stage-N</c>, <c>harvest</c> and <c>dead</c> states.</summary>
    [DataField(required: true)]
    public ResPath PlantRsi;

    [DataField]
    public int GrowthStages = 6;

    /// <summary>Whether the RSI has a dedicated harvest sprite. Mushrooms usually don't.</summary>
    [DataField]
    public bool HasHarvestSprite = true;

    // Default stats. Monkestation keeps these effectively uncapped.
    [DataField] public float Lifespan = 25;
    [DataField] public float Endurance = 15;
    [DataField] public float Maturation = 25;
    [DataField] public float Production = 25;
    [DataField] public float Yield = 30;
    [DataField] public float Potency = 10;
    [DataField] public float WeedRate = 1;
    [DataField] public float WeedChance = 5;

    /// <summary>Growth cycles needed to become harvestable, before maturation speeds it up.</summary>
    [DataField]
    public int HarvestAge = 120;

    /// <summary>Reagent -> production rate (fraction of the produce's volume, scaled by potency).</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Chemicals = new();

    /// <summary>
    /// Cooking reagents never come out below what SS14's old botany gave for them:
    /// min + potency / potencyDivisor, capped at max. Only applies while the plant still has
    /// that chemical gene.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, PlantCookingMinimum> CookingMinimums = new();

    /// <summary>Traits the species is born with.</summary>
    [DataField]
    public List<ProtoId<PlantTraitPrototype>> Traits = new();

    /// <summary>Per-trait mutability overrides for the traits above.</summary>
    [DataField]
    public Dictionary<ProtoId<PlantTraitPrototype>, PlantGeneFlags> TraitFlags = new();

    /// <summary>Stat-window mutations this species can produce on harvest.</summary>
    [DataField]
    public List<ProtoId<PlantMutationPrototype>> Mutations = new();

    /// <summary>Mutations unlocked by infusing a specific reagent in the splicer.</summary>
    [DataField]
    public List<ProtoId<PlantMutationPrototype>> InfusionMutations = new();

    /// <summary>Trait carried by grafts cut from this plant. Defaults to perennial growth.</summary>
    [DataField]
    public ProtoId<PlantTraitPrototype>? GraftTrait;

    /// <summary>Monkestation "rarity", used for cargo value.</summary>
    [DataField]
    public int Rarity;

    [DataField]
    public LogImpact? HarvestLogImpact;

    [DataField]
    public LogImpact? PlantLogImpact;
}

/// <summary>SS14's old per-plant chemical amount, used as a floor for cooking reagents.</summary>
[DataDefinition]
public partial struct PlantCookingMinimum
{
    [DataField]
    public float Min;

    [DataField]
    public float Max;

    [DataField]
    public float PotencyDivisor;

    public PlantCookingMinimum()
    {
    }

    public readonly float GetAmount(float potency)
    {
        var amount = Min;
        if (PotencyDivisor > 0 && potency > 0)
            amount += potency / PotencyDivisor;
        return MathF.Min(amount, Max);
    }
}
