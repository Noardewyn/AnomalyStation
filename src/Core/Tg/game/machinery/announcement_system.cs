// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/announcement_system.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the announcements): the Automated Announcement System, its config entries and their window
// ("AutomatedAnnouncement"), its breakage, and aas_config_announce, which the machines use for their radio announcements. Left out
// with a comment: the admin log of an injected href and the game log of an edited line (the logs and the admin tools, M10). The
// var blocks are generated (D111) but for the headset (so the type's own overrides are here too, the Hand mode).

public partial class datum_controller_global_vars
{
    // tg: code/game/machinery/announcement_system.dm:1  GLOBAL_LIST_EMPTY(announcement_systems)
    public DmList announcement_systems;

    // tg: code/game/machinery/announcement_system.dm:1  (GLOBAL_MANAGED(announcement_systems, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalannouncement_systems()
    {
        announcement_systems = Dm.list();
        Dm.append(ref gvars_datum_init_order, "announcement_systems");
    }
}

// tg: code/game/machinery/announcement_system.dm:3
public partial class obj_machinery_announcement_system : obj_machinery
{
    ///The headset that we use for broadcasting
    // tg: code/game/machinery/announcement_system.dm:23
    public obj_item_radio_headset radio;

    // The type's overrides of inherited vars (the Hand mode, types-converter.md 3.2: the class declares radio)
    // tg: code/game/machinery/announcement_system.dm:3
    public obj_machinery_announcement_system()
    {
        density = 1;
        name = Dm.text("\\improper Automated Announcement System");
        desc = "An automated announcement system that handles minor announcements over the radio.";
        icon = Dm.resource("icons/obj/machines/telecomms.dmi");
        icon_state = "AAS_On";
        base_icon_state = "AAS";
        verb_say = "coldly states";
        verb_ask = "queries";
        verb_exclaim = "alarms";
        active_power_usage = 50;
        circuit = typeof(obj_item_circuitboard_machine_announcement_system);
    }

    // tg: code/game/machinery/announcement_system.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        config_entries = init_subtypes(typeof(datum_aas_config_entry), Dm.list());
        object dot = base.Initialize(mapload, __extra);
        radio = (obj_item_radio_headset)Dm.@new(radio_type, this);
        Dm.append(ref GLOB.announcement_systems, this);
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/announcement_system.dm:38
    public override object randomize_language_if_on_station()
    {
        return null;
    }

    // tg: code/game/machinery/announcement_system.dm:41
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[][]", base_icon_state, Dm.truthy(is_operational) && !Dm.truthy(Dm.bitand(machine_stat, EMPED)) ? "On" : "Off", Dm.truthy(panel_open) ? "_Open" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/announcement_system.dm:45
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        datum_aas_config_entry entry = Dm.locate<datum_aas_config_entry_arrival>(config_entries);
        if (entry != null && Dm.truthy(entry.enabled))
            Dm.append(ref dot, greenlight);

        entry = Dm.locate<datum_aas_config_entry_newhead>(config_entries);
        if (entry != null && Dm.truthy(entry.enabled))
            Dm.append(ref dot, pinklight);

        if (Dm.truthy(Dm.bitand(machine_stat, EMPED)))
            Dm.append(ref dot, errorlight);
        return dot;
    }

    // tg: code/game/machinery/announcement_system.dm:58
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref radio);
        QDEL_LAZYLIST(ref config_entries);
        Dm.remove(ref GLOB.announcement_systems, this); //"OH GOD WHY ARE THERE 100,000 LISTED ANNOUNCEMENT SYSTEMS?!!"
        return base.Destroy(force);
    }

    // tg: code/game/machinery/announcement_system.dm:64
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/announcement_system.dm:67
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/announcement_system.dm:70
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open) || !Dm.truthy(Dm.bitand(machine_stat, EMPED)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You reset []'s firmware.", this)));
        await set_machine_stat(Dm.bitand(machine_stat, ~EMPED));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    /// Does funny breakage stuff
    // tg: code/game/machinery/announcement_system.dm:79
    public virtual async DmTask act_up()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, EMPED)))
            return;
        await set_machine_stat(Dm.bitor(machine_stat, EMPED));
        update_appearance();
        foreach (datum_aas_config_entry config in Dm.each<datum_aas_config_entry>(config_entries))
            config.act_up();
    }

    // tg: code/game/machinery/announcement_system.dm:87
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, Dm.bitor(EMPED, BROKEN)))) && !Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            await act_up();
        return dot;
    }

    // tg: code/game/machinery/announcement_system.dm:92
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        await act_up();
        balloon_alert(user, I18n.T("announcement strings corrupted"));
        return TRUE;
    }

    // tg: code/game/machinery/announcement_system.dm:100
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AutomatedAnnouncement");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/announcement_system.dm:106
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList configs = Dm.list();
        foreach (datum_aas_config_entry config in Dm.each<datum_aas_config_entry>(config_entries))
        {
            Dm.append(ref configs, Dm.list(Dm.list(
                Dm.kv("name", config.name),
                Dm.kv("entryRef", REF(config)),
                Dm.kv("enabled", config.enabled),
                Dm.kv("modifiable", config.modifiable),
                Dm.kv("announcementLinesMap", config.announcement_lines_map),
                Dm.kv("generalTooltip", config.general_tooltip),
                Dm.kv("varsAndTooltipsMap", config.vars_and_tooltips_map))));
        }
        return Dm.list(Dm.kv("config_entries", configs));
    }

    // tg: code/game/machinery/announcement_system.dm:120
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["max_announcement_len"] = MAX_AAS_LENGTH;

        return data;
    }

    // tg: code/game/machinery/announcement_system.dm:127
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await ((mob)usr).can_perform_action(this, ALLOW_SILICON_REACH)))
            return dot;
        if (Dm.truthy(Dm.bitand(machine_stat, EMPED)))
        {
            visible_message(span_warning(I18n.T("[] buzzes.", this)), span_hear(I18n.T("You hear a faint buzz.")));
            playsound(loc, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, TRUE);
            return dot;
        }

        add_fingerprint(usr as mob);
        datum_aas_config_entry config = Dm.locate_ref(@params["entryRef"]) as datum_aas_config_entry;
        if (!Dm.@in(config, config_entries))
            config = null;
        if (config == null || !Dm.truthy(config.modifiable))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "Toggle"))
        {
            config.enabled = Dm.num(!Dm.truthy(config.enabled));
            if (Dm.@in(config.GetType(), Dm.list(typeof(datum_aas_config_entry_arrival), typeof(datum_aas_config_entry_newhead))))
                update_appearance();
        }
        else if (Dm.eq(sw__1, "Text"))
        {
            if (!Dm.@in(@params["lineKey"], config.announcement_lines_map))
            {
                message_admins(Dm.text("[] tried to set announcement line for nonexisting line in the [] for AAS. Probably href injection. Received line: []", ADMIN_LOOKUPFLW(usr), config.name, Dm.html_encode(@params["lineKey"])));
                log_game(Dm.text("[] tried to mess with AAS. For [] he tried to edit nonexistend []", key_name(usr), config.name, @params["lineKey"]));
                return dot;
            }
            object new_message = trim(Dm.html_encode(@params["newText"]), MAX_AAS_LENGTH);
            if (Dm.truthy(new_message))
            {
                config.announcement_lines_map[@params["lineKey"]] = new_message;
                usr.log_message(Dm.text("updated [] line in the [] to: []", @params["lineKey"], config.name, new_message), LOG_GAME);
            }
        }
        return dot;
    }

    // tg: code/game/machinery/announcement_system.dm:158
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        object dot = await base.can_interact(user, require_adjacent_turf);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(Dm.bitand(machine_stat, EMPED)))
        {
            to_chat(user, span_warning(I18n.T("[]'s firmware appears to be malfunctioning!", this)));
            if (!isAI(user))	// Deus Ex Machina goes without multitool in his default complectation.
                to_chat(user, span_warning(I18n.T("However, you can reset it with [], while its []!", EXAMINE_HINT("multitool"), EXAMINE_HINT("panel is open"))));
            return FALSE;
        }
        return dot;
    }

    /// If AAS can't broadcast message, it shouldn't be picked by randomizer.
    // tg: code/game/machinery/announcement_system.dm:170
    public virtual bool has_supported_channels(DmList channels = null)
    {
        if (!Dm.truthy(LAZYLEN(channels)) || Dm.@in(RADIO_CHANNEL_COMMON, channels))
        {
            // Okay, I am not proud of this, but I don't want CentCom or Syndie AASs to broadcast on Common.
            // Because our overrides can just change radio withour creating new subtype we prefer to check both.
            return GetType() == typeof(obj_machinery_announcement_system) && radio_type == typeof(obj_item_radio_headset_silicon_ai); // ::radio_type
        }
        foreach (object channel in Dm.each(channels))
        {
            if (Dm.truthy(radio.channels[channel]))
                return true;
        }
        return false;
    }

    /// Can AAS receive request for broadcast from you? Null source means yes.
    // tg: code/game/machinery/announcement_system.dm:181
    public virtual bool can_be_reached_from(object source = null)
    {
        if (!(source is atom source_atom))
            return true;
        turf source_turf = get_turf(source_atom);
        if (source_turf == null)
            return true;
        // Keep updated with broadcasting.dm (/datum/signal/subspace/vocal/New)
        // FFF (For Future Feature): think about adding radio relay support. Maybe implementing /datum/signal/subspace/aas_event or something similar.
        return Dm.@in(z, SSmapping.get_connected_levels(source_turf));
    }

    /// Compiles the announcement message with the provided variables. Announcement line is optional.
    // tg: code/game/machinery/announcement_system.dm:192
    public virtual object compile_config_message(Type aas_config_entry_type = null, DmList variables_map = null, object announcement_line = null, float? fail_if_disabled = null)
    {
        fail_if_disabled ??= FALSE;
        datum_aas_config_entry config = Dm.locate_type(aas_config_entry_type, config_entries) as datum_aas_config_entry;
        if (config == null || (Dm.truthy(fail_if_disabled) && !Dm.truthy(config.enabled)))
            return null;
        return config.compile_announce(variables_map, announcement_line);
    }

    /// Sends a message to the appropriate channels.
    // tg: code/game/machinery/announcement_system.dm:199
    public virtual async DmTask broadcast(object message = null, DmList channels = null, object command_span = null)
    {
        command_span ??= FALSE;
        await use_energy(active_power_usage);
        if (!Dm.truthy(LAZYLEN(channels)))
        {
            radio.talk_into(this, message, null, Dm.truthy(command_span) ? Dm.list(speech_span, SPAN_COMMAND) : null);
            return;
        }

        // For some reasons, radio can't recognize RADIO_CHANNEL_COMMON in channels, so we need to handle it separately.
        if (Dm.@in(RADIO_CHANNEL_COMMON, channels))
        {
            radio.talk_into(this, message, null, Dm.truthy(command_span) ? Dm.list(speech_span, SPAN_COMMAND) : null);
            Dm.remove(channels, RADIO_CHANNEL_COMMON);
        }
        foreach (object channel in Dm.each(channels))
            radio.talk_into(this, message, channel, Dm.truthy(command_span) ? Dm.list(speech_span, SPAN_COMMAND) : null);
    }

    /// Announces configs entry message with the provided variables. Channels, announcement_line and command_span are optional.
    // tg: code/game/machinery/announcement_system.dm:213
    public virtual async DmTask announce(Type aas_config_entry_type = null, DmList variables_map = null, DmList channels = null, object announcement_line = null, object command_span = null)
    {
        object msg = compile_config_message(aas_config_entry_type, variables_map, announcement_line, TRUE);
        if (Dm.truthy(msg))
            await broadcast(msg, channels, command_span);
    }
}

public static partial class GlobalProcs
{
    /// Returns a random announcement system that is operational, has the specified config entry, signal can reach source and radio supports any channel in list. All args are optional.
    // tg: code/game/machinery/announcement_system.dm:219
    public static obj_machinery_announcement_system get_announcement_system(Type aas_config_entry_type = null, object source = null, DmList channels = null)
    {
        if (GLOB.announcement_systems.len == 0)
            return null;
        DmList intact_aass = Dm.list();
        foreach (obj_machinery_announcement_system announce in Dm.each_cast<obj_machinery_announcement_system>(GLOB.announcement_systems))
        {
            if (!QDELETED(announce) && Dm.truthy(announce.is_operational) && announce.has_supported_channels(channels) && announce.can_be_reached_from(source))
            {
                if (aas_config_entry_type != null)
                {
                    datum_aas_config_entry entry = Dm.locate_type(aas_config_entry_type, announce.config_entries) as datum_aas_config_entry;
                    if (entry == null || !Dm.truthy(entry.enabled))
                        continue;
                }
                Dm.append(ref intact_aass, announce);
            }
        }
        return intact_aass.len > 0 ? Dm.pick(intact_aass) as obj_machinery_announcement_system : null;
    }

    /// Announces the provided message with the provided variables and config entry type. Only aas_config_entry_type and variables_map are mandatory. Other args are optional.
    // tg: code/game/machinery/announcement_system.dm:233
    public static async DmTask aas_config_announce(Type aas_config_entry_type = null, DmList variables_map = null, object source = null, DmList channels = null, object announcement_line = null, object command_span = null)
    {
        obj_machinery_announcement_system announcer = get_announcement_system(aas_config_entry_type, source, channels);
        if (announcer == null)
            return;
        await announcer.announce(aas_config_entry_type, variables_map, channels, announcement_line, command_span);
    }
}

// tg: code/game/machinery/announcement_system.dm:239
public partial class datum_aas_config_entry : datum
{
    /// Compiles the announcement message with the provided variables. Announcement line is optional, may be both index or line key.
    // tg: code/game/machinery/announcement_system.dm:253
    public virtual object compile_announce(DmList variables_map = null, object announcement_line = null)
    {
        object announcement_message = LAZYACCESS(announcement_lines_map, announcement_line);
        // If index was provided LAZYACCESS will return us a key, not value
        if (isnum(announcement_line))
            announcement_message = announcement_lines_map[announcement_message];
        // Fallback - first line
        if (!Dm.truthy(announcement_message))
            announcement_message = announcement_lines_map[announcement_lines_map[1]];
        // Replace variables with their value
        foreach (object variable in Dm.each(vars_and_tooltips_map))
            announcement_message = replacetext_char(announcement_message, Dm.text("%[]", variable), variables_map[variable] is var or__1 && Dm.truthy(or__1) ? or__1 : "[NO DATA]");
        return announcement_message;
    }

    /// Called when the announcement system is emagged or EMPed.
    // tg: code/game/machinery/announcement_system.dm:267
    [ShouldCallParent]
    public virtual bool act_up()
    {
        // Please do not mess with entries, that players can't fix.
        if (!Dm.truthy(modifiable))
            return true;
        return false;
    }
}

/*
	Global config entries for the announcement system.
*/

// tg: code/game/machinery/announcement_system.dm:279
public partial class datum_aas_config_entry_arrival : datum_aas_config_entry
{
    // tg: code/game/machinery/announcement_system.dm:288
    public override bool act_up()
    {
        bool dot = base.act_up();
        if (dot)
            return dot;

        announcement_lines_map["Message"] = Dm.pick("#!@%ERR-34%2 CANNOT LOCAT@# JO# F*LE!",
            "CRITICAL ERROR 99.",
            "ERR)#: DA#AB@#E NOT F(*ND!");
        return dot;
    }
}

// tg: code/game/machinery/announcement_system.dm:297
public partial class datum_aas_config_entry_newhead : datum_aas_config_entry
{
    // tg: code/game/machinery/announcement_system.dm:306
    public override bool act_up()
    {
        bool dot = base.act_up();
        if (dot)
            return dot;

        announcement_lines_map["Message"] = Dm.pick("OV#RL()D: [UNKNOWN??] DET*#CT)D!",
            "ER)#R - B*@ TEXT F*O(ND!",
            "AAS.exe is not responding. NanoOS is searching for a solution to the problem.");
        return dot;
    }
}

// tg: code/game/machinery/announcement_system.dm:315
public partial class datum_aas_config_entry_researched_node : datum_aas_config_entry
{
    // tg: code/game/machinery/announcement_system.dm:323
    public override bool act_up()
    {
        bool dot = base.act_up();
        if (dot)
            return dot;

        announcement_lines_map["Message"] = Dm.pick(
            replacetext(Dm.initial(typeof(datum_aas_config_entry_researched_node), (datum_aas_config_entry_researched_node p) => p.announcement_lines_map)["Message"], "%NODE", Dm.initial(typeof(datum_techweb_node_mech_clown), (datum_techweb_node_mech_clown p) => p.display_name)),
            "R/NT1M3 A= ANNOUN-*#nt_SY!?EM.dm, LI%£ 86: N=0DE NULL!",
            "BEPIS BEPIS BEPIS",
            "ERR)#R - B*@ TEXT F*O(ND!");
        return dot;
    }
}

// tg: code/game/machinery/announcement_system.dm:342 (declared here so init_subtypes creates it, DV25: the officers' arrival on the security channel; gaps J2)
public partial class datum_aas_config_entry_announce_officer : datum_aas_config_entry
{
}
