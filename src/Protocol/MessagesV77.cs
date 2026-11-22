namespace Anomaly.Protocol;

// Protocol version 77 (DV371, D325): an entity's announced level ride (NetField.Ride).

/// <summary>
/// Version 77: a level change announced when its wait starts (tg's elevator ride, a ladder's climb): the levels it crosses
/// (<see cref="Dz"/>: -1 down, +1 up), the tick it arrives at and how many ticks it takes. The client draws the entity crossing
/// the level over those ticks; <see cref="None"/> (Dz 0) for no ride.
/// </summary>
public readonly record struct RideState(sbyte Dz, long EndTick, ushort Ticks)
{
    public static readonly RideState None = default;

    public bool IsNone => Dz == 0;
}
