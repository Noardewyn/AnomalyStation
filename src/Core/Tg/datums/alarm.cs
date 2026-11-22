// Translated from tgstation (snapshot 1d99bd7f): code/datums/alarm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. /datum/alarm_handler's vars, New and Destroy (package B, wave 4: /area/New creates one for every area,
// areas.dm:184, and /area/Destroy deletes it); M4b slice 2 (the firelocks' and fire alarms' area alarms): send_alarm,
// clear_alarm and clear_alarm_from_area; the engineering package E6 (D287): the alarm listener (/datum/alarm_listener), the
// alarm lists of the station alert consoles and the Canary program (the AI's and cyborgs' listeners come with them).

//This files deals with the generic sending and receiving of "alarms"
//This is a somewhat blanket term, it covers things like fire/power/atmos alarms, along with some oddballs
//Side effect of how the system is used, these are mostly things that are of interest to ais and borgs
//Though it could easily be expanded to cover other senders/revievers
//The system as a whole differs from reading off a global list in a few ways.
//In that A, it allows us to send cameras for ais/borgs/potentially others to jump to
//And B, it's not like we're giving you all the alarms that have been sent, because of the separate listing for each reviever
//You only receive alarms sent after you start to listen
//Also of note, due to an optimization done on areas, one alarm handler will only ever send one "on" or "off" alarm
//So the whole only receiving stuff sent post creation thing actually matters
//Honestly I'm not sure how much of this is a feature, and how much is just old code
//But I'm leaving it how I found it

///Represents a single source of alarms, one alarm handler will only ever count for one alarm per listener
// tg: code/datums/alarm.dm:15
[DmPath("/datum/alarm_handler")]
public partial class datum_alarm_handler : datum
{
    ///A list of alarm type -> list of areas we currently have alarms in
    // tg: code/datums/alarm.dm:17
    public DmList sent_alarms;
    ///Our source atom
    // tg: code/datums/alarm.dm:19
    public atom source_atom;

    // tg: code/datums/alarm.dm:21
    // (source_atom is object, not tg's declared atom: New type-tests it and reads its type on the failing path, rules 3.4)
    [DmParams("source_atom")]
    public override async DmTask New(params object[] __extra)
    {
        object source_atom = Dm.arg(__extra, 0);
        if (source_atom is atom)
            this.source_atom = source_atom as atom;
        else
        {
            object source_type = (isdatum(source_atom) ? (object)((datum)source_atom).type : "");
            stack_trace(Dm.text("a non atom was passed into alarm_handler! [] []", source_atom, source_type), "code/datums/alarm.dm", 26);
        }
        await base.New(__extra);
    }

    // tg: code/datums/alarm.dm:29
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        foreach ((object alarm_type, object sent_alarm) in Dm.pairs(sent_alarms))
        {
            foreach (area area_to_clear in Dm.each_cast<area>(sent_alarm))
                //Yeet all connected alarms
                clear_alarm_from_area(alarm_type, area_to_clear);
        }
        source_atom = null;
        return base.Destroy(force);
    }

    ///Sends an alarm to any interested things, does some checks to prevent unneeded work
    ///Important to note is that source_atom is not held as a ref, we're used as a proxy to prevent hard deletes
    ///optional_camera should only be used when you have one camera you want to pass along to alarm listeners, most of the time you should have no use for it
    // tg: code/datums/alarm.dm:40
    public virtual object send_alarm(object alarm_type = null, atom use_as_source_atom = null, object optional_camera = null)
    {
        if (use_as_source_atom == null)
            use_as_source_atom = source_atom;
        if (use_as_source_atom == null)
            return null;

        area our_area = get_area(use_as_source_atom) as area;
        float our_z_level = use_as_source_atom.z;

        DmList existing_alarms = LAZYACCESS(sent_alarms, alarm_type) as DmList;
        if (existing_alarms != null)
        {
            if (Dm.@in(our_area, existing_alarms))
                return FALSE;
        }
        else
        {
            LAZYSET(ref sent_alarms, alarm_type, Dm.list());
            existing_alarms = sent_alarms[alarm_type] as DmList; // List will definitely exist because of previous line, no need for lazy macro
        }

        Dm.append(ref existing_alarms, our_area);

        Dm.append_at(our_area.active_alarms, alarm_type, 1);

        if (Dm.truthy(_listen_lookup?[COMSIG_ALARM_TRIGGERED])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_ALARM_TRIGGERED, Dm.list(this, alarm_type, our_area));
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_ALARM_FIRE(alarm_type), ...) (__DEFINES/dcs/helpers.dm:9; signals_global.dm:64, "!alarm_fire [alarm_type]")
        string sig__1 = Dm.text("!alarm_fire []", alarm_type);
        if (Dm.truthy(SSdcs._listen_lookup?[sig__1]))
            SSdcs._SendSignal(sig__1, Dm.list(SSdcs, this, alarm_type, our_area, our_z_level, optional_camera));

        return TRUE;
    }

    ///Clears an alarm from any interested listeners
    // tg: code/datums/alarm.dm:66
    [SignalHandler]
    public virtual object clear_alarm(object alarm_type = null, object use_as_source_atom = null)
    {
        if (use_as_source_atom == null)
            use_as_source_atom = source_atom;
        if (use_as_source_atom == null)
            return null;

        return clear_alarm_from_area(alarm_type, get_area(use_as_source_atom as atom) as area);
    }

    ///Exists so we can request that the alarms from an area are cleared, even if our source atom is no longer in that area
    // tg: code/datums/alarm.dm:77
    public virtual object clear_alarm_from_area(object alarm_type = null, area our_area = null)
    {
        DmList existing_alarms = LAZYACCESS(sent_alarms, alarm_type) as DmList;
        if (existing_alarms == null)
            return FALSE;

        if (!Dm.@in(our_area, existing_alarms))
            return FALSE;

        LAZYREMOVE(ref existing_alarms, our_area);
        if (!Dm.truthy(LAZYLEN(existing_alarms)))
            LAZYREMOVE(ref sent_alarms, alarm_type);

        Dm.remove_at(our_area.active_alarms, alarm_type, 1);
        if (!Dm.truthy(Dm.length(our_area.active_alarms)))
            Dm.remove(ref our_area.active_alarms, alarm_type);

        if (Dm.truthy(_listen_lookup?[COMSIG_ALARM_CLEARED])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_ALARM_CLEARED, Dm.list(this, alarm_type, our_area));
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_ALARM_CLEAR(alarm_type), ...) (__DEFINES/dcs/helpers.dm:9; signals_global.dm:66, "!alarm_clear [alarm_type]")
        string sig__1 = Dm.text("!alarm_clear []", alarm_type);
        if (Dm.truthy(SSdcs._listen_lookup?[sig__1]))
            SSdcs._SendSignal(sig__1, Dm.list(SSdcs, this, alarm_type, our_area));
        return TRUE;
    }
}

// tg: code/datums/alarm.dm:98
public partial class datum_alarm_listener : datum
{
    ///Accepts a list of alarm types to pay attention to, a list of valid z levels, and a list of valid areas. areas and zlevels are ignored if null
    // tg: code/datums/alarm.dm:110
    [DmParams("alarms_to_listen_for", "allowed_z_levels", "allowed_areas")]
    public override async DmTask New(params object[] __extra)
    {
        object alarms_to_listen_for = Dm.arg(__extra, 0);
        this.allowed_z_levels = Dm.arg(__extra, 1) as DmList;
        this.allowed_areas = Dm.arg(__extra, 2) as DmList;
        foreach (object alarm_type in Dm.each(alarms_to_listen_for))
        {
            // COMSIG_GLOB_ALARM_FIRE(alarm_type), COMSIG_GLOB_ALARM_CLEAR(alarm_type) (signals_global.dm:64, :66)
            RegisterSignal(SSdcs, Dm.text("!alarm_fire []", alarm_type), nameof(add_alarm));
            RegisterSignal(SSdcs, Dm.text("!alarm_clear []", alarm_type), nameof(clear_alarm));
        }

        await base.New(__extra);
    }

    ///Adds an alarm to our alarms list, you shouldn't be calling this manually
    ///It should all be handled by the signal listening we do, unless you want to only send an alarm to one listener
    // tg: code/datums/alarm.dm:121
    [SignalHandler]
    public virtual void add_alarm(datum source = null, datum_alarm_handler handler = null, object alarm_type = null, area source_area = null, object source_z = null, object optional_camera = null)
    {
        if (!Dm.truthy(accepting_alarm_changes))
            return;

        if (allowed_z_levels != null && !Dm.@in(source_z, allowed_z_levels))
            return;

        if (allowed_areas != null && !Dm.@in(source_area.type, allowed_areas))
            return;

        DmList alarms_of_our_type = alarms[alarm_type] as DmList;
        if (alarms_of_our_type == null)
        {
            alarms[alarm_type] = Dm.list();
            alarms_of_our_type = alarms[alarm_type] as DmList;
        }

        if (Dm.truthy(alarms_of_our_type[source_area.name]))
        {
            DmList alarm = alarms_of_our_type[source_area.name] as DmList;
            DmList sources = alarm[3] as DmList;
            Dm.combine(ref sources, handler);
            //Return if a source already exists, we don't want to send a signal or add a new entry
            return;
        }

        //We normally directly pass in a ref to the area's camera's list to prevent hanging refs
        DmList cameras = source_area.cameras;
        if (Dm.truthy(optional_camera))
        {
            cameras = Dm.list(optional_camera); // This will cause harddels, so we need to clear manually
            RegisterSignal(optional_camera as datum, COMSIG_QDELETING, nameof(clear_camera_ref), @override: TRUE); //It's just fine to override, cause we clear all refs in the proc
        }

        //This does mean that only the first alarm of that camera type in the area will send a ping, but jesus what else can ya do
        alarms_of_our_type[source_area.name] = Dm.list(source_area, cameras, Dm.list(handler));
        if (Dm.truthy(_listen_lookup?[COMSIG_ALARM_LISTENER_TRIGGERED])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_ALARM_LISTENER_TRIGGERED, Dm.list(this, alarm_type, source_area));
    }

    ///Removes an alarm to our alarms list, you probably shouldn't be calling this manually
    ///It should all be handled by the signal listening we do, unless you want to only remove an alarm to one listener
    // tg: code/datums/alarm.dm:157
    [SignalHandler]
    public virtual void clear_alarm(datum source = null, datum_alarm_handler handler = null, object alarm_type = null, area source_area = null)
    {
        if (!Dm.truthy(accepting_alarm_changes))
            return;

        DmList alarms_of_our_type = alarms[alarm_type] as DmList;

        if (alarms_of_our_type == null)
            return;

        if (!Dm.truthy(alarms_of_our_type[source_area.name]))
            return;

        DmList alarm = alarms_of_our_type[source_area.name] as DmList;
        DmList sources = alarm[3] as DmList;
        Dm.remove(ref sources, handler);

        if (Dm.truthy(Dm.length(sources)))
            return; //Return if there's still sources left, no sense clearing the list or bothering anyone about it

        Dm.remove(ref alarms_of_our_type, source_area.name);

        if (!Dm.truthy(Dm.length(alarms_of_our_type)))
            Dm.remove(ref alarms, alarm_type);

        if (Dm.truthy(_listen_lookup?[COMSIG_ALARM_LISTENER_CLEARED])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_ALARM_LISTENER_CLEARED, Dm.list(this, alarm_type, source_area));
    }

    ///Does what it says on the tin, exists for signal hooking
    // tg: code/datums/alarm.dm:186
    [SignalHandler]
    public virtual void prevent_alarm_changes()
    {
        accepting_alarm_changes = FALSE;
    }

    ///Does what it says on the tin, exists for signal hooking
    // tg: code/datums/alarm.dm:191
    [SignalHandler]
    public virtual void allow_alarm_changes()
    {
        accepting_alarm_changes = TRUE;
    }

    ///Used to manually clear camera refs if one is ref'd directly
    // tg: code/datums/alarm.dm:196
    [SignalHandler]
    public virtual void clear_camera_ref(obj_machinery_camera source = null)
    {
        DmList alarms_cache = alarms; //Cache for sonic speec
        foreach (object alarm_type in Dm.each(alarms_cache))
        {
            DmList alarms_of_type = alarms_cache[alarm_type] as DmList; //Sonic cache speed forads
            foreach (object area_name in Dm.each(alarms_of_type))
            {
                DmList alarm_packet = alarms_of_type[area_name] as DmList;
                DmList cameras = alarm_packet[2] as DmList;
                if (cameras != null)
                    Dm.remove(ref cameras, source); // REF FOUND AND CLEARED BOYSSSS
            }
        }
    }
}
