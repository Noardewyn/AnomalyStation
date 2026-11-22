// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/electrical_storm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the electrical storm (the APCs around a random station turf blow their lights). The var blocks are
// generated (D111).

// tg: code/modules/events/electrical_storm.dm:12
public partial class datum_round_event_electrical_storm : datum_round_event
{
    // tg: code/modules/events/electrical_storm.dm:17
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("An electrical storm has been detected in your area, please repair potential electronic overloads.", "Electrical Storm Alert");
    }

    // tg: code/modules/events/electrical_storm.dm:21
    public override async DmTask<object> start()
    {
        DmList epicentreList = Dm.list();

        for (float i = 1; i <= lightsoutAmount; i++)
        {
            var T = get_safe_random_station_turf_equal_weight() as turf;
            if (T != null)
                Dm.append(ref epicentreList, T);
        }

        if (!Dm.truthy(epicentreList.len))
            return null;

        foreach (object centre in Dm.each(epicentreList))
        {
            foreach (obj_machinery_power_apc apc in Dm.each<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
            {
                if (Dm.num(get_dist(centre, apc)) <= lightsoutRange)
                    await apc.overload_lighting();
            }
        }
        return null;
    }
}
