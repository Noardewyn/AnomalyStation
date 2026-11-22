// Translated from tgstation (snapshot 1d99bd7f): code/datums/atmosphere/planetary.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated in M2 by P2 (with _atmosphere.cs): the planetary atmospheres of lavaland and the ice moon.

// Atmos types used for planetary airs
// tg: code/datums/atmosphere/planetary.dm:2
[DmPath("/datum/atmosphere/lavaland")]
public partial class datum_atmosphere_lavaland : datum_atmosphere
{
    // tg: code/datums/atmosphere/planetary.dm:3-26
    public datum_atmosphere_lavaland()
    {
        id = LAVALAND_DEFAULT_ATMOS;

        restricted_chance = 30;

        minimum_pressure = HAZARD_LOW_PRESSURE + 10;
        maximum_pressure = LAVALAND_EQUIPMENT_EFFECT_PRESSURE - 1;

        minimum_temp = BODYTEMP_COLD_DAMAGE_LIMIT + 1;
        maximum_temp = LAVALAND_MAX_TEMPERATURE;
    }

    // tg: code/datums/atmosphere/planetary.dm:5-20  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        base_gases = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), 5),
            Dm.kv(typeof(datum_gas_nitrogen), 10)
        );
        normal_gases = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), 10),
            Dm.kv(typeof(datum_gas_nitrogen), 10),
            Dm.kv(typeof(datum_gas_carbon_dioxide), 10)
        );
        restricted_gases = Dm.list(
            Dm.kv(typeof(datum_gas_plasma), 0.1f),
            Dm.kv(typeof(datum_gas_bz), 1.2f),
            Dm.kv(typeof(datum_gas_miasma), 1.2f),
            Dm.kv(typeof(datum_gas_water_vapor), 0.1f)
        );
    }
}

// tg: code/datums/atmosphere/planetary.dm:28
[DmPath("/datum/atmosphere/icemoon")]
public partial class datum_atmosphere_icemoon : datum_atmosphere
{
    // tg: code/datums/atmosphere/planetary.dm:29-51
    public datum_atmosphere_icemoon()
    {
        id = ICEMOON_DEFAULT_ATMOS;

        restricted_chance = 20;

        minimum_pressure = HAZARD_LOW_PRESSURE + 10;
        maximum_pressure = LAVALAND_EQUIPMENT_EFFECT_PRESSURE - 1;

        minimum_temp = ICEBOX_MIN_TEMPERATURE;
        maximum_temp = ICEBOX_MIN_TEMPERATURE;
    }

    // tg: code/datums/atmosphere/planetary.dm:31-45  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        base_gases = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), 5),
            Dm.kv(typeof(datum_gas_nitrogen), 10)
        );
        normal_gases = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), 10),
            Dm.kv(typeof(datum_gas_nitrogen), 10),
            Dm.kv(typeof(datum_gas_carbon_dioxide), 10)
        );
        restricted_gases = Dm.list(
            Dm.kv(typeof(datum_gas_plasma), 0.1f),
            Dm.kv(typeof(datum_gas_water_vapor), 0.1f),
            Dm.kv(typeof(datum_gas_miasma), 1.2f)
        );
    }
}
