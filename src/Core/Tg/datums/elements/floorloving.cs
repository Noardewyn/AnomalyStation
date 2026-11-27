// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/floorloving.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Prevents a movable atom from moving to somewhere which isn't an open turf with floor on it
// tg: code/datums/elements/floorloving.dm:2
public partial class datum_element_floor_loving : datum_element
{
    // tg: code/datums/elements/floorloving.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_MOVABLE_PRE_MOVE, nameof(attempting_move));
        return dot;
    }

    // tg: code/datums/elements/floorloving.dm:10
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, COMSIG_MOVABLE_PRE_MOVE);
        return base.Detach(target, __extra);
    }

    /// Block movement to any non-floor location
    // tg: code/datums/elements/floorloving.dm:15
    [SignalHandler]
    public virtual object attempting_move(atom_movable parent = null, object newloc = null)
    {
        if (!isopenturf(newloc) || is_space_or_openspace(newloc))
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
        if (isliving(parent))
        {
            mob_living living_parent = parent as mob_living;
            living_parent.balloon_alert(living_parent, "can't move there!");
        }
        return null;
    }
}
