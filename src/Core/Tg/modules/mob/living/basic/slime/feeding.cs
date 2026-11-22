// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/feeding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

public partial class mob_living_basic_slime
{
    private const string FEEDING_OFFSET = "feeding"; // tg: code/modules/mob/living/basic/slime/feeding.dm:57

    ///Can the slime eat this mob
    // tg: code/modules/mob/living/basic/slime/feeding.dm:2
    public virtual object can_feed_on(mob_living meal = null, object silent = null, object check_adjacent = null, object check_friendship = null)
    {
        if (!isliving(meal)) //sanity check
            return FALSE;

        if (IS_UNCONSCIOUS_OR_CRIT(this))
        {
            if (stat == DEAD)
                balloon_alert(this, "dead!");
            else if (IS_UNCONSCIOUS(this))
                balloon_alert(this, "unconscious!");
            else
                balloon_alert(this, "in critical!");
            return FALSE;
        }

        if (Dm.truthy(hunger_disabled))
        {
            if (!Dm.truthy(silent))
                balloon_alert(this, "not hungry!");
            return FALSE;
        }

        if (Dm.truthy(check_friendship) && Dm.truthy(has_faction(REF(meal))))
            return FALSE;

        if (Dm.truthy(check_adjacent) && (!Dm.truthy(Waiting.NoSleep(Adjacent(meal))) || !isturf(loc)))
            return FALSE;

        if (!Dm.truthy(Dm.bitand(mobility_flags, MOBILITY_MOVE)))
        {
            if (!Dm.truthy(silent))
                balloon_alert(this, "can't move!");
            return FALSE;
        }

        if (meal.stat == DEAD)
        {
            if (!Dm.truthy(silent))
                balloon_alert(this, "no life energy!");
            return FALSE;
        }

        if (Dm.locate<mob_living_basic_slime>(meal.buckled_mobs) != null)
        {
            if (!Dm.truthy(silent))
                balloon_alert(this, "another slime in the way!");
            return FALSE;
        }

        if (issilicon(meal) || Dm.truthy(Dm.bitand(meal.mob_biotypes, MOB_ROBOTIC)) || Dm.truthy(Dm.bitand(meal.flags_1, HOLOGRAM_1)))
        {
            balloon_alert(this, "no life energy!");
            return FALSE;
        }

        if (isslime(meal))
        {
            if (!Dm.truthy(silent))
                balloon_alert(this, "can't eat slime!");
            return FALSE;
        }

        if (Dm.num(GET_PHYSIOLOGY(meal, BRUTE)) <= 0 && Dm.num(GET_PHYSIOLOGY(meal, TOX)) <= 0) //The creature wouldn't take any damage, it must be too weird even for us.
        {
            if (!Dm.truthy(silent))
                balloon_alert(this, "not food!");
            return FALSE;
        }

        return TRUE;
    }

    ///The slime will start feeding on the target
    // tg: code/modules/mob/living/basic/slime/feeding.dm:60
    public virtual async DmTask start_feeding(mob_living target_mob = null)
    {
        target_mob.unbuckle_all_mobs(force: TRUE); //Slimes rip other mobs (eg: shoulder parrots) off (Slimes Vs Slimes is already handled in can_feed_on())
        if (Dm.truthy(await target_mob.buckle_mob(this, force: TRUE)))
        {
            add_offsets(FEEDING_OFFSET, y_add: Dm.num(target_mob.mob_size) <= MOB_SIZE_SMALL ? 0f : 3f);
            layer = MOB_ABOVE_PIGGYBACK_LAYER; //appear above the target mob
            await target_mob.apply_status_effect(typeof(datum_status_effect_slime_leech), this);
            target_mob.visible_message(
                span_danger(Dm.text("[] latches onto []!", name, target_mob)),
                span_userdanger(Dm.text("[] latches onto []!", name, target_mob)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
            to_chat(this, span_notice(Dm.text("<i>I start feeding on []...</i>", target_mob)));
            balloon_alert(this, "feeding started");
        }
        else
            balloon_alert(this, "latch failed!");
    }

    ///The slime will stop feeding
    // tg: code/modules/mob/living/basic/slime/feeding.dm:80
    public virtual void stop_feeding(object silent = null)
    {
        if (buckled == null)
            return;

        if (!Dm.truthy(silent))
        {
            visible_message(span_warning(Dm.text("[] lets go of []!", this, buckled)), span_notice(Dm.text("You let go of []", buckled)));
            balloon_alert(this, "feeding stopped");
        }
        remove_offsets(FEEDING_OFFSET);
        layer = Dm.num(initial(this, "layer"));
        Waiting.Detach(buckled.unbuckle_mob(this, force: TRUE));
    }
}
