// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_ai_behaviors/find_parent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Looks around for a nearby adult of one of BB_FIND_MOM_TYPES (skipping BB_IGNORE_MOM_TYPES) and stores it.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/find_parent.dm:2
public partial class datum_bt_node_ai_behavior_find_mom : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/find_parent.dm:13
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob living_pawn = controller.pawn as mob;
        DmList mom_types = controller.blackboard[mom_types_key] as DmList;
        if (!Dm.truthy(length(mom_types)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        DmList ignore_types = controller.blackboard[ignore_types_key] as DmList;
        DmList all_moms = Dm.list();
        foreach (mob mother in Dm.each<mob>(oview(look_range, living_pawn)))
        {
            if (Dm.truthy(is_possible_mom(mother, mom_types, ignore_types)))
                Dm.append(ref all_moms, mother);
        }

        if (Dm.truthy(length(all_moms)))
        {
            controller.set_blackboard_key(found_mom_key, pick(all_moms));
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
        }
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
    }

    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/find_parent.dm:29
    public virtual object is_possible_mom(mob mother = null, DmList mom_types = null, DmList ignore_types = null)
    {
        if (!Dm.truthy(is_type_in_list(mother, mom_types)))
            return FALSE;
        if (Dm.truthy(is_type_in_list(mother, ignore_types)))
            return FALSE;
        return TRUE;
    }
}

/// A baby emotes at its parent: crying if the parent is dead, dancing happily otherwise.
// tg: code/datums/ai/basic_mobs/basic_ai_behaviors/find_parent.dm:37
public partial class datum_bt_node_ai_behavior_look_to_parent : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_ai_behaviors/find_parent.dm:41
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob parent = controller.blackboard[parent_key] as mob;
        if (QDELETED(parent))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        mob_living baby = controller.pawn as mob_living;
        if (parent.stat == DEAD)
            baby.manual_emote(I18n.T("cries for their parent!"));
        else
            baby.manual_emote(I18n.T("dances around their parent!"));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}
