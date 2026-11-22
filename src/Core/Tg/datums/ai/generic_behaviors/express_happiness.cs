// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/express_happiness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Occasionally emotes based on the pawn's current happiness level.
// tg: code/datums/ai/generic_behaviors/express_happiness.dm:2
public partial class datum_bt_node_ai_behavior_express_happiness : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/express_happiness.dm:18-30  the var/static lists: constant content, a local of each list
    private static DmList default_happy_emotions() => Dm.list("celebrates happily!", "dances around in excitement!");
    private static DmList default_moderate_emotions() => Dm.list("looks satisfied.", "trots around.");
    private static DmList default_depressed_emotions() => Dm.list("looks depressed...", "turns its back and sulks...", "looks towards the floor in dissapointment...");

    // tg: code/datums/ai/generic_behaviors/express_happiness.dm:32
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (!SPT_PROB(emote_probability, seconds_per_tick))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;

        object happiness = controller.blackboard[happiness_key];
        if (happiness == null)
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;

        DmList emotion_list;
        if (Dm.num(happiness) >= high_happiness_threshold)
            emotion_list = controller.blackboard[happy_key] as DmList ?? default_happy_emotions();
        else if (Dm.num(happiness) >= moderate_happiness_threshold)
            emotion_list = controller.blackboard[moderate_key] as DmList ?? default_moderate_emotions();
        else
            emotion_list = controller.blackboard[sad_key] as DmList ?? default_depressed_emotions();

        if (!Dm.truthy(length(emotion_list)))
            return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;

        mob_living living_pawn = controller.pawn as mob_living;
        living_pawn.manual_emote(pick(emotion_list));
        return AI_BEHAVIOR_INSTANT | AI_BEHAVIOR_SUCCEEDED;
    }
}
