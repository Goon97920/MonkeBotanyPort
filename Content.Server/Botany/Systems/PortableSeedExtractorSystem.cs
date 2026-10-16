using System.Linq;
using Content.Shared.Botany.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;

namespace Content.Server.Botany.Systems;

/// <summary>
/// Monkestation's portaseeder: a plant bag that turns every plant inside it into seeds,
/// one seed per plant plus one (seedify with t_max 1). The seeds stay in the bag when they fit.
/// </summary>
public sealed partial class PortableSeedExtractorSystem : EntitySystem
{
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PortableSeedExtractorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<PortableSeedExtractorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<StorageComponent>(ent, out var storage))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("portable-seed-extractor-verb"),
            Priority = 2,
            Act = () => MakeSeeds((ent, ent.Comp, storage), user),
        });
    }

    private void MakeSeeds(Entity<PortableSeedExtractorComponent, StorageComponent> ent, EntityUid user)
    {
        var coords = Transform(ent).Coordinates;
        var count = 0;
        foreach (var item in ent.Comp2.Container.ContainedEntities.ToArray())
        {
            if (!TryComp<ProduceComponent>(item, out var produce) || produce.Genome is not { } genome)
                continue;

            // Gone before the seeds go in, so they can use its space.
            Del(item);
            count++;
            foreach (var seed in _botany.SeedifyExact(genome, coords, ent.Comp1.SeedsPerPlant))
            {
                _storage.Insert(ent, seed, out _, user: null, storageComp: ent.Comp2, playSound: false);
            }
        }

        _popup.PopupEntity(count > 0
            ? Loc.GetString("portable-seed-extractor-done", ("count", count))
            : Loc.GetString("portable-seed-extractor-nothing"), ent, user);
    }
}
