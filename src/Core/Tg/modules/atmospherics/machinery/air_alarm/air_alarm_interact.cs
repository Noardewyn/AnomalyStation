// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 7 (the wall-mounted frames, D148): the air alarm's construction states (buildstage: no circuit, no wires,
// complete) with the crowbar, the screwdriver, the wrench, the electronics and the cable, and its deconstruction; the air
// alarm frame's try_build. The air alarm has no wires yet (/datum/wires/airalarm: hacking, M7, a deferred leaf of
// Initialize): the wires window (wires.interact) and the wirecutters' "final wires" (wires.is_all_cut) are not-translated
// leaves and the wiring's wires.repair a deferred one, so a complete air alarm is taken apart only once the wires come
// (M7); a frame is built up to a complete alarm (E13: the RCD's rcd_vals and rcd_act). The reset of the wires stays a generated live-partial
// stub (the emag came with it, G1a, D316); the shock is the M7 shocks slice's. The interface lock (togglelock, attack_hand_secondary) is translated with the window (the air alarm
// window, 2026-10-16). The var blocks are generated.

// tg: code/modules/atmospherics/machinery/air_alarm/_air_alarm.dm:1
public partial class obj_machinery_airalarm : obj_machinery
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:1
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(buildstage, AIR_ALARM_BUILD_NO_WIRES))
            return null;
        user.visible_message(span_notice(I18n.T("[] removes the electronics from [].", user.name, name)),
            span_notice(I18n.T("You start prying out the circuit...")));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 20)))
        {
            if (Dm.eq(buildstage, AIR_ALARM_BUILD_NO_WIRES))
            {
                to_chat(user, span_notice(I18n.T("You remove the air alarm electronics.")));
                Dm.@new<obj_item_electronics_airalarm>(drop_location());
                playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:11
                buildstage = AIR_ALARM_BUILD_NO_CIRCUIT;
                update_appearance();
            }
        }
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:16
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(buildstage, AIR_ALARM_BUILD_COMPLETE))
            return null;
        tool.play_tool_sound(this);
        toggle_panel_open();
        to_chat(user, span_notice(I18n.T("The wires have been [].", Dm.truthy(panel_open) ? "exposed" : "unexposed")));
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:25
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (!(Dm.eq(buildstage, AIR_ALARM_BUILD_COMPLETE) && Dm.truthy(panel_open) && Dm.truthy(wires.is_all_cut())))
            return null;
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You cut the final wires.")));
        var cables = Dm.@new<obj_item_stack_cable_coil>(drop_location(), 5);
        await user.put_in_hands(cables);
        buildstage = AIR_ALARM_BUILD_NO_WIRES;
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:36
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(buildstage, AIR_ALARM_BUILD_NO_CIRCUIT))
            return null;
        to_chat(user, span_notice(I18n.T("You detach \\\\the [] from the wall.", this)));
        tool.play_tool_sound(this);
        obj_item_wallframe_airalarm alarm_frame = Dm.@new<obj_item_wallframe_airalarm>(drop_location());
        await user.put_in_hands(alarm_frame);
        qdel(this);
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:61
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (!Dm.truthy(await can_interact(user)))
            return dot;
        if (!Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)) || !isturf(loc))
            return dot;
        await togglelock(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:70
    public virtual async DmTask togglelock(mob_living user = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            to_chat(user, span_warning(I18n.T("It does nothing!")));
        else
        {
            if (HAS_TRAIT(user, TRAIT_SILICON_ACCESS)) // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178)
            {
                locked = Dm.truthy(locked) ? FALSE : TRUE;
                return;
            }
            // !wires.is_cut(WIRE_IDSCAN): the air alarm's wires are not set until M7 (set_wires is a deferred leaf of
            // Initialize), so no wire of it is cut
            bool idscan_cut = wires != null && Dm.truthy(wires.is_cut(WIRE_IDSCAN));
            if (Dm.truthy(await allowed__proc(World.Current.usr as mob)) && !idscan_cut)
            {
                locked = Dm.truthy(locked) ? FALSE : TRUE;
                to_chat(user, span_notice(I18n.T("You [] the air alarm interface.", Dm.truthy(locked) ? "lock" : "unlock")));
                if (!Dm.truthy(locked))
                    await ui_interact(user);
            }
            else
                to_chat(user, span_danger(I18n.T("Access denied.")));
        }
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:85
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        visible_message(span_warning(I18n.T("Sparks fly out of []!", this)));
        balloon_alert(user, "authentication sensors scrambled");
        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:96
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        disassembled ??= TRUE;
        Dm.@new<obj_item_stack_sheet_iron>(loc, 2);
        if (Dm.eq(buildstage, AIR_ALARM_BUILD_NO_WIRES) || Dm.eq(buildstage, AIR_ALARM_BUILD_COMPLETE))
        {
            obj_item_electronics_airalarm alarm = Dm.@new<obj_item_electronics_airalarm>(loc);
            if (!Dm.truthy(disassembled))
                await alarm.take_damage(alarm.max_integrity * 0.5f, sound_effect: FALSE);
        }
        if (Dm.eq(buildstage, AIR_ALARM_BUILD_COMPLETE))
            Dm.@new<obj_item_stack_cable_coil>(loc, 3);
        return null;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:105
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        update_last_used(user);
        float sw__1 = Dm.num(buildstage);
        if (sw__1 == AIR_ALARM_BUILD_COMPLETE)
        {
            if (Dm.truthy(tool.GetID()))// trying to unlock the interface with an ID card
            {
                await togglelock(user);
                return ITEM_INTERACT_SUCCESS;
            }

            // is_wire_tool(tool) (datums/wires/_wires.dm:13, check_secured = FALSE)
            if (Dm.truthy(panel_open) && (Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER) || Dm.eq(tool.tool_behaviour, TOOL_MULTITOOL) || tool is obj_item_assembly))
            {
                await wires.interact(user);
                return ITEM_INTERACT_SUCCESS;
            }

            return NONE;
        }
        else if (sw__1 == AIR_ALARM_BUILD_NO_WIRES)
        {
            if (tool is not obj_item_stack_cable_coil)
                return NONE;

            obj_item_stack_cable_coil cable = (obj_item_stack_cable_coil)tool;
            if (Dm.num(cable.get_amount()) < 5)
            {
                to_chat(user, span_warning(I18n.T("You need five lengths of cable to wire the air alarm!")));
                return ITEM_INTERACT_BLOCKING;
            }

            user.visible_message(span_notice(I18n.T("[] wires the air alarm.", user.name)),
                span_notice(I18n.T("You start wiring the air alarm...")));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;

            if (Dm.num(cable.get_amount()) < 5 || !Dm.eq(buildstage, AIR_ALARM_BUILD_NO_WIRES))
                return ITEM_INTERACT_BLOCKING;

            await cable.use(5);
            to_chat(user, span_notice(I18n.T("You wire the air alarm.")));
            await wires.repair();
            aidisabled = FALSE;
            locked = FALSE;
            shorted = FALSE;
            danger_level = AIR_ALARM_ALERT_NONE;
            buildstage = AIR_ALARM_BUILD_COMPLETE;
            select_mode(user, typeof(datum_air_alarm_mode_filtering));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        else if (sw__1 == AIR_ALARM_BUILD_NO_CIRCUIT)
        {
            if (tool is obj_item_electronics_airalarm)
            {
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                    return ITEM_INTERACT_BLOCKING;

                to_chat(user, span_notice(I18n.T("You insert the circuit.")));
                buildstage = AIR_ALARM_BUILD_NO_WIRES;
                update_appearance();
                qdel(tool);
                return ITEM_INTERACT_SUCCESS;
            }

            if (tool is obj_item_electroadaptive_pseudocircuit pseudocircuit)
            {
                if (!Dm.truthy(await pseudocircuit.adapt_circuit(user as mob_living_silicon_robot, circuit_cost: 0.025f * STANDARD_CELL_CHARGE)))
                    return ITEM_INTERACT_BLOCKING;

                user.visible_message(span_notice(I18n.T("[] fabricates a circuit and places it into [].", user, this)),
                    span_notice(I18n.T("You adapt an air alarm circuit and slot it into the assembly.")));
                buildstage = AIR_ALARM_BUILD_NO_WIRES;
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }

            return NONE;
        }

        return NONE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:171
    public virtual void reset(object wire = null)
    {
        if (Dm.eq(wire, WIRE_POWER) && !Dm.truthy(wires.is_cut(WIRE_POWER)))
        {
            shorted = FALSE;
            update_appearance();
        }
        else if (Dm.eq(wire, WIRE_AI) && !Dm.truthy(wires.is_cut(WIRE_AI)))
            aidisabled = FALSE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:181
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER))) // unpowered, no shock
            return FALSE;
        return await base.shock__proc(shocking, chance, shock_source, siemens_coeff);
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:190
public partial class obj_item_wallframe_airalarm : obj_item_wallframe
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:198
    public override async DmTask<object> try_build(atom support = null, mob user = null)
    {
        area A = get_area(user) as area;
        if (Dm.truthy(A.always_unpowered))
        {
            balloon_alert(user, I18n.T("cannot place in this area!"));
            return FALSE;
        }
        return await base.try_build(support, user);
    }
}

// tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:48 (E13: the RCD's simple circuits upgrade)
public partial class obj_machinery_airalarm : obj_machinery
{
    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:48
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if ((buildstage == AIR_ALARM_BUILD_NO_CIRCUIT) && Dm.truthy(Dm.bitand(the_rcd.construction_upgrades, RCD_UPGRADE_SIMPLE_CIRCUITS)))
            return Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 1));
        return FALSE;
    }

    // tg: code/modules/atmospherics/machinery/air_alarm/air_alarm_interact.dm:53
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_WALLFRAME))
        {
            balloon_alert(user, I18n.T("circuit installed"));
            buildstage = AIR_ALARM_BUILD_NO_WIRES;
            update_appearance();
            return TRUE;
        }
        return FALSE;
    }
}
