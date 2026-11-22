// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/cyborg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Slows down a cyborg for a short time.
// tg: code/datums/status_effects/debuffs/cyborg.dm:2
public partial class datum_status_effect_borg_slow : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/cyborg.dm:13
    [DmParams("new_owner", "slowdown")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object slowdown = Dm.arg(__extra, 0) ?? 1;
        this.slowdown = Dm.num(slowdown);
        return await base.on_creation(new_owner, Dm.forward(__extra, slowdown));
    }

    // tg: code/datums/status_effects/debuffs/cyborg.dm:17
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_borg_slowdown), multiplicative_slowdown: slowdown);
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/cyborg.dm:21
    public override async DmTask<object> on_remove()
    {
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_borg_slowdown));
        return null;
    }

    // tg: code/datums/status_effects/debuffs/cyborg.dm:24
    [DmParams("effect", "slowdown")]
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        float slowdown = Dm.num(Dm.arg(__extra, 0) ?? 1);
        await base.refresh(effect, __extra);
        if (this.slowdown <= slowdown)
            return;
        this.slowdown = slowdown;
        await owner.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_borg_slowdown), multiplicative_slowdown: this.slowdown);
    }
}
