using Robust.Shared.GameStates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._White.ItemSlotPicker;

[RegisterComponent]
[NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ItemSlotPickerComponent : Component
{
    [DataField("slots")]
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<string> ItemSlots = new();


    /// <summary>
    /// If there is only a single item slot occupied out of the ones available,
    /// choose said item slot instead of opening a radial menu with a single option.
    /// </summary>
    [DataField]
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool AutoTakeIfSingle = false;

    [DataField]
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool AutoClose = true;

    [DataField]
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool CloseOnLast = true;

    [DataField]
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float InsertDoAfter = 0f;

    [DataField]
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float EjectDoAfter = 0f;


}
