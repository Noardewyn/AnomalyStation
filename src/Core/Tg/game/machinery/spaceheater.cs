// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/spaceheater.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E9 (docs/ai/specs/engineering.md): the whole file, the space heater and the improvised chem heater. The examines (examine and heating_examine of both) are translated (X1). Deferred leaves: the
// screentips of the heater's Initialize (not-translated.md),
// the emissive overlays (DEVIATION DV12, as the other machines') and the beaker's reactions: the improvised heater's handle_reactions
// is the holder's own, whose reaction lookup is a deferred leaf (chemistry, 0/717 reactions). The var blocks are generated.
//
// GAP(types): tg's `var/obj/item/stock_parts/power_store/cell/cell = /obj/item/...` (:23) is generated as a Type (rules 3.2 V3: other
// types assign typepaths to vars of that name), which the heater's cell instance does not fit (types-converter.md 3.5, "Not covered").
// As the machine's board (circuit_value), the instance is cell_value; `cell` keeps the path the heater spawns with, and the chem
// heater's `cell = null` is its constructor's. The chem heater's `var/datum/controller/subsystem/processing/our_subsystem` (:338)
// holds SSmachines (the machine's subsystem_type), which is no processing subsystem in the port: the subsystem is our_subsystem_value,
// typed as the base subsystem (the generated `our_subsystem` stays null).

// tg: code/game/machinery/spaceheater.dm:7
public partial class obj_machinery_space_heater : obj_machinery
{
    // tg: code/game/machinery/spaceheater.dm:1-4  #define HEATER_MODE_* (file-local, #undef at :539)
    protected const string HEATER_MODE_STANDBY = "standby";
    protected const string HEATER_MODE_HEAT = "heat";
    protected const string HEATER_MODE_COOL = "cool";
    protected const string HEATER_MODE_AUTO = "auto";
    // tg: code/game/machinery/spaceheater.dm:5  #define BASE_HEATING_ENERGY (40 KILO JOULES): the `heating_energy` default (generated)

    ///The cell instance (tg's `cell` once Initialize has made it from the path)
    // tg: code/game/machinery/spaceheater.dm:23  (hand-written: see the GAP(types) above)
    public obj_item_stock_parts_power_store_cell cell_value;

    // tg: code/game/machinery/spaceheater.dm:49
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell_value;
    }

    // tg: code/game/machinery/spaceheater.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (cell != null) // ispath(cell)
            cell_value = Dm.@new(cell, this) as obj_item_stock_parts_power_store_cell;
        update_appearance();
        SSair.start_processing_machine(this);

        // AddElement(/datum/element/contextual_screentip_bare_hands, rmb_text = "Toggle power") and
        // AddElement(/datum/element/contextual_screentip_tools, tool_behaviors) (screwdriver: "Open hatch", wrench: "Anchor"): screentips (D264)
        Dm.Deferred("code/game/machinery/spaceheater.dm:59", "Initialize: AddElement(/datum/element/contextual_screentip_bare_hands) and AddElement(/datum/element/contextual_screentip_tools): screentips (not now, by the owner: D264)");

        Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));
        Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 8)));
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:77
    public override object Destroy(float? force = null)
    {
        SSair.stop_processing_machine(this);
        QDEL_NULL(ref cell_value);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/spaceheater.dm:82
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        set_panel_open(TRUE);
        QDEL_NULL(ref cell_value);
    }

    // tg: code/game/machinery/spaceheater.dm:86
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (cell_value != null)
        {
            Dm.append(ref component_parts, cell_value);
            cell_value = null;
        }
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/game/machinery/spaceheater.dm:92
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == cell_value)
            cell_value = null;
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:97
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("\\The [] is [], and the hatch is [].", this, Dm.truthy(on) ? I18n.T("on") : I18n.T("off"), Dm.truthy(panel_open) ? I18n.T("open") : I18n.T("closed")));
        if (cell_value != null)
            Dm.append(ref dot, I18n.T("The charge meter reads []%.", cell_value != null ? round(Dm.num(cell_value.percent()), 1) : 0));
        else
            Dm.append(ref dot, span_warning(I18n.T("There is no power cell installed.")));
        if (in_range(user, this) || isobserver(user))
        {
            Dm.append(ref dot, heating_examine());
            Dm.append(ref dot, span_notice(I18n.T("<b>Right-click</b> to toggle [].", Dm.truthy(on) ? I18n.T("off") : I18n.T("on"))));
        }
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:109
    public virtual object heating_examine()
    {
        float target_temp = round(Dm.num(target_temperature) - T0C, 1);
        float min_temp = max(settable_temperature_median - settable_temperature_range, TCMB) - T0C;
        float max_temp = settable_temperature_median + settable_temperature_range - T0C;
        return span_notice(I18n.T("The status display reads:<br>Heating power: <b>[] at []% efficiency.</b><br>Target temperature: <b>[]°C []°C - []°C]</b>\n", display_power(heating_energy, convert: TRUE, scheduler: SSair), (efficiency / 20) * 100, target_temp, min_temp, max_temp));
    }

    // tg: code/game/machinery/spaceheater.dm:115
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]-[]", base_icon_state, Dm.truthy(on) ? mode : "off");
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:119
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        // DEVIATION(DV12): if(on) . += emissive_appearance(icon, "[emissive_state ? emissive_state : base_icon_state + "-" + mode]-emissive", src, alpha = src.alpha),
        // the glow on the emissive plane, is not created (lighting planes and emissives are not ported).
        if (Dm.truthy(panel_open))
        {
            Dm.append(ref dot, Dm.text("[]-open", base_icon_state));
            // DEVIATION(DV12): . += emissive_blocker(icon, "[base_icon_state]-open", src, alpha = src.alpha) is not created either.
        }
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:128
    public override void on_set_panel_open(object old_value = null)
    {
        update_appearance();
        base.on_set_panel_open(old_value);
    }

    // tg: code/game/machinery/spaceheater.dm:132
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        if (!Dm.truthy(on) || !Dm.truthy(is_operational) || QDELETED(cell_value) || cell_value.charge <= 1)
        {
            if (Dm.truthy(on)) // If it's broken, turn it off too
            {
                on = FALSE;
                update_appearance();
            }
            return PROCESS_KILL;
        }

        turf local_turf = loc as turf;
        if (local_turf == null)
        {
            if (mode != HEATER_MODE_STANDBY)
            {
                mode = HEATER_MODE_STANDBY;
                update_appearance();
            }
            return dot.Value;
        }

        datum_gas_mixture enviroment = await local_turf.return_air() as datum_gas_mixture;

        string new_mode = HEATER_MODE_STANDBY;
        if (set_mode != HEATER_MODE_COOL && enviroment.temperature < Dm.num(target_temperature) - temperature_tolerance)
            new_mode = HEATER_MODE_HEAT;
        else if (set_mode != HEATER_MODE_HEAT && enviroment.temperature > Dm.num(target_temperature) + temperature_tolerance)
            new_mode = HEATER_MODE_COOL;

        if (mode != new_mode)
        {
            mode = new_mode;
            update_appearance();
        }

        if (mode == HEATER_MODE_STANDBY)
            return dot.Value;

        DmList turfs = Dm.add(local_turf.atmos_adjacent_turfs ?? Dm.list(), local_turf) as DmList;
        float required_energy = abs(enviroment.temperature - Dm.num(target_temperature)) * Dm.num(enviroment.heat_capacity());
        required_energy = min(required_energy, heating_energy, (cell_value.charge * efficiency) / Dm.length(turfs));
        if (required_energy < 1)
            return dot.Value;

        float delta_energy = required_energy;
        if (mode == HEATER_MODE_COOL)
            delta_energy *= -1;
        if (delta_energy == 0)
            return dot.Value;

        foreach (turf_open turf in Dm.each<turf_open>(turfs))
        {
            datum_gas_mixture turf_gasmix = await turf.return_air() as datum_gas_mixture;
            turf_gasmix.temperature += delta_energy / Dm.num(turf_gasmix.heat_capacity());
            air_update_turf(FALSE, FALSE);
        }
        await cell_value.use((required_energy * Dm.length(turfs)) / efficiency, TRUE);
        return dot.Value;
    }

    // tg: code/game/machinery/spaceheater.dm:179
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float laser = 0;
        float cap = 0;
        foreach (datum_stock_part_micro_laser micro_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            laser += Dm.num(micro_laser.tier);
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            cap += Dm.num(capacitor.tier);

        heating_energy = laser * Dm.initial(this, (obj_machinery_space_heater p) => p.heating_energy);

        settable_temperature_range = cap * Dm.initial(this, (obj_machinery_space_heater p) => p.settable_temperature_range);
        efficiency = (cap + 1) * Dm.initial(this, (obj_machinery_space_heater p) => p.efficiency) * 0.5f;

        target_temperature = clamp(Dm.num(target_temperature),
            max(settable_temperature_median - settable_temperature_range, TCMB),
            settable_temperature_median + settable_temperature_range);
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:197
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;
        if (cell_value != null)
            await cell_value.emp_act(severity);
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:204
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/spaceheater.dm:208
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = default_deconstruction_screwdriver(user, tool);
        user.visible_message(
            span_notice(I18n.T("[] []s the hatch on [].", user, Dm.truthy(panel_open) ? "open" : "close", this)),
            span_notice(I18n.T("You [] the hatch on [].", Dm.truthy(panel_open) ? "open" : "close", this)));
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:461  (tg defines /space_heater/crowbar_act twice, here and at :216: the later definition, with
    // the fingerprint, is the one that runs)
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        add_fingerprint(user);
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/spaceheater.dm:219
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
        {
            add_fingerprint(user);
            if (!Dm.truthy(panel_open))
            {
                to_chat(user, span_warning(I18n.T("The hatch must be open to insert a power cell!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (cell_value != null)
            {
                to_chat(user, span_warning(I18n.T("There is already a power cell inside!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            cell_value = tool as obj_item_stock_parts_power_store_cell;
            tool.add_fingerprint(usr);
            user.visible_message(
                span_notice(I18n.T("[] inserts [] into [].", user, tool, this)),
                span_notice(I18n.T("You insert [] into [].", tool, this)));
            SStgui.update_uis(this);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/spaceheater.dm:241
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(await can_interact(user)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        toggle_power(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/spaceheater.dm:247
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "SpaceHeater", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/spaceheater.dm:253
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["open"] = panel_open;
        data["on"] = on;
        data["mode"] = set_mode;
        data["hasPowercell"] = cell_value != null ? TRUE : FALSE;
        data["chemHacked"] = FALSE;
        if (cell_value != null)
            data["powerLevel"] = round(Dm.num(cell_value.percent()), 1);
        data["targetTemp"] = round(Dm.num(target_temperature) - T0C, 1);
        data["minTemp"] = max(settable_temperature_median - settable_temperature_range, TCMB) - T0C;
        data["maxTemp"] = settable_temperature_median + settable_temperature_range - T0C;

        turf local_turf = get_turf(loc) as turf;
        float? current_temperature = null;
        if (local_turf != null) // istype(local_turf)
        {
            datum_gas_mixture enviroment = await local_turf.return_air() as datum_gas_mixture;
            current_temperature = enviroment.temperature;
        }
        else if (isturf(local_turf))
            current_temperature = Dm.num(local_turf.temperature);
        if (current_temperature == null)
            data["currentTemp"] = "N/A";
        else
            data["currentTemp"] = round(current_temperature.Value - T0C, 1);
        return data;
    }

    // tg: code/game/machinery/spaceheater.dm:279
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "power":
                toggle_power();
                dot = TRUE;
                break;

            case "mode":
                set_mode = Dm.str(@params["mode"]);
                dot = TRUE;
                break;

            case "target":
            {
                if (!Dm.truthy(panel_open))
                    return dot;
                object target = @params["target"];
                if (text2num(target) != null)
                {
                    target = text2num(target) + T0C;
                    dot = TRUE;
                }
                if (Dm.truthy(dot))
                {
                    target_temperature = clamp(round(Dm.num(target)),
                        max(settable_temperature_median - settable_temperature_range, TCMB),
                        settable_temperature_median + settable_temperature_range);
                }
                break;
            }

            case "eject":
                if (Dm.truthy(panel_open) && cell_value != null)
                {
                    await usr.put_in_hands(cell_value);
                    dot = TRUE;
                }
                break;
        }
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:307
    public virtual void toggle_power(object user = null)
    {
        on = Dm.truthy(on) ? FALSE : TRUE;
        mode = HEATER_MODE_STANDBY;
        if (user != null)
        {
            if (QDELETED(cell_value))
                balloon_alert(user as mob, I18n.T("no cell!"));
            else if (!Dm.truthy(cell_value.charge__proc()))
                balloon_alert(user as mob, I18n.T("no charge!"));
            else if (!Dm.truthy(is_operational))
                balloon_alert(user as mob, I18n.T("not operational!"));
            else
                balloon_alert(user as mob, I18n.T("turned []", Dm.truthy(on) ? "on" : "off"));
        }
        update_appearance();
        if (Dm.truthy(on))
            SSair.start_processing_machine(this);
    }
}

///For use with heating reagents in a ghetto way
// tg: code/game/machinery/spaceheater.dm:325
public partial class obj_machinery_space_heater_improvised_chem_heater : obj_machinery_space_heater
{
    // the beaker's reagent holder (atom.reagents is an object in the port)
    private datum_reagents beaker_reagents => beaker.reagents as datum_reagents;

    ///The subsystem we're being processed by
    // tg: code/game/machinery/spaceheater.dm:338  (hand-written: see the GAP(types) above)
    private datum_controller_subsystem our_subsystem_value;

    // tg: code/game/machinery/spaceheater.dm:340
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        our_subsystem_value = Dm.locate_type(subsystem_type, Master?.subsystems) as datum_controller_subsystem;
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/machinery/spaceheater.dm:344
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref beaker);
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:348
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        if (!isliving(crafter))
            return;
        mob_living user = crafter as mob_living;
        obj_item_stock_parts_power_store_cell cell = Dm.locate<obj_item_stock_parts_power_store_cell>(range(1)) ?? user.is_holding_item_of_type(typeof(obj_item_stock_parts_power_store_cell)) as obj_item_stock_parts_power_store_cell;
        if (cell == null)
            return;
        turf turf = get_turf(cell) as turf;
        await forceMove(turf);
        await try_insert_cell(user, cell, TRUE); //puts it into the heater
    }

    // tg: code/game/machinery/spaceheater.dm:360
    public override object heating_examine()
    {
        object dot = base.heating_examine();
        // Conducted energy per joule of thermal energy difference in a tick.
        float conduction_energy = beaker_conduction_power * (set_mode == HEATER_MODE_AUTO ? 0.5f : 1) * Dm.num(our_subsystem_value.wait) / (1 * SECONDS);
        // This accounts for the timestep inaccuracy.
        Dm.append(ref dot, span_notice(I18n.T("Reagent conduction power: <b>[]/J</b>", conduction_energy < 1 ? display_power(-log(1 - conduction_energy) * SECONDS / Dm.num(our_subsystem_value.wait), convert: FALSE) : "∞W")));
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:367
    public override void toggle_power(object user = null)
    {
        base.toggle_power(user);
        if (Dm.truthy(on))
            begin_processing();
    }

    // tg: code/game/machinery/spaceheater.dm:372
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(on) || !Dm.truthy(is_operational) || QDELETED(cell_value) || cell_value.charge <= 1 || QDELETED(beaker))
        {
            if (Dm.truthy(on)) // If it's broken, turn it off too
            {
                on = FALSE;
                update_appearance();
            }
            return PROCESS_KILL;
        }

        if (Dm.truthy(beaker_reagents.total_volume))
        {
            float conduction_modifier = beaker_conduction_power;
            switch (set_mode)
            {
                case HEATER_MODE_AUTO:
                    conduction_modifier *= 0.5f;
                    break;
                case HEATER_MODE_HEAT:
                    if (Dm.num(target_temperature) < beaker_reagents.chem_temp)
                        return null;
                    break;
                case HEATER_MODE_COOL:
                    if (Dm.num(target_temperature) > beaker_reagents.chem_temp)
                        return null;
                    break;
            }

            float required_energy = abs(Dm.num(target_temperature) - beaker_reagents.chem_temp) * conduction_modifier * Dm.num(seconds_per_tick) * Dm.num(beaker_reagents.heat_capacity());
            required_energy = min(required_energy, heating_energy, cell_value.charge * efficiency);
            if (required_energy < 1)
                return null;

            float delta_energy = required_energy;
            if (mode == HEATER_MODE_COOL)
                delta_energy *= -1;
            if (delta_energy == 0)
                return null;

            beaker_reagents.adjust_thermal_energy(delta_energy);
            Waiting.NoSleep(beaker_reagents.handle_reactions());
            Waiting.NoSleep(cell_value.use(required_energy / efficiency, TRUE));
        }
        update_appearance();
        return null;
    }

    // tg: code/game/machinery/spaceheater.dm:407
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["chemHacked"] = TRUE;
        dot["beaker"] = beaker;
        dot["currentTemp"] = beaker != null ? round(beaker_reagents.chem_temp - T0C) : "N/A";
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:413
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "ejectBeaker":
                //Eject doesn't turn it off, so you can preheat for beaker swapping
                await replace_beaker(usr as mob_living);
                dot = TRUE;
                break;
        }
        return dot;
    }

    ///Slightly modified to ignore the open_hatch - it's always open, we hacked it.
    // tg: code/game/machinery/spaceheater.dm:424
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
            return await try_insert_cell(user, tool as obj_item_stock_parts_power_store_cell);

        //reagent containers
        if (tool is obj_item_reagent_containers && !Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) && Dm.truthy(tool.is_open_container()))
        {
            obj_item_reagent_containers container = tool as obj_item_reagent_containers;
            if (!Dm.truthy(await user.transferItemToLoc(container, this)))
                return ITEM_INTERACT_BLOCKING;
            await replace_beaker(user, container);
            to_chat(user, span_notice(I18n.T("You add [] to []'s water bath.", container, this)));
            await ui_interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (beaker == null || !Dm.truthy(is_type_in_list(tool, Dm.list(typeof(obj_item_reagent_containers_dropper), typeof(obj_item_ph_meter), typeof(obj_item_ph_paper), typeof(obj_item_reagent_containers_syringe)))))
            return NONE;
        //Dropper tools
        await tool.interact_with_atom(beaker, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/spaceheater.dm:445
    public virtual async DmTask<object> try_insert_cell(mob_living user = null, obj_item_stock_parts_power_store_cell battery = null, float? silent = null)
    {
        silent ??= FALSE;
        if (cell_value != null)
        {
            to_chat(user, span_warning(I18n.T("There is already a power cell inside!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(battery, this)))
            return ITEM_INTERACT_BLOCKING;

        cell_value = battery;
        battery.add_fingerprint(user);

        if (!Dm.truthy(silent))
            user.visible_message(span_notice(I18n.T("\\The [] inserts a power cell into \\the [].", user, this)), span_notice(I18n.T("You insert the power cell into \\the [].", this)));
        SStgui.update_uis(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/spaceheater.dm:465
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        disassembled ??= TRUE;
        object dot = await base.on_deconstruction(disassembled);
        if (Dm.truthy(disassembled))
        {
            if (beaker != null)
                await beaker.forceMove(drop_location() as atom);
            beaker = null;
        }
        DmList bonus_junk = Dm.list(
            Dm.kv(typeof(obj_item_stack_cable_coil), 2),
            Dm.kv(typeof(obj_item_stack_sheet_glass), 2),
            Dm.kv(typeof(obj_item_stack_sheet_iron), 2),
            Dm.kv(typeof(obj_item_thermometer), 1));
        foreach (Type item in Dm.each(bonus_junk))
        {
            if (prob(80))
                Dm.@new(item, get_turf(loc));
        }
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:480
    public virtual async DmTask<object> replace_beaker(mob_living user = null, obj_item_reagent_containers new_beaker = null)
    {
        if (user == null)
            return FALSE;
        if (beaker != null)
        {
            await try_put_in_hand(beaker, user);
            beaker = null;
        }
        if (new_beaker != null)
            beaker = new_beaker;
        update_appearance();
        return TRUE;
    }

    // tg: code/game/machinery/spaceheater.dm:491
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await replace_beaker(user as mob_living);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/spaceheater.dm:495
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (!Dm.truthy(on) || beaker == null || cell_value == null)
        {
            icon_state = "sheater-off";
            emissive_state = null;
            return dot;
        }
        if (Dm.num(target_temperature) < beaker_reagents.chem_temp)
        {
            icon_state = "sheater-cool";
            emissive_state = icon_state;
            return dot;
        }
        if (Dm.num(target_temperature) > beaker_reagents.chem_temp)
        {
            icon_state = "sheater-heat";
            emissive_state = icon_state;
            return dot;
        }
        icon_state = "sheater-off";
        emissive_state = null;
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:512
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, Dm.text("[]-beaker", icon_state));
        Dm.append(ref dot, Dm.text("[]-rigged", base_icon_state));
        // DEVIATION(DV12): . += emissive_blocker(icon, "[base_icon_state]-rigged", src, alpha = src.alpha) is not created (as the heater's).
        return dot;
    }

    // tg: code/game/machinery/spaceheater.dm:518
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float lasers_rating = 0;
        float capacitors_rating = 0;
        foreach (datum_stock_part_micro_laser laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            lasers_rating += Dm.num(laser.tier);
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            capacitors_rating += Dm.num(capacitor.tier);

        heating_energy = lasers_rating * Dm.initial(this, (obj_machinery_space_heater p) => p.heating_energy);

        settable_temperature_range = capacitors_rating * Dm.initial(this, (obj_machinery_space_heater p) => p.settable_temperature_range); //-20 - 80 at base
        efficiency = (capacitors_rating + 1) * Dm.initial(this, (obj_machinery_space_heater p) => p.efficiency) * 0.5f;

        target_temperature = clamp(Dm.num(target_temperature),
            max(settable_temperature_median - settable_temperature_range, TCMB),
            settable_temperature_median + settable_temperature_range);

        // No time integration is used, so we should clamp this to prevent being able to overshoot if there was a subtype with a high initial value.
        beaker_conduction_power = min((capacitors_rating + 1) * 0.5f * Dm.initial(this, (obj_machinery_space_heater_improvised_chem_heater p) => p.beaker_conduction_power), (1 * SECONDS) / Dm.num(our_subsystem_value.wait));
        return dot;
    }
}
