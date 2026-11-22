// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/stationary_port/port_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G3 (D272): the whole file.

/// Subtype for escape pod ports so that we can give them trait behaviour
// tg: code/modules/shuttle/stationary_port/port_types.dm:2
public partial class obj_docking_port_stationary_escape_pod : obj_docking_port_stationary
{
    // tg: code/modules/shuttle/stationary_port/port_types.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(enforce_specific_pod))
            return dot;

        if (HAS_TRAIT(SSstation, STATION_TRAIT_SMALLER_PODS))
        {
            roundstart_template = typeof(datum_map_template_shuttle_escape_pod_cramped);
            return dot;
        }
        if (HAS_TRAIT(SSstation, STATION_TRAIT_BIGGER_PODS))
            roundstart_template = typeof(datum_map_template_shuttle_escape_pod_luxury);
        return dot;
    }
}

// tg: code/modules/shuttle/stationary_port/port_types.dm:39
public partial class obj_docking_port_stationary_transit : obj_docking_port_stationary
{
    // tg: code/modules/shuttle/stationary_port/port_types.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref SSshuttle.transit_docking_ports, this);
        return dot;
    }

    // tg: code/modules/shuttle/stationary_port/port_types.dm:53
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(force))
        {
            if (get_docked() != null)
                log_world("A transit dock was destroyed while something was docked to it.");
            Dm.remove(ref SSshuttle.transit_docking_ports, this);
            if (owner != null)
            {
                if (owner.assigned_transit == this)
                    owner.assigned_transit = null;
                owner = null;
            }
            if (!QDELETED(reserved_area))
                qdel(reserved_area);
            reserved_area = null;
        }
        return base.Destroy(force);
    }
}

// tg: code/modules/shuttle/stationary_port/port_types.dm:68
public partial class obj_docking_port_stationary_picked : obj_docking_port_stationary
{
    // tg: code/modules/shuttle/stationary_port/port_types.dm:72
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(length(shuttlekeys)))
        {
            warning(Dm.text("Random docking port [] loaded with no shuttle keys", shuttle_id));
            return dot;
        }
        object selectedid = pick(shuttlekeys);
        // the var holds typepaths (V3): the template's type, whose port_id and suffix load_roundstart reads back to the same template
        roundstart_template = (SSmapping.shuttle_templates[selectedid] as datum)?.GetType();
        return dot;
    }
}
