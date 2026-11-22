// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/life.dm:1
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/alien/life.dm:1
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot)) //dead or deleted
            return dot;
        findQueen();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/life.dm:7
    public override async DmTask<object> check_breath(datum_gas_mixture breath = null)
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return null;

        if (breath == null || (Dm.num(breath.total_moles()) == 0))
            //Aliens breathe in vaccuum
            return 0;

        if (health <= crit_threshold)
            await adjust_oxy_loss(2);

        float plasma_used = 0;
        float plas_detect_threshold = 0.02f;
        float breath_pressure = (Dm.num(breath.total_moles()) * R_IDEAL_GAS_EQUATION * breath.temperature) / BREATH_VOLUME;
        DmList breath_moles = breath.moles;

        breath.assert_gases(typeof(datum_gas_plasma), typeof(datum_gas_oxygen));

        //Partial pressure of the plasma in our breath
        float plasma_pp = (Dm.num(breath_moles[typeof(datum_gas_plasma)]) / Dm.num(breath.total_moles())) * breath_pressure;

        if (plasma_pp > plas_detect_threshold) // Detect plasma in air
        {
            await adjustPlasma(Dm.num(breath_moles[typeof(datum_gas_plasma)]) * 250);
            throw_alert(ALERT_XENO_PLASMA, typeof(atom_movable_screen_alert_alien_plas));

            plasma_used = Dm.num(breath_moles[typeof(datum_gas_plasma)]);
        }
        else
            clear_alert(ALERT_XENO_PLASMA);

        //Breathe in plasma and out oxygen
        Dm.remove_at(breath_moles, typeof(datum_gas_plasma), plasma_used);
        Dm.append_at(breath_moles, typeof(datum_gas_oxygen), plasma_used);

        breath.garbage_collect();

        //BREATH TEMPERATURE
        handle_breath_temperature(breath);
        return null;
    }
}

// tg: code/modules/mob/living/carbon/alien/life.dm:46
public partial class mob_living_carbon_alien_adult : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/life.dm:46
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.Life(seconds_per_tick, __extra);
        if (QDELETED(this))
            return dot;
        Waiting.NoSleep(handle_organs(seconds_per_tick));
        return dot;
    }
}
