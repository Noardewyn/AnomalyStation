// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/meteor_heart/chasing_spikes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the emerging ground spike (chasing_spikes.dm:36-91), the base of the tendril's spikes; the meteor heart's
// spike chaser ability comes with the meteor heart. The var blocks are generated.

/// A spike comes out of the ground, dealing damage after a short delay
// tg: code/modules/mob/living/basic/space_fauna/meteor_heart/chasing_spikes.dm:36
public partial class obj_effect_temp_visual_emerging_ground_spike : obj_effect_temp_visual
{
    /// Weighted list of body zones to target while standing
    // tg: code/modules/mob/living/basic/space_fauna/meteor_heart/chasing_spikes.dm:55 (var/static/list/standing_damage_zones)
    public static DmList standing_damage_zones => TgState.Current.obj_effect_temp_visual_emerging_ground_spike__standing_damage_zones ??= Dm.list(
        Dm.kv(BODY_ZONE_CHEST, 1),
        Dm.kv(BODY_ZONE_R_LEG, 3),
        Dm.kv(BODY_ZONE_L_LEG, 3));

    // tg: code/modules/mob/living/basic/space_fauna/meteor_heart/chasing_spikes.dm:61
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        damage_blacklist_typecache = typecacheof(damage_blacklist_typecache) as DmList;
        pixel_x = Dm.num(pixel_x) + rand(-position_variance, position_variance);
        pixel_y = Dm.num(pixel_y) + rand(-position_variance, position_variance);
        _addtimer(Dm.@new<datum_callback>(this, nameof(impale)), harm_delay, TIMER_DELETE_ME, file: "code/modules/mob/living/basic/space_fauna/meteor_heart/chasing_spikes.dm", line: 66);
        return dot;
    }

    /// Stab people who are stood on us after a delay in the shins
    // tg: code/modules/mob/living/basic/space_fauna/meteor_heart/chasing_spikes.dm:69
    public virtual async DmTask<object> impale()
    {
        if (!isturf(loc))
            return null;

        float hit_someone = FALSE;
        foreach (mob_living victim in Dm.each<mob_living>(loc))
        {
            if (Dm.truthy(await harm_mob(victim)))
                hit_someone = TRUE;
        }

        if (Dm.truthy(hit_someone))
            playsound(this, Dm.resource("sound/items/weapons/slice.ogg"), vol: 50, vary: TRUE, pressure_affected: FALSE);
        else
            playsound(this, Dm.resource("sound/misc/splort.ogg"), vol: 25, vary: TRUE, pressure_affected: FALSE);
        return hit_someone;
    }

    // tg: code/modules/mob/living/basic/space_fauna/meteor_heart/chasing_spikes.dm:84
    public virtual async DmTask<object> harm_mob(mob_living victim = null)
    {
        if (Dm.truthy(is_type_in_typecache(victim, damage_blacklist_typecache)))
            return FALSE;

        object target_zone = Dm.truthy(victim.resting) ? BODY_ZONE_CHEST : pick_weight(standing_damage_zones);
        await victim.apply_damage(impale_damage, damagetype: BRUTE, def_zone: target_zone, wound_bonus: impale_wound_bonus, sharpness: SHARP_POINTY);
        to_chat(victim, span_userdanger(I18n.T("You've been impaled by \\a []!", this)));
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/space_fauna/meteor_heart/chasing_spikes.dm:55 (var/static/list/standing_damage_zones)
    internal DmList obj_effect_temp_visual_emerging_ground_spike__standing_damage_zones;
}
