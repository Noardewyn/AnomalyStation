// Translated from tgstation (snapshot 1d99bd7f): code/modules/holiday/foreign_calendar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the Islamic and Hebrew calendars the moving holidays (Ramadan, Hanukkah, Passover) are dated in, in
// DM's single-precision numbers as tg computes them. The var blocks are generated (D111).

/*
Source for the method of calcuation
https://www.fourmilab.ch/documents/calendar/
by John Walker 2015, released under public domain
*/
// tg: code/modules/holiday/foreign_calendar.dm:11
public partial class datum_foreign_calendar : datum
{
    // tg: code/modules/holiday/foreign_calendar.dm:1-4  (file-local defines, #undef at the end of the file)
    protected const float BYOND_EPOCH = 2451544.5f;
    protected const float HEBREW_EPOCH = 347995.5f;
    protected const float ISLAMIC_EPOCH = 1948439.5f;
    protected const float GREGORIAN_EPOCH = 1721424.5f;

    // tg: code/modules/holiday/foreign_calendar.dm:17
    [DmParams("yyyy", "mm", "dd")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(jd))
            jd = gregorian_to_jd(Dm.num(Dm.arg(__extra, 0)), Dm.num(Dm.arg(__extra, 1)), Dm.num(Dm.arg(__extra, 2)));
        set_date(jd);
    }

    // tg: code/modules/holiday/foreign_calendar.dm:22
    public virtual void set_date(object jd = null)
    {
    }

    ///Converts Gregorian date to Julian Day
    // tg: code/modules/holiday/foreign_calendar.dm:26
    public virtual float gregorian_to_jd(float year, float month, float day)
    {
        float dot = day; // days this month
        dot += (GREGORIAN_EPOCH - 1); // start at gregorian epoch
        dot += (365 * (year - 1)); // add number of days
        dot += round(((367 * month) - 362) / 12); // days from previous months this year
        dot += round((year - 1) / 4); // figuring out leap years
        dot -= round((year - 1) / 100);
        dot += round((year - 1) / 400);
        if (month > 2)
        {
            if (leap_gregorian(year))
                dot -= 1;
            else
                dot -= 2;
        }
        return dot;
    }

    ///Returns whether a year is a leap year in the Gregorian calendar
    // tg: code/modules/holiday/foreign_calendar.dm:41
    public virtual bool leap_gregorian(float year) => (year % 4 == 0) && ((year % 400 == 0) || (year % 100 != 0));

    ///Converts BYOND realtime to Julian Day
    // tg: code/modules/holiday/foreign_calendar.dm:45
    public virtual float realtime_to_jd(float realtime) => round(realtime / 864000) + BYOND_EPOCH;
}

//////////////////////////////
//     Islamic Calendar     //
//////////////////////////////
public partial class datum_foreign_calendar_islamic : datum_foreign_calendar
{
    // tg: code/modules/holiday/foreign_calendar.dm:51
    public virtual bool leap_islamic(float yr) => ((yr * 11 + 14) % 30) < 11;

    // tg: code/modules/holiday/foreign_calendar.dm:54
    public override void set_date(object jd_arg = null)
    {
        float j = Dm.num(jd);
        float jd_adj = round(j) + 0.5f; // adjust julian date so it ends in .5
        float y = round(((30 * (jd_adj - ISLAMIC_EPOCH)) + 10646) / 10631);
        yyyy = y;
        float m = min(12, CEILING(((j - (29 + islamic_to_jd(y, 1, 1))) / 29.5f) + 1, 1));
        mm = m;
        dd = j - islamic_to_jd(y, m, 1) + 1;
    }

    // tg: code/modules/holiday/foreign_calendar.dm:60
    public virtual float islamic_to_jd(float year, float month, float day)
    {
        return day + CEILING(29.5f * (month - 1), 1) + (year - 1) * 354 + round((3 + (11 * year)) / 30) + ISLAMIC_EPOCH - 1;
    }
}

//////////////////////////////
//      Hebrew Calendar     //
//////////////////////////////
public partial class datum_foreign_calendar_hebrew : datum_foreign_calendar
{
    // tg: code/modules/holiday/foreign_calendar.dm:66
    public virtual bool hebrew_leap(float year)
    {
        switch ((int)(year % 19))
        {
            case 0: case 3: case 6: case 8: case 11: case 14: case 17:
                return true;
            default:
                return false;
        }
    }

    // Hebrew to Julian
    // tg: code/modules/holiday/foreign_calendar.dm:74
    public virtual float hebrew_to_jd(float year, float month, float day)
    {
        float months = hebrew_year_months(year);
        float j = HEBREW_EPOCH + hebrew_delay_1(year) + hebrew_delay_2(year) + day + 1;
        if (month < 7)
        {
            for (float mon = 7; mon <= months; mon++)
                j += hebrew_month_days(year, mon);
            for (float mon = 1; mon < month; mon++)
                j += hebrew_month_days(year, mon);
        }
        else
        {
            for (float mon = 7; mon < month; mon++)
                j += hebrew_month_days(year, mon);
        }
        return j;
    }

    // Julian to Hebrew
    // tg: code/modules/holiday/foreign_calendar.dm:89
    public override void set_date(object jd_arg = null)
    {
        if (Dm.truthy(yyyy) && Dm.truthy(mm) && Dm.truthy(dd))
            return;
        float j = round(Dm.num(jd_arg)) + 0.5f;
        float count = round(((j - HEBREW_EPOCH) * 98496) / 35975351);
        float year = count - 1;
        for (float i = count; j >= hebrew_to_jd(i, 7, 1); i++)
            year++;
        float month = (j < hebrew_to_jd(year, 1, 1)) ? 7 : 1;
        for (float i = month; j > hebrew_to_jd(year, i, hebrew_month_days(year, i)); i++)
            month++;
        float day = (j - hebrew_to_jd(year, month, 1)) + 1;
        yyyy = year;
        mm = month;
        dd = day;
    }

    // tg: code/modules/holiday/foreign_calendar.dm:105
    public virtual float hebrew_year_months(float year) => hebrew_leap(year) ? 13 : 12;

    // Delay based on starting day of the year
    // tg: code/modules/holiday/foreign_calendar.dm:112
    public virtual float hebrew_delay_1(float year)
    {
        float months = round(((235 * year) - 234) / 19);
        float parts = 12084 + (13753 * months);
        float day = (months * 29) + round(parts / 25920);
        if (3 * (day + 1) % 7 < 3)
            day++;
        return day;
    }

    // Delay based on length of adjacent years
    // tg: code/modules/holiday/foreign_calendar.dm:121
    public virtual float hebrew_delay_2(float year)
    {
        float last = hebrew_delay_1(year - 1);
        float present = hebrew_delay_1(year);
        float next = hebrew_delay_1(year + 1);
        if (next - present == 356)
            return 2;
        else if (present - last == 382)
            return 1;
        else
            return 0;
    }

    // tg: code/modules/holiday/foreign_calendar.dm:132
    public virtual float hebrew_year_days(float year) => hebrew_to_jd(year + 1, 7, 1) - hebrew_to_jd(year, 7, 1);

    // tg: code/modules/holiday/foreign_calendar.dm:135
    public virtual float hebrew_month_days(float year, float month)
    {
        switch ((int)month)
        {
            //  First of all, dispose of fixed-length 29 day months
            case 2: case 4: case 6: case 10: case 13:
                return 29;
            //  If it's not a leap year, Adar has 29 days
            case 12:
                if (!hebrew_leap(year))
                    return 29;
                break;
            //  If it's Heshvan, days depend on length of year
            case 8:
                if (hebrew_year_days(year) % 10 != 5)
                    return 29;
                break;
            //  Similarly, Kislev varies with the length of year
            case 9:
                if (hebrew_year_days(year) % 10 == 3)
                    return 29;
                break;
        }
        //  Nope, it's a 30 day month
        return 30;
    }
}
