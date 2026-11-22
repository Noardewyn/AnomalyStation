// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/force_move.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 3 (wet floors and slipping: the slide of a slip on ice or lube): the whole file.

///Forced directional movement, but with a twist
///Let's block pressure and client movements while doing it so we can't be interrupted
///Supports spinning on each move, for lube related reasons
// tg: code/datums/components/force_move.dm:4
public partial class datum_component_force_move : datum_component
{
    // tg: code/datums/components/force_move.dm:6
    [DmParams("target", "spin")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        atom target = Dm.arg(__extra, 0) as atom;
        object spin = Dm.arg(__extra, 1);
        if (!Dm.truthy(target) || !ismob(parent))
            return COMPONENT_INCOMPATIBLE;

        mob mob_parent = (mob)parent;
        float dist = Dm.num(get_dist(mob_parent, target));
        datum_move_loop loop = GLOB.move_manager.move_towards(mob_parent, target, delay: 1, timeout: dist) as datum_move_loop;
        if (!Dm.truthy(loop))
            return COMPONENT_INCOMPATIBLE;
        RegisterSignal(mob_parent, COMSIG_MOB_CLIENT_PRE_LIVING_MOVE, nameof(stop_move));
        RegisterSignal(mob_parent, COMSIG_ATOM_PRE_PRESSURE_PUSH, nameof(stop_pressure));
        if (Dm.truthy(spin))
            RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(slip_spin));
        RegisterSignal(loop, COMSIG_QDELETING, nameof(loop_ended));
        return null;
    }

    // tg: code/datums/components/force_move.dm:21
    [SignalHandler]
    public virtual object stop_move(datum source = null)
    {
        // SIGNAL_HANDLER
        return COMSIG_MOB_CLIENT_BLOCK_PRE_LIVING_MOVE;
    }

    // tg: code/datums/components/force_move.dm:25
    [SignalHandler]
    public virtual object stop_pressure(datum source = null)
    {
        // SIGNAL_HANDLER
        return COMSIG_ATOM_BLOCKS_PRESSURE;
    }

    // tg: code/datums/components/force_move.dm:29
    [SignalHandler]
    public virtual void slip_spin(datum source = null)
    {
        // SIGNAL_HANDLER
        mob mob_parent = (mob)parent;
        mob_parent.spin(1, 1);
    }

    // tg: code/datums/components/force_move.dm:34
    [SignalHandler]
    public virtual void loop_ended(datum source = null)
    {
        // SIGNAL_HANDLER
        if (QDELETED(this))
            return;
        qdel(this);
    }
}
