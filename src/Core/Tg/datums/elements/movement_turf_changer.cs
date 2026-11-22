// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/movement_turf_changer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/// Changes the turf a movable walks onto into the given turf type
// tg: code/datums/elements/movement_turf_changer.dm:7
public partial class datum_element_movement_turf_changer : datum_element
{
    // tg: code/datums/elements/movement_turf_changer.dm:12
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        this.turf_type = Dm.arg(__extra, 0) as Type;
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        return dot;
    }

    // tg: code/datums/elements/movement_turf_changer.dm:20
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, COMSIG_MOVABLE_MOVED);
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/movement_turf_changer.dm:24
    [SignalHandler]
    public virtual void on_moved(atom_movable target = null, atom origin = null, object direction = null, object forced = null)
    {
        turf destination = target.loc as turf;
        if (!isturf(destination) || Dm.istype(destination, turf_type) || isgroundlessturf(destination))
            return;
        Waiting.NoSleep(destination.place_on_top(turf_type));
    }
}
