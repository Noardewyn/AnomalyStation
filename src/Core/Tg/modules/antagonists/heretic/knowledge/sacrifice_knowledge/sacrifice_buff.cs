// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/heretic/knowledge/sacrifice_knowledge/sacrifice_buff.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only fire_curse_hand translated (gaps RG2, D339: helgrasp, the inverse of helbital, and the necropolis curse throw curse
// hands); the heretic's unholy determination waits for the heretic.

public static partial class GlobalProcs
{
    /// Torment the target with a frightening hand
    // tg: code/modules/antagonists/heretic/knowledge/sacrifice_knowledge/sacrifice_buff.dm:101
    public static async DmTask fire_curse_hand(mob_living_carbon victim = null, turf forced_turf = null, float? range = null, Type projectile_type = null)
    {
        range ??= 8;
        projectile_type ??= typeof(obj_projectile_curse_hand_hel);
        object grab_dir = turn(victim.dir, Dm.num(pick(-90, 90, 180, 180))); // Not in front, favour behind
        turf spawn_turf = get_ranged_target_turf(victim, grab_dir, range) as turf;
        spawn_turf = forced_turf != null ? forced_turf : spawn_turf;
        if (spawn_turf == null)
            return;
        Dm.@new<obj_effect_temp_visual_dir_setting_curse_grasp_portal>(spawn_turf, victim.dir);
        playsound(spawn_turf, Dm.resource("sound/effects/curse/curse2.ogg"), 80, TRUE, -1);
        obj_projectile hand = Dm.@new(projectile_type, spawn_turf) as obj_projectile;
        await hand.aim_projectile(victim, spawn_turf);
        if (QDELETED(hand)) // safety check if above fails - above has a stack trace if it does fail
            return;
        await hand.fire();
    }
}
