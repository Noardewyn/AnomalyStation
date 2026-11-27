// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/limited_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the buff that applies again on each refresh up to its stacks (the Envirochow's health buff). The var block is
// generated.

/// These effects reapply their on_apply() effect when refreshed while stacks < max_stacks.
// tg: code/datums/status_effects/limited_effect.dm:2
public partial class datum_status_effect_limited_buff : datum_status_effect
{
    // tg: code/datums/status_effects/limited_effect.dm:12
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        if (stacks < max_stacks)
        {
            await on_apply();
            stacks++;
        }
        else
            maxed_out();
    }

    /// Called whenever the buff is refreshed when there are more stacks than max_stacks.
    // tg: code/datums/status_effects/limited_effect.dm:20
    public virtual void maxed_out()
    {
        return;
    }
}
