// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/temporary_visuals/temporary_visual.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 1 (the blood splatter of a hit): the whole file, the temporary visual's life (a random direction, deleted after its
// duration) and the direction-setting visual; M5x slice 5 added the singularity's acts. The var blocks are generated (D111).

///temporary visual effects
// tg: code/game/objects/effects/temporary_visuals/temporary_visual.dm:2
[DmPath("/obj/effect/temp_visual")]
public partial class obj_effect_temp_visual : obj_effect
{
    // tg: code/game/objects/effects/temporary_visuals/temporary_visual.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(randomdir))
            Waiting.NoSleep(setDir(pick(GLOB.cardinals)));

        // QDEL_IN_STOPPABLE(src, duration) (__DEFINES/qdel.dm:61), inline (rules 11.1 (f))
        timerid = _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), Dm.num(duration) > GC_FILTER_QUEUE ? WEAKREF(this) : this), Dm.num(duration), TIMER_STOPPABLE, file: "code/game/objects/effects/temporary_visuals/temporary_visual.dm", line: 19);
        return dot;
    }

    // tg: code/game/objects/effects/temporary_visuals/temporary_visual.dm:21
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        deltimer(timerid);
        return dot;
    }
}

// tg: code/game/objects/effects/temporary_visuals/temporary_visual.dm:31
public partial class obj_effect_temp_visual_dir_setting : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/temporary_visual.dm:34
    [DmParams("mapload", "set_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object set_dir = Dm.arg(__extra, 0);
        if (Dm.truthy(set_dir))
            Waiting.NoSleep(setDir(set_dir));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/temporary_visuals/temporary_visual.dm:25 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_effect_temp_visual : obj_effect
{
    // tg: code/game/objects/effects/temporary_visuals/temporary_visual.dm:25
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/game/objects/effects/temporary_visuals/temporary_visual.dm:28
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}
