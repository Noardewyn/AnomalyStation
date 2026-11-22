// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/emitter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 zaps slice: the whole file (the emitter's wires: a pulse fires a beam or swaps a gun's mode). The hacking window is
// left out with wire hacking (D162). The var block is generated (D111).

// tg: code/datums/wires/emitter.dm:2
public partial class datum_wires_emitter : datum_wires
{
    // tg: code/datums/wires/emitter.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(WIRE_ZAP, WIRE_HACK);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/emitter.dm:10
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_power_emitter E = (obj_machinery_power_emitter)holder;
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_ZAP))
            await E.fire_beam_pulse();
        else if (Dm.eq(sw__1, WIRE_HACK))
        {
            E.mode = Dm.num(!Dm.truthy(E.mode));
            E.set_projectile();
        }
        await base.on_pulse(wire, user);
    }
}
