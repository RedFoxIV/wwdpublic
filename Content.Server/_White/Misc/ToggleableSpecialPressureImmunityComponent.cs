using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server._White.Misc;

[RegisterComponent]
public sealed partial class ToggleableSpecialPressureImmunityComponent : Component
{
    [DataField]
    public float? HighPressureMultiplier;
    [DataField]
    public float? HighPressureModifier;
    [DataField]
    public float? LowPressureMultiplier;
    [DataField]
    public float? LowPressureModifier;
}

public sealed class ToggleableSpecialPressureImmunityComponentySystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly BarotraumaSystem _baratrum = default!; // ha
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ToggleableSpecialPressureImmunityComponent, GetSpecialPressureProtectionValuesEvent>(OnPressureValuesEvent);
        SubscribeLocalEvent<ToggleableSpecialPressureImmunityComponent, ItemToggledEvent>(OnItemToggled);
    }

    private void OnItemToggled(EntityUid uid, ToggleableSpecialPressureImmunityComponent comp, ref ItemToggledEvent args)
    {
        if (_inventory.TryGetContainingEntity(uid, out var holder))
            _baratrum.UpdateCachedResistances(holder.Value);
    }

    private void OnPressureValuesEvent(EntityUid uid, ToggleableSpecialPressureImmunityComponent comp, ref GetSpecialPressureProtectionValuesEvent args)
    {
        if (args.Handled || !_toggle.IsActivated(uid))
            return;

        args.HighPressureMultiplier = comp.HighPressureMultiplier;
        args.HighPressureModifier = comp.HighPressureModifier;
        args.LowPressureMultiplier = comp.LowPressureMultiplier;
        args.LowPressureModifier = comp.LowPressureModifier;
        args.Handled = true;
    }
}
