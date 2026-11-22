// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/scanner_gate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SG1: the whole file (the scanner gate's wires: a pulse toggles the pass light, the fail light or the signal block; the
// status lines). The var block is generated (D111).

// tg: code/datums/wires/scanner_gate.dm:1
public partial class datum_wires_scanner_gate : datum_wires
{
    // tg: code/datums/wires/scanner_gate.dm:7
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        await base.on_pulse(wire, user);
        var scan_gate = (obj_machinery_scanner_gate)holder;
        if (Dm.eq(wire, WIRE_ACCEPT))
            scan_gate.light_pass = Dm.num(!Dm.truthy(scan_gate.light_pass));
        else if (Dm.eq(wire, WIRE_DENY))
            scan_gate.light_fail = Dm.num(!Dm.truthy(scan_gate.light_fail));
        else if (Dm.eq(wire, WIRE_DISABLE))
            scan_gate.ignore_signals = Dm.num(!Dm.truthy(scan_gate.ignore_signals));
    }

    // tg: code/datums/wires/scanner_gate.dm:18
    public override DmList get_status()
    {
        var scanner = (obj_machinery_scanner_gate)holder;
        DmList dot = Dm.list();
        Dm.append(ref dot, I18n.T("The Green light is [].", Dm.truthy(scanner.light_pass) ? I18n.T("on") : I18n.T("off")));
        Dm.append(ref dot, I18n.T("The Red light is [].", Dm.truthy(scanner.light_fail) ? I18n.T("on") : I18n.T("off")));
        Dm.append(ref dot, I18n.T("The Purple light is [].", Dm.truthy(scanner.ignore_signals) ? I18n.T("on") : I18n.T("off")));
        return dot;
    }
}
