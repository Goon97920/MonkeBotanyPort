using Content.Shared.Botany;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Botany.UI;

[UsedImplicitly]
public sealed class PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PlantAnalyzerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PlantAnalyzerWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is PlantAnalyzerState analyzer)
            _window?.UpdateState(analyzer);
    }
}
