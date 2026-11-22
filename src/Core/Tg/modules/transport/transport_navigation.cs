// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/transport_navigation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the nav beacons' procs (the rest of the file is vars).

/**
 * transport_controller landmarks. used to map specific destinations on the map.
 */
// tg: code/modules/transport/transport_navigation.dm:4
public partial class obj_effect_landmark_transport_nav_beacon_tram : obj_effect_landmark_transport_nav_beacon
{
    // tg: code/modules/transport/transport_navigation.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        tram_loop = Dm.@new<datum_looping_sound_tram>(this);
        LAZYADDASSOCLIST(ref SStransport.nav_beacons, specific_transport_id, this);
        return dot;
    }

    // tg: code/modules/transport/transport_navigation.dm:24
    public override object Destroy(float? force = null)
    {
        LAZYREMOVEASSOC(ref SStransport.nav_beacons, specific_transport_id, this);
        QDEL_NULL(ref tram_loop);
        return base.Destroy(force);
    }
}
