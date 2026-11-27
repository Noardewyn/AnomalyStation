// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/ore_collecting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*
 * A component to allow us to collect ore
 */
// tg: code/datums/elements/ore_collecting.dm:4
public partial class datum_element_ore_collecting : datum_element
{
    // tg: code/datums/elements/ore_collecting.dm:7
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(collect_ore));
        return dot;
    }

    // tg: code/datums/elements/ore_collecting.dm:14
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_HOSTILE_PRE_ATTACKINGTARGET);
        return dot;
    }

    // tg: code/datums/elements/ore_collecting.dm:18
    [SignalHandler]
    public virtual object collect_ore(mob_living source = null, atom target = null)
    {
        if (!(target is obj_item_stack_ore))
            return null;

        atom_movable movable_target = target as atom_movable;
        Waiting.NoSleep(movable_target.forceMove(source));
        return COMPONENT_HOSTILE_NO_ATTACK;
    }
}
