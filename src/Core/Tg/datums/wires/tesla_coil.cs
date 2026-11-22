// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/tesla_coil.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 zaps slice: the whole file (the coil's one wire; a pulse, an EMP's or a signaller's, makes it zap). The hacking window
// is left out with wire hacking (D162). The var block is generated (D111).

// tg: code/datums/wires/tesla_coil.dm:1
public partial class datum_wires_tesla_coil : datum_wires
{
    // tg: code/datums/wires/tesla_coil.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(WIRE_ZAP);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/tesla_coil.dm:10
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_power_energy_accumulator_tesla_coil C = (obj_machinery_power_energy_accumulator_tesla_coil)holder;
        await C.zap();
        await base.on_pulse(wire, user);
    }
}
