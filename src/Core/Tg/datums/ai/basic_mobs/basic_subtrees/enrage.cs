// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/basic_subtrees/enrage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Halves the basic mob's melee attack cooldown while its health is at or below a threshold, and restores it once recovered.
// tg: code/datums/ai/basic_mobs/basic_subtrees/enrage.dm:2
public partial class datum_bt_node_ai_behavior_enrage : datum_bt_node_ai_behavior
{
    // tg: code/datums/ai/basic_mobs/basic_subtrees/enrage.dm:6
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (controller.pawn is not mob_living_basic basic_pawn)
            return AI_BEHAVIOR_FAILED;

        bool low_health = basic_pawn.health / basic_pawn.maxHealth <= health_threshold;
        bool is_enraged = Dm.truthy(controller.blackboard_key_exists(BB_BASIC_MOB_ENRAGE));

        if (low_health && !is_enraged)
        {
            float current_cooldown = basic_pawn.melee_attack_cooldown;
            controller.set_blackboard_key(BB_BASIC_MOB_ENRAGE, TRUE);
            controller.set_blackboard_key(BB_BASIC_MOB_PREVIOUS_MELEE_COOLDOWN, current_cooldown);
            basic_pawn.melee_attack_cooldown = current_cooldown / 2;

            if (Dm.truthy(controller.blackboard_key_exists(BB_CURRENT_TARGET)))
                basic_pawn.visible_message(span_danger(I18n.T("\\The [] gets an enraged look at []!", basic_pawn, controller.blackboard[BB_CURRENT_TARGET])));
            else
                basic_pawn.visible_message(span_danger(I18n.T("\\The [] gets an enraged look!", basic_pawn)));
            return AI_BEHAVIOR_SUCCEEDED;
        }

        if (!low_health && is_enraged)
        {
            // Technically something else could have modified the cooldown before/after but that requires further consideration so don't use this behavior in these scenarios
            basic_pawn.melee_attack_cooldown = Dm.num(controller.blackboard[BB_BASIC_MOB_PREVIOUS_MELEE_COOLDOWN]);
            controller.clear_blackboard_key(BB_BASIC_MOB_PREVIOUS_MELEE_COOLDOWN);
            controller.clear_blackboard_key(BB_BASIC_MOB_ENRAGE);
            return AI_BEHAVIOR_SUCCEEDED;
        }

        return AI_BEHAVIOR_FAILED;
    }
}
