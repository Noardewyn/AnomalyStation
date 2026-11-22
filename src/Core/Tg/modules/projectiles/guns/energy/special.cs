// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/special.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ion rifle (M7x slice B) and the ion carbine with its seclight mount, the cyborg's printer LMG (silicons SI9,
// D326), the plasma cutters (charged by plasma, a welding tool that spends charge, the butchering and the tool flash, the emp burn),
// the wormhole projector (left click blue, right click orange, its portals linked to each other once both exist), the gravity gun,
// the tesla cannon (its folding stock, the stock's sparks, its automatic fire with tg's looping sound), the marksman revolver (its
// coins flipped on the right button, regained over time) and the photon cannon's light (gaps GU1, D333). The floral somatoray, the
// meteor guns, the mind flayer and the temperature guns declare no procs. The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/energy/special.dm:1
public partial class obj_item_gun_energy_ionrifle : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/special.dm:19
    public override async DmTask add_seclight_point()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 17), Dm.named("overlay_y", 9)));
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }
}

// tg: code/modules/projectiles/guns/energy/special.dm:26
public partial class obj_item_gun_energy_ionrifle_carbine : obj_item_gun_energy_ionrifle
{
    // tg: code/modules/projectiles/guns/energy/special.dm:34
    public override async DmTask add_seclight_point()
    {
        await base.add_seclight_point();
        // We use the same overlay as the parent, so we can just let the component inherit the correct offsets here
        await AddComponent(typeof(datum_component_seclite_attachable), Dm.named("overlay_x", 18), Dm.named("overlay_y", 11));
    }
}

// tg: code/modules/projectiles/guns/energy/special.dm:87
public partial class obj_item_gun_energy_plasmacutter : obj_item_gun_energy
{
    /// amount of charge used up to start action (multiplied by amount) and per progress_flash_divisor ticks of welding
    // tg: code/modules/projectiles/guns/energy/special.dm:85  #define PLASMA_CUTTER_CHARGE_WELD (0.025 * STANDARD_CELL_CHARGE) (file-local)
    private const float PLASMA_CUTTER_CHARGE_WELD = 0.025f * STANDARD_CELL_CHARGE;

    // tg: code/modules/projectiles/guns/energy/special.dm:106
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 2.5f * SECONDS),
            Dm.named("effectiveness", 105),
            Dm.named("bonus_modifier", 0),
            Dm.named("butcher_sound", Dm.resource("sound/items/weapons/plasma_cutter.ogg"))));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_flash), 1));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:117
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (cell != null)
            Dm.append(ref dot, span_notice(I18n.T("[] is []% charged.", this, round(Dm.num(cell.percent())))));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:122
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        float charge_multiplier = 0;
        if (istype(tool, typeof(obj_item_stack_sheet_mineral_plasma)))
            charge_multiplier = 2;
        else if (istype(tool, typeof(obj_item_stack_ore_plasma)))
            charge_multiplier = 1;

        if (!Dm.truthy(charge_multiplier))
            return NONE;

        if (cell.charge == cell.maxcharge)
        {
            balloon_alert(user, I18n.T("already fully charged!"));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_stack sheet = tool as obj_item_stack;
        if (!Dm.truthy(await sheet.use(1)))
            return ITEM_INTERACT_BLOCKING;

        await cell.give(0.2f * STANDARD_CELL_CHARGE * charge_multiplier);
        balloon_alert(user, I18n.T("cell recharged"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:144
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;

        if (!isliving(loc))
            return dot;

        mob_living user = loc as mob_living;
        user.visible_message(span_danger(I18n.T("Concentrated plasma discharges from [] onto [], burning them!", this, user)), span_userdanger(I18n.T("[] malfunctions, spewing concentrated plasma onto you! It burns!", this)));
        await user.adjust_fire_stacks(4);
        user.ignite_mob();
        return dot;
    }

    // Can we weld? Plasma cutter does not use charge continuously.
    // Amount cannot be defaulted to 1: most of the code specifies 0 in the call.
    // tg: code/modules/projectiles/guns/energy/special.dm:159
    public override object tool_use_check(mob_living user = null, object amount = null, object heat_required = null)
    {
        if (QDELETED(cell))
        {
            balloon_alert(user, I18n.T("no cell inserted!"));
            return FALSE;
        }
        // Amount cannot be used if drain is made continuous, e.g. amount = 5, charge_weld = 25
        // Then it'll drain 125 at first and 25 periodically, but fail if charge dips below 125 even though it still can finish action
        // Alternately it'll need to drain amount*charge_weld every period, which is either obscene or makes it free for other uses
        if (Dm.truthy(amount) ? cell.charge < PLASMA_CUTTER_CHARGE_WELD * Dm.num(amount) : cell.charge < PLASMA_CUTTER_CHARGE_WELD)
        {
            balloon_alert(user, I18n.T("not enough charge!"));
            return FALSE;
        }
        if (Dm.lt(heat, heat_required))
        {
            to_chat(user, span_warning(I18n.T("[] is not hot enough to complete this task!", this)));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:175
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        if (QDELETED(cell))
            return FALSE; // !QDELETED(cell) && ...: the && stops at the false 0
        return await cell.use(Dm.truthy(used) ? Dm.num(used) * PLASMA_CUTTER_CHARGE_WELD : PLASMA_CUTTER_CHARGE_WELD);
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:178
    public override async DmTask<object> use_tool(atom target = null, mob_living user = null, object delay = null, float? amount = null, float? volume = null, datum_callback extra_checks = null, float? timed_action_flags = null)
    {
        amount ??= 1;
        volume ??= 0;
        object dot = null;
        if (!Dm.truthy(amount))
        {
            amount = 1;
            return await base.use_tool(target, user, delay, amount, volume, extra_checks, timed_action_flags);
        }

        object sparks = mutable_appearance(Dm.resource("icons/effects/welding_effect.dmi"), "welding_sparks", GASFIRE_LAYER, this, ABOVE_LIGHTING_PLANE);
        target.add_overlay(sparks);
        LAZYADD(ref update_overlays_on_z, sparks); // tg adds the sparks to the cutter's own list here (the welder adds them to the target's)
        dot = await base.use_tool(target, user, delay, amount, volume, extra_checks, timed_action_flags);
        LAZYREMOVE(ref update_overlays_on_z, sparks);
        target.cut_overlay(sparks);
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:190
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        object and__1 = await user.Adjacent(target);
        return await fire_gun(target, user, Dm.truthy(and__1) ? Dm.num(!isturf(target)) : and__1, @params);
    }
}

// tg: code/modules/projectiles/guns/energy/special.dm:206
public partial class obj_item_gun_energy_wormhole_projector : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/special.dm:203  #define AMMO_SELECT_BLUE 1 (file-local)
    private const float AMMO_SELECT_BLUE = 1;
    // tg: code/modules/projectiles/guns/energy/special.dm:204  #define AMMO_SELECT_ORANGE 2 (file-local)
    private const float AMMO_SELECT_ORANGE = 2;

    // tg: code/modules/projectiles/guns/energy/special.dm:222
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("<b>Left-click</b> to fire blue wormholes and <b><font color=orange>right-click</font></b> to fire orange wormholes.")));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:226
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_assembly_signaler_anomaly_bluespace)))
            return NONE;
        if (Dm.truthy(firing_core))
        {
            user.balloon_alert(user, I18n.T("already has a core!"));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You insert [] into the wormhole projector and the weapon gently hums to life.", tool)));
        firing_core = TRUE;
        playsound(loc, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:238
    public override object can_shoot()
    {
        if (!Dm.truthy(firing_core))
            return FALSE;
        return base.can_shoot();
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:243
    public override async DmTask shoot_with_empty_chamber(mob_living user = null)
    {
        await base.shoot_with_empty_chamber(user);
        to_chat(user, span_danger(I18n.T("The display says, 'NO CORE INSTALLED'.")));
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:247
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        inhand_icon_state = Dm.text("[][]", base_icon_state, select); // icon_state = inhand_icon_state = "[base_icon_state][select]"
        icon_state = (string)inhand_icon_state;
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:251
    public override void update_ammo_types()
    {
        base.update_ammo_types();
        for (float i = 1; i <= ammo_type.Count; i++)
        {
            obj_item_ammo_casing_energy_wormhole W = ammo_type[i] as obj_item_ammo_casing_energy_wormhole;
            if (W is obj_item_ammo_casing_energy_wormhole)
            {
                W.gun = WEAKREF(this) as datum_weakref;
                obj_projectile_beam_wormhole WH = W.loaded_projectile as obj_projectile_beam_wormhole;
                if (WH is obj_projectile_beam_wormhole)
                    WH.gun = WEAKREF(this) as datum_weakref;
            }
        }
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:261
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        if (Dm.truthy(LAZYACCESS(params2list(@params), RIGHT_CLICK)))
        {
            if (select == AMMO_SELECT_BLUE) //Last fired in left click mode. Switch to orange wormhole (right click).
                await select_fire();
        }
        else
        {
            if (select == AMMO_SELECT_ORANGE) //Last fired in right click mode. Switch to blue wormhole (left click).
                await select_fire();
        }
        return await base.try_fire_gun(target, user, @params);
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:270
    [SignalHandler]
    public virtual void on_portal_destroy(obj_effect_portal P = null)
    {
        if (P == p_blue)
            p_blue = null;
        else if (P == p_orange)
            p_orange = null;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:277
    public virtual object has_blue_portal()
    {
        if (p_blue is obj_effect_portal && !QDELETED(p_blue))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:282
    public virtual object has_orange_portal()
    {
        if (p_orange is obj_effect_portal && !QDELETED(p_orange))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:287
    public virtual void crosslink()
    {
        if (!Dm.truthy(has_blue_portal()) && !Dm.truthy(has_orange_portal()))
            return;
        if (!Dm.truthy(has_blue_portal()) && Dm.truthy(has_orange_portal()))
        {
            p_orange.link_portal(null);
            return;
        }
        if (!Dm.truthy(has_orange_portal()) && Dm.truthy(has_blue_portal()))
        {
            p_blue.link_portal(null);
            return;
        }
        p_orange.link_portal(p_blue);
        p_blue.link_portal(p_orange);
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:299
    public virtual void create_portal(obj_projectile_beam_wormhole wormhole_beam = null, turf target = null)
    {
        obj_effect_portal new_portal = Dm.@new<obj_effect_portal>(target, 300, null, FALSE, null);
        RegisterSignal(new_portal, COMSIG_QDELETING, nameof(on_portal_destroy));
        if (istype(wormhole_beam, typeof(obj_projectile_beam_wormhole_orange)))
        {
            qdel(p_orange);
            p_orange = new_portal;
            new_portal.icon_state = "portal1";
            new_portal.set_light_color(COLOR_MOSTLY_PURE_ORANGE);
            new_portal.update_light();
        }
        else
        {
            qdel(p_blue);
            p_blue = new_portal;
        }
        crosslink();
        playsound(new_portal, SFX_PORTAL_CREATED, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
    }
}

/* 3d printer 'pseudo guns' for borgs */

// tg: code/modules/projectiles/guns/energy/special.dm:322  (silicons SI9, D326: the syndicate assault cyborg's LMG)
public partial class obj_item_gun_energy_printer : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/special.dm:332
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), 0.3f * SECONDS));
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }
}

// tg: code/modules/projectiles/guns/energy/special.dm:359
public partial class obj_item_gun_energy_gravity_gun : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/special.dm:379
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_assembly_signaler_anomaly_grav)))
            return NONE;
        if (Dm.truthy(firing_core))
        {
            user.balloon_alert(user, I18n.T("already has a core!"));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You insert [] into the gravitational manipulator and the weapon gently hums to life.", tool)));
        firing_core = TRUE;
        playsound(loc, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:391
    public override object can_shoot()
    {
        if (!Dm.truthy(firing_core))
            return FALSE;
        return base.can_shoot();
    }
}

/**
-----------------Tesla Cannon--------------------------------

An advanced weapon that provides extremely high dps output at pinpoint accuracy due to its hitscan nature.

Due to its normal w_class when folded it is suitable as a heavy reinforcement weapon, since the cell drains very quickly when firing.

The power level is somewhat tempered by several drawbacks such as research requirements, anomalock, two handed firing requirement, and insultation providing damage reduction.

it is often confused with the mech weapon of the same name, since it is a bit more obscure despite being very powerful. Formerly called the tesla revolver.
**/
// tg: code/modules/projectiles/guns/energy/special.dm:407
public partial class obj_item_gun_energy_tesla_cannon : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/special.dm:427
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), Dm.named("autofire_shot_delay", 100 * MILLISECONDS), Dm.named("firing_sound_loop", typeof(datum_looping_sound_tesla_cannon))));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:431
    public override async DmTask<object> can_trigger_gun(mob_living user = null, object akimbo_usage = null)
    {
        if (Dm.truthy(ready_to_fire))
            return await base.can_trigger_gun(user, akimbo_usage);
        //If we have charge, but the stock is folded, do sparks.
        if (Dm.truthy(can_shoot()))
        {
            balloon_alert(user, I18n.T("electricity arcing to stock!"));

            if (prob(75)) //fake sparks to cut on spark spam
                playsound(user, Dm.resource("sound/effects/sparks/sparks1.ogg"), 50, TRUE);
            else
                do_sparks(3, FALSE, user);
        }
        return FALSE;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:444
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(ready_to_fire))
        {
            w_class = WEIGHT_CLASS_NORMAL;
            ready_to_fire = FALSE;
            icon_state = "tesla";
            playsound(user, Dm.resource("sound/items/weapons/gun/tesla/squeak_latch.ogg"), 100);
        }
        else
        {
            playsound(user, Dm.resource("sound/items/weapons/gun/tesla/click_creak.ogg"), 100);
            if (!Dm.truthy(await do_after(user, 1.5f * SECONDS)))
                return dot;
            w_class = WEIGHT_CLASS_BULKY;
            ready_to_fire = TRUE;
            icon_state = "tesla_unfolded";
            playsound(user, Dm.resource("sound/items/weapons/gun/tesla/squeak_latch.ogg"), 100);
        }

        update_appearance();
        balloon_alert_to_viewers(I18n.T("[] stock", Dm.truthy(ready_to_fire) ? I18n.T("unfolded") : I18n.T("folded")));
        return dot;
    }
}

// tg: code/modules/projectiles/guns/energy/special.dm:464
public partial class obj_item_gun_energy_marksman_revolver : obj_item_gun_energy
{
    // tg: code/__HELPERS/view.dm:38  #define DEFAULT_SIGHT_DISTANCE 7 (CAN_THEY_SEE reads it; as fullauto.cs)
    private const int DEFAULT_SIGHT_DISTANCE = 7;

    // tg: code/modules/projectiles/guns/energy/special.dm:481
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        coin_count = max_coins;
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:485
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(max_coins))
            Dm.append(ref dot, I18n.T("It currently has [] out of [] coins, and takes [] seconds to recharge each one.", coin_count, max_coins, coin_regen_rate / 10));
        else
            Dm.append(ref dot, I18n.T("It has infinite coins available for use."));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:492
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(max_coins) || coin_count >= max_coins)
        {
            STOP_PROCESSING(SSobj, this);
            return null;
        }

        if (coin_regen_cd <= world.time) // COOLDOWN_FINISHED(src, coin_regen_cd), inline (rules 11.1 (a))
        {
            if (ismob(loc))
            {
                mob owner = loc as mob;
                owner.playsound_local(get_turf(owner), Dm.resource("sound/machines/ding.ogg"), 20); // the mob's turf: playsound_local's turf_source (as cigarettes.cs)
            }
            coin_count++;
            coin_regen_cd = world.time + coin_regen_rate; // COOLDOWN_START(src, coin_regen_cd, coin_regen_rate), inline (rules 11.1 (a))
        }
        return null;
    }

    // tg: code/modules/projectiles/guns/energy/special.dm:504
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        if (!Dm.truthy(LAZYACCESS(params2list(@params), RIGHT_CLICK)))
            return await base.try_fire_gun(target, user, @params);
        if (!(Dm.@in(user, viewers(DEFAULT_SIGHT_DISTANCE, target)) || in_range(target, user))) // CAN_THEY_SEE(target, user) (__HELPERS/view.dm:45), inline
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(max_coins) && coin_count <= 0)
        {
            to_chat(user, span_warning(I18n.T("You don't have any coins right now!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(max_coins))
        {
            START_PROCESSING(SSobj, this);
            coin_count = max(0, coin_count - 1);
        }

        float dx__1 = rand(-1, 1), dy__1 = rand(-1, 1); // the two offsets in tg's argument order
        turf target_turf = get_offset_target_turf(target, dx__1, dy__1) as turf; // choose a random tile adjacent to the clicked one
        playsound(user.loc, Dm.resource("sound/effects/coin2.ogg"), 50, TRUE);
        user.visible_message(span_warning(I18n.T("[] flips a coin towards []!", user, target)), span_danger(I18n.T("You flip a coin towards []!", target)));
        obj_projectile_bullet_coin new_coin = Dm.@new<obj_projectile_bullet_coin>(get_turf(user), target_turf, user);
        await new_coin.aim_projectile(target_turf, user);
        await new_coin.fire();
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/projectiles/guns/energy/special.dm:526
public partial class obj_item_gun_energy_photon : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/special.dm:541
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_light_on(TRUE)); // The gun quite literally shoots mini-suns.
        return dot;
    }
}
