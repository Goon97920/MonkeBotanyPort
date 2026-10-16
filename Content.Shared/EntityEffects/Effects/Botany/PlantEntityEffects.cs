using Content.Shared.Botany;
using Content.Shared.Botany.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.EntityEffects.Effects.Botany;

// Reagent effects on hydroponics trays, ported from Monkestation's on_plant_apply and
// on_plant_grower_apply procs. They target the tray entity; the scale is the number of units of the
// reagent in the tray. Each tray cycle (10 s) applies every reagent's effects once.
//
// Effects implementing IPlantTrayLevelEffect run at the start of the cycle on the tray itself
// ("grower" effects). All others run during the plant's growth step and act on the planted genome.

/// <summary>Marker for effects that act on the tray rather than the planted genome.</summary>
public interface IPlantTrayLevelEffect;

/// <summary>
/// Computes Monkestation-style amounts: either round(units * PerUnit), units * PerUnit, or a random
/// integer between FlatMin and FlatMax (inclusive) when those are set.
/// </summary>
public abstract partial class BasePlantAmountEffect<T> : EntityEffectBase<T> where T : BasePlantAmountEffect<T>
{
    /// <summary>Amount per unit of reagent in the tray.</summary>
    [DataField]
    public float PerUnit;

    /// <summary>Whether to round the per-unit amount to a whole number (DM round()).</summary>
    [DataField]
    public bool Round = true;

    /// <summary>If set (with <see cref="FlatMax"/>), a random flat amount is used instead.</summary>
    [DataField]
    public int? FlatMin;

    [DataField]
    public int? FlatMax;

    /// <summary>Constant added to the per-unit amount.</summary>
    [DataField]
    public float Flat;

    public float GetAmount(float units, System.Random random)
    {
        if (FlatMin is { } min && FlatMax is { } max)
            return random.Next(Math.Min(min, max), Math.Max(min, max) + 1);

        var amount = units * PerUnit + Flat;
        if (Round)
            amount = MathF.Sign(amount) * MathF.Round(MathF.Abs(amount), MidpointRounding.AwayFromZero);
        return amount;
    }

    protected string AmountText()
    {
        if (FlatMin is { } min && FlatMax is { } max)
            return Loc.GetString("entity-effect-guidebook-plant-flat-range", ("min", min), ("max", max));
        return Loc.GetString("entity-effect-guidebook-plant-per-unit", ("amount", PerUnit.ToString("0.###")));
    }
}

/// <summary>Adjusts one of the planted genome's core stats.</summary>
public sealed partial class PlantAdjustStat : BasePlantAmountEffect<PlantAdjustStat>
{
    [DataField(required: true)]
    public PlantStat Stat;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("entity-effect-guidebook-plant-stat",
            ("stat", Loc.GetString($"plant-stat-{Stat.ToString().ToLowerInvariant()}")),
            ("positive", PerUnit > 0 || (FlatMin ?? 0) > 0),
            ("amount", AmountText()),
            ("chance", Probability));
}

/// <summary>Adjusts the planted plant's health.</summary>
public sealed partial class PlantAdjustHealth : BasePlantAmountEffect<PlantAdjustHealth>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("entity-effect-guidebook-plant-health",
            ("positive", PerUnit > 0 || (FlatMin ?? 0) > 0),
            ("amount", AmountText()),
            ("chance", Probability));
}

/// <summary>Trains a trait into the plant; it is gained once progress reaches 100.</summary>
public sealed partial class PlantTrainTrait : BasePlantAmountEffect<PlantTrainTrait>
{
    [DataField(required: true)]
    public ProtoId<PlantTraitPrototype> Trait;

    public PlantTrainTrait()
    {
        Round = false;
    }

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var name = prototype.TryIndex(Trait, out var trait) ? Loc.GetString(trait.Name) : Trait.Id;
        return Loc.GetString("entity-effect-guidebook-plant-train-trait", ("trait", name), ("chance", Probability));
    }
}

[Serializable, NetSerializable]
public enum PlantTrayAttribute : byte
{
    Toxicity,
    Pests,
    Weeds,
    SelfSustain,
}

/// <summary>Adjusts a tray attribute: toxicity, pests, weeds or self-sustenance.</summary>
public sealed partial class PlantTrayAdjust : BasePlantAmountEffect<PlantTrayAdjust>, IPlantTrayLevelEffect
{
    [DataField(required: true)]
    public PlantTrayAttribute Attribute;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("entity-effect-guidebook-plant-tray",
            ("attribute", Loc.GetString($"plant-tray-attribute-{Attribute.ToString().ToLowerInvariant()}")),
            ("positive", PerUnit > 0 || (FlatMin ?? 0) > 0),
            ("amount", AmountText()),
            ("chance", Probability));
}

/// <summary>Makes the tray's next growth cycle come sooner (Earthsblood).</summary>
public sealed partial class PlantTraySpeedUp : EntityEffectBase<PlantTraySpeedUp>, IPlantTrayLevelEffect
{
    [DataField]
    public TimeSpan Time = TimeSpan.FromSeconds(3);

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("entity-effect-guidebook-plant-speed-up", ("time", Time.TotalSeconds), ("chance", Probability));
}
