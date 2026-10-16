using Content.Shared.Botany.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.Components;

/// <summary>
/// A seed packet. Holds a genome that is planted into a tray.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeedComponent : Component
{
    /// <summary>Species used to build the genome when none is set yet.</summary>
    [DataField("plantId", required: true), AutoNetworkedField]
    public ProtoId<PlantSpeciesPrototype> Species;

    /// <summary>The seed's genes. Created from <see cref="Species"/> on map init if null.</summary>
    /// <remarks>
    /// Server only, so a pile of a thousand strange seeds doesn't send a thousand genomes to every
    /// client in view. Examine text is built on the server, and the machines send what they show
    /// through their UI states.
    /// </remarks>
    [DataField]
    public PlantGenome? Genome;

    /// <summary>
    /// Strange seeds: randomize stats, chemicals and traits when the genome is first created.
    /// </summary>
    [DataField]
    public bool RandomizeGenome;

    /// <summary>
    /// Reagents a randomized seed can never roll (lesser strange seeds). Empty means no blacklist.
    /// </summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> RandomReagentBlacklist = new();

    /// <summary>Sample seeds (from the seed extractor's analysis) can't be planted or modified.</summary>
    [DataField]
    public bool Sample;
}
