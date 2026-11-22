// Translated from tgstation (snapshot 1d99bd7f): code/datums/quirks/negative_quirks/pyrophobia.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps MS1): the pyrophobia mood event's procs (the fear system's fire source gives it, fearful/sources); the quirk comes with
// the quirks (owner-deferred, D332). The var block is generated (D111).

// tg: code/datums/quirks/negative_quirks/pyrophobia.dm:24
public partial class datum_mood_event_pyrophobia : datum_mood_event
{
    // tg: code/datums/quirks/negative_quirks/pyrophobia.dm:29
    [DmParams("fire_size")]
    public override async DmTask add_effects(params object[] __extra)
    {
        object fire_size = Dm.arg(__extra, 0);
        fire_size ??= 0f;
        mood_change = -1 * Dm.num(fire_size);
    }

    // tg: code/datums/quirks/negative_quirks/pyrophobia.dm:32
    [DmParams("home", "new_event", "fire_size")]
    public override object be_replaced(datum_mood home = null, datum_mood_event new_event = null, params object[] __extra)
    {
        object fire_size = Dm.arg(__extra, 0);
        fire_size ??= 0f;
        mood_change = min(mood_change, -1 * Dm.num(fire_size));
        return BLOCK_NEW_MOOD;
    }
}
