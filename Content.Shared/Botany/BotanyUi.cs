using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Botany;

[Serializable, NetSerializable]
public enum PlantAnalyzerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum PlantDnaManipulatorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum PlantSplicerUiKey : byte
{
    Key,
}

/// <summary>Plant analyzer report: a title plus a markup body built on the server.</summary>
[Serializable, NetSerializable]
public sealed class PlantAnalyzerState(string title, string report) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly string Report = report;
}

[Serializable, NetSerializable]
public sealed partial class PlantAnalyzerDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum PlantGeneKind : byte
{
    Core,
    Chemical,
    Trait,
}

/// <summary>One gene row in the DNA manipulator.</summary>
[Serializable, NetSerializable]
public sealed class PlantGeneRow
{
    public PlantGeneKind Kind;

    /// <summary>For core genes the <see cref="PlantStat"/>; otherwise the index in the genome list.</summary>
    public int Index;

    public string Name = string.Empty;

    public bool CanExtract;
    public bool CanRemove;
    public bool CanReplace;

    /// <summary>Shown when extraction would degrade the gene (part tier caps).</summary>
    public string? ExtractWarning;
}

[Serializable, NetSerializable]
public sealed class PlantDnaManipulatorState : BoundUserInterfaceState
{
    public string? SeedName;
    public string? DiskName;
    public bool HasDisk;
    public bool DiskReadOnly;
    public bool DiskHasGene;
    public bool CanInsert;
    public string? InsertLabel;
    public List<PlantGeneRow> Genes = new();
}

[Serializable, NetSerializable]
public sealed class PlantDnaExtractMessage(PlantGeneKind kind, int index) : BoundUserInterfaceMessage
{
    public readonly PlantGeneKind Kind = kind;
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class PlantDnaRemoveMessage(PlantGeneKind kind, int index) : BoundUserInterfaceMessage
{
    public readonly PlantGeneKind Kind = kind;
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class PlantDnaReplaceMessage(int stat) : BoundUserInterfaceMessage
{
    public readonly int Stat = stat;
}

[Serializable, NetSerializable]
public sealed class PlantDnaInsertMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PlantSplicerState : BoundUserInterfaceState
{
    public string? FirstSeed;
    public string? FirstSeedStats;
    public string? SecondSeed;
    public string? SecondSeedStats;
    public string? Beaker;
    public float InfusionDamage;
    public float PotentialDamage;
    public string? InfusionPreview;
}

[Serializable, NetSerializable]
public sealed class PlantSplicerSpliceMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class PlantSplicerInfuseMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum SeedExtractorUiKey : byte
{
    Key,
}

/// <summary>
/// One pile of stored seeds. Like Monkestation, seeds pile together when their name and core
/// stats match, and the pile shows the genes of the first seed put in.
/// </summary>
[Serializable, NetSerializable]
public sealed class SeedExtractorPileEntry
{
    public string Key = string.Empty;
    public string Name = string.Empty;
    public string? SeedRsi;
    public int Amount;
    public float Potency;
    public float Yield;
    public float Production;
    public float Endurance;
    public float Lifespan;
    public float Maturation;
    public List<string> Traits = new();
    public List<string> Chemicals = new();
    public List<string> Mutations = new();
}

[Serializable, NetSerializable]
public sealed class SeedExtractorState(List<SeedExtractorPileEntry> piles, int stored, int capacity) : BoundUserInterfaceState
{
    public readonly List<SeedExtractorPileEntry> Piles = piles;
    public readonly int Stored = stored;
    public readonly int Capacity = capacity;
}

/// <summary>Take one seed from a pile.</summary>
[Serializable, NetSerializable]
public sealed class SeedExtractorTakeMessage(string key) : BoundUserInterfaceMessage
{
    public readonly string Key = key;
}

/// <summary>Destroy every seed in a pile.</summary>
[Serializable, NetSerializable]
public sealed class SeedExtractorScrapMessage(string key) : BoundUserInterfaceMessage
{
    public readonly string Key = key;
}
