// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/time.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only server_timestamp, the midnight rollover (GLOB.midnight_rollovers, GLOB.rollovercheck_last_timeofday,
// update_midnight_rollover) and time_to_twelve_hour translated (the boot's log_world and REALTIMEOFDAY use them), and
// DisplayTimeText (M4b slice 1: SSair's roundstart active turf message), round_timestamp (M6 slice 1: the hidden prints), daysSince (gaps P5: the randomized recipes' age), ISOtime (gaps RD2: the R&D console's unlock feedback); the rest of time.dm is
// completed by the package that translates it.

public static partial class GlobalProcs
{
    /// Returns UTC timestamp with the specifified format, with optionally deciseconds or optional IC time (year offset), AKA Nanotrasen Standard Time (NST)
    // tg: code/__HELPERS/time.dm:2
    public static object server_timestamp(string format = null, object show_ds = null, object ic_time = null, object twelve_hour_clock = null)
    {
        format ??= "hh:mm:ss";
        string time_string = (Dm.truthy(twelve_hour_clock) ? (string)time_to_twelve_hour(format, world.timeofday, world.timezone) : time2text(world.timeofday, format, world.timezone));
        if (Dm.truthy(ic_time) && Dm.truthy(findtext(format, "YYYY"))) //if we have a year, replace the year
            time_string = Dm.str(replacetext_char(time_string, Dm.text("[]", GLOB.year_integer), CURRENT_STATION_YEAR));
        return (Dm.truthy(show_ds) ? Dm.text("[]:[]", time_string, Dm.mod(world.timeofday, 10)) : time_string);
    }

    /// Returns timestamp since the round started, AKA Pay Time (PT)
    // tg: code/__HELPERS/time.dm:9  (M6 slice 1: the hidden prints' time stamps)
    public static string round_timestamp(string format = null, float? wtime = null)
    {
        format ??= "hh:mm:ss";
        wtime ??= STATION_TIME_PASSED();
        return time2text(wtime.Value, format, NO_TIMEZONE);
    }

    ///returns timestamp in a sql and a not-quite-compliant ISO 8601 friendly format. Do not use for SQL, use NOW() instead
    // tg: code/__HELPERS/time.dm:13  (gaps RD2: the R&D console's node unlock feedback)
    public static string ISOtime(object timevar = null)
    {
        return time2text(Dm.truthy(timevar) ? Dm.num(timevar) : world.timeofday, "YYYY-MM-DD hh:mm:ss", world.timezone);
    }

    // tg: code/__HELPERS/time.dm:19  (narrowed to float, translation-rules.md Appendix A: every exit returns
    // GLOB.midnight_rollovers, a number that is never null)
    public static float update_midnight_rollover()
    {
        if (world.timeofday < GLOB.rollovercheck_last_timeofday) //TIME IS GOING BACKWARDS!
            GLOB.midnight_rollovers++;
        GLOB.rollovercheck_last_timeofday = world.timeofday;
        return GLOB.midnight_rollovers;
    }

    /**
     * Converts a time expressed in deciseconds (like world.time) to the 12-hour time format.
     * the format arg is the format passed down to time2text() (e.g. "hh:mm" is hours and minutes but not seconds).
     * the timezone is the time value offset from the local time. It's to be applied outside time2text() to get the AM/PM right.
     */
    //Takes a value of time in deciseconds.
    //Returns a text value of that number in hours, minutes, or seconds.
    // tg: code/__HELPERS/time.dm:79
    public static string DisplayTimeText(object time_value = null, float? round_seconds_to = null)
    {
        round_seconds_to ??= 0.1f;
        float second = FLOOR(Dm.num(time_value) * 0.1f, Dm.num(round_seconds_to));
        if (second == 0)
            return "right now";
        if (second < 60)
            return Dm.text("[] second[]", second, (second != 1) ? "s" : "");
        float minute = FLOOR(second / 60, 1);
        second = FLOOR(MODULUS(second, 60), Dm.num(round_seconds_to));
        string secondT = null;
        if (second != 0)
            secondT = Dm.text(" and [] second[]", second, (second != 1) ? "s" : "");
        if (minute < 60)
            return Dm.text("[] minute[][]", minute, (minute != 1) ? "s" : "", secondT);
        float hour = FLOOR(minute / 60, 1);
        minute = MODULUS(minute, 60);
        string minuteT = null;
        if (minute != 0)
            minuteT = Dm.text(" and [] minute[]", minute, (minute != 1) ? "s" : "");
        if (hour < 24)
            return Dm.text("[] hour[][][]", hour, (hour != 1) ? "s" : "", minuteT, secondT);
        float day = FLOOR(hour / 24, 1);
        hour = MODULUS(hour, 24);
        string hourT = null;
        if (hour != 0)
            hourT = Dm.text(" and [] hour[]", hour, (hour != 1) ? "s" : "");
        return Dm.text("[] day[][][][]", day, (day != 1) ? "s" : "", hourT, minuteT, secondT);
    }

    // tg: code/__HELPERS/time.dm:115
    public static object time_to_twelve_hour(string format = null, object time = null, float? timezone = null)
    {
        format ??= "hh:mm:ss";
        time ??= STATION_TIME_PASSED();
        timezone ??= NO_TIMEZONE;
        time = MODULUS(Dm.num(Dm.add(time, (Dm.num(timezone) * (1 * HOURS)))), 24 * HOURS);
        string am_pm = "AM";
        if (Dm.gt(time, 12 * HOURS))
        {
            am_pm = "PM";
            if (Dm.gt(time, 13 * HOURS))
                Dm.remove(ref time, 12 * HOURS); // e.g. 4:16 PM but not 00:42 PM
        }
        else if (Dm.lt(time, 1 * HOURS))
            Dm.append(ref time, 12 * HOURS); // e.g. 12.23 AM
        //set NO_TIMEZONE because we've already applied the timezone above.
        return Dm.text("[] []", time2text(Dm.num(time), format, NO_TIMEZONE), am_pm);
    }
}

// tg: code/__HELPERS/time.dm:17  GLOBAL_VAR_INIT(midnight_rollovers, 0)
// tg: code/__HELPERS/time.dm:18  GLOBAL_VAR_INIT(rollovercheck_last_timeofday, 0)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/__HELPERS/time.dm:17  (GLOBAL_RAW(/midnight_rollovers), __DEFINES/__globals.dm:32)
    // RULE-GAP: rules 3.2 types a var by its declaration's default; GLOBAL_VAR_INIT declares the var without one and assigns
    // its InitValue in InitGlobal. The InitValue (0) is taken as the default (V4), as Appendix A's narrowed
    // update_midnight_rollover assumes.
    public float midnight_rollovers;
    // tg: code/__HELPERS/time.dm:18  (GLOBAL_RAW(/rollovercheck_last_timeofday))
    public float rollovercheck_last_timeofday;

    // tg: code/__HELPERS/time.dm:17  (GLOBAL_MANAGED(midnight_rollovers, 0), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmidnight_rollovers()
    {
        midnight_rollovers = 0;
        Dm.append(ref gvars_datum_init_order, "midnight_rollovers");
    }

    // tg: code/__HELPERS/time.dm:18  (GLOBAL_MANAGED(rollovercheck_last_timeofday, 0))
    public virtual void InitGlobalrollovercheck_last_timeofday()
    {
        rollovercheck_last_timeofday = 0;
        Dm.append(ref gvars_datum_init_order, "rollovercheck_last_timeofday");
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/time.dm:107
    public static object daysSince(object realtimev = null)
    {
        return round((world.realtime - Dm.num(realtimev)) / (24 * HOURS));
    }
}

// Gaps R3 (D327): the weekday helpers the holidays use (time.dm:27-74)
public static partial class GlobalProcs
{
    ///Returns a string day as an integer in ISO format 1 (Monday) - 7 (Sunday)
    // tg: code/__HELPERS/time.dm:27
    public static float weekday_to_iso(object ddd)
    {
        switch (Dm.str(ddd))
        {
            case MONDAY: return 1;
            case TUESDAY: return 2;
            case WEDNESDAY: return 3;
            case THURSDAY: return 4;
            case FRIDAY: return 5;
            case SATURDAY: return 6;
            case SUNDAY: return 7;
        }
        return 0;
    }

    ///Returns an integer in ISO format 1 (Monday) - 7 (Sunday) as a string day
    // tg: code/__HELPERS/time.dm:45
    public static string iso_to_weekday(float ddd)
    {
        switch ((int)ddd)
        {
            case 1: return MONDAY;
            case 2: return TUESDAY;
            case 3: return WEDNESDAY;
            case 4: return THURSDAY;
            case 5: return FRIDAY;
            case 6: return SATURDAY;
            case 7: return SUNDAY;
        }
        return null;
    }

    /// Returns the day (mon, tues, wen...) in number format, 1 (monday) - 7 (sunday) from the passed in date (year, month, day)
    /// All inputs are expected indexed at 1
    // tg: code/__HELPERS/time.dm:64
    public static float day_of_month(float year, float month, float day)
    {
        // https://en.wikipedia.org/wiki/Zeller%27s_congruence
        float m = month < 3 ? month + 12 : month; // month (march = 3, april = 4...february = 14)
        float K = year % 100; // year of century
        float J = round(year / 100); // zero-based century
        // day 0-6 saturday to friday:
        float h = (day + round(13 * (m + 1) / 5) + K + round(K / 4) + round(J / 4) - 2 * J) % 7;
        //convert to ISO 1-7 monday first format
        return ((h + 5) % 7) + 1;
    }

    // tg: code/__HELPERS/time.dm:74
    public static float first_day_of_month(float year, float month) => day_of_month(year, month, 1);
}
