// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/ed209/ed209_ability.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/ed209/ed209_ability.dm:1
public partial class datum_action_cooldown_mob_cooldown_ed209_charge : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/bots/ed209/ed209_ability.dm:13
    public override async DmTask<object> Activate(atom target = null)
    {
        turf target_turf = get_turf(target);
        if (isclosedturf(target_turf) || isspaceturf(target_turf))
        {
            owner.balloon_alert(owner, "base not suitable!");
            return FALSE;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(commence_launch), target), telegraph_duration, file: "code/modules/mob/living/basic/bots/ed209/ed209_ability.dm", line: 18);
        owner.Shake(duration: telegraph_duration);
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209_ability.dm:23
    public virtual async DmTask commence_launch(atom target = null)
    {
        turf target_turf = get_turf(target);
        await owner.throw_at(target: target_turf, range: 7, speed: 1, spin: FALSE, callback: Dm.@new<datum_callback>(this, nameof(on_tackle), target_turf));
        Dm.@new<obj_effect_temp_visual_mook_dust>(owner.loc);
    }

    // tg: code/modules/mob/living/basic/bots/ed209/ed209_ability.dm:28
    public virtual async DmTask on_tackle(turf target = null, object original_pixel_y = null)
    {
        playsound(get_turf(owner), Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
        Dm.@new<obj_effect_temp_visual_mook_dust>(owner.loc);
        foreach (mob_living victim in Dm.each<mob_living>(oview(1, owner)))
        {
            if (Dm.@in(victim, owner.buckled_mobs))
                continue;
            await victim.apply_damage(tackle_damage);
            if (QDELETED(victim))
                continue;
            object throw_dir = victim.loc == owner.loc ? get_dir(owner, victim) : pick(GLOB.alldirs);
            turf throwtarget = get_edge_target_turf(victim, throw_dir) as turf;
            await victim.throw_at(target: throwtarget, range: 3, speed: 1);
            victim.visible_message(span_warning(Dm.text("[] eats steel!", victim)));
        }
    }
}
