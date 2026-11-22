// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/spinny.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * spinny.dm
 *
 * It's a component that spins things a whole bunch, like [proc/dance_rotate] but without the sleeps)
*/
// tg: code/datums/components/spinny.dm:7
public partial class datum_component_spinny : datum_component
{
    // tg: code/datums/components/spinny.dm:13
    [DmParams("steps", "reverse")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object steps = Dm.arg(__extra, 0) ?? 12f;
        object reverse = Dm.arg(__extra, 1) ?? FALSE;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        steps_left = steps;
        turn_degrees = Dm.truthy(reverse) ? -90 : 90;
        START_PROCESSING(SSfastprocess, this);
        return null;
    }

    // tg: code/datums/components/spinny.dm:21
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/datums/components/spinny.dm:25
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        steps_left = Dm.num(steps_left) - 1;
        if (parent is not atom spinny_boy || Dm.num(steps_left) <= 0)
        {
            qdel(this);
            return null;
        }

        // 25% chance to make 2 turns instead of 1 since the old dance_rotate wasn't strictly clockwise/counterclockwise
        Waiting.NoSleep(spinny_boy.setDir(Dm.turn(spinny_boy.dir, turn_degrees * (prob(25) ? 2 : 1))));
        return null;
    }
}
