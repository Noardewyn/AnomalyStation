// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/boss/thing/thing_objects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps WP1, D333): only the exclamation marks (:51-79), which the Sky Bulge spear's jump shows over its bearer
// (weaponry/melee/spear.dm): a mark that lasts the duration it is given, its kind that follows a thing as it moves, and its kind
// that grows in from twice its size. The Thing boss's own spikes and acid (:1-49, :82-140) come with the boss and its ruin
// (not-translated.md 211). The var blocks are generated (D111).

// tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:51
public partial class obj_effect_temp_visual_telegraphing_exclamation : obj_effect_temp_visual_telegraphing
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:56
    [DmParams("mapload", "duration")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        if (duration != null)
            this.duration = Dm.num(duration);
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:61
public partial class obj_effect_temp_visual_telegraphing_exclamation_following : obj_effect_temp_visual_telegraphing_exclamation
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:61
    [DmParams("mapload", "duration", "following")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom_movable following = Dm.arg(__extra, 1) as atom_movable;
        object dot = base.Initialize(mapload, __extra);
        if (following == null)
            return INITIALIZE_HINT_QDEL;
        glide_size = following.glide_size;
        RegisterSignal(following, COMSIG_MOVABLE_MOVED, nameof(follow));
        return dot;
    }

    ///called when the thing we're following moves
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:69
    [SignalHandler]
    public virtual void follow(datum source = null)
    {
        Waiting.NoSleep(forceMove(get_turf(source)));
    }
}

// tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:74
public partial class obj_effect_temp_visual_telegraphing_exclamation_animated : obj_effect_temp_visual_telegraphing_exclamation
{
    // tg: code/modules/mob/living/basic/boss/thing/thing_objects.dm:77
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        transform = Dm.matrix().Multiply(2);
        animate(this, Dm.named("alpha", 255), Dm.named("transform", Dm.matrix()), Dm.named("time", duration / 3));
        return dot;
    }
}
