// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/server.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD2, D333: the R&D consoles and servers): the R&D server that mines the station techweb's points
// while it works (SSresearch's fire), its status for the server control console, the EMP that halts it for a minute, the multitool
// that buffers its techweb; and the master server with the source code hard drive a traitor steals (four screws, a crowbar, six
// wires), whose loss halves the techweb's income. The space ninja's drain (mod/modules/modules_ninja.dm) has live-partial rows.
// The var block is generated (D111).

// tg: code/modules/research/server.dm:15
public partial class obj_machinery_rnd_server : obj_machinery_rnd
{
    // HDD_PANEL_CLOSED .. HDD_OVERLOADED and SERVER_NOMINAL_TEXT (server.dm:1-13), the file's own defines
    internal const float HDD_PANEL_CLOSED = 0;
    internal const float HDD_PANEL_OPEN = 1;
    internal const float HDD_PRIED = 2;
    internal const float HDD_CUT_LOOSE = 3;
    internal const float HDD_OVERLOADED = 4;
    internal const string SERVER_NOMINAL_TEXT = "Nominal";

    // tg: code/modules/research/server.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //servers handle techwebs differently as we are expected to be there to connect
        //every other machinery on-station.
        if (stored_research == null)
        {
            if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link)))) /* CONFIG_GET(flag/no_default_techweb_link) */
                stored_research = Dm.@new<datum_techweb>();
            else
            {
                datum_techweb science_web = Dm.locate<datum_techweb_science>(SSresearch.techwebs);
                Waiting.NoSleep(connect_techweb(science_web));
            }
        }
        Dm.combine(ref stored_research.techweb_servers, this);
        name = Dm.text("[] []", name, num2hex(rand(1, 65535), -1)); //gives us a random four-digit hex number as part of the name. Y'know, for fluff.
        return dot;
    }

    // tg: code/modules/research/server.dm:41
    public override object Destroy(float? force = null)
    {
        if (stored_research != null)
            Dm.remove(ref stored_research.techweb_servers, this);
        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link)))) /* CONFIG_GET(flag/no_default_techweb_link) */
        {
            qdel(stored_research); // QDEL_NULL(stored_research)
            stored_research = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/research/server.dm:48
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            icon_state = Dm.text("[]-off", base_icon_state);
        else if (Dm.truthy(panel_open))
            icon_state = Dm.text("[]-on", base_icon_state) + "_t";
        else
            // "working" will cover EMP'd, disabled, or just broken
            icon_state = Dm.text("[]-[]", base_icon_state, Dm.truthy(working) ? "on" : "halt");
        return dot;
    }

    // tg: code/modules/research/server.dm:58
    public override object power_change()
    {
        refresh_working();
        return base.power_change();
    }

    // tg: code/modules/research/server.dm:62
    public override async DmTask on_set_machine_stat(object old_value = null)
    {
        refresh_working();
        await base.on_set_machine_stat(old_value);
    }

    /// Checks if we should be working or not, and updates accordingly.
    // tg: code/modules/research/server.dm:67
    public virtual void refresh_working()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | EMPED)) || Dm.truthy(research_disabled))
            working = FALSE;
        else
            working = TRUE;

        update_current_power_usage();
        update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/modules/research/server.dm:76
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await set_machine_stat(Dm.bitor(machine_stat, EMPED));
        _addtimer(Dm.@new<datum_callback>(this, nameof(fix_emp)), 60 * SECONDS, file: "code/modules/research/server.dm", line: 82);
        refresh_working();
        return dot;
    }

    /// Callback to un-emp the server afetr some time.
    // tg: code/modules/research/server.dm:85
    public virtual async DmTask fix_emp()
    {
        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(EMPED)));
        refresh_working();
    }

    /// Toggles whether or not researched_disabled is, yknow, disabled
    // tg: code/modules/research/server.dm:90
    public virtual void toggle_disable(mob user = null)
    {
        research_disabled = Dm.truthy(research_disabled) ? FALSE : TRUE;
        user.log_message(Dm.text("[] []", Dm.truthy(research_disabled) ? "shut off" : "turned on", this), LOG_GAME);
        refresh_working();
    }

    /// Gets status text based on this server's status for the computer.
    // tg: code/modules/research/server.dm:96
    public virtual object get_status_text()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, EMPED)))
            return "O&F@I*$ - R3*&O$T R@U!R%D";
        else if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return I18n.T("Offline - Server Unpowered");
        else if (Dm.truthy(research_disabled))
            return I18n.T("Offline - Server Control Disabled");
        else if (!Dm.truthy(working))
            // If, for some reason, working is FALSE even though we're not emp'd or powerless,
            // We need something to update our working state - such as rebooting the server
            return I18n.T("Offline - Reboot Required");

        return SERVER_NOMINAL_TEXT;
    }

    // tg: code/modules/research/server.dm:110
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        if (stored_research == null)
            return null;
        (tool as obj_item_multitool).set_buffer(stored_research);
        balloon_alert(user, "saved to multitool buffer");
        return TRUE;
    }
}

/// Master R&D server. As long as this still exists and still holds the HDD for the theft objective, research points generate at normal speed. Destroy it or an antag steals the HDD? Half research speed.
// tg: code/modules/research/server.dm:118
public partial class obj_machinery_rnd_server_master : obj_machinery_rnd_server
{
    // tg: code/modules/research/server.dm:126
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = Dm.text("\\improper Master ") + name;
        desc = Dm.text("[]\n[]", desc, I18n.T("It looks incredibly resistant to damage!"));
        source_code_hdd = Dm.@new<obj_item_disk_computer_hdd_theft>(this);

        add_overlay("RD-server-objective-stripes");
        return dot;
    }

    // tg: code/modules/research/server.dm:134
    public override object Destroy(float? force = null)
    {
        qdel(source_code_hdd); // QDEL_NULL(source_code_hdd)
        source_code_hdd = null;
        return base.Destroy(force);
    }

    // tg: code/modules/research/server.dm:138
    public override object get_status_text()
    {
        object dot = base.get_status_text();
        // Give us a special message if we're nominal, but our hard drive is gone
        if (Dm.eq(dot, SERVER_NOMINAL_TEXT) && source_code_hdd == null)
            return Dm.text("<font color=orange>[]</font>", I18n.T("Nominal - Hard Drive Missing"));
        return dot;
    }

    // tg: code/modules/research/server.dm:144
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        if (Dm.eq(deconstruction_state, HDD_PANEL_CLOSED))
            Dm.append(ref dot, I18n.T("The front panel is closed. You can see some recesses which may have <b>screws</b>."));
        else if (Dm.eq(deconstruction_state, HDD_PANEL_OPEN))
            Dm.append(ref dot, I18n.T("The front panel is dangling open. The HDD is in a secure housing. Looks like you'll have to <b>pry</b> it loose."));
        else if (Dm.eq(deconstruction_state, HDD_PRIED))
            Dm.append(ref dot, I18n.T("The front panel is dangling open. The HDD has been pried from its housing. It is still connected by <b>wires</b>."));
        else if (Dm.eq(deconstruction_state, HDD_CUT_LOOSE))
            Dm.append(ref dot, I18n.T("The front panel is dangling open. All you can see inside are cut wires and mangled metal."));
        else if (Dm.eq(deconstruction_state, HDD_OVERLOADED))
            Dm.append(ref dot, I18n.T("The front panel is dangling open. The HDD inside is destroyed and the wires are all burned."));
        return dot;
    }

    // tg: code/modules/research/server.dm:159
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!Dm.truthy(tool.tool_behaviour))
            return await base.tool_act(user, tool, modifiers);
        // Only antags are given the training and knowledge to disassemble this thing.
        if (!Dm.truthy(user.is_antag()))
        {
            if (Dm.truthy(user.combat_mode))
                return ITEM_INTERACT_SKIP_TO_ATTACK;
            balloon_alert(user, "you can't find an obvious maintenance hatch!");
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.tool_act(user, tool, modifiers);
    }

    // tg: code/modules/research/server.dm:170
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (tool is not obj_item_disk_computer_hdd_theft)
            return NONE;
        if (Dm.eq(deconstruction_state, HDD_PANEL_CLOSED))
            balloon_alert(user, "you can't find a place to insert it!");
        else if (Dm.eq(deconstruction_state, HDD_PANEL_OPEN))
            balloon_alert(user, "you weren't trained to install this!");
        else if (Dm.eq(deconstruction_state, HDD_PRIED))
            balloon_alert(user, "the HDD housing is completely broken, it won't fit!");
        else if (Dm.eq(deconstruction_state, HDD_CUT_LOOSE))
            balloon_alert(user, "the HDD housing is completely broken and all the wires are cut!");
        else if (Dm.eq(deconstruction_state, HDD_OVERLOADED))
            balloon_alert(user, "the inside is scorched and all the wires are burned!");
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/research/server.dm:186
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(deconstruction_state, HDD_PANEL_CLOSED) || Dm.truthy(user.combat_mode))
            return NONE;

        to_chat(user, span_notice(I18n.T("You can see [] screw\\s. You start unscrewing []...", front_panel_screws, Dm.eq(front_panel_screws, 1) ? "it" : "them")));
        while (Dm.truthy(await tool.use_tool(this, user, 7.5f * SECONDS, volume: 100)))
        {
            front_panel_screws--;
            if (front_panel_screws > 0)
            {
                to_chat(user, span_notice(I18n.T("The screw breaks as you remove it. Only [] left...", front_panel_screws)));
                continue;
            }
            deconstruction_state = HDD_PANEL_OPEN;
            to_chat(user, span_notice(I18n.T("You remove the last screw from []'s front panel.", this)));
            add_overlay("RD-server-hdd-panel-open");
            break;
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/server.dm:202
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(deconstruction_state, HDD_PANEL_OPEN) || Dm.truthy(user.combat_mode))
            return FALSE;

        to_chat(user, span_notice(I18n.T("You can see [] in a secure housing behind the front panel. You begin to pry it loose...", source_code_hdd)));
        if (Dm.truthy(await tool.use_tool(this, user, 15 * SECONDS, volume: 100)))
        {
            to_chat(user, span_notice(I18n.T("You destroy the housing, prying [] free.", source_code_hdd)));
            deconstruction_state = HDD_PRIED;
        }
        return TRUE;
    }

    // tg: code/modules/research/server.dm:212
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.eq(deconstruction_state, HDD_PRIED) || Dm.truthy(user.combat_mode))
            return FALSE;

        to_chat(user, span_notice(I18n.T("There are [] wire\\s connected to []. You start cutting []...", hdd_wires, source_code_hdd, Dm.eq(hdd_wires, 1) ? "it" : "them")));
        while (Dm.truthy(await tool.use_tool(this, user, 7.5f * SECONDS, volume: 100)))
        {
            hdd_wires--;

            if (hdd_wires <= 0)
            {
                deconstruction_state = HDD_CUT_LOOSE;
                to_chat(user, span_notice(I18n.T("You cut the final wire and remove [].", source_code_hdd)));
                await try_put_in_hand(source_code_hdd, user);
                source_code_hdd = null;
                stored_research.income_modifier *= 0.5f;
                return TRUE;
            }
            to_chat(user, span_notice(I18n.T("You delicately cut the wire. [] wire\\s left...", hdd_wires)));
        }
        return TRUE;
    }

    // tg: code/modules/research/server.dm:230
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        // If the machine contains a source code HDD, destroying it will negatively impact research speed. Safest to log this.
        if (source_code_hdd != null)
        {
            // Destroyed with a hard drive inside = harm income
            stored_research.income_modifier *= 0.5f;
            // If there's a usr, this was likely a direct deconstruction of some sort. Extra logging info!
            if (usr != null)
            {
                mob user = usr;

                message_admins(Dm.text("[] deconstructed [].", ADMIN_LOOKUPFLW(user), ADMIN_JMP(this)));
                user.log_message(Dm.text("deconstructed [].", this), LOG_GAME);
                return await base.on_deconstruction(disassembled);
            }

            message_admins(Dm.text("[] has been deconstructed by an unknown user.", ADMIN_JMP(this)));
            log_game(Dm.text("[] has been deconstructed by an unknown user.", this));
        }

        return await base.on_deconstruction(disassembled);
    }

    /// Destroys the source_code_hdd if present and sets the machine state to overloaded, adding the panel open overlay if necessary.
    // tg: code/modules/research/server.dm:249
    public virtual void overload_source_code_hdd()
    {
        if (source_code_hdd != null)
        {
            qdel(source_code_hdd); // QDEL_NULL(source_code_hdd)
            source_code_hdd = null;
            // Overloaded = harm income
            stored_research.income_modifier *= 0.5f;
        }

        if (Dm.eq(deconstruction_state, HDD_PANEL_CLOSED))
            add_overlay("RD-server-hdd-panel-open");

        front_panel_screws = 0;
        hdd_wires = 0;
        deconstruction_state = HDD_OVERLOADED;
    }
}
