// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the round's random events, the lone operative's; gaps R3: the holidays, filled from world.realtime,
// the date the host gives the world (DV3), when ALLOW_HOLIDAYS is on). DEVIATION(DV380): an event whose control or event type is not ported (inert) is
// set aside as tg sets aside an event of another kind of map (nonrunning_events_by_name): never picked, configured or forced. The var
// block is generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/controllers/subsystem/events.dm:167  GLOBAL_LIST(holidays)
    public DmList holidays;

    // tg: code/controllers/subsystem/events.dm:167  (GLOBAL_UNMANAGED(holidays), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalholidays()
    {
        return;
    }
}

// tg: code/controllers/subsystem/events.dm:1  SUBSYSTEM_DEF(events)
public partial class datum_controller_subsystem_events : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(events) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSevents)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/events.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSevents != this)
        {
            if (SSevents is datum_controller_subsystem_events)
            {
                Recover();
                qdel(SSevents);
            }
            SSevents = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/events.dm:26
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        foreach (Type type in Dm.each_cast<Type>(typesof(typeof(datum_round_event_control))))
        {
            var @event = await Dm.new_async(type) as datum_round_event_control;
            if (@event.typepath == null)
                continue;
            // DEVIATION(DV380): an event not ported yet (its control or its event inert) runs nowhere, as an event of another map
            if (!Dm.truthy(@event.valid_for_map()) || InertSeam.IsInertType(type) || InertSeam.IsInertType(@event.typepath))
            {
                nonrunning_events_by_name[@event.name] = @event.type;
                continue; //don't want this one! leave it for the garbage collector
            }
            Dm.append(ref control, @event); //add it to the list of all events (controls)
            events_by_name[@event.name] = @event;
        }

        frequency_lower = Dm.num(config.Get(typeof(datum_config_entry_number_events_frequency_lower)));
        frequency_upper = Dm.num(config.Get(typeof(datum_config_entry_number_events_frequency_upper)));

        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_events_config_enabled))))
            setup_config();

        reschedule();
        // Instantiate our holidays list if it hasn't been already
        if (GLOB.holidays == null)
            fill_holidays();
        return SS_INIT_SUCCESS;
    }

    ///Takes the events config json and applies any var edits made there to their respective event.
    // tg: code/controllers/subsystem/events.dm:50
    public virtual void setup_config()
    {
        object json_file = Dm.file(Dm.text("[]/events.json", config.directory));
        if (!Dm.truthy(Dm.fexists(json_file)))
            return;
        var configuration = json_decode(file2text(json_file)) as DmList;
        foreach (object variable in Dm.each(configuration))
        {
            var @event = events_by_name[variable] as datum_round_event_control;
            if (@event == null)
            {
                if (nonrunning_events_by_name[variable] == null) // don't stack_trace events that aren't running due to map flags
                    stack_trace(Dm.text("Invalid event [] attempting to be configured.", variable), "code/controllers/subsystem/events.dm", 59);
                continue;
            }
            foreach (object event_variable in Dm.each(configuration[variable] as DmList))
            {
                if (!Dm.has_var(@event, Dm.str(event_variable)))
                {
                    stack_trace(Dm.text("Invalid event configuration variable [] in variable changes for [].", event_variable, variable), "code/controllers/subsystem/events.dm", 63);
                    continue;
                }
                Dm.set_var(@event, Dm.str(event_variable), (configuration[variable] as DmList)[event_variable]);
            }
        }
    }

    // tg: code/controllers/subsystem/events.dm:67
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
        {
            checkEvent(); //only check these if we aren't resuming a paused fire
            this.currentrun = running.Copy();
        }

        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;

        while (Dm.truthy(currentrun.len))
        {
            var thing = currentrun[currentrun.len] as datum;
            currentrun.len--;
            if (thing != null)
                thing.process(wait * 0.1f);
            else
                running.Remove(thing);
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    //checks if we should select a random event yet, and reschedules if necessary
    // tg: code/controllers/subsystem/events.dm:86
    public virtual void checkEvent()
    {
        if (scheduled <= world.time)
        {
            // #ifdef MAP_TEST ... #else: production builds spawn the event
            spawnEvent();
            reschedule();
        }
    }

    //decides which world.time we should select another random event at.
    // tg: code/controllers/subsystem/events.dm:96
    public virtual void reschedule()
    {
        scheduled = world.time + rand(frequency_lower, max(frequency_lower, frequency_upper));
    }

    /**
     * Selects a random event based on whether it can occur and its 'weight'(probability)
     *
     * Arguments:
     * * excluded_event - The event path we will be foregoing, if present.
     */
    // tg: code/controllers/subsystem/events.dm:105
    public virtual void spawnEvent(datum_round_event_control excluded_event = null)
    {
        // set waitfor = FALSE //for the admin prompt
        Waiting.WaitforFalse(spawnEvent__body(excluded_event), null);
    }

    private async DmTask<object> spawnEvent__body(datum_round_event_control excluded_event)
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_random_events))))
            return null;

        object players_amt = get_active_player_count(alive_check: TRUE, afk_check: TRUE, human_check: TRUE);
        // Only alive, non-AFK human players count towards this.

        DmList event_roster = Dm.list();

        foreach (datum_round_event_control event_to_check in Dm.each<datum_round_event_control>(control))
        {
            if (excluded_event != null && event_to_check.typepath == excluded_event.typepath) //If an event has been rerolled we won't just roll the same one again.
                continue;
            if (!Dm.truthy(event_to_check.can_spawn_event(players_amt)))
                continue;
            if (event_to_check.weight < 0) //for round-start events etc.
            {
                object res = await TriggerEvent(event_to_check);
                if (Dm.eq(res, EVENT_INTERRUPTED))
                    continue; //like it never happened
                if (Dm.eq(res, EVENT_CANT_RUN))
                    return null;
            }
            else
                event_roster[event_to_check] = event_to_check.weight;
        }

        var event_to_run = pick_weight(event_roster) as datum_round_event_control;
        if (event_to_run != null)
            await TriggerEvent(event_to_run);
        return null;
    }

    ///Does the last pre-flight checks for the passed event, and runs it if the event is ready.
    // tg: code/controllers/subsystem/events.dm:134
    public virtual async DmTask<object> TriggerEvent(datum_round_event_control event_to_trigger = null)
    {
        object dot = await event_to_trigger.preRunEvent();
        if (Dm.eq(dot, EVENT_CANT_RUN))//we couldn't run this event for some reason, set its max_occurrences to 0
            event_to_trigger.max_occurrences = 0;
        else if (Dm.eq(dot, EVENT_READY))
            await event_to_trigger.run_event(random: TRUE);
        return dot;
    }

    ///Toggles whether or not wizard events will be in the event pool, and sends a notification to the admins.
    // tg: code/controllers/subsystem/events.dm:142
    public virtual void toggleWizardmode()
    {
        wizardmode = Dm.truthy(wizardmode) ? FALSE : TRUE;
        message_admins(Dm.text("Summon Events has been []!", Dm.truthy(wizardmode)
            ? Dm.text("enabled, events will occur every [] to [] minutes", SSevents.frequency_lower / 600, SSevents.frequency_upper / 600)
            : "disabled"));
        log_game(Dm.text("Summon Events was []!", Dm.truthy(wizardmode) ? "enabled" : "disabled"));
    }

    ///Sets the event frequency bounds back to their initial value.
    // tg: code/controllers/subsystem/events.dm:148
    public virtual void resetFrequency()
    {
        frequency_lower = Dm.num(config.Get(typeof(datum_config_entry_number_events_frequency_lower)));
        frequency_upper = Dm.num(config.Get(typeof(datum_config_entry_number_events_frequency_upper)));
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/events.dm:1  SUBSYSTEM_DEF(events): GLOBAL_REAL(SSevents, /datum/controller/subsystem/events)
    internal datum_controller_subsystem_events SSevents;
}

// tg: code/controllers/subsystem/events.dm:174 (interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_events SSevents => ref TgState.Current.SSevents;

    /**
     * Checks that the passed holiday is located in the global holidays list.
     *
     * Returns a holiday datum, or null if it's not that holiday.
     */
    // tg: code/controllers/subsystem/events.dm:174
    public static object check_holidays(object holiday_to_find = null)
    {
        if (config == null || !Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_holidays)))) // a test world without a configuration: no holiday
            return null; // Holiday stuff was not enabled in the config!

        if (GLOB.holidays == null && !Dm.truthy(fill_holidays()))
            return null; // Failed to generate holidays, for some reason

        return GLOB.holidays[holiday_to_find];
    }

    /**
     * Fills the holidays list if applicable, or leaves it an empty list.
     */
    // tg: code/controllers/subsystem/events.dm:186
    public static object fill_holidays()
    {
        if (config == null || !Dm.truthy(config.Get(typeof(datum_config_entry_flag_allow_holidays))))
            return FALSE; // Holiday stuff was not enabled in the config!

        GLOB.holidays = Dm.list();
        foreach (Type holiday_type in Dm.each<Type>(subtypesof(typeof(datum_holiday))))
        {
            var holiday = (datum_holiday)Dm.@new(holiday_type);
            bool delete_holiday = true;
            foreach (object timezone in Dm.each(holiday.timezones))
            {
                float time_in_timezone = world.realtime + Dm.num(timezone) * HOURS;

                object YYYY = text2num(time2text(time_in_timezone, "YYYY", world.timezone)); // get the current year
                object MM = text2num(time2text(time_in_timezone, "MM", world.timezone)); // get the current month
                object DD = text2num(time2text(time_in_timezone, "DD", world.timezone)); // get the current day
                object DDD = time2text(time_in_timezone, "DDD", world.timezone); // get the current weekday

                if (Dm.truthy(holiday.shouldCelebrate(DD, MM, YYYY, DDD)))
                {
                    holiday.celebrate();
                    GLOB.holidays[holiday.name] = holiday;
                    delete_holiday = false;
                    break;
                }
            }
            if (delete_holiday)
                qdel(holiday);
        }

        if (GLOB.holidays.len > 0)
        {
            shuffle_inplace(GLOB.holidays);
            // DEVIATION: tg regenerates the station name here for holiday prefixes; the port keeps its fixed name
            world.update_status();
        }
        return TRUE;
    }
}
