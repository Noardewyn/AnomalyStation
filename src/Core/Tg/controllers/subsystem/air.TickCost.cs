using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// DEVIATION(DV33, amends DV3): tg's SSair yields when BYOND's world.tick_usage passes the share of the tick the Master gave
// it (MC_TICK_CHECK) and finishes its cycle in the next ticks. The port has no wall clock (D31), so each unit of SSair's
// work charges a fixed virtual cost (DmWorldState.ChargeTick) and tg's own checks pause and resume SSair on it: the same
// work is spread over the same ticks on every machine and in a replay. One tick (100 percent) is 20 ms of work on the
// reference machine (an M1 Pro, Release), so SSair takes at most about 20 ms of a 50 ms server tick (TICK_LIMIT_RUNNING 98,
// TICK_LIMIT_MC 70), leaving the rest to replication, the network and slower machines. The costs were measured with a
// fire in a 48 x 48 room of lean plasma (tests/Anomaly.Server.Tests/M4b/AtmosTickBudgetTests.cs: the worst tick went from
// 65 ms to 24 ms, the fire burnt the same turfs); the ones marked estimated are not exercised by that room.
public static class AtmosTickCost
{
    /// <summary>The reference work of one virtual tick, in milliseconds.</summary>
    public const float MsPerTick = 20f;

    private const float PercentPerMs = 100f / MsPerTick;

    /// <summary>An active turf's <c>process_cell</c> (sharing with its neighbours, reacting): measured.</summary>
    public const float ActiveTurf = 0.080f * PercentPerMs;

    /// <summary>A hotspot's <c>process</c> (its exposure, reaction, spread): measured.</summary>
    public const float Hotspot = 0.030f * PercentPerMs;

    /// <summary>An excited group's cooldowns: estimated.</summary>
    public const float ExcitedGroup = 0.005f * PercentPerMs;

    /// <summary>Each turf of an excited group that breaks down or is dismantled: estimated.</summary>
    public const float ExcitedGroupTurf = 0.010f * PercentPerMs;

    /// <summary>A superconducting turf's <c>super_conduct</c>: estimated.</summary>
    public const float SuperConductingTurf = 0.020f * PercentPerMs;

    /// <summary>A turf's high pressure movements (space wind): estimated.</summary>
    public const float HighPressureTurf = 0.020f * PercentPerMs;

    /// <summary>A pipenet's <c>process</c>: estimated.</summary>
    public const float Pipenet = 0.020f * PercentPerMs;

    /// <summary>A machine's <c>process_atmos</c>: estimated.</summary>
    public const float Machine = 0.020f * PercentPerMs;

    /// <summary>An atom's <c>process_exposure</c>: estimated.</summary>
    public const float Atom = 0.010f * PercentPerMs;

    /// <summary>One step of a pipenet rebuild or an adjacency rebuild: estimated.</summary>
    public const float RebuildStep = 0.005f * PercentPerMs;
}

public partial class datum_controller_subsystem_air
{
    // DEVIATION(DV33): the virtual tick usage of a unit of work (AtmosTickCost).
    private static void ChargeTick(float percent) => DmWorldState.Current.ChargeTick(World.Current, percent);
}
