// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/smes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 2 (the power storage): the whole file but get_save_vars (admin variable editing and map saving, M10). The var block is
// generated (D111). The investigate logs and the admin messages stay deferred leaves (admin tooling, M10); the shock of a live
// cable under the terminal's installer is the M7 shocks slice's; the glow on the emissive plane is
// DEVIATION(DV12) (lighting planes and emissives are not ported). The SMES's charge lives in the power cells of its component
// parts (battery.dm, power_store.dm), as in tg.

// the SMES
// stores power

// tg: code/modules/power/smes.dm:5
public partial class obj_machinery_power_smes : obj_machinery_power
{
    // tg: code/modules/power/smes.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        //screentips
        register_context();

        ///initial charge
        if (Dm.truthy(charge))
        {
            foreach (obj_item_stock_parts_power_store power_cell in Dm.each<obj_item_stock_parts_power_store>(component_parts))
            {
                Waiting.NoSleep(power_cell.use(power_cell.charge__proc()));
                if (Dm.truthy(charge))
                    charge -= Dm.num(Waiting.NoSleep(power_cell.give(charge)));
            }
            charge = 0;
        }

        //locate terminal (dir_loop: the labelled break of tg's nested loops)
        bool terminal_found = false;
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            turf turf = get_step(this, Dm.num(direction));
            foreach (obj_machinery_power_terminal term in Dm.each<obj_machinery_power_terminal>(turf))
            {
                if (term != null && Dm.eq(term.dir, REVERSE_DIR(Dm.num(direction))))
                {
                    terminal = term;
                    terminal_found = true;
                    break;
                }
            }
            if (terminal_found)
                break;
        }
        if (terminal == null)
        {
            Waiting.NoSleep(atom_break());
            return dot;
        }
        terminal.master = this;
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/power/smes.dm:75
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        obj_item_stock_parts_power_store lowest_charged_part = null;
        foreach (obj_item_stock_parts_power_store power_cell in Dm.each<obj_item_stock_parts_power_store>(component_parts))
        {
            if (lowest_charged_part != null && (power_cell.charge >= lowest_charged_part.charge || power_cell.charge == power_cell.maxcharge))
                continue;
            lowest_charged_part = power_cell;
        }
        return lowest_charged_part;
    }

    // tg: code/modules/power/smes.dm:83
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        obj_structure_cable C = Dm.locate<obj_structure_cable>(loc);
        if (!QDELETED(C))
        {
            cable_layer = C.cable_layer;
            connect_to_network();
        }
    }

    // tg: code/modules/power/smes.dm:89
    public override async DmTask disconnect_terminal()
    {
        if (terminal != null)
        {
            terminal.master = null;
            terminal = null;
            await atom_break();
        }
    }

    // tg: code/modules/power/smes.dm:95
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(SSticker?.IsRoundInProgress()))
        {
            var turf = get_turf(this) as turf;
            message_admins(Dm.text("[] deleted at []", this, ADMIN_VERBOSEJMP(turf)));
            log_game(Dm.text("[] deleted at []", this, AREACOORD(turf)));
            investigate_log(Dm.text("deleted at []", AREACOORD(turf)), INVESTIGATE_ENGINE);
        }
        Waiting.NoSleep(disconnect_terminal());
        return base.Destroy(force);
    }

    // tg: code/modules/power/smes.dm:104
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null)
            return dot;

        if (istype(held_item, typeof(obj_item_stack_cable_coil)) && terminal == null && Dm.truthy(can_place_terminal(user as mob_living, held_item as obj_item_stack_cable_coil, TRUE)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Install terminal";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "Close" : "Open");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH) && Dm.truthy(panel_open))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Rotate";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_WIRECUTTER) && terminal != null && Dm.truthy(panel_open))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Cut terminal";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR) && terminal == null && Dm.truthy(panel_open))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/power/smes.dm:126
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);

        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(panel_open) ? I18n.T("closed") : I18n.T("opened"))));
        if (Dm.truthy(panel_open))
        {
            if (terminal == null)
                Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));
            Dm.append(ref dot, span_notice(I18n.T("It can [] to rotate.", EXAMINE_HINT(I18n.T("wrenched")))));
        }

        if (terminal == null)
            Dm.append(ref dot, span_warning(I18n.T("A terminal that requires [] needs to be installed!.", EXAMINE_HINT(I18n.T("10 cable pieces")))));
        else if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("The terminal can be [] apart.", EXAMINE_HINT(I18n.T("cut")))));
        return dot;
    }

    // tg: code/modules/power/smes.dm:140
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open) || !Dm.truthy(is_operational))
            return dot;

        if (Dm.truthy(show_display_lights))
        {
            Dm.append(ref dot, Dm.text("smes-op[]", Dm.truthy(outputting) ? 1 : 0));
            Dm.append(ref dot, Dm.text("smes-oc[]", Dm.truthy(inputting) ? 1 : 0));

            object clevel = chargedisplay();
            if (Dm.num(clevel) > 0)
                Dm.append(ref dot, Dm.text("smes-og[]", clevel));

            // . += emissive_appearance(icon, light_mask, src, alpha = src.alpha): DEVIATION(DV12), the glow on the emissive plane
        }
        return dot;
    }

    /// Returns the total charge of this smes
    // tg: code/modules/power/smes.dm:163
    public virtual object total_charge()
    {
        object dot = null;
        foreach (obj_item_stock_parts_power_store power_cell in Dm.each<obj_item_stock_parts_power_store>(component_parts))
            dot = Dm.add(dot, power_cell.charge__proc());
        return dot;
    }

    /**
     * Adjusts the total charge of this smes
     * Arguments
     *
     * * charge_adjust - the amount of give/take from this smes
     */
    // tg: code/modules/power/smes.dm:177
    public virtual async DmTask<object> adjust_charge(object charge_adjust = null)
    {
        object dot = null;
        bool give = Dm.num(charge_adjust) > 0;
        charge_adjust = Math.Abs(Dm.num(charge_adjust));
        foreach (obj_item_stock_parts_power_store power_cell in Dm.each<obj_item_stock_parts_power_store>(component_parts))
        {
            object amount_adjusted;
            if (give)
                amount_adjusted = await power_cell.give(charge_adjust);
            else
                amount_adjusted = await power_cell.use(charge_adjust, TRUE);

            dot = Dm.add(dot, amount_adjusted);
            charge_adjust = Dm.num(charge_adjust) - Dm.num(amount_adjusted);
            if (!Dm.truthy(charge_adjust))
                return dot;
        }
        return dot;
    }

    // tg: code/modules/power/smes.dm:192
    public override async DmTask<object> RefreshParts()
    {
        // SHOULD_CALL_PARENT(FALSE)

        float power_coefficient = 0;
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            power_coefficient += Dm.num(capacitor.tier);
        input_level_max = Dm.initial(this, (obj_machinery_power_smes s) => s.input_level_max) * power_coefficient;
        output_level_max = Dm.initial(this, (obj_machinery_power_smes s) => s.output_level_max) * power_coefficient;

        total_capacity = 0;
        foreach (obj_item_stock_parts_power_store power_cell in Dm.each<obj_item_stock_parts_power_store>(component_parts))
            total_capacity += Dm.num(power_cell.max_charge());

        await update_static_data_for_all_viewers();
        return null;
    }

    // tg: code/modules/power/smes.dm:207
    public override object should_have_node()
    {
        return TRUE;
    }

    /**
     * Can we place the terminal based on the players position
     *
     * Arguments
     * * mob/living/user - the player attempting to install the cable
     * * obj/item/stack/cable_coil/installing_cable - the cable coil used to install the terminal
     * * silent - should we display error messages
    */
    // tg: code/modules/power/smes.dm:218
    public virtual object can_place_terminal(mob_living user = null, obj_item_stack_cable_coil installing_cable = null, object silent = null)
    {
        silent ??= TRUE;
        // PRIVATE_PROC(TRUE)

        float set_dir = get_dir(user, this);
        if (Dm.truthy(Dm.bitand(set_dir, set_dir - 1)))//we don't want diagonal click
            return FALSE;

        turf terminal_turf = get_turf(user);
        if (!Dm.truthy(panel_open))
        {
            if (!Dm.truthy(silent) && user != null)
                balloon_alert(user, I18n.T("open the maintenance panel!"));
            return FALSE;
        }
        if (terminal_turf.underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
        {
            if (!Dm.truthy(silent) && user != null)
                balloon_alert(user, I18n.T("remove the floor plating!"));
            return FALSE;
        }
        if (terminal != null)
        {
            if (!Dm.truthy(silent) && user != null)
                balloon_alert(user, I18n.T("already wired!"));
            return FALSE;
        }
        if (Dm.num(installing_cable.get_amount()) < 10)
        {
            if (!Dm.truthy(silent) && user != null)
                balloon_alert(user, I18n.T("need ten lengths of cable!"));
            return FALSE;
        }
        return TRUE;
    }

    // adapted from APC item interacts for cable act handling
    // tg: code/modules/power/smes.dm:245
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = NONE;
        obj_item_stack_cable_coil installing_cable = tool as obj_item_stack_cable_coil; // the typed argument of tg's proc
        if (installing_cable != null)
        {
            dot = ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(can_place_terminal(user, installing_cable, FALSE)))
                return ITEM_INTERACT_BLOCKING;

            //select cable layer
            float terminal_cable_layer = installing_cable.target_layer;
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            {
                object choice = await tgui_input_list(user, I18n.T("Select Power Input Cable Layer"), I18n.T("Select Cable Layer"), GLOB.cable_name_to_layer);
                if (choice == null
                    || !Dm.truthy(user.is_holding(installing_cable))
                    || !Dm.truthy(await user.Adjacent(this))
                    || Dm.truthy(user.incapacitated)
                    || !Dm.truthy(can_place_terminal(user, installing_cable, TRUE))
                )
                    return ITEM_INTERACT_BLOCKING;
                terminal_cable_layer = Dm.num(GLOB.cable_name_to_layer[choice]);
            }
            user.visible_message(span_notice(I18n.T("[] starts adding cables to [].", user.name, this)));
            balloon_alert(user, I18n.T("adding cables..."));
            playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);

            //use cable
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(can_place_terminal(user, installing_cable, TRUE)))
                return ITEM_INTERACT_BLOCKING;
            obj_item_stack_cable_coil cable = installing_cable;
            turf turf = get_turf(user);
            obj_structure_cable connected_cable = turf.get_cable_node(terminal_cable_layer) as obj_structure_cable; //get the connecting node cable, if there's one
            if (prob(50) && Dm.truthy(await electrocute_mob(user as mob_living_carbon, connected_cable, connected_cable, 1, TRUE))) //animate the electrocution if uncautious and unlucky
            {
                do_sparks(5, TRUE, this);
                return ITEM_INTERACT_BLOCKING;
            }
            await cable.use(10);
            user.visible_message(span_notice(I18n.T("[] adds cables to [].", user.name, this)));
            balloon_alert(user, I18n.T("cables added"));

            //build the terminal and link it to the network
            terminal = Dm.@new<obj_machinery_power_terminal>(turf);
            terminal.master = this;
            terminal.cable_layer = terminal_cable_layer;
            await terminal.setDir(get_dir(turf, this));
            terminal.connect_to_network();
            await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/power/smes.dm:292
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]-o", base_icon_state) : (string)base_icon_state;
        return dot;
    }

    // tg: code/modules/power/smes.dm:296
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/power/smes.dm:299
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item item = null)
    {
        if (terminal != null && Dm.truthy(panel_open))
        {
            await terminal.dismantle(user, item);
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/power/smes.dm:305
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (terminal != null)
        {
            balloon_alert(user, I18n.T("remove the power terminal!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(await default_deconstruction_crowbar(user, tool)))
        {
            var ground = get_turf(this) as turf;
            message_admins(Dm.text("[] has been deconstructed by [] in [].", this, ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(ground)));
            user.log_message(Dm.text("deconstructed []", this), LOG_GAME);
            investigate_log(Dm.text("deconstructed by [] at [].", key_name(user), AREACOORD(this)), INVESTIGATE_ENGINE);
            return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_BLOCKING;
    }

    //changing direction using wrench
    // tg: code/modules/power/smes.dm:320
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_FAILURE;
        if (Dm.truthy(await default_change_direction_wrench(user, tool)))
        {
            await disconnect_terminal();
            foreach (obj_machinery_power_terminal term in Dm.each<obj_machinery_power_terminal>(get_step(this, dir)))
            {
                if (term != null && Dm.eq(term.dir, REVERSE_DIR(dir)))
                {
                    terminal = term;
                    terminal.master = this;
                    to_chat(user, span_notice(I18n.T("Terminal found.")));
                    await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
                    update_appearance(UPDATE_OVERLAYS);
                    return ITEM_INTERACT_SUCCESS;
                }
            }
            to_chat(user, span_alert(I18n.T("No power terminal found.")));
        }
        return dot;
    }

    // tg: code/modules/power/smes.dm:334
    public override async DmTask<object> cable_layer_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("open panel first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.cable_layer_act(user, tool);
    }

    // tg: code/modules/power/smes.dm:340
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.multitool_act(user, tool);
        if (Dm.eq(dot, ITEM_INTERACT_SUCCESS))
            connect_to_network();
        return dot;
    }

    ///Returns the charge level this smes is at 0->5 for display purposes
    // tg: code/modules/power/smes.dm:346
    public virtual object chargedisplay()
    {
        // SHOULD_BE_PURE(TRUE)

        return clamp(round(5 * Dm.num(Dm.div(total_charge(), total_capacity))), 0, 5);
    }

    // tg: code/modules/power/smes.dm:351
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(is_operational))
            return null;

        //store machine state to see if we need to update the icon overlays
        object last_disp = chargedisplay();
        float last_chrg = inputting;
        float last_onln = outputting;

        //outputting
        if (Dm.truthy(output_attempt) && Dm.truthy(powernet))
        {
            float output_energy = Dm.num(power_to_energy(output_level));

            output_used = 0;
            if (output_energy <= 0)
                outputting = FALSE;
            else
            {
                outputting = TRUE;

                // reduce the storage (may be recovered in /restore() if excessive)
                output_used = Dm.num(Waiting.NoSleep(adjust_charge(-output_energy)));
                if (Dm.truthy(output_used))
                {
                    add_avail(output_used);

                    // either from no charge or set to 0
                    if (output_used < 0.1f)
                    {
                        outputting = FALSE;
                        investigate_log("lost power and turned off", INVESTIGATE_ENGINE);
                    }
                }
                else
                    outputting = FALSE;
            }
        }
        else
            outputting = FALSE;

        //inputting
        if (Dm.truthy(input_attempt) && terminal != null)
        {
            float input_energy = Dm.num(power_to_energy(input_level));

            input_available = Dm.num(terminal.surplus());
            if (input_energy <= 0)
                inputting = FALSE;
            else
            {
                inputting = TRUE;

                // increase the charge
                float load = Dm.num(Waiting.NoSleep(adjust_charge(min(input_energy, input_available))));
                if (Dm.truthy(load))
                    terminal.add_load(load); // add the load to the terminal side network
                else
                    inputting = FALSE;
            }
        }
        else
            inputting = FALSE;

        // only update icon if state changed
        if (!Dm.eq(last_disp, chargedisplay()) || last_chrg != inputting || last_onln != outputting)
            update_appearance(UPDATE_OVERLAYS);
        return null;
    }

    // called after all power processes are finished
    // restores charge level to smes if there was excess this ptick
    // tg: code/modules/power/smes.dm:409
    public virtual async DmTask restore()
    {
        if (!Dm.truthy(is_operational))
            return;

        if (!Dm.truthy(outputting))
        {
            output_used = 0;
            return;
        }

        float excess = min(output_used, ((datum_powernet)powernet).netexcess); // clamp it to how much was actually output by this SMES last ptick

        // now recharge this amount
        object clev = chargedisplay();

        excess = Dm.num(await adjust_charge(excess)); // restore unused power
        ((datum_powernet)powernet).netexcess -= excess; // remove the excess from the powernet, so later SMESes don't try to use it

        output_used -= excess;

        if (!Dm.eq(clev, chargedisplay())) //if needed updates the icons overlay
            update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/power/smes.dm:430
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Smes", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/power/smes.dm:436
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("capacity", total_capacity),
            Dm.kv("inputLevelMax", input_level_max),
            Dm.kv("outputLevelMax", output_level_max)
        );
    }

    // tg: code/modules/power/smes.dm:443
    public override async DmTask<object> ui_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("charge", total_charge()),
            Dm.kv("inputAttempt", input_attempt),
            Dm.kv("inputting", inputting),
            Dm.kv("inputLevel", input_level),
            Dm.kv("inputAvailable", energy_to_power(input_available)),
            Dm.kv("outputAttempt", output_attempt),
            Dm.kv("outputLevel", output_level),
            Dm.kv("outputUsed", energy_to_power(output_used)),
            Dm.kv("outputting", outputting)
        );
    }

    // tg: code/modules/power/smes.dm:456
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "tryinput":
                input_attempt = Dm.truthy(input_attempt) ? FALSE : TRUE;
                log_smes(ui.user);
                update_appearance(UPDATE_OVERLAYS);
                return TRUE;

            case "tryoutput":
                output_attempt = Dm.truthy(output_attempt) ? FALSE : TRUE;
                log_smes(ui.user);
                update_appearance(UPDATE_OVERLAYS);
                return TRUE;

            case "input":
            {
                object target = @params["target"];
                float? adjust = text2num(@params["adjust"]);
                if (Dm.eq(target, "min"))
                {
                    target = 0;
                    dot = TRUE;
                }
                else if (Dm.eq(target, "max"))
                {
                    target = input_level_max;
                    dot = TRUE;
                }
                else if (Dm.truthy(adjust))
                {
                    target = input_level + Dm.num(adjust);
                    dot = TRUE;
                }
                else if (text2num(target) != null)
                {
                    target = text2num(target);
                    dot = TRUE;
                }
                if (Dm.truthy(dot))
                {
                    input_level = clamp(Dm.num(target), 0, input_level_max);
                    log_smes(ui.user);
                    return dot;
                }
                break;
            }

            case "output":
            {
                object target = @params["target"];
                float? adjust = text2num(@params["adjust"]);
                if (Dm.eq(target, "min"))
                {
                    target = 0;
                    dot = TRUE;
                }
                else if (Dm.eq(target, "max"))
                {
                    target = output_level_max;
                    dot = TRUE;
                }
                else if (Dm.truthy(adjust))
                {
                    target = output_level + Dm.num(adjust);
                    dot = TRUE;
                }
                else if (text2num(target) != null)
                {
                    target = text2num(target);
                    dot = TRUE;
                }
                if (Dm.truthy(dot))
                {
                    output_level = clamp(Dm.num(target), 0, output_level_max);
                    log_smes(ui.user);
                }
                break;
            }
        }
        return dot;
    }

    ///Logs the current state of this smes
    // tg: code/modules/power/smes.dm:514
    public virtual void log_smes(mob user = null)
    {
        // PRIVATE_PROC(TRUE)

        investigate_log(Dm.text("Input/Output: []/[] | Charge: [] | Output-mode: [] | Input-mode: [] by []", input_level, output_level, total_charge(), Dm.truthy(output_attempt) ? "ON" : "OFF", Dm.truthy(input_attempt) ? "AUTO" : "OFF", Dm.truthy(user) ? key_name(user) : "outside forces"), INVESTIGATE_ENGINE);
    }

    // tg: code/modules/power/smes.dm:519
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        input_attempt = rand(0, 1);
        inputting = input_attempt;
        output_attempt = rand(0, 1);
        outputting = output_attempt;
        output_level = rand(0, output_level_max);
        input_level = rand(0, input_level_max);
        await adjust_charge(-STANDARD_BATTERY_CHARGE / Dm.num(severity));
        update_appearance(UPDATE_OVERLAYS);
        log_smes();
        return dot;
    }
}

// tg: code/modules/power/smes.dm:556
public partial class obj_machinery_power_smes_magical : obj_machinery_power_smes
{
    // tg: code/modules/power/smes.dm:556
    public override async DmTask<object> adjust_charge(object charge_adjust = null)
    {
        //give charge without consuming anything
        if (Dm.num(charge_adjust) < 0)
            return Math.Abs(Dm.num(charge_adjust));
        //no point charging this already infinite smes
        return 0;
    }
}

// tg: code/modules/power/smes.dm:155  (interleaved, rules 1.9)
public partial class obj_machinery_power_smes : obj_machinery_power
{
    // tg: code/modules/power/smes.dm:155
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        charge = Dm.num(total_charge());
        Dm.append(ref dot, nameof(charge));
        Dm.append(ref dot, nameof(input_level));
        Dm.append(ref dot, nameof(output_level));
        return dot;
    }
}
