// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E12a (D287): the whole file. The portable electrolyzer runs on its power cell (or, wrenched down, on the area's
// APC) and electrolyses the air of its tile every SSair cycle (gas_mixture.electrolyze and electrolyzer_reactions.dm, live since
// the M7 zaps slice); the panel, the cell, the anchoring, the parts, the pictures and the Electrolyzer window. Deferred leaves:
// the examine is translated (X1); add_context is a live-partial stub (the screentips).
// The var block is generated but for cell (below).

// tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:1-2  (file-local, #undef at :236-237)
// ELECTROLYZER_MODE_STANDBY "standby", ELECTROLYZER_MODE_WORKING "working": the constants of the class below

// tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:4
public partial class obj_machinery_electrolyzer : obj_machinery
{
    private const string ELECTROLYZER_MODE_STANDBY = "standby";
    private const string ELECTROLYZER_MODE_WORKING = "working";

    ///used to check if there is a cell in the machine
    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:18
    // The generated field would be a Type (rules 3.2 V3: some other type assigns a typepath to a var named `cell`), which a power
    // cell instance does not fit: the hand-written member keeps tg's declared type, so the constructor with tg's overrides is here
    // (types-converter.md 3.1, "Hand"; as obj_machinery_light.cell).
    public obj_item_stock_parts_power_store cell;

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:4
    public obj_machinery_electrolyzer()
    {
        anchored = FALSE;
        density = TRUE;
        interaction_flags_machine = Dm.bitor(INTERACT_MACHINE_ALLOW_SILICON, INTERACT_MACHINE_OPEN);
        icon = Dm.resource("icons/obj/pipes_n_cables/atmos.dmi");
        icon_state = "electrolyzer-off";
        name = "electrolyzer";
        desc = "A portable electrolyzer, allowing for on-site production of Hydrogen. Warranty void if used by clowns.";
        max_integrity = 250;
        armor_type = typeof(datum_armor_machinery_electrolyzer);
        circuit = typeof(obj_item_circuitboard_machine_electrolyzer);
        use_power = NO_POWER_USE; // We don't use area power, we always use the cell
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:32
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:35
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // if(ispath(cell)) cell = new cell(src): the typed member never holds a path (no electrolyzer type sets one)
        SSair.start_processing_machine(this);
        update_appearance(UPDATE_ICON);
        register_context();
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:55
    public override object Destroy(float? force = null)
    {
        if (cell != null)
            QDEL_NULL(ref cell);
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:60
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (cell != null)
        {
            LAZYADD(ref component_parts, cell);
            cell = null;
        }
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:66
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("\\The [] is [], and the panel is [].", this, Dm.truthy(on) ? I18n.T("on") : I18n.T("off"), Dm.truthy(panel_open) ? I18n.T("open") : I18n.T("closed")));

        if (cell != null)
            Dm.append(ref dot, I18n.T("The charge meter reads []%.", cell != null ? round(Dm.num(cell.percent()), 1) : 0));
        else
            Dm.append(ref dot, I18n.T("There is no power cell installed."));
        if (in_range(user, this) && !isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("<b>Alt-click</b> to toggle [].", Dm.truthy(on) ? I18n.T("off") : I18n.T("on"))));
            Dm.append(ref dot, span_notice(I18n.T("<b>Anchor</b> it to drain power from the area's APC instead its internal power cell.")));
        }
        Dm.append(ref dot, span_notice(I18n.T("It will drain power from the [].", Dm.truthy(anchored) ? I18n.T("area's APC") : I18n.T("internal power cell"))));
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:80
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("electrolyzer-[]", Dm.truthy(on) ? Dm.text("[]", mode) : "off");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:84
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, "electrolyzer-open");
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:89
    public override object process_atmos(object seconds_per_tick = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process_atmos__body(dot), dot); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(DotBox<object> dot)
    {
        if (!Dm.truthy(is_operational) && Dm.truthy(on))
            on = FALSE;
        if (!Dm.truthy(on))
            return PROCESS_KILL;

        if ((cell == null || cell.charge <= 0) && !Dm.truthy(anchored))
        {
            on = FALSE;
            update_appearance(UPDATE_ICON);
            return PROCESS_KILL;
        }

        turf our_turf = loc as turf;
        if (our_turf == null)
        {
            if (!Dm.eq(mode, ELECTROLYZER_MODE_STANDBY))
            {
                mode = ELECTROLYZER_MODE_STANDBY;
                update_appearance(UPDATE_ICON);
            }
            return dot.Value;
        }

        string new_mode = Dm.truthy(on) ? ELECTROLYZER_MODE_WORKING : ELECTROLYZER_MODE_STANDBY; //change the mode to working if the machine is on

        if (!Dm.eq(mode, new_mode)) //check if the mode is set correctly
        {
            mode = new_mode;
            update_appearance(UPDATE_ICON);
        }

        if (Dm.eq(mode, ELECTROLYZER_MODE_STANDBY))
            return dot.Value;

        datum_gas_mixture env = await our_turf.return_air() as datum_gas_mixture; //get air from the turf

        if (env == null)
            return dot.Value;

        call_reactions(env);

        air_update_turf(FALSE, FALSE);

        float power_to_use = (5 * (3 * working_power) * working_power) / (efficiency + working_power);
        if (Dm.truthy(anchored))
            await use_energy(power_to_use);
        else
            await cell.use(power_to_use);
        return dot.Value;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:132
    public virtual void call_reactions(datum_gas_mixture env = null)
    {
        env.electrolyze(working_power: working_power);
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:135
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float power = 0;
        float cap = 0;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            power += Dm.num(servo.tier);
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            cap += Dm.num(capacitor.tier);

        working_power = power; //used in the amount of moles processed

        efficiency = (cap + 1) * 0.5f; //used in the amount of charge in power cell uses
        return dot;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:148
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 50);
        toggle_panel_open();
        balloon_alert(user, I18n.T("[] panel", Dm.truthy(panel_open) ? "opened" : "closed"));
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:155
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool); // . = ..()
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:160
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:163
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (!istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
            return NONE;

        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open panel!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (cell != null)
        {
            balloon_alert(user, I18n.T("cell inside!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        cell = tool as obj_item_stock_parts_power_store;
        tool.add_fingerprint(usr);
        balloon_alert(user, I18n.T("inserted cell"));
        SStgui.update_uis(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:185
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close panel!"));
            return CLICK_ACTION_BLOCKING;
        }
        toggle_power(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:192
    public virtual void toggle_power(mob user = null)
    {
        if (!Dm.truthy(anchored) && cell == null)
        {
            balloon_alert(user, I18n.T("insert cell or anchor!"));
            return;
        }
        on = Dm.truthy(on) ? FALSE : TRUE;
        mode = ELECTROLYZER_MODE_STANDBY;
        update_appearance(UPDATE_ICON);
        balloon_alert(user, I18n.T("turned []", Dm.truthy(on) ? "on" : "off"));
        if (Dm.truthy(on))
            SSair.start_processing_machine(this);
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:203
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:206
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Electrolyzer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:212
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["open"] = panel_open;
        data["on"] = on;
        data["hasPowercell"] = cell != null ? TRUE : FALSE;
        data["anchored"] = anchored;
        if (cell != null)
            data["powerLevel"] = round(Dm.num(cell.percent()), 1);
        return data;
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer.dm:222
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "power":
                toggle_power(ui.user);
                dot = TRUE;
                break;
            case "eject":
                if (Dm.truthy(panel_open) && cell != null)
                {
                    await cell.forceMove(drop_location() as atom);
                    cell = null;
                    dot = TRUE;
                }
                break;
        }
        return dot;
    }
}
