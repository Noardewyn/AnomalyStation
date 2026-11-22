// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/play_dead.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Plays dead until a per-tick probability check (default 10%) triggers revival.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/play_dead.dm:2
public partial class datum_bt_node_ai_behavior_play_dead : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/play_dead.dm:5
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        if (controller.pawn is not mob_living_basic pawn || Dm.truthy(pawn.stat))
            return FALSE;
        Waiting.Detach(pawn.emote("deathgasp", intentional: FALSE));
        ADD_TRAIT(pawn, TRAIT_FAKEDEATH, BASIC_MOB_DEATH_TRAIT);
        pawn.look_dead();
        return null;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/play_dead.dm:12
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (SPT_PROB(probability, seconds_per_tick))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        return AI_BEHAVIOR_DELAY;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/play_dead.dm:17
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        mob_living_basic pawn = controller.pawn as mob_living_basic;
        if (QDELETED(pawn) || Dm.truthy(pawn.stat))
            return;
        pawn.visible_message(span_notice(I18n.T("[] miraculously springs back to life!", pawn)));
        REMOVE_TRAIT(pawn, TRAIT_FAKEDEATH, BASIC_MOB_DEATH_TRAIT);
        pawn.look_alive();
    }
}
