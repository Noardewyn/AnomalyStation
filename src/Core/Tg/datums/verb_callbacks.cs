// Translated from tgstation (snapshot 1d99bd7f): code/datums/verb_callbacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M1 P1, with the verb queues of SSverb_manager and SSinput).

///like normal callbacks but they also record their creation time for measurement purposes
///they also require the same usr/user that made the callback to both still exist and to still have a client in order to execute
// tg: code/datums/verb_callbacks.dm:3
[DmPath("/datum/callback/verb_callback")]
public partial class datum_callback_verb_callback : datum_callback
{
    ///the tick this callback datum was created in. used for testing latency
    // tg: code/datums/verb_callbacks.dm:5
    public float creation_time = 0;

    // tg: code/datums/verb_callbacks.dm:7
    [DmParams("thingtocall", "proctocall")]
    public override async DmTask New(params object[] __extra)
    {
        object thingtocall = Dm.arg(__extra, 0);
        object proctocall = Dm.arg(__extra, 1);
        creation_time = DS2TICKS(world.time);
        await base.New(Dm.forward(__extra, thingtocall, proctocall));
    }

    // tg: #ifndef UNIT_TESTS (verb_callbacks.dm:11-26): active in production (rules 1.7)
    // tg: code/datums/verb_callbacks.dm:12
    public override async DmTask<object> Invoke(params object[] __extra)
    {
        object dot = null;
        mob our_user = user?.resolve() as mob;
        if (QDELETED(our_user) || our_user.client == null)
            return dot;
        mob temp = usr;
        dot = await base.Invoke(__extra);
        usr = temp;
        return dot;
    }

    // tg: code/datums/verb_callbacks.dm:20 (set waitfor = FALSE, inherited from /datum/callback/InvokeAsync: this body can
    // sleep only through ..(), so it needs no wrapper of its own, waiting-model.md 5.4)
    public override object InvokeAsync(params object[] __extra)
    {
        object dot = null;
        mob usr__caller = usr;
        try
        {
            mob our_user = user?.resolve() as mob;
            if (QDELETED(our_user) || our_user.client == null)
                return dot;
            mob temp = usr;
            dot = base.InvokeAsync(__extra);
            usr = temp;
            return dot;
        }
        finally { usr = usr__caller; }
    }
}
