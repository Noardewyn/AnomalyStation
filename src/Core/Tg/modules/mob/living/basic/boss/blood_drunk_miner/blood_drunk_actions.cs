// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm:1
public partial class datum_action_cooldown_mob_cooldown_charge_basic_charge_blood_drunk_miner : datum_action_cooldown_mob_cooldown_charge_basic_charge
{
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm:13
    public override async DmTask hit_target(atom_movable source = null, object target = null, object damage_dealt = null)
    {
        await base.hit_target(source, target, damage_dealt);
        if (!isbasicmob(source) || !isliving(target))
            return;
        mob_living_basic basic_source = (mob_living_basic)source;
        await basic_source.melee_attack(target as atom, ignore_cooldown: TRUE);
    }
}

// tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm:20
public partial class datum_action_cooldown_mob_cooldown_transform_weapon : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm:30
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        disable_cooldown_actions();
        await do_transform();
        StartCooldown(rand(Dm.num(cooldown_time), max_cooldown_time), 0);
        enable_cooldown_actions();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm:37
    public virtual async DmTask do_transform()
    {
        if (owner is not mob_living_basic_boss_blood_drunk_miner blood_drunk_miner)
            return;
        await blood_drunk_miner.transform_saw();
    }
}

// tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm:43
public partial class datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_kinetic_accelerator : datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire
{
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm:62
    public override async DmTask<object> Activate(atom target_atom = null)
    {
        owner.visible_message(span_danger(I18n.T("[] fires the proto-kinetic accelerator!", owner)));
        owner.face_atom(target_atom);
        owner.do_alert_animation(alert_delay + (shot_count - 1) * shot_delay);
        disable_cooldown_actions();
        if (alert_delay > prefire_delay) // As to delay movement blocking
        {
            await sleep(alert_delay - prefire_delay); // SLEEP_CHECK_DEATH(alert_delay - prefire_delay, owner)
            if (QDELETED(owner) || owner.stat == DEAD)
                return null;
        }
        return await base.Activate(target_atom);
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm:71
    public override async DmTask<object> attack_sequence(mob_living firer = null, atom target = null)
    {
        await sleep(prefire_delay); // SLEEP_CHECK_DEATH(prefire_delay, firer)
        if (QDELETED(firer) || firer.stat == DEAD)
            return null;
        object dot = await base.attack_sequence(firer, target);
        await sleep(reload_delay);
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_actions.dm:76
    public override obj_projectile shoot_projectile(atom origin = null, atom target = null, object set_angle = null, mob firer = null, object projectile_spread = null, object speed_multiplier = null, object override_projectile_type = null, object override_homing = null)
    {
        obj_projectile dot = base.shoot_projectile(origin, target, set_angle, firer, projectile_spread, speed_multiplier, override_projectile_type, override_homing);
        Dm.@new<obj_effect_temp_visual_dir_setting_firing_effect>(get_turf(firer), firer.dir);
        return dot;
    }
}
