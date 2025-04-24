using Content.Shared.Inventory;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._White.InteractionLimiter;

/// <summary>
/// Prevents an item with this component from being interacted with unless some condition is met
/// ex. being in user's hands, pockets, clothing slot, clothing storage, innate storage (see slimepeople), etc.
/// Default values allow all interactions as usual.
/// </summary>
[RegisterComponent]
[AutoGenerateComponentState]
public sealed partial class InteractionLimiterComponent : Component
{
    [DataField]
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public InteractionLimits Conditions = new();

    [AutoNetworkedField]
    public bool Equipped;
}


/// <summary>
/// Limits interactions for items in owner's storage. Default values allow all interactions as usual.
/// </summary>
[RegisterComponent]
[AutoGenerateComponentState]
public sealed partial class StorageInteractionLimiterComponent : Component
{
    [DataField]
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public InteractionLimits Conditions = new();
}



[DataDefinition]
[Serializable, NetSerializable]
public partial struct InteractionLimits
{
    public InteractionLimits() { }
    public InteractionLimits(bool inHand = true,
                             bool onFloor = true,
                             SlotFlags flags = SlotFlags.All,
                             bool inCont = true,
                             bool inHeldCont = true,
                             bool inWornCont = true)
    {
        InHand = inHand;
        OnFloor = onFloor;
        PermittedSlots = flags;
        InContainer = inCont;
        InHeldContainer = inHeldCont;
        InWornContainer = inWornCont;
    }
    /// <summary>
    /// Allows interactions with component's owner if it's in user's hand 
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool InHand = true;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool OnFloor = true;
    /// <summary>
    /// Allows interactions with component's owner if it's in user's specified clothing slots
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public SlotFlags PermittedSlots = SlotFlags.All;

    /// <summary>
    /// Allows interactions with component's owner if it's in a container that is *not* being worn.
    /// If container has StorageInteractionLimiterComponent, they both must permit this interaction.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool InContainer = true; // i.e. on the floor

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool InHeldContainer = true; // held

    /// <summary>
    /// Allows interactions with component's owner if it's in a container that is being worn by user.
    /// If container has StorageInteractionLimiterComponent, they both must permit this interaction.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool InWornContainer = true; // ex. in backpack or outer clothing slot

    public static InteractionLimits MergeAnd(InteractionLimits A, InteractionLimits B) =>
        new(A.InHand & B.InHand,
            A.OnFloor & B.OnFloor,
            A.PermittedSlots & B.PermittedSlots,
            A.InContainer & B.InContainer,
            A.InWornContainer & B.InWornContainer);

    public InteractionLimits MergeAnd(InteractionLimits B) =>
        new(InHand & B.InHand,
            OnFloor & B.OnFloor,
            PermittedSlots & B.PermittedSlots,
            InContainer & B.InContainer,
            InWornContainer & B.InWornContainer);
}
