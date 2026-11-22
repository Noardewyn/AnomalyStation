// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/anomaly/anomaly_ectoplasm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file but its admin option, the admin's override of the outburst's power and orbiters
// (/datum/event_admin_setup/anomaly_ectoplasm: the admins' Trigger Event panel, D275), which alone sets the overrides. The var blocks
// are generated (D111).

// tg: code/modules/events/anomaly/anomaly_ectoplasm.dm:17
public partial class datum_round_event_anomaly_anomaly_ectoplasm : datum_round_event_anomaly
{
    // tg: code/modules/events/anomaly/anomaly_ectoplasm.dm:26
    public override async DmTask<object> start()
    {
        if (!Dm.truthy(effect_override) || !Dm.truthy(orbit_override))
            return await base.start(); //If we provide no override, just run the usual startup.

        var anomaly_turf = await placer.findValidTurf(impact_area) as turf;
        obj_effect_anomaly_ectoplasm newAnomaly = null;
        if (anomaly_turf != null)
        {
            newAnomaly = Dm.@new(anomaly_path, anomaly_turf) as obj_effect_anomaly_ectoplasm;
            newAnomaly.override_ghosts = TRUE;
            newAnomaly.effect_power = Dm.num(effect_override);
            newAnomaly.ghosts_orbiting = Dm.num(orbit_override);
            newAnomaly.intensity_update();
        }
        if (newAnomaly != null)
            announce_to_ghosts(newAnomaly);
        return null;
    }

    // tg: code/modules/events/anomaly/anomaly_ectoplasm.dm:41
    public override async DmTask announce__proc(object fake = null)
    {
        if (impact_area == null)
            impact_area = placer.findValidArea() as area;
        priority_announce(Dm.text("Paranormal ectoplasmic outburst detected on [] [].", ANOMALY_ANNOUNCE_HARMFUL_TEXT, impact_area.name), "Anomaly Alert");
    }
}
