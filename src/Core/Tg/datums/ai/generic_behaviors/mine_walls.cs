// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/generic_behaviors/mine_walls.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Searches for a nearby mineral wall the pawn can mine and sets the target key.
// tg: code/datums/ai/generic_behaviors/mine_walls.dm:2
public partial class datum_bt_node_ai_behavior_find_mineral_wall : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/mine_walls.dm:7
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob living_pawn = controller.pawn as mob;
        foreach (turf_closed_mineral potential_wall in Dm.each<turf_closed_mineral>(oview(9, living_pawn)))
        {
            if (!Dm.truthy(Waiting.NoSleep(check_if_mineable(controller, potential_wall))))
                continue;
            controller.set_blackboard_key(target_key, potential_wall);
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }

    /// Returns TRUE if the given wall can be approached and mined.
    // tg: code/datums/ai/generic_behaviors/mine_walls.dm:17
    public virtual async DmTask<object> check_if_mineable(datum_ai_controller controller = null, turf target_wall = null)
    {
        mob_living source = controller.pawn as mob_living;
        float direction_to_turf = get_dir(target_wall, source);
        if (!Dm.truthy(ISDIAGONALDIR(direction_to_turf)))
            return TRUE;
        foreach (object direction_check in Dm.each(GLOB.cardinals))
        {
            if (!Dm.truthy(Dm.bitand(direction_check, direction_to_turf)))
                continue;
            turf test_turf = get_step(target_wall, Dm.num(direction_check));
            if (test_turf == null)
                continue;
            if (!Dm.truthy(await test_turf.is_blocked_turf(ignore_atoms: Dm.list(source))))
                return TRUE;
        }
        return FALSE;
    }
}

/// Mines the mineral wall at target_key when adjacent. Clears the target key on finish.
// tg: code/datums/ai/generic_behaviors/mine_walls.dm:32
public partial class datum_bt_node_ai_behavior_mine_wall : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/generic_behaviors/mine_walls.dm:36
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob_living_basic living_pawn = controller.pawn as mob_living_basic;
        turf_closed_mineral target = controller.blackboard[target_key] as turf_closed_mineral;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (!Dm.truthy(Waiting.NoSleep(living_pawn.Adjacent(target))))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        if (!Dm.truthy(controller.ai_can_interact(target)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.Detach(controller.ai_interact(target));
        if (target is turf_closed_mineral_gibtonite)
            living_pawn.manual_emote(I18n.T("sighs..."));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/generic_behaviors/mine_walls.dm:50
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}
