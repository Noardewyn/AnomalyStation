// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/anomaly/anomaly_bioscrambler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file. The var blocks are generated (D111).

// tg: code/modules/events/anomaly/anomaly_bioscrambler.dm:12
public partial class datum_round_event_anomaly_anomaly_bioscrambler : datum_round_event_anomaly
{
    // tg: code/modules/events/anomaly/anomaly_bioscrambler.dm:17
    public override async DmTask announce__proc(object fake = null)
    {
        if (impact_area == null)
            impact_area = placer.findValidArea() as area;
        priority_announce(Dm.text("Biologic limb swapping agent detected on [] []. Wear biosuits or other protective gear to counter the effects.", ANOMALY_ANNOUNCE_MEDIUM_TEXT, impact_area.name), "Anomaly Alert");
    }
}
