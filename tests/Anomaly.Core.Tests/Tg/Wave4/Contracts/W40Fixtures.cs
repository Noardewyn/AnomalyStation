using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;

namespace Anomaly.Core.Tests.Tg.Wave4.Contracts;

// Fixtures of the W4.0 contracts tests (wave 4): the hand-written builtins (step, image), the constructor hook of the
// builtin types (rules 10.2, "Builtin types"), the completed /atom procs and the translated helpers. Fixture paths are
// /<kind>/test_w40_<name> (translation-rules.md 15), which no tg path starts with. Their New does not call ..(), so the
// engine and the contracts are exercised without tg's /atom/New (SSatoms). What they record goes to the per-world
// EngineTrace of the wave-3 engine tests.

/// <summary>A movable whose Move records its call and returns what the test set (step's return value, move-chain.md E2).</summary>
[DmPath("/obj/test_w40_mover")]
public class obj_test_w40_mover : obj
{
    /// <summary>What Move returns; Move does not move.</summary>
    public object move_result = Dm.TRUE;

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        EngineTrace.Add("Move " + EngineTrace.D(newloc) + " dir=" + EngineTrace.D(direction));
        return move_result;
    }

    /// <summary>Records CanPassThrough (called by /atom/proc/CanPass when generic_canpass is FALSE) and returns 7.</summary>
    public override object CanPassThrough(atom blocker = null, object movement_dir = null, object blocker_opinion = null)
    {
        EngineTrace.Add("CanPassThrough " + EngineTrace.D(blocker) + " dir=" + EngineTrace.D(movement_dir) + " opinion=" + EngineTrace.D(blocker_opinion));
        return 7;
    }
}

/// <summary>A dense obj with tg's /obj defaults, as a CanPass target.</summary>
[DmPath("/obj/test_w40_block")]
public class obj_test_w40_block : obj
{
    public obj_test_w40_block()
    {
        density = Dm.TRUE;
    }

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>The toy world's room (tg's /area/misc/testroom, STANDARD_GRAVITY) without tg's /area/New.</summary>
[DmPath("/area/misc/testroom/test_w40_room")]
public class area_misc_testroom_test_w40_room : area_misc_testroom
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>tg's /area/space (NO_GRAVITY) without tg's /area/New.</summary>
[DmPath("/area/space/test_w40_space")]
public class area_space_test_w40_space : area_space
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>A turf that forces no gravity (tg's /turf/var/force_no_gravity) without tg's /turf/New.</summary>
[DmPath("/turf/test_w40_nograv")]
public class turf_test_w40_nograv : turf
{
    public turf_test_w40_nograv()
    {
        force_no_gravity = Dm.TRUE;
    }

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>A plain mob without tg's /atom/New.</summary>
[DmPath("/mob/test_w40_mob")]
public class mob_test_w40_mob : mob
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}
