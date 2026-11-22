// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/hivebot/_hivebot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole folder's mobs (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/hivebot/_hivebot.dm:1
public partial class mob_living_basic_hivebot : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/hivebot/_hivebot.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_decal_cleanable_blood_gibs_robot_debris)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_appearance_on_aggro), Dm.named("overlay_icon", icon), Dm.named("overlay_state", Dm.text("[]_attack", Dm.initial(GetType(), (atom p) => p.icon_state)))));
        if (!Dm.truthy(ranged_attacker))
            return dot;
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks), typeof(obj_item_ammo_casing_hivebot), Dm.named("cooldown_time", ranged_attack_cooldown)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/hivebot/_hivebot.dm:50
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        do_sparks(3, TRUE, this);
        return await base.death(gibbed, __extra);
    }
}

// tg: code/modules/mob/living/basic/space_fauna/hivebot/_hivebot.dm:82
public partial class mob_living_basic_hivebot_mechanic : mob_living_basic_hivebot
{
    // tg: code/modules/mob/living/basic/space_fauna/hivebot/_hivebot.dm:97
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(Dm.@new<datum_action_cooldown_spell_conjure_foam_wall>(this).Grant(this)); // GRANT_ACTION
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/hivebot/_hivebot.dm:101
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;

        if (ismachinery(target))
        {
            repair_machine(target as obj_machinery);
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }

        if (target is mob_living_basic_hivebot bot_target)
        {
            await repair_hivebot(bot_target);
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/hivebot/_hivebot.dm:114
    public virtual void repair_machine(obj_machinery fixable = null)
    {
        if (Dm.num(fixable.get_integrity()) >= Dm.num(fixable.max_integrity))
        {
            to_chat(this, span_warning("Diagnostics indicate that this machine is at peak integrity."));
            return;
        }
        if (repair_cooldown > world.time) // !COOLDOWN_FINISHED
        {
            balloon_alert(this, "recharging!");
            return;
        }
        Waiting.NoSleep(fixable.repair_damage(Dm.num(fixable.max_integrity) - Dm.num(fixable.get_integrity())));
        do_sparks(3, TRUE, fixable);
        to_chat(this, span_warning("Repairs complete!"));
        repair_cooldown = world.time + 50 * SECONDS;
    }

    // tg: code/modules/mob/living/basic/space_fauna/hivebot/_hivebot.dm:126
    public virtual async DmTask repair_hivebot(mob_living_basic bot_target = null)
    {
        if (Dm.num(bot_target.health) >= Dm.num(bot_target.maxHealth))
        {
            to_chat(this, span_warning("Diagnostics indicate that this unit is at peak integrity."));
            return;
        }
        if (repair_cooldown > world.time) // !COOLDOWN_FINISHED
        {
            balloon_alert(this, "recharging!");
            return;
        }
        await bot_target.revive(HEAL_ALL);
        do_sparks(3, TRUE, bot_target);
        to_chat(this, span_warning("Repairs complete!"));
        repair_cooldown = world.time + 50 * SECONDS;
    }
}
