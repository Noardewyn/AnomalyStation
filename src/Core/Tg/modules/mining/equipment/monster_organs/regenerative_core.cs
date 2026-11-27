// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/monster_organs/regenerative_core.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * On use in hand, heals you over time and removes injury movement debuffs. Also makes you a bit sad.
 * On use when implanted, fully heals. Automatically fully heals if you would enter crit.
 */
// tg: code/modules/mining/equipment/monster_organs/regenerative_core.dm:5
public partial class obj_item_organ_monster_core_regenerative_core : obj_item_organ_monster_core
{
    // tg: code/modules/mining/equipment/monster_organs/regenerative_core.dm:15
    public override object preserve(float? implanted = null)
    {
        implanted ??= FALSE;
        if (Dm.truthy(implanted))
            SSblackbox.record_feedback("nested tally", "hivelord_core", 1, Dm.list(Dm.text("[]", type), "implanted"));
        else
            SSblackbox.record_feedback("nested tally", "hivelord_core", 1, Dm.list(Dm.text("[]", type), "stabilizer"));
        return base.preserve(implanted);
    }

    // tg: code/modules/mining/equipment/monster_organs/regenerative_core.dm:22
    public override object go_inert()
    {
        object dot = base.go_inert();
        if (!Dm.truthy(dot))
            return dot;
        SSblackbox.record_feedback("nested tally", "hivelord_core", 1, Dm.list(Dm.text("[]", type), "inert"));
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/regenerative_core.dm:28
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (owner.health <= owner.crit_threshold)
            await trigger_organ_action(TRIGGER_FORCE_AVAILABLE);
    }

    // tg: code/modules/mining/equipment/monster_organs/regenerative_core.dm:33
    public override void on_triggered_internal()
    {
        Waiting.NoSleep(owner.revive(Dm.bitand(HEAL_ALL, ~HEAL_REFRESH_ORGANS)));
        qdel(this);
    }

    /// Log applications and apply moodlet.
    // tg: code/modules/mining/equipment/monster_organs/regenerative_core.dm:38
    public override void apply_to(mob_living target = null, mob user = null)
    {
        Waiting.NoSleep(target.add_mood_event("legion_core", typeof(datum_mood_event_healsbadman)));
        if (target != user)
        {
            target.visible_message(span_notice(I18n.T("[] forces [] to apply []... Black tendrils entangle and reinforce []!", user, target, this, target.p_them())));
            SSblackbox.record_feedback("nested tally", "hivelord_core", 1, Dm.list(Dm.text("[]", type), "used", "other"));
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You start to smear [] on yourself. Disgusting tendrils hold you together and allow you to keep moving, but for how long?", this)));
            SSblackbox.record_feedback("nested tally", "hivelord_core", 1, Dm.list(Dm.text("[]", type), "used", "self"));
        }
        base.apply_to(target, user);
    }
}
