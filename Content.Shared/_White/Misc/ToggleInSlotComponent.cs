using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._White.Misc;

[RegisterComponent]
public sealed partial class ToggleInSlotsComponent : Component
{
    [DataField]
    public bool InHands = false;
    [DataField]
    public SlotFlags Slots = SlotFlags.NONE;

    [MemberNotNullWhen(true, nameof(CurrentUser))]
    public bool Active => CurrentUser is not null;
    public EntityUid? CurrentUser = null;
    public bool DeferredDeactivation = false;

    [DataField]
    public ComponentRegistry AddedComponents = new();
}

public sealed class GrantComponentsOnActivationSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly IComponentFactory _compfactory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ToggleInSlotsComponent, EntGotRemovedFromContainerMessage>(OnGotRemoved);
        SubscribeLocalEvent<ToggleInSlotsComponent, EntGotInsertedIntoContainerMessage>(OnGotInserted);
        SubscribeLocalEvent<ToggleInSlotsComponent, ItemToggledEvent>(OnToggle);
        SubscribeLocalEvent<ToggleInSlotsComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ToggleInSlotsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            DebugTools.Assert(comp.Active || !comp.DeferredDeactivation, "Inactive ToggleInSlotsComponent had its DeferredDeactivation set to true.");
            if (comp.DeferredDeactivation && comp.Active)
            {
                _toggle.TryDeactivate(uid);
            }
        }
    }

    private void OnGotRemoved(EntityUid uid, ToggleInSlotsComponent comp, EntGotRemovedFromContainerMessage args)
    {
        if(comp.Active)
            comp.DeferredDeactivation = true;
    }
    
    private void OnGotInserted(EntityUid uid, ToggleInSlotsComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (!comp.Active)
            return;

        if(!IsInCorrectSlot(uid, comp))
        {
            _toggle.TryDeactivate(uid, comp.CurrentUser);
            return;
        }
        comp.DeferredDeactivation = false;
    }

    private void OnActivateAttempt(EntityUid uid, ToggleInSlotsComponent comp, ref ItemToggleActivateAttemptEvent args)
    {
        if(!IsInCorrectSlot(uid, comp))
            args.Cancelled = true;
    }

    private void OnToggle(EntityUid uid, ToggleInSlotsComponent comp, ItemToggledEvent args)
    {
        if (args.User is not EntityUid user)
            return;

        if (args.Activated)
        {
            comp.CurrentUser = args.User;
        }
        else
        {
            comp.CurrentUser = null;
            comp.DeferredDeactivation = false;
        }
    }

    private bool IsInCorrectSlot(EntityUid uid, ToggleInSlotsComponent comp)
    {
        if (!_container.TryGetContainingContainer(uid, out var cont)) // if we're on the floor, then whatever
            return false;

        return (_inventory.TryGetSlot(cont.Owner, cont.ID, out var slotdef) && (slotdef.SlotFlags & comp.Slots) == 0) || // either we're in a correct inventory slot
               (_hands.IsHolding(cont.Owner, uid) && comp.InHands); // or are beind held
    }
}
