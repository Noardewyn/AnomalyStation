// Translated from tgstation (snapshot 1d99bd7f): code/datums/mood_events/area_events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MS1 (D333, D339): the whole file. The var block is generated (D111).

// tg: code/datums/mood_events/area_events.dm:11
public partial class datum_mood_event_area : datum_mood_event
{
    // tg: code/datums/mood_events/area_events.dm:15
    [DmParams("_mood_change", "_description")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object _mood_change = Dm.arg(__extra, 0);
        object _description = Dm.arg(__extra, 1);
        mood_change = Dm.num(_mood_change);
        description = _description;
    }
}
