// Translated from tgstation (snapshot 1d99bd7f): code/datums/holocall.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the crew's holocalls between holopads (the caller's camera eye is seen from a ghost's orbit,
// DV382), the recorded messages and the holodisks with their presets. testing() is a debug build's log line (not compiled in tg's
// release builds) and leaves no C#. The var blocks are generated (D111).

// tg: code/datums/holocall.dm:1
public partial class mob_eye_camera_remote_holo : mob_eye_camera_remote
{
    // tg: code/datums/holocall.dm:1
    public override void setLoc(object destination = null, float? force_update = null)
    {
        // If we're moving outside the space of our projector, then just... don't
        obj_machinery_holopad H = origin_ref?.resolve() as obj_machinery_holopad;
        if (H == null || !Waiting.NoSleep(H.move_hologram(user_ref?.resolve() as datum, destination as turf)))
        {
            sprint = Dm.initial(GetType(), (mob_eye_camera_remote e) => e.sprint); // Reset sprint so it doesn't balloon in our calling proc
            return;
        }
        base.setLoc(destination, force_update);
    }
}

// tg: code/datums/holocall.dm:9
public partial class obj_machinery_holopad : obj_machinery
{
    // tg: code/datums/holocall.dm:9
    public override async DmTask remove_eye_control(mob_living user = null)
    {
        if (user.remote_control is not mob_eye_camera_remote eye)
            throw Dm.CRASH("Attempted to remove eye control from non-camera eye. Something has gone horribly wrong.", "code/datums/holocall.dm", 12);
        await eye.assign_user(null);
    }
}

//this datum manages its own references

// tg: code/datums/holocall.dm:17
public partial class datum_holocall : datum
{
    //creates a holocall made by `call_source` from `calling_pad` to `callees`
    // tg: code/datums/holocall.dm:40
    [DmParams("call_source", "calling_pad", "callees", "elevated_access")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living call_source = Dm.arg<mob_living>(__extra, 0);
        obj_machinery_holopad calling_pad = Dm.arg<obj_machinery_holopad>(__extra, 1);
        DmList callees = Dm.arg<DmList>(__extra, 2);
        object elevated_access = __extra.Length > 3 ? __extra[3] : FALSE;
        call_start_time = world.time;
        user = call_source;
        calling_pad.outgoing_call = this;
        calling_holopad = calling_pad;
        head_call = Dm.truthy(elevated_access) ? TRUE : FALSE;
        dialed_holopads = Dm.list();

        foreach (obj_machinery_holopad connected_holopad in Dm.each<obj_machinery_holopad>(callees))
        {
            if (!QDELETED(connected_holopad) && Dm.truthy(connected_holopad.is_operational))
            {
                Dm.append(ref dialed_holopads, connected_holopad);
                if (Dm.truthy(head_call))
                {
                    if (Dm.truthy(connected_holopad.secure))
                    {
                        await calling_pad.say("Auto-connection refused, falling back to call mode.");
                        await connected_holopad.say("Incoming call.");
                    }
                    else
                        await connected_holopad.say("Incoming connection.");
                }
                else
                    await connected_holopad.say("Incoming call.");
                connected_holopad.set_holocall(this);
            }
        }

        if (dialed_holopads.len == 0)
        {
            await calling_pad.say("Connection failure.");
            qdel(this);
            return;
        }

        // testing("Holocall started"): a debug build's log line
    }

    //cleans up ALL references :)
    // tg: code/datums/holocall.dm:67
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref hangup);
        QDEL_NULL(ref eye);

        if (connected_holopad != null && !QDELETED(hologram))
        {
            hologram = null;
            connected_holopad.clear_holo(user);
        }

        user = null;

        //Hologram survived holopad destro
        if (!QDELETED(hologram))
        {
            hologram.HC = null;
            QDEL_NULL(ref hologram);
        }
        hologram = null;

        foreach (obj_machinery_holopad dialed_holopad in Dm.each<obj_machinery_holopad>(dialed_holopads.Copy()))
            dialed_holopad.set_holocall(this, false);

        dialed_holopads.Cut();

        if (calling_holopad != null) //if the call is answered, then calling_holopad wont be in dialed_holopads and thus wont have set_holocall(src, FALSE) called
        {
            calling_holopad.callee_hung_up();
            calling_holopad = null;
        }
        if (connected_holopad != null)
        {
            connected_holopad.SetLightsAndPower();
            connected_holopad = null;
        }

        // testing("Holocall destroyed"): a debug build's log line

        return base.Destroy(force);
    }

    //Gracefully disconnects a holopad `H` from a call. Pads not in the call are ignored. Notifies participants of the disconnection
    // tg: code/datums/holocall.dm:101
    public virtual void Disconnect(obj_machinery_holopad H = null)
    {
        if (H == connected_holopad)
        {
            area A = get_area(connected_holopad) as area;
            Waiting.NoSleep(calling_holopad.say(Dm.text("[] holopad disconnected.", A)));
        }
        else if (H == calling_holopad && connected_holopad != null)
            Waiting.NoSleep(connected_holopad.say(Dm.text("[] disconnected.", user)));

        ConnectionFailure(H, true);
    }

    //Forcefully disconnects disconnected_holopad from a call. Pads not in the call are ignored.
    // tg: code/datums/holocall.dm:112
    public virtual void ConnectionFailure(obj_machinery_holopad disconnected_holopad = null, bool graceful = false)
    {
        if (disconnected_holopad == connected_holopad || disconnected_holopad == calling_holopad)
        {
            if (!graceful && disconnected_holopad != calling_holopad)
                Waiting.NoSleep(calling_holopad.say("Connection failure."));
            qdel(this);
            return;
        }

        disconnected_holopad.set_holocall(this, false);

        Dm.remove(ref dialed_holopads, disconnected_holopad);
        if (dialed_holopads.len == 0)
        {
            if (graceful)
                Waiting.NoSleep(calling_holopad.say("Call rejected."));
            qdel(this);
        }
    }

    ///Answers a call made to answering_holopad which cannot be the calling holopad. Pads not in the call are ignored
    // tg: code/datums/holocall.dm:130
    public virtual async DmTask Answer(obj_machinery_holopad answering_holopad = null)
    {
        if (answering_holopad == calling_holopad)
            throw Dm.CRASH("How cute, a holopad tried to answer itself.", "code/datums/holocall.dm", 133);

        if (!Dm.@in(answering_holopad, dialed_holopads))
            return;

        if (connected_holopad != null)
            throw Dm.CRASH("Multi-connection holocall", "code/datums/holocall.dm", 139);

        foreach (obj_machinery_holopad other_dialed_holopad in Dm.each<obj_machinery_holopad>(dialed_holopads.Copy()))
        {
            if (other_dialed_holopad == answering_holopad)
                continue;
            Disconnect(other_dialed_holopad);
        }

        foreach (datum_holocall previously_answered_holocall in Dm.each<datum_holocall>(answering_holopad.holo_calls?.Copy())) //disconnect the other holocalls answering_holopad is occupied with
        {
            if (previously_answered_holocall != this)
                previously_answered_holocall.Disconnect(answering_holopad);
        }

        connected_holopad = answering_holopad;

        if (!Check())
            return;

        calling_holopad.callee_picked_up();
        hologram = await answering_holopad.activate_holo(user);
        hologram.HC = this;

        eye = Dm.@new<mob_eye_camera_remote_holo>(get_turf(answering_holopad), answering_holopad);
        await eye.assign_user(user);

        hangup = Dm.@new<datum_action_innate_end_holocall>(eye, this);
        await hangup.Grant(user);
        playsound(answering_holopad, Dm.resource("sound/machines/ping.ogg"), 100);
        await answering_holopad.say("Connection established.");
    }

    //Checks the validity of a holocall and qdels itself if it's not. Returns TRUE if valid, FALSE otherwise
    // tg: code/datums/holocall.dm:168
    public virtual bool Check()
    {
        foreach (obj_machinery_holopad dialed_holopad in Dm.each<obj_machinery_holopad>(dialed_holopads.Copy()))
        {
            if (!Dm.truthy(dialed_holopad.is_operational))
                ConnectionFailure(dialed_holopad);
        }

        if (QDELETED(this))
            return false;

        bool dot = !QDELETED(user) && !Dm.truthy(user.incapacitated) && !QDELETED(calling_holopad) && Dm.truthy(calling_holopad.is_operational) && user.loc == calling_holopad.loc;

        if (dot)
        {
            if (connected_holopad == null)
            {
                dot = world.time < (Dm.num(call_start_time) + HOLOPAD_MAX_DIAL_TIME);
                if (!dot)
                    Waiting.NoSleep(calling_holopad.say("No answer received."));
            }
        }

        if (!dot)
            qdel(this);
        return dot;
    }
}

// tg: code/datums/holocall.dm:188
public partial class datum_action_innate_end_holocall : datum_action_innate
{
    // tg: code/datums/holocall.dm:194
    [DmParams("Target", "HC")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        hcall = Dm.arg<datum_holocall>(__extra, 1);
    }

    // tg: code/datums/holocall.dm:198
    public override async DmTask<object> Activate()
    {
        hcall.Disconnect(hcall.calling_holopad);
        await DmTask.CompletedTask;
        return null;
    }
}

//RECORDS
// tg: code/datums/holocall.dm:203
public partial class datum_holorecord : datum
{
    // tg: code/datums/holocall.dm:209
    public virtual void set_caller_image(mob user = null)
    {
        object olddir = user.dir;
        Waiting.NoSleep(user.setDir(SOUTH));
        caller_image = image(user);
        Waiting.NoSleep(user.setDir(olddir));
    }
}

// tg: code/datums/holocall.dm:215
public partial class obj_item_disk_holodisk : obj_item_disk
{
    // tg: code/datums/holocall.dm:227
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(preset_record_text))
            Waiting.Detach(build_record()); // INVOKE_ASYNC
        return dot;
    }

    // tg: code/datums/holocall.dm:232
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref record);
        return base.Destroy(force);
    }

    // tg: code/datums/holocall.dm:236
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_disk_holodisk holodisk_original)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (holodisk_original.record == null)
        {
            to_chat(user, span_warning(I18n.T("[] has no record on it!", holodisk_original)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (record == null)
            record = Dm.@new<datum_holorecord>();

        record.caller_name = holodisk_original.record.caller_name;
        record.caller_image = holodisk_original.record.caller_image;
        record.entries = holodisk_original.record.entries.Copy();
        record.language = holodisk_original.record.language;
        to_chat(user, span_notice(I18n.T("You copy the record from [] to [] by connecting the ports!", holodisk_original, this)));
        name = holodisk_original.name;

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/datums/holocall.dm:257
    public virtual async DmTask build_record()
    {
        record = Dm.@new<datum_holorecord>();
        DmList lines = splittext(preset_record_text, "\n") as DmList;
        foreach (object line in Dm.each(lines))
        {
            string prepared_line = Dm.str(trim(line));
            if (!Dm.truthy(length(prepared_line)))
                continue;
            float splitpoint = Dm.num(findtext(prepared_line, " "));
            if (!Dm.truthy(splitpoint))
                continue;
            string command = Dm.str(copytext(prepared_line, 1, splitpoint));
            string value = Dm.str(copytext(prepared_line, splitpoint + length(Dm.str(prepared_line[(int)splitpoint - 1]))));
            switch (command)
            {
                case "DELAY":
                {
                    object delay_value = text2num(value);
                    if (!Dm.truthy(delay_value))
                        continue;
                    Dm.append(ref record.entries, Dm.list(Dm.list(HOLORECORD_DELAY, delay_value)));
                    break;
                }
                case "NAME":
                    if (!Dm.truthy(record.caller_name))
                        record.caller_name = value;
                    else
                        Dm.append(ref record.entries, Dm.list(Dm.list(HOLORECORD_RENAME, value)));
                    break;
                case "SAY":
                    Dm.append(ref record.entries, Dm.list(Dm.list(HOLORECORD_SAY, value)));
                    break;
                case "SOUND":
                    Dm.append(ref record.entries, Dm.list(Dm.list(HOLORECORD_SOUND, value)));
                    break;
                case "LANGUAGE":
                {
                    Type lang_type = text2path(value);
                    if (ispath(lang_type, typeof(datum_language)))
                        Dm.append(ref record.entries, Dm.list(Dm.list(HOLORECORD_LANGUAGE, lang_type)));
                    break;
                }
                case "PRESET":
                {
                    Type preset_type = text2path(value);
                    if (ispath(preset_type, typeof(datum_preset_holoimage)))
                        Dm.append(ref record.entries, Dm.list(Dm.list(HOLORECORD_PRESET, preset_type)));
                    break;
                }
            }
        }
        if (preset_image_type == null)
            record.caller_image = image(Dm.resource("icons/mob/simple/animal.dmi"), "old");
        else
        {
            datum_preset_holoimage H = Dm.@new<datum_preset_holoimage>(preset_image_type);
            record.caller_image = await H.build_image();
        }
    }
}

//These build caller image from outfit and some additional data, for use by mappers for ruin holorecords
// tg: code/datums/holocall.dm:298
public partial class datum_preset_holoimage : datum
{
    // tg: code/datums/holocall.dm:304
    public virtual async DmTask<image> build_image()
    {
        image dot;
        if (nonhuman_mobtype != null)
        {
            Type L = nonhuman_mobtype;
            dot = image(Dm.initial(L, (atom a) => a.icon), Dm.initial(L, (atom a) => a.icon_state));
        }
        else
        {
            mob_living_carbon_human_dummy mannequin = await generate_or_wait_for_human_dummy("HOLODISK_PRESET") as mob_living_carbon_human_dummy;
            if (species_type != null)
                mannequin.set_species(species_type);
            if (outfit_type != null)
                await mannequin.equipOutfit(outfit_type, TRUE);
            await mannequin.setDir(SOUTH);
            dot = image(mannequin);
            await unset_busy_human_dummy("HOLODISK_PRESET");
        }
        return dot;
    }
}
