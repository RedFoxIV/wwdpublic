namespace Content.Shared._White.ItemSlotAction;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class ItemSlotActionComponent : Component
{
    [DataField("slots")]
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<string> ItemSlots = new();

    //[DataField]
    //[AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    //public bool Blacklist = true;
}

