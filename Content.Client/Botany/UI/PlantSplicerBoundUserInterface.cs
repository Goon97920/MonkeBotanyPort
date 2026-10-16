using Content.Shared.Botany;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Botany.UI;

[UsedImplicitly]
public sealed class PlantSplicerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PlantSplicerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PlantSplicerWindow>();
        _window.OnEjectFirst += () => SendMessage(new ItemSlotButtonPressedEvent("seed_slot_one"));
        _window.OnEjectSecond += () => SendMessage(new ItemSlotButtonPressedEvent("seed_slot_two"));
        _window.OnEjectBeaker += () => SendMessage(new ItemSlotButtonPressedEvent("beaker_slot"));
        _window.OnSplice += () => SendMessage(new PlantSplicerSpliceMessage());
        _window.OnInfuse += () => SendMessage(new PlantSplicerInfuseMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is PlantSplicerState splicer)
            _window?.UpdateState(splicer);
    }
}
