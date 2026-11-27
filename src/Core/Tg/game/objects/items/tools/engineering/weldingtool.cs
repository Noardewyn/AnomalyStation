// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/engineering/weldingtool.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the base welder (M4a P4a): its fuel, lighting, burning, the tool checks and the appearance, and the
// subtypes' procs (the experimental and the abductor's welder refuel in process). The procs of a later
// milestone are stubs: the declared roots below (healing robotic limbs M6) and the rows of
// docs/ai/specs/live-partial.txt "## P4 (M4a)" for the overrides (suicide_act M10,
// cyborg_unequip since silicons SI2, interact_with_atom M6, afterattack M4b; ignition_effect is translated, M7 lighting slice L1). The var block is
// generated (D111). M5 slice 10: the flamethrower assembly (the screwdriver that opens the fuel tank, the rods that start a
// flamethrower; the industrial and emergency welders refuse the screwdriver).

// tg: code/game/objects/items/tools/engineering/weldingtool.dm:1
public partial class obj_item_weldingtool : obj_item
{
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (light_system == OVERLAY_LIGHT || light_system == OVERLAY_LIGHT_DIRECTIONAL || light_system == OVERLAY_LIGHT_BEAM) // IS_OVERLAY_LIGHT_SYSTEM (__DEFINES/lighting.dm), inline
        {
            // middleman = new(src, "weldingtool"); RegisterSignal(middleman, COMSIG_LIGHT_MIDDLEMAN_UPDATED,
            // PROC_REF(light_updated)); middleman.being_overriding_light(): the light's flicker, the lighting module
            // (D24, rules 13: the client computes light from the replicated light_on, range, power and colour).
            Dm.Deferred("code/game/objects/items/tools/engineering/weldingtool.dm:61", "Initialize: the light middleman's flicker (the lighting module, D24)");
        }
        // AddElement(/datum/element/update_icon_updates_onmob): redrawing the holder's hand when the icon changes; the
        // held item's inhand_icon_state replicates with the item (M4a P2's hands), so nothing reads the element.
        Dm.Deferred("code/game/objects/items/tools/engineering/weldingtool.dm:64", "Initialize: AddElement(/datum/element/update_icon_updates_onmob), the holder's overlays (presentation, M10+)");
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_flash), light_range));
        Dm.Deferred("code/game/objects/items/tools/engineering/weldingtool.dm:66", "Initialize: AddElement(/datum/element/falling_hazard, ...), the welder falling on a head (falls, M5)");

        create_reagents(max_fuel);
        if (Dm.truthy(starting_fuel))
            Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_fuel), max_fuel));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:73
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref middleman);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:77
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(welding))
            inhand_icon_state = Dm.text("[]1", Dm.initial(this, (obj_item_weldingtool p) => p.inhand_icon_state));
        else
            inhand_icon_state = Dm.text("[]", Dm.initial(this, (obj_item_weldingtool p) => p.inhand_icon_state));
        return await base.update_icon_state(__extra);
    }


    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:85
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(change_icons))
        {
            object ratio = Dm.div(get_fuel(), max_fuel);
            ratio = ceil(Dm.num(ratio) * 4) * 25;
            Dm.append(ref dot, Dm.text("[][]", Dm.initial(this, (obj_item_weldingtool p) => p.icon_state), ratio));
        }
        if (Dm.truthy(welding))
            Dm.append(ref dot, Dm.text("[]-on", Dm.initial(this, (obj_item_weldingtool p) => p.icon_state)));
        return dot;
    }


    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:95
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(welding))
        {
            force = 15;
            damtype = BURN;
            burned_fuel_for += Dm.num(seconds_per_tick);
            if (burned_fuel_for >= TOOL_FUEL_BURN_INTERVAL)
                Waiting.NoSleep(use(TRUE));
            update_appearance();
        }

        //Welders left on now use up fuel, but lets not have them run out quite that fast
        else
        {
            force = 3;
            damtype = BRUTE;
            update_appearance();
            if (!Dm.truthy(can_off_process))
                STOP_PROCESSING(SSobj, this);
            return null;
        }

        //This is to start fires. process() is only called if the welder is on.
        // GAP(lead): /obj/item/proc/open_flame (items.dm:959, M4a P2's file) is a skeleton (Dm.NotTranslated); its body,
        // inline with the default flame_heat = 700 and mob_slots = ITEM_SLOT_MASK|ITEM_SLOT_HANDS, until it is translated
        // there (request in p4a.json); then this block is the call open_flame().
        {
            atom location = loc;
            if (ismob(location))
            {
                mob pyromanic = location as mob;
                if (Dm.truthy(Dm.bitand(pyromanic.get_slot_by_item(this), Dm.bitor(ITEM_SLOT_MASK, ITEM_SLOT_HANDS))))
                    location = get_turf(pyromanic);
            }
            if (isturf(location))
                (location as turf).hotspot_expose(700, 5);
        }
        return null;
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:117  suicide_act: live-partial (M10).
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:119
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await flamethrower_screwdriver(tool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:123
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stack_rods)))
            return NONE;
        await flamethrower_rods(tool, user);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:132
    public override async DmTask cyborg_unequip(mob user = null)
    {
        if (!Dm.truthy(isOn()))
            return;
        switched_on(user);
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:137
    public override async DmTask<object> use_tool(atom target = null, mob_living user = null, object delay = null, float? amount = null, float? volume = null, datum_callback extra_checks = null, float? timed_action_flags = null)
    {
        object dot = null;
        object sparks = mutable_appearance(Dm.resource("icons/effects/welding_effect.dmi"), "welding_sparks", GASFIRE_LAYER, this, ABOVE_LIGHTING_PLANE);
        target.add_overlay(sparks);
        LAZYADD(ref target.update_overlays_on_z, sparks);
        dot = await base.use_tool(target, user, delay, amount, volume, extra_checks, timed_action_flags);
        LAZYREMOVE(ref target.update_overlays_on_z, sparks);
        target.cut_overlay(sparks);
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:145  interact_with_atom: live-partial (emptying the
    // tank M8, repairing robotic limbs M6).

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:158
    [SignalHandler]
    public virtual void light_updated(datum source = null)
    {
        // fire_flicker_middleman(middleman): the light's flicker, the lighting module (D24); no middleman is made (Initialize).
        Dm.Deferred("code/game/objects/items/tools/engineering/weldingtool.dm:160", "light_updated: fire_flicker_middleman (the lighting module, D24)");
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:145  (M5 slice 4: a welder that does not slice a wall or a
    // girder reaches it; the robotic limb repair stays in try_heal_loop, M6)
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(status) && Dm.truthy(interacting_with.is_refillable()))
        {
            await (reagents as datum_reagents).trans_to(interacting_with, Dm.num((reagents as datum_reagents).total_volume), transferred_by: user);
            to_chat(user, span_notice(I18n.T("You empty []'s fuel tank into [].", this, interacting_with)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        if (!ishuman(interacting_with))
            return NONE;
        if (Dm.truthy(user.combat_mode))
            return NONE;

        return await try_heal_loop(interacting_with, user);
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:162
    public virtual async DmTask<object> try_heal_loop(atom interacting_with = null, mob_living user = null, float? repeating = null)
    {
        Dm.NotTranslated("code/game/objects/items/tools/engineering/weldingtool.dm:162", "try_heal_loop: repairing robotic limbs (robotic bodyparts and item_heal, M6)");
        return default;
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:187
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!Dm.truthy(isOn()))
            return;
        await use(1);
        turf location = get_turf(user);
        location.hotspot_expose(700, 50, 1);
        if (QDELETED(target) || !isliving(target)) // can't ignite something that doesn't exist
            return;
        mob_living attacked_mob = (mob_living)target;
        if (Dm.truthy(attacked_mob.ignite_mob()))
        {
            message_admins(Dm.text("[] set [] on fire with [] at []", ADMIN_LOOKUPFLW(user), key_name_admin(attacked_mob), this, AREACOORD(user)));
            user.log_message(Dm.text("set [] on fire with [].", key_name(attacked_mob), this), LOG_ATTACK);
        }
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:200
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        // if(reagents.spark_act(0, SPARK_ACT_ENCLOSED, banned_reagents = /datum/reagent/fuel) & SPARK_ACT_DESTRUCTIVE):
        // a rigged welder (a reagent other than fuel in its tank) blowing up. Only refuelling and the open tank (the
        // flamethrower screwdriver, M5; transfers, M8) put another reagent in, so spark_act always returns NONE in M4a.
        Dm.Deferred("code/game/objects/items/tools/engineering/weldingtool.dm:201", "attack_self: reagents.spark_act, the rigged welder (reagent transfers, M8)");

        switched_on(user);
        update_appearance();
        return null;
    }

    /// Returns the amount of fuel in the welder
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:211
    public virtual object get_fuel()
    {
        return Dm.add((reagents as datum_reagents).get_reagent_amount(typeof(datum_reagent_fuel)), (reagents as datum_reagents).get_reagent_amount(typeof(datum_reagent_toxin_plasma)));
    }

    /// Uses fuel from the welding tool.
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:215
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        used ??= 0;
        if (!Dm.truthy(isOn()) || !Dm.truthy(check_fuel()))
            return FALSE;

        if (Dm.gt(used, 0))
            burned_fuel_for = 0;

        if (Dm.ge(get_fuel(), used))
        {
            await (reagents as datum_reagents).remove_reagent(typeof(datum_reagent_fuel), used);
            check_fuel();
            return TRUE;
        }
        else
            return FALSE;
    }


    /// Toggles the welding value.
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:231
    public virtual object set_welding(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(welding, new_value))
            return dot;
        dot = welding;
        welding = Dm.num(new_value);
        Waiting.NoSleep(set_light_on(welding));
        return dot;
    }


    /// Turns off the welder if there is no more fuel (does this really need to be its own proc?)
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:240
    public virtual object check_fuel(mob user = null)
    {
        if (Dm.le(get_fuel(), 0) && Dm.truthy(welding))
        {
            Waiting.NoSleep(set_light_on(FALSE));
            switched_on(user);
            update_appearance();
            return FALSE;
        }
        return TRUE;
    }

    // /Switches the welder on
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:249
    public virtual void switched_on(mob user = null)
    {
        if (!Dm.truthy(status))
        {
            balloon_alert(user, I18n.T("unsecured!"));
            return;
        }
        set_welding(Dm.not(welding));
        if (Dm.truthy(welding))
        {
            if (Dm.ge(get_fuel(), 1))
            {
                playsound(loc, activation_sound, 50, TRUE); // tg: code/game/objects/items/tools/engineering/weldingtool.dm:256
                force = 15;
                damtype = BURN;
                hitsound = Dm.resource("sound/items/tools/welder.ogg");
                update_appearance();
                START_PROCESSING(SSobj, this);
            }
            else
            {
                balloon_alert(user, I18n.T("no fuel!"));
                switched_off();
            }
        }
        else
        {
            playsound(loc, deactivation_sound, 50, TRUE); // tg: code/game/objects/items/tools/engineering/weldingtool.dm:266
            switched_off();
        }
    }

    /// Switches the welder off
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:270
    public virtual void switched_off()
    {
        set_welding(FALSE);

        force = 3;
        damtype = BRUTE;
        hitsound = SFX_SWING_HIT;
        update_appearance();
    }


    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:279
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("It contains [] unit\\s of fuel out of [].", get_fuel(), max_fuel));
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:283
    public override object get_temperature()
    {
        return welding * heat;
    }

    /// Returns whether or not the welding tool is currently on.
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:287
    public virtual object isOn()
    {
        return welding;
    }

    /// If welding tool ran out of fuel during a construction task, construction fails.
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:291
    public override object tool_use_check(mob_living user = null, object amount = null, object heat_required = null)
    {
        if (!Dm.truthy(isOn()) || !Dm.truthy(check_fuel()))
        {
            to_chat(user, span_warning(I18n.T("[] has to be on to complete this task!", this)));
            return FALSE;
        }
        if (Dm.lt(get_fuel(), amount))
        {
            to_chat(user, span_warning(I18n.T("You need more welding fuel to complete this task!")));
            return FALSE;
        }
        if (Dm.lt(heat, heat_required))
        {
            to_chat(user, span_warning(I18n.T("[] is not hot enough to complete this task!", this)));
            return FALSE;
        }
        return TRUE;
    }

    /// Ran when the welder is attacked by a screwdriver.
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:304
    public virtual async DmTask flamethrower_screwdriver(obj_item tool = null, mob user = null)
    {
        if (Dm.truthy(welding))
        {
            to_chat(user, span_warning(I18n.T("Turn it off first!")));
            return;
        }
        status = Dm.truthy(status) ? FALSE : TRUE;
        if (Dm.truthy(status))
        {
            to_chat(user, span_notice(I18n.T("You resecure [] and close the fuel tank.", this)));
            ((datum_reagents)reagents).flags = Dm.bitand(((datum_reagents)reagents).flags, Dm.bitnot(OPENCONTAINER));
        }
        else
        {
            to_chat(user, span_notice(I18n.T("[] can now be attached, modified, and refuelled.", this)));
            ((datum_reagents)reagents).flags = Dm.bitor(((datum_reagents)reagents).flags, OPENCONTAINER);
        }
        // tg: code/game/objects/items/tools/engineering/weldingtool.dm:315
        add_fingerprint(user);
    }

    /// First step of building a flamethrower (when a welder is attacked by rods)
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:318
    public virtual async DmTask flamethrower_rods(obj_item tool = null, mob user = null)
    {
        if (!Dm.truthy(status))
        {
            var used_rods = (obj_item_stack_rods)tool;
            if (Dm.truthy(await used_rods.use(1)))
            {
                var flamethrower_frame = Dm.@new<obj_item_flamethrower>(user.loc);
                if (!Dm.truthy(await remove_item_from_storage(flamethrower_frame, user)))
                    await user.transferItemToLoc(this, flamethrower_frame, TRUE);
                flamethrower_frame.weldtool = this;
                // tg: code/game/objects/items/tools/engineering/weldingtool.dm:326
                add_fingerprint(user);
                to_chat(user, span_notice(I18n.T("You add a rod to a welder, starting to build a flamethrower.")));
                await user.put_in_hands(flamethrower_frame);
            }
            else
                to_chat(user, span_warning(I18n.T("You need one rod to start building a flamethrower!")));
        }
    }

    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:332  (M7 lighting slice L1: a welder lights a candle)
    public override async DmTask<object> ignition_effect(atom ignitable_atom = null, mob user = null)
    {
        if (Dm.truthy(await use_tool(ignitable_atom, user as mob_living, 0)))
            return span_rose(I18n.T("[] casually lights [] with [], what a badass.", user, ignitable_atom, this));
        return "";
    }
}

// tg: code/game/objects/items/tools/engineering/weldingtool.dm:341
public partial class obj_item_weldingtool_largetank : obj_item_weldingtool
{
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:349
    public override async DmTask flamethrower_screwdriver(obj_item tool = null, mob user = null)
    {
        return;
    }
}

// tg: code/game/objects/items/tools/engineering/weldingtool.dm:362
public partial class obj_item_weldingtool_mini : obj_item_weldingtool
{
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:372
    public override async DmTask flamethrower_screwdriver(obj_item tool = null, mob user = null)
    {
        return;
    }
}

// tg: code/game/objects/items/tools/engineering/weldingtool.dm:378
public partial class obj_item_weldingtool_abductor : obj_item_weldingtool
{
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:390
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.num(get_fuel()) <= Dm.num(max_fuel))
            Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_fuel), 1));
        base.process(seconds_per_tick, __extra);
        return null;
    }
}

// tg: code/game/objects/items/tools/engineering/weldingtool.dm:403  (gaps J5, D298)
public partial class obj_item_weldingtool_experimental : obj_item_weldingtool
{
    // tg: code/game/objects/items/tools/engineering/weldingtool.dm:417
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        base.process(seconds_per_tick, __extra);
        if (Dm.num(get_fuel()) < Dm.num(max_fuel) && Dm.num(nextrefueltick) < world.time)
        {
            nextrefueltick = world.time + 10;
            Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_fuel), 1));
        }
        return null;
    }
}
