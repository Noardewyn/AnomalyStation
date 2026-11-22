// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/anomaly/_anomaly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the anomaly events' base (an anomaly in a random safe area of the station, announced), but its
// admin option, the admin's own spot for it (/datum/event_admin_setup/set_location/anomaly: the admins' Trigger Event panel, D275).
// The var blocks are generated (D111).

// tg: code/modules/events/anomaly/_anomaly.dm:12
public partial class datum_round_event_anomaly : datum_round_event
{
    // tg: code/modules/events/anomaly/_anomaly.dm:21
    public override async DmTask<object> setup()
    {
        if (spawn_location != null)
            impact_area = get_area(spawn_location) as area;
        else
            impact_area = placer.findValidArea() as area;
        return null;
    }

    // tg: code/modules/events/anomaly/_anomaly.dm:28
    public override async DmTask announce__proc(object fake = null)
    {
        if (impact_area == null)
            impact_area = placer.findValidArea() as area;
        priority_announce(Dm.text("Energetic flux wave detected on [] [].", ANOMALY_ANNOUNCE_DANGEROUS_TEXT, impact_area.name), "Anomaly Alert");
    }

    // tg: code/modules/events/anomaly/_anomaly.dm:33
    public override async DmTask<object> start()
    {
        turf anomaly_turf;

        if (spawn_location != null)
            anomaly_turf = spawn_location;
        else
            anomaly_turf = await placer.findValidTurf(impact_area) as turf;

        object newAnomaly = null;
        if (anomaly_turf != null)
            newAnomaly = make_anomaly(anomaly_turf);
        if (Dm.truthy(newAnomaly))
        {
            apply_anomaly_properties(newAnomaly as obj_effect_anomaly);
            announce_to_ghosts(newAnomaly as atom);
        }
        return null;
    }

    // tg: code/modules/events/anomaly/_anomaly.dm:48
    public virtual object make_anomaly(turf anomaly_turf = null)
    {
        return Dm.@new(anomaly_path, anomaly_turf);
    }

    /// Make any further post-creation modifications to the anomaly
    // tg: code/modules/events/anomaly/_anomaly.dm:52
    public virtual void apply_anomaly_properties(obj_effect_anomaly new_anomaly = null)
    {
        return;
    }
}
