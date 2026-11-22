// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/strong_pull.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the cargo gauntlets' strong pull: what the wearer pulls cannot be pulled by anyone else, and a
// pulled closet keeps TRAIT_STRONGPULL). The var block is generated (D111).

/*
This component attaches to mobs, and makes their pulls !strong!
Basically, the items they pull cannot be pulled (except by the puller)
*/
// tg: code/datums/components/strong_pull.dm:6
public partial class datum_component_strong_pull : datum_component
{
    // tg: code/datums/components/strong_pull.dm:9
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/strong_pull.dm:13
    public override object Destroy(float? force = null)
    {
        if (strongpulling != null)
            lose_strong_grip();
        return base.Destroy(force);
    }

    // tg: code/datums/components/strong_pull.dm:18
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_LIVING_START_PULL, nameof(on_pull));
    }

    /**
     * Called when the parent grabs something, adds signals to the object to reject interactions
     */
    // tg: code/datums/components/strong_pull.dm:25
    [SignalHandler]
    public virtual void on_pull(datum source = null, atom_movable pulled = null, object state = null, object force = null)
    {
        strongpulling = pulled;
        RegisterSignal(strongpulling, COMSIG_ATOM_CAN_BE_PULLED, nameof(reject_further_pulls));
        RegisterSignal(strongpulling, COMSIG_ATOM_NO_LONGER_PULLED, nameof(on_no_longer_pulled));
        if (strongpulling is obj_structure_closet && !(strongpulling is obj_structure_closet_body_bag))
        {
            obj_structure_closet grabbed_closet = (obj_structure_closet)strongpulling;
            ADD_TRAIT(grabbed_closet, TRAIT_STRONGPULL, REF(this));
        }
    }

    /**
     * Signal for rejecting further grabs
     */
    // tg: code/datums/components/strong_pull.dm:37
    [SignalHandler]
    public virtual object reject_further_pulls(datum source = null, mob_living puller = null)
    {
        if (puller != parent) //for increasing grabs, you need to have a valid pull. thus, parent should be able to pull the same object again
        {
            strongpulling.balloon_alert(puller, I18n.T("gripped too tightly!"));
            return COMSIG_ATOM_CANT_PULL;
        }
        return null;
    }

    /*
     * Unregisters signals and stops any buffs to pulling.
     */
    // tg: code/datums/components/strong_pull.dm:46
    public virtual void lose_strong_grip()
    {
        UnregisterSignal(strongpulling, Dm.list(COMSIG_ATOM_CAN_BE_PULLED, COMSIG_ATOM_NO_LONGER_PULLED));
        if (strongpulling is obj_structure_closet)
        {
            obj_structure_closet ungrabbed_closet = (obj_structure_closet)strongpulling;
            REMOVE_TRAIT(ungrabbed_closet, TRAIT_STRONGPULL, REF(this));
        }
        strongpulling = null;
    }

    /**
     * Called when the hooked object is no longer pulled and removes the strong grip.
     */
    // tg: code/datums/components/strong_pull.dm:56
    [SignalHandler]
    public virtual void on_no_longer_pulled(datum source = null, atom_movable last_puller = null)
    {
        lose_strong_grip();
    }
}
