using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Metalgen;

/// <summary>
/// Reaction effect for metalgen + mercury: if 40u or more of a metal is in the solution, the
/// metalgen is imprinted with its material and 40u of the metal is used up
/// (Monkestation metalgen_imprint, with mercury in place of liquid dark matter).
/// </summary>
public sealed partial class MetalgenImprint : EntityEffectBase<MetalgenImprint>
{
    /// <summary>Metal reagents and the material each imprints.</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, ProtoId<MaterialPrototype>> Metals = new();

    [DataField]
    public FixedPoint2 RequiredAmount = 40;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-metalgen-imprint", ("amount", RequiredAmount));
}

/// <summary>
/// Keeps a batch's imprint through the imprint reaction when there's no metal to imprint a new one.
/// </summary>
public sealed partial class MetalgenKeepImprintExtension : ReactionExtension
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Metalgen";

    public override void React(IReadOnlyList<ReagentQuantity> reactants, List<ReagentQuantity> products)
    {
        MetalgenData? data = null;
        foreach (var reactant in reactants)
        {
            if (reactant.Reagent.Prototype != Reagent || reactant.Reagent.Data == null)
                continue;

            foreach (var d in reactant.Reagent.Data)
            {
                if (d is MetalgenData m)
                    data = m;
            }
        }

        if (data == null)
            return;

        for (var i = 0; i < products.Count; i++)
        {
            if (products[i].Reagent.Prototype != Reagent)
                continue;

            products[i] = new ReagentQuantity(Reagent, products[i].Quantity, new List<ReagentData> { data.Clone() });
        }
    }
}
