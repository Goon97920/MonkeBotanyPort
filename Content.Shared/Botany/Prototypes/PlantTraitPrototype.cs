using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.Prototypes;

/// <summary>
/// A plant trait gene (Monkestation's <c>/datum/plant_gene/trait</c>).
/// Behaviour is either a simple flag read by the botany systems, or components added to
/// harvested produce, which their own systems handle.
/// </summary>
[Prototype("plantTrait")]
public sealed partial class PlantTraitPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>Default mutability flags of this trait.</summary>
    [DataField]
    public PlantGeneFlags Flags = PlantGeneFlags.Removable | PlantGeneFlags.Mutatable | PlantGeneFlags.Graftable;

    /// <summary>Traits sharing any group cannot be on the same plant (Monkestation "trait_ids").</summary>
    [DataField]
    public List<string> Groups = new();

    /// <summary>Species this trait can never be added to.</summary>
    [DataField]
    public List<ProtoId<PlantSpeciesPrototype>> SpeciesBlacklist = new();

    /// <summary>
    /// Whether strange seeds may roll this trait. Defaults to the Mutatable flag; set false to
    /// keep a mutatable trait out of the random pool entirely (e.g. the syndicate needles).
    /// </summary>
    [DataField]
    public bool RandomPool = true;

    /// <summary>When gained through tray training, halves the plant's yield (Monkestation TRAIT_HALVES_YIELD).</summary>
    [DataField]
    public bool HalvesYieldOnGain;

    // ---- Flags read by the botany systems ----

    /// <summary>Plant can be harvested repeatedly.</summary>
    [DataField] public bool Perennial;

    /// <summary>Plant produces no seeds on harvest.</summary>
    [DataField] public bool Seedless;

    /// <summary>Multiplies the produce's reagent capacity (Densified Chemicals = 2).</summary>
    [DataField] public float VolumeMultiplier = 1f;

    /// <summary>Overrides the produce's base reagent capacity (Dank Vesicles = 420).</summary>
    [DataField] public float? BaseVolume;

    /// <summary>Replaces nutriment/vitamin with the produce's juice.</summary>
    [DataField] public bool Juicing;

    /// <summary>Replaces nutriment/vitamin with wine.</summary>
    [DataField] public bool Distilling;

    /// <summary>Burns nutriment to heat the contents by this many kelvin per unit (Exothermic = 25).</summary>
    [DataField] public float HeatPerNutriment;

    /// <summary>Burns nutriment to cool the contents by this many kelvin per unit (Endothermic = 5).</summary>
    [DataField] public float CoolPerNutriment;

    /// <summary>Contents don't react until squashed.</summary>
    [DataField] public bool NoReact;

    /// <summary>Eaten in a single bite.</summary>
    [DataField] public bool OneBite;

    /// <summary>Glow color for bioluminescence traits.</summary>
    [DataField] public Color? GlowColor;

    /// <summary>Monkestation glow "rate": radius = 1.4 + potency * rate.</summary>
    [DataField] public float GlowRate = 0.03f;

    /// <summary>
    /// Components added to produce harvested from a plant with this trait. Their systems
    /// read the produce's genome for potency and the like.
    /// </summary>
    [DataField]
    public ComponentRegistry ProduceComponents = new();
}
