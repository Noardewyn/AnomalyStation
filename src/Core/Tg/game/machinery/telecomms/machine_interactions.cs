// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machine_interactions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// This file is separate from telecommunications.dm to isolate the implementation
// of basic interactions with the machines.

// The whole file (M9 slice D: telecomms): the machines' tools, their multitool window ("Telecomms": power, id, network, the
// filtered frequencies, the links and the multitool buffer) and the extra options of the bus, the relay and the server. Left out
// with a comment: the colour of a server's custom frequency (tgui_color_picker, the colour input window: not-translated.md 73),
// so a new custom frequency is not added and an edited one keeps its colour; the game log lines (the logs, M10); the AI's
// multitool (the AI, a later milestone). The var block is generated (D111).

// tg: code/game/machinery/telecomms/machine_interactions.dm:4
public partial class obj_machinery_telecomms : obj_machinery
{
    // tg: code/game/machinery/telecomms/machine_interactions.dm:18
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/telecomms/machine_interactions.dm:21
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await attack_hand(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/telecomms/machine_interactions.dm:25
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/telecomms/machine_interactions.dm:28
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Telecomms");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/telecomms/machine_interactions.dm:34
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        Dm.append(ref data, add_option());

        data["channels"] = get_channels();
        data["minfreq"] = MIN_FREE_FREQ;
        data["maxfreq"] = MAX_FREE_FREQ;
        data["frequency"] = tempfreq;

        obj_item_multitool heldmultitool = get_multitool(user);
        data["multitool"] = heldmultitool;

        if (heldmultitool != null)
            data["multibuff"] = heldmultitool.buffer;

        data["toggled"] = toggled;
        data["id"] = id;
        data["network"] = network;
        data["prefab"] = autolinkers.len > 0 ? TRUE : FALSE;

        DmList linked = Dm.list();
        float i = 0;
        data["linked"] = Dm.list();
        foreach (obj_machinery_telecomms machine in Dm.each<obj_machinery_telecomms>(links))
        {
            i++;
            if (Dm.truthy(machine.hide) && !Dm.truthy(hide))
                continue;
            DmList entry = Dm.list();
            entry["index"] = i;
            entry["name"] = machine.name;
            entry["id"] = machine.id;
            Dm.append(ref linked, Dm.list(entry));
        }
        data["linked"] = linked;

        DmList frequencies = Dm.list();
        data["frequencies"] = Dm.list();
        foreach (object x in Dm.each(freq_listening))
            Dm.append(ref frequencies, Dm.list(x));
        data["frequencies"] = frequencies;
        return data;
    }

    // tg: code/game/machinery/telecomms/machine_interactions.dm:76
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob_living current_user = usr as mob_living;
        if (!HAS_TRAIT(current_user, TRAIT_SILICON_ACCESS)) // !HAS_SILICON_ACCESS(current_user) (isAdminGhostAI: the admin tools, M10)
        {
            if (!(current_user.get_active_held_item() is obj_item_multitool))
                return dot;
        }

        obj_item_multitool heldmultitool = get_multitool(current_user);

        object sw__1 = action;
        if (Dm.eq(sw__1, "toggle"))
        {
            toggled = Dm.num(!Dm.truthy(toggled));
            update_power();
            update_appearance();
            current_user.log_message(Dm.text("toggled [] [].", Dm.truthy(toggled) ? "On" : "Off", this), LOG_GAME);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "id"))
        {
            if (Dm.truthy(@params["value"]))
            {
                if (Dm.num(length(@params["value"])) > 32)
                {
                    to_chat(current_user, span_warning(I18n.T("Error: Machine ID too long!")));
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
                    return dot;
                }
                else
                {
                    id = Dm.str(@params["value"]);
                    current_user.log_message(Dm.text("has changed the ID for [] to [].", this, id), LOG_GAME);
                    dot = TRUE;
                }
            }
        }
        else if (Dm.eq(sw__1, "network"))
        {
            if (Dm.truthy(@params["value"]))
            {
                if (Dm.num(length(@params["value"])) > 15)
                {
                    to_chat(current_user, span_warning(I18n.T("Error: Network name too long!")));
                    playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
                    return dot;
                }
                else
                {
                    foreach (obj_machinery_telecomms linked_machine in Dm.each<obj_machinery_telecomms>(links.Copy()))
                        remove_link(linked_machine);
                    network = Dm.str(@params["value"]);
                    links = Dm.list();
                    current_user.log_message(Dm.text("has changed the network for [] to [].", this, network), LOG_GAME);
                    dot = TRUE;
                }
            }
        }
        else if (Dm.eq(sw__1, "tempfreq"))
        {
            if (Dm.truthy(@params["value"]))
                tempfreq = Dm.num(text2num(@params["value"])) * 10;
        }
        else if (Dm.eq(sw__1, "freq"))
        {
            if (Dm.@in(tempfreq, banned_frequencies))
            {
                to_chat(current_user, span_warning(I18n.T("Error: Interference preventing filtering frequency: \"[] kHz\"", tempfreq / 10)));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
            }
            else
            {
                if (!Dm.@in(tempfreq, freq_listening))
                {
                    Dm.append(ref freq_listening, tempfreq);
                    current_user.log_message(Dm.text("added frequency [] for [].", tempfreq, this), LOG_GAME);
                    dot = TRUE;
                }
            }
        }
        else if (Dm.eq(sw__1, "delete"))
        {
            Dm.remove(freq_listening, @params["value"]);
            current_user.log_message(Dm.text("removed frequency [] for [].", @params["value"], this), LOG_GAME);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "unlink"))
        {
            obj_machinery_telecomms machine_to_unlink = links[text2num(@params["value"])] as obj_machinery_telecomms;
            if (machine_to_unlink != null)
                dot = remove_link(machine_to_unlink, current_user) ? TRUE : FALSE;
        }
        else if (Dm.eq(sw__1, "link"))
        {
            if (heldmultitool != null)
            {
                obj_machinery_telecomms machine_to_link = heldmultitool.buffer as obj_machinery_telecomms;
                dot = add_new_link(machine_to_link, current_user) ? TRUE : FALSE;
            }
        }
        else if (Dm.eq(sw__1, "buffer"))
        {
            heldmultitool.set_buffer(this);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "flush"))
        {
            heldmultitool.set_buffer(null);
            dot = TRUE;
        }

        await add_act(action, @params);
        dot = TRUE;
        return dot;
    }

    /// Adds new_connection to src's links list AND vice versa. Also updates `links_by_telecomms_type`.
    // tg: code/game/machinery/telecomms/machine_interactions.dm:153
    public virtual bool add_new_link(obj_machinery_telecomms new_connection = null, mob user = null)
    {
        if (new_connection == null || new_connection == this)
            return false;

        if (Dm.@in(new_connection, links) && Dm.@in(this, new_connection.links))
            return false;

        Dm.combine(links, new_connection);
        Dm.combine(new_connection.links, this);

        LAZYADDASSOCLIST(ref links_by_telecomms_type, new_connection.telecomms_type, new_connection);
        LAZYADDASSOCLIST(ref new_connection.links_by_telecomms_type, telecomms_type, this);

        if (user != null)
            user.log_message(Dm.text("linked [] for [].", this, new_connection), LOG_GAME);
        return true;
    }

    /// Removes old_connection from src's links list AND vice versa. Also updates `links_by_telecomms_type`.
    // tg: code/game/machinery/telecomms/machine_interactions.dm:171
    public virtual bool remove_link(obj_machinery_telecomms old_connection = null, mob user = null)
    {
        if (old_connection == null || old_connection == this)
            return false;

        if (Dm.@in(old_connection, links))
        {
            Dm.remove(links, old_connection);
            LAZYREMOVEASSOC(ref links_by_telecomms_type, old_connection.telecomms_type, old_connection);
        }

        if (Dm.@in(this, old_connection.links))
        {
            Dm.remove(old_connection.links, this);
            LAZYREMOVEASSOC(ref old_connection.links_by_telecomms_type, telecomms_type, this);
        }

        if (user != null)
            user.log_message(Dm.text("unlinked [] and [].", this, old_connection), LOG_GAME);

        return true;
    }

    /**
     *
     * Returns information (name and color) about all channels in machine's network
     */
    // tg: code/game/machinery/telecomms/machine_interactions.dm:192
    public virtual DmList get_channels()
    {
        DmList channels = Dm.list();
        foreach (object channel_freq in Dm.each(GLOB.reserved_radio_frequencies))
        {
            object channel_name = GLOB.reserved_radio_frequencies[channel_freq];
            Dm.append(ref channels, Dm.list(Dm.list(
                Dm.kv("freq", text2num(channel_freq)),
                Dm.kv("name", channel_name),
                Dm.kv("color", GLOB.reserved_radio_colors[channel_name]))));
        }

        obj_machinery_telecomms_hub hub = null;
        if (this is obj_machinery_telecomms_hub)
            hub = (obj_machinery_telecomms_hub)this;
        else
        {
            foreach (obj_machinery_telecomms link in Dm.each<obj_machinery_telecomms>(links))
            {
                if (link is obj_machinery_telecomms_hub)
                {
                    hub = (obj_machinery_telecomms_hub)link;
                    break;
                }
            }
        }

        if (hub != null)
        {
            foreach (obj_machinery_telecomms link in Dm.each<obj_machinery_telecomms>(hub.links))
            {
                if (link is obj_machinery_telecomms_server server)
                {
                    foreach (object freq_info_freq in Dm.each(server.frequency_infos))
                    {
                        DmList freq_info = server.frequency_infos[freq_info_freq] as DmList;
                        Dm.append(ref channels, Dm.list(Dm.list(
                            Dm.kv("freq", text2num(freq_info_freq)),
                            Dm.kv("name", freq_info["name"]),
                            Dm.kv("color", freq_info["color"]))));
                    }
                }
            }
        }
        else if (this is obj_machinery_telecomms_server server)
        {
            foreach (object freq_info_freq in Dm.each(server.frequency_infos))
            {
                DmList freq_info = server.frequency_infos[freq_info_freq] as DmList;
                Dm.append(ref channels, Dm.list(Dm.list(
                    Dm.kv("freq", text2num(freq_info_freq)),
                    Dm.kv("name", freq_info["name"]),
                    Dm.kv("color", freq_info["color"]))));
            }
        }

        return channels;
    }

    /**
     * Wrapper for adding additional options to a machine's interface.
     *
     * Returns a list, or `null` if it wasn't implemented by the machine.
     */
    // tg: code/game/machinery/telecomms/machine_interactions.dm:239
    public virtual DmList add_option()
    {
        return null;
    }

    /**
     * Wrapper for adding another time of action for `ui_act()`, rather than
     * having you override `ui_act` yourself.
     *
     * Returns `TRUE` if the action was handled, nothing if not.
     */
    // tg: code/game/machinery/telecomms/machine_interactions.dm:276
    public virtual async DmTask<object> add_act(object action = null, DmList @params = null)
    {
        return null;
    }

    /// Returns a multitool from a user depending on their mobtype.
    // tg: code/game/machinery/telecomms/machine_interactions.dm:348
    public virtual obj_item_multitool get_multitool(mob user = null)
    {
        if (isAI(user))
        {
            var U = (mob_living_silicon_ai)user;
            return U.aiMulti;
        }

        obj_item held_item = user.get_active_held_item() as obj_item;
        if (QDELETED(held_item))
            return null;
        held_item = held_item.get_proxy_attacker_for(this, user) as obj_item; //for borgs omni tool
        if (!Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
            return null;

        if (!HAS_TRAIT(user, TRAIT_SILICON_ACCESS)) // !HAS_SILICON_ACCESS(user) (isAdminGhostAI: the admin tools, M10)
            return held_item as obj_item_multitool;
        if (iscyborg(user) && in_range(user, this))
            return held_item as obj_item_multitool;
        return null;
    }
}

// tg: code/game/machinery/telecomms/machine_interactions.dm:242
public partial class obj_machinery_telecomms_bus : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machine_interactions.dm:242
    public override DmList add_option()
    {
        DmList data = Dm.list();
        data["type"] = "bus";
        data["changefrequency"] = change_frequency;
        return data;
    }

    // tg: code/game/machinery/telecomms/machine_interactions.dm:288
    public override async DmTask<object> add_act(object action = null, DmList @params = null)
    {
        object dot = null;
        if (Dm.eq(action, "change_freq"))
        {
            float newfreq = Dm.num(text2num(@params["value"])) * 10;
            if (newfreq != 0)
            {
                if (newfreq < 10000)
                {
                    change_frequency = newfreq;
                    dot = TRUE;
                }
                else
                    change_frequency = 0;
            }
        }
        return dot;
    }
}

// tg: code/game/machinery/telecomms/machine_interactions.dm:248
public partial class obj_machinery_telecomms_relay : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machine_interactions.dm:248
    public override DmList add_option()
    {
        DmList data = Dm.list();
        data["type"] = "relay";
        data["broadcasting"] = broadcasting;
        data["receiving"] = receiving;
        return data;
    }

    // tg: code/game/machinery/telecomms/machine_interactions.dm:279
    public override async DmTask<object> add_act(object action = null, DmList @params = null)
    {
        object dot = null;
        object sw__1 = action;
        if (Dm.eq(sw__1, "broadcast"))
        {
            broadcasting = Dm.num(!Dm.truthy(broadcasting));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "receive"))
        {
            receiving = Dm.num(!Dm.truthy(receiving));
            dot = TRUE;
        }
        return dot;
    }
}

// tg: code/game/machinery/telecomms/machine_interactions.dm:255
public partial class obj_machinery_telecomms_server : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machine_interactions.dm:255
    public override DmList add_option()
    {
        DmList data = Dm.list();
        data["type"] = "server";
        DmList infos = Dm.list();
        foreach (object freq_info_freq in Dm.each(frequency_infos))
        {
            DmList freq_info = frequency_infos[freq_info_freq] as DmList;
            Dm.append(ref infos, Dm.list(Dm.list(
                Dm.kv("frequency", freq_info_freq),
                Dm.kv("name", freq_info["name"]),
                Dm.kv("color", freq_info["color"]))));
        }

        data["frequencyinfos"] = infos;
        return data;
    }

    // tg: code/game/machinery/telecomms/machine_interactions.dm:299
    public override async DmTask<object> add_act(object action = null, DmList @params = null)
    {
        object dot = null;
        object sw__1 = action;
        if (Dm.eq(sw__1, "delete"))
        {
            Dm.remove(frequency_infos, num2text(Dm.num(@params["value"])));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "modify_freq_info"))
        {
            object freq = @params["freq"];
            DmList info = frequency_infos[freq] as DmList;
            if (info != null)
            {
                object new_name = await tgui_input_text(usr, I18n.T("Please enter new frequency name"), I18n.T("Modifying Frequency Information"), info["name"], MAX_NAME_LEN);
                if (Dm.truthy(new_name))
                {
                    foreach (DmList channel in Dm.each<DmList>(get_channels()))
                    {
                        if (!Dm.eq(num2text(Dm.num(channel["freq"])), freq) && Dm.eq(channel["name"], new_name))
                            return dot;
                    }
                }
                info["name"] = new_name;
                // No color changing for channels with theme settings
                if (!Dm.truthy(GLOB.freqtospan[Dm.text("[]", freq)]))
                {
                    // var/new_color = tgui_color_picker(usr, "Choose color for frequency", "Modifying Frequency Information", info["color"]):
                    // the colour input window (not-translated.md 73); no colour is picked, so the colour stays
                    Dm.NotTranslated("code/game/machinery/telecomms/machine_interactions.dm:316", "add_act: tgui_color_picker, the colour of an edited custom frequency (the colour input window)");
                }
                frequency_infos[@params["freq"]] = info;
                dot = TRUE;
            }
        }
        else if (Dm.eq(sw__1, "add_freq_info"))
        {
            object freq = await tgui_input_number(usr, I18n.T("Please enter frequency"), I18n.T("Adding Frequency Information"), 145.9f, 160, 120, round_value: FALSE);
            if (!Dm.truthy(freq))
                return dot;
            freq = Dm.round(Dm.num(freq) * 10);
            if (!Dm.@in(freq, freq_listening))
                return dot;
            object name = await tgui_input_text(usr, I18n.T("Please enter frequency name"), I18n.T("Adding Frequency Information"), max_length: MAX_NAME_LEN);
            if (!Dm.truthy(name))
                return dot;

            foreach (DmList channel in Dm.each<DmList>(get_channels()))
            {
                if (Dm.eq(channel["freq"], freq) || Dm.eq(channel["name"], name))
                    return dot;
            }
            // var/color = tgui_color_picker(usr, "Choose color for frequency", "Adding Frequency Information"); if(!color) return:
            // the colour input window (not-translated.md 73); without a colour tg adds nothing
            Dm.NotTranslated("code/game/machinery/telecomms/machine_interactions.dm:335", "add_act: tgui_color_picker, the colour of a new custom frequency (the colour input window)");
            return dot;
        }
        else if (Dm.eq(sw__1, "delete_freq_info"))
        {
            Dm.remove(frequency_infos, @params["freq"]);
            dot = TRUE;
        }
        return dot;
    }
}
