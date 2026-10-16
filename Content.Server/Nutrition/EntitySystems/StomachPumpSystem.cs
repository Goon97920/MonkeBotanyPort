using Content.Shared.Body;
using Content.Shared.Nutrition;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Nutrition;
using Content.Shared.Popups;

namespace Content.Server.Nutrition.EntitySystems;

/// <summary>
/// Stomach pumps: empty a patient's stomach and secondary stomach into a container.
/// </summary>
public sealed partial class StomachPumpSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SecondaryStomachSystem _secondaryStomach = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StomachPumpComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<StomachPumpComponent, StomachPumpDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<StomachPumpComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (HasComp<BodyComponent>(target))
        {
            args.Handled = true;
            if (GetStomachVolume(target) + _secondaryStomach.GetVolume(target) <= 0)
            {
                _popup.PopupEntity(Loc.GetString("stomach-pump-nothing", ("target", Identity.Entity(target, EntityManager))), target, args.User);
                return;
            }

            _popup.PopupEntity(Loc.GetString("stomach-pump-start", ("target", Identity.Entity(target, EntityManager))), target, args.User);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new StomachPumpDoAfterEvent(), ent, target, ent)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            });
            return;
        }

        // Attach the hose to a container or machine, or detach it.
        if (target == ent.Comp.AttachedTo)
        {
            args.Handled = true;
            ent.Comp.AttachedTo = null;
            Dirty(ent);
            _popup.PopupEntity(Loc.GetString("stomach-pump-detached", ("target", target)), target, args.User);
            return;
        }

        if (GetContainerSolution(target) == null)
            return;

        args.Handled = true;
        ent.Comp.AttachedTo = target;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("stomach-pump-attached", ("target", target)), target, args.User);
    }

    private void OnDoAfter(Entity<StomachPumpComponent> ent, ref StomachPumpDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } patient)
            return;

        args.Handled = true;
        var pumped = PumpStomachs(patient, ent.Comp.PumpAmount);
        if (pumped.Volume <= 0)
        {
            _popup.PopupEntity(Loc.GetString("stomach-pump-done", ("target", Identity.Entity(patient, EntityManager))), patient, args.User);
            return;
        }

        DepositPumped(ent, args.User, patient, pumped);

        var remaining = GetStomachVolume(patient) + _secondaryStomach.GetVolume(patient);
        if (remaining <= 0)
        {
            _popup.PopupEntity(Loc.GetString("stomach-pump-done", ("target", Identity.Entity(patient, EntityManager))), patient, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("stomach-pump-progress", ("remaining", remaining)), patient, args.User);
        args.Repeat = true;
    }

    /// <summary>Takes up to <paramref name="amount"/> from the stomachs first, then the secondary stomach.</summary>
    private Solution PumpStomachs(EntityUid patient, FixedPoint2 amount)
    {
        var result = new Solution();
        if (_body.TryGetOrgansWithComponent<StomachComponent>(patient, out var stomachs))
        {
            foreach (var stomach in stomachs)
            {
                if (amount - result.Volume <= 0)
                    break;
                if (!_solution.TryGetSolution(stomach.Owner, StomachSystem.DefaultSolutionName, out var soln, out var sol) || sol.Volume <= 0)
                    continue;

                result.AddSolution(_solution.SplitSolution(soln.Value, amount - result.Volume), null);
            }
        }

        var left = amount - result.Volume;
        if (left > 0)
            result.AddSolution(_secondaryStomach.Take(patient, left), null);

        return result;
    }

    private FixedPoint2 GetStomachVolume(EntityUid patient)
    {
        var total = FixedPoint2.Zero;
        if (!_body.TryGetOrgansWithComponent<StomachComponent>(patient, out var stomachs))
            return total;

        foreach (var stomach in stomachs)
        {
            if (_solution.TryGetSolution(stomach.Owner, StomachSystem.DefaultSolutionName, out _, out var sol))
                total += sol.Volume;
        }

        return total;
    }

    /// <summary>Into the inserted container, then the attached one, then the floor.</summary>
    private void DepositPumped(Entity<StomachPumpComponent> ent, EntityUid user, EntityUid patient, Solution pumped)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, ent.Comp.ContainerSlotId) is { } inserted
            && GetContainerSolution(inserted) is { } insertedSoln)
        {
            _solution.TryAddSolution(insertedSoln, pumped.SplitSolution(insertedSoln.Comp.Solution.AvailableVolume));
        }

        if (pumped.Volume > 0 && ent.Comp.AttachedTo is { } attached)
        {
            if (Deleted(attached) || !_interaction.InRangeUnobstructed(ent.Owner, attached, ent.Comp.AttachRange))
            {
                ent.Comp.AttachedTo = null;
                Dirty(ent);
                _popup.PopupEntity(Loc.GetString("stomach-pump-hose-detached"), user, user, PopupType.SmallCaution);
            }
            else if (GetContainerSolution(attached) is { } attachedSoln)
            {
                _solution.TryAddSolution(attachedSoln, pumped.SplitSolution(attachedSoln.Comp.Solution.AvailableVolume));
            }
        }

        if (pumped.Volume > 0)
            _puddle.TrySpillAt(patient, pumped, out _);
    }

    /// <summary>
    /// A container's refillable solution, or that of a container held in one of a machine's item slots.
    /// </summary>
    private Entity<SolutionComponent>? GetContainerSolution(EntityUid uid)
    {
        if (_solution.TryGetRefillableSolution(uid, out var soln, out _))
            return soln;

        if (!TryComp<ItemSlotsComponent>(uid, out var slots))
            return null;

        foreach (var slot in slots.Slots.Values)
        {
            if (slot.Item is { } item && _solution.TryGetRefillableSolution(item, out var held, out _))
                return held;
        }

        return null;
    }
}
