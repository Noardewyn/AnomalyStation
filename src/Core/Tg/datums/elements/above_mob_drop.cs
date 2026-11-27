// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/above_mob_drop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Element that makes mob drops appear above their corpses until moved or picked up
// tg: code/datums/elements/above_mob_drop.dm:2
public partial class datum_element_above_mob_drop : datum_element
{
    // tg: code/datums/elements/above_mob_drop.dm:8
    [DmParams("target", "target_layer")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        float? target_layer = Dm.arg<float?>(__extra, 0);
        target_layer ??= ABOVE_LYING_MOB_LAYER;
        object dot = await base.Attach(target, Dm.forward(__extra, target_layer));
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        this.target_layer = Dm.num(target_layer);
        atom_movable owner = target as atom_movable;
        owner.layer = Dm.num(target_layer);
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        return dot;
    }

    // tg: code/datums/elements/above_mob_drop.dm:17
    [SignalHandler]
    public virtual void on_moved(atom_movable source = null)
    {
        if (source.layer == target_layer)
            source.layer = Dm.initial(source, (atom_movable p) => p.layer);
        Detach(source);
    }
}
