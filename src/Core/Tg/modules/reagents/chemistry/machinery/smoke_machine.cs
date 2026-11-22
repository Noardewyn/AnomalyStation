// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/smoke_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps F1): the smoke machine and its SmokeMachine window. Its screentips (add_context) are a live-partial row
// (D264); the admin and combat logs of turning it on are leaves (logs, M10+).

// tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:4
public partial class obj_machinery_smoke_machine : obj_machinery
{
    /// Actual volume is REAGENTS_BASE_VOLUME plus REAGENTS_BASE_VOLUME * rating for each matterbin
    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:2  #define REAGENTS_BASE_VOLUME 100 (file-local)
    private const float REAGENTS_BASE_VOLUME = 100;

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        create_reagents(REAGENTS_BASE_VOLUME, INJECTABLE);

        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand), Dm.named("distinct_reagent_cap", 5)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));

        register_context();
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:43
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await (reagents as datum_reagents).expose(loc, TOUCH);
        await (reagents as datum_reagents).clear_reagents();
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:66
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        datum_reagents holder = reagents as datum_reagents;

        Dm.append(dot, span_notice(I18n.T("Reagent capacity <b>[]/[]</b>.", holder.total_volume, holder.maximum_volume)));
        Dm.append(dot, span_notice(I18n.T("Operating at <b>[]%</b> efficiency.", round((efficiency / 26) * 100))));

        Dm.append(dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT("screwed"), Dm.truthy(panel_open) ? "closed" : "open")));
        if (Dm.truthy(panel_open))
            Dm.append(dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT("pried"))));

        if (Dm.truthy(anchored))
            Dm.append(dot, span_notice(I18n.T("It can be [] loose.", EXAMINE_HINT("wrenched"))));
        else
            Dm.append(dot, span_warning(I18n.T("It needs to be [] in place to work.", EXAMINE_HINT("anchored"))));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:81
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(is_operational) || !Dm.truthy(on) || !Dm.truthy((reagents as datum_reagents)?.total_volume))
        {
            icon_state = Dm.text("[]0[]", base_icon_state, Dm.truthy(panel_open) ? "-o" : "");
            return await base.update_icon_state(__extra);
        }

        icon_state = Dm.text("[]1", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:89
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        //new capacity to store reagents from matter bins
        float new_volume = REAGENTS_BASE_VOLUME;
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            new_volume += REAGENTS_BASE_VOLUME * Dm.num(matter_bin.tier);
        (reagents as datum_reagents).maximum_volume = new_volume;

        //new efficiency from capacitors
        efficiency = 18;
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            efficiency += 2 * Dm.num(capacitor.tier);

        //new maximum range from servos
        max_range = 1;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            max_range += Dm.num(servo.tier);
        max_range = Dm.max(3, Dm.num(max_range));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:109
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(tool.flags_1, HOLOGRAM_1)) || !Dm.truthy(await user.can_perform_action(this, ALLOW_SILICON_REACH)))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        //transfer reagents from an open container into machine
        if (is_reagent_container(tool) && Dm.truthy(tool.is_open_container()))
        {
            obj_item_reagent_containers RC = tool as obj_item_reagent_containers;
            object units = await (RC.reagents as datum_reagents).trans_to(this, RC.amount_per_transfer_from_this, transferred_by: user);
            if (Dm.truthy(units))
            {
                to_chat(user, span_notice(I18n.T("You transfer [] units of the solution to [].", units, this)));
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:123
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
        {
            balloon_alert(user, I18n.T("turn off first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.eq(await default_unfasten_wrench(user, tool, time: 4 * SECONDS), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;

        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:133
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
        {
            balloon_alert(user, I18n.T("turn off first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:140
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
        {
            balloon_alert(user, I18n.T("turn off first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:147
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body()
    {
        datum_reagents holder = reagents as datum_reagents;
        if (!Dm.truthy(holder.total_volume) || !Dm.truthy(anchored) || !Dm.truthy(on) || !Dm.truthy(is_operational))
        {
            on = FALSE;
            update_appearance(UPDATE_ICON_STATE);
            return PROCESS_KILL;
        }

        turf location = get_turf(this);
        if (Dm.locate<obj_effect_particle_effect_fluid_smoke>(location) != null)
            return null;

        float smoke_amount = DIAMOND_AREA(Dm.num(setting) * 3);
        await do_chem_smoke(amount: smoke_amount, holder: this, location: location, carry: holder, carry_limit: 20, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_chem_smoke_machine));
        await holder.remove_all(smoke_amount / efficiency);
        await use_energy(Dm.num(active_power_usage) * (Dm.num(setting) / Dm.num(max_range)));
        update_appearance(UPDATE_ICON_STATE);
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:163
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "SmokeMachine", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:169
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        datum_reagents holder = reagents as datum_reagents;

        DmList tank_data = Dm.list();
        tank_data["maxVolume"] = holder.maximum_volume;
        tank_data["currentVolume"] = holder.total_volume;
        DmList tankContents = Dm.list();
        foreach (datum_reagent reagent in Dm.each<datum_reagent>(holder.reagent_list))
            Dm.append(ref tankContents, Dm.list(Dm.list(Dm.kv("name", reagent.name), Dm.kv("volume", round(Dm.num(reagent.volume), CHEMICAL_VOLUME_ROUNDING)))));
        tank_data["contents"] = tankContents;
        dot["tank"] = tank_data;

        dot["active"] = on;
        dot["setting"] = setting;
        dot["maxSetting"] = max_range;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/smoke_machine.dm:185
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        datum_reagents holder = reagents as datum_reagents;
        switch (action as string)
        {
            case "purge":
                await holder.clear_reagents();
                update_appearance(UPDATE_ICON_STATE);
                return TRUE;

            case "setting":
            {
                object amount = @params?["amount"];
                if (amount == null)
                    return FALSE;

                amount = text2num(amount);
                if (amount == null)
                    return FALSE;

                float value = Dm.num(amount);
                if (value >= 1 && value <= max_range) // amount in 1 to max_range
                {
                    setting = value;
                    return TRUE;
                }
                break;
            }

            case "power":
                on = Dm.truthy(on) ? FALSE : TRUE;
                if (Dm.truthy(on) && Dm.truthy(holder.total_volume))
                {
                    mob user = ui.user;
                    object english_list_text = english_list(holder.reagent_list);
                    message_admins(Dm.text("[] activated a smoke machine that contains [] at [].", ADMIN_LOOKUPFLW(user), english_list_text, ADMIN_VERBOSEJMP(this)));
                    user.log_message(Dm.text("activated a smoke machine that contains []", english_list_text), LOG_GAME);
                    log_combat(user, this, Dm.text("has activated [] which contains [] at [].", this, english_list_text, AREACOORD(this)));
                    begin_processing();
                }
                else
                {
                    on = FALSE;
                    end_processing();
                }
                update_appearance(UPDATE_ICON_STATE);
                return TRUE;
        }
        return dot;
    }
}
