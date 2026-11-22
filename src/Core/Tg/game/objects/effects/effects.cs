// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of a type that the wave-4 var blocks and signatures name (rules 2.3: the earliest .dme file that declares it), written by the W4.0 contracts; only the type is declared, and the file is translated with its module.
// M4b slice 1 (the air that moves): /obj/effect/experience_pressure_difference (:36), so the space wind leaves effects
// where they are. M4b slice 3 (fire): /obj/effect/fire_act (:24), so fire leaves effects alone. M5x slice 1 (explosions):
// /obj/effect/ex_act (:39), so a blast leaves effects alone.

// tg: code/game/objects/effects/effects.dm:4
[DmPath("/obj/effect")]
public partial class obj_effect : obj
{
    // tg: code/game/objects/effects/effects.dm:24
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        return null;
    }

    // tg: code/game/objects/effects/effects.dm:27
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/objects/effects/effects.dm:39
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/objects/effects/effects.dm:36
    public override void experience_pressure_difference(object pressure_difference = null, object direction = null, float? pressure_resistance_prob_delta = null)
    {
        return;
    }
}

// tg: code/game/objects/effects/effects.dm:46
[DmPath("/obj/effect/abstract")]
public partial class obj_effect_abstract : obj_effect
{
}

// tg: code/game/objects/effects/effects.dm:42 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_effect : obj
{
    // tg: code/game/objects/effects/effects.dm:42
    public override async DmTask<object> singularity_act()
    {
        qdel(this);
        return null;
    }
}

// tg: code/game/objects/effects/effects.dm:49 (M5x slice 5)
public partial class obj_effect_abstract : obj_effect
{
    // tg: code/game/objects/effects/effects.dm:49
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }

    // tg: code/game/objects/effects/effects.dm:52
    public override async DmTask<object> singularity_act()
    {
        return null;
    }
}
