// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/disposal/eject.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: the disposal system).

public partial class obj : atom_movable
{
    /**
     * General proc used to expel a holder's contents through src (for bins holder is also the src).
     */
    // tg: code/modules/recycling/disposal/eject.dm:4
    public virtual async DmTask pipe_eject(obj holder = null, object direction = null, float? throw_em = null, turf target = null, object throw_range = null, object throw_speed = null)
    {
        throw_em ??= TRUE;
        throw_range ??= 5;
        throw_speed ??= 1;
        turf origin_turf = get_turf(this);
        turf target_turf;
        if ((target == null)) // done up here as a safety
            target_turf = get_offset_target_turf(loc, rand(5) - rand(5), rand(5) - rand(5)) as turf;
        else
            target_turf = target;

        if (QDELETED(origin_turf))
        {
            stack_trace("pipe_eject() attempted to operate on a qdeleted turf! In order to avoid sending things to nullspace, we are going to send everything directly to the target turf instead.", "code/modules/recycling/disposal/eject.dm", 14);
            origin_turf = target_turf;
        }

        DmList contents_to_eject = holder.contents;
        DmList contents_to_throw = Dm.list();

        foreach (atom_movable thing in Dm.each<atom_movable>(contents_to_eject.Copy()))
        {
            await thing.forceMove(origin_turf);
            thing.SEND_SIGNAL(COMSIG_MOVABLE_PIPE_EJECTING, Dm.list(thing, direction));
            if (QDELETED(thing))
                continue;

            Dm.append(contents_to_throw, thing);
        }

        if (!Dm.truthy(throw_em))
            return;

        foreach (atom_movable throwable in Dm.each<atom_movable>(contents_to_throw))
        {
            if ((target == null)) // we want the thrown things to be spread out a bit if we weren't given a target
                target_turf = get_offset_target_turf(loc, rand(5) - rand(5), rand(5) - rand(5)) as turf;

            await throwable.throw_at(target_turf, throw_range, throw_speed);
        }
    }
}
