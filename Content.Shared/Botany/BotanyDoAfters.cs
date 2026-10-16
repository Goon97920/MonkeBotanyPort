using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Botany;

/// <summary>Digging a plant out of a tray with a spade.</summary>
[Serializable, NetSerializable]
public sealed partial class PlantSpadeDoAfterEvent : SimpleDoAfterEvent;
