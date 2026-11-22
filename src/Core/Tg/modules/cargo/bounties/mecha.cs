// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/mecha.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file. The var blocks are generated (D111).

// tg: code/modules/cargo/bounties/mecha.dm:1
public partial class datum_bounty_item_mech : datum_bounty_item
{
    // tg: code/modules/cargo/bounties/mecha.dm:6
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        description = I18n.T("Upper management has requested holodiagnostic scans of \\a [] mech be sent as soon as possible. A <b>diagnostic holoscan</b> can be generated from inside a new mecha. Ship it to receive a large payment.", name);
    }

    // tg: code/modules/cargo/bounties/mecha.dm:10
    public override object applies_to(obj shipped = null)
    {
        object dot = base.applies_to(shipped);
        if (!Dm.truthy(dot))
            return dot;

        if (shipped is obj_vehicle_sealed_mecha)
            shipped.balloon_alert_to_viewers(I18n.T("make diagnostic from inside!"));

        var diagnostic_sheet = shipped as obj_item_mecha_diagnostic;
        if (diagnostic_sheet?.mech_data == null)
            throw Dm.CRASH("Diagnostic sheet submitted with no mech data contained!", "code/modules/cargo/bounties/mecha.dm", 19);
        if (Dm.istype(diagnostic_sheet.mech_data, required_data))
            return TRUE;
        return FALSE;
    }
}
