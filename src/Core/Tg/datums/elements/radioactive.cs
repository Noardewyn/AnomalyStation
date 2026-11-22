// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/radioactive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the whole file. The var block is generated (D111).

/// This atom will regularly pulse radiation.
// tg: code/datums/elements/radioactive.dm:4
public partial class datum_element_radioactive : datum_element
{
    // tg: code/datums/elements/radioactive.dm:1
    private const float DELAY_BETWEEN_RADIATION_PULSES = 3 * SECONDS;

    // tg: code/datums/elements/radioactive.dm:18
    public override async DmTask New(params object[] __extra)
    {
        START_PROCESSING(SSdcs, this);
    }

    // tg: code/datums/elements/radioactive.dm:21
    [DmParams("target", "range", "threshold", "chance", "minimum_exposure_time")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object range = Dm.arg(__extra, 0) ?? 3;
        object threshold = Dm.arg(__extra, 1) ?? RAD_LIGHT_INSULATION;
        object chance = Dm.arg(__extra, 2) ?? URANIUM_IRRADIATION_CHANCE;
        object minimum_exposure_time = Dm.arg(__extra, 3) ?? URANIUM_RADIATION_MINIMUM_EXPOSURE_TIME;

        object dot = await base.Attach(target, Dm.forward(__extra, range, threshold, chance, minimum_exposure_time));

        radioactive_objects[target] = world.time;

        this.range = range;
        this.threshold = threshold;
        this.chance = chance;
        this.minimum_exposure_time = minimum_exposure_time;
        return dot;
    }

    // tg: code/datums/elements/radioactive.dm:38
    public override object Detach(datum source = null, params object[] __extra)
    {
        Dm.remove(ref radioactive_objects, source);

        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/radioactive.dm:43
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        foreach (object radioactive_object in Dm.each(radioactive_objects))
        {
            if (world.time - Dm.num(radioactive_objects[radioactive_object]) < DELAY_BETWEEN_RADIATION_PULSES)
                continue;

            radiation_pulse(
                radioactive_object as atom,
                max_range: range,
                threshold: threshold,
                chance: chance,
                minimum_exposure_time: minimum_exposure_time
            );

            radioactive_objects[radioactive_object] = world.time;
        }
        return null;
    }
}
