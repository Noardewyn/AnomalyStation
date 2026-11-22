// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/mulebot/mulebot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the ridable element (riding, section 109: a mob on the platform is buckled without the rider's offset) (gaps package C3).

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:1
public partial class mob_living_basic_bot_mulebot : mob_living_basic_bot
{
    // (a class with a hand-declared var keeps its constructor by hand, types-converter.md)
    public object id; // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:42 (hand-declared: text)
    public obj_item_stock_parts_power_store cell; // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:53 (hand-declared: an instance)

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:2
    public mob_living_basic_bot_mulebot()
    {
        name = Dm.text("\\improper MULEbot");
        desc = "A Multiple Utility Load Effector bot.";
        icon = Dm.resource("icons/mob/silicon/aibots.dmi");
        icon_state = "mulebot0";
        base_icon_state = "mulebot";
        light_color = "#ffcc99";
        light_power = 0.8f;
        health = 50;
        maxHealth = 50;
        density = 1;
        mob_size = 3;
        move_resist = 2000;
        animate_movement = 2;
        speed = 3;
        combat_mode = 1;
        buckle_lying = 0;
        buckle_prevents_pull = 1;
        bot_mode_flags = 16777199;
        radio_key = typeof(obj_item_encryptionkey_headset_cargo);
        radio_channel = "Supply";
        pass_flags = 512;
        bot_type = "MULEbot";
        additional_access = typeof(datum_id_trim_job_cargo_technician);
        path_image_color = "#7F5200";
        hud_type = typeof(datum_hud_living_mulebot);
        hackables = "obstacle detection circuits";
        possessed_message = "You are a MULEbot! Do your best to make sure that packages get to their destination!";
        ai_controller = typeof(datum_ai_controller_basic_controller_bot_mulebot);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:14
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        physiology = Dm.list(Dm.kv("brute", 0.5f), Dm.kv("burn", 0.7f), Dm.kv("toxin", 0), Dm.kv("oxygen", 0), Dm.kv("stamina", 0)); // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:14
        req_one_access = Dm.list("robotics", "cargo"); // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:27
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:64
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (prob(replacement_chance) && Dm.truthy(mapload))
        {
            Dm.@new<mob_living_basic_bot_mulebot_paranormal>(loc);
            return INITIALIZE_HINT_QDEL;
        }

        set_wires(Dm.@new<datum_wires_mulebot>(this));
        obj_item_stock_parts_power_store_cell_upgraded new_cell = Dm.@new<obj_item_stock_parts_power_store_cell_upgraded>(this);
        assign_cell(new_cell);
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        controller.set_blackboard_key(BB_MULEBOT_HOME_BEACON, "");
        Dm.Deferred("code/modules/mob/living/basic/bots/mulebot/mulebot.dm:74", "Initialize: AddElement(ridable, riding/creature/mulebot): the rider's offset on the platform (datums/components/riding, section 109)");
        ADD_TRAIT(this, TRAIT_NOMOBSWAP, INNATE_TRAIT);
        add_traits(Dm.list(TRAIT_NOMOBSWAP, TRAIT_COMBAT_MODE_LOCK), INNATE_TRAIT);
        RegisterSignal(this, COMSIG_MOVABLE_PRE_MOVE, nameof(on_pre_move));

        set_id(Dm.truthy(suffix) ? suffix : assign_random_name());
        suffix = null;
        if (Dm.eq(name, Dm.text("\\improper MULEbot")))
            name = Dm.text("\\improper MULEbot []", id);
        set_home(get_turf(this));
        controller.update_able_to_run();
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:91
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, COMSIG_MOVABLE_PRE_MOVE);
        Waiting.NoSleep(unload());
        qdel(cell);
        cell = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:97
    public virtual void assign_cell(atom new_cell = null)
    {
        cell = new_cell as obj_item_stock_parts_power_store;
        atom_movable_screen_mob_charge charge_hud = hud_used?.screen_objects?[HUD_MULEBOT_CHARGE] as atom_movable_screen_mob_charge;
        charge_hud?.update_battery_overlay(new_cell);
        charge_hud?.calculate_charge();
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:103
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)) && !HAS_AI_ACCESS(user))
        {
            await (wires as datum_wires).interact(user);
            return null;
        }
        if (Dm.truthy((wires as datum_wires).is_cut(WIRE_RX)) && HAS_AI_ACCESS(user))
            return null;

        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:112
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)))
        {
            if (cell != null)
            {
                Dm.append(ref dot, span_notice(Dm.text("It has \\a [] installed.", cell)));
                Dm.append(ref dot, span_info("You can use a <b>crowbar</b> to remove it."));
            }
            else
                Dm.append(ref dot, span_notice("It has an empty compartment where a <b>power cell</b> can be installed."));
        }
        if (load != null) //observer check is so we don't show the name of the ghost that's sitting on it to prevent metagaming who's ded.
            Dm.append(ref dot, span_notice(Dm.text("\\A [] is on its load platform.", isobserver(load) ? "ghostly figure" : load)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:123
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:126
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        if (!Dm.truthy(can_unarmed_attack()))
            return null;
        if (isturf(target) && isturf(loc) && Dm.truthy(await loc.Adjacent(target)) && load != null)
            await unload(get_dir(loc, target));
        else
            return await base.melee_attack(target, modifiers, ignore_cooldown);
        return null;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:134
    public override object turn_on(mob user = null)
    {
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)))
        {
            if (user != null)
                to_chat(user, span_warning(Dm.text("[]'s maintenance panel is open!", this)));
            return FALSE;
        }
        if (!Dm.truthy(has_power()))
        {
            if (user != null)
                to_chat(user, span_warning(Dm.text("[] has no power!", this)));
            return FALSE;
        }
        return base.turn_on(user);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:145
    public override async DmTask<object> update_icon_state(params object[] __extra) //if you change the icon_state names, please make sure to update /datum/wires/mulebot/on_pulse() as well. <3
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(Dm.bitand(bot_mode_flags, BOT_MODE_ON)) ? (wires as datum_wires)?.is_cut(WIRE_AVOIDANCE) : "0");
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:149
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)))
            Dm.append(ref dot, Dm.text("[]-hatch", base_icon_state));
        if (load == null || ismob(load)) //mob offsets and such are handled by the riding component / buckling
            return dot;
        mutable_appearance load_overlay = mutable_appearance(load.icon, load.icon_state, layer + 0.01f) as mutable_appearance;
        load_overlay.pixel_y = Dm.initial(load, (atom p) => p.pixel_y) + 11;
        Dm.append(ref dot, load_overlay);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:158
    [SignalHandler]
    public virtual void handle_buzzing(datum_move_loop_has_target_jps_frustrations source = null, object frustration_counter = null)
    {
        update_bot_mode(new_mode: BOT_BLOCKED);
        object buzz_mode = Dm.num(frustration_counter) >= Dm.num(source.maximum_frustrations) ? MULEBOT_MOOD_ANNOYED : MULEBOT_MOOD_SIGH;
        buzz(buzz_mode);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:164
    public override void handle_loop_movement(atom_movable source = null, atom oldloc = null, object dir = null, object forced = null) //incase we start moving again after being previously blocked, update our mode
    {
        base.handle_loop_movement(source, oldloc, dir, forced);
        if (!Dm.eq(mode, BOT_BLOCKED))
            return;
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        if (controller.blackboard[BB_CURRENT_MOVEMENT_TARGET] is not obj_machinery_navbeacon beacon)
            return;
        object intended_mode = Dm.eq(beacon.location, controller.blackboard[BB_MULEBOT_HOME_BEACON]) ? BOT_GO_HOME : BOT_DELIVER;
        update_bot_mode(new_mode: intended_mode);
    }

    ///Noises that mulebots make
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:174
    public virtual void buzz(object type = null)
    {
        switch (type as string)
        {
            case MULEBOT_MOOD_SIGH:
                audible_message(span_hear(Dm.text("[] makes a sighing buzz.", this)));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
                break;
            case MULEBOT_MOOD_ANNOYED:
                audible_message(span_hear(Dm.text("[] makes an annoyed buzzing sound.", this)));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, FALSE);
                break;
            case MULEBOT_MOOD_DELIGHT:
                audible_message(span_hear(Dm.text("[] makes a delighted ping!", this)));
                playsound(this, Dm.resource("sound/machines/ping.ogg"), 50, FALSE);
                break;
            case MULEBOT_MOOD_CHIME:
                audible_message(span_hear(Dm.text("[] makes a chiming sound!", this)));
                playsound(this, Dm.resource("sound/machines/chime.ogg"), 50, FALSE);
                break;
        }
        flick(Dm.text("[]1", base_icon_state), this);
    }

    /// returns true if the bot is fully powered.
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:191
    public virtual object has_power()
    {
        datum_wires our_wires = wires as datum_wires;
        return Dm.num(cell != null && Dm.num(cell.charge) > 0 && !Dm.truthy(our_wires.is_cut(WIRE_POWER1)) && !Dm.truthy(our_wires.is_cut(WIRE_POWER2)));
    }
}
