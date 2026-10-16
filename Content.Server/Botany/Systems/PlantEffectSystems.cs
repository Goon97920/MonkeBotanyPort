using Content.Shared.Botany.Components;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Botany;
using Robust.Shared.Random;

namespace Content.Server.Botany.Systems;

// Systems for the tray reagent effects in Content.Shared/EntityEffects/Effects/Botany.

public sealed partial class PlantAdjustStatEffectSystem : EntityEffectSystem<PlantTrayComponent, PlantAdjustStat>
{
    [Dependency] private IRobustRandom _random = default!;

    protected override void Effect(Entity<PlantTrayComponent> entity, ref EntityEffectEvent<PlantAdjustStat> args)
    {
        if (entity.Comp.Genome is not { } genome)
            return;

        var amount = PlantEffectHelpers.Amount(args.Effect, args.Scale, _random);
        genome.AdjustStat(args.Effect.Stat, amount);
        Dirty(entity);
    }
}

public sealed partial class PlantAdjustHealthEffectSystem : EntityEffectSystem<PlantTrayComponent, PlantAdjustHealth>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PlantTraySystem _tray = default!;

    protected override void Effect(Entity<PlantTrayComponent> entity, ref EntityEffectEvent<PlantAdjustHealth> args)
    {
        if (entity.Comp.Genome == null)
            return;

        _tray.AdjustHealth(entity, PlantEffectHelpers.Amount(args.Effect, args.Scale, _random));
    }
}

public sealed partial class PlantTrainTraitEffectSystem : EntityEffectSystem<PlantTrayComponent, PlantTrainTrait>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private BotanySystem _botany = default!;

    protected override void Effect(Entity<PlantTrayComponent> entity, ref EntityEffectEvent<PlantTrainTrait> args)
    {
        if (entity.Comp.Genome is not { } genome)
            return;

        _botany.TrainTrait(genome, args.Effect.Trait, PlantEffectHelpers.Amount(args.Effect, args.Scale, _random));
        Dirty(entity);
    }
}

public sealed partial class PlantTrayAdjustEffectSystem : EntityEffectSystem<PlantTrayComponent, PlantTrayAdjust>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PlantTraySystem _tray = default!;

    protected override void Effect(Entity<PlantTrayComponent> entity, ref EntityEffectEvent<PlantTrayAdjust> args)
    {
        _tray.AdjustTrayAttribute(entity, args.Effect.Attribute, PlantEffectHelpers.Amount(args.Effect, args.Scale, _random));
    }
}

public sealed partial class PlantTraySpeedUpEffectSystem : EntityEffectSystem<PlantTrayComponent, PlantTraySpeedUp>
{
    [Dependency] private PlantTraySystem _tray = default!;

    protected override void Effect(Entity<PlantTrayComponent> entity, ref EntityEffectEvent<PlantTraySpeedUp> args)
    {
        _tray.SpeedUp(entity, args.Effect.Time);
    }
}

internal static class PlantEffectHelpers
{
    public static float Amount<T>(T effect, float scale, IRobustRandom random) where T : BasePlantAmountEffect<T>
    {
        return effect.GetAmount(scale, random.GetRandom());
    }
}
