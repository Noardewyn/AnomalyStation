// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/taperecorder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file. The universal recorder records what is said near it onto its tape with the tape's time stamps
// (Hear), plays it back through its own say at the recorded pace, prints the transcript on paper and ejects the tape, through its
// radial menu (tg's radial_taperecorder.dmi), its alt-click and its four GAME_VERBs; its states (empty, idle, recording, playing)
// and its hiss loop show and sound. The tape flips sides (tg's `_reverse` state), unspools (the `ribbonoverlay` overlay; a fire
// unspools it) and is wound back with a screwdriver; the random tapes pick a colour, the dyed one is tinted. The GAME_VERB wrappers
// are SSverbs.invoke_verb's (controllers/subsystem/verbs.dm:36: no caller with a client, no body; the body runs as INVOKE_ASYNC, as
// iv_drip.cs's). The var blocks are generated (D111).

// tg: code/game/objects/items/devices/taperecorder.dm:1
public partial class obj_item_taperecorder : obj_item
{
    // tg: code/game/objects/items/devices/taperecorder.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (starting_tape_type != null)
            mytape = Dm.@new(starting_tape_type, this) as obj_item_tape;
        soundloop = Dm.@new<datum_looping_sound_tape_recorder_hiss>(this);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:40
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref soundloop);
        QDEL_NULL(ref mytape);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:45
    public virtual object readout()
    {
        if (mytape != null)
        {
            if (Dm.truthy(playing))
                return span_notice(I18n.T("<b>PLAYING</b>"));
            else
            {
                float time = mytape.used_capacity / 10; //deciseconds / 10 = seconds
                float mins = round(time / 60);
                float secs = time - mins * 60;
                return span_notice(I18n.T("<b>[]</b>m <b>[]</b>s", mins, secs));
            }
        }
        return span_notice(I18n.T("<b>NO TAPE INSERTED</b>"));
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:56
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (in_range(this, user) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("The display reads:")));
            Dm.append(ref dot, Dm.text("[]", readout()));
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:62
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        play();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:66
    public virtual void update_available_icons()
    {
        icons_available = Dm.list();

        if (!Dm.truthy(playing) && !Dm.truthy(recording))
        {
            Dm.append(ref icons_available, Dm.list(Dm.kv("Record", image(icon: radial_icon_file, icon_state: "record"))));
            Dm.append(ref icons_available, Dm.list(Dm.kv("Play", image(icon: radial_icon_file, icon_state: "play"))));
            if (Dm.truthy(canprint) && (mytape?.storedinfo.len ?? 0) != 0)
                Dm.append(ref icons_available, Dm.list(Dm.kv("Print Transcript", image(icon: radial_icon_file, icon_state: "print"))));
        }

        if (Dm.truthy(playing) || Dm.truthy(recording))
            Dm.append(ref icons_available, Dm.list(Dm.kv("Stop", image(icon: radial_icon_file, icon_state: "stop"))));

        if (mytape != null)
            Dm.append(ref icons_available, Dm.list(Dm.kv("Eject", image(icon: radial_icon_file, icon_state: "eject"))));
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:81
    public virtual void update_sound()
    {
        if (!Dm.truthy(playing) && !Dm.truthy(recording))
            soundloop.stop();
        else
            soundloop.start();
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:87
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (mytape != null || !(tool is obj_item_tape))
            return NONE;
        if (tool is obj_item_music_tape)
        {
            balloon_alert(user, I18n.T("won't fit!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        mytape = tool as obj_item_tape;
        balloon_alert(user, I18n.T("inserted []", mytape));
        playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_close.ogg"), 50, FALSE);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:101
    public virtual async DmTask eject(mob user = null)
    {
        if (mytape == null)
        {
            balloon_alert(user, I18n.T("no tape!"));
            return;
        }
        if (Dm.truthy(playing))
        {
            balloon_alert(user, I18n.T("stop the tape first!"));
            return;
        }
        playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_open.ogg"), 50, FALSE);
        balloon_alert(user, I18n.T("ejected []", mytape));
        stop();
        await user.put_in_hands(mytape);
        mytape = null;
        update_appearance();
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:115
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        mytape?.unspool(); //Fires unspool the tape, which makes sense if you don't think about it
        return await base.fire_act(exposed_temperature, exposed_volume);
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/items/devices/taperecorder.dm:120
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc != user || mytape == null || !Dm.truthy(user.is_holding(this)))
            return await base.attack_hand(user, modifiers, __extra);
        await eject(user);
        return null;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:125
    public virtual object can_use(mob user = null)
    {
        if (user != null && ismob(user))
        {
            if (!Dm.truthy(user.incapacitated))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:132  GAME_VERB(/obj/item/taperecorder, ejectverb, "Eject Tape", null)
    public virtual void ejectverb(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_ejectverb(Dm.list()));
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:132  (the verb's body, __gvb_ejectverb)
    public virtual async DmTask __gvb_ejectverb(DmList structured_args = null)
    {
        if (!Dm.truthy(can_use(usr)))
        {
            balloon_alert(usr, I18n.T("can't use!"));
            return;
        }
        if (mytape == null)
        {
            balloon_alert(usr, I18n.T("no tape!"));
            return;
        }
        await eject(usr);
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:143
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (mytape == null)
        {
            icon_state = "taperecorder_empty";
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(recording))
        {
            icon_state = "taperecorder_recording";
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(playing))
        {
            icon_state = "taperecorder_playing";
            return await base.update_icon_state(__extra);
        }
        icon_state = "taperecorder_idle";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:157
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        message_mods ??= Dm.list();
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (Dm.truthy(message_mods[MODE_RELAY]) || mytape == null || speaker is obj_item_taperecorder)
            return dot;

        Dm.append(ref mytape.timestamp, mytape.used_capacity);
        Dm.append(ref mytape.storedinfo, Dm.text("\\[[]\\] []: []", time2text(mytape.used_capacity, "mm:ss", NO_TIMEZONE), speaker.get_voice(), raw_message));
        return dot;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:166  GAME_VERB(/obj/item/taperecorder, record, "Start Recording", null)
    public virtual void record(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_record(Dm.list()));
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:166  (the verb's body, __gvb_record)
    public virtual async DmTask __gvb_record(DmList structured_args = null)
    {
        if (!Dm.truthy(can_use(usr)))
        {
            balloon_alert(usr, I18n.T("can't use!"));
            return;
        }
        if (mytape == null || Dm.truthy(mytape.unspooled))
        {
            balloon_alert(usr, I18n.T("no spooled tape!"));
            return;
        }
        if (Dm.truthy(recording))
        {
            balloon_alert(usr, I18n.T("stop recording first!"));
            return;
        }
        if (Dm.truthy(playing))
        {
            balloon_alert(usr, I18n.T("already playing!"));
            return;
        }

        playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_play.ogg"), 50, FALSE);

        if (mytape.used_capacity < mytape.max_capacity)
        {
            recording = TRUE;
            become_hearing_sensitive();
            balloon_alert(usr, I18n.T("started recording"));
            update_sound();
            update_appearance();
            float used = mytape.used_capacity; //to stop runtimes when you eject the tape
            float max = mytape.max_capacity;
            while (Dm.truthy(recording) && used < max)
            {
                mytape.used_capacity += 1 * SECONDS;
                used += 1 * SECONDS;
                if (max - used < time_left_warning && !Dm.truthy(time_warned))
                {
                    time_warned = TRUE;
                    balloon_alert(usr, I18n.T("[] second\\s left", (max - used) / 10));
                }
                await sleep(1 * SECONDS);
            }
            if (used >= max)
            {
                balloon_alert(usr, I18n.T("tape full!"));
                await sleep(1 * SECONDS); //prevent balloon alerts layering over the top of each other
            }
            stop();
        }
        else
        {
            balloon_alert(usr, I18n.T("tape full!"));
            playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_stop.ogg"), 50, FALSE);
        }
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:207  GAME_VERB(/obj/item/taperecorder, stop, "Stop", null)
    public virtual void stop(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.DetachSync(() => __gvb_stop(Dm.list()));
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:207  (the verb's body, __gvb_stop)
    public virtual void __gvb_stop(DmList structured_args = null)
    {
        if (!Dm.truthy(can_use(usr)))
        {
            balloon_alert(usr, I18n.T("can't use!"));
            return;
        }

        if (Dm.truthy(recording))
        {
            playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_stop.ogg"), 50, FALSE);
            balloon_alert(usr, I18n.T("stopped recording"));
            recording = FALSE;
            lose_hearing_sensitivity();
        }
        else if (Dm.truthy(playing))
        {
            playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_stop.ogg"), 50, FALSE);
            balloon_alert(usr, I18n.T("stopped playing"));
            playing = FALSE;
        }
        time_warned = FALSE;
        update_appearance();
        update_sound();
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:226  GAME_VERB(/obj/item/taperecorder, play, "Play Tape", null)
    public virtual void play(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_play(Dm.list()));
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:226  (the verb's body, __gvb_play)
    public virtual async DmTask __gvb_play(DmList structured_args = null)
    {
        if (!Dm.truthy(can_use(usr)))
        {
            balloon_alert(usr, I18n.T("can't use!"));
            return;
        }
        if (mytape == null || Dm.truthy(mytape.unspooled))
        {
            balloon_alert(usr, I18n.T("no spooled tape!"));
            return;
        }
        if (Dm.truthy(recording))
        {
            balloon_alert(usr, I18n.T("stop recording first!"));
            return;
        }
        if (Dm.truthy(playing))
        {
            balloon_alert(usr, I18n.T("already playing!"));
            return;
        }
        if ((mytape.storedinfo?.len ?? 0) <= 0)
        {
            balloon_alert(usr, I18n.T("[] is empty!", mytape));
            return;
        }

        playing = TRUE;
        update_appearance();
        update_sound();
        balloon_alert(usr, I18n.T("started playing"));
        playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_play.ogg"), 50, FALSE);
        float used = mytape.used_capacity; //to stop runtimes when you eject the tape
        float max = mytape.max_capacity;
        // for(var/i = 1, used <= max, sleep(playsleepseconds)): the loop's step sleeps, so it is the body's last statement
        float i = 1;
        while (used <= max)
        {
            if (mytape == null)
                break;
            if (playing == FALSE)
                break;
            if (mytape.storedinfo.len < i)
            {
                balloon_alert(usr, I18n.T("recording ended"));
                await stoplag(1 * SECONDS); //prevents multiple balloon alerts covering each other
                break;
            }
            await say(Dm.text("[]", mytape.storedinfo[i]), sanitize: FALSE, message_mods: Dm.list(Dm.kv(MODE_SEQUENTIAL, TRUE))); //We want to display this properly, don't double encode
            if (mytape.storedinfo.len < i + 1)
            {
                playsleepseconds = 1;
                await sleep(1 * SECONDS);
            }
            else
                playsleepseconds = Dm.max(Dm.num(mytape.timestamp[i + 1]) - Dm.num(mytape.timestamp[i]), 1 * SECONDS);
            if (playsleepseconds > 14 * SECONDS)
            {
                await sleep(1 * SECONDS);
                await say(I18n.T("Skipping [] seconds of silence.", playsleepseconds / 10), message_mods: Dm.list(Dm.kv(MODE_SEQUENTIAL, TRUE)));
                playsleepseconds = 1 * SECONDS;
            }
            i++;
            await sleep(playsleepseconds);
        }

        stop();
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:275
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (mytape == null)
        {
            balloon_alert(user, I18n.T("it's empty!"));
            return null;
        }

        update_available_icons();
        if (Dm.truthy(icons_available))
        {
            object selection = await show_radial_menu(user, this, icons_available, radius: 38, require_near: TRUE, tooltips: TRUE);
            if (!Dm.truthy(selection))
                return null;
            object sw__1 = selection;
            if (Dm.eq(sw__1, "Stop"))
                stop();
            else if (Dm.eq(sw__1, "Record"))
                record();
            else if (Dm.eq(sw__1, "Play"))
                play();
            else if (Dm.eq(sw__1, "Print Transcript"))
                print_transcript();
            else if (Dm.eq(sw__1, "Eject"))
                await eject(user);
        }
        return null;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:297  GAME_VERB(/obj/item/taperecorder, print_transcript, "Print Transcript", null)
    public virtual void print_transcript(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_print_transcript(Dm.list()));
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:297  (the verb's body, __gvb_print_transcript)
    public virtual async DmTask __gvb_print_transcript(DmList structured_args = null)
    {
        DmList transcribed_info = mytape.storedinfo;
        if (!Dm.truthy(length(transcribed_info)))
        {
            balloon_alert(usr, I18n.T("tape is empty!"));
            return;
        }
        if (!Dm.truthy(canprint))
        {
            balloon_alert(usr, I18n.T("can't print that fast!"));
            return;
        }
        if (!Dm.truthy(can_use(usr)))
        {
            balloon_alert(usr, I18n.T("can't use!"));
            return;
        }
        if (mytape == null || Dm.truthy(mytape.unspooled))
        {
            balloon_alert(usr, I18n.T("no spooled tape!"));
            return;
        }
        if (Dm.truthy(recording))
        {
            balloon_alert(usr, I18n.T("stop recording first!"));
            return;
        }
        if (Dm.truthy(playing))
        {
            balloon_alert(usr, I18n.T("already playing!"));
            return;
        }

        string transcribed_text = I18n.T("<b>Transcript:</b><br><br>");
        float page_count = 1;

        string tape_name = mytape.name;
        string initial_tape_name = Dm.initial(mytape, (atom p) => p.name);
        string paper_name = I18n.T("paper- '[] Transcript'", tape_name == initial_tape_name ? I18n.T("Tape") : Dm.text("[]", tape_name));

        foreach (object transcript_excerpt in Dm.each(transcribed_info))
        {
            float excerpt_length = Dm.num(length(transcript_excerpt));

            // Very unexpected. Better abort non-gracefully.
            if (excerpt_length > MAX_PAPER_LENGTH)
            {
                balloon_alert(usr, I18n.T("data corrupted, can't print!"));
                throw Dm.CRASH(Dm.text("Transcript entry has more than [] chars: [] chars", MAX_PAPER_LENGTH, excerpt_length), "code/game/objects/items/devices/taperecorder.dm", 332);
            }

            // If we're going to overflow the paper's length, print the current transcribed text out first and reset to prevent us
            // going over the paper char count.
            if ((Dm.num(length(transcribed_text)) + excerpt_length) > MAX_PAPER_LENGTH)
            {
                obj_item_paper transcript_paper__1 = Dm.@new<obj_item_paper>(get_turf(this));
                transcript_paper__1.add_raw_text(transcribed_text);
                transcript_paper__1.name = I18n.T("[] page []", paper_name, page_count);
                transcript_paper__1.update_appearance();
                transcribed_text = "";
                page_count++;
            }

            transcribed_text += Dm.text("[]<br>", transcript_excerpt);
        }

        obj_item_paper transcript_paper = Dm.@new<obj_item_paper>(get_turf(this));
        transcript_paper.add_raw_text(transcribed_text);
        transcript_paper.name = I18n.T("[] page []", paper_name, page_count);
        transcript_paper.update_appearance();

        balloon_alert(usr, I18n.T("transcript printed\n[] page\\s", page_count));
        playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_print.ogg"), 50, FALSE);

        // Can't put the entire stack into their hands if there's multple pages, but hey we can at least put one page in.
        await usr.put_in_hands(transcript_paper);
        canprint = FALSE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(canprint__varset), TRUE), 30 * SECONDS, file: "code/game/objects/items/devices/taperecorder.dm", line: 357);
    }

    /// VARSET_CALLBACK(src, canprint, TRUE)'s target (taperecorder.dm:357): ___callbackvarset's datum branch (varset_callback.dm:8-23)
    /// for this var (___callbackvarset is not declared, the GAP(lead) of modules/assembly/doorcontrol.cs).
    public virtual void canprint__varset(object var_value)
    {
        canprint = Dm.num(var_value);
    }
}

// tg: code/game/objects/items/devices/taperecorder.dm:363
public partial class obj_item_tape : obj_item
{
    // tg: code/game/objects/items/devices/taperecorder.dm:393
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        initial_icon_state = icon_state; //random tapes will set this after choosing their icon

        object mycolor = random_short_color();
        name += Dm.text(" ([])", mycolor); //multiple tapes can get confusing fast
        if (icon_state == "tape_greyscale")
            Waiting.NoSleep(add_atom_colour(Dm.text("#[]", mycolor), FIXED_COLOUR_PRIORITY));

        if (prob(50))
            tapeflip();
        return dot;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:405
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(unspooled))
            Dm.append(ref dot, span_notice(I18n.T("It looks like the tape is unspooled. A screwdriver might fix this.")));
        return dot;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:410
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        unspool();
        await base.fire_act(exposed_temperature, exposed_volume);
        return null;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:414
    public virtual void update_available_icons()
    {
        icons_available = Dm.list();

        if (!Dm.truthy(unspooled))
            Dm.append(ref icons_available, Dm.list(Dm.kv("Unwind tape", image(icon: radial_icon_file, icon_state: "tape_unwind"))));
        Dm.append(ref icons_available, Dm.list(Dm.kv("Flip tape", image(icon: radial_icon_file, icon_state: "tape_flip"))));
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:421
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        update_available_icons();
        if (Dm.truthy(icons_available))
        {
            object selection = await show_radial_menu(user, this, icons_available, radius: 38, require_near: TRUE, tooltips: TRUE);
            if (!Dm.truthy(selection))
                return null;
            object sw__1 = selection;
            if (Dm.eq(sw__1, "Flip tape"))
            {
                if (loc != user)
                    return null;
                tapeflip();
                balloon_alert(user, I18n.T("flipped tape"));
                playsound(this, Dm.resource("sound/items/taperecorder/tape_flip.ogg"), 70, FALSE);
            }
            else if (Dm.eq(sw__1, "Unwind tape"))
            {
                if (loc != user)
                    return null;
                unspool();
                balloon_alert(user, I18n.T("unspooled tape"));
            }
        }
        return null;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:440
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        if (prob(50))
            tapeflip();
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        return dot;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:445
    public virtual void unspool()
    {
        //Let's not add infinite amounts of overlays when our fire_act is called repeatedly
        if (!Dm.truthy(unspooled))
            add_overlay("ribbonoverlay");
        unspooled = TRUE;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:451
    public virtual void respool()
    {
        cut_overlay("ribbonoverlay");
        unspooled = FALSE;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:455
    public virtual void tapeflip()
    {
        //first we save a copy of our current side
        DmList storedinfo_currentside = storedinfo.Copy();
        DmList timestamp_currentside = timestamp.Copy();
        float used_capacity_currentside = used_capacity;
        //then we overwite our current side with our other side
        storedinfo = storedinfo_otherside.Copy();
        timestamp = timestamp_otherside.Copy();
        used_capacity = used_capacity_otherside;
        //then we overwrite our other side with the saved side
        storedinfo_otherside = storedinfo_currentside.Copy();
        timestamp_otherside = timestamp_currentside.Copy();
        used_capacity_otherside = used_capacity_currentside;

        if (Dm.eq(icon_state, initial_icon_state))
            icon_state = Dm.text("[]_reverse", initial_icon_state);
        else if (icon_state == Dm.text("[]_reverse", initial_icon_state)) //so flipping doesn't overwrite an unexpected icon_state (e.g. an admin's)
            icon_state = (string)initial_icon_state;
    }

    // tg: code/game/objects/items/devices/taperecorder.dm:474
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(unspooled))
            return FALSE;
        balloon_alert(user, I18n.T("respooling tape..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 12 * SECONDS)))
        {
            balloon_alert(user, I18n.T("respooling failed!"));
            return FALSE;
        }
        balloon_alert(user, I18n.T("tape respooled"));
        respool();
        return null;
    }
}

//Random colour tapes
// tg: code/game/objects/items/devices/taperecorder.dm:485
public partial class obj_item_tape_random : obj_item_tape
{
    // tg: code/game/objects/items/devices/taperecorder.dm:488
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = Dm.text("tape_[]", pick("white", "blue", "red", "yellow", "purple", "greyscale"));
        object dot = base.Initialize(mapload, __extra);
        return dot;
    }
}
