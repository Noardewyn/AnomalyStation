// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/light_blocking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;


namespace Anomaly.Core.Tg;

// M7 lumcount: the whole file (the opacity bookkeeping of the lighting: a turf's opacity_sources and directional_opacity).
// The var block is generated (D111).

/**
 * Attached to movable atoms with opacity. Listens to them move and updates their old and new turf loc's opacity accordingly.
 */
// tg: code/datums/elements/light_blocking.dm:4
public partial class datum_element_light_blocking : datum_element
{
    // tg: code/datums/elements/light_blocking.dm:9
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_target_move));
        atom_movable movable_target = target as atom_movable;
        if (!isturf(movable_target.loc))
            return dot;
        foreach (turf turf_loc in Dm.each<turf>(movable_target.locs))
            turf_loc.add_opacity_source(movable_target);
        return dot;
    }

    // tg: code/datums/elements/light_blocking.dm:21
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(COMSIG_MOVABLE_MOVED));
        atom_movable movable_target = target as atom_movable;
        if (!isturf(movable_target.loc))
            return dot;
        foreach (turf turf_loc in Dm.each<turf>(movable_target.locs))
            turf_loc.remove_opacity_source(movable_target);
        return dot;
    }

    ///Updates old and new turf loc opacities.
    // tg: code/datums/elements/light_blocking.dm:32
    [SignalHandler]
    public virtual void on_target_move(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        if (isturf(old_loc))
        {
            if (old_locs != null)
            {
                foreach (turf old_turf in Dm.each<turf>(old_locs))
                    old_turf.remove_opacity_source(source);
            }
            else
            {
                turf old_turf = (turf)old_loc;
                old_turf.remove_opacity_source(source);
            }
        }
        if (isturf(source.loc))
        {
            foreach (turf new_turf in Dm.each<turf>(source.locs))
                new_turf.add_opacity_source(source);
        }
    }
}
