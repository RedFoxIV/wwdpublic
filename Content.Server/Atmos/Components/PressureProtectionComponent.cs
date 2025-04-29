using Content.Server.Atmos.EntitySystems;

namespace Content.Server.Atmos.Components;

[RegisterComponent]
[Access(typeof(BarotraumaSystem))]
public sealed partial class PressureProtectionComponent : Component
{
    [DataField]
    public float HighPressureMultiplier = DefaultHighPressureMultiplier;

    [DataField]
    public float HighPressureModifier = DefaultHighPressureModifier;

    [DataField]
    public float LowPressureMultiplier = DefaultLowPressureMultiplier;

    [DataField]
    public float LowPressureModifier = DefaultLowPressureModifier;

    // WWDP EDIT // for clarity ig?
    public const float DefaultHighPressureMultiplier = 1f;
    public const float DefaultHighPressureModifier = 0f;
    public const float DefaultLowPressureMultiplier = 1f;
    public const float DefaultLowPressureModifier = 0f;
    // WWDP EDIT
}

/// <summary>
/// Event raised on an entity with <see cref="PressureProtectionComponent"/> in order to adjust its default values.
/// </summary>
[ByRefEvent]
public record struct GetPressureProtectionValuesEvent
{
    public float HighPressureMultiplier;
    public float HighPressureModifier;
    public float LowPressureMultiplier;
    public float LowPressureModifier;
}


/// <summary>
/// Event raised on an entity with <see cref="PressureProtectionComponent"/> in order to adjust its default values.
/// Fired on all entities equipped and held by a person except the ones in the barotrauma protection slots.
/// Also has inverse min-max behaviour: the strongest proections will be used of the weakest.
/// </summary>
[ByRefEvent]
public record struct GetSpecialPressureProtectionValuesEvent
{
    public bool Handled;
    public float? HighPressureMultiplier;
    public float? HighPressureModifier;
    public float? LowPressureMultiplier;
    public float? LowPressureModifier;
}

