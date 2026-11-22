// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/synth_blood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (a blood pack's blood is synthetic: an IV drip's transfusion adds the component). The var block is
// generated (D111).

/// Used for tracking mobs that have had synthetic blood injected into them.
/// The higher the synth content, the more nerfed the blood is for blood worms.
/// This decays over time while the mob is alive.
// tg: code/datums/components/synth_blood.dm:4
public partial class datum_component_synth_blood : datum_component
{
    // tg: code/datums/components/synth_blood.dm:11
    [DmParams("new_synth_content")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        float new_synth_content = Dm.num(Dm.arg(__extra, 0) ?? 0);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        if (new_synth_content <= 0)
            return COMPONENT_REDUNDANT;

        synth_content = min(new_synth_content, 1);

        RegisterSignal(parent, COMSIG_LIVING_LIFE, nameof(on_life));
        return null;
    }

    // tg: code/datums/components/synth_blood.dm:21
    [DmParams("new_component", "i_am_original", "new_synth_content")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        float new_synth_content = Dm.num(Dm.arg(__extra, 0));
        if (C is datum_component_synth_blood new_component)
            new_synth_content = new_component.synth_content;
        if (new_synth_content <= 0)
            qdel(this);

        synth_content = min(new_synth_content, 1);
    }

    // tg: code/datums/components/synth_blood.dm:29
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(parent, COMSIG_LIVING_LIFE);
        return base.Destroy(force);
    }

    // tg: code/datums/components/synth_blood.dm:33
    [SignalHandler]
    public virtual void on_life(mob_living living_parent = null, object seconds_per_tick = null, object times_fired = null)
    {
        if (synth_content <= 0)
            qdel(this);
        else if (!Dm.eq(living_parent.stat, DEAD))
            // Ten minutes (or 600 seconds) to fully clear synth content from one to zero.
            synth_content = max(synth_content - (1f / 600) * Dm.num(seconds_per_tick), 0);
    }
}
