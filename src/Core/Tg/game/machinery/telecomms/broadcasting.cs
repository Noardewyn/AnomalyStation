// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/telecomms/broadcasting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the radio): the subspace signal, its copies and its vocal broadcast to the radios and their
// hearers. Text to speech joins the broadcast to its line (D308, the TTS identifier). Left out with a comment:
// the blackbox's broadcast count and the telecomms log (the logs, M10). The var blocks are generated (D111).

// Subtype of /datum/signal with additional processing information.
// tg: code/game/machinery/telecomms/broadcasting.dm:2
public partial class datum_signal_subspace : datum_signal
{
    // tg: code/game/machinery/telecomms/broadcasting.dm:21
    [DmParams("data")]
    public override async DmTask New(params object[] __extra)
    {
        object data = Dm.arg(__extra, 0);
        this.data = data as DmList ?? Dm.list();
    }

    /**
     * Handles creating a new subspace signal that's a hard copy of this one, linked
     * to this current signal via the `original` value, so that it can be traced back.
     */
    // tg: code/game/machinery/telecomms/broadcasting.dm:28
    public virtual datum_signal_subspace copy()
    {
        datum_signal_subspace copy = Dm.@new<datum_signal_subspace>();
        copy.original = this;
        copy.source = source;
        copy.levels = levels;
        copy.frequency = frequency;
        copy.server_type = server_type;
        copy.transmission_method = transmission_method;
        copy.data = data.Copy();
        return copy;
    }

    /**
     * Handles marking the current signal, as well as its original signal,
     * and their original signals (recursively) as done, in their `data["done"]`.
     */
    // tg: code/game/machinery/telecomms/broadcasting.dm:43
    public virtual void mark_done()
    {
        datum_signal_subspace current = this;
        while (current != null)
        {
            current.data["done"] = TRUE;
            current = current.original;
        }
    }

    /**
     * Handles sending this signal to every available receiver and mainframe.
     */
    // tg: code/game/machinery/telecomms/broadcasting.dm:52
    public virtual void send_to_receivers()
    {
        foreach (obj_machinery_telecomms_receiver receiver in Dm.each<obj_machinery_telecomms_receiver>(GLOB.telecomm_machines))
            receiver.receive_signal(this);
        foreach (obj_machinery_telecomms_allinone all_in_one_receiver in Dm.each<obj_machinery_telecomms_allinone>(GLOB.telecomm_machines))
            all_in_one_receiver.receive_signal(this);
    }

    /// Handles broadcasting this signal out, to be implemented by subtypes.
    // tg: code/game/machinery/telecomms/broadcasting.dm:59  (set waitfor = FALSE: this definition cannot sleep, a plain method, waiting-model.md 5.4)
    public virtual object broadcast()
    {
        return null;
    }
}

// Vocal transmissions (i.e. using saycode).
// Despite "subspace" in the name, these transmissions can also be RADIO
// (intercoms and SBRs) or SUPERSPACE (CentCom).
// tg: code/game/machinery/telecomms/broadcasting.dm:64
public partial class datum_signal_subspace_vocal : datum_signal_subspace
{
    // tg: code/game/machinery/telecomms/broadcasting.dm:71  #define COMPRESSION_VOCAL_SIGNAL_MIN 35 (file-local)
    private const float COMPRESSION_VOCAL_SIGNAL_MIN = 35;
    // tg: code/game/machinery/telecomms/broadcasting.dm:72  #define COMPRESSION_VOCAL_SIGNAL_MAX 65 (file-local)
    private const float COMPRESSION_VOCAL_SIGNAL_MAX = 65;

    // tg: code/game/machinery/telecomms/broadcasting.dm:74
    [DmParams("source", "frequency", "speaker", "language", "message", "spans", "message_mods")]
    public override async DmTask New(params object[] __extra)
    {
        obj source = Dm.arg<obj>(__extra, 0); // the originating radio
        object frequency = Dm.arg(__extra, 1); // the frequency the signal is taking place on
        atom_movable_virtualspeaker speaker = Dm.arg<atom_movable_virtualspeaker>(__extra, 2); // representation of the method's speaker
        object language = Dm.arg(__extra, 3); // the language of the message
        object message = Dm.arg(__extra, 4); // the text content of the message
        object spans = Dm.arg(__extra, 5); // the list of spans applied to the message
        object message_mods = Dm.arg(__extra, 6); // the list of modification applied to the message. Whispering, singing, ect
        this.source = source;
        this.frequency = Dm.num(frequency);
        this.language = language as Type;
        virt = speaker;
        datum_language lang_instance = GLOB.language_datum_instances[language] as datum_language;
        data = Dm.list(
            Dm.kv("name", speaker.name),
            Dm.kv("job", speaker.job),
            Dm.kv("message", message),
            Dm.kv("compression", Dm.rand(COMPRESSION_VOCAL_SIGNAL_MIN, COMPRESSION_VOCAL_SIGNAL_MAX)),
            Dm.kv("language", lang_instance.name),
            Dm.kv("spans", spans),
            Dm.kv("mods", message_mods));
        levels = SSmapping.get_connected_levels(get_turf(source));
    }

    // tg: code/game/machinery/telecomms/broadcasting.dm:102
    public override datum_signal_subspace copy()
    {
        datum_signal_subspace_vocal copy = Dm.@new<datum_signal_subspace_vocal>(source, frequency, virt, language);
        copy.original = this;
        copy.data = data.Copy();
        copy.levels = levels;
        return copy;
    }

    /// This is the meat function for making radios hear vocal transmissions.
    // tg: code/game/machinery/telecomms/broadcasting.dm:110  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4)
    public override object broadcast()
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(broadcast__body(dot), dot);
    }

    // The body of the set waitfor = FALSE wrapper above (waiting-model.md 5.4); it never writes '.'.
    private async DmTask<object> broadcast__body(DotBox<object> dot)
    {
        // Perform final composition steps on the message.
        object message = copytext_char(data["message"], 1, MAX_BROADCAST_LEN);
        if (!Dm.truthy(message))
            return dot.Value;
        object compression = data["compression"];
        if (Dm.gt(compression, 0))
            message = Gibberish(message, Dm.ge(compression, COMPRESSION_REPLACE_CHARACTER_THRESHOLD) ? TRUE : FALSE);

        object signal_reaches_every_z_level = levels;

        if (Dm.@in(0, levels))
            signal_reaches_every_z_level = RADIO_NO_Z_LEVEL_RESTRICTION;

        // Assemble the list of radios
        DmList radios = Dm.list();
        object sw__1 = transmission_method;
        if (Dm.eq(sw__1, TRANSMISSION_SUBSPACE))
        {
            // Reaches any radios on the levels
            DmList all_radios_of_our_frequency = GLOB.all_radios[Dm.text("[]", frequency)] as DmList;
            if (Dm.truthy(LAZYLEN(all_radios_of_our_frequency)))
                radios = all_radios_of_our_frequency.Copy();

            foreach (obj_item_radio subspace_radio in Dm.each<obj_item_radio>(radios.Copy()))
            {
                if (!Dm.truthy(subspace_radio.can_receive(frequency, signal_reaches_every_z_level)))
                    Dm.remove(radios, subspace_radio);
            }

            // Syndicate radios can hear all well-known radio channels
            foreach (object channel in Dm.each(GLOB.default_radio_channels))
            {
                if (Dm.eq(GLOB.default_radio_channels[channel], frequency))
                {
                    foreach (obj_item_radio syndicate_radios in Dm.each<obj_item_radio>(GLOB.all_radios[Dm.text("[]", FREQ_SYNDICATE)] as DmList))
                    {
                        if (Dm.truthy(syndicate_radios.can_receive(FREQ_SYNDICATE, RADIO_NO_Z_LEVEL_RESTRICTION)))
                            Dm.combine(radios, syndicate_radios);
                    }
                }
            }
        }

        else if (Dm.eq(sw__1, TRANSMISSION_RADIO))
        {
            // Only radios not currently in subspace mode
            foreach (obj_item_radio non_subspace_radio in Dm.each<obj_item_radio>(GLOB.all_radios[Dm.text("[]", frequency)] as DmList))
            {
                if (!Dm.truthy(non_subspace_radio.subspace_transmission) && Dm.truthy(non_subspace_radio.can_receive(frequency, signal_reaches_every_z_level)))
                    Dm.append(ref radios, non_subspace_radio);
            }
        }

        else if (Dm.eq(sw__1, TRANSMISSION_SUPERSPACE))
        {
            // Only radios which are independent
            foreach (obj_item_radio independent_radio in Dm.each<obj_item_radio>(GLOB.all_radios[Dm.text("[]", frequency)] as DmList))
            {
                if (Dm.truthy(Dm.bitand(independent_radio.special_channels, RADIO_SPECIAL_CENTCOM)) && Dm.truthy(independent_radio.can_receive(frequency, signal_reaches_every_z_level)))
                    Dm.append(ref radios, independent_radio);
            }
        }

        foreach (obj_item_radio called_radio in Dm.each_cast<obj_item_radio>(radios))
            await called_radio.on_receive_message(data);
        DmList message_mods = data["mods"] as DmList;

        object tts_radio_id = LAZYACCESS(message_mods, MODE_TTS_IDENTIFIER);
        // Flat list of mobs who can hear the message
        DmList receive;
        // Assoc list of weakref to a radio to list of weakrefs to mobs who can hear the message
        DmList receive_radios = null;

        if (Dm.truthy(tts_radio_id)) // only do this if we have a TTS identifier to save on perf
        {
            receive = Dm.list();
            receive_radios = Dm.list();
            DmList tracked = get_hearers_in_radio_ranges_track_radios(radios);
            foreach (object radio in Dm.each(tracked))
            {
                DmList radio_hearers = tracked[radio] as DmList;
                Dm.combine(receive, radio_hearers);
                object radio_ref = WEAKREF(radio as datum);
                foreach (mob possible_hearer in Dm.each<mob>(radio_hearers))
                {
                    if (possible_hearer.client != null && Dm.truthy(can_hear_radio_tts(possible_hearer, frequency)))
                    {
                        if (!Dm.truthy(receive_radios[radio_ref]))
                            receive_radios[radio_ref] = Dm.list();
                        (receive_radios[radio_ref] as DmList).Add(WEAKREF(possible_hearer));
                    }
                }
            }
        }
        else
            receive = get_hearers_in_radio_ranges(radios);

        // Add observers who have ghost radio enabled.
        foreach (mob_dead_observer ghost in Dm.each<mob_dead_observer>(GLOB.player_list))
        {
            if (Dm.truthy(Dm.bitand(get_chat_toggles(ghost.client), CHAT_GHOSTRADIO)))
            {
                Dm.combine(receive, ghost);
                if (Dm.truthy(tts_radio_id) && Dm.truthy(can_hear_radio_tts(ghost, frequency)))
                {
                    if (!Dm.truthy(receive_radios[TTS_GHOST_RADIO]))
                        receive_radios[TTS_GHOST_RADIO] = Dm.list();
                    (receive_radios[TTS_GHOST_RADIO] as DmList).Add(WEAKREF(ghost));
                }
            }
        }

        if (Dm.truthy(tts_radio_id) && Dm.length(receive_radios) > 0)
        {
            SStts.queued_radio_messages[tts_radio_id] = receive_radios;
            SStts.queued_radio_messages_compression[tts_radio_id] = compression;
            SStts.queued_radio_messages_frequency[tts_radio_id] = frequency; // D308: the client's departmental-radio option
            SStts.play_queued_radio(); // DEVIATION(DV262): tg's fire picks it up on its next tick
        }

        // Render the message and have everybody hear it.
        // Always call this on the virtualspeaker to avoid issues.
        DmList spans = data["spans"] as DmList;

        foreach (atom_movable hearer in Dm.each_cast<atom_movable>(receive))
        {
            if (hearer == null)
            {
                stack_trace("null found in the hearers list returned by the spatial grid. this is bad", "code/game/machinery/telecomms/broadcasting.dm", 200);
                continue;
            }
            Dm.remove(spans, blacklisted_spans);
            await hearer.Hear(virt, language, message, frequency, data["frequency_name"], data["frequency_color"], spans, message_mods, message_range: INFINITY);
        }

        // This following recording is intended for research and feedback in the use of department radio channels
        // tg: :201  SSblackbox.LogBroadcast(frequency): the round statistics (M10)

        string spans_part = "";
        if (Dm.truthy(length(spans)))
        {
            spans_part = "(spans:";
            foreach (object span in Dm.each(spans))
                spans_part = Dm.text("[] []", spans_part, span);
            spans_part = Dm.text("[] ) ", spans_part);
        }

        object lang_name = data["language"];
        object log_text = Dm.text("\\[[]\\] []\"[]\" (language: [])", get_radio_name(frequency), spans_part, message, lang_name);

        mob source_mob = virt.source as mob;

        if (ismob(source_mob))
            source_mob.log_message(log_text, LOG_TELECOMMS);
        else
            log_telecomms(Dm.text("[] [] []", virt.source, log_text, loc_name(get_turf(virt.source) as atom)));

        // QDEL_IN(virt, 5 SECONDS), expanded (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 5 * SECONDS > GC_FILTER_QUEUE ? WEAKREF(virt) : virt), 5 * SECONDS, file: "code/game/machinery/telecomms/broadcasting.dm", line: 226);  // Make extra sure the virtualspeaker gets qdeleted
        return dot.Value;
    }
}
