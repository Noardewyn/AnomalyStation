// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/parts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the refill canisters' thank-you note (the parts' var blocks are generated). The var blocks are generated.

// tg: code/modules/cargo/exports/parts.dm:37
public partial class datum_export_refill_canister : datum_export
{
    // tg: code/modules/cargo/exports/parts.dm:42
    public override object total_printout(datum_export_report ex = null, object notes = null)
    {
        notes ??= TRUE;
        object dot = base.total_printout(ex, notes);
        if (Dm.truthy(dot) && Dm.truthy(notes))
            dot = Dm.add(dot, I18n.T(" Thank you for restocking the station!"));
        return dot;
    }
}
