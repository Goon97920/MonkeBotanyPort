using Robust.Shared.GameStates;

namespace Content.Shared.Botany.Components;

// Components added to harvested produce by plant traits (see PlantTraitPrototype.ProduceComponents).
// They hold no state of their own: their systems read the produce's genome for potency.

/// <summary>Liquid Contents: bursts when thrown, slipped on or squeezed in hand.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantSquashComponent : Component;

/// <summary>Slippery Skin: people slip on it. Stun time scales with potency.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantSlipperyComponent : Component
{
    /// <summary>Monkestation rate: deciseconds of stun per point of (capped) potency.</summary>
    [DataField]
    public float Rate = 1.6f;
}

/// <summary>
/// Hypodermic Prickles: injects some of its reagents when it hits someone after being thrown,
/// or when they slip on it. Amount is 50 * potency / (2000 + potency) units.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantStingingComponent : Component
{
    [DataField]
    public float MaxInjection = 50f;

    [DataField]
    public float Midpoint = 2000f;
}

/// <summary>
/// Prickly Adhesion: embeds in whoever it hits when thrown. With Hypodermic Prickles it also
/// injects <see cref="InjectAmount"/> units every <see cref="InjectInterval"/> while embedded.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantStickyComponent : Component
{
    [DataField]
    public float InjectAmount = 10f;

    [DataField]
    public TimeSpan InjectInterval = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Syndicate hypodermic needles: when used as a melee weapon, injects through hardsuits and armor.
/// Thrown and slip injections are still blocked normally.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantSyndicateNeedlesComponent : Component
{
    [DataField]
    public float MaxInjection = 50f;

    [DataField]
    public float Midpoint = 2000f;
}

/// <summary>Bluespace Activity: teleports whoever squashes or slips on it.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantTeleportComponent : Component;

/// <summary>Gaseous Decomposition: squashing it releases its reagents as smoke.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantSmokeComponent : Component;

/// <summary>Electrical Activity: may shock whoever squashes or slips on it.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantElectricComponent : Component
{
    [DataField]
    public float ProbabilityMultiplier = 2f;
}

/// <summary>Capacitive Cell Production: add five cable to turn it into a power cell.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantBatteryComponent : Component
{
    [DataField]
    public int CablesNeeded = 5;
}

/// <summary>Hallucinatory Feedback: laughs when slipped on.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlantLaughterComponent : Component;

/// <summary>Raised on produce when it is squashed, before it is deleted.</summary>
[ByRefEvent]
public readonly record struct PlantSquashedEvent(EntityUid? Target);

/// <summary>Raised on produce when someone slips on it.</summary>
[ByRefEvent]
public readonly record struct PlantSlippedOnEvent(EntityUid Slipped);
