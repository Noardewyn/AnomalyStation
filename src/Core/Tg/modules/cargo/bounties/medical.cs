// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/medical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file; the medical bounties are var blocks (generated) but the scanning report's. The var blocks are
// generated (D111).

// tg: code/modules/cargo/bounties/medical.dm:109
public partial class datum_bounty_item_medical_scanning : datum_bounty_item_medical
{
    // tg: code/modules/cargo/bounties/medical.dm:116
    public override object applies_to(obj shipped = null)
    {
        object dot = base.applies_to(shipped);
        if (shipped is not obj_item_paper_medical_report report)
            return FALSE;

        var patient = report.last_healthy_scanned_mob?.resolve() as mob_living;
        if (patient != null)
            return TRUE;
        return FALSE;
    }
}
