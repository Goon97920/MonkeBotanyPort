using Content.Shared.Botany;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Botany.UI;

[UsedImplicitly]
public sealed class PlantDnaManipulatorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PlantDnaManipulatorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PlantDnaManipulatorWindow>();
        _window.OnEjectSeed += () => SendMessage(new ItemSlotButtonPressedEvent("seed_slot"));
        _window.OnEjectDisk += () => SendMessage(new ItemSlotButtonPressedEvent("disk_slot"));
        _window.OnInsert += () => SendMessage(new PlantDnaInsertMessage());
        _window.OnExtract += (kind, index) => SendMessage(new PlantDnaExtractMessage(kind, index));
        _window.OnRemove += (kind, index) => SendMessage(new PlantDnaRemoveMessage(kind, index));
        _window.OnReplace += stat => SendMessage(new PlantDnaReplaceMessage(stat));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is PlantDnaManipulatorState dna)
            _window?.UpdateState(dna);
    }
}
