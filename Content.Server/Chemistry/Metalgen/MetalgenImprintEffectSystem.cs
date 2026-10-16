using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Metalgen;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Server.Chemistry.Metalgen;

/// <summary>
/// Runs on the solution after metalgen reacts with mercury. Monkestation checks every reagent in
/// the beaker: each metal with 40u or more imprints its material (the last one wins) and loses 40u.
/// </summary>
public sealed partial class MetalgenImprintEffectSystem : EntityEffectSystem<SolutionComponent, MetalgenImprint>
{
    public static readonly ProtoId<ReagentPrototype> Metalgen = "Metalgen";

    protected override void Effect(Entity<SolutionComponent> entity, ref EntityEffectEvent<MetalgenImprint> args)
    {
        var solution = entity.Comp.Solution;
        ProtoId<MaterialPrototype>? material = null;

        foreach (var (reagent, metal) in args.Effect.Metals)
        {
            if (solution.GetTotalPrototypeQuantity(reagent) < args.Effect.RequiredAmount)
                continue;

            material = metal;
            solution.RemoveReagent(reagent, args.Effect.RequiredAmount, ignoreReagentData: true);
        }

        if (material == null)
            return;

        var total = solution.GetTotalPrototypeQuantity(Metalgen);
        if (total <= FixedPoint2.Zero)
            return;

        solution.RemoveReagent(Metalgen, total, ignoreReagentData: true);
        solution.AddReagent(new ReagentId(Metalgen, new List<ReagentData> { new MetalgenData { Material = material.Value } }), total);
    }
}
