using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// DV33 (amends DV3): world.tick_usage is a deterministic virtual tick usage, in percent of a tick as BYOND's: the work a
// proc charges with ChargeTick (a fixed cost per unit of work, never a wall clock, so a replay and every machine run the
// same), counted from 0 at the start of each world tick. tg's MC reads it as it reads BYOND's (TICK_CHECK, MC_TICK_CHECK,
// the Master's share of the tick per subsystem), so a subsystem that charges is paused past its share and resumed the
// next tick, as tg spreads its heavy subsystems. Only SSair charges for now (air.cs, AtmosTickCost); everything else
// costs 0, as under DV3.
public sealed partial class DmWorldState
{
    private World _usageWorld;
    private long _usageTick = -1;
    private float _usage;

    /// <summary><c>world.tick_usage</c>: the virtual usage charged in the world's current tick (DV33).</summary>
    public float TickUsage => _usageWorld != null && _usageWorld.Tick == _usageTick ? _usage : 0f;

    /// <summary>Charges <paramref name="percent"/> of a tick to the world's current tick (DV33).</summary>
    public void ChargeTick(World world, float percent)
    {
        if (!ReferenceEquals(world, _usageWorld) || world.Tick != _usageTick)
        {
            _usageWorld = world;
            _usageTick = world.Tick;
            _usage = 0f;
        }
        _usage += percent;
    }
}
