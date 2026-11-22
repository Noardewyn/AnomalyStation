// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/deer/deer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/farm_animals/deer/deer.dm:1
public partial class mob_living_basic_deer : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/deer/deer.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), Dm.named("footstep_type", FOOTSTEP_MOB_SHOE)));
        float time_to_freeze_for = rand(5, 10) * SECONDS;
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        controller.set_blackboard_key(BB_STATIONARY_SECONDS, time_to_freeze_for);
        controller.set_blackboard_key(BB_STATIONARY_COOLDOWN, time_to_freeze_for * rand(3, 5));
        // Things that will scare us into being stationary. Vehicles are scary to deers because they might have headlights. (tg's var/static stationary_scary_things)
        controller.set_blackboard_key(BB_STATIONARY_TARGETS, typecacheof(Dm.list(typeof(obj_vehicle))));
        return dot;
    }
}
