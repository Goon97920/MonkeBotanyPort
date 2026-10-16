using Content.Shared.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Metalgen;

/// <summary>
/// An object transmuted by metalgen: it's now made of <see cref="Material"/>, tinted its color,
/// and named after it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MetalgenTransmutedComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<MaterialPrototype> Material;

    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    /// <summary>The name before the first transmutation, so repeated ones don't stack prefixes.</summary>
    [DataField]
    public string? OriginalName;
}
