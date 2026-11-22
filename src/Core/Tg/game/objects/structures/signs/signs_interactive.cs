// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/signs/signs_interactive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST3 (D333): the file whole, the wall clock (the station's time on examine, in the reader's twelve-hour preference, and the
// shift's time for a reader who can read) and the wall calendar (the date in the station's year and the holidays of the day). The
// var blocks and the directional helpers' types are generated (D111).

// tg: code/game/objects/structures/signs/signs_interactive.dm:1
public partial class obj_structure_sign_clock : obj_structure_sign
{
    // tg: code/game/objects/structures/signs/signs_interactive.dm:8
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("The current NST (local) time is: [].", server_timestamp(ic_time: TRUE, twelve_hour_clock: user.client?.prefs?.read_preference(typeof(datum_preference_toggle_twelve_hour))))));
        if (Dm.truthy(user.is_literate()))
            Dm.append(ref dot, span_info(I18n.T("That means it is currently [] into the shift.", round_timestamp())));
        return dot;
    }
}

// tg: code/game/objects/structures/signs/signs_interactive.dm:14
public partial class obj_structure_sign_calendar : obj_structure_sign
{
    // tg: code/game/objects/structures/signs/signs_interactive.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("The current date is: [], [].", time2text(world.realtime, "DDD, MMM DD", world.timezone), CURRENT_STATION_YEAR)));
        if (Dm.truthy(Dm.length(GLOB.holidays)))
        {
            Dm.append(ref dot, span_info(I18n.T("Events:")));
            foreach (object holidayname in Dm.each(GLOB.holidays))
                Dm.append(ref dot, span_info(Dm.text("[]", holidayname)));
        }
        return dot;
    }
}
