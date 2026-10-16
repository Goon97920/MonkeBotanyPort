using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Prototypes;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Botany.Systems;

/// <summary>
/// Monkestation seed extractor.
/// <list type="bullet">
/// <item>Produce in, rand(1,4) * multiplier + 1 seeds out, dropped where the player clicked.
/// Seeds keep the produce's genome. Seeds the extractor makes never go into its storage.</item>
/// <item>Seeds put in by hand or from a bag are stored as data (no entities) in piles of
/// matching name and core stats, and are taken back out one at a time through the UI.</item>
/// </list>
/// </summary>
public sealed partial class SeedExtractorSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SeedExtractorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SeedExtractorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<SeedExtractorComponent, SeedExtractorTakeMessage>(OnTake);
        SubscribeLocalEvent<SeedExtractorComponent, SeedExtractorScrapMessage>(OnScrap);
        SubscribeLocalEvent<SeedExtractorComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInteractUsing(Entity<SeedExtractorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_power.IsPowered(ent.Owner))
            return;

        if (TryComp<ProduceComponent>(args.Used, out var produce))
        {
            args.Handled = true;
            if (Extract(ent, (args.Used, produce), args.ClickLocation))
                _popup.PopupEntity(Loc.GetString("seed-extractor-component-interact-message", ("name", args.Used)), ent, args.User);
            return;
        }

        if (TryComp<SeedComponent>(args.Used, out var seed))
        {
            args.Handled = true;
            if (ent.Comp.StoredCount >= ent.Comp.MaxSeeds)
            {
                _popup.PopupEntity(Loc.GetString("seed-extractor-full", ("machine", ent.Owner)), ent, args.User);
                return;
            }

            if (!TryStore(ent, (args.Used, seed)))
            {
                _popup.PopupEntity(Loc.GetString("seed-extractor-cannot-store", ("seed", args.Used)), ent, args.User);
                return;
            }

            _audio.PlayPvs(ent.Comp.StoreSound, ent);
            _popup.PopupEntity(Loc.GetString("seed-extractor-stored", ("seed", args.Used), ("machine", ent.Owner)), ent, args.User);
            UpdateUi(ent);
            return;
        }

        if (!TryComp<StorageComponent>(args.Used, out var storage))
            return;

        // A bag: produce is turned into seeds on the floor, seeds go into storage.
        var extracted = 0;
        var stored = 0;
        var full = false;
        foreach (var item in storage.Container.ContainedEntities.ToArray())
        {
            if (TryComp<ProduceComponent>(item, out var p))
            {
                if (Extract(ent, (item, p), args.ClickLocation))
                    extracted++;
                continue;
            }

            if (!TryComp<SeedComponent>(item, out var s))
                continue;

            if (ent.Comp.StoredCount >= ent.Comp.MaxSeeds)
            {
                full = true;
                continue;
            }

            if (TryStore(ent, (item, s)))
                stored++;
        }

        if (extracted == 0 && stored == 0)
        {
            if (full)
            {
                args.Handled = true;
                _popup.PopupEntity(Loc.GetString("seed-extractor-full", ("machine", ent.Owner)), ent, args.User);
            }
            return;
        }

        args.Handled = true;
        if (extracted > 0)
            _popup.PopupEntity(Loc.GetString("seed-extractor-component-bag-message", ("count", extracted)), ent, args.User);
        if (stored > 0)
        {
            _audio.PlayPvs(ent.Comp.StoreSound, ent);
            _popup.PopupEntity(Loc.GetString(full ? "seed-extractor-bag-stored-full" : "seed-extractor-bag-stored",
                ("count", stored), ("machine", ent.Owner)), ent, args.User);
            UpdateUi(ent);
        }
    }

    /// <summary>
    /// Seeds land where the player clicked, so each batch gets its own pile.
    /// </summary>
    private bool Extract(Entity<SeedExtractorComponent> ent, Entity<ProduceComponent> produce, EntityCoordinates clickLocation)
    {
        if (produce.Comp.Genome is not { } genome)
            return false;

        var coords = clickLocation.IsValid(EntityManager) ? clickLocation : Transform(ent).Coordinates;
        _botany.Seedify(genome, coords, ent.Comp.SeedMultiplier);
        _audio.PlayPvs(ent.Comp.ExtractSound, ent);
        QueueDel(produce);
        return true;
    }

    #region Storage

    /// <summary>Monkestation generate_seed_hash: name plus core stats.</summary>
    private static string GetPileKey(string name, PlantGenome genome)
    {
        return $"{name};{genome.Lifespan};{genome.Endurance};{genome.Maturation};{genome.Production};{genome.Yield};{genome.Potency}";
    }

    private bool TryStore(Entity<SeedExtractorComponent> ent, Entity<SeedComponent> seed)
    {
        if (seed.Comp.Sample || seed.Comp.Genome is not { } genome || TerminatingOrDeleted(seed))
            return false;

        var name = Name(seed);
        var key = GetPileKey(name, genome);
        if (!ent.Comp.Piles.TryGetValue(key, out var pile))
        {
            pile = new SeedExtractorPile
            {
                Name = name,
                Species = genome.Species,
            };
            ent.Comp.Piles[key] = pile;
            ent.Comp.PileOrder.Add(key);
        }

        pile.Genomes.Add(genome);
        ent.Comp.StoredCount++;

        // The genome now lives in the pile; detach it before the entity goes away.
        seed.Comp.Genome = null;
        QueueDel(seed);
        return true;
    }

    private void OnTake(Entity<SeedExtractorComponent> ent, ref SeedExtractorTakeMessage args)
    {
        if (!ent.Comp.Piles.TryGetValue(args.Key, out var pile) || pile.Genomes.Count == 0)
            return;

        var genome = pile.Genomes[0];
        pile.Genomes.RemoveAt(0);
        ent.Comp.StoredCount--;
        if (pile.Genomes.Count == 0)
            RemovePile(ent, args.Key);

        if (_botany.SpawnSeed(genome, Transform(ent).Coordinates) is { } seed)
        {
            if (_hands.TryPickupAnyHand(args.Actor, seed))
                _popup.PopupEntity(Loc.GetString("seed-extractor-take", ("seed", seed)), ent, args.Actor);
            else
                _popup.PopupEntity(Loc.GetString("seed-extractor-take-floor", ("seed", seed)), ent, args.Actor);
        }

        UpdateUi(ent);
    }

    private void OnScrap(Entity<SeedExtractorComponent> ent, ref SeedExtractorScrapMessage args)
    {
        if (!ent.Comp.Piles.TryGetValue(args.Key, out var pile))
            return;

        ent.Comp.StoredCount -= pile.Genomes.Count;
        RemovePile(ent, args.Key);
        UpdateUi(ent);
    }

    private static void RemovePile(Entity<SeedExtractorComponent> ent, string key)
    {
        ent.Comp.Piles.Remove(key);
        ent.Comp.PileOrder.Remove(key);
    }

    /// <summary>
    /// Deconstructing the machine drops its seeds back out as entities, like Monkestation's contents.
    /// </summary>
    private void OnShutdown(Entity<SeedExtractorComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.StoredCount == 0 || TerminatingOrDeleted(Transform(ent).MapUid))
            return;

        var coords = Transform(ent).Coordinates;
        foreach (var key in ent.Comp.PileOrder)
        {
            foreach (var genome in ent.Comp.Piles[key].Genomes)
            {
                _botany.SpawnSeed(genome, coords);
            }
        }

        ent.Comp.Piles.Clear();
        ent.Comp.PileOrder.Clear();
        ent.Comp.StoredCount = 0;
    }

    #endregion

    #region UI

    private void OnUiOpened(Entity<SeedExtractorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    /// <summary>
    /// The state is one entry per pile, so it stays small however many seeds are stored.
    /// Only sent while someone has the window open.
    /// </summary>
    private void UpdateUi(Entity<SeedExtractorComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, SeedExtractorUiKey.Key))
            return;

        var entries = new List<SeedExtractorPileEntry>(ent.Comp.PileOrder.Count);
        foreach (var key in ent.Comp.PileOrder)
        {
            var pile = ent.Comp.Piles[key];
            var genome = pile.Genomes[0];
            var entry = new SeedExtractorPileEntry
            {
                Key = key,
                Name = pile.Name,
                Amount = pile.Genomes.Count,
                Potency = genome.Potency,
                Yield = genome.Yield,
                Production = genome.Production,
                Endurance = genome.Endurance,
                Lifespan = genome.Lifespan,
                Maturation = genome.Maturation,
            };

            if (_proto.TryIndex(genome.Species, out var species))
                entry.SeedRsi = species.PlantRsi.ToString();

            foreach (var trait in genome.Traits)
            {
                entry.Traits.Add(_botany.GetTraitGeneName(trait));
            }

            foreach (var chem in genome.Chemicals)
            {
                entry.Chemicals.Add(Loc.GetString("seed-extractor-chemical",
                    ("reagent", _botany.GetReagentName(chem.Reagent)),
                    ("rate", MathF.Round(chem.Rate * 100, 1))));
            }

            foreach (var mutationId in genome.Mutations)
            {
                if (_proto.TryIndex(mutationId, out var mutation)
                    && _proto.TryIndex(mutation.Result, out PlantSpeciesPrototype? result))
                    entry.Mutations.Add(Loc.GetString(result.Name));
            }

            entries.Add(entry);
        }

        _ui.SetUiState(ent.Owner, SeedExtractorUiKey.Key,
            new SeedExtractorState(entries, ent.Comp.StoredCount, ent.Comp.MaxSeeds));
    }

    #endregion
}
