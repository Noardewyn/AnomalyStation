// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/shovel_hands.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3a (D349): the whole file: the golem's stone arms dig the floor like a shovel. The var block is generated.

/// This component lets mobs dig up the floor with their bare hands
// tg: code/datums/components/shovel_hands.dm:2
public partial class datum_component_shovel_hands : datum_component
{
    // tg: code/datums/components/shovel_hands.dm:7
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        internal_shovel = Dm.@new<obj_item_shovel>(null);
        RegisterSignal(internal_shovel, COMSIG_QDELETING, nameof(shovel_destroyed));
        return dot;
    }

    // tg: code/datums/components/shovel_hands.dm:14
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignals(parent, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_HOSTILE_PRE_ATTACKINGTARGET), nameof(dig));
    }

    // tg: code/datums/components/shovel_hands.dm:18
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_HOSTILE_PRE_ATTACKINGTARGET));
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/shovel_hands.dm:23
    public override object Destroy(float? force = null)
    {
        if (internal_shovel != null)
            UnregisterSignal(internal_shovel, COMSIG_QDELETING);
        QDEL_NULL(ref internal_shovel);
        return base.Destroy(force);
    }

    /// Called when you click on literally anything with your hands
    // tg: code/datums/components/shovel_hands.dm:29
    [SignalHandler]
    public virtual object dig(mob_living mole = null, atom target = null, params object[] __extra)
    {
        if (!isopenturf(target))
            return null;

        Waiting.Detach(target.attackby(internal_shovel, mole)); // INVOKE_ASYNC
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Don't know how the fuck this happened but I guess you can't dig any more
    // tg: code/datums/components/shovel_hands.dm:36
    [SignalHandler]
    public virtual void shovel_destroyed(atom shovel = null, params object[] __extra)
    {
        UnregisterSignal(shovel, COMSIG_QDELETING);
        qdel(this);
    }
}
