// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/basic_stamina_slowdown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Applies a simple scaling slowdown as a mob's stamina is depleted
// tg: code/datums/elements/basic_stamina_slowdown.dm:2
public partial class datum_element_basic_stamina_slowdown : datum_element
{
    // tg: code/datums/elements/basic_stamina_slowdown.dm:12
    [DmParams("target", "minium_stamina_threshold", "maximum_stamina", "maximum_slowdown")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object minium_stamina_threshold = Dm.arg(__extra, 0) ?? 40;
        object maximum_stamina = Dm.arg(__extra, 1) ?? 120;
        object maximum_slowdown = Dm.arg(__extra, 2) ?? 12;
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        this.minium_stamina_threshold = minium_stamina_threshold;
        this.maximum_stamina = maximum_stamina;
        this.maximum_slowdown = maximum_slowdown;
        RegisterSignal(target, COMSIG_LIVING_STAMINA_UPDATE, nameof(on_stamina_changed));
        return dot;
    }

    // tg: code/datums/elements/basic_stamina_slowdown.dm:22
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_LIVING_STAMINA_UPDATE);
        return dot;
    }

    /// When our stamina changes check how slow we should be
    // tg: code/datums/elements/basic_stamina_slowdown.dm:27
    [SignalHandler]
    public virtual void on_stamina_changed(mob_living source = null)
    {
        if (Dm.num(source.staminaloss) >= Dm.num(minium_stamina_threshold))
        {
            float current_slowdown = (Dm.num(source.staminaloss) / Dm.num(maximum_stamina)) * Dm.num(maximum_slowdown);
            Waiting.NoSleep(source.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_basic_stamina_slowdown), TRUE, multiplicative_slowdown: current_slowdown));
        }
        else
            source.remove_movespeed_modifier(typeof(datum_movespeed_modifier_basic_stamina_slowdown));
    }
}
