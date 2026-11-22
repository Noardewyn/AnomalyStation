// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/wisdomcow.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4 (D327): the whole file but the admins' setups (/datum/event_admin_setup, as the other events'): a wise cow appears in a puff of
// smoke somewhere safe on the station (the cow itself since gaps C4). The var blocks are generated (D111).

// tg: code/modules/events/wisdomcow.dm:15
public partial class datum_round_event_wisdomcow : datum_round_event
{
    // tg: code/modules/events/wisdomcow.dm:25
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("A wise cow has been spotted in the area. Be sure to ask for her advice.", "Nanotrasen Cow Ranching Agency");
    }

    // tg: code/modules/events/wisdomcow.dm:28
    public override async DmTask<object> start()
    {
        turf targetloc;
        if (spawn_location != null)
            targetloc = spawn_location;
        else
            targetloc = get_safe_random_station_turf();
        var wise = Dm.@new<mob_living_basic_cow_wisdom>(targetloc, selected_wisdom, selected_experience, forced_reagent_type);
        await do_smoke(1, wise, targetloc);
        announce_to_ghosts(wise);
        return null;
    }
}
