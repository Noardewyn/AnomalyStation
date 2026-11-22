// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/firewall.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/status_effects/debuffs/firewall.dm:1
public partial class datum_status_effect_firewalled : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/firewall.dm:10
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object set_duration = Dm.arg(__extra, 0) ?? 30 * SECONDS;
        this.duration = Dm.num_or_null(set_duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, set_duration));
    }

    // tg: code/datums/status_effects/debuffs/firewall.dm:14
    [DmParams("effect", "set_duration")]
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        float set_duration = Dm.num(Dm.arg(__extra, 0) ?? 30 * SECONDS);
        this.duration = max(Dm.num(this.duration), set_duration);
    }
}
