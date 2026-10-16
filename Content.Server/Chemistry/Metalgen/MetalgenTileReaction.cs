using Content.Shared.Chemistry.Metalgen;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;

namespace Content.Server.Chemistry.Metalgen;

/// <summary>
/// Spilled imprinted metalgen transmutes what's built on the tile (Monkestation expose_turf).
/// SS14 floor tiles can't change material, so the walls, tables and machines standing on it do.
/// The metalgen isn't used up.
/// </summary>
[DataDefinition]
public sealed partial class MetalgenTileReaction : ITileReaction
{
    public FixedPoint2 TileReact(TileRef tile,
        ReagentPrototype reagent,
        FixedPoint2 reactVolume,
        IEntityManager entityManager,
        List<ReagentData>? data)
    {
        if (data == null)
            return FixedPoint2.Zero;

        Robust.Shared.Prototypes.ProtoId<Content.Shared.Materials.MaterialPrototype>? material = null;
        foreach (var d in data)
        {
            if (d is MetalgenData m)
                material = m.Material;
        }

        if (material == null)
            return FixedPoint2.Zero;

        var lookup = entityManager.System<EntityLookupSystem>();
        var metalgen = entityManager.System<MetalgenSystem>();
        var xformQuery = entityManager.GetEntityQuery<TransformComponent>();
        foreach (var uid in lookup.GetLocalEntitiesIntersecting(tile, flags: LookupFlags.Static))
        {
            if (xformQuery.TryGetComponent(uid, out var xform) && xform.Anchored)
                metalgen.Transmute(uid, material.Value);
        }

        return FixedPoint2.Zero;
    }
}
