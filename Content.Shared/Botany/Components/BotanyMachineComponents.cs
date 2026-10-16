using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.Components;

/// <summary>
/// Turns produce into seeds. Monkestation drops rand(1,4) * multiplier + 1 seeds per produce;
/// the default multiplier of 4 matches a tier 4 manipulator.
/// Seeds put in by hand (or from a bag) are stored as data, not entities, and come back out
/// through the UI. Seeds the extractor makes always land on the floor.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SeedExtractorComponent : Component
{
    [DataField]
    public int SeedMultiplier = 4;

    /// <summary>Seeds the machine can store (Monkestation: 1000 per matter bin tier).</summary>
    [DataField]
    public int MaxSeeds = 4000;

    [DataField]
    public SoundSpecifier? ExtractSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier? StoreSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    /// <summary>Stored seeds, server side. Keyed by name and core stats, in insertion order.</summary>
    [ViewVariables]
    public Dictionary<string, SeedExtractorPile> Piles = new();

    /// <summary>Order the piles were first made in, for a stable UI.</summary>
    [ViewVariables]
    public List<string> PileOrder = new();

    [ViewVariables]
    public int StoredCount;
}

/// <summary>A pile of stored seeds: their genomes, and the species of the first one for the icon.</summary>
public sealed class SeedExtractorPile
{
    public string Name = string.Empty;
    public ProtoId<Prototypes.PlantSpeciesPrototype> Species;
    public readonly List<PlantGenome> Genomes = new();
}

/// <summary>
/// A plant data disk. Stores one gene for the DNA manipulator.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlantGeneDiskComponent : Component
{
    [DataField, AutoNetworkedField]
    public PlantDiskGene? Gene;

    /// <summary>Write-protect tab. Toggled by using the disk in hand.</summary>
    [DataField, AutoNetworkedField]
    public bool ReadOnly;
}

/// <summary>
/// The plant DNA manipulator. Extracts genes from seeds onto disks (destroying the seed),
/// removes chemical and trait genes, inserts genes from disks and replaces core stats.
/// Extraction caps match Monkestation's tier 4 parts.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantDnaManipulatorComponent : Component
{
    [DataField]
    public string SeedSlotId = "seed_slot";

    [DataField]
    public string DiskSlotId = "disk_slot";

    /// <summary>Highest potency that survives extraction. Null is uncapped (tier 4).</summary>
    [DataField]
    public float? MaxPotency;

    /// <summary>Highest yield that survives extraction (2 + tier * 2).</summary>
    [DataField]
    public float MaxYield = 10;

    /// <summary>Highest endurance and lifespan that survive extraction (10 + tier * 25).</summary>
    [DataField]
    public float MaxEndurance = 110;

    /// <summary>Lowest weed rate that survives extraction.</summary>
    [DataField]
    public float MinWeedRate = 0;

    /// <summary>Lowest weed chance that survives extraction.</summary>
    [DataField]
    public float MinWeedChance = 3;
}

/// <summary>
/// The seed splicer. Splices two seeds into a hybrid, or infuses a seed with a beaker of reagents.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantSplicerComponent : Component
{
    [DataField]
    public string FirstSeedSlotId = "seed_slot_one";

    [DataField]
    public string SecondSeedSlotId = "seed_slot_two";

    [DataField]
    public string BeakerSlotId = "beaker_slot";

    /// <summary>Units of a reagent needed to infuse it as a new chemical gene.</summary>
    [DataField]
    public float InfusionGeneVolume = 100f;

    /// <summary>Infusion damage per unit of reagent in the beaker (Monkestation: 1 per 10u).</summary>
    [DataField]
    public float DamagePerUnit = 0.1f;

    /// <summary>
    /// Stat changes per unit of a reagent when infused (Monkestation generate_infusion_values).
    /// Each stat gets units * a random step between min and max.
    /// As in Monkestation, when several listed reagents change the same stat the last one wins.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, Dictionary<PlantStat, PlantInfusionRange>> Infusions = new();
}

/// <summary>
/// A per-unit range rolled in steps, like Monkestation's rand(a, b) * step.
/// </summary>
[DataDefinition, Serializable]
public partial struct PlantInfusionRange
{
    [DataField(required: true)]
    public float Min;

    [DataField(required: true)]
    public float Max;

    /// <summary>Granularity of the roll. 0 rolls continuously.</summary>
    [DataField]
    public float Step = 0.1f;

    public PlantInfusionRange()
    {
    }

    public float Roll(Robust.Shared.Random.IRobustRandom random)
    {
        var lo = MathF.Min(Min, Max);
        var hi = MathF.Max(Min, Max);
        if (Step <= 0)
            return random.NextFloat(lo, hi);

        var steps = (int) MathF.Round((hi - lo) / Step);
        return lo + random.Next(steps + 1) * Step;
    }
}

/// <summary>
/// A graft cut from a plant with secateurs. Applying it to a planted tray adds its trait and
/// pulls the plant's stats two thirds of the way towards the graft's stats.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlantGraftComponent : Component
{
    [DataField, AutoNetworkedField]
    public PlantTraitGene? Trait;

    [DataField, AutoNetworkedField]
    public string ParentName = string.Empty;

    [DataField, AutoNetworkedField] public float Lifespan;
    [DataField, AutoNetworkedField] public float Endurance;
    [DataField, AutoNetworkedField] public float Production;
    [DataField, AutoNetworkedField] public float WeedRate;
    [DataField, AutoNetworkedField] public float WeedChance;
    [DataField, AutoNetworkedField] public float Yield;
}

/// <summary>Removes the plant from a tray (Monkestation spade).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BotanySpadeComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);
}

/// <summary>Uproots weeds (Monkestation cultivator).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BotanyCultivatorComponent : Component;

/// <summary>Cuts grafts off harvestable plants (Monkestation secateurs).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BotanySecateursComponent : Component
{
    [DataField]
    public EntProtoId GraftPrototype = "PlantGraft";
}

/// <summary>
/// The plant analyzer. Shows a report of a tray, seed or produce.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantAnalyzerComponent : Component
{
    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public SoundSpecifier? ScanningEndSound;
}

/// <summary>
/// Monkestation portaseeder: alt-click turns the plants inside into seeds, kept in the bag.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PortableSeedExtractorComponent : Component
{
    /// <summary>Monkestation seedify t_max: each plant becomes this many seeds plus one.</summary>
    [DataField]
    public int SeedsPerPlant = 1;
}
