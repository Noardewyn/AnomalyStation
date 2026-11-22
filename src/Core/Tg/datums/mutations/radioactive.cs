// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/radioactive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): Radioactivity, a radiation emitter scaled by the power chromosome. The var blocks are generated (D111).

// tg: code/datums/mutations/radioactive.dm:1
public partial class datum_mutation_radioactive : datum_mutation
{
    // tg: code/datums/mutations/radioactive.dm:14
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        if (!Dm.truthy(dot))
            return dot;
        datum_component_radioactive_emitter radioactivity_source = await make_radioactive(acquirer) as datum_component_radioactive_emitter;
        radioactivity_source_ref = WEAKREF(radioactivity_source) as datum_weakref;
        return dot;
    }

    // tg: code/datums/mutations/radioactive.dm:21
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (!QDELETED(owner))
            await make_radioactive(owner);
        return dot;
    }

    /**
     * Makes the passed mob radioactive, or if they're already radioactive,
     * update their radioactivity to the newly set values
     */
    // tg: code/datums/mutations/radioactive.dm:30
    public virtual async DmTask<object> make_radioactive(mob_living_carbon_human who = null)
    {
        return await who.AddComponent(
            typeof(datum_component_radioactive_emitter),
            Dm.named("cooldown_time", 5 * SECONDS),
            Dm.named("range", 1 * (GET_MUTATION_POWER(this) * 2)),
            Dm.named("threshold", RAD_MEDIUM_INSULATION));
    }

    // tg: code/datums/mutations/radioactive.dm:38
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        QDEL_NULL(ref radioactivity_source_ref);
        return await base.on_losing(owner);
    }
}
