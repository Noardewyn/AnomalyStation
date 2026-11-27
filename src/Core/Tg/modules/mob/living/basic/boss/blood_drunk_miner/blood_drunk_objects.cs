// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_objects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_objects.dm:11
public partial class obj_effect_temp_visual_dir_setting_miner_death : obj_effect_temp_visual_dir_setting
{
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_objects.dm:15
    [DmParams("mapload", "set_dir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.Detach(fade_out()); // INVOKE_ASYNC
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_objects.dm:19
    public virtual async DmTask fade_out()
    {
        matrix our_matrix = Dm.matrix();
        our_matrix.Turn(Dm.num(pick(90, 270)));
        object final_dir = dir;
        if (Dm.truthy(Dm.bitand(dir, Dm.bitor(EAST, WEST)))) //Facing east or west
            final_dir = pick(NORTH, SOUTH); //So you fall on your side rather than your face or ass

        animate(this, Dm.named("transform", our_matrix), Dm.named("pixel_y", -6), Dm.named("dir", final_dir), Dm.named("time", 2), Dm.named("easing", QUAD_EASING));
        await sleep(0.5f * SECONDS);
        animate(this, Dm.named("color", Dm.list("#A7A19E", "#A7A19E", "#A7A19E", Dm.list(0, 0, 0))), Dm.named("time", 10), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
        await sleep(0.4f * SECONDS);
        animate(this, Dm.named("alpha", 0), Dm.named("time", 6), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)), Dm.named("flags", ANIMATION_PARALLEL));
    }
}
