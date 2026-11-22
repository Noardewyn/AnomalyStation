// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/special.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file; the special bounties are var blocks (generated) but the syndicate documents'. The var blocks
// are generated (D111).

// tg: code/modules/cargo/bounties/special.dm:18
public partial class datum_bounty_item_special_syndicate_documents : datum_bounty_item_special
{
    // tg: code/modules/cargo/bounties/special.dm:27
    public override object applies_to(obj O = null)
    {
        if (!Dm.truthy(base.applies_to(O)))
            return FALSE;
        if (O is obj_item_documents_photocopy Copy)
            return Copy.copy_type != null && ispath(Copy.copy_type, typeof(obj_item_documents_syndicate)) ? TRUE : FALSE;
        return TRUE;
    }
}
