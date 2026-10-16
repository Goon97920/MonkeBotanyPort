using Content.Shared.Botany.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Botany;

/// <summary>
/// Draws the planted species on the tray's plant layer.
/// The warning lights are handled by the prototype's GenericVisualizer.
/// </summary>
public sealed partial class PlantTrayVisualizerSystem : VisualizerSystem<PlantTrayComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, PlantTrayComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!SpriteSystem.LayerMapTryGet((uid, args.Sprite), PlantTrayLayers.Plant, out var layer, false))
            return;

        AppearanceSystem.TryGetData<string>(uid, PlantTrayVisuals.PlantRsi, out var rsi, args.Component);
        AppearanceSystem.TryGetData<string>(uid, PlantTrayVisuals.PlantState, out var state, args.Component);

        if (string.IsNullOrEmpty(rsi) || string.IsNullOrEmpty(state))
        {
            SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, false);
            return;
        }

        SpriteSystem.LayerSetRsi((uid, args.Sprite), layer, new ResPath(rsi), state);
        SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, true);
    }
}
