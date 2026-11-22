// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/portagrav.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the portable gravity unit, whole (should_have_node and connect_to_network since M7). Anchored and switched on (the
// right click or its Portagrav window) it lays a gravity field of its range (0 to its capacitors' maximum) and strength (0 to 2, 4
// emagged) around itself, drawing power per tile of range from its cell or, in wire mode, its cable (lasers lower the draw), and turns
// off when the power runs out; the panel takes and gives a cell, the screwdriver and crowbar deconstruct it, its anchors, lights and
// glow are overlays. Not here: the tools' screentip element (screentips, M10+), the emag's
// attack log (logging). The var blocks are generated but for `cell` below.
//
// GAP(types): tg's `var/obj/item/stock_parts/power_store/cell/cell = /obj/item/stock_parts/power_store/cell/high` (:17) holds the path
// the unit spawns with and then the cell; the generator types it `Type` from its default, so it is declared here as the DM value it is
// (a hand-written var block, types-converter.md 3.2: the constructor below is tg's var overrides of the type).

// tg: code/game/machinery/portagrav.dm:1
public partial class obj_machinery_power_portagrav : obj_machinery_power
{
    ///The cell we spawn with
    // tg: code/game/machinery/portagrav.dm:17  (hand-written: see the GAP(types) above)
    public object cell = typeof(obj_item_stock_parts_power_store_cell_high);

    // tg: code/game/machinery/portagrav.dm:1
    public obj_machinery_power_portagrav()
    {
        anchored = 0;
        density = 1;
        interaction_flags_machine = 8;
        icon = Dm.resource("icons/obj/machines/gravity_generator.dmi");
        icon_state = "portagrav";
        base_icon_state = "portagrav";
        name = "Portable Gravity Unit";
        desc = "Generates gravity around itself. Powered by wire or cell. Must be anchored before use.";
        max_integrity = 250;
        circuit = typeof(obj_item_circuitboard_machine_portagrav);
        armor_type = typeof(datum_armor_portable_gravity);
        interaction_flags_click = 64;
        //We don't use area power
        use_power = 0;
    }

    // The cell as the object it is once Initialize made it (tg reads `cell.` on its typed var).
    private obj_item_stock_parts_power_store_cell cell_obj => cell as obj_item_stock_parts_power_store_cell;

    // tg: code/game/machinery/portagrav.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (cell is Type cell_path)
            cell = Dm.@new(cell_path, this);
        if (Dm.truthy(anchored) && Dm.truthy(wire_mode))
            connect_to_network();

        Waiting.NoSleep(AddElement(
            typeof(datum_element_contextual_screentip_bare_hands),
            Dm.named("rmb_text", "Toggle power")));

        // var/static/list/tool_behaviors = list(TOOL_WRENCH = list(SCREENTIP_CONTEXT_LMB = "Anchor"))
        Dm.Deferred("code/game/machinery/portagrav.dm:53", "Initialize: AddElement(/datum/element/contextual_screentip_tools, tool_behaviors): screentips (M10+)");
        return dot;
    }

    // tg: code/game/machinery/portagrav.dm:55
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        cell = null;
        return dot;
    }

    // tg: code/game/machinery/portagrav.dm:59
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(anchored))
            Dm.append(ref dot, "portagrav_anchors");
        if (Dm.truthy(on))
        {
            Dm.append(ref dot, "portagrav_lights");
            Dm.append(ref dot, "activated");
        }
        return dot;
    }

    // tg: code/game/machinery/portagrav.dm:69
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("It is [].", Dm.truthy(on) ? I18n.T("on") : I18n.T("off")));
        Dm.append(ref dot, I18n.T("The charge meter reads: [].", cell_obj != null ? I18n.T("[]%", round(Dm.num(cell_obj.percent()), 1)) : I18n.T("NO CELL")));
        Dm.append(ref dot, I18n.T("It is[] anchored.", Dm.truthy(anchored) ? "" : I18n.T(" not")));
        if (in_range(user, this) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("<b>Right-click</b> to toggle [].", Dm.truthy(on) ? I18n.T("off") : I18n.T("on"))));
        return dot;
    }

    // tg: code/game/machinery/portagrav.dm:75
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float power_usage = Dm.initial(this, (obj_machinery_power_portagrav p) => p.draw_per_range);
        foreach (datum_stock_part_micro_laser laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            power_usage -= BASE_MACHINE_ACTIVE_CONSUMPTION / 10 * (Dm.num(laser.tier) - 1);
        draw_per_range = power_usage;
        float new_range = 4;
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            new_range += Dm.num(capacitor.tier);
        max_range = new_range;
        await update_field();
        return dot;
    }

    // tg: code/game/machinery/portagrav.dm:87
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_o", base_icon_state) : (string)base_icon_state;
        return dot;
    }

    // tg: code/game/machinery/portagrav.dm:91
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/portagrav.dm:94
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/portagrav.dm:97
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
            return NONE;
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, "must open panel!");
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(cell))
        {
            balloon_alert(user, "already has a cell!");
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_FAILURE;
        cell = tool;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/portagrav.dm:113
    public override object should_have_node()
    {
        return anchored;
    }

    // tg: code/game/machinery/portagrav.dm:116
    public override object connect_to_network()
    {
        object dot = null;
        if (!Dm.truthy(anchored))
            return FALSE;
        dot = base.connect_to_network();
        return dot;
    }

    // tg: code/game/machinery/portagrav.dm:121
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (Dm.truthy(on))
        {
            balloon_alert(user, "turn off first!");
            return dot;
        }
        await default_unfasten_wrench(user, tool);
        if (Dm.truthy(anchored) && Dm.truthy(wire_mode))
            connect_to_network();
        else
            disconnect_from_network();
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/portagrav.dm:134
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/game/machinery/portagrav.dm:137
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(panel_open) || cell == null || user is not mob_living_carbon carbon || Dm.truthy(carbon.combat_mode))
            return dot;
        if (Dm.truthy(await carbon.put_in_hands(cell_obj)))
            cell = null;
        return dot;
    }

    // tg: code/game/machinery/portagrav.dm:144
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(await can_interact(user)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        await toggle_on(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/portagrav.dm:150
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        visible_message(span_warning(I18n.T("Sparks fly out of []!", this)));
        if (user != null)
        {
            balloon_alert(user, "unsafe gravity unlocked");
            user.log_message(Dm.text("emagged [].", this), LOG_ATTACK);
        }
        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        return TRUE;
    }

    // tg: code/game/machinery/portagrav.dm:161
    public virtual async DmTask toggle_on(mob user = null)
    {
        if (Dm.truthy(on))
            turn_off(user);
        else
            await turn_on(user);
    }

    // tg: code/game/machinery/portagrav.dm:167
    public virtual async DmTask<object> turn_on(mob user = null)
    {
        if (!Dm.truthy(anchored))
        {
            if (user != null)
                balloon_alert(user, "not anchored!");
            return FALSE;
        }
        if ((!Dm.truthy(wire_mode) && Dm.num(cell_obj?.charge) < draw_per_range * Dm.num(range)) || (Dm.truthy(wire_mode) && Dm.num(surplus()) < draw_per_range * Dm.num(range)))
        {
            if (user != null)
                balloon_alert(user, "not enough power!");
            return FALSE;
        }
        if (user != null)
            balloon_alert(user, "turned on");
        on = TRUE;
        START_PROCESSING(SSmachines, this);
        gravity_field = await Dm.new_async<datum_proximity_monitor_advanced_gravity_subtle_effect>(this, Dm.named("range", range), Dm.named("gravity", grav_strength));
        update_appearance();
        return null;
    }

    // tg: code/game/machinery/portagrav.dm:183
    public virtual void turn_off(mob user = null)
    {
        on = FALSE;
        if (user != null)
            balloon_alert(user, "turned off");
        STOP_PROCESSING(SSmachines, this);
        QDEL_NULL(ref gravity_field);
        update_appearance();
    }

    // tg: code/game/machinery/portagrav.dm:191
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(on) || !Dm.truthy(anchored))
            return PROCESS_KILL;
        if (Dm.truthy(wire_mode))
        {
            if (Dm.truthy(powernet) && Dm.num(surplus()) >= draw_per_range * Dm.num(range))
                add_load(draw_per_range * Dm.num(range));
            else
                turn_off();
        }
        else
        {
            if (cell_obj == null || !Dm.truthy(Waiting.NoSleep(cell_obj.use(draw_per_range * Dm.num(range)))))
                turn_off();
        }
        return null;
    }

    // tg: code/game/machinery/portagrav.dm:203
    public virtual async DmTask update_field()
    {
        if (gravity_field == null)
            return;
        await gravity_field.set_range(range);
        gravity_field.gravity_value = grav_strength;
        gravity_field.recalculate_field(full_recalc: TRUE);
    }

    // tg: code/game/machinery/portagrav.dm:210
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Portagrav", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/portagrav.dm:216
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        if (cell_obj != null)
            dot["percentage"] = (Dm.num(cell_obj.charge) / Dm.num(cell_obj.maxcharge)) * 100;
        dot["gravity"] = grav_strength;
        dot["range"] = range;
        dot["maxrange"] = max_range;
        dot["on"] = on;
        dot["wiremode"] = wire_mode;
        dot["draw"] = display_power(draw_per_range * Dm.num(range));
        return dot;
    }

    // tg: code/game/machinery/portagrav.dm:227
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        playsound(this, Dm.resource("sound/machines/terminal/terminal_button07.ogg"), 45, TRUE);
        switch (action)
        {
            case "adjust_grav":
            {
                object adjustment = text2num(@params["adjustment"]);
                if (adjustment == null)
                    return dot;
                float bonus = Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? 2 : 0;
                // REPLACE 0 with NEGATIVE_GRAVITY ONCE NEGATIVE GRAVITY IS SOMETHING ACTUALLY FUNCTIONAL
                float result = clamp(grav_strength + Dm.num(adjustment), 0, GRAVITY_DAMAGE_THRESHOLD - 1 + bonus);
                if (result == grav_strength)
                    return dot;
                grav_strength = result;
                await update_field();
                return TRUE;
            }
            case "toggle_power":
                await toggle_on(usr);
                return TRUE;
            case "toggle_wire":
                wire_mode = Dm.num(!Dm.truthy(wire_mode));
                if (Dm.truthy(wire_mode) && Dm.truthy(anchored))
                    connect_to_network();
                else
                    disconnect_from_network();
                return TRUE;
            case "adjust_range":
            {
                object adjustment = text2num(@params["adjustment"]);
                if (adjustment == null)
                    return dot;
                float result = clamp(Dm.num(range) + Dm.num(adjustment), 0, Dm.num(max_range));
                if (result == Dm.num(range))
                    return dot;
                range = result;
                await update_field();
                return TRUE;
            }
        }
        return dot;
    }
}
