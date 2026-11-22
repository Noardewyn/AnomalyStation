// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/climb_walkable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8z slice C (D239): the whole file. The capsule of a mob with TRAIT_ON_CLIMBABLE walks through the tiles solid only through this
// element's structures (LocomotionSeam.IsClimbWalkable, MapGrid's climb-walkable bit); tg's own CanPass below stays the truth for the
// crossing's Move.

/// Allows objects that entered parent's tile to move freely through other objects with this component regardless of density
// tg: code/datums/elements/climb_walkable.dm:2
public partial class datum_element_climb_walkable : datum_element
{
    // tg: code/datums/elements/climb_walkable.dm:3  var/static/list/turf_connections (rules 10.6, a TgState field)
    public static ref DmList turf_connections => ref TgState.Current.datum_element_climb_walkable__turf_connections;

    // tg: code/datums/elements/climb_walkable.dm:9
    [DmParams("target")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        await target.AddElement(typeof(datum_element_connect_loc), turf_connections);
        RegisterSignal(target, COMSIG_ATOM_TRIED_PASS, nameof(can_allow_through));
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_climbable_moved));
        return dot;
    }

    // tg: code/datums/elements/climb_walkable.dm:15
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_TRIED_PASS, COMSIG_MOVABLE_MOVED));
        foreach (atom_movable climber in Dm.each<atom_movable>(get_turf(source)))
        {
            if (HAS_TRAIT(climber, TRAIT_ON_CLIMBABLE))
                REMOVE_TRAIT(climber, TRAIT_ON_CLIMBABLE, ELEMENT_TRAIT(type));
        }
        source.RemoveElement(typeof(datum_element_connect_loc), turf_connections);
        return dot;
    }

    // tg: code/datums/elements/climb_walkable.dm:23
    [SignalHandler]
    public virtual object can_allow_through(datum source = null, atom_movable mover = null, object border_dir = null)
    {
        if (HAS_TRAIT(mover, TRAIT_ON_CLIMBABLE))
            return COMSIG_COMPONENT_PERMIT_PASSAGE;
        return null;
    }

    // Removes the climbable trait if the crate or whatever it is gets pushed away.
    // tg: code/datums/elements/climb_walkable.dm:29
    [SignalHandler]
    public virtual void on_climbable_moved(datum source = null, atom old_loc = null)
    {
        foreach (atom_movable climber in Dm.each<atom_movable>(get_turf(old_loc)))
        {
            if (HAS_TRAIT(climber, TRAIT_ON_CLIMBABLE))
                REMOVE_TRAIT(climber, TRAIT_ON_CLIMBABLE, ELEMENT_TRAIT(type));
        }
    }
}

// tg: code/datums/elements/climb_walkable.dm:35  (the earliest .dme file that declares /obj/structure holds its [DmPath], rules 2.3)
[DmPath("/obj/structure")]
public partial class obj_structure : obj
{
    // tg: code/datums/elements/climb_walkable.dm:35
    [SignalHandler]
    public virtual void on_climb_enter(datum source = null, atom_movable arrived = null)
    {
        if (arrived == this) //You can't climb onto yourself
            return;
        if (HAS_TRAIT_FROM(arrived, TRAIT_MOVE_FLYING, ADMIN_TRAIT))
            return; // Dev flight does not start a climb when crossing a structure's tile.
        if (Dm.truthy(arrived.density))
            ADD_TRAIT(arrived, TRAIT_ON_CLIMBABLE, ELEMENT_TRAIT(typeof(datum_element_climb_walkable)));
    }

    // tg: code/datums/elements/climb_walkable.dm:42
    [SignalHandler]
    public virtual void on_climb_exit(datum source = null, atom_movable gone = null, object direction = null)
    {
        if (HAS_TRAIT(gone, TRAIT_ON_CLIMBABLE))
            REMOVE_TRAIT(gone, TRAIT_ON_CLIMBABLE, ELEMENT_TRAIT(typeof(datum_element_climb_walkable)));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/elements/climb_walkable.dm:3  var/static/list/turf_connections = list(COMSIG_ATOM_ENTERED = TYPE_PROC_REF(/obj/structure, on_climb_enter), ...)
    internal DmList datum_element_climb_walkable__turf_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_structure.on_climb_enter)),
        Dm.kv(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(obj_structure.on_climb_enter)),
        Dm.kv(COMSIG_ATOM_EXITED, nameof(obj_structure.on_climb_exit)));
}
