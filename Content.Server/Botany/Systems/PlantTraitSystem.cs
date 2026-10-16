using Content.Shared.Body;
using Content.Shared.Nutrition;
using System.Numerics;
using Content.Server.Chemistry.Components;
using Content.Server.Electrocution;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Botany;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Slippery;
using Content.Shared.Stacks;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Botany.Systems;

/// <summary>
/// Plant trait behaviour on harvested produce. Ported from Monkestation's plant_genes.dm:
/// Liquid Contents, Slippery Skin, Hypodermic Prickles, Prickly Adhesion, Bluespace Activity,
/// Gaseous Decomposition, Electrical Activity, Capacitive Cell Production, Hallucinatory Feedback
/// and bioluminescence, plus the syndicate hypodermic needles.
/// </summary>
public sealed partial class PlantTraitSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ElectrocutionSystem _electrocution = default!;
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private IngestionSystem _ingestion = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SecondaryStomachSystem _secondaryStomach = default!;
    [Dependency] private StepTriggerSystem _stepTrigger = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PointLightSystem _light = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private TransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> HardsuitTag = "Hardsuit";
    private static readonly ProtoId<TagPrototype> CableCoilTag = "CableCoil";
    private static readonly ProtoId<DamageTypePrototype> Piercing = "Piercing";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> Lube = "SpaceLube";
    private static readonly EntProtoId PlantBattery = "PowerCellPotato";
    private static readonly EntProtoId SmokePrototype = "Smoke";
    private static readonly SoundSpecifier LaughSound = new SoundCollectionSpecifier("ClownRecorder");

    /// <summary>Cell charge of a 100 potency plant battery (Monkestation: potency * 0.005 * standard cell).</summary>
    private const float BatteryChargePerPotency = 3.6f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantSquashComponent, ThrowDoHitEvent>(OnSquashThrowHit);
        SubscribeLocalEvent<PlantSquashComponent, UseInHandEvent>(OnSquashUseInHand);
        SubscribeLocalEvent<PlantSquashComponent, PlantSlippedOnEvent>(OnSquashSlipped);

        SubscribeLocalEvent<PlantSlipperyComponent, SlipEvent>(OnSlip);

        SubscribeLocalEvent<PlantStingingComponent, ThrowDoHitEvent>(OnStingThrowHit);
        SubscribeLocalEvent<PlantStingingComponent, PlantSlippedOnEvent>(OnStingSlipped);

        SubscribeLocalEvent<PlantStickyComponent, ThrowDoHitEvent>(OnStickyThrowHit);

        SubscribeLocalEvent<PlantSyndicateNeedlesComponent, MeleeHitEvent>(OnNeedlesMeleeHit);

        SubscribeLocalEvent<PlantTeleportComponent, PlantSquashedEvent>(OnTeleportSquashed);
        SubscribeLocalEvent<PlantTeleportComponent, PlantSlippedOnEvent>(OnTeleportSlipped);

        SubscribeLocalEvent<PlantSmokeComponent, PlantSquashedEvent>(OnSmokeSquashed);

        SubscribeLocalEvent<PlantElectricComponent, PlantSquashedEvent>(OnElectricSquashed);
        SubscribeLocalEvent<PlantElectricComponent, PlantSlippedOnEvent>(OnElectricSlipped);

        SubscribeLocalEvent<PlantBatteryComponent, InteractUsingEvent>(OnBatteryInteractUsing);

        SubscribeLocalEvent<PlantLaughterComponent, PlantSlippedOnEvent>(OnLaughterSlipped);

        SubscribeLocalEvent<ProduceComponent, BeforeIngestedEvent>(OnProduceBeforeIngested, after: [typeof(IngestionSystem)]);
        SubscribeLocalEvent<ProduceComponent, IngestedEvent>(OnProduceIngested, before: [typeof(IngestionSystem)]);
    }

    private static float CappedPotency(PlantGenome genome) => MathF.Min(genome.Potency, 100f);

    private PlantGenome? GetGenome(EntityUid uid) => TryComp<ProduceComponent>(uid, out var p) ? p.Genome : null;

    #region Applying traits

    /// <summary>
    /// Adds trait components to freshly grown produce and sets up glow, slipperiness and bite size.
    /// </summary>
    public void ApplyProduceTraits(Entity<ProduceComponent> ent)
    {
        if (ent.Comp.Genome is not { } genome)
            return;

        var oneBite = false;
        PlantTraitPrototype? glow = null;

        foreach (var gene in genome.Traits)
        {
            if (!_proto.TryIndex(gene.Trait, out var trait))
                continue;

            if (trait.ProduceComponents.Count > 0)
                EntityManager.AddComponents(ent, trait.ProduceComponents, removeExisting: false);

            if (trait.GlowColor != null)
                glow = trait;
            if (trait.OneBite)
                oneBite = true;
        }

        if (glow != null)
            ApplyGlow(ent, glow, genome);

        if (HasComp<PlantSlipperyComponent>(ent))
            SetupSlippery(ent, genome);

        if (HasComp<PlantStickyComponent>(ent))
            SetupSticky(ent, genome);

        if (HasComp<PlantSyndicateNeedlesComponent>(ent))
            EnsureComp<MeleeWeaponComponent>(ent);

        SetBiteSize(ent, genome, oneBite);
    }

    /// <summary>Monkestation glow: radius 1.4 + potency * rate, power potency * (rate + 0.01).</summary>
    public void ApplyGlow(EntityUid uid, PlantTraitPrototype trait, PlantGenome genome)
    {
        var potency = CappedPotency(genome);
        var light = _light.EnsureLight(uid);
        _light.SetRadius(uid, 1.4f + potency * trait.GlowRate, light);
        _light.SetEnergy(uid, Math.Clamp(potency * (trait.GlowRate + 0.01f), 0.1f, 3f), light);
        _light.SetColor(uid, trait.GlowColor ?? Color.White, light);
        _light.SetEnabled(uid, true, light);
    }

    /// <summary>
    /// Monkestation bite size: 1 + round(max(potency * 0.05, 1) * volume * 0.01). Large Bites eats it all at once.
    /// </summary>
    private void SetBiteSize(Entity<ProduceComponent> ent, PlantGenome genome, bool oneBite)
    {
        if (!TryComp<EdibleComponent>(ent, out var edible))
            return;

        if (oneBite)
        {
            _ingestion.SetTransferAmount((ent, edible), null);
            return;
        }

        var volume = 100f;
        if (_solution.TryGetSolution(ent.Owner, ent.Comp.TargetSolution, out _, out var solution))
            volume = solution.MaxVolume.Float();

        var bite = GetBiteSize(genome.Potency, volume);
        _ingestion.SetTransferAmount((ent, edible), FixedPoint2.New(Math.Min(bite, FixedPoint2.MaxValue.Float())));
    }

    /// <summary>Monkestation bite_consumption: 1 + round(max(potency * 0.05, 1) * volume * 0.01).</summary>
    public static float GetBiteSize(float potency, float volume)
    {
        return 1f + MathF.Round(MathF.Max(potency * 0.05f, 1f) * (volume * 0.01f), MidpointRounding.AwayFromZero);
    }

    private void SetupSlippery(Entity<ProduceComponent> ent, PlantGenome genome)
    {
        // Banana-style plants slip through their peel instead.
        if (TryComp<EdibleComponent>(ent, out var edible) && edible.Trash.Count > 0)
            return;

        var rate = Comp<PlantSlipperyComponent>(ent).Rate;
        var deciseconds = CappedPotency(genome) * rate;
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.TargetSolution, out _, out var solution)
            || solution.GetTotalPrototypeQuantity(Lube) <= 0)
        {
            deciseconds /= 3f;
        }

        deciseconds = MathF.Min(deciseconds, 140f);

        var slippery = EnsureComp<SlipperyComponent>(ent);
        slippery.SlipData.KnockdownTime = TimeSpan.FromSeconds(deciseconds / 10f);
        slippery.SlipData.StunTime = TimeSpan.FromSeconds(Math.Min(0.5f, deciseconds / 10f));
        Dirty(ent, slippery);

        var step = EnsureComp<StepTriggerComponent>(ent);
        _stepTrigger.SetIntersectRatio(ent, 0.2f, step);

        _fixtures.TryCreateFixture(ent,
            new PhysShapeAabb(0.2f),
            "slips",
            hard: false,
            collisionLayer: (int) CollisionGroup.SlipLayer);
    }

    private void SetupSticky(Entity<ProduceComponent> ent, PlantGenome genome)
    {
        var embed = EnsureComp<EmbeddableProjectileComponent>(ent);
        embed.MinimumSpeed = 0f;
        embed.EmbedOnThrow = true;
        Dirty(ent, embed);

        // Prickly Adhesion only injects together with Hypodermic Prickles.
        if (!HasComp<PlantStingingComponent>(ent))
            return;

        var sticky = Comp<PlantStickyComponent>(ent);
        var inject = EnsureComp<SolutionInjectWhileEmbeddedComponent>(ent);
        inject.Solution = ent.Comp.TargetSolution;
        inject.TransferAmount = FixedPoint2.New(sticky.InjectAmount);
        inject.UpdateInterval = sticky.InjectInterval;
        inject.NextUpdate = _timing.CurTime + sticky.InjectInterval;
        inject.PierceArmor = false;
    }

    #endregion

    #region Eating

    private void OnProduceBeforeIngested(Entity<ProduceComponent> ent, ref BeforeIngestedEvent args)
    {
        if (args.Cancelled || args.Solution is not { } solution || ent.Comp.Genome == null)
            return;

        // Monkestation has no stomach cap: let the whole bite through. Whatever doesn't fit in the
        // stomach goes to the secondary stomach (see OnProduceIngested).
        var wanted = FixedPoint2.Min(args.Transfer, solution.Volume);
        if (wanted > args.Max)
            args.TryNewMaximum(wanted);
    }

    private void OnProduceIngested(Entity<ProduceComponent> ent, ref IngestedEvent args)
    {
        if (ent.Comp.Genome == null)
            return;

        var available = GetStomachSpace(args.Target);
        var overflow = args.Split.Volume - available;
        if (overflow <= 0)
            return;

        // The rest waits in a secondary stomach and trickles into the real one as it empties.
        var extra = args.Split.SplitSolution(overflow);
        _secondaryStomach.Add(args.Target, extra);
    }

    private FixedPoint2 GetStomachSpace(EntityUid eater)
    {
        var best = FixedPoint2.Zero;
        if (!_body.TryGetOrgansWithComponent<StomachComponent>(eater, out var stomachs))
            return best;

        foreach (var stomach in stomachs)
        {
            if (!_solution.TryGetSolution(stomach.Owner, StomachSystem.DefaultSolutionName, out _, out var sol))
                continue;
            if (sol.AvailableVolume > best)
                best = sol.AvailableVolume;
        }

        return best;
    }

    #endregion

    #region Injection

    /// <summary>
    /// Injects up to <paramref name="amount"/> of the produce's reagents into a living target.
    /// Hardsuits block it unless <paramref name="pierceArmor"/>.
    /// </summary>
    private bool TryInject(EntityUid produce, EntityUid target, float amount, bool pierceArmor, EntityUid? user = null)
    {
        if (amount <= 0 || !HasComp<BloodstreamComponent>(target))
            return false;

        if (!TryComp<ProduceComponent>(produce, out var produceComp)
            || !_solution.TryGetSolution(produce, produceComp.TargetSolution, out var soln, out var solution)
            || solution.Volume <= 0)
            return false;

        if (!pierceArmor
            && _inventory.TryGetSlotEntity(target, "outerClothing", out var suit)
            && _tag.HasTag(suit.Value, HardsuitTag))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("plant-prickles-blocked", ("target", target)), target, user.Value, PopupType.SmallCaution);
            return false;
        }

        var split = _solution.SplitSolution(soln.Value, FixedPoint2.New(amount));
        if (split.Volume <= 0)
            return false;

        if (!_bloodstream.TryAddToBloodstream(target, split))
            return false;

        _popup.PopupEntity(Loc.GetString("plant-prickles-pricked", ("plant", produce)), target, target, PopupType.SmallCaution);
        return true;
    }

    /// <summary>Hypodermic Prickles amount: 50 * potency / (2000 + potency).</summary>
    private static float PricklesAmount(PlantGenome genome, float max, float midpoint) =>
        BotanySystem.QpSigmoid(midpoint, max, genome.Potency);

    private void OnStingThrowHit(Entity<PlantStingingComponent> ent, ref ThrowDoHitEvent args)
    {
        if (GetGenome(ent) is not { } genome)
            return;

        TryInject(ent, args.Target, PricklesAmount(genome, ent.Comp.MaxInjection, ent.Comp.Midpoint), false, args.Component.Thrower);
    }

    private void OnStingSlipped(Entity<PlantStingingComponent> ent, ref PlantSlippedOnEvent args)
    {
        if (GetGenome(ent) is not { } genome)
            return;

        TryInject(ent, args.Slipped, PricklesAmount(genome, ent.Comp.MaxInjection, ent.Comp.Midpoint), false);
    }

    private void OnNeedlesMeleeHit(Entity<PlantSyndicateNeedlesComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || GetGenome(ent) is not { } genome)
            return;

        var amount = PricklesAmount(genome, ent.Comp.MaxInjection, ent.Comp.Midpoint);
        foreach (var target in args.HitEntities)
        {
            if (target == args.User)
                continue;
            TryInject(ent, target, amount, pierceArmor: true, args.User);
        }
    }

    /// <summary>Prickly Adhesion: thrown damage of 50 * potency / (1000 + potency).</summary>
    private void OnStickyThrowHit(Entity<PlantStickyComponent> ent, ref ThrowDoHitEvent args)
    {
        if (GetGenome(ent) is not { } genome)
            return;

        var force = BotanySystem.QpSigmoid(1000f, 50f, genome.Potency);
        if (force <= 0)
            return;

        var type = HasComp<PlantStingingComponent>(ent) ? Piercing : Blunt;
        var damage = new DamageSpecifier(_proto.Index(type), FixedPoint2.New(force));
        _damageable.TryChangeDamage(args.Target, damage, origin: args.Component.Thrower);
    }

    #endregion

    #region Squash and slip

    private void OnSquashThrowHit(Entity<PlantSquashComponent> ent, ref ThrowDoHitEvent args)
    {
        Squash(ent, args.Target, args.Component.Thrower);
    }

    private void OnSquashUseInHand(Entity<PlantSquashComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Squash(ent, args.User, args.User);
    }

    private void OnSquashSlipped(Entity<PlantSquashComponent> ent, ref PlantSlippedOnEvent args)
    {
        Squash(ent, args.Slipped, null);
    }

    private void OnSlip(Entity<PlantSlipperyComponent> ent, ref SlipEvent args)
    {
        var ev = new PlantSlippedOnEvent(args.Slipped);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>
    /// Bursts the produce: splashes its reagents on the target's tile and deletes it.
    /// </summary>
    public void Squash(EntityUid produce, EntityUid? target, EntityUid? user)
    {
        if (TerminatingOrDeleted(produce))
            return;

        var coords = target is { } t && !TerminatingOrDeleted(t)
            ? Transform(t).Coordinates
            : Transform(produce).Coordinates;

        _popup.PopupCoordinates(Loc.GetString("plant-squashed", ("plant", produce)), coords, PopupType.Small);

        var ev = new PlantSquashedEvent(target);
        RaiseLocalEvent(produce, ref ev);

        if (TryComp<ProduceComponent>(produce, out var produceComp)
            && _solution.TryGetSolution(produce, produceComp.TargetSolution, out var soln, out var solution)
            && solution.Volume > 0)
        {
            soln.Value.Comp.Solution.CanReact = true;
            var spilled = _solution.SplitSolution(soln.Value, solution.Volume);
            _puddle.TrySplashSpillAt(produce, coords, spilled, out _, user: user);
        }

        QueueDel(produce);
    }

    private void OnTeleportSquashed(Entity<PlantTeleportComponent> ent, ref PlantSquashedEvent args)
    {
        if (args.Target is { } target && HasComp<BodyComponent>(target))
            RandomTeleport(ent, target);
    }

    private void OnTeleportSlipped(Entity<PlantTeleportComponent> ent, ref PlantSlippedOnEvent args)
    {
        if (HasComp<PlantSquashComponent>(ent))
            return; // Squashing handles it.

        RandomTeleport(ent, args.Slipped);
        _popup.PopupEntity(Loc.GetString("plant-teleport-slip"), args.Slipped, args.Slipped, PopupType.MediumCaution);

        // Monkestation: half the time the plant comes along, otherwise it's used up.
        if (_random.Prob(0.5f))
            RandomTeleport(ent, ent);
        else
            QueueDel(ent);
    }

    /// <summary>Teleports within max(round(potency / 20), 1) tiles.</summary>
    private void RandomTeleport(EntityUid produce, EntityUid target)
    {
        if (GetGenome(produce) is not { } genome)
            return;

        var radius = MathF.Max(MathF.Round(CappedPotency(genome) / 20f), 1f);
        var offset = _random.NextVector2(radius);
        var xform = Transform(target);
        _transform.AttachToGridOrMap(target, xform);
        _transform.SetCoordinates(target, xform, xform.Coordinates.Offset(offset));
    }

    private void OnSmokeSquashed(Entity<PlantSmokeComponent> ent, ref PlantSquashedEvent args)
    {
        if (GetGenome(ent) is not { } genome
            || !TryComp<ProduceComponent>(ent, out var produce)
            || !_solution.TryGetSolution(ent.Owner, produce.TargetSolution, out var soln, out var solution)
            || solution.Volume <= 0)
            return;

        var range = MathF.Sqrt(CappedPotency(genome) * 0.1f);
        var spread = Math.Max(1, (int) MathF.Ceiling(range * range * 3f));
        var coords = args.Target is { } t ? Transform(t).Coordinates : Transform(ent).Coordinates;
        var smoke = Spawn(SmokePrototype, coords.SnapToGrid(EntityManager));
        if (!TryComp<SmokeComponent>(smoke, out var smokeComp))
        {
            Del(smoke);
            return;
        }

        var contents = _solution.SplitSolution(soln.Value, solution.Volume);
        _smoke.StartSmoke(smoke, contents, 10f, spread, smokeComp);
    }

    private void OnElectricSquashed(Entity<PlantElectricComponent> ent, ref PlantSquashedEvent args)
    {
        if (args.Target is { } target)
            TryZap(ent, target);
    }

    private void OnElectricSlipped(Entity<PlantElectricComponent> ent, ref PlantSlippedOnEvent args)
    {
        if (!HasComp<PlantSquashComponent>(ent))
            TryZap(ent, args.Slipped);
    }

    /// <summary>Monkestation zap: power = 45 * potency / (750 + potency), chance power * 2 percent.</summary>
    private void TryZap(Entity<PlantElectricComponent> ent, EntityUid target)
    {
        if (GetGenome(ent) is not { } genome || !HasComp<BodyComponent>(target))
            return;

        var power = BotanySystem.QpSigmoid(750f, 45f, genome.Potency);
        if (!_random.Prob(Math.Clamp(power * ent.Comp.ProbabilityMultiplier / 100f, 0f, 1f)))
            return;

        _electrocution.TryDoElectrocution(target, ent, (int) MathF.Round(power), TimeSpan.FromSeconds(1), true, ignoreInsulation: true);
    }

    private void OnLaughterSlipped(Entity<PlantLaughterComponent> ent, ref PlantSlippedOnEvent args)
    {
        _popup.PopupEntity(Loc.GetString("plant-laughter", ("plant", ent.Owner)), ent);
        _audio.PlayPvs(LaughSound, ent);
    }

    #endregion

    #region Battery

    private void OnBatteryInteractUsing(Entity<PlantBatteryComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, CableCoilTag) || GetGenome(ent) is not { } genome)
            return;

        args.Handled = true;
        if (!TryComp<StackComponent>(args.Used, out var stack) || !_stack.TryUse((args.Used, stack), ent.Comp.CablesNeeded))
        {
            _popup.PopupEntity(Loc.GetString("plant-battery-need-cable", ("amount", ent.Comp.CablesNeeded), ("plant", ent.Owner)), ent, args.User);
            return;
        }

        var cell = Spawn(PlantBattery, Transform(args.User).Coordinates);
        var charge = genome.Potency * BatteryChargePerPotency;
        if (genome.HasTrait("ElectricalActivity"))
            charge *= 150f; // Monkestation: * (rate 0.2 * 750)

        // Capped at 1e9 for this server (Monkestation itself has no cap).
        charge = MathF.Min(charge, 1e9f);
        _battery.SetMaxCharge(cell, charge);
        _battery.SetCharge(cell, charge);
        _metaData.SetEntityName(cell, Loc.GetString("plant-battery-name", ("plant", ent.Owner)));
        _popup.PopupEntity(Loc.GetString("plant-battery-made", ("plant", ent.Owner)), args.User, args.User);
        QueueDel(ent);
    }

    #endregion
}
