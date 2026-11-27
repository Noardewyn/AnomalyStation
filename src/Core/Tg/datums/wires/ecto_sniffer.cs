// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/ecto_sniffer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ectoscopic sniffer's one wire (a pulse makes it report, cutting it deafens its sensor to the ghosts). The
// var block is generated (D111).

// tg: code/datums/wires/ecto_sniffer.dm:1
public partial class datum_wires_ecto_sniffer : datum_wires
{
    // tg: code/datums/wires/ecto_sniffer.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(WIRE_ACTIVATE);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/ecto_sniffer.dm:10
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_ecto_sniffer our_sniffer = holder as obj_machinery_ecto_sniffer;
        await our_sniffer.activate();
        await base.on_pulse(wire, user);
    }

    // tg: code/datums/wires/ecto_sniffer.dm:15
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        await DmTask.CompletedTask;
        obj_machinery_ecto_sniffer our_sniffer = holder as obj_machinery_ecto_sniffer;
        our_sniffer.sensor_enabled = Dm.num(mend);
    }
}
