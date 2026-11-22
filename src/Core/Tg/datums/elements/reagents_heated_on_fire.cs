// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/reagents_heated_on_fire.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (a reagent container's contents heat in a fire).

/// When this atom is exposed to fire, that will propagate to its reagents
// tg: code/datums/elements/reagents_heated_on_fire.dm:2
public partial class datum_element_reagents_exposed_on_fire : datum_element
{
    // tg: code/datums/elements/reagents_heated_on_fire.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_ATOM_FIRE_ACT, nameof(on_fire));
        RegisterSignal(target, COMSIG_ITEM_MICROWAVE_ACT, nameof(on_microwave));
        return dot;
    }

    // tg: code/datums/elements/reagents_heated_on_fire.dm:11
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ATOM_FIRE_ACT);
        UnregisterSignal(source, COMSIG_ITEM_MICROWAVE_ACT);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/reagents_heated_on_fire.dm:16
    [SignalHandler]
    public virtual void on_fire(atom source = null, object exposed_temp = null, object exposed_vol = null)
    {
        if (source.reagents is datum_reagents holder)
            Waiting.NoSleep(holder.expose_temperature(exposed_temp));
    }

    // tg: code/datums/elements/reagents_heated_on_fire.dm:21
    [SignalHandler]
    public virtual object on_microwave(atom source = null, object microwave_source = null, mob microwaver = null, object randomize_pixel_offset = null)
    {
        if (source.reagents is datum_reagents holder)
            Waiting.NoSleep(holder.expose_temperature(1000));
        return COMPONENT_MICROWAVE_SUCCESS;
    }
}
