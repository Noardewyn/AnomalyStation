// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/break_spine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Applies full-body brute damage to a target while pulling them. Used with a move_to_target sequence.
// tg: code/datums/ai/generic_behaviors/break_spine.dm:2
public partial class datum_bt_node_ai_behavior_break_spine : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/break_spine.dm:6
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living batman = controller.blackboard[target_key] as mob_living;
        mob_living big_guy = controller.pawn as mob_living;

        if (QDELETED(batman) || get_dist(batman, big_guy) >= give_up_distance)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_FAILED;

        if (IS_UNCONSCIOUS_OR_CRIT(batman))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;

        Waiting.Detach(big_guy.start_pulling(batman));
        big_guy.face_atom(batman);
        batman.visible_message(span_warning(I18n.T("[] gets a slightly too tight hug from []!", batman, big_guy)), span_userdanger(I18n.T("You feel your body break as [] embraces you!", big_guy)));
        foreach (object zone in Dm.each(Dm.sub(GLOB.all_body_zones, BODY_ZONE_HEAD)))
            Waiting.NoSleep(batman.apply_damage(15, BRUTE, zone, wound_bonus: 35));

        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/generic_behaviors/break_spine.dm:24
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (Dm.truthy(succeeded))
        {
            mob_living attacker = controller.pawn as mob_living;
            if (!QDELETED(attacker))
                await attacker.stop_pulling();
            controller.clear_blackboard_key(target_key);
        }
    }
}

/// Bane variant: says a quote from bane.json on successful spine-breaking.
// tg: code/datums/ai/generic_behaviors/break_spine.dm:33
public partial class datum_bt_node_ai_behavior_break_spine_bane : datum_bt_node_ai_behavior_break_spine
{
    // tg: code/datums/ai/generic_behaviors/break_spine.dm:35
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        if (Dm.truthy(succeeded))
        {
            mob_living bane = controller.pawn as mob_living;
            if (QDELETED(bane))
                return;
            DmList bane_quotes = strings("bane.json", "bane") as DmList;
            Waiting.Detach(bane.say(pick(bane_quotes)));
        }
    }
}
