// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/conveyor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The conveyors (the owner, 2026-11-04): the whole file (the conveyor switch's wires: one dud, and a pulse throws the switch
// as the hand that last opened the wires). The var block is generated (D111).

// tg: code/datums/wires/conveyor.dm:1
public partial class datum_wires_conveyor : datum_wires
{
    // tg: code/datums/wires/conveyor.dm:7
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        add_duds(1);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/conveyor.dm:11
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        obj_machinery_conveyor_switch C = (obj_machinery_conveyor_switch)holder;
        mob_living_carbon_human fingerman = fingerman_ref?.resolve() as mob_living_carbon_human;
        await C.interact(fingerman);
    }

    // tg: code/datums/wires/conveyor.dm:16
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        fingerman_ref = WEAKREF(user) as datum_weakref;
        return true;
    }
}
