// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Lavaland mob which tries to line up with its target and fire a laser
// tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:2
public partial class mob_living_basic_mining_brimdemon : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_CLAW));
        beam = Dm.@new<datum_action_cooldown_mob_cooldown_brimbeam>(this);
        Waiting.NoSleep(beam.Grant(this));
        (ai_controller as datum_ai_controller).set_blackboard_key(BB_TARGETED_ACTION, beam);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:48
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref beam);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:52
    public override async DmTask<object> RangedAttack(atom target = null, object modifiers = null)
    {
        await beam.Trigger(null, null, target);
        return null;
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:55
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        if (Dm.truthy(gibbed))
            return dot;
        obj_effect_temp_visual_brim_burst bang = Dm.@new<obj_effect_temp_visual_brim_burst>(loc);
        await forceMove(bang);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:62
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stat != DEAD)
            Dm.Deferred("code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:65", "update_overlays: emissive_appearance(icon, \"[icon_living]_e\", src), the brimdemon's glow in the dark (DEVIATION DV12)");
        return dot;
    }
}

/// Show a funny animation before doing an explosion
// tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:68
public partial class obj_effect_temp_visual_brim_burst : obj_effect_temp_visual
{
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:74
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(bang)), duration - (1 * DECISECONDS), TIMER_DELETE_ME, file: "code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm", line: 76);
        animate(this, Dm.named("color", "#ff8888"), Dm.named("transform", Dm.matrix().Scale(1.1f)), Dm.named("time", 0.7f * SECONDS));
        animate(Dm.named("color", "#ffffff"), Dm.named("transform", Dm.matrix()), Dm.named("time", 0.2f * SECONDS));
        animate(Dm.named("color", "#ff4444"), Dm.named("transform", Dm.matrix().Scale(1.3f)), Dm.named("time", 0.5f * SECONDS));
        animate(Dm.named("color", "#ffffff"), Dm.named("transform", Dm.matrix()), Dm.named("time", 0.2f * SECONDS));
        animate(Dm.named("color", "#ff0000"), Dm.named("transform", Dm.matrix().Scale(1.5f)), Dm.named("time", 0.3f * SECONDS));
        return dot;
    }

    /// Make an explosion
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon.dm:84
    public virtual async DmTask bang()
    {
        turf origin_turf = get_turf(this) as turf;
        playsound(origin_turf, Dm.resource("sound/effects/pop_expl.ogg"), 50);
        Dm.@new<obj_effect_temp_visual_explosion_fast>(origin_turf);
        DmList possible_targets = Builtins.range(1, origin_turf);
        foreach (mob_living target in Dm.each<mob_living>(possible_targets))
        {
            object armor = target.run_armor_check(attack_flag: BOMB);
            await target.apply_damage(20, damagetype: BURN, blocked: Dm.num(armor), spread_damage: TRUE);
        }

        foreach (atom_movable thing in Dm.each_cast<atom_movable>(contents))
            await thing.forceMove(loc);
    }
}
