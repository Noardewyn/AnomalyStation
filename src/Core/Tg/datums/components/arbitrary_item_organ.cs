// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/arbitrary_item_organ.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. An item put into a body by organ manipulation becomes a broken dummy organ holding it.
// The var block is generated.

/// Applied to an organ, makes it take on the appearance of an item
/// If the item <-> organ relationship is broken, the organ will be destroyed
// tg: code/datums/components/arbitrary_item_organ.dm:3
public partial class datum_component_arbitrary_item_organ : datum_component
{
    // tg: code/datums/components/arbitrary_item_organ.dm:7
    [DmParams("base_item")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        obj_item base_item = Dm.arg<obj_item>(__extra, 0);
        if (!isorgan(parent))
            return COMPONENT_INCOMPATIBLE;
        if (!isitem(base_item))
            return COMPONENT_INCOMPATIBLE;

        this.base_item = base_item;

        await base_item.forceMove((atom)parent);

        obj_item_organ organ = (obj_item_organ)parent;
        Dm.combine(ref organ.organ_flags, Dm.bitor(Dm.bitor(ORGAN_FAKE, ORGAN_PROMINENT), ORGAN_HAZARDOUS));
        Dm.mask(ref organ.organ_flags, Dm.bitnot(ORGAN_ORGANIC));
        await organ.apply_organ_damage(INFINITY);
        organ.name = base_item.name;

        RegisterSignal(organ, COMSIG_ORGAN_ADJUST_DAMAGE, nameof(break_again));
        RegisterSignal(organ, COMSIG_ORGAN_BODYPART_REMOVED, nameof(organ_removed));

        RegisterSignals(base_item, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED), nameof(base_item_lost));
        return null;
    }

    // tg: code/datums/components/arbitrary_item_organ.dm:28
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ORGAN_ADJUST_DAMAGE, COMSIG_ORGAN_REMOVED));
        UnregisterSignal(base_item, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED));
        base_item = null;
        return base.Destroy(force);
    }

    /// If our internal item is deleted or removed from the organ's contents, nuke the organ
    // tg: code/datums/components/arbitrary_item_organ.dm:35
    [SignalHandler]
    public virtual void base_item_lost(obj_item base_item = null)
    {
        if (QDELING(parent))
            return;

        qdel(parent);
    }

    /// Any attempt to heal the organ should damage cap it again
    // tg: code/datums/components/arbitrary_item_organ.dm:44
    [SignalHandler]
    public virtual void break_again(obj_item_organ organ = null, object damage_amount = null, params object[] __extra)
    {
        if (Dm.num(damage_amount) < INFINITY)
            Waiting.NoSleep(organ.apply_organ_damage(INFINITY));
    }

    /// If the organ is removed it reverts to being a normal item
    // tg: code/datums/components/arbitrary_item_organ.dm:50
    [SignalHandler]
    public virtual void organ_removed(obj_item_organ organ = null, obj_item_bodypart limb = null, params object[] __extra)
    {
        Waiting.NoSleep(base_item.forceMove((limb.owner?.drop_location() is var or__1 && Dm.truthy(or__1) ? or__1 : limb.drop_location()) as atom)); // triggers qdel(organ)
    }
}
