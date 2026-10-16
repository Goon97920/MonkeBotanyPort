using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.Prototypes;

/// <summary>
/// An inclusive stat window. A missing bound is unbounded.
/// </summary>
[DataDefinition]
public partial struct PlantStatRange
{
    [DataField] public float? Min;
    [DataField] public float? Max;

    public readonly bool Contains(float value)
    {
        if (Min is { } min && value < min)
            return false;
        if (Max is { } max && value > max)
            return false;
        return true;
    }
}

/// <summary>
/// A species mutation (Monkestation's <c>/datum/hydroponics/plant_mutation</c>).
/// While the plant's stats sit inside every listed window, about 65% of harvested seeds
/// and 10% of harvested produce come out as <see cref="Result"/>.
/// </summary>
[Prototype("plantMutation")]
public sealed partial class PlantMutationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>The species the plant mutates into.</summary>
    [DataField(required: true)]
    public ProtoId<PlantSpeciesPrototype> Result;

    /// <summary>Informational only, shown in the guidebook. The mutation lists on species decide what can mutate.</summary>
    [DataField]
    public List<ProtoId<PlantSpeciesPrototype>> From = new();

    [DataField] public PlantStatRange? Potency;
    [DataField] public PlantStatRange? Yield;
    [DataField] public PlantStatRange? Production;
    [DataField] public PlantStatRange? Endurance;
    [DataField] public PlantStatRange? Lifespan;

    /// <summary>For infusion mutations: infusing any of these unlocks the mutation.</summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> InfusionReagents = new();

    public bool IsViable(PlantGenome genome)
    {
        return (Potency?.Contains(genome.Potency) ?? true)
               && (Yield?.Contains(genome.Yield) ?? true)
               && (Production?.Contains(genome.Production) ?? true)
               && (Endurance?.Contains(genome.Endurance) ?? true)
               && (Lifespan?.Contains(genome.Lifespan) ?? true);
    }
}
