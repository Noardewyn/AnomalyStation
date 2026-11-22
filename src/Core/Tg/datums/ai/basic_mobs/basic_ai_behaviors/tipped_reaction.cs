// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/tipped_reaction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

///A tipped-over mob looks to a nearby person for help, then resigns itself to its fate.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/tipped_reaction.dm:2
public partial class datum_bt_node_ai_behavior_tipped_reaction : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/tipped_reaction.dm:8
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_carbon tipper = controller.blackboard[tipper_key] as mob_living_carbon;

        mob_living_carbon savior = null;
        // look for someone in a radius around us for help. If our original tipper is in range, prioritize them
        foreach (mob_living_carbon potential_aid in Dm.each<mob_living_carbon>(oview(3, get_turf(controller.pawn))))
        {
            if (potential_aid == tipper)
            {
                savior = tipper;
                break;
            }
            savior = potential_aid;
        }

        string seen_message;
        string self_message;
        if (prob(75) && savior != null)
        {
            object text = pick("imploringly", "pleadingly", "with a resigned expression");
            seen_message = I18n.T("[] looks at [] [].", controller.pawn, savior, text);
            self_message = I18n.T("You look at [] [].", savior, text);
        }
        else
        {
            seen_message = I18n.T("[] seems resigned to its fate.", controller.pawn);
            self_message = I18n.T("You resign yourself to your fate.");
        }
        controller.pawn.visible_message(span_notice(seen_message), span_notice(self_message));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/tipped_reaction.dm:31
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        //I'VE SAID MY PEACE...
        controller.set_blackboard_key(reacting_key, FALSE);
        controller.clear_blackboard_key(tipper_key);
    }
}
