using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.MedicalScanner;
using Content.Shared.Temperature.Components;

namespace Content.Server.Medical.HealthAnalyzer;

/// <inheritdoc/>
public sealed partial class ServerHealthAnalyzerSystem : HealthAnalyzerSystem
{
    [Dependency] private EntityQuery<TemperatureComponent> _temperatureQuery;
    [Dependency] private SecondaryStomachSystem _secondaryStomach = default!;

    public override HealthAnalyzerUiState GetHealthAnalyzerUiState(EntityUid? target, bool scanMode)
    {
        var state = base.GetHealthAnalyzerUiState(target, scanMode);

        if (_temperatureQuery.TryComp(target, out var temp))
            state.Temperature = temp.Temperature;

        if (target != null)
            state.UndigestedPlantMatter = _secondaryStomach.GetVolume(target.Value).Float();

        return state;
    }
}
