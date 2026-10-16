using System.Linq;
using Content.Server.Materials.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Botany.Components;
using Content.Shared.Storage;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Robust.Server.Audio;

namespace Content.Server.Materials;

public sealed partial class ProduceMaterialExtractorSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private MaterialStorageSystem _materialStorage = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        // Before item slots, so a bag of produce is emptied into the machine rather than put in its
        // output slot. An empty bag isn't handled here and goes into the slot.
        SubscribeLocalEvent<ProduceMaterialExtractorComponent, InteractUsingEvent>(OnInteractUsing,
            before: new[] { typeof(ItemSlotsSystem) });
    }

    private void OnInteractUsing(Entity<ProduceMaterialExtractorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!this.IsPowered(ent, EntityManager))
            return;

        // A plant bag (or any storage) dumps all of its produce in at once.
        if (!HasComp<ProduceComponent>(args.Used) && TryComp<StorageComponent>(args.Used, out var storage))
        {
            var converted = 0;
            foreach (var item in storage.Container.ContainedEntities.ToArray())
            {
                if (TryComp<ProduceComponent>(item, out var p) && TryExtract(ent, (item, p)))
                    converted++;
            }

            if (converted == 0)
                return;

            _audio.PlayPvs(ent.Comp.ExtractSound, ent);
            _popup.PopupEntity(Loc.GetString("material-extractor-comp-bag", ("count", converted)), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (!TryComp<ProduceComponent>(args.Used, out var produce))
            return;

        args.Handled = true;
        if (!TryExtract(ent, (args.Used, produce)))
        {
            _popup.PopupEntity(Loc.GetString("material-extractor-comp-wrongreagent", ("used", args.Used)), args.User, args.User);
            return;
        }

        _audio.PlayPvs(ent.Comp.ExtractSound, ent);
    }

    /// <summary>
    /// Monkestation biogenerator: every unit of nutriment (including vitamin and protein), rounded up,
    /// times the productivity (4 for tier 4 parts).
    /// </summary>
    private bool TryExtract(Entity<ProduceMaterialExtractorComponent> ent, Entity<ProduceComponent> produce)
    {
        if (!_solutionContainer.TryGetSolution(produce.Owner, produce.Comp.TargetSolution, out var solution))
            return false;

        var matAmount = solution.Value.Comp.Solution.Contents
            .Where(r => ent.Comp.ExtractionReagents.Contains(r.Reagent.Prototype))
            .Sum(r => r.Quantity.Float());

        var changed = (int) Math.Min(MathF.Ceiling(matAmount) * ent.Comp.Productivity, int.MaxValue / 2f);
        if (changed <= 0)
            return false;

        // Fill up to the storage limit (1e9 for the biogenerator); a full machine refuses produce.
        if (TryComp<MaterialStorageComponent>(ent, out var storage) && storage.StorageLimit is { } limit)
        {
            var space = limit - _materialStorage.GetTotalMaterialAmount(ent, storage, true);
            if (space <= 0)
                return false;
            changed = Math.Min(changed, space);
        }

        _materialStorage.TryChangeMaterialAmount(ent, ent.Comp.ExtractedMaterial, changed);
        QueueDel(produce);
        return true;
    }
}
