// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/smes_portable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The connector is a
// SMES without cells of its own: a bank wrenched onto it lends it the cells of its component parts. The var blocks are generated.

// tg: code/modules/power/smes_portable.dm:3
public partial class obj_machinery_power_smes_connector : obj_machinery_power_smes
{
    // tg: code/modules/power/smes_portable.dm:17
    public override object Destroy(float? force = null)
    {
        if (connected_smes != null) // in the unlikely but possible case a SMES is connected and this explodes
            Waiting.NoSleep(connected_smes.disconnect_port());
        return base.Destroy(force);
    }

    // tg: code/modules/power/smes_portable.dm:21
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        connected_smes?.update_appearance(updates);
        return dot;
    }

    // tg: code/modules/power/smes_portable.dm:25
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (connected_smes == null)
            return dot;

        if (Dm.truthy(inputting))
            Dm.append(ref dot, "bp-c");
        else
        {
            if (Dm.num(total_charge()) > 0)
                Dm.append(ref dot, "bp-o");
            else
                Dm.append(ref dot, "bp-d");
        }
        return dot;
    }

    // tg: code/modules/power/smes_portable.dm:38
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        //happens if the terminal gets rped without a bank attached. No division by zero error
        if (!Dm.truthy(total_capacity))
            total_capacity = 1;
        return dot;
    }

    // tg: code/modules/power/smes_portable.dm:45
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(connector_free(user)))
            return ITEM_INTERACT_BLOCKING;
        return await base.wrench_act(user, tool);
    }

    // tg: code/modules/power/smes_portable.dm:50
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(connector_free(user)))
            return ITEM_INTERACT_BLOCKING;
        return await base.screwdriver_act(user, tool);
    }

    // tg: code/modules/power/smes_portable.dm:55
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(connector_free(user)))
            return ITEM_INTERACT_BLOCKING;
        return await base.crowbar_act(user, tool);
    }

    /**
     * Checks if the connector is free
     * Arguments
     *
     * * mob/living/user - the player doing the checking
     */
    // tg: code/modules/power/smes_portable.dm:66
    public virtual object connector_free(mob_living user = null)
    {
        if (connected_smes != null)
        {
            balloon_alert(user, I18n.T("disconnect SMES first!"));
            return FALSE;
        }
        return TRUE;
    }

    /**
     * Connects the power bank to the smes
     * Arguments
     *
     * * obj/machinery/power/smesbank/bank - the bank to connect
     */
    // tg: code/modules/power/smes_portable.dm:80
    public virtual void connect_smes(obj_machinery_smesbank bank = null)
    {
        connected_smes = bank;
        if (connected_smes != null)
        {
            total_capacity = 0;
            foreach (obj_item_stock_parts_power_store power_cell in Dm.each<obj_item_stock_parts_power_store>(connected_smes.component_parts))
            {
                Dm.append(ref component_parts, power_cell);
                total_capacity += Dm.num(power_cell.max_charge());
            }
        }
        else
        {
            inputting = FALSE;
            outputting = FALSE;
            total_capacity = 1;
            foreach (obj_item_stock_parts_power_store power_cell in Dm.each<obj_item_stock_parts_power_store>(component_parts))
                Dm.remove(ref component_parts, power_cell);
            SStgui.close_uis(this);
        }
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/power/smes_portable.dm:98
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (connected_smes == null)
        {
            balloon_alert(user, I18n.T("no power bank!"));
            return FALSE;
        }

        return await base.ui_interact(user, ui, __extra);
    }

    // tg: code/modules/power/smes_portable.dm:105
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        return connected_smes != null ? await base.ui_act(action, @params, ui, state) : FALSE;
    }
}

/// The actual portable part of the portable SMES system. Pretty useless without an actual connector.
// tg: code/modules/power/smes_portable.dm:110
public partial class obj_machinery_smesbank : obj_machinery
{
    // tg: code/modules/power/smes_portable.dm:125
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        ///Initial connection for mapload, We attempt to locate the connector but only connect to it after it has initialized
        if (Dm.truthy(mapload))
            connected_port = Dm.locate<obj_machinery_power_smes_connector>(loc);

        ///Initial charge
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

        register_context();
        return dot;
    }

    // tg: code/modules/power/smes_portable.dm:142
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();

        //we somehow located an deleted port or no port at all. clear out
        if (QDELETED(connected_port))
        {
            connected_port = null;
            return;
        }

        //connect to the port located during mapload
        obj_machinery_power_smes_connector possible_connector = connected_port;
        connected_port = null;
        if (!Dm.truthy(await connect_port(possible_connector)))
            return;
        connected_port.input_attempt = TRUE;
        connected_port.output_attempt = TRUE;
    }

    // tg: code/modules/power/smes_portable.dm:158
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        await set_anchored(FALSE);
    }

    // tg: code/modules/power/smes_portable.dm:161
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null)
            return dot;

        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]", connected_port != null ? "Disconnect" : "Connect");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] Panel", Dm.truthy(panel_open) ? "Close" : "Open");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR) && Dm.truthy(panel_open) && connected_port == null)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/power/smes_portable.dm:176
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("its maintenance panel can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(panel_open) ? I18n.T("closed") : I18n.T("open"))));
        if (connected_port != null)
            Dm.append(ref dot, span_notice(I18n.T("You need to [] from the port before deconstructing.", EXAMINE_HINT(I18n.T("unwrench")))));
        else
        {
            if (Dm.truthy(panel_open))
                Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));
            Dm.append(ref dot, span_notice(I18n.T("It should be [] onto a connector port to operate.", EXAMINE_HINT(I18n.T("wrenched")))));
        }
        return dot;
    }

    // tg: code/modules/power/smes_portable.dm:186
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(disconnect_port());
        return base.Destroy(force);
    }

    // tg: code/modules/power/smes_portable.dm:190
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open) || !Dm.truthy(is_operational))
            return dot;

        if (connected_port != null)
        {
            Dm.append(ref dot, Dm.text("smes-op[]", Dm.truthy(connected_port.outputting) ? 1 : 0));
            Dm.append(ref dot, Dm.text("smes-oc[]", Dm.truthy(connected_port.inputting) ? 1 : 0));
            object clevel = connected_port.chargedisplay();
            if (Dm.num(clevel) > 0)
                Dm.append(ref dot, Dm.text("smes-og[]", clevel));
        }
        return dot;
    }

    // tg: code/modules/power/smes_portable.dm:202
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (connected_port != null)
            await connected_port.interact(user);
        return dot;
    }

    // adapted from portable atmos connection code
    // tg: code/modules/power/smes_portable.dm:207
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item wrench = null)
    {
        if (connected_port != null)
        {
            if (!Dm.truthy(await wrench.use_tool(this, user, 8 * SECONDS)))
                return ITEM_INTERACT_BLOCKING;
            user.visible_message(
                I18n.T("[] disconnects [].", user, this),
                span_notice(I18n.T("You unfasten [] from [].", this, connected_port)),
                span_hear(I18n.T("You hear a ratchet.")));
            investigate_log(Dm.text("was disconnected from [] by [].", connected_port, key_name(user)), INVESTIGATE_ENGINE);
            await disconnect_port();
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS;
        }

        obj_machinery_power_smes_connector possible_connector = Dm.locate<obj_machinery_power_smes_connector>(loc);
        if (!Dm.truthy(await wrench.use_tool(this, user, 4 * SECONDS, extra_checks: Dm.@new<datum_callback>(this, nameof(connect_port), possible_connector, user, TRUE))))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await connect_port(possible_connector, user)))
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(
            I18n.T("[] connects [].", user, this),
            span_notice(I18n.T("You fasten [] to [].", this, possible_connector)),
            span_hear(I18n.T("You hear a ratchet.")));
        update_appearance(UPDATE_OVERLAYS);
        investigate_log(Dm.text("was connected to [] by [].", possible_connector, key_name(user)), INVESTIGATE_ENGINE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/power/smes_portable.dm:233
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]-o", base_icon_state) : Dm.str(base_icon_state);
        return dot;
    }

    // tg: code/modules/power/smes_portable.dm:237
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/power/smes_portable.dm:240
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (connected_port != null)
        {
            balloon_alert(user, I18n.T("disconnect from [] first!", connected_port));
            return ITEM_INTERACT_FAILURE;
        }

        return await default_deconstruction_crowbar(user, tool);
    }

    /**
     * Attempt to connect the portable SMES to a given connector. Adapted from portable atmos connection code.
     *
     * Arguments
     * * obj/machinery/power/smes/connector/possible_connector - the connector we are trying to link with
     * * mob/living/user - the mob trying to connect the port
     * * check_only - don't connect but check if we can
     */
    // tg: code/modules/power/smes_portable.dm:255
    public virtual async DmTask<object> connect_port(obj_machinery_power_smes_connector possible_connector = null, mob_living user = null, float? check_only = null)
    {
        check_only ??= FALSE;
        if (QDELETED(possible_connector))
        {
            if (Dm.truthy(user))
                balloon_alert(user, I18n.T("no connector!"));
            return FALSE;
        }

        //Make sure not already connected to something else
        if (Dm.truthy(possible_connector.panel_open))
        {
            if (Dm.truthy(user))
                balloon_alert(user, I18n.T("close connector panel!"));
            return FALSE;
        }

        //Perform the connection
        if (Dm.truthy(check_only))
            return TRUE;
        connected_port = possible_connector;
        connected_port.connect_smes(this);
        await set_anchored(TRUE);
        update_appearance(UPDATE_OVERLAYS);
        return TRUE;
    }

    /// Disconnects the portable SMES from its assigned connector, if it has any. Also adapted from portable atmos connection code.
    // tg: code/modules/power/smes_portable.dm:279
    public virtual async DmTask disconnect_port()
    {
        if (connected_port == null)
            return;

        connected_port.connect_smes(null);
        connected_port = null;
        await set_anchored(FALSE);
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/power/smes_portable.dm:290
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        ///Drain the charge
        float charge_adjust = STANDARD_BATTERY_CHARGE / Dm.num(severity);
        foreach (obj_item_stock_parts_power_store power_cell in Dm.each<obj_item_stock_parts_power_store>(component_parts))
        {
            charge_adjust -= Dm.num(await power_cell.use(charge_adjust, TRUE));
            if (!Dm.truthy(charge_adjust))
                break;
        }
        return dot;
    }
}
