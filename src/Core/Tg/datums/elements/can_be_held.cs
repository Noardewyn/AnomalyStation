// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/can_be_held.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

// tg: code/datums/elements/can_be_held.dm:2
public partial class datum_element_can_be_held : datum_element
{
    // tg: code/datums/elements/can_be_held.dm:5
    public override async DmTask<object> Attach(datum source = null, params object[] __extra)
    {
        object dot = await base.Attach(source, __extra);

        if (!isliving(source))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(source, COMSIG_MOUSEDROP_ONTO, nameof(on_mousedrop_onto));
        RegisterSignal(source, COMSIG_MOB_STRIP_MENU_OPEN, nameof(on_strip_menu_open));
        RegisterSignal(source, COMSIG_STORAGE_DUMP_PRE_TRANSFER, nameof(on_attempt_storage_dump));
        return dot;
    }

    // tg: code/datums/elements/can_be_held.dm:15
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_MOUSEDROP_ONTO, COMSIG_MOB_STRIP_MENU_OPEN, COMSIG_STORAGE_DUMP_PRE_TRANSFER));
        return base.Detach(source, __extra);
    }

    /// Used to determine the "intent" of the action that the user mob is trying to employ on the target.
    // tg: code/datums/elements/can_be_held.dm:20
    public virtual object trying_to_hold_mob(mob_living user = null, mob_living target = null)
    {
        return isliving(user) && Dm.eq(user.grab_state, GRAB_AGGRESSIVE) && user.pulling == target ? TRUE : FALSE;
    }

    /// Handles the mob being dropped onto the user mob.
    // tg: code/datums/elements/can_be_held.dm:24
    [SignalHandler]
    public virtual object on_mousedrop_onto(datum source = null, atom over = null, mob user = null)
    {
        if (!Dm.truthy(trying_to_hold_mob(user as mob_living, source as mob_living)))
            return null;

        Waiting.Detach(((mob_living)source).mob_try_pickup(user as mob_living));
        return COMPONENT_CANCEL_MOUSEDROP_ONTO;
    }

    /// Blocks strip menu opening if we can reasonably assert that the mob is trying to be picked up
    // tg: code/datums/elements/can_be_held.dm:33
    [SignalHandler]
    public virtual object on_strip_menu_open(datum source = null, atom over = null, mob user = null)
    {
        if (!Dm.truthy(trying_to_hold_mob(user as mob_living, source as mob_living)))
            return null;

        return COMPONENT_BLOCK_STRIP_MENU_OPEN;
    }

    /// Blocks storage dumping if we can reasonably assert that the mob is trying to be picked up
    // tg: code/datums/elements/can_be_held.dm:41
    [SignalHandler]
    public virtual object on_attempt_storage_dump(datum source = null, atom over = null, mob user = null)
    {
        if (!Dm.truthy(trying_to_hold_mob(user as mob_living, source as mob_living)))
            return null;

        return CANCEL_STORAGE_DUMP;
    }
}
