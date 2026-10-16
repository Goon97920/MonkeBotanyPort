using Content.Shared.Body;
using Content.Shared.Nutrition;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Nutrition.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.Nutrition.EntitySystems;

/// <summary>
/// Releases oversized produce bites into the stomach as it empties, and handles stomach pumps.
/// </summary>
public sealed partial class SecondaryStomachSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private StomachSystem _stomach = default!;

    /// <summary>Volume waiting in an entity's secondary stomach.</summary>
    public FixedPoint2 GetVolume(EntityUid uid)
    {
        return TryComp<SecondaryStomachComponent>(uid, out var comp) ? comp.Contents.Volume : FixedPoint2.Zero;
    }

    /// <summary>Adds reagents to the secondary stomach. They will not react while there.</summary>
    public void Add(EntityUid eater, Solution solution)
    {
        if (solution.Volume <= 0)
            return;

        var comp = EnsureComp<SecondaryStomachComponent>(eater);
        if (comp.NextRelease == TimeSpan.Zero)
            comp.NextRelease = _timing.CurTime + comp.ReleaseInterval;

        comp.Contents.CanReact = false;
        comp.Contents.MaxVolume += solution.Volume;
        comp.Contents.AddSolution(solution, null);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SecondaryStomachComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextRelease)
                continue;

            comp.NextRelease = now + comp.ReleaseInterval;
            Release((uid, comp));
        }
    }

    /// <summary>Moves as much as fits into the emptiest stomach.</summary>
    private void Release(Entity<SecondaryStomachComponent> ent)
    {
        if (ent.Comp.Contents.Volume <= 0)
        {
            RemCompDeferred<SecondaryStomachComponent>(ent);
            return;
        }

        if (!_body.TryGetOrgansWithComponent<StomachComponent>(ent.Owner, out var stomachs))
            return;

        Entity<StomachComponent>? best = null;
        var space = FixedPoint2.Zero;
        foreach (var stomach in stomachs)
        {
            if (!_solution.TryGetSolution(stomach.Owner, StomachSystem.DefaultSolutionName, out _, out var sol))
                continue;
            if (sol.AvailableVolume <= space)
                continue;

            space = sol.AvailableVolume;
            best = stomach;
        }

        if (best == null || space <= 0)
            return;

        var portion = ent.Comp.Contents.SplitSolution(space);
        portion.CanReact = true;
        if (!_stomach.TryTransferSolution((best.Value.Owner, best.Value.Comp), portion))
        {
            // Couldn't fit after all; put it back.
            ent.Comp.Contents.AddSolution(portion, null);
            return;
        }

        if (ent.Comp.Contents.Volume <= 0)
            RemCompDeferred<SecondaryStomachComponent>(ent);
    }

    /// <summary>Removes up to <paramref name="amount"/> from the secondary stomach.</summary>
    public Solution Take(EntityUid uid, FixedPoint2 amount)
    {
        if (!TryComp<SecondaryStomachComponent>(uid, out var comp))
            return new Solution();

        var taken = comp.Contents.SplitSolution(amount);
        if (comp.Contents.Volume <= 0)
            RemCompDeferred<SecondaryStomachComponent>(uid);
        return taken;
    }
}
