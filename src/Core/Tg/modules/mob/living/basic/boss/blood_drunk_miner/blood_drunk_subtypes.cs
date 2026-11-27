// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_subtypes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/// Heals slightly on melee hits
// tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_subtypes.dm:4
public partial class mob_living_basic_boss_blood_drunk_miner_guidance : mob_living_basic_boss_blood_drunk_miner
{
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_subtypes.dm:6
    [SignalHandler]
    public override object attack_override(mob_living source = null, atom target = null, object proximity = null, object modifiers = null)
    {
        object dot = base.attack_override(source, target, proximity, modifiers);
        if (Dm.truthy(Dm.bitand(dot, COMPONENT_HOSTILE_NO_ATTACK)))
            Waiting.NoSleep(adjust_brute_loss(-2 * rapid_melee_hits));
        return dot;
    }
}

/// Better at dash attacking
// tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_subtypes.dm:12
public partial class mob_living_basic_boss_blood_drunk_miner_hunter : mob_living_basic_boss_blood_drunk_miner
{
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_subtypes.dm:1  #define HUNTER_DASH_PROBABILITY 12 (file-local)
    private const float HUNTER_DASH_PROBABILITY = 12;

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_subtypes.dm:14
    public override void post_attack_effects(mob_living victim = null, DmList modifiers = null)
    {
        if (!prob(HUNTER_DASH_PROBABILITY))
            return;

        datum_action dash_attack = (ai_controller as datum_ai_controller).blackboard[BB_BDM_DASH_ATTACK_ABILITY] as datum_action;
        if (dash_attack != null)
            Waiting.Detach(dash_attack.Trigger(this, NONE, victim)); // INVOKE_ASYNC
    }
}

/// Slow but constantly dashes and has longer barrages
// tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_subtypes.dm:23
public partial class mob_living_basic_boss_blood_drunk_miner_doom : mob_living_basic_boss_blood_drunk_miner
{
    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_subtypes.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_action_cooldown dash_ability = (ai_controller as datum_ai_controller).blackboard[BB_BDM_DASH_ABILITY] as datum_action_cooldown;
        if (dash_ability != null)
            dash_ability.cooldown_time = 0.8f * SECONDS;

        datum_action_cooldown dash_attack = (ai_controller as datum_ai_controller).blackboard[BB_BDM_DASH_ATTACK_ABILITY] as datum_action_cooldown;
        if (dash_attack != null)
            dash_attack.cooldown_time = 4 * SECONDS;
        return dot;
    }

    // tg: code/modules/mob/living/basic/boss/blood_drunk_miner/blood_drunk_subtypes.dm:40
    public override object get_innate_actions()
    {
        DmList innate_abilities = Dm.list(
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_charge_basic_charge_blood_drunk_miner), BB_BDM_DASH_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_projectile_attack_rapid_fire_kinetic_accelerator_long_burst), BB_BDM_KINETIC_ACCELERATOR_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_dash_attack), BB_BDM_DASH_ATTACK_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_transform_weapon), BB_BDM_TRANSFORM_WEAPON_ABILITY));
        return innate_abilities;
    }
}
