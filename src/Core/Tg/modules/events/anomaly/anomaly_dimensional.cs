// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/anomaly/anomaly_dimensional.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file but its admin option, the admin's choice of the theme (/datum/event_admin_setup/listed_options/
// anomaly_dimensional: the admins' Trigger Event panel, D275), which alone sets anomaly_theme. The var blocks are generated (D111).

// tg: code/modules/events/anomaly/anomaly_dimensional.dm:13
public partial class datum_round_event_anomaly_anomaly_dimensional : datum_round_event_anomaly
{
    // tg: code/modules/events/anomaly/anomaly_dimensional.dm:20
    public override void apply_anomaly_properties(obj_effect_anomaly new_anomaly = null)
    {
        if (!Dm.truthy(anomaly_theme))
            return;
        ((obj_effect_anomaly_dimensional)new_anomaly).prepare_area(new_theme_path: anomaly_theme);
    }

    // tg: code/modules/events/anomaly/anomaly_dimensional.dm:25
    public override async DmTask announce__proc(object fake = null)
    {
        if (impact_area == null)
            impact_area = placer.findValidArea() as area;
        priority_announce(Dm.text("Dimensional instability detected on [] [].", ANOMALY_ANNOUNCE_MEDIUM_TEXT, impact_area.name), "Anomaly Alert");
    }
}
