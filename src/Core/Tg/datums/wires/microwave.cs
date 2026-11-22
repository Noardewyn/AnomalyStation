// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/microwave.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the microwave's wires. The var block is generated (D111).

// tg: code/datums/wires/microwave.dm:1
public partial class datum_wires_microwave : datum_wires
{
    // tg: code/datums/wires/microwave.dm:5
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            WIRE_ACTIVATE,
            WIRE_MODE_SELECT
        );
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/microwave.dm:12
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        bool dot = false;
        obj_machinery_microwave mw = (obj_machinery_microwave)holder;
        if (Dm.truthy(mw.panel_open))
            dot = true;
        return dot;
    }

    // tg: code/datums/wires/microwave.dm:20
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_microwave mw = (obj_machinery_microwave)holder;
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_ACTIVATE))
            await mw.cook();
        else if (Dm.eq(sw__1, WIRE_MODE_SELECT))
        {
            if (Dm.truthy(mw.vampire_charging_capable))
                mw.vampire_charging_enabled = Dm.num(!Dm.truthy(mw.vampire_charging_enabled));
        }
    }

    // tg: code/datums/wires/microwave.dm:29
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        obj_machinery_microwave mw = (obj_machinery_microwave)holder;
        object sw__1 = wire;
        if (Dm.eq(sw__1, WIRE_ACTIVATE))
            mw.wire_disabled = Dm.num(!Dm.truthy(mend));
        else if (Dm.eq(sw__1, WIRE_MODE_SELECT))
            mw.wire_mode_swap = Dm.num(!Dm.truthy(mend));
    }
}
