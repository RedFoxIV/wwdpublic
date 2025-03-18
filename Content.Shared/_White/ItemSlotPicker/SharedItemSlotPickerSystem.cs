using Content.Shared._White.ItemSlotPicker.UI;
using Content.Shared.ActionBlocker;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._White.ItemSlotPicker;

public abstract class SharedItemSlotPickerSystem : EntitySystem
{
    [Dependency] protected readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] protected readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] protected readonly ActionBlockerSystem _blocker = default!;
    [Dependency] protected readonly SharedInteractionSystem _interact = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ItemSlotPickerComponent, ComponentInit>(CompInit);
        SubscribeLocalEvent<ItemSlotPickerComponent, AlternativeInteractionEvent>(AltInteract);
        SubscribeLocalEvent<ItemSlotPickerComponent, ItemSlotPickerSlotPickedMessage>(OnMessage);
    }

    protected virtual void CompInit(EntityUid uid, ItemSlotPickerComponent comp, ComponentInit args)
    {
        _ui.SetUi(uid, ItemSlotPickerKey.Key, new InterfaceData("ItemSlotPickerBoundUserInterface", 1.5f));
    }

    protected virtual void AltInteract(EntityUid uid, ItemSlotPickerComponent comp, AlternativeInteractionEvent args)
    {
        var user = args.User;
        if (!TryComp<ItemSlotsComponent>(uid, out var slots) ||
            !TryComp<HandsComponent>(user, out var hands) ||
            !_blocker.CanComplexInteract(user) ||
            !_blocker.CanInteract(user, uid) ||
            !_interact.InRangeAndAccessible(user, uid, 1.5f))
            return;

        args.Handled = true;
        List<ItemSlot> OccupiedSlots = new(comp.ItemSlots.Count);
        List<ItemSlot> FreeSlots = new(comp.ItemSlots.Count);
        foreach(var slotId in comp.ItemSlots)
        {
            if(!_itemSlots.TryGetSlot(uid, slotId, out var slot, slots))
                continue;
            if (slot.HasItem)
                OccupiedSlots.Add(slot);
            else
                FreeSlots.Add(slot);
        }

        if (hands.ActiveHandEntity is EntityUid item)
            foreach (var slot in FreeSlots)
            {
                if (TryInsert(uid, slot, item, user))
                    return; // I wish this altverb bullshit wasn't a thing.
            }
        if(comp.AutoTakeIfSingle && OccupiedSlots.Count == 1)
        {
            TryEject(uid, OccupiedSlots[0], user, comp, slots);
            return;
        }
        _ui.TryToggleUi(uid, ItemSlotPickerKey.Key, user);
    }

    protected virtual void OnMessage(EntityUid uid, ItemSlotPickerComponent comp, ItemSlotPickerSlotPickedMessage args)
    {
        if (!TryComp<ItemSlotsComponent>(uid, out var slots) ||
            !comp.ItemSlots.Contains(args.SlotId) ||
            !_itemSlots.TryGetSlot(uid, args.SlotId, out var slot))
            return;
        //_itemSlots.TryEjectToHands(uid, slot, args.Actor, true);
        TryEject(uid, slot, args.Actor, comp, slots);
        if(comp.AutoClose)
            _ui.CloseUi(uid, ItemSlotPickerKey.Key, args.Actor);
    }

    protected virtual bool TryInsert(EntityUid uid, ItemSlot slot, EntityUid item, EntityUid user)
    {
        return _itemSlots.TryInsert(uid, slot, item, user, true);
    }

    protected virtual bool TryEject(EntityUid uid, ItemSlot slot, EntityUid user, ItemSlotPickerComponent? comp = null, ItemSlotsComponent? slotsComp = null)
    {
        if (!Resolve(uid, ref comp) ||
            !Resolve(uid, ref slotsComp))
            return false;

        if (comp.CloseOnLast)
        {
            int count = 0;
            foreach (var slotId in comp.ItemSlots)
            {
                if (_itemSlots.TryGetSlot(uid, slotId, out var _slot, slotsComp) && _slot.HasItem)
                    count++;
                if (count > 1)
                    break; // we only care whether it's 1 or more
            }
            if(count == 1)
                _ui.CloseUi(uid, ItemSlotPickerKey.Key, user);
        }
        return _itemSlots.TryEjectToHands(uid, slot, user, true);
    }
}
[Serializable, NetSerializable]
public enum ItemSlotPickerKey { Key };
