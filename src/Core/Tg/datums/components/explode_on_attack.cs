// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/explode_on_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Makes a mob explode when it attacks something living.
// tg: code/datums/components/explode_on_attack.dm:6
public partial class datum_component_explode_on_attack : datum_component
{
    // tg: code/datums/components/explode_on_attack.dm:14
    [DmParams("impact_range", "destroy_on_explode", "mob_type_dont_bomb")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        this.impact_range = Dm.arg(__extra, 0) ?? 1f;
        this.destroy_on_explode = Dm.arg(__extra, 1) ?? TRUE;
        this.mob_type_dont_bomb = Dm.arg(__extra, 2) as DmList ?? Dm.list();
        return null;
    }

    // tg: code/datums/components/explode_on_attack.dm:21
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(bomb_target));
    }

    // tg: code/datums/components/explode_on_attack.dm:24
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET);
    }

    // tg: code/datums/components/explode_on_attack.dm:28
    [SignalHandler]
    public virtual object bomb_target(mob_living owner = null, atom victim = null)
    {
        if (!isliving(victim))
            return null;
        if (Dm.truthy(is_type_in_typecache(victim, mob_type_dont_bomb)))
            return null;

        Waiting.NoSleep(explosion(owner, light_impact_range: Dm.num(impact_range)));
        if (Dm.truthy(destroy_on_explode) && owner != null)
            qdel(owner);
        return COMPONENT_HOSTILE_NO_ATTACK;
    }
}
