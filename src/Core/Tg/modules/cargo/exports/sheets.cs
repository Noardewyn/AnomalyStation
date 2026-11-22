// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/sheets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the stacks sell by their amount (the hides' and sheets' var blocks are generated). The var blocks are generated.

// tg: code/modules/cargo/exports/sheets.dm:1
public partial class datum_export_stack : datum_export
{
    // tg: code/modules/cargo/exports/sheets.dm:5
    public override object get_amount(obj exported_item = null)
    {
        if (exported_item is obj_item_stack S)
            return S.amount;
        return 0;
    }
}
