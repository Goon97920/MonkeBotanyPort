using Content.Shared.Chemistry.Metalgen;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client.Chemistry.Metalgen;

/// <summary>Tints objects transmuted by metalgen in their new material's color.</summary>
public sealed partial class MetalgenTransmutedVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetalgenTransmutedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MetalgenTransmutedComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<MetalgenTransmutedComponent> ent, ref ComponentStartup args) => Apply(ent);

    private void OnState(Entity<MetalgenTransmutedComponent> ent, ref AfterAutoHandleStateEvent args) => Apply(ent);

    private void Apply(Entity<MetalgenTransmutedComponent> ent)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), ent.Comp.Color);
    }
}
