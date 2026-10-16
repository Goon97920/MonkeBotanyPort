using Content.Shared.Chemistry.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Botany.Components;

[Serializable, NetSerializable]
public enum PlantState : byte
{
    None,
    Growing,
    Harvestable,
    Dead,
}

/// <summary>
/// A hydroponics tray or soil plot. Ported from Monkestation's <c>/datum/component/plant_growing</c>
/// and <c>/datum/component/growth_information</c>: the tray holds the planted genome, a
/// nutrient solution whose reagents change the plant's stats every cycle, and water.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class PlantTrayComponent : Component
{
    /// <summary>The planted genome, if anything is planted.</summary>
    [DataField, AutoNetworkedField]
    public PlantGenome? Genome;

    [DataField, AutoNetworkedField]
    public PlantState State = PlantState.None;

    /// <summary>Growth cycles since planting or the last repeat harvest.</summary>
    [DataField, AutoNetworkedField]
    public int GrowthCycle;

    /// <summary>Age in cycles. Stops increasing once the plant is fully grown.</summary>
    [DataField, AutoNetworkedField]
    public int Age;

    /// <summary>Plant health. Maximum is three times endurance.</summary>
    [DataField, AutoNetworkedField]
    public float Health;

    /// <summary>Extra lifespan granted by repeat harvests (5% of lifespan each).</summary>
    [DataField, AutoNetworkedField]
    public float RepeatedHarvestValue;

    /// <summary>Water, as a percentage of <see cref="MaxWater"/>.</summary>
    [DataField, AutoNetworkedField]
    public float WaterPercent = 100f;

    /// <summary>Units of water the tray holds at 100%.</summary>
    [DataField]
    public float MaxWater = 200f;

    [DataField, AutoNetworkedField]
    public float Toxicity;

    [DataField, AutoNetworkedField]
    public float Pests;

    [DataField, AutoNetworkedField]
    public float Weeds;

    /// <summary>Self-sustenance percentage (Earthsblood). At 100% weeds stop growing.</summary>
    [DataField, AutoNetworkedField]
    public float SelfSustain;

    /// <summary>Counts work cycles; water drain and weeds happen every second cycle.</summary>
    [DataField]
    public int WorkCycle;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextWork = TimeSpan.Zero;

    /// <summary>Time between growth cycles (Monkestation work_processes).</summary>
    [DataField]
    public TimeSpan WorkInterval = TimeSpan.FromSeconds(10);

    /// <summary>Whether the tray shows warning lights (hydroponics trays do, soil doesn't).</summary>
    [DataField]
    public bool DrawWarnings;

    [DataField]
    public SoundSpecifier? WateringSound;

    [DataField]
    public string SoilSolutionName = "soil";

    [ViewVariables]
    public Entity<SolutionComponent>? SoilSolution;

    /// <summary>Reagent units in the soil the last time the server looked. Networked for examine.</summary>
    [DataField, AutoNetworkedField]
    public float Nutrients;

    /// <summary>Percentage of growth done, networked for UI and examine.</summary>
    [DataField, AutoNetworkedField]
    public float GrowthPercent;

    public const float MaxWeeds = 10f;
    public const float MaxPests = 10f;
}

/// <summary>Appearance keys for trays.</summary>
[Serializable, NetSerializable]
public enum PlantTrayVisuals : byte
{
    /// <summary>RSI path of the planted species (string), empty when nothing is planted.</summary>
    PlantRsi,
    /// <summary>State in that RSI to draw (string).</summary>
    PlantState,
    HealthLight,
    WaterLight,
    NutritionLight,
    AlertLight,
    HarvestLight,
}

[Serializable, NetSerializable]
public enum PlantTrayLayers : byte
{
    Plant,
}
