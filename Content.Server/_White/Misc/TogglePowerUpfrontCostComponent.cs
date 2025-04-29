using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Shared.Item.ItemToggle.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server._White.Misc;

[RegisterComponent]
public sealed partial class ToggleCellDrawUpfrontComponent : Component
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float ActivateCost = 0f;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float DeactivateCost = 0f;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public string Popup = "not-enough-charge-upfront-popup";
}

public sealed class TogglePowerUpfrontCostSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly BatterySystem _battery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ToggleCellDrawUpfrontComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<ToggleCellDrawUpfrontComponent, ItemToggleDeactivateAttemptEvent>(OnDeactivateAttempt);
    }

    private void OnActivateAttempt(EntityUid uid, ToggleCellDrawUpfrontComponent comp, ItemToggleActivateAttemptEvent args)
    {
        if (comp.ActivateCost <= 0 || _cell.TryUseCharge(uid, comp.ActivateCost) || _battery.TryUseCharge(uid, comp.ActivateCost))
            return;
        args.Popup = Loc.GetString(comp.Popup);
        args.Cancelled = true;
    }

    private void OnDeactivateAttempt(EntityUid uid, ToggleCellDrawUpfrontComponent comp, ItemToggleDeactivateAttemptEvent args)
    {
        if (comp.DeactivateCost <= 0 || _cell.TryUseCharge(uid, comp.DeactivateCost) || _battery.TryUseCharge(uid, comp.DeactivateCost))
            return;
        //args.Popup = Loc.GetString(comp.Popup); // damn
        args.Cancelled = true;
    }
}
