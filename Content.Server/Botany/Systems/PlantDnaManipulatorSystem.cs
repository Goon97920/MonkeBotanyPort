using Content.Server.Power.EntitySystems;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Botany.Systems;

/// <summary>
/// The plant DNA manipulator (Monkestation gene_modder.dm). Works on the seed in its seed slot
/// and the data disk in its disk slot:
/// <list type="bullet">
/// <item>Extract copies a gene to the disk and destroys the seed. Core genes are degraded to the
/// manipulator's limits. Trait mutability flags are ignored, so "immutable" traits can be extracted.</item>
/// <item>Remove deletes a chemical or trait gene.</item>
/// <item>Insert adds the disk's chemical or trait gene, if the seed can take it.</item>
/// <item>Replace overwrites a core stat with the disk's value.</item>
/// </list>
/// </summary>
public sealed partial class PlantDnaManipulatorSystem : EntitySystem
{
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>Core genes shown by the manipulator (Monkestation leaves maturation out).</summary>
    private static readonly PlantStat[] CoreGenes =
    [
        PlantStat.Potency,
        PlantStat.Yield,
        PlantStat.Production,
        PlantStat.Endurance,
        PlantStat.Lifespan,
        PlantStat.WeedRate,
        PlantStat.WeedChance,
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantDnaManipulatorComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<PlantDnaManipulatorComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<PlantDnaManipulatorComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<PlantDnaManipulatorComponent, PlantDnaExtractMessage>(OnExtract);
        SubscribeLocalEvent<PlantDnaManipulatorComponent, PlantDnaRemoveMessage>(OnRemove);
        SubscribeLocalEvent<PlantDnaManipulatorComponent, PlantDnaInsertMessage>(OnInsert);
        SubscribeLocalEvent<PlantDnaManipulatorComponent, PlantDnaReplaceMessage>(OnReplace);

        SubscribeLocalEvent<PlantGeneDiskComponent, UseInHandEvent>(OnDiskUseInHand);
        SubscribeLocalEvent<PlantGeneDiskComponent, MapInitEvent>(OnDiskMapInit);
    }

    /// <summary>Prefilled disks (uplink disks) get their "gene (plant data disk)" name.</summary>
    private void OnDiskMapInit(Entity<PlantGeneDiskComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Gene is { } gene)
            _botany.SetDiskName(ent, gene);
    }

    private void OnDiskUseInHand(Entity<PlantGeneDiskComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.ReadOnly = !ent.Comp.ReadOnly;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.ReadOnly ? "plant-disk-protect-on" : "plant-disk-protect-off"), ent, args.User);
    }

    private void OnInserted(Entity<PlantDnaManipulatorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUi(ent);
    }

    private void OnRemoved(Entity<PlantDnaManipulatorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUi(ent);
    }

    private void OnUiOpened(Entity<PlantDnaManipulatorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private Entity<SeedComponent>? GetSeed(Entity<PlantDnaManipulatorComponent> ent)
    {
        return _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.SeedSlotId) is { } item && TryComp<SeedComponent>(item, out var seed)
            ? (item, seed)
            : null;
    }

    private Entity<PlantGeneDiskComponent>? GetDisk(Entity<PlantDnaManipulatorComponent> ent)
    {
        return _itemSlots.GetItemOrNull(ent.Owner, ent.Comp.DiskSlotId) is { } item && TryComp<PlantGeneDiskComponent>(item, out var disk)
            ? (item, disk)
            : null;
    }

    private bool CanOperate(Entity<PlantDnaManipulatorComponent> ent)
    {
        return _power.IsPowered(ent.Owner);
    }

    #region Operations

    private void OnExtract(Entity<PlantDnaManipulatorComponent> ent, ref PlantDnaExtractMessage args)
    {
        if (!CanOperate(ent) || GetSeed(ent) is not { } seed || seed.Comp.Genome is not { } genome
            || GetDisk(ent) is not { } disk || disk.Comp.ReadOnly)
            return;

        PlantDiskGene? gene = null;
        switch (args.Kind)
        {
            case PlantGeneKind.Core:
                var stat = (PlantStat) args.Index;
                if (Array.IndexOf(CoreGenes, stat) < 0)
                    return;
                gene = new PlantDiskGene { Stat = stat, StatValue = DegradeCoreGene(ent.Comp, stat, genome.GetStat(stat)) };
                break;
            case PlantGeneKind.Chemical:
                if (args.Index < 0 || args.Index >= genome.Chemicals.Count)
                    return;
                gene = new PlantDiskGene { Chemical = genome.Chemicals[args.Index].Clone() };
                break;
            case PlantGeneKind.Trait:
                if (args.Index < 0 || args.Index >= genome.Traits.Count)
                    return;
                gene = new PlantDiskGene { Trait = genome.Traits[args.Index].Clone() };
                break;
        }

        if (gene == null)
            return;

        disk.Comp.Gene = gene;
        Dirty(disk);
        _botany.SetDiskName(disk, gene);

        // The sample is destroyed in the process.
        QueueDel(seed);
        _popup.PopupEntity(Loc.GetString("plant-dna-extracted", ("gene", _botany.GetDiskGeneName(gene))), ent, args.Actor);
        UpdateUi(ent);
    }

    /// <summary>
    /// Monkestation tier 4 limits: potency uncapped, yield at most 10, endurance and lifespan at most
    /// 110, weed rate at least 0 and weed chance at least 3. Production is uncapped at tier 4.
    /// </summary>
    public static float DegradeCoreGene(PlantDnaManipulatorComponent comp, PlantStat stat, float value)
    {
        return stat switch
        {
            PlantStat.Potency => comp.MaxPotency is { } max ? MathF.Min(value, max) : value,
            PlantStat.Yield => MathF.Min(value, comp.MaxYield),
            PlantStat.Endurance or PlantStat.Lifespan => MathF.Min(value, comp.MaxEndurance),
            PlantStat.WeedRate => MathF.Max(value, comp.MinWeedRate),
            PlantStat.WeedChance => MathF.Max(value, comp.MinWeedChance),
            _ => value,
        };
    }

    private void OnRemove(Entity<PlantDnaManipulatorComponent> ent, ref PlantDnaRemoveMessage args)
    {
        if (!CanOperate(ent) || GetSeed(ent) is not { } seed || seed.Comp.Genome is not { } genome)
            return;

        string? name = null;
        switch (args.Kind)
        {
            case PlantGeneKind.Chemical when args.Index >= 0 && args.Index < genome.Chemicals.Count:
                name = _botany.GetChemicalGeneName(genome.Chemicals[args.Index]);
                genome.Chemicals.RemoveAt(args.Index);
                break;
            case PlantGeneKind.Trait when args.Index >= 0 && args.Index < genome.Traits.Count:
                name = _botany.GetTraitGeneName(genome.Traits[args.Index]);
                genome.Traits.RemoveAt(args.Index);
                break;
        }

        if (name == null)
            return;

        MarkExperimental(seed);
        _popup.PopupEntity(Loc.GetString("plant-dna-removed", ("gene", name)), ent, args.Actor);
        UpdateUi(ent);
    }

    private void OnInsert(Entity<PlantDnaManipulatorComponent> ent, ref PlantDnaInsertMessage args)
    {
        if (!CanOperate(ent) || GetSeed(ent) is not { } seed || seed.Comp.Genome is not { } genome
            || GetDisk(ent) is not { } disk || disk.Comp.Gene is not { } gene || !CanInsert(seed.Comp, gene))
            return;

        if (gene.Chemical is { } chem)
            _botany.AddOrUpgradeChemical(genome, chem);
        else if (gene.Trait is { } trait)
            genome.Traits.Add(trait.Clone());
        else
            return;

        MarkExperimental(seed);
        _popup.PopupEntity(Loc.GetString("plant-dna-inserted", ("gene", _botany.GetDiskGeneName(gene))), ent, args.Actor);
        UpdateUi(ent);
    }

    private void OnReplace(Entity<PlantDnaManipulatorComponent> ent, ref PlantDnaReplaceMessage args)
    {
        if (!CanOperate(ent) || GetSeed(ent) is not { } seed || seed.Comp.Genome is not { } genome
            || GetDisk(ent) is not { } disk || disk.Comp.Gene is not { Stat: { } stat } gene
            || (int) stat != args.Stat)
            return;

        genome.SetStat(stat, gene.StatValue);
        MarkExperimental(seed);
        _popup.PopupEntity(Loc.GetString("plant-dna-replaced", ("gene", _botany.GetDiskGeneName(gene))), ent, args.Actor);
        UpdateUi(ent);
    }

    private bool CanInsert(SeedComponent seed, PlantDiskGene gene)
    {
        if (seed.Sample || seed.Genome is not { } genome)
            return false;

        if (gene.Chemical is { } chem)
            return _botany.CanAddChemical(genome, chem);
        if (gene.Trait is { } trait)
            return _botany.CanAddTrait(genome, trait.Trait);
        return false;
    }

    private void MarkExperimental(Entity<SeedComponent> seed)
    {
        if (seed.Comp.Genome is { } genome)
            genome.Experimental = true;

        Dirty(seed);
        _botany.UpdateSeedName(seed);
    }

    #endregion

    private void UpdateUi(Entity<PlantDnaManipulatorComponent> ent)
    {
        var state = new PlantDnaManipulatorState();
        var seed = GetSeed(ent);
        var disk = GetDisk(ent);

        if (disk is { } d)
        {
            state.HasDisk = true;
            state.DiskReadOnly = d.Comp.ReadOnly;
            state.DiskHasGene = d.Comp.Gene != null;
            state.DiskName = d.Comp.Gene is { } g ? _botany.GetDiskGeneName(g) : Loc.GetString("plant-dna-empty-disk");
        }

        if (seed is { } s && s.Comp.Genome is { } genome)
        {
            state.SeedName = Name(s);
            var canExtract = disk is { } dk && !dk.Comp.ReadOnly;
            var diskGene = disk?.Comp.Gene;

            foreach (var stat in CoreGenes)
            {
                var value = genome.GetStat(stat);
                var degraded = DegradeCoreGene(ent.Comp, stat, value);
                state.Genes.Add(new PlantGeneRow
                {
                    Kind = PlantGeneKind.Core,
                    Index = (int) stat,
                    Name = $"{_botany.GetStatName(stat)} {BotanySystem.FormatStat(value)}",
                    CanExtract = canExtract,
                    CanReplace = diskGene?.Stat == stat,
                    ExtractWarning = Math.Abs(degraded - value) > 0.0001f
                        ? Loc.GetString("plant-dna-extract-degrade", ("value", BotanySystem.FormatStat(degraded)))
                        : null,
                });
            }

            for (var i = 0; i < genome.Chemicals.Count; i++)
            {
                state.Genes.Add(new PlantGeneRow
                {
                    Kind = PlantGeneKind.Chemical,
                    Index = i,
                    Name = _botany.GetChemicalGeneName(genome.Chemicals[i]),
                    CanExtract = canExtract,
                    CanRemove = true,
                });
            }

            for (var i = 0; i < genome.Traits.Count; i++)
            {
                state.Genes.Add(new PlantGeneRow
                {
                    Kind = PlantGeneKind.Trait,
                    Index = i,
                    Name = _botany.GetTraitGeneName(genome.Traits[i]),
                    CanExtract = canExtract,
                    CanRemove = true,
                });
            }

            if (diskGene is { Stat: null } && CanInsert(s.Comp, diskGene))
            {
                state.CanInsert = true;
                state.InsertLabel = Loc.GetString("plant-dna-insert", ("gene", _botany.GetDiskGeneName(diskGene)));
            }
        }

        _ui.SetUiState(ent.Owner, PlantDnaManipulatorUiKey.Key, state);
    }
}
