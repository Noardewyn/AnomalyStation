// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/moving_randomly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the element that moves a movable one step in a random direction every SSdcs tick (the beyblade gizmo's spin).
// The var block is generated (D111).

/// Just move something around in the simplest way possible
// tg: code/datums/elements/moving_randomly.dm:2
public partial class datum_element_moving_randomly : datum_element
{
    // tg: code/datums/elements/moving_randomly.dm:8
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;

        Dm.append(ref movers, target);
        return dot;
    }

    // tg: code/datums/elements/moving_randomly.dm:16
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);

        Dm.remove(ref movers, source);
        return dot;
    }

    // tg: code/datums/elements/moving_randomly.dm:21
    public override DmTask New(params object[] __extra)
    {
        START_PROCESSING(SSdcs, this);
        return DmTask.CompletedTask;
    }

    // tg: code/datums/elements/moving_randomly.dm:24  (set waitfor = FALSE, inherited from /datum/process: the wrapper of
    // waiting-model.md 5.4)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null);
    }

    private async DmTask<object> process__body()
    {
        foreach (atom_movable mover in Dm.each<atom_movable>(movers))
            await mover.Move(get_step(mover, Dm.num(pick(GLOB.alldirs))));
        return null;
    }
}
