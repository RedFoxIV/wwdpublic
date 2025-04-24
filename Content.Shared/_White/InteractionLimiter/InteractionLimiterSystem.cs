using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._White.InteractionLimiter;

public sealed class InteractionLimiterSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<InteractionLimiterComponent, GettingInteractedWithAttemptEvent>(OnInteractAttempt);
        SubscribeLocalEvent<StorageInteractionLimiterComponent, StorageInteractAttemptEvent>(OnStorageInteractAttempt);
        //SubscribeLocalEvent<InteractionLimiterComponent, GettingPickedUpAttemptEvent>();
        // this event exists but its ctor is straight up never called anywhere in code. wtf?
        //SubscribeLocalEvent<StorageInteractionLimiterComponent, StorageInteractUsingAttemptEvent>(OnStorageInteractUsingAttempt);
        SubscribeLocalEvent<StorageInteractionLimiterComponent, StorageOpenAttemptEvent>(OnStorageOpenAttempt);
    }


    public void OnInteractAttempt(EntityUid uid, InteractionLimiterComponent comp, ref GettingInteractedWithAttemptEvent args)
    {
        if(args.Cancelled)
            return;
        args.Cancelled = !CanInteractWith(args.Uid, uid);
    }

    public void OnStorageInteractAttempt(EntityUid uid, StorageInteractionLimiterComponent comp, ref StorageInteractAttemptEvent args)
    {
        if(args.Cancelled)
            return;
        args.Cancelled = !CanInteractWith(args.User, uid);
    }

    public void OnStorageOpenAttempt(EntityUid uid, StorageInteractionLimiterComponent comp, ref StorageOpenAttemptEvent args)
    {
        if(args.Cancelled)
            return;
        args.Cancelled = !CanInteractWith(args.User, uid);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>loopCheck is a safety bool to ensure this method doesn't somehow end up stuck in an endless recursion.</remarks>
    /// <returns></returns>
    public bool CanInteractWith(EntityUid user, EntityUid target, InteractionLimiterComponent? limiter = null, bool loopCheck = true)
    {
        if (_container.TryGetContainingContainer(target, out var targetContainer)) // target is in something
        {
            if (targetContainer.Owner == user) // target is in user's container, either held or worn
            {
                if (!Resolve(target, ref limiter))
                    return true;

                if (_inventory.InSlotWithAnyFlags(target, limiter.Conditions.PermittedSlots))
                    return true;

                if (limiter.Conditions.InHand && _hands.IsHolding(user, target))
                    return true;

                return false;
            }
            else
            {
                DebugTools.Assert(loopCheck);
                return NestedContainersCheck(user, target);

            }
        }
        // target is not in a container, most likely just on the floor
        return limiter?.Conditions.OnFloor ?? true;
    }

    // this assumes that target is in a container that is not user.
    private bool NestedContainersCheck(EntityUid user, EntityUid target)
    {
        bool CanInteractIfInStorage = true;
        EntityUid? loopEntity = target;
        EntityUid prevLoopEntity = target;
        while(_container.TryGetContainingContainer(loopEntity.Value, out var cont))
        {
            loopEntity = cont.Owner;
            // loopEntity being user means we're in a bag that is worn by user
            // or in a box in a bag worn by user
            // or in a small box in a box in a bag, etc.
            // the very first parent being user means target is either worn or held and it should've been handled
            // in CanInteractWith which called this method in the first place.
            if (loopEntity == user)
                return CanInteractWith(user, prevLoopEntity, loopCheck: false); // we confirm if the user can interact with the outermost container and exit the loop early

            // we can't interact with the cigarette pack in a box if _either_ the box forbids interacting with it's contents (checked below)
            // or the pack forbids interations with itself while it's in a container (checked here)
            if (TryComp<InteractionLimiterComponent>(prevLoopEntity, out var comp2) && !comp2.Conditions.InContainer)
                return false;

            if (TryComp<StorageInteractionLimiterComponent>(loopEntity, out var comp) && !comp.Conditions.InContainer)
                return false;
                    
            prevLoopEntity = loopEntity.Value;
        }
        // at this point we've confirmed that all containers in the tree (cigarette pack, box, bag)
        // allow interactions with their respective contents and prevLoopEntity holds the outermost one.
        // (and loopEntity will be null, see TryGetContainingEntity.)

        // we now need to confirm that the user can interact with the outermost container itself
        return CanInteractWith(user, prevLoopEntity, loopCheck: false);
        
    }

    /*
    private bool NestedContainersCheck(EntityUid user, EntityUid target)
    {
        var xform = Transform(target);
        var parent = xform.ParentUid;

        bool inUser = false;

        InteractionLimits conditions = new();
        EntityUid prev = target;
        StorageInteractionLimiterComponent? outerMostLimiter = null; // it ends up with actual outermost component only after the while loop below 
        bool CanInteractIfInStorage = true;

        // walk upwards in the transform entity tree until we hit either user or the tree's end
        while (parent.IsValid())
        {
            if (!_container.ContainsEntity(parent, prev)) // make sure the previous entity is contained in the current one, just in case
                break;      // if not, break early since we only care about nested containers

            if(TryComp<StorageInteractionLimiterComponent>(parent, out outerMostLimiter)) 
            {
                CanInteractIfInStorage &= outerMostLimiter.Conditions.InContainer;
                // at one point while travelling the tree, one of the nested containers disallowed interacting with its contents
                // if we have a cigarette pack inside a box inside a bag and the box forbids interactions with its contents if it's in a container,
                // we won't have direct access cigarettes in the cigarette pack until we move the box from the bag
                if (!CanInteractIfInStorage)
                    return false; // exit early
            }
            xform = Transform(parent);
            
            // double check with ContainsEntity() to avoid embedded objects bullshit
            if (xform.ParentUid == user && _container.ContainsEntity(user, parent)) 
            {
                inUser = true;
                break;
            }
            prev = parent;
            parent = xform.ParentUid
        }

        EntityUid outerMostContainer = parent; // for clarity

        if (inUser) // either held or worn by user
        {
            if (outerMostLimiter is null)
                return true; // none of the nested containers have a limiter

            if (_inventory.InSlotWithAnyFlags(outerMostContainer, outerMostLimiter.Conditions.PermittedSlots))
                return true;

            if (outerMostLimiter.Conditions.InHand && _hands.IsHolding(user, outerMostContainer))
                return true;

            return false;
        }
        // if inUser is false, then the outermost container is on the floor
        return outerMostLimiter is null || outerMostLimiter.Conditions.OnFloor
    }*/
}

