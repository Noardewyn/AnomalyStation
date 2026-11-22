// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/temperature_over_time.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file (the Spinesthetics chip's overheating head). The var blocks are generated (D111).

/**
 * Affects temperature over time.
 * I don't know how this wasn't a thing already.

 * Incidentally: Thermal insulation is actually really bad for this, since it traps the temperature inside.
 * If you're going to use this in a situation where it'd make sense for insulation to hinder its effects,
 * you should to check for it manually.
 */
// tg: code/datums/status_effects/debuffs/temperature_over_time.dm:10
public partial class datum_status_effect_temperature_over_time : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/temperature_over_time.dm:32
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/temperature_over_time.dm:36
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        if (HAS_TRAIT(owner, TRAIT_RESISTHEAT) && temperature_value > 1)
            qdel(this); // git out
        else if (HAS_TRAIT(owner, TRAIT_RESISTCOLD) && temperature_value < 1)
            qdel(this); // git out
        return dot;
    }

    // tg: code/datums/status_effects/debuffs/temperature_over_time.dm:43
    public override async DmTask<object> on_remove()
    {
        return await base.on_remove();
    }

    // tg: code/datums/status_effects/debuffs/temperature_over_time.dm:46
    public override object get_examine_text(mob examiner = null)
    {
        if (temperature_value > 0)
            return I18n.T("[] [] sweating bullets!", owner.p_They(), owner.p_are());

        return I18n.T("[] [] shivering!", owner.p_They(), owner.p_are());
    }

    // tg: code/datums/status_effects/debuffs/temperature_over_time.dm:53
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if ((HAS_TRAIT(owner, TRAIT_RESISTHEAT) && temperature_value > 1) || (HAS_TRAIT(owner, TRAIT_RESISTCOLD) && temperature_value < 1))
        {
            qdel(this); // git out
            return null;
        }
        temperaturetion(seconds_between_ticks);
        return null;
    }

    // tg: code/datums/status_effects/debuffs/temperature_over_time.dm:59
    public virtual void temperaturetion(object seconds_per_tick = null)
    {
        // I feel like there should be an easier way to do this but I am a fool
        if (Dm.truthy(capped_temperature_hot) && owner.bodytemperature > capped_temperature_hot)
            return;
        if (Dm.truthy(capped_temperature_cold) && owner.bodytemperature < capped_temperature_cold)
            return;

        owner.adjust_bodytemperature(temperature_value * Dm.num(seconds_per_tick)); // note that this has no softcap reduction, unlike fire
        temperature_value += temperature_decay;
        if (temperature_value == 0)
            qdel(this);

        if (Dm.truthy(removal_temperature_hot) && owner.bodytemperature > removal_temperature_hot)
        {
            qdel(this);
            return;
        }
        if (Dm.truthy(removal_temperature_cold) && owner.bodytemperature < removal_temperature_cold)
        {
            qdel(this);
            return;
        }
    }
}
