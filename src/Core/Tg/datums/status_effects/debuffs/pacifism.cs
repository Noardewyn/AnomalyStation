// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/pacifism.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the pacifism the hypnotic flash gives (adjust_pacifism).

// tg: code/datums/status_effects/debuffs/pacifism.dm:1
public partial class datum_status_effect_pacify : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/pacifism.dm:5
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0) ?? 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/pacifism.dm:9
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        ADD_TRAIT(owner, TRAIT_PACIFISM, TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/pacifism.dm:13
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, TRAIT_PACIFISM, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}
