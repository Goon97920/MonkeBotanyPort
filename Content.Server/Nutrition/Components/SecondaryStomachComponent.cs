using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Nutrition.Components;

/// <summary>
/// Holds the part of an oversized produce bite that didn't fit in the eater's stomach.
/// It trickles into the stomach as space frees up and is removed once empty.
/// Nothing reacts while it is in here. A stomach pump can remove it.
/// </summary>
[RegisterComponent, Access(typeof(SecondaryStomachSystem)), AutoGenerateComponentPause]
public sealed partial class SecondaryStomachComponent : Component
{
    [DataField]
    public Solution Contents = new() { CanReact = false };

    [DataField]
    public TimeSpan ReleaseInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRelease;
}
