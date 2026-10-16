using Content.Shared.Botany;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Botany.UI;

[UsedImplicitly]
public sealed class SeedExtractorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SeedExtractorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SeedExtractorWindow>();
        _window.OnTake += key => SendMessage(new SeedExtractorTakeMessage(key));
        _window.OnScrap += key => SendMessage(new SeedExtractorScrapMessage(key));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is SeedExtractorState extractor)
            _window?.UpdateState(extractor);
    }
}
