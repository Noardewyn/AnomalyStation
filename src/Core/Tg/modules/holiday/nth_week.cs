// Translated from tgstation (snapshot 1d99bd7f): code/modules/holiday/nth_week.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the holidays on the nth weekday of a month (Thanksgiving, Mother's and Father's Day, Beer Day, Moth Week,
// Ice Cream Day). The var blocks are generated (D111).

///A holiday lasting one day only that falls on the nth weekday in a month i.e. 3rd Wednesday of February.
// tg: code/modules/holiday/nth_week.dm:2
public partial class datum_holiday_nth_week : datum_holiday
{
    // tg: code/modules/holiday/nth_week.dm:12
    public override object shouldCelebrate(object dd = null, object mm = null, object yy = null, object ddd = null)
    {
        float year = Dm.num(yy);
        // Does not support holidays across multiple years..
        if (!Dm.truthy(end_month))
            end_month = begin_month;
        if (!Dm.truthy(end_week))
            end_week = begin_week;
        if (!Dm.truthy(end_weekday))
            end_weekday = begin_weekday;

        // check that it's not past the last day
        end_day = Dm.num(weekday_to_iso(end_weekday)) - Dm.num(first_day_of_month(year, end_month));
        if (end_day < 0)
            end_day += 7;
        end_day += (Dm.num(end_week) - 1) * 7 + 1;

        // check that it's not past the first day
        begin_day = Dm.num(weekday_to_iso(begin_weekday)) - Dm.num(first_day_of_month(year, begin_month));
        if (begin_day < 0)
            begin_day += 7;
        begin_day += (begin_week - 1) * 7 + 1;
        return base.shouldCelebrate(dd, mm, yy, ddd);
    }
}

// tg: code/modules/holiday/nth_week.dm:47
public partial class datum_holiday_nth_week_mother : datum_holiday_nth_week
{
    // tg: code/modules/holiday/nth_week.dm:52
    public override object greet() => I18n.T("Happy Mother's Day in most of the Americas, Asia, and Oceania!");
}

// tg: code/modules/holiday/nth_week.dm:67  (a /datum/holiday/beer of its own beside /datum/holiday/nth_week/beer, as tg writes it)
public partial class datum_holiday_beer : datum_holiday
{
    public override object get_station_name() => pick("Stout", "Porter", "Lager", "Ale", "Malt", "Bock", "Doppelbock", "Hefeweizen", "Pilsner", "IPA", "Lite"); //I'm sorry for the last one
}

// tg: code/modules/holiday/nth_week.dm:70
public partial class datum_holiday_nth_week_moth : datum_holiday_nth_week
{
    //National Moth Week falls on the last full week of July, including the saturday and sunday before. See http://nationalmothweek.org/ for precise tracking.
    // tg: code/modules/holiday/nth_week.dm:79
    public override object shouldCelebrate(object dd = null, object mm = null, object yyyy = null, object ddd = null)
    {
        if (Dm.num(first_day_of_month(Dm.num(yyyy), Dm.num(mm))) >= 5) //Friday or later start of the month means week 5 is a full week.
        {
            begin_week += 1;
            end_week = Dm.num(end_week) + 1;
        }
        return base.shouldCelebrate(dd, mm, yyyy, ddd);
    }

    // tg: code/modules/holiday/nth_week.dm:85
    public override object get_station_name() => pick("Mothball", "Lepidopteran", "Lightbulb", "Moth", "Giant Atlas", "Twin-spotted Sphynx", "Madagascan Sunset", "Luna", "Death's Head", "Emperor Gum", "Polyphenus", "Oleander Hawk", "Io", "Rosy Maple", "Cecropia", "Noctuidae", "Giant Leopard", "Dysphania Militaris", "Garden Tiger");
}

// tg: code/modules/holiday/nth_week.dm:88
public partial class datum_holiday_nth_week_ice_cream : datum_holiday_nth_week
{
    // tg: code/modules/holiday/nth_week.dm:94
    public override object get_station_name() => pick("Ice Cream", "Gelato", "Semifreddo", "Cornuto", "Soft Serve", "Cone", "Ice", "Cream", "I Scream", "Parfait", "Straccciatella", "Sherbet", "Snow", "Affogato", "Mochi", "Tartufo", "Sundae", "Neapolitan");
}
