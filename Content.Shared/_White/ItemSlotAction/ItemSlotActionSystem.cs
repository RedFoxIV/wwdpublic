using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._White.ItemSlotAction;
/*
public sealed class ItemSlotActionSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        //SubscribeLocalEvent<ItemSlotActionComponent, GetItemActionsEvent>(GetItemActions);
        SubscribeLocalEvent<ItemSlotActionComponent, GotEquippedEvent>(OnDidEquip);
        SubscribeLocalEvent<ItemSlotActionComponent, GotEquippedHandEvent>(OnHandEquipped);
        SubscribeLocalEvent<ItemSlotActionComponent, GotUnequippedEvent>(OnDidUnequip);
        SubscribeLocalEvent<ItemSlotActionComponent, GotUnequippedHandEvent>(OnHandUnequipped);

        SubscribeLocalEvent<ItemSlotActionComponent, EntGotInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<ItemSlotActionComponent, EntGotRemovedFromContainerMessage>(OnEntRemoved);
    }

    //private void GetItemActions(EntityUid uid, ItemSlotActionComponent comp, GetItemActionsEvent args)
    //{
    //    if (!TryComp<ItemSlotsComponent>(uid, out var slots))
    //        return;
    //
    //    foreach(var slotId in comp.ItemSlots)
    //    {
    //        if (!_itemSlots.TryGetSlot(uid, slotId, out var slot, slots) ||
    //            !slot.HasItem)
    //            continue;
    //        RaiseLocalEvent(slot.Item!.Value, args);
    //    }
    //}

    private void OnEntInserted(EntityUid uid, ItemSlotActionComponent component, EntGotInsertedIntoContainerMessage args)
    {
        if (component.ItemSlots.Contains(args.Container.ID))
        {
            EntityUid item = args.Entity;
            var ev = new GetItemActionsEvent(_actionContainer, args., item, args.SlotFlags);
            RaiseLocalEvent(item, ev);
            if (ev.Actions.Count == 0)
                return;
            _action.GrantActions(args.Equipee, ev.Actions, item);
        }
    }

    private void OnEntRemoved(EntityUid uid, ItemSlotActionComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (component.ItemSlots.Contains(args.Container.ID))
        {

        }
    }

    private void OnDidEquip(EntityUid uid, ItemSlotActionComponent component, GotEquippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        foreach(var slotId in component.ItemSlots)
        {
            if (!_itemSlots.TryGetSlot(uid, slotId, out var slot) ||
                !slot.HasItem)
                continue;

            EntityUid item = slot.Item!.Value;
            var ev = new GetItemActionsEvent(_actionContainer, args.Equipee, item, args.SlotFlags);
            RaiseLocalEvent(item, ev);
            if (ev.Actions.Count == 0)
                return;
            _action.GrantActions(args.Equipee, ev.Actions, item);
        }

    }

    private void OnHandEquipped(EntityUid uid, ItemSlotActionComponent component, GotEquippedHandEvent args)
    {
        if (_timing.ApplyingState)
            return;

        foreach (var slotId in component.ItemSlots)
        {
            if(!_itemSlots.TryGetSlot(uid, slotId, out var slot) ||
                !slot.HasItem)
                continue;

            EntityUid item = slot.Item!.Value;
            var ev = new GetItemActionsEvent(_actionContainer, args.User, item);
            RaiseLocalEvent(item, ev);
            if (ev.Actions.Count == 0)
                return;
            _action.GrantActions(args.User, ev.Actions, item);
        }
    }

    private void OnDidUnequip(EntityUid uid, ItemSlotActionComponent component, GotUnequippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        foreach (var slotId in component.ItemSlots)
        {
            if (!_itemSlots.TryGetSlot(uid, slotId, out var slot) ||
                !slot.HasItem)
                continue;

            _action.RemoveProvidedActions(args.Equipee, slot.Item!.Value);
        }
    }

    private void OnHandUnequipped(EntityUid uid, ItemSlotActionComponent component, GotUnequippedHandEvent args)
    {
        if (_timing.ApplyingState)
            return;

        foreach (var slotId in component.ItemSlots)
        {
            if(!_itemSlots.TryGetSlot(uid, slotId, out var slot) ||
                !slot.HasItem)
                continue;

            _action.RemoveProvidedActions(args.User, slot.Item!.Value);
        }
    }
}
*/
