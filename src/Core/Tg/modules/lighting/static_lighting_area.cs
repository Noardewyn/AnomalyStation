// Translated from tgstation (snapshot 1d99bd7f): code/modules/lighting/static_lighting_area.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the /area var static_lighting (:37-39) declared (W4.0 contracts, rules 10.2: /area/space and the
// testroom override it, game/area/areas/misc.dm). The module's lumcount is translated for gameplay (M7 lumcount, D170); only the area's base-lighting presentation is not (D24, rules 13).
// Engineering E14: create_area_lighting_objects (the blueprints' new area, __HELPERS/areas.dm:186); remove_area_lighting_objects comes with its user.

// tg: code/modules/lighting/static_lighting_area.dm:37
public partial class area : atom
{
    ///Whether this area allows static lighting and thus loads the lighting objects
    // tg: code/modules/lighting/static_lighting_area.dm:39
    public float static_lighting = TRUE;

    ///regenerates lighting objects for turfs in this area, primary use is VV changes
    // tg: code/modules/lighting/static_lighting_area.dm:46
    public virtual void create_area_lighting_objects()
    {
        foreach (turf T in Dm.each<turf>(this))
        {
            if (Dm.truthy(T.space_lit))
                continue;
            T.lighting_build_overlay();
            CHECK_TICK();
        }
    }
}
