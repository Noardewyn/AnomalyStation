// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/joint_damage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*
 * A component given to mobs to damage a linked mob
 */
// tg: code/datums/components/joint_damage.dm:4
public partial class datum_component_joint_damage : datum_component
{
    // tg: code/datums/components/joint_damage.dm:10
    [DmParams("overlord_mob")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        mob overlord_mob = Dm.arg<mob>(__extra, 0);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        mob_living parent_mob = parent as mob_living;
        previous_health_count = parent_mob.health;
        if (overlord_mob != null)
            this.overlord_mob = WEAKREF(overlord_mob) as datum_weakref;
        return null;
    }

    // tg: code/datums/components/joint_damage.dm:18
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_LIVING_HEALTH_UPDATE, nameof(damage_overlord));
        RegisterSignal(parent, COMSIG_LIVING_DEATH, nameof(damage_overlord));
    }

    // tg: code/datums/components/joint_damage.dm:22
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_LIVING_HEALTH_UPDATE, COMSIG_LIVING_DEATH));
    }

    // tg: code/datums/components/joint_damage.dm:25
    public override object Destroy(float? force = null)
    {
        overlord_mob = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/joint_damage.dm:29
    [SignalHandler]
    public virtual void damage_overlord(mob_living source = null)
    {
        mob_living overlord_to_damage = overlord_mob?.resolve() as mob_living;
        if (overlord_to_damage != null)
            Waiting.NoSleep(overlord_to_damage.adjust_brute_loss(Dm.num(previous_health_count) - source.health)); ///damage or heal overlord
        previous_health_count = source.health;
    }
}
