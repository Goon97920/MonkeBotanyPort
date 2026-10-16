using Content.Shared.Botany.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.Components;

/// <summary>
/// Harvested produce. Carries a copy of the genome of the plant it grew on, which the seed
/// extractor turns back into seeds.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ProduceComponent : Component
{
    [DataField("plantId"), AutoNetworkedField]
    public ProtoId<PlantSpeciesPrototype>? Species;

    /// <summary>Server only, like <see cref="SeedComponent.Genome"/>.</summary>
    [DataField]
    public PlantGenome? Genome;

    /// <summary>Solution that receives the plant's chemicals.</summary>
    [DataField]
    public string TargetSolution = "food";

    /// <summary>
    /// Base reagent capacity before traits (Monkestation PLANT_REAGENT_VOLUME = 100).
    /// </summary>
    [DataField]
    public float BaseVolume = 100f;
}
