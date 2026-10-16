using System.Text;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Server.GameObjects;

namespace Content.Server.Botany.Systems;

/// <summary>
/// The plant analyzer: scans trays, seeds, produce and grafts and shows a text report,
/// laid out like Monkestation's analyzer output.
/// </summary>
public sealed partial class PlantAnalyzerSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>(OnDoAfter);
    }

    private bool CanScan(EntityUid target)
    {
        return HasComp<PlantTrayComponent>(target)
               || HasComp<SeedComponent>(target)
               || HasComp<ProduceComponent>(target)
               || HasComp<PlantGraftComponent>(target);
    }

    private void OnAfterInteract(Entity<PlantAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !CanScan(target))
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.ScanDelay,
            new PlantAnalyzerDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnDoAfter(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        var (title, report) = BuildReport(target);
        _audio.PlayPvs(ent.Comp.ScanningEndSound, ent);
        _ui.SetUiState(ent.Owner, PlantAnalyzerUiKey.Key, new PlantAnalyzerState(title, report));
        _ui.OpenUi(ent.Owner, PlantAnalyzerUiKey.Key, args.User);
    }

    public (string Title, string Report) BuildReport(EntityUid target)
    {
        var sb = new StringBuilder();
        var title = Name(target);

        if (TryComp<PlantTrayComponent>(target, out var tray))
        {
            if (tray.Genome is { } genome)
            {
                title = _botany.GetPlantName(genome);
                sb.AppendLine(Loc.GetString("plant-analyzer-age", ("age", tray.Age)));
                sb.AppendLine(Loc.GetString("plant-analyzer-health", ("health", BotanySystem.FormatStat(tray.Health)), ("max", BotanySystem.FormatStat(genome.Endurance * 3))));
                sb.AppendLine(Loc.GetString("plant-analyzer-growth", ("growth", BotanySystem.FormatStat(tray.GrowthPercent))));
                sb.AppendLine(Loc.GetString($"plant-analyzer-state-{tray.State.ToString().ToLowerInvariant()}"));
                sb.AppendLine();
                _botany.AppendStatsReport(sb, genome);
                sb.AppendLine();
                _botany.AppendChemicalsReport(sb, genome);
            }
            else
            {
                sb.AppendLine(Loc.GetString("plant-analyzer-no-plant"));
            }

            sb.AppendLine();
            sb.AppendLine(Loc.GetString("plant-analyzer-medium-header"));
            sb.AppendLine(Loc.GetString("plant-analyzer-water", ("water", BotanySystem.FormatStat(tray.WaterPercent))));
            sb.AppendLine(Loc.GetString("plant-analyzer-weeds", ("weeds", BotanySystem.FormatStat(tray.Weeds)), ("max", PlantTrayComponent.MaxWeeds)));
            sb.AppendLine(Loc.GetString("plant-analyzer-pests", ("pests", BotanySystem.FormatStat(tray.Pests)), ("max", PlantTrayComponent.MaxPests)));
            sb.AppendLine(Loc.GetString("plant-analyzer-toxicity", ("toxicity", BotanySystem.FormatStat(tray.Toxicity))));
            if (tray.SelfSustain > 0)
                sb.AppendLine(Loc.GetString("plant-analyzer-self-sustain", ("percent", BotanySystem.FormatStat(tray.SelfSustain))));

            sb.AppendLine(Loc.GetString("plant-analyzer-medium-contains"));
            if (_solution.TryGetSolution(target, tray.SoilSolutionName, out _, out var soil) && soil.Contents.Count > 0)
            {
                foreach (var reagent in soil.Contents)
                {
                    sb.AppendLine(Loc.GetString("plant-analyzer-reagent-line",
                        ("amount", reagent.Quantity),
                        ("reagent", _botany.GetReagentName(reagent.Reagent.Prototype))));
                }
            }
            else
            {
                sb.AppendLine(Loc.GetString("plant-analyzer-no-reagents"));
            }

            return (title, sb.ToString().TrimEnd());
        }

        if (TryComp<PlantGraftComponent>(target, out var graft))
        {
            if (graft.Trait is { } trait)
                sb.AppendLine(Loc.GetString("plant-analyzer-graft-trait", ("trait", _botany.GetTraitGeneName(trait))));
            sb.AppendLine(Loc.GetString("plant-analyzer-stat-line", ("stat", _botany.GetStatName(PlantStat.Lifespan)), ("value", BotanySystem.FormatStat(graft.Lifespan))));
            sb.AppendLine(Loc.GetString("plant-analyzer-stat-line", ("stat", _botany.GetStatName(PlantStat.Endurance)), ("value", BotanySystem.FormatStat(graft.Endurance))));
            sb.AppendLine(Loc.GetString("plant-analyzer-stat-line", ("stat", _botany.GetStatName(PlantStat.Production)), ("value", BotanySystem.FormatStat(graft.Production))));
            sb.AppendLine(Loc.GetString("plant-analyzer-stat-line", ("stat", _botany.GetStatName(PlantStat.Yield)), ("value", BotanySystem.FormatStat(graft.Yield))));
            sb.AppendLine(Loc.GetString("plant-analyzer-stat-line", ("stat", _botany.GetStatName(PlantStat.WeedRate)), ("value", BotanySystem.FormatStat(graft.WeedRate))));
            sb.AppendLine(Loc.GetString("plant-analyzer-stat-line", ("stat", _botany.GetStatName(PlantStat.WeedChance)), ("value", BotanySystem.FormatStat(graft.WeedChance))));
            return (title, sb.ToString().TrimEnd());
        }

        var g = CompOrNull<SeedComponent>(target)?.Genome ?? CompOrNull<ProduceComponent>(target)?.Genome;
        if (g == null)
            return (title, Loc.GetString("plant-analyzer-no-genes"));

        _botany.AppendStatsReport(sb, g);
        sb.AppendLine();
        _botany.AppendChemicalsReport(sb, g);

        if (TryComp<ProduceComponent>(target, out var produce)
            && _solution.TryGetSolution(target, produce.TargetSolution, out _, out var contents))
        {
            sb.AppendLine();
            sb.AppendLine(Loc.GetString("plant-analyzer-contents-header", ("volume", contents.Volume), ("max", contents.MaxVolume)));
            foreach (var reagent in contents.Contents)
            {
                sb.AppendLine(Loc.GetString("plant-analyzer-reagent-line",
                    ("amount", reagent.Quantity),
                    ("reagent", _botany.GetReagentName(reagent.Reagent.Prototype))));
            }
        }

        return (title, sb.ToString().TrimEnd());
    }
}
