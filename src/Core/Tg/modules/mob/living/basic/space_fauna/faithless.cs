// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/faithless.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/faithless.dm:1
public partial class mob_living_basic_faithless : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/faithless.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_SPACEWALK, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_door_pryer)));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_SHOE));
        Waiting.NoSleep(AddElement(typeof(datum_element_mob_grabber), Dm.named("steal_from_others", FALSE)));
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_HUNT_TARGET_LIST, typecacheof(Dm.list(typeof(obj_machinery_light))));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/faithless.dm:46
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot) || target is not mob_living living_target)
            return dot;

        if (prob(paralyze_chance))
        {
            await living_target.Paralyze(paralyze_duration);
            living_target.visible_message(span_danger(Dm.text("\\The [] knocks \\the [] down!", this, target)), span_userdanger(Dm.text("\\The [] knocks you down!", this)));
        }
        return dot;
    }
}
