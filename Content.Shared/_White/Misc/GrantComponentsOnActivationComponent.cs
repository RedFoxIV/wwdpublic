using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
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
public sealed partial class GrantComponentsOnActivationComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry ComponentsToAdd = new();

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
    [Dependency] private readonly IComponentFactory _compfactory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GrantComponentsOnActivationComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<GrantComponentsOnActivationComponent, ActivateInWorldEvent>(OnActivatedInWorld);
        SubscribeLocalEvent<GrantComponentsOnActivationComponent, EntGotRemovedFromContainerMessage>(OnGotRemoved);
        SubscribeLocalEvent<GrantComponentsOnActivationComponent, EntGotInsertedIntoContainerMessage>(OnGotInserted);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<GrantComponentsOnActivationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            DebugTools.Assert(comp.Active || !comp.DeferredDeactivation, "Inactive GrantComponentsOnActivationComponent had its DeferredDeactivation set to true.");
            if (comp.DeferredDeactivation && comp.Active)
            {
                Deactivate(uid, comp, comp.CurrentUser.Value);
            }
        }
    }

    private void OnUseInHand(EntityUid uid, GrantComponentsOnActivationComponent comp, UseInHandEvent args)
    {
        if (comp.InHands)
            Toggle(uid, comp, args.User);
    }

    private void OnActivatedInWorld(EntityUid uid, GrantComponentsOnActivationComponent comp, ActivateInWorldEvent args)
    {
        if (!_container.TryGetContainingContainer(uid, out var holderContainer) || holderContainer.Owner != args.User ||
           !_inventory.TryGetContainingSlot(uid, out var slotdef) || (slotdef.SlotFlags & comp.Slots) == 0)
            return;
        Toggle(uid, comp, args.User);
    }

    private void OnGotRemoved(EntityUid uid, GrantComponentsOnActivationComponent comp, EntGotRemovedFromContainerMessage args)
    {
        comp.DeferredDeactivation = true;
    }
    
    private void OnGotInserted(EntityUid uid, GrantComponentsOnActivationComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (!comp.Active) // if we're not active, then whatever
            return;

        if((args.Container.Owner != comp.CurrentUser) ||                                                        // if the container is not current user, or
           (_hands.IsHolding(args.Container.Owner, uid) && !comp.InHands) ||                                    // if we're being held and we don't work when held, or
           (_inventory.TryGetContainingSlot(uid, out var slotdef) && (slotdef.SlotFlags & comp.Slots) == 0))    // if we're equipped and we don't allow being equipped (into that slot)
        {
            Deactivate(uid, comp, comp.CurrentUser.Value); // then deactivate;
            return;
        }
        comp.DeferredDeactivation = false; // otherwise reset deferred deactivation
    }

    private void Toggle(EntityUid uid, GrantComponentsOnActivationComponent comp, EntityUid user)
    {
        if (comp.Active)
            Deactivate(uid, comp, user);
        else
            Activate(uid, comp, user);
    }

    private void Activate(EntityUid uid, GrantComponentsOnActivationComponent comp, EntityUid user)
    {
        DebugTools.Assert(!comp.Active, "Tried to Activate an already active granter");
        comp.CurrentUser = user;
        foreach (var component in comp.ComponentsToAdd)
        {
            if (HasComp(uid, component.Value.Component.GetType()))
                continue;

            EntityManager.AddComponent(user, component.Value); // WD EDIT
            comp.AddedComponents.Add(component.Key, component.Value);
        }

    }

    private void Deactivate(EntityUid uid, GrantComponentsOnActivationComponent comp, EntityUid user)
    {
        DebugTools.Assert(comp.Active, "Tried to Deactivate an already inactive granter");
        DebugTools.Assert(comp.CurrentUser == user, "Passed wrong user to Deactivate");
        EntityManager.RemoveComponents(user, comp.AddedComponents);
        comp.AddedComponents.Clear();
        comp.CurrentUser = null;
        comp.DeferredDeactivation = false;
    }
}
