// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/itembound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: a movable held inside a container is incapacitated and in stasis until it leaves
// it (the experi-scanner's suicide, the admins' item tomb). The var block is generated (D111).

/// When a movable has this component AND they are in the contents of a container, they will no longer be able to use their hands and be immobilized until they are removed from the container. So far, this is only useful for smites.
// tg: code/datums/components/itembound.dm:2
public partial class datum_component_itembound : datum_component
{
    // tg: code/datums/components/itembound.dm:8
    [DmParams("passed_container")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        atom_movable passed_container = Dm.arg<atom_movable>(__extra, 0);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;
        if (QDELETED(passed_container))
            return null;
        containerref = WEAKREF(passed_container) as datum_weakref;
        RegisterSignal(passed_container, COMSIG_ATOM_EXAMINE_MORE, nameof(on_examined));
        move_tracker = Dm.@new<datum_movement_detector>(parent, Dm.@new<datum_callback>(this, nameof(verify_containment)));
        return null;
    }

    // tg: code/datums/components/itembound.dm:18
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        ADD_TRAIT(parent, TRAIT_INCAPACITATED, SMITE_TRAIT);
        if (isliving(parent))
        {
            mob_living living_parent = parent as mob_living;
            await living_parent.apply_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_ADMIN);
        }
    }

    // tg: code/datums/components/itembound.dm:25
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        REMOVE_TRAIT(parent, TRAIT_INCAPACITATED, SMITE_TRAIT);
        if (isliving(parent))
        {
            mob_living living_parent = parent as mob_living;
            living_parent.remove_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_ADMIN);
        }
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/itembound.dm:32
    [SignalHandler]
    public virtual void on_examined(atom source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("If you hold it up to your ear, you can hear the screams of the damned.")));
    }

    /// Ensure that when we move, we still are in the container. If not in the container, remove all the traits.
    // tg: code/datums/components/itembound.dm:37
    public virtual void verify_containment()
    {
        atom_movable container = containerref.resolve() as atom_movable;
        if (!QDELETED(container) && Dm.truthy(container.contains(parent as atom)))
            return;
        qdel(this);
    }

    // tg: code/datums/components/itembound.dm:43
    public override object Destroy(float? force = null)
    {
        atom_movable container = containerref?.resolve() as atom_movable;
        if (!QDELETED(container))
            UnregisterSignal(container, COMSIG_ATOM_EXAMINE_MORE);
        containerref = null;
        QDEL_NULL(ref move_tracker);
        return base.Destroy(force);
    }
}
