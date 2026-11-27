// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the megafauna's trophies; the elites' broodmother tongue and legionnaire spine come with the elites. The var blocks are
// generated.

/*!
 * Contains crusher trophies you can obtain from megafauna
 */

//blood-drunk hunter
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:6
public partial class obj_item_crusher_trophy_miner_eye : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:13
    public override string effect_desc()
    {
        return I18n.T("mark detonation to grant stun immunity and <b>90%</b> damage reduction for <b>1</b> second");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:16
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        await user.apply_status_effect(typeof(datum_status_effect_blooddrunk));
        return dot;
    }
}

//ash drake
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:21
public partial class obj_item_crusher_trophy_tail_spike : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:27
    public override string effect_desc()
    {
        return I18n.T("mark detonation to do <b>[]</b> damage to nearby creatures and push them back", bonus_value);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:30
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        foreach (mob_living living_target in Dm.each<mob_living>(oview(2, user)))
        {
            if (Dm.truthy(user.faction_check_atom(living_target)) || living_target.stat == DEAD)
                continue;
            playsound(living_target, Dm.resource("sound/effects/magic/fireball.ogg"), 20, TRUE);
            Dm.@new<obj_effect_temp_visual_fire>(living_target.loc);
            _addtimer(Dm.@new<datum_callback>(this, nameof(pushback), living_target, user), 1, file: "code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm", line: 37); //no free backstabs, we push AFTER module stuff is done
            await living_target.adjust_fire_loss(bonus_value, forced: TRUE);
        }
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:40
    public virtual async DmTask pushback(mob_living target = null, mob_living user = null)
    {
        if (!QDELETED(target) && !QDELETED(user) && (!Dm.truthy(target.anchored) || ismegafauna(target)) && target.move_resist < INFINITY) //megafauna will always be pushed
            await step(target, get_dir(user, target));
    }
}

//bubblegum
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:45
public partial class obj_item_crusher_trophy_demon_claws : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:53  var/static/list/damage_heal_order
    public static DmList damage_heal_order => TgState.Current.obj_item_crusher_trophy_demon_claws__damage_heal_order ??= Dm.list(BRUTE, BURN, OXY);

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:55
    public override string effect_desc()
    {
        return I18n.T("melee hits to do <b>[]</b> more damage and heal you for <b>[]</b>, with <b>5X</b> effect on mark detonation", bonus_value * 0.2f, bonus_value * 0.1f);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:58
    public override async DmTask<object> add_to(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        object dot = await base.add_to(crusher, user);
        if (!Dm.truthy(dot))
            return dot;
        crusher.force_wielded = Dm.num(crusher.force_wielded) + bonus_value * 0.2f;
        crusher.detonation_damage += bonus_value * 0.8f;
        crusher.update_wielding();
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:66
    public override async DmTask<object> remove_from(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        object dot = await base.remove_from(crusher, user);
        if (!Dm.truthy(dot))
            return dot;
        crusher.force_wielded = Dm.num(crusher.force_wielded) - bonus_value * 0.2f;
        crusher.detonation_damage -= bonus_value * 0.8f;
        crusher.update_wielding();
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:74
    public override async DmTask on_melee_hit(mob_living target = null, mob_living user = null)
    {
        await user.heal_ordered_damage(bonus_value * 0.1f, damage_heal_order);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:77
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        await user.heal_ordered_damage(bonus_value * 0.4f, damage_heal_order);
        return dot;
    }
}

// Colossus, allows user to fire shots during click CD
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:82
public partial class obj_item_crusher_trophy_blaster_tubes : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:91
    public override string effect_desc()
    {
        return I18n.T("destabilizer shots to be fire-able freely inbetween swings");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:94
    public override async DmTask<object> add_to(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        object dot = await base.add_to(crusher, user);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(crusher, COMSIG_ITEM_EQUIPPED, nameof(on_crusher_equipped));
        RegisterSignal(crusher, COMSIG_ITEM_DROPPED, nameof(on_crusher_dropped));
        if (Dm.eq(user.get_slot_by_item(crusher), ITEM_SLOT_HANDS))
            RegisterSignal(user, COMSIG_MOB_CLICKON, nameof(try_crusher_fire));
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:103
    public override async DmTask<object> remove_from(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        object dot = await base.remove_from(crusher, user);
        UnregisterSignal(crusher, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
        if (ismob(crusher.loc))
            UnregisterSignal(crusher.loc, COMSIG_MOB_CLICKON);
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:109
    [SignalHandler]
    public virtual void on_crusher_equipped(obj_item_kinetic_crusher source = null, mob user = null, object slot = null)
    {
        if (Dm.eq(slot, ITEM_SLOT_HANDS) && isliving(user))
            RegisterSignal(user, COMSIG_MOB_CLICKON, nameof(try_crusher_fire));
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:115
    [SignalHandler]
    public virtual void on_crusher_dropped(obj_item_kinetic_crusher source = null, mob user = null)
    {
        UnregisterSignal(user, COMSIG_MOB_CLICKON);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:119
    [SignalHandler]
    public virtual void try_crusher_fire(mob_living user = null, atom target = null, DmList modifiers = null)
    {
        // Only trigger on normal RMB when on cooldown
        if (Dm.num(user.next_move) < world.time || !Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) || Dm.truthy(LAZYACCESS(modifiers, SHIFT_CLICK)) || Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)) || Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK)))
            return;

        if (Dm.truthy(INCAPACITATED_IGNORING(user, INCAPABLE_STASIS)) || HAS_TRAIT(user, TRAIT_HANDS_BLOCKED) || Dm.truthy(user.throw_mode) || (!Dm.truthy(LAZYACCESS(modifiers, "catcher")) && Dm.truthy(target.IsObscured())))
            return;

        obj_item_kinetic_crusher crusher = loc as obj_item_kinetic_crusher;
        if (crusher is obj_item_kinetic_crusher && user.get_active_held_item() == crusher) // istype() just in case, crusher handles wielding/CD in its secondary interact on its own
            Waiting.NoSleep(crusher.interact_with_atom_secondary(target, user, modifiers)); // Standard shot recharge delay is 1.5 seconds (swing + shot CDs would be 1.6) so its fine to not have a penalty
    }
}

//hierophant
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:133
public partial class obj_item_crusher_trophy_vortex_talisman : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:140
    public override string effect_desc()
    {
        return I18n.T("mark detonation to create a homing hierophant chaser");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:143
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        if (isliving(target))
        {
            obj_effect_temp_visual_hierophant_chaser chaser = Dm.@new<obj_effect_temp_visual_hierophant_chaser>(get_turf(user), user, target, 3, TRUE);
            chaser.monster_damage_boost = FALSE; // Weaker cuz no cooldown
            chaser.damage = 20;
            log_combat(user, target, "fired a chaser at", this);
        }
        return dot;
    }
}

// Demonic frost miner
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:153
public partial class obj_item_crusher_trophy_ice_block_talisman : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:160
    public override string effect_desc()
    {
        return I18n.T("mark detonation to freeze a creature in a block of ice for a period, preventing them from moving");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:163
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        await target.apply_status_effect(typeof(datum_status_effect_ice_block_talisman));
        return dot;
    }
}

// Wendigo
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:168
public partial class obj_item_crusher_trophy_wendigo_horn : obj_item_crusher_trophy
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:174
    public override string effect_desc()
    {
        return I18n.T("melee hits to inflict twice as much damage");
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:177
    public override async DmTask<object> add_to(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        object dot = await base.add_to(crusher, user);
        if (!Dm.truthy(dot))
            return dot;
        crusher.force_wielded = Dm.num(crusher.force_wielded) + 20;
        crusher.update_wielding();
        return dot;
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:184
    public override async DmTask<object> remove_from(obj_item_kinetic_crusher crusher = null, mob_living user = null)
    {
        object dot = await base.remove_from(crusher, user);
        if (!Dm.truthy(dot))
            return dot;
        crusher.force_wielded = Dm.num(crusher.force_wielded) - 20;
        crusher.update_wielding();
        return dot;
    }
}

//The Thing
// tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:265
public partial class obj_item_crusher_trophy_flesh_glob : obj_item_crusher_trophy
{
    /// the order in which we heal damage
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:273  var/static/list/damage_heal_order
    public static DmList damage_heal_order => TgState.Current.obj_item_crusher_trophy_flesh_glob__damage_heal_order ??= Dm.list(BRUTE, BURN, OXY, TOX);

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:275
    public override string effect_desc()
    {
        return I18n.T("melee hits heal you for <b>[]</b>, and for <b>[]</b> on mark detonation", bonus_value * 0.2f, bonus_value * 0.5f);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:278
    public override async DmTask on_melee_hit(mob_living target = null, mob_living user = null)
    {
        await user.heal_ordered_damage(bonus_value * 0.2f, damage_heal_order);
    }

    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:281
    public override async DmTask<object> on_mark_detonation(mob_living target = null, mob_living user = null, obj_item_kinetic_crusher pkc = null)
    {
        object dot = await base.on_mark_detonation(target, user, pkc);
        await user.heal_ordered_damage(bonus_value * 0.5f, damage_heal_order);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/equipment/kinetic_crusher/trophies_megafauna.dm:53, :273 (var/static, rules 10.6)
    internal DmList obj_item_crusher_trophy_demon_claws__damage_heal_order;
    internal DmList obj_item_crusher_trophy_flesh_glob__damage_heal_order;
}
