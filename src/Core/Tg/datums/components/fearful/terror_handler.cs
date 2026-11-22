// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/fearful/terror_handler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3b, D316: the fear system the phobias run on). The var block is generated (D111).

/// Terror handlers, aka datums which determine current fear behaviors.
/// Separated into two groups, fear sources and fear effects, latter processing after all former are finished
// tg: code/datums/components/fearful/terror_handler.dm:3
public partial class datum_terror_handler : datum
{
    // tg: code/datums/components/fearful/terror_handler.dm:17
    [DmParams("new_owner", "new_component")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        owner = Dm.arg(__extra, 0) as mob_living;
        component = Dm.arg(__extra, 1) as datum_component_fearful;
    }

    // tg: code/datums/components/fearful/terror_handler.dm:22
    public override object Destroy(float? force = null)
    {
        owner = null;
        component = null;
        return base.Destroy(force);
    }

    /// Single tick of terror handler, returns adjustment to terror buildup
    // tg: code/datums/components/fearful/terror_handler.dm:28
    public virtual async DmTask<object> tick(object seconds_per_tick = null, object terror_buildup = null)
    {
        return 0;
    }

    /// Additional effects when we're hugged by a mob, returns fear adjustment per hug. Should only be positive.
    // tg: code/datums/components/fearful/terror_handler.dm:32
    public virtual object on_hug(mob_living hugger = null)
    {
        return 0;
    }
}
