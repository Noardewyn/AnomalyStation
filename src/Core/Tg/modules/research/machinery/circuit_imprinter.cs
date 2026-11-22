// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/machinery/circuit_imprinter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD1, D333: the fabricators): the circuit imprinter, its efficiency by its servos and its lack of an
// insertion animation, and the offstation one. The departmental imprinters (departmental_circuit_imprinter.dm) only set vars: their
// classes are generated (D111).

// tg: code/modules/research/machinery/circuit_imprinter.dm:1
public partial class obj_machinery_rnd_production_circuit_imprinter : obj_machinery_rnd_production
{
    // tg: code/modules/research/machinery/circuit_imprinter.dm:10
    public override object compute_efficiency()
    {
        float rating = 0;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            rating += Dm.num(servo.tier);

        return Dm.pow(0.5f, max(rating - 1, 0)); // One sheet, half sheet, quarter sheet, eighth sheet.
    }

    // tg: code/modules/research/machinery/circuit_imprinter.dm:17
    [DmParams("mat")]
    public override void flick_animation(datum_material mat = null)
    {
        return; //we presently have no animation
    }
}
