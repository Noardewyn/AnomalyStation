// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/sideway_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Makes a mob face sideways to the way it walks, as a crab does.
// tg: code/datums/elements/sideway_movement.dm:2
public partial class datum_element_sideway_movement : datum_element
{
    // tg: code/datums/elements/sideway_movement.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_MOB_CLIENT_MOVED, nameof(on_client_move));
        RegisterSignal(target, COMSIG_MOVABLE_MOVED_FROM_LOOP, nameof(on_moved_from_loop));
        return dot;
    }

    // tg: code/datums/elements/sideway_movement.dm:11
    [SignalHandler]
    public virtual void on_client_move(atom_movable source = null, object direction = null, object old_dir = null)
    {
        on_move(source, direction, old_dir);
    }

    // tg: code/datums/elements/sideway_movement.dm:15
    [SignalHandler]
    public virtual void on_moved_from_loop(atom_movable source = null, datum_move_loop loop = null, object old_dir = null, object direction = null)
    {
        if (!Dm.truthy(CHECK_MOVE_LOOP_FLAGS(source, Dm.bitor(MOVEMENT_LOOP_OUTSIDE_CONTROL, MOVEMENT_LOOP_NO_DIR_UPDATE))))
            on_move(source, direction, old_dir);
    }

    // tg: code/datums/elements/sideway_movement.dm:20
    public virtual void on_move(atom_movable source = null, object direction = null, object old_dir = null)
    {
        if (!Dm.truthy(source.set_dir_on_move))
            return;
        object new_dir = old_dir;
        if (Dm.eq(direction, old_dir) || Dm.eq(direction, REVERSE_DIR(Dm.num(old_dir))))
            new_dir = Dm.turn(Dm.num(source.dir), Dm.num(pick(Dm.list(90f, -90f))));
        Waiting.NoSleep(source.setDir(new_dir));
    }
}
