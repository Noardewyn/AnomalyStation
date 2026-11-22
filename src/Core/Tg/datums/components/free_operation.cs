// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/free_operation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice B (D220): the whole file. The stasis bed's patient is ready to operate on (TRAIT_READY_TO_OPERATE on the mob and each
// limb, kept on limbs attached later); surgery itself is not ported, nothing reads the trait yet. The var block is generated.

/// Allows mobs with this component to have all of their limbs operated on without needing drapes
// tg: code/datums/components/free_operation.dm:2
public partial class datum_component_free_operation : datum_component
{
    // tg: code/datums/components/free_operation.dm:5
    [DmParams("check")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        if (!iscarbon(parent) && !isbasicmob(parent))
            return COMPONENT_REDUNDANT;
        ADD_TRAIT(parent, TRAIT_READY_TO_OPERATE, REF(this));
        mob_living_carbon owner = parent as mob_living_carbon;
        if (owner == null)
            return null;
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(owner.get_bodyparts(TRUE)))
            ADD_TRAIT(limb, TRAIT_READY_TO_OPERATE, REF(this));
        return null;
    }

    // tg: code/datums/components/free_operation.dm:17
    public override object Destroy(float? force = null)
    {
        REMOVE_TRAIT(parent, TRAIT_READY_TO_OPERATE, REF(this));
        mob_living_carbon owner = parent as mob_living_carbon;
        if (owner == null)
            return base.Destroy(force);
        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(owner.get_bodyparts(TRUE)))
            REMOVE_TRAIT(limb, TRAIT_READY_TO_OPERATE, REF(this));
        return base.Destroy(force);
    }

    // tg: code/datums/components/free_operation.dm:26
    public override async DmTask RegisterWithParent()
    {
        if (isbasicmob(parent))
            return;
        RegisterSignal(parent, COMSIG_CARBON_ATTACH_LIMB, nameof(flag_limb));
        RegisterSignal(parent, COMSIG_CARBON_REMOVE_LIMB, nameof(unflag_limb));
    }

    // tg: code/datums/components/free_operation.dm:32
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_CARBON_ATTACH_LIMB, COMSIG_CARBON_REMOVE_LIMB));
    }

    // tg: code/datums/components/free_operation.dm:35
    [SignalHandler]
    public virtual void flag_limb(mob_living_carbon source = null, obj_item_bodypart limb = null, object special = null, object lazy = null)
    {
        ADD_TRAIT(limb, TRAIT_READY_TO_OPERATE, REF(this));
    }

    // tg: code/datums/components/free_operation.dm:39
    [SignalHandler]
    public virtual void unflag_limb(mob_living_carbon source = null, obj_item_bodypart limb = null, object special = null, object dismembered = null)
    {
        REMOVE_TRAIT(limb, TRAIT_READY_TO_OPERATE, REF(this));
    }
}
