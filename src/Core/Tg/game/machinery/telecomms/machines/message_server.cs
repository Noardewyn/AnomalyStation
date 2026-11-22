// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/machines/message_server.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: the PDA messages' server): the blackbox recorder and its blackbox, the messaging server that logs
// and routes the PDA messages, its calibration and preset password, the messaging signals and the server's log datums; the
// request consoles' messages (/datum/signal/subspace/messaging/rc's broadcast) followed in gaps SC1. The var blocks are
// generated (D111).

// A decorational representation of SSblackbox, usually placed alongside the message server. Also contains a traitor theft item.
// tg: code/game/machinery/telecomms/machines/message_server.dm:2
public partial class obj_machinery_blackbox_recorder : obj_machinery
{
    // tg: code/game/machinery/telecomms/machines/message_server.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        stored = Dm.@new<obj_item_blackbox>(this);
        return dot;
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:23
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (stored != null)
        {
            await stored.forceMove(drop_location() as atom);
            if (Dm.truthy(await Adjacent(user)))
                await user.put_in_hands(stored);
            stored = null;
            to_chat(user, span_notice(I18n.T("You remove the blackbox from []. The tapes stop spinning.", this)));
            update_appearance();
            return dot;
        }
        else
        {
            to_chat(user, span_warning(I18n.T("It seems that the blackbox is missing...")));
            return dot;
        }
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:37
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_blackbox))
            return NONE;
        if (stored != null)
        {
            to_chat(user, span_warning(I18n.T("There's already a blackbox in \\the [].", this))); //something's gone wrong to get here, but you know, it could happen
            return ITEM_INTERACT_BLOCKING;
        }
        if (HAS_TRAIT(tool, TRAIT_NODROP) || !Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(span_notice(I18n.T("[] clicks [] into []!", user, tool, this)),
            span_notice(I18n.T("You press the device into [], and it clicks into place. The tapes begin spinning again.", this)));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        stored = tool;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:53
    public override object Destroy(float? force = null)
    {
        if (stored != null)
        {
            Waiting.NoSleep(stored.forceMove(loc));
            Dm.@new<obj_effect_decal_cleanable_blood_oil>(loc);
        }
        return base.Destroy(force);
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:59
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("blackbox[]", stored != null ? null : "_b");
        return await base.update_icon_state(__extra);
    }
}

/**
 * The equivalent of the server, for PDA and request console messages.
 * Without it, PDA and request console messages cannot be transmitted.
 * PDAs require the rest of the telecomms setup, but request consoles only
 * require the message server.
 */
// tg: code/game/machinery/telecomms/machines/message_server.dm:81
public partial class obj_machinery_telecomms_message_server : obj_machinery_telecomms
{
    // tg: code/game/machinery/telecomms/machines/message_server.dm:104  #define MESSAGE_SERVER_FUNCTIONING_MESSAGE (file-local)
    private const string MESSAGE_SERVER_FUNCTIONING_MESSAGE = "This is an automated message. The messaging system is functioning correctly.";

    // tg: code/game/machinery/telecomms/machines/message_server.dm:106
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(calibrating))
        {
            calibrating += Dm.num(world.time);
            Waiting.Detach(say(I18n.T("Calibrating... Estimated wait time: [] minutes.", Dm.rand(3, 9))));
            Dm.append(ref pda_msgs, Dm.@new<datum_data_tablet_msg>("System Administrator", "system", I18n.T("This is an automated message. System calibration started at [] (PT: []).", server_timestamp(ic_time: TRUE), round_timestamp())));
        }
        else
            Dm.append(ref pda_msgs, Dm.@new<datum_data_tablet_msg>("System Administrator", "system", I18n.T(MESSAGE_SERVER_FUNCTIONING_MESSAGE)));
        return dot;
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:115
    public override object Destroy(float? force = null)
    {
        foreach (obj_machinery_computer_message_monitor monitor in Dm.each<obj_machinery_computer_message_monitor>(listening_computers?.Copy()))
            monitor.set_linked_server(null);
        listening_computers = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:121
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(calibrating))
            Dm.append(ref dot, span_warning(I18n.T("It's still calibrating.")));
        return dot;
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:126
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        if (Dm.truthy(calibrating) && calibrating <= Dm.num(world.time))
        {
            calibrating = 0;
            Dm.append(ref pda_msgs, Dm.@new<datum_data_tablet_msg>("System Administrator", "system", I18n.T(MESSAGE_SERVER_FUNCTIONING_MESSAGE)));
        }
        return dot;
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:134
    public override async DmTask receive_information(datum_signal signal_in = null, obj_machinery_telecomms machine_from = null)
    {
        // can't log non-message signals
        if (!(signal_in is datum_signal_subspace_messaging signal) || !Dm.truthy(signal.data["message"]) || !Dm.truthy(on) || Dm.truthy(calibrating))
            return;

        // log the signal
        if (signal is datum_signal_subspace_messaging_tablet_message PDAsignal)
        {
            datum_data_tablet_msg log_message = Dm.@new<datum_data_tablet_msg>(PDAsignal.format_target(), PDAsignal.format_sender(), PDAsignal.format_message(), PDAsignal.format_photo_path());
            Dm.append(ref pda_msgs, log_message);
        }
        else if (signal is datum_signal_subspace_messaging_rc)
        {
            datum_data_rc_msg msg = Dm.@new<datum_data_rc_msg>(signal.data["receiving_department"], signal.data["sender_department"], signal.data["message"], signal.data["stamped"], signal.data["verified"], signal.data["priority"]);
            if (Dm.truthy(signal.data["sender_department"])) // don't log messages not from a department but allow them to work
                Dm.append(ref rc_msgs, msg);
        }
        signal.data["reject"] = FALSE;

        // pass it along to either the hub or the broadcaster
        if (!Dm.truthy(await relay_information(signal, typeof(obj_machinery_telecomms_hub))))
            await relay_information(signal, typeof(obj_machinery_telecomms_broadcaster));
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:154
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(calibrating))
            Dm.append(ref dot, "message_server_calibrate");
        return dot;
    }
}

// Preset messaging server
// tg: code/game/machinery/telecomms/machines/message_server.dm:161
public partial class obj_machinery_telecomms_message_server_preset : obj_machinery_telecomms_message_server
{
    // tg: code/game/machinery/telecomms/machines/message_server.dm:169
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Just in case there are multiple preset messageservers somehow once the CE arrives,
        // we want those on the station to share the same preset default decrypt key shown in his memories.
        bool is_on_station = Dm.truthy(is_station_level(z));
        if (is_on_station && Dm.truthy(GLOB.preset_station_message_server_key))
        {
            decryptkey = Dm.str(GLOB.preset_station_message_server_key);
            return dot;
        }
        //Generate a random password for the message server
        decryptkey = Dm.str(pick("the", "if", "of", "as", "in", "a", "you", "from", "to", "an", "too", "little", "snow", "dead", "drunk", "rosebud", "duck", "al", "le"));
        decryptkey += Dm.str(pick("diamond", "beer", "mushroom", "assistant", "clown", "captain", "twinkie", "security", "nuke", "small", "big", "escape", "yellow", "gloves", "monkey", "engine", "nuclear", "ai"));
        decryptkey += Dm.text("[]", Dm.rand(0, 9));
        if (is_on_station)
            GLOB.preset_station_message_server_key = decryptkey;
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/machinery/telecomms/machines/message_server.dm:167  GLOBAL_VAR(preset_station_message_server_key)
    public object preset_station_message_server_key;

    // tg: code/game/machinery/telecomms/machines/message_server.dm:167  (GLOBAL_UNMANAGED(preset_station_message_server_key), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalpreset_station_message_server_key()
    {
        return;
    }
}

// Root messaging signal datum
// tg: code/game/machinery/telecomms/machines/message_server.dm:186
public partial class datum_signal_subspace_messaging : datum_signal_subspace
{
    // tg: code/game/machinery/telecomms/machines/message_server.dm:189
    [DmParams("init_source", "init_data")]
    public override async DmTask New(params object[] __extra)
    {
        source = Dm.arg(__extra, 0) as obj;
        data = Dm.arg(__extra, 1) as DmList;
        turf origin_turf = get_turf(source);
        levels = SSmapping.get_connected_levels(origin_turf) as DmList;
        if (!Dm.@in("reject", data))
            data["reject"] = TRUE;
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:197
    public override datum_signal_subspace copy()
    {
        datum_signal_subspace_messaging copy = Dm.@new(GetType(), source, data.Copy()) as datum_signal_subspace_messaging;
        copy.original = this;
        copy.levels = levels;
        return copy;
    }
}

// Tablet message signal datum
// tg: code/game/machinery/telecomms/machines/message_server.dm:205 (interleaved types, rules 1.9)
public partial class datum_signal_subspace_messaging_tablet_message : datum_signal_subspace_messaging
{
    /// Returns a string representing the target of this message, formatted properly.
    // tg: code/game/machinery/telecomms/machines/message_server.dm:205
    public virtual object format_target()
    {
        if (Dm.truthy(data["everyone"]))
            return "Everyone";

        datum_computer_file_program_messenger target_app = (data["targets"] as DmList)[1] as datum_computer_file_program_messenger;
        obj_item_modular_computer target = target_app.computer;
        return STRINGIFY_PDA_TARGET(target.saved_identification, target.saved_job);
    }

    /// Returns a string representing the sender of this message, formatted properly.
    // tg: code/game/machinery/telecomms/machines/message_server.dm:214
    public virtual object format_sender()
    {
        object display_name = get_messenger_name(Dm.locate_ref(data["ref"]) as datum_computer_file_program_messenger);
        return Dm.truthy(display_name) ? display_name : STRINGIFY_PDA_TARGET(data["fakename"], data["fakejob"]);
    }

    /// Returns the formatted message contained in this message. Use this to apply
    /// any processing to it if it needs to be formatted in a specific way.
    // tg: code/game/machinery/telecomms/machines/message_server.dm:220
    public virtual object format_message()
    {
        return data["message"];
    }

    /// Returns the formatted photo path contained in this message, if there's one.
    // tg: code/game/machinery/telecomms/machines/message_server.dm:224
    public virtual object format_photo_path()
    {
        return data["photo"];
    }

    // tg: code/game/machinery/telecomms/machines/message_server.dm:227
    public override object broadcast()
    {
        foreach (object app_entry in Dm.each(data["targets"]))
        {
            if (app_entry is datum_computer_file_program_messenger app && !QDELETED(app))
                app.receive_message(this);
        }
        return null;
    }
}

// Request Console signal datum
// tg: code/game/machinery/telecomms/machines/message_server.dm:234
public partial class datum_signal_subspace_messaging_rc : datum_signal_subspace_messaging
{
    // tg: code/game/machinery/telecomms/machines/message_server.dm:233
    // Gaps SC1: translated with the request consoles. create_message speaks (say, the radio announcement), async in the port and
    // never sleeping in tg's sense: Waiting.Detach runs it to its end in place, as tg's call does, from this sync override.
    public override object broadcast()
    {
        object recipient_department = ckey(data["recipient_department"]);
        foreach (obj_machinery_requests_console console in Dm.each<obj_machinery_requests_console>(GLOB.req_console_all))
        {
            if (Dm.eq(ckey(console.department), recipient_department) || (Dm.truthy(data["ore_update"]) && Dm.truthy(console.receive_ore_updates)))
                Waiting.Detach(console.create_message(data));
        }
        return null;
    }
}

/// Log datums stored by the message server.
// tg: code/game/machinery/telecomms/machines/message_server.dm:240
public partial class datum_data_tablet_msg : datum
{
    // tg: code/game/machinery/telecomms/machines/message_server.dm:253
    [DmParams("param_rec", "param_sender", "param_message", "param_photo")]
    public override async DmTask New(params object[] __extra)
    {
        object param_rec = Dm.arg(__extra, 0);
        object param_sender = Dm.arg(__extra, 1);
        object param_message = Dm.arg(__extra, 2);
        object param_photo = Dm.arg(__extra, 3);
        if (Dm.truthy(param_rec))
            recipient = Dm.str(param_rec);
        if (Dm.truthy(param_sender))
            sender = Dm.str(param_sender);
        if (Dm.truthy(param_message))
            message = Dm.str(param_message);
        if (Dm.truthy(param_photo))
            picture_asset_key = param_photo;
    }
}

// tg: code/game/machinery/telecomms/machines/message_server.dm:265  #define REQUEST_PRIORITY_NORMAL "Normal", _HIGH "High", _EXTREME
// "Extreme", _UNDETERMINED "Undetermined" (file-local)
// tg: code/game/machinery/telecomms/machines/message_server.dm:271
public partial class datum_data_rc_msg : datum
{
    // tg: code/game/machinery/telecomms/machines/message_server.dm:284
    [DmParams("param_rec", "param_sender", "param_message", "param_stamp", "param_id_auth", "param_priority")]
    public override async DmTask New(params object[] __extra)
    {
        object param_rec = Dm.arg(__extra, 0);
        object param_sender = Dm.arg(__extra, 1);
        object param_message = Dm.arg(__extra, 2);
        object param_stamp = Dm.arg(__extra, 3);
        object param_id_auth = Dm.arg(__extra, 4);
        object param_priority = Dm.arg(__extra, 5);
        if (Dm.truthy(param_rec))
            receiving_department = Dm.str(param_rec);
        if (Dm.truthy(param_sender))
            sender_department = Dm.str(param_sender);
        if (Dm.truthy(param_message))
            message = Dm.str(param_message);
        if (Dm.truthy(param_stamp))
            stamp = Dm.str(param_stamp);
        if (Dm.truthy(param_id_auth))
            id_auth = Dm.str(param_id_auth);
        if (Dm.truthy(param_priority))
        {
            if (Dm.eq(param_priority, REQ_NORMAL_MESSAGE_PRIORITY))
                priority = "Normal";
            else if (Dm.eq(param_priority, REQ_HIGH_MESSAGE_PRIORITY))
                priority = "High";
            else if (Dm.eq(param_priority, REQ_EXTREME_MESSAGE_PRIORITY))
                priority = "Extreme";
            else
                priority = "Undetermined";
        }
    }
}
