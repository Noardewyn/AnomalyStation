// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/lighting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 3 (fire): the whole file (a burning mob carries a /obj/effect/dummy/lighting_obj/moblight/fire).

/**
 * Basically, a fake object that emits light.
 *
 * Why is this used sometimes instead of giving atoms light values directly?
 * Because using these, you can have multiple light sources in a single object.
 */
// tg: code/game/objects/effects/lighting.dm:7
public partial class obj_effect_dummy_lighting_obj : obj_effect_dummy
{
    // tg: code/game/objects/effects/lighting.dm:17
    [DmParams("mapload", "range", "power", "color", "duration")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object range = Dm.arg(__extra, 0);
        object power = Dm.arg(__extra, 1);
        object color = Dm.arg(__extra, 2);
        object duration = Dm.arg(__extra, 3);
        object dot = base.Initialize(mapload, Dm.forward(__extra, range, power, color, duration));
        if (range != null)
            set_light_range(range);
        if (power != null)
            set_light_power(power);
        if (color != null)
            set_light_color(color);
        if (Dm.truthy(duration))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), Dm.num(duration) > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), Dm.num(duration), file: "code/game/objects/effects/lighting.dm", line: 26); // QDEL_IN(src, duration), inline (rules 11.1 (f))
        return dot;
    }
}

// tg: code/game/objects/effects/lighting.dm:28
public partial class obj_effect_dummy_lighting_obj_moblight : obj_effect_dummy_lighting_obj
{
    // tg: code/game/objects/effects/lighting.dm:31
    [DmParams("mapload", "range", "power", "color", "duration")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!ismob(loc))
            return INITIALIZE_HINT_QDEL;
        return dot;
    }
}
