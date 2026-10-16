using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

/// <summary>
/// Monkestation (tgstation) healium formation: BZ and freon between 25K and 300K.
/// SS14 has no freon, so frezon (its coolant gas) takes its place. Exothermic.
/// </summary>
[UsedImplicitly]
public sealed partial class HealiumFormationReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var bz = mixture.GetMoles(Gas.BZ);
        var frezon = mixture.GetMoles(Gas.Frezon);
        var temperature = mixture.Temperature;

        var heatEfficiency = Math.Min(temperature * 0.3f, Math.Min(frezon / 2.75f, bz / 0.25f));
        if (heatEfficiency <= 0 || frezon - heatEfficiency * 2.75f < 0 || bz - heatEfficiency * 0.25f < 0)
            return ReactionResult.NoReaction;

        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        mixture.AdjustMoles(Gas.Frezon, -heatEfficiency * 2.75f);
        mixture.AdjustMoles(Gas.BZ, -heatEfficiency * 0.25f);
        mixture.AdjustMoles(Gas.Healium, heatEfficiency * 3f);

        var energyReleased = heatEfficiency * Atmospherics.HealiumFormationEnergy / heatScale;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
