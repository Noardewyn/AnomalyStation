// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/effect_trail.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base; the cosmic field trails stay with the heretic's forcefield (inert, section 113) (gaps package C6).

/// An element used for making a trail of effects appear behind a movable atom when it moves.
// tg: code/datums/elements/effect_trail.dm:6
public partial class datum_element_effect_trail : datum_element
{
    // tg: code/datums/elements/effect_trail.dm:12
    [DmParams("target", "chosen_effect")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(generate_effect));
        this.chosen_effect = Dm.arg(__extra, 0) as Type ?? typeof(obj_effect_forcefield_cosmic_field);
        return dot;
    }

    // tg: code/datums/elements/effect_trail.dm:19
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_MOVABLE_MOVED);
        return base.Detach(source, __extra);
    }

    /// Generates an effect
    // tg: code/datums/elements/effect_trail.dm:24
    [SignalHandler]
    public virtual void generate_effect(atom_movable target_object = null)
    {
        if (get_turf(target_object) is not turf_open open_turf)
            return;
        Dm.@new(chosen_effect, open_turf);
    }
}
