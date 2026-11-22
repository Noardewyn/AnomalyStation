// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/gasmixtures/reaction_factors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T3 (D164): the whole file. The reactions' factor texts, which the atmos handbook of the gas analyzer's window shows
// (atmos_handbooks_init, __HELPERS/atmospherics.dm). tg's `[/datum/gas/oxygen::name]` is a compile-time read of the gas datum's name.

public partial class datum_gas_reaction_standard
{
    // tg: `[/datum/gas/x::name]`, the compile-time read of the gas datum's name (types-converter.md 9)
    protected static string gas_name(Type gas_path) => Dm.initial(gas_path, (datum_gas p) => p.name);
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:1
public partial class datum_gas_reaction_standard_water_vapor : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:1
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_water_vapor), Dm.text("Condensation will consume [] moles. Freezing will not consume any. Both requires a minimum of [] moles to occur.", MOLES_GAS_VISIBLE, MOLES_GAS_VISIBLE)),
            Dm.kv("Temperature", Dm.text("Freezes a tile at []K or below, wets it at []K or below.", WATER_VAPOR_DEPOSITION_POINT, WATER_VAPOR_CONDENSATION_POINT)),
            Dm.kv("Location", "Can only happen on tiles.")
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:10
public partial class datum_gas_reaction_standard_miaster : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:10
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_miasma), Dm.text("[] is sterilized at a rate that scales with the difference between the temperature and []K.", gas_name(typeof(datum_gas_miasma)), MIASTER_STERILIZATION_TEMP)),
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("One mole of [] is released per mole of [] consumed.", gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_miasma)))),
            Dm.kv("Temperature", Dm.text("Higher temperature increases the speed of [] sterilization.", gas_name(typeof(datum_gas_miasma)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] sterilized.", MIASTER_STERILIZATION_ENERGY, gas_name(typeof(datum_gas_miasma))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:19
public partial class datum_gas_reaction_standard_plasmafire : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:19
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("[] consumption is determined by the temperature, ranging from [] moles per mole of [] consumed at []K to [] moles per mole of [] consumed at []K. Higher [] concentration up to [] times the [] increases the speed of [] consumption.", gas_name(typeof(datum_gas_oxygen)), OXYGEN_BURN_RATIO_BASE, gas_name(typeof(datum_gas_plasma)), PLASMA_MINIMUM_BURN_TEMPERATURE, OXYGEN_BURN_RATIO_BASE - 1, gas_name(typeof(datum_gas_plasma)), PLASMA_UPPER_TEMPERATURE, gas_name(typeof(datum_gas_oxygen)), PLASMA_OXYGEN_FULLBURN, gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_plasma)))),
            Dm.kv(typeof(datum_gas_plasma), Dm.text("[] is consumed at a rate that scales with the difference between the temperature and []K, with maximum scaling at []K.", gas_name(typeof(datum_gas_plasma)), PLASMA_MINIMUM_BURN_TEMPERATURE, PLASMA_UPPER_TEMPERATURE)),
            Dm.kv(typeof(datum_gas_tritium), Dm.text("[] is formed at 1 mole per mole of [] consumed if there are at least 97 times more [] than [].", gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_plasma)))),
            Dm.kv(typeof(datum_gas_water_vapor), Dm.text("[] is formed at 0.25 moles per mole of [] consumed if [] isn't being formed.", gas_name(typeof(datum_gas_water_vapor)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_tritium)))),
            Dm.kv(typeof(datum_gas_carbon_dioxide), Dm.text("[] is formed at 0.75 moles per mole of [] consumed if [] isn't being formed.", gas_name(typeof(datum_gas_carbon_dioxide)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_tritium)))),
            Dm.kv("Temperature", Dm.text("Minimum temperature of []K to occur. Higher temperature up to []K increases the [] efficiency and also the [] consumption rate.", PLASMA_MINIMUM_BURN_TEMPERATURE, PLASMA_UPPER_TEMPERATURE, gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_plasma)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] consumed.", FIRE_PLASMA_ENERGY_RELEASED, gas_name(typeof(datum_gas_plasma))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:40
public partial class datum_gas_reaction_standard_h2fire : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:40
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("[] is consumed at 0.5 moles per mole of [] consumed. Higher [] concentration up to [] times the [] increases the [] consumption rate.", gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_hydrogen)), gas_name(typeof(datum_gas_oxygen)), HYDROGEN_OXYGEN_FULLBURN, gas_name(typeof(datum_gas_hydrogen)), gas_name(typeof(datum_gas_hydrogen)))),
            Dm.kv(typeof(datum_gas_hydrogen), Dm.text("[] is consumed rapidly fast as long as there's enough [] to allow combustion.", gas_name(typeof(datum_gas_hydrogen)), gas_name(typeof(datum_gas_oxygen)))),
            Dm.kv(typeof(datum_gas_water_vapor), Dm.text("[] is produced at 1 mole per mole of [] combusted.", gas_name(typeof(datum_gas_water_vapor)), gas_name(typeof(datum_gas_hydrogen)))),
            Dm.kv("Temperature", Dm.text("Minimum temperature of []K to occur", FIRE_MINIMUM_TEMPERATURE_TO_EXIST)),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mol of [] consumed.", FIRE_HYDROGEN_ENERGY_RELEASED, gas_name(typeof(datum_gas_hydrogen))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:51
public partial class datum_gas_reaction_standard_tritfire : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:51
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("[] is consumed at 0.5 moles per mole of [] consumed. Higher [] concentration up to [] times the [] increases the [] consumption rate.", gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_oxygen)), TRITIUM_OXYGEN_FULLBURN, gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_tritium)))),
            Dm.kv(typeof(datum_gas_tritium), Dm.text("[] is consumed at rapidly fast as long as there's enough [] to allow combustion.", gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_oxygen)))),
            Dm.kv(typeof(datum_gas_water_vapor), Dm.text("[] is produced at 1 mole per mole of [] combusted.", gas_name(typeof(datum_gas_water_vapor)), gas_name(typeof(datum_gas_tritium)))),
            Dm.kv("Temperature", Dm.text("Minimum temperature of []K to occur", FIRE_MINIMUM_TEMPERATURE_TO_EXIST)),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mol of [] consumed.", FIRE_TRITIUM_ENERGY_RELEASED, gas_name(typeof(datum_gas_tritium)))),
            Dm.kv("Radiation", "This reaction emits radiation proportional to the amount of energy released.")
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:63
public partial class datum_gas_reaction_standard_freonfire : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:63
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("[] consumption is determined by the temperature, ranging from [] moles per mole of [] consumed at []K to [] moles per mole of [] consumed at []K. Higher [] concentration up to [] times the [] increases [] consumption rate.", gas_name(typeof(datum_gas_oxygen)), OXYGEN_BURN_RATIO_BASE, gas_name(typeof(datum_gas_freon)), FREON_LOWER_TEMPERATURE, OXYGEN_BURN_RATIO_BASE - 1, gas_name(typeof(datum_gas_freon)), FREON_MAXIMUM_BURN_TEMPERATURE, gas_name(typeof(datum_gas_oxygen)), FREON_OXYGEN_FULLBURN, gas_name(typeof(datum_gas_freon)), gas_name(typeof(datum_gas_freon)))),
            Dm.kv(typeof(datum_gas_freon), Dm.text("[] is consumed at a rate that scales with the distance of the temperature from []K. Its relationship with [] also determines consumption rate.", gas_name(typeof(datum_gas_freon)), FREON_MAXIMUM_BURN_TEMPERATURE, gas_name(typeof(datum_gas_oxygen)))),
            Dm.kv(typeof(datum_gas_carbon_dioxide), Dm.text("[] is formed at 1 mole per mole of [] consumed.", gas_name(typeof(datum_gas_carbon_dioxide)), gas_name(typeof(datum_gas_freon)))),
            Dm.kv("Temperature", Dm.text("Can only occur between [] - []K.", FREON_LOWER_TEMPERATURE, FREON_MAXIMUM_BURN_TEMPERATURE)),
            Dm.kv("Energy", Dm.text("[] joules of energy is absorbed per mole of [] consumed.", FIRE_FREON_ENERGY_CONSUMED, gas_name(typeof(datum_gas_freon)))),
            Dm.kv("Hot Ice", Dm.text("This reaction produces \"hot ice\" when occurring between []-[]K.", HOT_ICE_FORMATION_MINIMUM_TEMPERATURE, HOT_ICE_FORMATION_MAXIMUM_TEMPERATURE))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:79
public partial class datum_gas_reaction_standard_nitrousformation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:79
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("10 moles of [] needs to be present for the reaction to occur. [] is consumed at 0.5 moles per mole of [] formed.", gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_nitrous_oxide)))),
            Dm.kv(typeof(datum_gas_nitrogen), Dm.text(" 20 moles of [] needs to be present for the reaction to occur. [] is consumed at 1 mole per mole of [] formed.", gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_nitrous_oxide)))),
            Dm.kv(typeof(datum_gas_bz), Dm.text("5 moles of [] needs to be present for the reaction to occur. Not consumed.", gas_name(typeof(datum_gas_bz)))),
            Dm.kv(typeof(datum_gas_nitrous_oxide), Dm.text("[] gets produced rapidly.", gas_name(typeof(datum_gas_nitrous_oxide)))),
            Dm.kv("Temperature", Dm.text("Can only occur between [] - []K", N2O_FORMATION_MIN_TEMPERATURE, N2O_FORMATION_MAX_TEMPERATURE)),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] formed.", N2O_FORMATION_ENERGY, gas_name(typeof(datum_gas_nitrous_oxide))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:91
public partial class datum_gas_reaction_standard_nitrous_decomp : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:91
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_nitrous_oxide), Dm.text("[] is decomposed at a rate that scales negatively with the distance between the temperature and average of the minimum and maximum temperature of the reaction. Minimum of [] to occur.", gas_name(typeof(datum_gas_nitrous_oxide)), MINIMUM_MOLE_COUNT * 2)), //okay this one isn't made into a define yet.
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("[] is formed at 0.5 moles per mole of [] decomposed.", gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_nitrous_oxide)))),
            Dm.kv(typeof(datum_gas_nitrogen), Dm.text("[] is formed at 1 mole per mole of [] decomposed.", gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_nitrous_oxide)))),
            Dm.kv("Temperature", Dm.text("The decomposition rate scales with the product of the distances between temperature and minimum and maximum temperature. Can only happen between [] - []K.", N2O_DECOMPOSITION_MIN_TEMPERATURE, N2O_DECOMPOSITION_MAX_TEMPERATURE)),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] decomposed.", N2O_DECOMPOSITION_ENERGY, gas_name(typeof(datum_gas_nitrous_oxide))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:102
public partial class datum_gas_reaction_standard_bzformation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:102
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_plasma), Dm.text("Each mole of [] made consumes 0.8 moles of []. If there is more [] than [], [] formation rate gets slowed down.", gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_bz)))),
            Dm.kv(typeof(datum_gas_nitrous_oxide), Dm.text("Each mole of [] made consumes 0.4 moles of []. If there is less [] than [] the reaction rate is slowed down. At three times the amount of [] to [], it will start breaking down into [] and [] - the lower the ratio, the more [] decomposes.", gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_nitrous_oxide)))),
            Dm.kv(typeof(datum_gas_bz), Dm.text("The lower the pressure and larger the volume the more [] gets made. Less [] than [] will slow down the reaction.", gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_plasma)))),
            Dm.kv(typeof(datum_gas_nitrogen), Dm.text("Each mole [] decomposed makes 1 mol []. Lower ratio of [] to [] means a higher ratio of decomposition to [] production.", gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_bz)))),
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("Each mole [] decomposed makes 0.5 moles []. Lower ratio of [] to [] means a higher ratio of decomposition to [] production.", gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_bz)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mol of [] made. [] decomposition releases [] per mol decomposed.", BZ_FORMATION_ENERGY, gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_nitrous_oxide)), N2O_DECOMPOSITION_ENERGY))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:121
public partial class datum_gas_reaction_standard_pluox_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:121
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_carbon_dioxide), Dm.text("1 mole of [] gets consumed per mole of [] formed.", gas_name(typeof(datum_gas_carbon_dioxide)), gas_name(typeof(datum_gas_pluoxium)))),
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("[] is consumed at 0.5 moles per mole of [] formed.", gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_pluoxium)))),
            Dm.kv(typeof(datum_gas_tritium), Dm.text("[] is converted into [] at 0.01 moles per mole of [] formed.", gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_hydrogen)), gas_name(typeof(datum_gas_pluoxium)))),
            Dm.kv(typeof(datum_gas_pluoxium), Dm.text("[] is produced at a constant rate in any given mixture.", gas_name(typeof(datum_gas_pluoxium)))),
            Dm.kv(typeof(datum_gas_hydrogen), Dm.text("[] is formed from the [] losing their neutrons.", gas_name(typeof(datum_gas_hydrogen)), gas_name(typeof(datum_gas_tritium)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] formed.", PLUOXIUM_FORMATION_ENERGY, gas_name(typeof(datum_gas_pluoxium)))),
            Dm.kv("Temperature", Dm.text("Can only occur between [] - []K", PLUOXIUM_FORMATION_MIN_TEMP, PLUOXIUM_FORMATION_MAX_TEMP))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:132
public partial class datum_gas_reaction_standard_nitrium_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:132
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_bz), Dm.text("5 moles of [] needs to be present for the reaction to occur. [] is consumed at 0.05 moles per mole of [] formed.", gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_nitrium)))),
            Dm.kv(typeof(datum_gas_tritium), Dm.text("20 moles of [] needs to be present for the reaction to occur. [] is consumed at 1 mole per mole of [] formed.", gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_nitrium)))),
            Dm.kv(typeof(datum_gas_nitrogen), Dm.text("10 moles of [] needs to be present for the reaction to occur. [] is consumed at 1 mole per mole of [] formed.", gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_nitrium)))),
            Dm.kv(typeof(datum_gas_nitrium), Dm.text("[] is produced at a rate that scales with the temperature.", gas_name(typeof(datum_gas_nitrium)))),
            Dm.kv("Temperature", Dm.text("Can only occur above []K", NITRIUM_FORMATION_MIN_TEMP)),
            Dm.kv("Energy", Dm.text("[] joules of energy is absorbed per mole of [] formed.", NITRIUM_FORMATION_ENERGY, gas_name(typeof(datum_gas_nitrium))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:145
public partial class datum_gas_reaction_standard_nitrium_decomposition : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:145
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("[] moles of [] need to be present for the reaction to occur. Not consumed.", MINIMUM_MOLE_COUNT, gas_name(typeof(datum_gas_oxygen)))),
            Dm.kv(typeof(datum_gas_nitrium), Dm.text("[] is consumed at a rate that scales with the temperature.", gas_name(typeof(datum_gas_nitrium)))),
            Dm.kv(typeof(datum_gas_hydrogen), Dm.text("[] is produced at 1 mole per mole of [] decomposed.", gas_name(typeof(datum_gas_hydrogen)), gas_name(typeof(datum_gas_nitrium)))),
            Dm.kv(typeof(datum_gas_nitrogen), Dm.text("[] is produced at 1 mole per mole of [] decomposed.", gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_nitrium)))),
            Dm.kv("Temperature", Dm.text("Can only occur below []. Higher temperature increases the [] decomposition rate.", NITRIUM_DECOMPOSITION_MAX_TEMP, gas_name(typeof(datum_gas_nitrium)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] decomposed.", NITRIUM_DECOMPOSITION_ENERGY, gas_name(typeof(datum_gas_nitrium))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:155
public partial class datum_gas_reaction_standard_freonformation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:155
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_plasma), Dm.text("At least 0.06 moles of [] needs to be present. [] is consumed at 0.6 moles per mole of [] formed.", gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_freon)))),
            Dm.kv(typeof(datum_gas_carbon_dioxide), Dm.text("At least 0.03 moles of [] needs to be present. [] is consumed at 0.3 moles per mole of [] formed.", gas_name(typeof(datum_gas_carbon_dioxide)), gas_name(typeof(datum_gas_carbon_dioxide)), gas_name(typeof(datum_gas_freon)))),
            Dm.kv(typeof(datum_gas_bz), Dm.text("At least 0.01 moles of [] needs to be present. [] is consumed at 0.1 moles per mole of [] formed.", gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_freon)))),
            Dm.kv(typeof(datum_gas_freon), Dm.text("[] is produced at a rate that scales with temperature. See temperature factor for more information.", gas_name(typeof(datum_gas_freon)))),
            Dm.kv("Energy", Dm.text("Between 100 and 800 joules of energy is absorbed per mole of [] produced", gas_name(typeof(datum_gas_freon)))), // I don't know why the energy release is also a sigmoidal function, but it should really just be constant to be honest.
            Dm.kv("Temperature", Dm.text("Minimum temperature of []K to occur. Production speed peaks at 800K - However, at temperatures above 5500K, production speed can exceed the low temperature peak (reaching up to three times fast at 8500K).", FIRE_MINIMUM_TEMPERATURE_TO_EXIST + 100))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:171
public partial class datum_gas_reaction_standard_nobliumformation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:171
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_nitrogen), Dm.text("10 moles of [] needs to be present for the reaction to occur. [] is consumed at 10 moles per mole of [] formed.", gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_hypernoblium)))),
            Dm.kv(typeof(datum_gas_tritium), Dm.text("5 moles of [] needs to be present for the reaction to occur. [] is consumed at 5 moles per mole of [] formed. The relative consumption rate of [] decreases in the exposure of [].", gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_hypernoblium)), gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_bz)))),
            Dm.kv(typeof(datum_gas_hypernoblium), Dm.text("[] production scales based on the sum of the [] and [] moles.", gas_name(typeof(datum_gas_hypernoblium)), gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_tritium)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] produced.", NOBLIUM_FORMATION_ENERGY, gas_name(typeof(datum_gas_hypernoblium)))),
            Dm.kv(typeof(datum_gas_bz), Dm.text("[] is not consumed in the reaction but will lower the amount of energy released. It also reduces amount of [] consumed by a ratio between [] and [], greater [] than [] will reduce more.", gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_tritium)))),
            Dm.kv("Temperature", Dm.text("Can only occur between [] - []K", NOBLIUM_FORMATION_MIN_TEMP, NOBLIUM_FORMATION_MAX_TEMP))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:187
public partial class datum_gas_reaction_standard_halon_o2removal : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:187
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_halon), Dm.text("[] is consumed at a rate that scales with temperature.", gas_name(typeof(datum_gas_halon)))),
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("20 moles of [] is consumed per mole of [] combusted.", gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_halon)))),
            Dm.kv(typeof(datum_gas_carbon_dioxide), Dm.text("[] is produced at 5 moles per mole of [] consumed.", gas_name(typeof(datum_gas_carbon_dioxide)), gas_name(typeof(datum_gas_halon)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is absorbed per mole of [] consumed.", HALON_COMBUSTION_ENERGY, gas_name(typeof(datum_gas_halon)))),
            Dm.kv("Temperature", Dm.text("Can only occur above []K. Higher temperature increases [] consumption rate.", FIRE_MINIMUM_TEMPERATURE_TO_EXIST, gas_name(typeof(datum_gas_halon))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:196
public partial class datum_gas_reaction_standard_healium_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:196
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_bz), Dm.text("[] is consumed at 1/12th of a mole per mole of [] formed.", gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_healium)))),
            Dm.kv(typeof(datum_gas_freon), Dm.text("[] is consumed at 11/12th of a mole per mole of [] formed.", gas_name(typeof(datum_gas_freon)), gas_name(typeof(datum_gas_healium)))),
            Dm.kv(typeof(datum_gas_healium), Dm.text("[] is formed at a rate that scales with the temperature.", gas_name(typeof(datum_gas_healium)))),
            Dm.kv("Temperature", Dm.text("Can only occur between [] - []. Higher temperature increases [] formation rate.", HEALIUM_FORMATION_MIN_TEMP, HEALIUM_FORMATION_MAX_TEMP, gas_name(typeof(datum_gas_healium)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] formed.", Dm.div(HEALIUM_FORMATION_ENERGY, 3), gas_name(typeof(datum_gas_healium))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:206
public partial class datum_gas_reaction_standard_zauker_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:206
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_hypernoblium), Dm.text("[] is consumed at 0.02 moles per mole of [] formed.", gas_name(typeof(datum_gas_hypernoblium)), gas_name(typeof(datum_gas_zauker)))),
            Dm.kv(typeof(datum_gas_nitrium), Dm.text("[] is consumed at 1 mole per mole of [] formed.", gas_name(typeof(datum_gas_nitrium)), gas_name(typeof(datum_gas_zauker)))),
            Dm.kv(typeof(datum_gas_zauker), Dm.text("[] is produced at a rate that scales with the temperature.", gas_name(typeof(datum_gas_zauker)))),
            Dm.kv("Temperature", Dm.text("Can only occur between [] - []K. [] formation rate is proportional to the temperature.", ZAUKER_FORMATION_MIN_TEMPERATURE, ZAUKER_FORMATION_MAX_TEMPERATURE, gas_name(typeof(datum_gas_zauker)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is absorbed per mole of [] formed.", 2 * ZAUKER_FORMATION_ENERGY, gas_name(typeof(datum_gas_zauker))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:215
public partial class datum_gas_reaction_standard_zauker_decomp : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:215
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_zauker), Dm.text("[] is consumed at [] moles per second in any unique gas mixture.", gas_name(typeof(datum_gas_zauker)), Dm.div(ZAUKER_DECOMPOSITION_MAX_RATE * SECONDS, SSair.wait))),
            Dm.kv(typeof(datum_gas_nitrogen), Dm.text("At least [] moles of [] needs to be present for this reaction to occur. [] is produced at 0.7 moles per mole of [] decomposed.", MINIMUM_MOLE_COUNT, gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_zauker)))),
            Dm.kv(typeof(datum_gas_oxygen), Dm.text("[] is produced at 0.3 moles per mole of [] decomposed.", gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_zauker)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] decomposed.", ZAUKER_DECOMPOSITION_ENERGY, gas_name(typeof(datum_gas_zauker))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:224
public partial class datum_gas_reaction_standard_proto_nitrate_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:224
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_pluoxium), Dm.text("[] is consumed at 1/11th of a mole per mole of [] formed.", gas_name(typeof(datum_gas_pluoxium)), gas_name(typeof(datum_gas_proto_nitrate)))),
            Dm.kv(typeof(datum_gas_hydrogen), Dm.text("[] is consumed at 10/11th of a mole per mole of [] formed.", gas_name(typeof(datum_gas_hydrogen)), gas_name(typeof(datum_gas_proto_nitrate)))),
            Dm.kv(typeof(datum_gas_proto_nitrate), Dm.text("[] is produced at a rate that scales with the temperature.", gas_name(typeof(datum_gas_proto_nitrate)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] formed.", Dm.div(PN_FORMATION_ENERGY, 2.2f), gas_name(typeof(datum_gas_proto_nitrate)))),
            Dm.kv("Temperature", Dm.text("Can only occur between [] - []K. Higher temperature increases [] formation rate.", PN_FORMATION_MIN_TEMPERATURE, PN_FORMATION_MAX_TEMPERATURE, gas_name(typeof(datum_gas_proto_nitrate))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:234
public partial class datum_gas_reaction_standard_proto_nitrate_hydrogen_response : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:234
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_hydrogen), Dm.text("[] moles of [] needs to be present for the reaction to occur. [] is consumed at 2 moles per mole of [] formed.", PN_HYDROGEN_CONVERSION_THRESHOLD, gas_name(typeof(datum_gas_hydrogen)), gas_name(typeof(datum_gas_hydrogen)), gas_name(typeof(datum_gas_proto_nitrate)))),
            Dm.kv(typeof(datum_gas_proto_nitrate), Dm.text("[] moles of [] needs to be present for the reaction to occur. [] is produced a rate that scales with its mole count, up to a max of [] moles per second.", MINIMUM_MOLE_COUNT, gas_name(typeof(datum_gas_proto_nitrate)), gas_name(typeof(datum_gas_proto_nitrate)), Dm.div(PN_HYDROGEN_CONVERSION_MAX_RATE * 0.5f * SECONDS, SSair.wait))),
            Dm.kv("Energy", Dm.text("[] joules of energy is absorbed per mole of [] formed.", PN_HYDROGEN_CONVERSION_ENERGY * 2, gas_name(typeof(datum_gas_proto_nitrate))))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:244
public partial class datum_gas_reaction_standard_proto_nitrate_tritium_response : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:244
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_tritium), Dm.text("[] radiates its neutrons at a rate that scales with the temperature and [] mole count.", gas_name(typeof(datum_gas_tritium)), gas_name(typeof(datum_gas_proto_nitrate)))),
            Dm.kv(typeof(datum_gas_proto_nitrate), Dm.text("[] is consumed at 0.005 moles per mole of neutrons released.", gas_name(typeof(datum_gas_proto_nitrate)))),
            Dm.kv(typeof(datum_gas_hydrogen), Dm.text("[] remains after the neutrons escape.", gas_name(typeof(datum_gas_hydrogen)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of neutron released.", Dm.div(PN_TRITIUM_CONVERSION_ENERGY, 2))),
            Dm.kv("Radiation", "Neutrons are released as ionising radiation.")
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:253
public partial class datum_gas_reaction_standard_proto_nitrate_bz_response : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:253
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_proto_nitrate), Dm.text("[] moles of [] needs to be present for the reaction to occur. [] accelerates the [] decomposition.", MINIMUM_MOLE_COUNT, gas_name(typeof(datum_gas_proto_nitrate)), gas_name(typeof(datum_gas_proto_nitrate)), gas_name(typeof(datum_gas_bz)))),
            Dm.kv(typeof(datum_gas_bz), Dm.text("[] gets decomposed into [] and []. The [] then decomposes into [] and [], with the [] then decaying into [].", gas_name(typeof(datum_gas_bz)), gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_nitrous_oxide)), gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_oxygen)), gas_name(typeof(datum_gas_helium)))),
            Dm.kv(typeof(datum_gas_nitrogen), Dm.text("[] is produced at 0.4 moles per mole of [] decomposed.", gas_name(typeof(datum_gas_nitrogen)), gas_name(typeof(datum_gas_bz)))),
            Dm.kv(typeof(datum_gas_helium), Dm.text("[] is produced at 1.6 moles per mole of [] decomposed.", gas_name(typeof(datum_gas_helium)), gas_name(typeof(datum_gas_bz)))),
            Dm.kv(typeof(datum_gas_plasma), Dm.text("[] is produced at 0.8 moles per mole of [] decomposed.", gas_name(typeof(datum_gas_plasma)), gas_name(typeof(datum_gas_bz)))),
            Dm.kv("Energy", Dm.text("[] joules of energy is released per mole of [] decomposed.", PN_BZASE_ENERGY, gas_name(typeof(datum_gas_bz)))),
            Dm.kv("Radiation", "Radiation is released during this decomposition process."),
            Dm.kv("Hallucinations", "This reaction can cause various carbon based lifeforms in the vicinity to hallucinate."),
            Dm.kv("Nuclear Particles", Dm.text("This reaction emits extremely high energy nuclear particles, up to [] per second per unique gas mixture.", 2 * PN_BZASE_NUCLEAR_PARTICLE_MAXIMUM)),
            Dm.kv("Temperature", Dm.text("Can only occur between [] - []K.", PN_BZASE_MIN_TEMP, PN_BZASE_MAX_TEMP))
        );
    }
}

// tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:271
public partial class datum_gas_reaction_standard_antinoblium_replication : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reaction_factors.dm:271
    public override void init_factors()
    {
        factor = Dm.list(
            Dm.kv(typeof(datum_gas_antinoblium), Dm.text("[] moles of [] is needed to replicate itself. Requires other gases to be converted to [].", MOLES_GAS_VISIBLE, gas_name(typeof(datum_gas_antinoblium)), gas_name(typeof(datum_gas_antinoblium)))),
            Dm.kv("Temperature", Dm.text("Can only occur above []K.", REACTION_OPPRESSION_MIN_TEMP))
        );
    }
}

