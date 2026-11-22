// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/_event.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the round's events, the lone operative's; the ghosts' alert of an event's object of interest
// since gaps E1a) but an admin-forced event's options (the event admin setups, /datum/event_admin_setup: the admins' Trigger Event
// panel, the admin tools, D275). The var blocks are generated (D111).

//this singleton datum is used by the events controller to dictate how it selects events
// tg: code/modules/events/_event.dm:4
public partial class datum_round_event_control : datum
{
    // tg: code/modules/events/_event.dm:1  #define RANDOM_EVENT_ADMIN_INTERVENTION_TIME (20 SECONDS)
    private const float RANDOM_EVENT_ADMIN_INTERVENTION_TIME = 20 * SECONDS;

    // tg: code/modules/events/_event.dm:59
    public override async DmTask New(params object[] __extra)
    {
        if (!Dm.truthy(length(admin_setup)))
            return;
        DmList admin_setup_types = admin_setup.Copy();
        admin_setup.Cut();
        foreach (Type admin_setup_type in Dm.each_cast<Type>(admin_setup_types))
            Dm.append(ref admin_setup, await Dm.new_async(admin_setup_type, this));
    }

    /// Returns true if event can run in current map
    // tg: code/modules/events/_event.dm:72
    public virtual object valid_for_map()
    {
        if (!Dm.truthy(map_flags))
            return TRUE;
        if (Dm.truthy(SSmapping.is_planetary()))
        {
            if (Dm.truthy(Dm.bitand(map_flags, EVENT_SPACE_ONLY)))
                return FALSE;
        }
        else
        {
            if (Dm.truthy(Dm.bitand(map_flags, EVENT_PLANETARY_ONLY)))
                return FALSE;
        }
        return TRUE;
    }

    // Checks if the event can be spawned. Used by event controller and "false alarm" event.
    // Admin-created events override this.
    // tg: code/modules/events/_event.dm:85
    public virtual object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        // SHOULD_CALL_PARENT(TRUE)
        allow_magic ??= FALSE;
        if (occurrences >= max_occurrences)
            return FALSE;
        if (earliest_start >= world.time - Dm.num(SSticker.round_start_time))
            return FALSE;
        if (!Dm.truthy(allow_magic) && wizardevent != SSevents.wizardmode)
            return FALSE;
        if (Dm.num(players_amt) < min_players)
            return FALSE;
        if (Dm.truthy(holidayID) && !Dm.truthy(check_holidays(holidayID)))
            return FALSE;
        if (datum_controller_subsystem_shuttle.EMERGENCY_ESCAPED_OR_ENDGAMED())
            return FALSE;
        if (ispath(typepath, typeof(datum_round_event_ghost_role)) && !Dm.truthy(Dm.bitand(GLOB.ghost_role_flags, GHOSTROLE_MIDROUND_EVENT)))
            return FALSE;
        if (Dm.truthy(admin_disabled))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/events/_event.dm:106
    public virtual async DmTask<object> preRunEvent()
    {
        if (!ispath(typepath, typeof(datum_round_event)))
            return EVENT_CANT_RUN;

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_PRE_RANDOM_EVENT, src) & CANCEL_PRE_RANDOM_EVENT (__DEFINES/dcs/helpers.dm:7), expanded inline
        if (Dm.truthy(Dm.bitand(Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_PRE_RANDOM_EVENT]) ? SSdcs._SendSignal(COMSIG_GLOB_PRE_RANDOM_EVENT, Dm.list(SSdcs, this)) : NONE, CANCEL_PRE_RANDOM_EVENT)))
            return EVENT_INTERRUPTED;

        triggering = TRUE;

        // We sleep HERE, in pre-event setup (because there's no sense doing it in run_event() since the event is already running!) for the given amount of time to make an admin has enough time to cancel an event un-fitting of the present round or at least reroll it.
        message_admins(Dm.text("Random Event triggering in []: []. (<a href='byond://?src=[];cancel=1'>CANCEL</a>) (<a href='byond://?src=[];different_event=1'>SOMETHING ELSE</a>)",
            DisplayTimeText(RANDOM_EVENT_ADMIN_INTERVENTION_TIME), name, REF(this), REF(this)));
        await sleep(RANDOM_EVENT_ADMIN_INTERVENTION_TIME);
        object players_amt = get_active_player_count(alive_check: TRUE, afk_check: TRUE, human_check: TRUE);
        if (!Dm.truthy(can_spawn_event(players_amt)))
        {
            message_admins(Dm.text("Second pre-condition check for [] failed, rerolling...", name));
            SSevents.spawnEvent(excluded_event: this);
            return EVENT_INTERRUPTED;
        }

        if (!Dm.truthy(triggering))
            return EVENT_CANCELLED; //admin cancelled
        triggering = FALSE;
        return EVENT_READY;
    }

    // tg: code/modules/events/_event.dm:129
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);
        if (Dm.truthy(href_list["cancel"]))
        {
            if (!Dm.truthy(triggering))
            {
                to_chat(usr, span_admin(I18n.T("You are too late to cancel that event")));
                return null;
            }
            triggering = FALSE;
            message_admins(Dm.text("[] cancelled event [].", key_name_admin(usr), name));
            log_admin_private(Dm.text("[] cancelled event [].", key_name(usr), name));
            SSblackbox.record_feedback("tally", "event_admin_cancelled", 1, typepath);
        }
        if (Dm.truthy(href_list["different_event"]))
        {
            if (!Dm.truthy(triggering))
            {
                to_chat(usr, span_admin(I18n.T("Too late to change events now!")));
                return null;
            }
            triggering = FALSE;
            message_admins(Dm.text("[] chose to have event [] rolled into a different event.", key_name_admin(usr), name));
            log_admin_private(Dm.text("[] rerolled event [].", key_name(usr), name));
            SSblackbox.record_feedback("tally", "event_admin_rerolled", 1, typepath);
            SSevents.spawnEvent(excluded_event: this);
        }
        return null;
    }

    /*
    Runs the event
    * Arguments:
    * - random: shows if the event was triggered randomly, or by on purpose by an admin or an item
    * - announce_chance_override: if the value is not null, overrides the announcement chance when an admin calls an event
    */
    // tg: code/modules/events/_event.dm:155
    public virtual async DmTask<object> run_event(float? random = null, object announce_chance_override = null, float? admin_forced = null, object event_cause = null)
    {
        random ??= FALSE;
        admin_forced ??= FALSE;
        /*
        * We clear our signals first so we don't cancel a wanted event by accident,
        * the majority of time the admin will probably want to cancel a single midround spawned random events
        * and not multiple events called by others admins
        * * In the worst case scenario we can still recall a event which we cancelled by accident, which is much better then to have a unwanted event
        */
        UnregisterSignal(SSdcs, COMSIG_GLOB_RANDOM_EVENT);
        var round_event = await Dm.new_async(typepath, TRUE, this) as datum_round_event;
        if (Dm.truthy(admin_forced) && Dm.truthy(length(admin_setup)))
        {
            //not part of the signal because it's conditional and relies on usr heavily
            // for(var/datum/event_admin_setup/admin_setup_datum in admin_setup) admin_setup_datum.apply_to_event(round_event): the admins'
            // event options (the event admin setups, the admin tools' Trigger Event panel, D275)
            Dm.Deferred("code/modules/events/_event.dm:167", "run_event: an admin-forced event's options (/datum/event_admin_setup, the admin tools)");
        }
        SEND_SIGNAL(COMSIG_CREATED_ROUND_EVENT, Dm.list(this, round_event));
        await round_event.setup();
        round_event.current_players = Dm.num(get_active_player_count(alive_check: 1, afk_check: 1, human_check: 1));
        occurrences++;

        if (announce_chance_override != null)
            round_event.announce_chance = Dm.num(announce_chance_override);

        // testing("[time2text(world.time, "hh:mm:ss", 0)] [round_event.type]") expands to nothing in production (__HELPERS/logging/_logging.dm:49)
        triggering = TRUE;

        if (!Dm.truthy(triggering))
        {
            RegisterSignal(SSdcs, COMSIG_GLOB_RANDOM_EVENT, nameof(stop_random_event));
            round_event.cancel_event = TRUE;
            return round_event;
        }

        triggering = FALSE;
        log_game(Dm.text("[] Event triggering: [] ([]).", Dm.truthy(random) ? "Random" : "Forced", name, typepath));

        if (Dm.truthy(alert_observers))
            round_event.announce_deadchat(random, event_cause);

        SSblackbox.record_feedback("tally", "event_ran", 1, Dm.text("[]", round_event));
        return round_event;
    }

    //Returns the component for the listener
    // tg: code/modules/events/_event.dm:194
    [SignalHandler]
    public virtual object stop_random_event()
    {
        return CANCEL_RANDOM_EVENT;
    }
}

//NOTE: Times are measured in master controller ticks!
// tg: code/modules/events/_event.dm:198
public partial class datum_round_event : datum
{
    //Called first before processing.
    //Allows you to setup your event, such as randomly
    //setting the start_when and or announce_when variables.
    //Only called once.
    //EDIT: if there's anything you want to override within the new() call, it will not be overridden by the time this proc is called.
    //It will only have been overridden by the time we get to announce() start() tick() or end() (anything but setup basically).
    //This is really only for setting defaults which can be overridden later when New() finishes.
    // tg: code/modules/events/_event.dm:230
    public virtual async DmTask<object> setup()
    {
        return null;
    }

    ///Announces the event name to deadchat, override this if what an event should show to deadchat is different to its event name.
    // tg: code/modules/events/_event.dm:235
    public virtual void announce_deadchat(object random = null, object cause = null)
    {
        deadchat_broadcast(Dm.text(" has just been[] triggered[]!", Dm.truthy(random) ? " randomly" : "", Dm.truthy(cause) ? Dm.text(" by []", cause) : ""),
            Dm.text("<b>[]</b>", control.name), message_type: DEADCHAT_ANNOUNCEMENT); //STOP ASSUMING IT'S BADMINS!
    }

    //Called when the tick is equal to the start_when variable.
    //Allows you to start before announcing or vice versa.
    //Only called once.
    // tg: code/modules/events/_event.dm:241
    public virtual async DmTask<object> start()
    {
        return null;
    }

    //Called after something followable has been spawned by an event
    //Provides ghosts a follow link to an atom if possible
    //Only called once.
    // tg: code/modules/events/_event.dm:248
    public virtual void announce_to_ghosts(atom atom_of_interest = null)
    {
        if (Dm.truthy(control.alert_observers) && atom_of_interest != null)
        {
            notify_ghosts(
                I18n.T("[] has an object of interest: []!", control.name, atom_of_interest),
                source: atom_of_interest);
        }
    }

    //Called when the tick is equal to the announce_when variable.
    //Allows you to announce before starting or vice versa.
    //Only called once.
    // tg: code/modules/events/_event.dm:260
    public virtual async DmTask announce__proc(object fake = null)
    {
        return;
    }

    //Called on or after the tick counter is equal to start_when.
    //You can include code related to your event or add your own
    //time stamped events.
    //Called more than once.
    // tg: code/modules/events/_event.dm:267
    public virtual async DmTask tick()
    {
        return;
    }

    //Called on or after the tick is equal or more than end_when
    //You can include code related to the event ending.
    //Do not place spawn() in here, instead use tick() to check for
    //the activeFor variable.
    //For example: if(activeFor == myOwnVariable + 30) doStuff()
    //Only called once.
    // tg: code/modules/events/_event.dm:276
    public virtual async DmTask end()
    {
        return;
    }

    //Do not override this proc, instead use the appropriate procs.
    //This proc will handle the calls to the appropriate procs.
    // tg: code/modules/events/_event.dm:283  (/datum/proc/process is waitfor = FALSE: a ghost role's start polls the ghosts)
    [ShouldNotOverride]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // `.` is never assigned: no box (waiting-model.md 5.4)
    }

    private async DmTask<object> process__body()
    {
        if (!Dm.truthy(processing))
            return null;

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_RANDOM_EVENT, src) & CANCEL_RANDOM_EVENT (__DEFINES/dcs/helpers.dm:7), expanded inline
        if (Dm.truthy(Dm.bitand(Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_RANDOM_EVENT]) ? SSdcs._SendSignal(COMSIG_GLOB_RANDOM_EVENT, Dm.list(SSdcs, this)) : NONE, CANCEL_RANDOM_EVENT)))
        {
            processing = FALSE;
            kill();
            return null;
        }

        if (activeFor == start_when)
        {
            processing = FALSE;
            await start();
            processing = TRUE;
        }

        if (activeFor == announce_when && prob(announce_chance))
        {
            processing = FALSE;
            await announce__proc(FALSE);
            processing = TRUE;
        }

        if (start_when < activeFor && activeFor < end_when)
        {
            processing = FALSE;
            await tick();
            processing = TRUE;
        }

        if (activeFor == end_when)
        {
            processing = FALSE;
            await end();
            processing = TRUE;
        }

        // Everything is done, let's clean up.
        if (activeFor >= end_when && activeFor >= announce_when && activeFor >= start_when)
        {
            processing = FALSE;
            kill();
        }

        activeFor++;
        return null;
    }

    //Garbage collects the event by removing it from the global events list,
    //which should be the only place it's referenced.
    //Called when start(), announce() and end() has all been called.
    // tg: code/modules/events/_event.dm:324
    public virtual void kill()
    {
        Dm.remove(ref SSevents.running, this);
    }

    //Sets up the event then adds the event to the the list of running events
    // tg: code/modules/events/_event.dm:329
    [DmParams("my_processing", "event_controller")]
    public override async DmTask New(params object[] __extra)
    {
        control = Dm.arg<datum_round_event_control>(__extra, 1);
        processing = Dm.num(Dm.arg(__extra, 0) ?? TRUE);
        Dm.append(ref SSevents.running, this);
        await base.New(__extra);
    }
}
