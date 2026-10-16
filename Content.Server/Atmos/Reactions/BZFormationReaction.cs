using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

/// <summary>
/// Monkestation (tgstation) BZ formation: nitrous oxide and plasma make BZ, faster at low pressure
/// and high volume. With more than 3 parts plasma per part N2O, the N2O starts decomposing into
/// nitrogen and oxygen instead, with plasma as a catalyst. Exothermic.
/// </summary>
[UsedImplicitly]
public sealed partial class BZFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var n2o = mixture.GetMoles(Gas.NitrousOxide);
        var plasma = mixture.GetMoles(Gas.Plasma);
        var pressure = mixture.Pressure;
        if (pressure <= 0 || plasma <= 0)
            return ReactionResult.NoReaction;

        var environmentEfficiency = mixture.Volume / pressure;
        var ratioEfficiency = Math.Min(n2o / plasma, 1f);
        var decomposedFactor = Math.Max(4f * (plasma / (n2o + plasma) - 0.75f), 0f);
        var plasmaUse = 0.8f * (1f - decomposedFactor);
        var bzFormed = Math.Min(0.01f * ratioEfficiency * environmentEfficiency,
            Math.Min(n2o / 0.4f, plasmaUse > 0 ? plasma / plasmaUse : float.MaxValue));

        if (bzFormed <= 0 || n2o - bzFormed * 0.4f < 0 || plasma - plasmaUse * bzFormed < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var temperature = mixture.Temperature;

        if (decomposedFactor > 0)
        {
            var decomposed = 0.4f * bzFormed * decomposedFactor;
            mixture.AdjustMoles(Gas.Nitrogen, decomposed);
            mixture.AdjustMoles(Gas.Oxygen, 0.5f * decomposed);
        }

        mixture.AdjustMoles(Gas.BZ, bzFormed * (1f - decomposedFactor));
        mixture.AdjustMoles(Gas.NitrousOxide, -0.4f * bzFormed);
        mixture.AdjustMoles(Gas.Plasma, -plasmaUse * bzFormed);

        var energyReleased = bzFormed * (Atmospherics.BZFormationEnergy
                                         + decomposedFactor * (Atmospherics.N2ODecompositionEnergy - Atmospherics.BZFormationEnergy));
        energyReleased /= heatScale;

        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
