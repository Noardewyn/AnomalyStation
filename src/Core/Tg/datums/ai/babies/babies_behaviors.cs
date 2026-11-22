// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/babies/babies_behaviors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/// Find someone to erp with
// tg: code/datums/ai/babies/babies_behaviors.dm:5
public partial class datum_bt_node_ai_behavior_find_partner : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/babies/babies_behaviors.dm:1
    private const float FIND_PARTNER_COOLDOWN = 1 * MINUTES;

    // tg: code/datums/ai/babies/babies_behaviors.dm:12
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob_living living_pawn = controller.pawn as mob_living;
        if (Dm.eq(living_pawn.gender, FEMALE))
            return FALSE;
        if (!Dm.truthy(controller.blackboard[partner_types_key]) || !Dm.truthy(controller.blackboard[child_types_key]))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/ai/babies/babies_behaviors.dm:20
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob pawn_mob = controller.pawn as mob;
        mob_living living_pawn = controller.pawn as mob_living;
        float maximum_pop = Dm.truthy(controller.blackboard[BB_MAX_CHILDREN]) ? Dm.num(controller.blackboard[BB_MAX_CHILDREN]) : max_nearby_pop;
        DmList similar_species_types = Dm.add(controller.blackboard[partner_types_key], controller.blackboard[child_types_key]) as DmList;
        DmList possible_partners = Dm.list();
        float nearby_pop = 0;

        foreach (mob_living other in Dm.each<mob_living>(oview(range, pawn_mob)))
        {
            if (!Dm.truthy(pawn_mob.faction_check_atom(other)))
                return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

            if (!Dm.truthy(is_type_in_list(other, similar_species_types)))
                continue;

            if (++nearby_pop >= maximum_pop)
            {
                controller.set_blackboard_key(BB_PARTNER_SEARCH_TIMEOUT, world.time + FIND_PARTNER_COOLDOWN);
                return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
            }

            if (!HAS_TRAIT(other, TRAIT_MOB_BREEDER) || Dm.truthy(other.ckey))
                continue;

            if (IS_UNCONSCIOUS_OR_CRIT(other)) //Check if it's conscious FIRST.
                continue;

            if (!Dm.eq(other.gender, living_pawn.gender) && !Dm.truthy(Dm.bitand(other.flags_1, HOLOGRAM_1)))
                Dm.append(ref possible_partners, other);
        }

        if (!Dm.truthy(length(possible_partners)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        controller.set_blackboard_key(target_key, pick(possible_partners));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }
}

/// I'm not saying what this behavior does :flushed:
// tg: code/datums/ai/babies/babies_behaviors.dm:56
public partial class datum_bt_node_ai_behavior_make_babies : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/babies/babies_behaviors.dm:59
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        mob target = controller.blackboard[target_key] as mob;
        return !QDELETED(target) && !IS_UNCONSCIOUS_OR_CRIT(target) ? TRUE : FALSE;
    }

    // tg: code/datums/ai/babies/babies_behaviors.dm:63
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        mob target = controller.blackboard[target_key] as mob;
        if (QDELETED(target) || IS_UNCONSCIOUS_OR_CRIT(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;
        Waiting.Detach(controller.ai_interact(target, FALSE));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/datums/ai/babies/babies_behaviors.dm:70
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}
