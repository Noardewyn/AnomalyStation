// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/radio/radio.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the radio): the station bounced radio, its channels and keys, its transmission (subspace first,
// then a mundane backup), what it hears and relays, its tgui window ("Radio"), its overlays and its sounds. Left out with a
// comment: the wires window an unscrewed radio opens (the hacking interface, D162). DEVIATION(DV47): the radio noise volume
// preference is read as its default, 100 (the preferences are not translated). The var block is generated (D111) but for the
// keyslot, which holds a type path until Initialize makes the key (so the type's own overrides are here too, the Hand mode).

// tg: code/game/objects/items/devices/radio/radio.dm:1  #define FREQ_LISTENING (1<<0) (file-local)

// tg: code/game/objects/items/devices/radio/radio.dm:3
[DmPath("/obj/item/radio")]
public partial class obj_item_radio : obj_item
{
    private const int FREQ_LISTENING = 1 << 0;

    /// Encryption key handling
    // tg: code/game/objects/items/devices/radio/radio.dm:72  (a type path until Initialize makes the key)
    public object keyslot;

    // The type's overrides of inherited vars (the Hand mode, types-converter.md 3.2: the class declares keyslot, so the generated
    // part holds only the vars it does not declare)
    // tg: code/game/objects/items/devices/radio/radio.dm:3
    public obj_item_radio()
    {
        icon = Dm.resource("icons/obj/devices/voice.dmi");
        name = "station bounced radio";
        icon_state = "walkietalkie";
        inhand_icon_state = "walkietalkie";
        lefthand_file = Dm.resource("icons/mob/inhands/items/devices_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/items/devices_righthand.dmi");
        worn_icon_state = "radio";
        desc = "A basic handheld radio that communicates with local telecommunication networks.";
        dog_fashion = typeof(datum_dog_fashion_back);
        interaction_flags_atom = 1540;
        sound_vary = 1;
        pickup_sound = "generic_device_pickup";
        drop_sound = "generic_device_drop";
        obj_flags = 4096;
        slot_flags = 512;
        throw_speed = 3;
        throw_range = 7;
        w_class = 2;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:23
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), 7.5f), Dm.kv(typeof(datum_material_glass), 2.5f));
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:100
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        set_wires(Dm.@new<datum_wires_radio>(this));
        object dot = base.Initialize(mapload, __extra);

        if (ispath(keyslot))
        {
            keyslot = Dm.@new((Type)keyslot, this);
            recalculateChannels();
        }

        perform_update_icon = FALSE;
        set_listening(listening);
        set_broadcasting(broadcasting);
        set_frequency(sanitize_frequency(frequency, freerange, Dm.bitand(special_channels, RADIO_SPECIAL_SYNDIE)));
        set_on(on);
        perform_update_icon = TRUE;

        if (Dm.truthy(should_update_icon))
            update_appearance(UPDATE_ICON);

        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_WIRES));
        register_context();
        // No subtypes
        if (GetType() != typeof(obj_item_radio))
            return dot;
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), string_list(Dm.list(typeof(datum_crafting_recipe_improv_explosive)))));
        if (Dm.prob(Dm.truthy(check_holidays(APRIL_FOOLS)) ? 50 : 0.5f)) // Extremely rare chance to replace a normal radio with a toy one, because it's funny
            make_silly();
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:127
    public override object Destroy(float? force = null)
    {
        remove_radio_all(this); //Just to be sure
        if (keyslot is obj_item_encryptionkey)
        {
            qdel(keyslot);
            keyslot = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:133
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.eq(held_item?.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Remove encryption key";
            context[SCREENTIP_CONTEXT_RMB] = Dm.truthy(unscrewed) ? "Screw in" : "Unscrew";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (held_item is obj_item_encryptionkey)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Install encryption key";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:143
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        object dot = await base.on_saboteur(source, disrupt_duration);
        if (Dm.truthy(broadcasting)) //no broadcasting but it can still be used to send radio messages.
        {
            set_broadcasting(FALSE);
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:149
    public virtual void set_frequency(object new_frequency = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_RADIO_NEW_FREQUENCY]))
            this._SendSignal(COMSIG_RADIO_NEW_FREQUENCY, Dm.list(this, Dm.list(new_frequency)));
        remove_radio(this, frequency);
        if (Dm.truthy(new_frequency))
            frequency = Dm.num(new_frequency);

        if (Dm.truthy(listening) && Dm.truthy(on))
            add_radio(this, new_frequency);
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:158
    public virtual void recalculateChannels()
    {
        resetChannels();

        if (keyslot is obj_item_encryptionkey key)
        {
            foreach (object channel_name in Dm.each(key.channels))
            {
                if (!Dm.@in(channel_name, channels))
                    channels[channel_name] = key.channels[channel_name];
            }

            special_channels = Dm.bitor(special_channels, key.special_channels);
        }

        foreach (object channel_name in Dm.each(channels))
            LAZYSET(ref secure_radio_connections, channel_name, add_radio(this, GLOB.default_radio_channels[channel_name]));

        if (!Dm.truthy(listening))
            remove_radio_all(this);
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:174
    public virtual void resetChannels()
    {
        foreach (object ch_name in Dm.each(channels))
            SSradio.remove_object(this, GLOB.default_radio_channels[ch_name]);

        channels = Dm.list();
        LAZYNULL(ref secure_radio_connections);
        special_channels = NONE;

        if (!Dm.truthy(freerange) && (Dm.gt(frequency, MAX_FREQ) || Dm.lt(frequency, MIN_FREQ)))
            frequency = FREQ_COMMON;
    }

    ///goes through all radio channels we should be listening for and readds them to the global list
    // tg: code/game/objects/items/devices/radio/radio.dm:186
    public virtual void readd_listening_radio_channels()
    {
        foreach (object channel_name in Dm.each(channels))
            add_radio(this, GLOB.default_radio_channels[channel_name]);

        add_radio(this, frequency);
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:192
    public virtual void make_syndie() // Turns normal radios into Syndicate radios!
    {
        qdel(keyslot);
        keyslot = Dm.@new<obj_item_encryptionkey_syndicate>();
        special_channels = Dm.bitor(special_channels, RADIO_SPECIAL_SYNDIE);
        recalculateChannels();
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:198
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(unscrewed) && !isAI(user))
        {
            // tg: :200  wires.interact(user): the wires window (the hacking interface, D162)
            Dm.Deferred("code/game/objects/items/devices/radio/radio.dm:200", "interact: wires.interact(user), the unscrewed radio's wires window (the hacking interface, D162)");
            add_fingerprint(user);
        }
        else
            return await base.interact(user, __extra);
        return null;
    }

    //simple getters only because i NEED to enforce complex setter use for these vars for caching purposes but VAR_PROTECTED requires getter usage as well.
    //if another decorator is made that doesn't require getters feel free to nuke these and change these vars over to that

    ///simple getter for the on variable. necessary due to VAR_PROTECTED
    // tg: code/game/objects/items/devices/radio/radio.dm:209
    public virtual float is_on()
    {
        return on;
    }

    ///simple getter for the frequency variable. necessary due to VAR_PROTECTED
    // tg: code/game/objects/items/devices/radio/radio.dm:213
    public virtual float? get_frequency()
    {
        return frequency;
    }

    ///simple getter for the broadcasting variable. necessary due to VAR_PROTECTED
    // tg: code/game/objects/items/devices/radio/radio.dm:217
    public virtual float get_broadcasting()
    {
        return broadcasting;
    }

    ///simple getter for the listening variable. necessary due to VAR_PROTECTED
    // tg: code/game/objects/items/devices/radio/radio.dm:221
    public virtual float get_listening()
    {
        return listening;
    }

    //now for setters for the above protected vars

    /**
     * setter for the listener var, adds or removes this radio from the global radio list if we are also on
     *
     * * new_listening - the new value we want to set listening to
     * * actual_setting - whether or not the radio is supposed to be listening, sets should_be_listening to the new listening value if true, otherwise just changes listening
     */
    // tg: code/game/objects/items/devices/radio/radio.dm:232
    public virtual void set_listening(object new_listening = null, float? actual_setting = null)
    {
        actual_setting ??= TRUE;

        listening = Dm.num(new_listening);
        if (Dm.truthy(actual_setting))
            should_be_listening = listening;

        if (Dm.truthy(listening) && Dm.truthy(on))
            readd_listening_radio_channels();
        else if (!Dm.truthy(listening))
            remove_radio_all(this);

        if (Dm.truthy(perform_update_icon) && overlay_speaker_idle != null)
            Waiting.NoSleep(update_icon());
        else if (!Dm.truthy(perform_update_icon))
            should_update_icon = TRUE;
    }

    /**
     * setter for broadcasting that makes us not hearing sensitive if not broadcasting and hearing sensitive if broadcasting
     * hearing sensitive in this case only matters for the purposes of listening for words said in nearby tiles, talking into us directly bypasses hearing
     *
     * * new_broadcasting- the new value we want to set broadcasting to
     * * actual_setting - whether or not the radio is supposed to be broadcasting, sets should_be_broadcasting to the new value if true, otherwise just changes broadcasting
     */
    // tg: code/game/objects/items/devices/radio/radio.dm:255
    public virtual void set_broadcasting(object new_broadcasting = null, float? actual_setting = null)
    {
        actual_setting ??= TRUE;

        broadcasting = Dm.num(new_broadcasting);
        if (Dm.truthy(actual_setting))
            should_be_broadcasting = broadcasting;

        if (Dm.truthy(broadcasting) && Dm.truthy(on)) //we don't need hearing sensitivity if we aren't broadcasting, because talk_into doesn't care about hearing
            become_hearing_sensitive(INNATE_TRAIT);
        else if (!Dm.truthy(broadcasting))
            lose_hearing_sensitivity(INNATE_TRAIT);

        if (Dm.truthy(perform_update_icon) && overlay_mic_idle != null)
            Waiting.NoSleep(update_icon());
        else if (!Dm.truthy(perform_update_icon))
            should_update_icon = TRUE;
    }

    ///setter for the on var that sets both broadcasting and listening to off or whatever they were supposed to be
    // tg: code/game/objects/items/devices/radio/radio.dm:272
    public virtual void set_on(object new_on = null)
    {
        on = Dm.num(new_on);

        if (Dm.truthy(on))
        {
            set_broadcasting(should_be_broadcasting);//set them to whatever they're supposed to be
            set_listening(should_be_listening);
        }
        else
        {
            set_broadcasting(FALSE, actual_setting: FALSE);//fake set them to off
            set_listening(FALSE, actual_setting: FALSE);
        }
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:283
    public override object talk_into(atom_movable talking_movable = null, object message = null, object channel = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(talking_movable._listen_lookup?[COMSIG_MOVABLE_USING_RADIO]) ? NONE : talking_movable._SendSignal(COMSIG_MOVABLE_USING_RADIO, Dm.list(talking_movable, this)), COMPONENT_CANNOT_USE_RADIO)))
            return NONE;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(this._listen_lookup?[COMSIG_RADIO_NEW_MESSAGE]) ? NONE : this._SendSignal(COMSIG_RADIO_NEW_MESSAGE, Dm.list(this, talking_movable, message, channel)), COMPONENT_CANNOT_USE_RADIO)))
            return NONE;

        if (spans == null)
            spans = Dm.list(talking_movable.speech_span);
        if (!Dm.truthy(language))
            language = talking_movable.get_selected_language();
        Waiting.Detach(talk_into_impl(talking_movable, message, channel, LAZYLISTDUPLICATE(spans), language, LAZYLISTDUPLICATE(message_mods)));
        return Dm.bitor(ITALICS, REDUCE_RANGE);
    }

    /**
     * Handles talking into the radio
     *
     * Unlike most speech related procs, spans and message_mods are not guaranteed to be lists
     *
     * * talking_movable - the atom that is talking
     * * message - the message to be spoken
     * * channel - the channel to be spoken on
     * * spans - the spans to be used, lazylist
     * * language - the language to be spoken in. (Should) never be null
     * * message_mods - the message mods to be used, lazylist
     */
    // tg: code/game/objects/items/devices/radio/radio.dm:308
    public virtual async DmTask<object> talk_into_impl(atom_movable talking_movable = null, object message = null, object channel = null, DmList spans = null, object language = null, DmList message_mods = null)
    {
        if (!Dm.truthy(on))
            return null; // the device has to be on
        if (talking_movable == null || !Dm.truthy(message))
            return null;
        if (Dm.truthy(wires.is_cut(WIRE_TX)))  // Permacell and otherwise tampered-with radios
            return null;
        if (!Dm.truthy(await talking_movable.try_speak(message, ignore_spam: TRUE, filterproof: TRUE)))
            return null;

        if (Dm.truthy(use_command))
            Dm.combine(ref spans, SPAN_COMMAND);

        object radio_message = message;
        if (Dm.truthy(LAZYACCESS(message_mods, WHISPER_MODE)))
        {
            // Radios don't pick up whispers very well
            radio_message = stars(radio_message);
            Dm.combine(ref spans, SPAN_ITALICS);
        }

        flick_overlay_view(overlay_mic_active, 5 * SECONDS);

        /*
        Roughly speaking, radios attempt to make a subspace transmission (which
        is received, processed, and rebroadcast by the telecomms satellite) and
        if that fails, they send a mundane radio transmission.

        Headsets cannot send/receive mundane transmissions, only subspace.
        Syndicate radios can hear transmissions on all well-known frequencies.
        CentCom radios can hear the CentCom frequency no matter what.
        */

        // From the channel, determine the frequency and get a reference to it.
        object freq;
        if (Dm.truthy(channel) && channels != null && channels.len > 0)
        {
            if (Dm.eq(channel, MODE_DEPARTMENT))
                channel = channels[1];
            freq = LAZYACCESS(secure_radio_connections, channel);
            if (!Dm.truthy(channels[channel])) // if the channel is turned off, don't broadcast
                return null;
        }
        else
        {
            freq = frequency;
            channel = null;
        }

        // Nearby active jammers prevent the message from transmitting
        if (is_within_radio_jammer_range(this) && !Dm.truthy(Dm.bitand(special_channels, RADIO_SPECIAL_SYNDIE)))
            return null;

        // Determine the identity information which will be attached to the signal.
        atom_movable_virtualspeaker speaker = Dm.@new<atom_movable_virtualspeaker>(null, talking_movable, this);

        // Construct the signal
        datum_signal_subspace_vocal signal = Dm.@new<datum_signal_subspace_vocal>(this, freq, speaker, language, radio_message, spans, message_mods);

        // Independent radios, on the CentCom frequency, reach all independent radios
        if (Dm.truthy(Dm.bitand(special_channels, RADIO_SPECIAL_CENTCOM)) && (Dm.eq(freq, FREQ_CENTCOM) || Dm.eq(freq, FREQ_STATUS_DISPLAYS)))
        {
            signal.data["compression"] = 0;
            signal.transmission_method = TRANSMISSION_SUPERSPACE;
            signal.levels = Dm.list(0);
            signal.broadcast();
            return null;
        }

        if (isliving(talking_movable))
        {
            mob_living talking_living = (mob_living)talking_movable;
            // DEVIATION(DV47): read_preference(/datum/preference/numeric/volume/sound_radio_noise) is its default, 100
            // (modules/client/preferences/sounds.dm:6); the preferences are not translated
            object volume_modifier = talking_living.client != null ? 100 : null;
            if (Dm.truthy(radio_noise) && !HAS_TRAIT(talking_living, TRAIT_DEAF) && Dm.truthy(volume_modifier) && !Dm.eq(signal.frequency, FREQ_COMMON) && !Dm.truthy(LAZYACCESS(message_mods, MODE_SEQUENTIAL)) && audio_cooldown <= world.time) // COOLDOWN_FINISHED(src, audio_cooldown)
            {
                audio_cooldown = world.time + 0.5f * SECONDS; // COOLDOWN_START(src, audio_cooldown, 0.5 SECONDS)
                sound radio_noise_sound = sound(Dm.resource("sound/items/radio/radio_talk.ogg"), volume: Dm.num(volume_modifier));
                radio_noise_sound.frequency = Dm.num(get_rand_frequency_low_range());
                Dm.output_to(talking_living, radio_noise_sound); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            }
        }

        // All radios make an attempt to use the subspace system first
        signal.send_to_receivers();

        // If the radio is subspace-only, that's all it can do
        if (Dm.truthy(subspace_transmission))
            return null;

        // Non-subspace radios will check in a couple of seconds, and if the signal
        // was never received, send a mundane broadcast (no headsets).
        _addtimer(Dm.@new<datum_callback>(this, nameof(backup_transmission), signal), 2 * SECONDS, file: "code/game/objects/items/devices/radio/radio.dm", line: 387);
        return null;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:389
    public virtual void backup_transmission(datum_signal_subspace_vocal signal = null)
    {
        turf T = get_turf(this);
        if (Dm.truthy(signal.data["done"]) && Dm.@in(T.z, signal.levels))
            return;

        // Okay, the signal was never processed, send a mundane broadcast.
        signal.data["compression"] = 0;
        signal.transmission_method = TRANSMISSION_RADIO;
        signal.levels = SSmapping.get_connected_levels(T);
        signal.broadcast();
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:400
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (Dm.truthy(radio_freq) || !Dm.truthy(broadcasting) || Dm.num(get_dist(this, speaker)) > canhear_range || Dm.truthy(message_mods[MODE_RELAY]))
            return dot;
        DmList filtered_mods = Dm.list();

        if (Dm.truthy(message_mods[MODE_SING]))
            filtered_mods[MODE_SING] = message_mods[MODE_SING];
        if (Dm.truthy(message_mods[WHISPER_MODE]))
            filtered_mods[WHISPER_MODE] = message_mods[WHISPER_MODE];
        if (Dm.truthy(message_mods[SAY_MOD_VERB]))
            filtered_mods[SAY_MOD_VERB] = message_mods[SAY_MOD_VERB];
        if (Dm.truthy(message_mods[MODE_CUSTOM_SAY_EMOTE]))
        {
            filtered_mods[MODE_CUSTOM_SAY_EMOTE] = message_mods[MODE_CUSTOM_SAY_EMOTE];
            filtered_mods[MODE_CUSTOM_SAY_ERASE_INPUT] = message_mods[MODE_CUSTOM_SAY_ERASE_INPUT];
        }
        if (Dm.eq(message_mods[RADIO_EXTENSION], MODE_L_HAND) || Dm.eq(message_mods[RADIO_EXTENSION], MODE_R_HAND))
        {
            // try to avoid being heard double
            if (loc == speaker && ismob(speaker))
            {
                mob mob_speaker = (mob)speaker;
                object idx = mob_speaker.get_held_index_of_item(this);
                // left hands are odd slots
                if (Dm.truthy(idx) && Dm.eq(Dm.mod(idx, 2), Dm.eq(message_mods[RADIO_EXTENSION], MODE_L_HAND) ? 1 : 0))
                    return dot;
            }
        }
        if (Dm.truthy(message_mods[MODE_TTS_IDENTIFIER]))
            filtered_mods[MODE_TTS_IDENTIFIER] = message_mods[MODE_TTS_IDENTIFIER];
        talk_into(speaker, raw_message, spans: spans, language: message_language, message_mods: filtered_mods);
        return dot;
    }

    /// Checks if this radio can receive on the given frequency.
    // tg: code/game/objects/items/devices/radio/radio.dm:428
    public virtual object can_receive(object input_frequency = null, object levels = null)
    {
        // deny checks
        if (!Dm.eq(levels, RADIO_NO_Z_LEVEL_RESTRICTION))
        {
            turf position = get_turf(this);
            if (position == null || !Dm.@in(position.z, levels))
                return FALSE;
        }

        if (Dm.eq(input_frequency, FREQ_SYNDICATE) && !Dm.truthy(Dm.bitand(special_channels, RADIO_SPECIAL_SYNDIE)))
            return FALSE;

        // allow checks: are we listening on that frequency?
        if (Dm.eq(input_frequency, frequency))
            return TRUE;
        foreach (object ch_name in Dm.each(channels))
        {
            if (Dm.truthy(Dm.bitand(channels[ch_name], FREQ_LISTENING)))
            {
                if (Dm.eq(GLOB.default_radio_channels[ch_name], text2num(input_frequency)) || Dm.truthy(Dm.bitand(special_channels, RADIO_SPECIAL_SYNDIE)))
                    return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:447
    public virtual async DmTask on_receive_message(DmList data = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_RADIO_RECEIVE_MESSAGE]))
            this._SendSignal(COMSIG_RADIO_RECEIVE_MESSAGE, Dm.list(this, data));
        if (!isliving(loc))
            return;

        mob_living holder = (mob_living)loc;
        // DEVIATION(DV47): read_preference(/datum/preference/numeric/volume/sound_radio_noise) is its default, 100; a holder
        // without a client reads null, as in tg
        object volume_modifier = holder.client != null ? 100 : null;

        if (!Dm.truthy(radio_noise) || HAS_TRAIT(holder, TRAIT_DEAF) || !Dm.truthy(volume_modifier))
            return;
        DmList spans = data["spans"] as DmList;
        if (audio_cooldown <= world.time) // COOLDOWN_FINISHED(src, audio_cooldown)
        {
            audio_cooldown = world.time + 0.5f * SECONDS; // COOLDOWN_START(src, audio_cooldown, 0.5 SECONDS)
            sound radio_receive = sound(Dm.resource("sound/items/radio/radio_receive.ogg"), volume: Dm.num(volume_modifier));
            radio_receive.frequency = Dm.num(get_rand_frequency_low_range());
            Dm.output_to(holder, radio_receive); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
        if (Dm.@in(SPAN_COMMAND, spans) && important_audio_cooldown <= world.time) // COOLDOWN_FINISHED(src, important_audio_cooldown)
        {
            important_audio_cooldown = world.time + 0.5f * SECONDS; // COOLDOWN_START(src, important_audio_cooldown, 0.5 SECONDS)
            sound radio_important = sound(Dm.resource("sound/items/radio/radio_important.ogg"), volume: Dm.num(volume_modifier));
            radio_important.frequency = Dm.num(get_rand_frequency_low_range());
            Dm.output_to(holder, radio_important); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:469
    public override object ui_state(mob user = null)
    {
        return GLOB.inventory_state;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:472
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        datum_ui_state state = Dm.arg<datum_ui_state>(__extra, 0);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Radio", name);
            if (state != null)
                ui.set_state(state);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:480
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["broadcasting"] = broadcasting;
        data["listening"] = listening;
        data["frequency"] = frequency;
        data["minFrequency"] = Dm.truthy(freerange) ? MIN_FREE_FREQ : MIN_FREQ;
        data["maxFrequency"] = Dm.truthy(freerange) ? MAX_FREE_FREQ : MAX_FREQ;
        data["freqlock"] = !Dm.eq(freqlock, RADIO_FREQENCY_UNLOCKED) ? TRUE : FALSE;
        data["channels"] = Dm.list();
        foreach (object channel in Dm.each(channels))
            ((DmList)data["channels"])[channel] = Dm.bitand(channels[channel], FREQ_LISTENING);
        data["command"] = command;
        data["useCommand"] = use_command;
        data["subspace"] = subspace_transmission;
        data["subspaceSwitchable"] = subspace_switchable;
        data["headset"] = FALSE;

        return data;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:500
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "frequency"))
        {
            if (!Dm.eq(freqlock, RADIO_FREQENCY_UNLOCKED))
                return dot;
            float tune = 0;
            object adjust = text2num(@params["adjust"]);
            if (Dm.truthy(adjust))
                tune = Dm.num(frequency) + Dm.num(adjust) * 10;
            else if (tune != 0)
                tune *= 10;
            if (tune != 0)
                set_frequency(sanitize_frequency(tune, freerange, Dm.bitand(special_channels, RADIO_SPECIAL_SYNDIE)));
            dot = TRUE;
        }

        else if (Dm.eq(sw__1, "tune_to_channel"))
        {
            if (!Dm.eq(freqlock, RADIO_FREQENCY_UNLOCKED))
                return dot;
            object channel = @params["channel"];
            if (!Dm.@in(channel, channels))
                return dot;
            // bypasses frequency range, force tunes to a specific encrypted channel
            set_frequency(GLOB.default_radio_channels[channel]);
            dot = TRUE;
        }

        else if (Dm.eq(sw__1, "listen"))
        {
            set_listening(Dm.num(!Dm.truthy(listening)));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "broadcast"))
        {
            set_broadcasting(Dm.num(!Dm.truthy(broadcasting)));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "channel"))
        {
            object channel = @params["channel"];
            if (!Dm.@in(channel, channels))
                return dot;
            if (Dm.truthy(Dm.bitand(channels[channel], FREQ_LISTENING)))
                channels[channel] = Dm.bitand(channels[channel], ~FREQ_LISTENING);
            else
                channels[channel] = Dm.bitor(channels[channel], FREQ_LISTENING);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "command"))
        {
            use_command = Dm.num(!Dm.truthy(use_command));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "subspace"))
        {
            if (Dm.truthy(subspace_switchable))
            {
                subspace_transmission = Dm.num(!Dm.truthy(subspace_transmission));
                if (!Dm.truthy(subspace_transmission))
                    channels = Dm.list();
                else
                    recalculateChannels();
                dot = TRUE;
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:556
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(frequency) && in_range(this, user))
            Dm.append(ref dot, span_notice(I18n.T("It is set to broadcast over the [] frequency.", span_radio(Dm.text("[]", Dm.num(frequency) / 10)))));
        if (Dm.truthy(unscrewed))
            Dm.append(ref dot, span_notice(I18n.T("It can be attached and modified.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("It cannot be modified or attached.")));
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:565
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(unscrewed))
            return dot;
        if (Dm.truthy(broadcasting) && Dm.truthy(overlay_mic_idle))
            Dm.append(ref dot, overlay_mic_idle);
        if (Dm.truthy(listening) && Dm.truthy(overlay_speaker_idle))
            Dm.append(ref dot, overlay_speaker_idle);
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:574
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) && Dm.eq(tool.tool_behaviour, TOOL_SCREWDRIVER))
            return await screwdriver_act(user, tool);
        if (tool is obj_item_encryptionkey key)
            return await install_key(user, key);
        return NONE;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:581
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        add_fingerprint(user);
        unscrewed = Dm.num(!Dm.truthy(unscrewed));
        tool.play_tool_sound(this, 10);
        if (Dm.truthy(unscrewed))
            to_chat(user, span_notice(I18n.T("[] can now be attached and modified!", this)));
        else
            to_chat(user, span_notice(I18n.T("[] can no longer be modified or attached!", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:591
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object sw__1 = keylock;
        if (Dm.eq(sw__1, RADIO_KEYSLOT_LOCKED))
        {
            to_chat(user, span_warning(I18n.T("The screws locking []'s keyslot are stripped, and can't be removed.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        else if (Dm.eq(sw__1, RADIO_KEYSLOT_EMAGGABLE_LOCK))
        {
            to_chat(user, span_warning(I18n.T("The screws locking []'s keyslot are fastened tight, and likely can't be removed without some kind of magnet...", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        DmList removed_keys = await remove_keys(user);
        if (Dm.num(length(removed_keys)) > 1)
            to_chat(user, span_notice(I18n.T("You remove the encryption keys from [].", this)));
        else if (Dm.num(length(removed_keys)) == 1)
            to_chat(user, span_notice(I18n.T("You remove [] from [].", removed_keys[1], this)));
        else
            to_chat(user, span_warning(I18n.T("[] doesn't have any unique encryption keys! How useless...", this)));
        tool.play_tool_sound(this, 10);
        return TRUE;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:610
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == keyslot)
        {
            keyslot = null;
            if (!QDELING(this))
                recalculateChannels();
        }
        return dot;
    }

    /// Attempts to put all keys in the radio into the user's hands
    /// Returns a list of the removed keys
    // tg: code/game/objects/items/devices/radio/radio.dm:619
    public virtual async DmTask<DmList> remove_keys(mob_living user = null)
    {
        DmList dot = Dm.list();
        if (!(keyslot is obj_item_encryptionkey key))
            return dot;

        Dm.append(ref dot, key);
        await user.put_in_hands(key); // null via Exited
        return dot;
    }

    /// Attempts to install the given encryption key into the radio
    // tg: code/game/objects/items/devices/radio/radio.dm:628
    public virtual async DmTask<object> install_key(mob_living user = null, obj_item_encryptionkey key = null)
    {
        if (Dm.truthy(keyslot))
        {
            loc.balloon_alert(user, I18n.T("cannot hold a second key!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(freqlock) || Dm.truthy(keylock))
        {
            loc.balloon_alert(user, I18n.T("keyslot is locked!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(key, this)))
        {
            loc.balloon_alert(user, I18n.T("cannot install!"));
            return ITEM_INTERACT_BLOCKING;
        }

        keyslot = key;
        recalculateChannels();
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        loc.balloon_alert(user, I18n.T("encryption key installed"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:646
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        emped++; //There's been an EMP; better count it
        float curremp = emped; //Remember which EMP this was
        if (Dm.truthy(listening) && ismob(loc)) // if the radio is turned on and on someone's person they notice
            to_chat(loc, span_warning(I18n.T("\\The [] overloads.", this)));
        foreach (object ch_name in Dm.each(channels))
            channels[ch_name] = 0;
        set_on(FALSE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(end_emp_effect), curremp), 20 * SECONDS, file: "code/game/objects/items/devices/radio/radio.dm", line: 657);
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:659
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] starts bouncing [] off [] head! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:663
    public virtual object end_emp_effect(object curremp = null)
    {
        if (!Dm.eq(emped, curremp)) //Don't fix it if it's been EMP'd again
            return FALSE;
        emped = FALSE;
        set_on(TRUE);
        return TRUE;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:670
    public virtual void make_silly()
    {
        name = Dm.text("\\improper Little-Crew: Assistant's First Radio");
        icon_state = "walkieian";
        desc = "A Little-Crew branded toy radio in the shape of a lovable pet. After Little-Crew HQ was hit with a Donksoft Nuke, these have become collector's items!";
        overlay_speaker_idle = null;
        overlay_mic_idle = null;
        overlay_mic_active = null;
    }
}

///////////////////////////////
//////////Borg Radios//////////
///////////////////////////////
//Giving borgs their own radio to have some more room to work with -Sieve

// tg: code/game/objects/items/devices/radio/radio.dm:683
public partial class obj_item_radio_borg : obj_item_radio
{
    // tg: code/game/objects/items/devices/radio/radio.dm:690
    public override void resetChannels()
    {
        base.resetChannels();
        if (!iscyborg(loc))
            return;
        mob_living_silicon_robot borg = (mob_living_silicon_robot)loc;
        foreach (object ch_name in Dm.each(borg.model?.radio_channels))
            channels[ch_name] = TRUE;
    }
}

// tg: code/game/objects/items/devices/radio/radio.dm:698
public partial class obj_item_radio_borg_syndicate : obj_item_radio_borg
{
    // tg: code/game/objects/items/devices/radio/radio.dm:702
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_frequency(FREQ_SYNDICATE);
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/radio.dm:706
public partial class obj_item_radio_off : obj_item_radio // Station bounced radios, their only difference is spawning with the speakers off, this was made to help the lag.
{
    // tg: code/game/objects/items/devices/radio/radio.dm:709
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_listening(FALSE);
        return dot;
    }
}

// RADIOS USED BY BROADCASTING
// tg: code/game/objects/items/devices/radio/radio.dm:714
public partial class obj_item_radio_entertainment : obj_item_radio
{
    // tg: code/game/objects/items/devices/radio/radio.dm:721
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_frequency(FREQ_ENTERTAINMENT);
        return dot;
    }
}

// tg: code/game/objects/items/devices/radio/radio.dm:725
public partial class obj_item_radio_entertainment_speakers : obj_item_radio_entertainment // Used inside of the entertainment monitors, not to be used as a actual item
{
    // tg: code/game/objects/items/devices/radio/radio.dm:729
    public virtual void toggle_mute()
    {
        should_be_listening = Dm.num(!Dm.truthy(should_be_listening));
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:732
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_broadcasting(FALSE);
        set_listening(TRUE);
        if (wires != null)
            Waiting.NoSleep(wires.cut(WIRE_TX));
        return dot;
    }

    // tg: code/game/objects/items/devices/radio/radio.dm:738
    public override async DmTask on_receive_message(DmList data = null)
    {
        playsound(this, SFX_MUFFLED_SPEECH, 60, TRUE, -4, ignore_walls: FALSE, volume_preference: typeof(datum_preference_numeric_volume_sound_radio_noise));

        await base.on_receive_message(data);
    }
}

// tg: code/game/objects/items/devices/radio/radio.dm:753
public partial class obj_item_radio_entertainment_microphone : obj_item_radio_entertainment // Used inside of a broadcast camera, not to be used as a actual item
{
    // tg: code/game/objects/items/devices/radio/radio.dm:757
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_broadcasting(TRUE);
        set_listening(FALSE);
        if (wires != null)
            Waiting.NoSleep(wires.cut(WIRE_RX));
        return dot;
    }
}

// In case you want to map it in/spawn it for some reason
// tg: code/game/objects/items/devices/radio/radio.dm:772
public partial class obj_item_radio_toy : obj_item_radio
{
    // tg: code/game/objects/items/devices/radio/radio.dm:774
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        make_silly();
        return dot;
    }
}
