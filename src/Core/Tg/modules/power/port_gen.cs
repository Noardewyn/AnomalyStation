// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/port_gen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 2 (the placeholder power source, D162): the whole file. should_have_node and connect_to_network are from the owner's
// play test 2026-10-09 and M7 slice 1. The var blocks are generated (D111). The glow on the emissive plane is DEVIATION(DV12)
// (lighting planes and emissives are not ported); the generator's hum is tg's /datum/looping_sound/generator.

//Baseline portable generator. Has all the default handling. Not intended to be used on its own (since it generates unlimited power).
// tg: code/modules/power/port_gen.dm:2
public partial class obj_machinery_power_port_gen : obj_machinery_power
{
    // tg: code/modules/power/port_gen.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        soundloop = Dm.@new<datum_looping_sound_generator>(this, active);
        return dot;
    }

    // tg: code/modules/power/port_gen.dm:25
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref soundloop);
        return base.Destroy(force);
    }

    // tg: code/modules/power/port_gen.dm:29
    public override object should_have_node()
    {
        return anchored;
    }

    // tg: code/modules/power/port_gen.dm:32
    public override object connect_to_network()
    {
        object dot = null;
        if (!Dm.truthy(anchored))
            return FALSE;
        dot = base.connect_to_network();
        return dot;
    }

    // tg: code/modules/power/port_gen.dm:37
    public virtual object HasFuel() //Placeholder for fuel check.
    {
        return TRUE;
    }

    // tg: code/modules/power/port_gen.dm:40
    public virtual async DmTask UseFuel() //Placeholder for fuel use.
    {
        return;
    }

    // tg: code/modules/power/port_gen.dm:43
    public virtual async DmTask DropFuel()
    {
        return;
    }

    // tg: code/modules/power/port_gen.dm:46
    public virtual void handleInactive()
    {
        return;
    }

    // tg: code/modules/power/port_gen.dm:49
    public virtual void TogglePower()
    {
        if (Dm.truthy(active))
        {
            active = FALSE;
            update_appearance();
            soundloop.stop();
        }
        else if (Dm.truthy(HasFuel()))
        {
            active = TRUE;
            START_PROCESSING(SSmachines, this);
            update_appearance();
            soundloop.start();
        }
    }

    // tg: code/modules/power/port_gen.dm:60
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]", base_icon_state, active);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/power/port_gen.dm:64
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open) || !Dm.truthy(is_operational) || !Dm.truthy(active))
            return dot;

        // . += emissive_appearance(icon, light_mask, src, alpha = src.alpha): DEVIATION(DV12), the glow on the emissive plane
        return dot;
    }

    // tg: code/modules/power/port_gen.dm:71
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(active))
        {
            if (!Dm.truthy(HasFuel()) || !Dm.truthy(anchored))
            {
                TogglePower();
                return null;
            }
            if (Dm.truthy(powernet))
                add_avail(power_to_energy(power_gen * power_output));
            Waiting.NoSleep(UseFuel());
        }
        else
            handleInactive();
        return null;
    }

    // tg: code/modules/power/port_gen.dm:82
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, Dm.text("It is[]running.", !Dm.truthy(active) ? "n't " : " "));
        return dot;
    }

    // tg: code/modules/power/port_gen.dm:199
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(active))
            return NONE;
        toggle_panel_open();
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You [] the access panel.", Dm.truthy(panel_open) ? I18n.T("open") : I18n.T("close"))));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/port_gen.dm:207
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(active))
            return NONE;
        if (!Dm.truthy(anchored) && !Dm.truthy(isinspace()))
        {
            await set_anchored(TRUE);
            to_chat(user, span_notice(I18n.T("You secure the generator to the floor.")));
            return ITEM_INTERACT_SUCCESS;
        }

        await set_anchored(FALSE);
        to_chat(user, span_notice(I18n.T("You unsecure the generator from the floor.")));
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/port_gen.dm:220
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(active))
            return NONE;
        return await default_deconstruction_crowbar(user, tool);
    }
}

/////////////////
// P.A.C.M.A.N //
/////////////////
// tg: code/modules/power/port_gen.dm:90
public partial class obj_machinery_power_port_gen_pacman : obj_machinery_power_port_gen
{
    // tg: code/modules/power/port_gen.dm:101
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(anchored))
            connect_to_network();

        sheet_name = (string)Dm.initial(sheet_path, "name");
        return dot;
    }

    // tg: code/modules/power/port_gen.dm:109
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await DropFuel();
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/power/port_gen.dm:113
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        obj_item_circuitboard_machine_pacman our_board = circuit_value as obj_item_circuitboard_machine_pacman; // GAP(types): the board instance is circuit_value
        if (Dm.truthy(our_board.high_production_profile))
        {
            icon_state = "portgen1_0";
            base_icon_state = "portgen1";
            max_sheets = 20;
            time_per_sheet = 60;
            power_gen = 30 * KILO * JOULES;
            sheet_path = typeof(obj_item_stack_sheet_mineral_uranium);
        }
    }

    // tg: code/modules/power/port_gen.dm:123
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The generator has [] units of [] fuel left, producing [].", sheets, sheet_name, display_power(power_gen))));
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It is anchored to the ground.")));
        return dot;
    }

    // tg: code/modules/power/port_gen.dm:129
    public override object HasFuel()
    {
        if (Dm.num(sheets) >= 1 / (time_per_sheet / power_output) - sheet_left)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/power/port_gen.dm:134
    public override async DmTask DropFuel()
    {
        if (Dm.truthy(sheets))
        {
            Dm.@new(sheet_path, drop_location(), sheets);
            sheets = 0;
        }
    }

    // tg: code/modules/power/port_gen.dm:139
    public override async DmTask UseFuel()
    {
        float needed_sheets = 1 / (time_per_sheet / power_output);
        float temp = min(needed_sheets, sheet_left);
        needed_sheets -= temp;
        sheet_left -= temp;
        sheets -= round(needed_sheets);
        needed_sheets -= round(needed_sheets);
        if (sheet_left <= 0 && sheets > 0)
        {
            sheet_left = 1 - needed_sheets;
            sheets--;
        }

        float lower_limit = 56 + power_output * 10;
        float upper_limit = 76 + power_output * 10;
        float bias = 0;
        if (power_output > 4)
        {
            upper_limit = 400;
            bias = power_output - 3;
        }
        if (current_heat < lower_limit)
            current_heat += 3;
        else
        {
            current_heat += rand(-7 + bias, 7 + bias);
            if (current_heat < lower_limit)
                current_heat = lower_limit;
            if (current_heat > upper_limit)
                current_heat = upper_limit;
        }

        if (current_heat > 300)
        {
            await overheat();
            qdel(this);
        }
    }

    // tg: code/modules/power/port_gen.dm:169
    public override void handleInactive()
    {
        current_heat = max(current_heat - 2, 0);
        if (current_heat == 0)
            STOP_PROCESSING(SSmachines, this);
    }

    // tg: code/modules/power/port_gen.dm:174
    public virtual async DmTask overheat()
    {
        await explosion(this, devastation_range: 2, heavy_impact_range: 5, light_impact_range: 2, flash_range: -1);
    }

    // tg: code/modules/power/port_gen.dm:177
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = null;
        dot = await base.set_anchored(anchorvalue);
        if (dot == null)
            return dot; //no need to process if we didn't change anything.
        if (Dm.truthy(anchorvalue))
            connect_to_network();
        else
            disconnect_from_network();
        return dot;
    }

    // tg: code/modules/power/port_gen.dm:186
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, sheet_path))
            return await base.item_interaction(user, tool, modifiers, __extra);
        obj_item_stack addstack = tool as obj_item_stack;
        float amount = min((max_sheets - Dm.num(sheets)), Dm.num(addstack.amount));
        if (amount < 1)
        {
            to_chat(user, span_notice(I18n.T("\\The [] is full!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You add [] sheets to \\the [].", amount, this)));
        sheets += amount;
        await addstack.use(amount);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/port_gen.dm:225
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        Dm.combine(ref obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("maximum power output unlocked"));
        await emp_act(EMP_HEAVY);
        return TRUE;
    }

    // tg: code/modules/power/port_gen.dm:233
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return await interact(user);
    }

    // tg: code/modules/power/port_gen.dm:236
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await interact(user);
    }

    // tg: code/modules/power/port_gen.dm:239
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PortableGenerator", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/power/port_gen.dm:245
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["active"] = active;
        data["sheet_name"] = capitalize(sheet_name);
        data["sheets"] = sheets;
        data["stack_percent"] = round(sheet_left * 100, 0.1f);

        data["anchored"] = anchored;
        data["connected"] = (powernet == null ? 0 : 1);
        data["ready_to_boot"] = Dm.truthy(anchored) ? HasFuel() : anchored;
        data["power_generated"] = display_power(power_gen, convert: FALSE);
        data["power_output"] = display_power(power_gen * power_output, convert: FALSE);
        data["power_available"] = (powernet == null ? 0 : display_power(avail()));
        data["current_heat"] = current_heat;
        return data;
    }

    // tg: code/modules/power/port_gen.dm:262
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "toggle_power":
                TogglePower();
                dot = TRUE;
                break;

            case "eject":
                if (!Dm.truthy(active))
                {
                    await DropFuel();
                    dot = TRUE;
                }
                break;

            case "lower_power":
                if (power_output > 1)
                {
                    power_output--;
                    dot = TRUE;
                }
                break;

            case "higher_power":
                if (power_output < 4 || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                {
                    power_output++;
                    dot = TRUE;
                }
                break;
        }
        return dot;
    }
}
