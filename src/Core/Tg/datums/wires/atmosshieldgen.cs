// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/atmosshieldgen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E10: the whole file (the atmospheric shield generator's wire: a pulse toggles an anchored generator). The
// hacking window is left out with wire hacking (D162). The var block is generated (D111).

// tg: code/datums/wires/atmosshieldgen.dm:1
public partial class datum_wires_atmosshieldgen : datum_wires
{
    // tg: code/datums/wires/atmosshieldgen.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(WIRE_ACTIVATE);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/atmosshieldgen.dm:10
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_atmos_shield_gen generator = (obj_machinery_atmos_shield_gen)holder;
        if (!Dm.truthy(generator.anchored))
            return;
        generator.toggle();
    }
}
