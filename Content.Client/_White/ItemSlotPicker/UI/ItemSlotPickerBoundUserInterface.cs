using Content.Client.Chat.UI;
using Content.Client.UserInterface.Controls;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Control;
using System.Numerics;
using Content.Shared._White.ItemSlotPicker;
using Content.Shared._White.ItemSlotPicker.UI;
using Content.Client._White.UI.ViewportBoundRadialMenu;
using Robust.Client.UserInterface;
using Content.Shared.Inventory;
using Content.Shared.Clothing.Components;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Content.Client.UserInterface.ControlExtensions;

namespace Content.Client._White.ItemSlotPicker.UI;

// a UFO came by and left this message here
[UsedImplicitly]
public sealed class ItemSlotPickerBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    private IUserInterfaceManager _ui = default!;

    private readonly ItemSlotsSystem _itemSlots;
    private readonly SharedTransformSystem _transform;
    private readonly InventorySystem _inventory;

    private RadialMenu? _menu;
    private RadialContainer? _layer;

    public ItemSlotPickerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _itemSlots = EntMan.System<ItemSlotsSystem>();
        _inventory = EntMan.System<InventorySystem>();
        _transform = EntMan.System<SharedTransformSystem>();
    }

    protected override void Open()
    {
        _ui ??= IoCManager.Resolve<IUserInterfaceManager>();
        base.Open();
        if (EntMan.TryGetComponent<ClothingComponent>(Owner, out var clothing) &&
            clothing.InSlot is not null &&
           _inventory.TryGetSlot(_transform.GetParentUid(Owner), clothing.InSlot, out var slotDef) &&
           _ui.ActiveScreen!.GetControlOfType<InventoryDisplay>()[0]!.TryGetButton(clothing.InSlot, out var button))
        {
            _menu = new ParentBoundRadialMenu();
            _ui.ActiveScreen!.GetWidget<MainViewport>()!.Parent!.AddChild(_menu);
            LayoutContainer.SetPosition(_menu, button!.Position);
        }
        else
        {
            _menu = new EntityCenteredRadialMenu(Owner);
            _ui.ActiveScreen!.GetWidget<MainViewport>()!.Parent!.AddChild(_menu);
        }
        _menu.OnClose += Close;
        _menu.CloseButtonStyleClass = "RadialMenuCloseButton";
        _menu.BackButtonStyleClass = "RadialMenuBackButton";
        
        UpdateLayer();
        _menu.OpenCenteredAt(_eye.WorldToScreen(_transform.GetWorldPosition(Owner)) / _clyde.ScreenSize);
    }

    private void UpdateLayer()
    {
        var picker = EntMan.GetComponent<ItemSlotPickerComponent>(Owner);
        if (_layer is not null)
            _menu!.RemoveChild(_layer);

        _layer = new RadialContainer();
        foreach (var slotID in picker.ItemSlots)
        {
            if (!_itemSlots.TryGetSlot(Owner, slotID, out var slot) ||
                !slot.HasItem)
                continue;

            // i see no value in having 99 different radial button types with the only difference being what data they hold
            // hence i'm just setting all relevant parameters after constructing the button.
            var button = new RadialMenuTextureButton
            {
                StyleClasses = { "RadialMenuButton" },
                SetSize = new Vector2(64f, 64f),
                ToolTip = Loc.GetString(slot.Name),
            };

            var tex = new TextureRect
            {
                VerticalAlignment = VAlignment.Center,
                HorizontalAlignment = HAlignment.Center,
                Texture = EntMan.GetComponent<SpriteComponent>(slot.Item!.Value).Icon?.Default,
                TextureScale = new Vector2(2f, 2f),
            };

            button.AddChild(tex);
            button.OnButtonUp += _ => { SendPredictedMessage(new ItemSlotPickerSlotPickedMessage(slotID)); };
            _layer.AddChild(button);
        }
        _menu!.AddChild(_layer);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is not ItemSlotPickerContentsChangedMessage)
            return;
        UpdateLayer();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        _menu?.Dispose();
    }
}

[Virtual]
public class EntityCenteredRadialMenu : ParentBoundRadialMenu
{
    public EntityUid Entity;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    private readonly SharedTransformSystem _transform;

    private Vector2 _cachedPos;

    public EntityCenteredRadialMenu(EntityUid entity) : base()
    {
        Entity = entity;
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();
    }

    public EntityCenteredRadialMenu(EntityUid entity, IEntityManager man, IEyeManager eye, IClyde clyde) : base()
    {
        Entity = entity;
        _clyde = clyde;
        _entMan = man;
        _eye = eye;
        _transform = _entMan.System<SharedTransformSystem>();
    }


    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_entMan.Deleted(Entity) ||
            Parent is null ||
            !_entMan.TryGetComponent<TransformComponent>(Entity, out var transforam))
            return;
        var pos = _eye.WorldToScreen(_transform.GetWorldPosition(Entity)) / Parent.PixelSize;
        if (pos == _cachedPos)
            return;
        _cachedPos = pos;
        RecenterWindow(pos);
    }
}
