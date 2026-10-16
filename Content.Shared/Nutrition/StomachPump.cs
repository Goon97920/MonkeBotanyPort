using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Nutrition;

/// <summary>
/// Pumps a patient's stomach and secondary stomach (undigested oversized produce bites) a chunk at
/// a time. Pumped reagents go into the container in <see cref="ContainerSlotId"/>, otherwise into the
/// container or machine the pump is attached to, otherwise onto the floor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StomachPumpComponent : Component
{
    /// <summary>Units removed per pump cycle.</summary>
    [DataField]
    public FixedPoint2 PumpAmount = FixedPoint2.New(50);

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    /// <summary>Item slot holding a beaker or similar container.</summary>
    [DataField]
    public string ContainerSlotId = "beaker_slot";

    /// <summary>A container or machine the pump's hose is attached to.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? AttachedTo;

    /// <summary>How far the attached container can be from the pump.</summary>
    [DataField]
    public float AttachRange = 3f;
}

[Serializable, NetSerializable]
public sealed partial class StomachPumpDoAfterEvent : SimpleDoAfterEvent;
