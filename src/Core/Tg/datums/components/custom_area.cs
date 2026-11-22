// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/custom_area.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (engineering E14: the station blueprints' new areas, __HELPERS/areas.dm:162). The var block is generated.

/**
 * Container for data necessary to track custom areas.
 * Currently this is just the areas this area was created on top of, in case the area is used to create a custom shuttle.
 */
// tg: code/datums/components/custom_area.dm:5
public partial class datum_component_custom_area : datum_component
{
    // tg: code/datums/components/custom_area.dm:8
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isarea(parent))
            return COMPONENT_INCOMPATIBLE;
        return null;
    }

    // tg: code/datums/components/custom_area.dm:12
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_AREA_TURF_ADDED, nameof(on_turf_added));
        RegisterSignal(parent, COMSIG_AREA_TURF_REMOVED, nameof(on_turf_removed));
    }

    // tg: code/datums/components/custom_area.dm:16
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_AREA_TURF_ADDED, COMSIG_AREA_TURF_REMOVED));
    }

    // tg: code/datums/components/custom_area.dm:19
    public virtual void on_turf_added(area source = null, turf turf = null, area old_area = null)
    {
        previous_areas[turf] = old_area;
    }

    // tg: code/datums/components/custom_area.dm:22
    public virtual void on_turf_removed(area source = null, turf turf = null)
    {
        Dm.remove(ref previous_areas, turf);
    }
}
