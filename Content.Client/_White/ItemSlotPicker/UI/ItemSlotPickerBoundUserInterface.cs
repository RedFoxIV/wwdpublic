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
using Content.Client.UserInterface.Systems.Inventory;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;

namespace Content.Client._White.ItemSlotPicker.UI;

// a UFO came by and left this message here
[UsedImplicitly]
public sealed class ItemSlotPickerBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

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
        if (_ui.ActiveScreen is null)
            throw new NullReferenceException("Tried to open ItemSlotPicker with UserInterfaceManager.ActiveScreen being null. What the hell did you do?");

        base.Open();
        _menu = CreateRadialMenu();
        _menu.OnClose += Close;
        _menu.CloseButtonStyleClass = "RadialMenuCloseButton";
        _menu.BackButtonStyleClass = "RadialMenuBackButton";

        UpdateLayer();
        _menu.OpenCenteredAt(_eye.WorldToScreen(_transform.GetWorldPosition(Owner)) / _clyde.ScreenSize);
    }

    private RadialMenu CreateRadialMenu()
    {
        RadialMenu? ret = TrySlotBoundRadialMenu();
        ret ??= new EntityCenteredRadialMenu(Owner, _ui.ActiveScreen!.GetWidget<MainViewport>()!.Parent!); // why am i getting mainviewport's parent for this?
        return ret;
    }

    private RadialMenu? TrySlotBoundRadialMenu()
    {
        //if (EntMan.TryGetComponent<ClothingComponent>(Owner, out var clothing) ||
        //    clothing.InSlot is not string slotName)
        //    return null;
        //
        //var inventoryUIcontroller = _ui.GetUIController<InventoryUIController>();
        //_ui.GetActiveUIWidget<HotbarGui>().Get
        //if(!inventoryUIcontroller._inventoryHotbar.GetButton(clothing.InSlot) is SlotButton button &&
        //    
        //
        //
        //{
        //    _menu = new ParentBoundRadialMenu();
        //    !.Parent!.AddChild(_menu);
        //    LayoutContainer.SetPosition(_menu, button!.Position);
        //}
        return null;
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
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    private readonly SharedTransformSystem _transform;
    InventoryUIController _invUIcontroller;
    private Vector2 _cachedPos;

    public EntityCenteredRadialMenu(EntityUid entity, Control parent) : base(parent)
    {
        Entity = entity;
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();
        _invUIcontroller = _ui.GetUIController<InventoryUIController>();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        TryCenterOnEntity();
        base.FrameUpdate(args);
    }

    private void TryCenterOnEntity()
    {
        var uiPos = TrySnapToUI();

        if (_entMan.Deleted(Entity) ||
            Parent is null ||
            !_entMan.TryGetComponent<TransformComponent>(Entity, out var transforam))
            return;
        var pos = uiPos ?? _eye.WorldToScreen(_transform.GetWorldPosition(Entity)) / Parent.PixelSize;
        if (pos == _cachedPos)
            return;
        _cachedPos = pos;
        RecenterWindow(pos);
    }

    private Vector2? TrySnapToUI()
    {
        if (!_entMan.TryGetComponent<ClothingComponent>(Entity, out var clothingComp) ||
           clothingComp.InSlot is not string slotName)
            return null;

        if(_invUIcontroller._inventoryHotbar?.GetButton(slotName) is SlotControl button)
        {
            return button.Position + button.Size / 2;
        }

        if(_ui.GetActiveUIWidget<HotbarGui>() is HotbarGui hotbar)
        {
            var buttons = hotbar.GetControlOfType<SlotControl>(true, true); // forgive me god
            foreach(var slotControl in buttons)
                if (slotControl.SlotName == slotName)
                    return (slotControl.GlobalPosition - Parent!.GlobalPosition + slotControl.Size / 2) / Parent.Size;
        }

        return null;
    }
}
