using Content.Shared.Chemistry.Reagent;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Chemistry.Metalgen;

/// <summary>
/// The material a batch of metalgen has been imprinted with (Monkestation metalgen data["material"]).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class MetalgenData : ReagentData
{
    [DataField(required: true)]
    public ProtoId<MaterialPrototype> Material;

    public override ReagentData Clone() => new MetalgenData { Material = Material };

    public override bool Equals(ReagentData? other) => other is MetalgenData m && m.Material == Material;

    public override int GetHashCode() => Material.GetHashCode();

    public override string ToString(string prototype) => $"{prototype}:{Material}";
}
