// Translated from tgstation (snapshot 1d99bd7f): code/datums/weather/weather_types/radiation_storm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file: the radiation storm (it mutates the unprotected outside the maintenance and the shelters, the
// status displays warn of it) and the radioactive nebula's endless storm. The var blocks are generated (D111).

//Radiation storms occur when the station passes through an irradiated area, and irradiate anyone not standing in protected areas (maintenance, emergency storage, etc.)
// tg: code/datums/weather/weather_types/radiation_storm.dm:2
public partial class datum_weather_rad_storm : datum_weather
{
    // tg: code/datums/weather/weather_types/radiation_storm.dm:50
    public override object telegraph(DmList weather_data = null)
    {
        object dot = base.telegraph(weather_data);
        status_alarm(TRUE);
        return dot;
    }

    // tg: code/datums/weather/weather_types/radiation_storm.dm:55
    public override async DmTask weather_act_mob(mob_living living = null)
    {
        if (!prob(mutate_chance))
            return;

        if (!ishuman(living) || HAS_TRAIT(living, TRAIT_GODMODE))
            return;

        var human = (mob_living_carbon_human)living;
        if (!Dm.truthy(human.can_mutate()))
            return;

        if (HAS_TRAIT(human, TRAIT_RADIMMUNE))
            return;

        if (Dm.truthy(SSradiation.wearing_rad_protected_clothing(human)))
            return;

        await human.random_mutate_unique_identity();
        await human.random_mutate_unique_features();

        if (prob(50))
            await do_mutate(human);

        await base.weather_act_mob(living);
    }

    // tg: code/datums/weather/weather_types/radiation_storm.dm:82
    public override object end()
    {
        object dot = base.end();
        if (!Dm.truthy(dot))
            return dot;
        priority_announce("The radiation threat has passed. Please return to your workplaces.", "Anomaly Alert");
        status_alarm(FALSE);
        return dot;
    }

    // tg: code/datums/weather/weather_types/radiation_storm.dm:89
    public virtual async DmTask do_mutate(mob_living_carbon_human mutant = null)
    {
        if (prob(negative_mutation_chance))
            await mutant.easy_random_mutate(NEGATIVE + MINOR_NEGATIVE);
        else
            await mutant.easy_random_mutate(POSITIVE);
        await mutant.domutcheck();
    }

    //Makes the status displays show the radiation warning for those who missed the announcement.
    // tg: code/datums/weather/weather_types/radiation_storm.dm:96
    public virtual void status_alarm(object active = null)
    {
        if (Dm.truthy(active))
            send_status_display_radiation_alert();
        else
            clear_status_display_radiation();
    }
}

/// Used by the radioactive nebula when the station doesnt have enough shielding
// tg: code/datums/weather/weather_types/radiation_storm.dm:103
public partial class datum_weather_rad_storm_nebula : datum_weather_rad_storm
{
    // tg: code/datums/weather/weather_types/radiation_storm.dm:114
    public override async DmTask weather_act_mob(mob_living living = null)
    {
        await base.weather_act_mob(living);

        if (!prob(radiation_chance))
            return;

        if (!Dm.truthy(SSradiation.can_irradiate_basic(living)) || Dm.truthy(SSradiation.wearing_rad_protected_clothing(living as mob_living_carbon_human)))
            return;

        radiation_pulse(source: living, max_range: 0, threshold: RAD_LIGHT_INSULATION, chance: URANIUM_IRRADIATION_CHANCE);
    }

    // tg: code/datums/weather/weather_types/radiation_storm.dm:129
    public override void status_alarm(object active = null)
    {
        if (!Dm.truthy(active)) //we stay on
            return;
        base.status_alarm(active);
    }
}
