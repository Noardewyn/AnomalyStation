// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/gasmixtures/reactions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 3 (fire and plasma): the whole file (the reactions' factor texts, init_factors, are reaction_factors.cs, M7 slice T3).
// Deferred leaves: the radiation pulses of the tritium fire and the proto-nitrate
// responses (radiation, a later milestone), halon's resin foam (fluids and foam, M5), proto-nitrate's nuclear particles
// and hallucination pulse (later milestones), and the condensation's wash of the tile (the cleaning code, M5).

//Most other defines used in reactions are located in ..\__DEFINES\reactions.dm
// #define SET_REACTION_RESULTS(amount) air.reaction_results[type] = amount  (expanded inline, rules 11.1)

public static partial class GlobalProcs
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:4
    public static object init_gas_reactions()
    {
        DmList priority_reactions = Dm.list();

        //Builds a list of gas id to reaction group
        foreach (object gas_id in Dm.each(GLOB.meta_gas_info[META_GAS_ID]))
        {
            priority_reactions[gas_id] = Dm.list(
                /* PRIORITY_PRE_FORMATION = */ Dm.list(),
                /* PRIORITY_FORMATION = */ Dm.list(),
                /* PRIORITY_POST_FORMATION = */ Dm.list(),
                /* PRIORITY_FIRE = */ Dm.list()
            );
        }

        foreach (Type reaction_type in Dm.each_cast<Type>(subtypesof(typeof(datum_gas_reaction_standard))))
        {
            if (Dm.truthy(Dm.initial(reaction_type, (datum_gas_reaction_standard p) => p.exclude)))
                continue;
            datum_gas_reaction_standard reaction = Dm.@new(reaction_type) as datum_gas_reaction_standard;
            Type reaction_key = null;
            foreach (object req in Dm.each(reaction.requirements))
            {
                if (ispath(req))
                {
                    Type req_gas = req as Type;
                    if (reaction_key == null || Dm.initial(reaction_key, (datum_gas p) => p.rarity) > Dm.initial(req_gas, (datum_gas p) => p.rarity))
                        reaction_key = req_gas;
                }
            }
            reaction.major_gas = reaction_key;
            Dm.append_at((DmList)priority_reactions[reaction_key], reaction.priority_group, reaction);
        }

        //Culls empty gases
        foreach (object gas_id in Dm.each(GLOB.meta_gas_info[META_GAS_ID]))
        {
            bool passed = false;
            foreach (DmList priority_grouping in Dm.each<DmList>(priority_reactions[gas_id]))
            {
                if (Dm.truthy(length(priority_grouping)))
                {
                    passed = true;
                    break;
                }
            }
            if (passed)
                continue;
            priority_reactions[gas_id] = null;
        }

        return priority_reactions;
    }
}

// tg: code/modules/atmospherics/gasmixtures/reactions.dm:70
public partial class datum_gas_reaction_standard : datum_gas_reaction
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:78
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        init_reqs();
        init_factors();
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:83
    public virtual void init_reqs() // Override this
    {
        throw Dm.CRASH(Dm.text("Reaction [] made without specifying requirements.", type), "code/modules/atmospherics/gasmixtures/reactions.dm", 84);
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:86
    public virtual void init_factors()
    {
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:88
    public virtual async DmTask<object> react(datum_gas_mixture air = null, object location = null)
    {
        return NO_REACTION;
    }
}

/**
 * Steam Condensation/Deposition:
 *
 * Makes turfs slippery.
 * Can frost things if the gas is cold enough.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:98
public partial class datum_gas_reaction_standard_water_vapor : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:104
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_water_vapor), MOLES_GAS_VISIBLE),
            Dm.kv("MAX_TEMP", WATER_VAPOR_CONDENSATION_POINT)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:110
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        if (!isturf(holder))
            return dot;

        turf_open location = holder as turf_open;
        float consumed = 0;
        float sw = air.temperature;
        if (sw <= WATER_VAPOR_DEPOSITION_POINT) // -INFINITY to WATER_VAPOR_DEPOSITION_POINT
        {
            if (location != null && Dm.truthy(await location.freeze_turf()))
                consumed = MOLES_GAS_VISIBLE;
            datum_component_wet_floor wet_floor = location?.GetComponent(typeof(datum_component_wet_floor)) as datum_component_wet_floor;
            if (wet_floor != null && Dm.eq(wet_floor.highest_strength, TURF_WET_PERMAFROST))
                dot |= VOLATILE_REACTION;
        }
        else if (sw >= WATER_VAPOR_DEPOSITION_POINT && sw <= WATER_VAPOR_CONDENSATION_POINT) // WATER_VAPOR_DEPOSITION_POINT to WATER_VAPOR_CONDENSATION_POINT
        {
            if (!isgroundlessturf(location) && !isnoslipturf(location))
                await location.water_vapor_gas_act();
            consumed = MOLES_GAS_VISIBLE;
        }

        if (consumed != 0)
        {
            Dm.remove_at(air.moles, typeof(datum_gas_water_vapor), consumed);
            air.reaction_results[type] = consumed;
            dot |= REACTING;
        }
        return dot;
    }
}

/**
 * Dry Heat Sterilization:
 *
 * Clears out pathogens in the air.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:140
public partial class datum_gas_reaction_standard_miaster : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:146
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_miasma), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", MIASTER_STERILIZATION_TEMP)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:152
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float water_vapor_moles = Dm.num(cached_moles[typeof(datum_gas_water_vapor)]);
        float miasma_moles = Dm.num(cached_moles[typeof(datum_gas_miasma)]);
        // As the name says it, it needs to be dry
        if (water_vapor_moles != 0 && water_vapor_moles / Dm.num(air.total_moles()) > MIASTER_STERILIZATION_MAX_HUMIDITY)
            return NO_REACTION;

        //Replace miasma with oxygen
        float cleaned_air = min(miasma_moles, MIASTER_STERILIZATION_RATE_BASE + (air.temperature - MIASTER_STERILIZATION_TEMP) / MIASTER_STERILIZATION_RATE_SCALE);
        Dm.remove_at(cached_moles, typeof(datum_gas_miasma), cleaned_air);
        Dm.append_at(cached_moles, typeof(datum_gas_oxygen), cleaned_air);

        //Possibly burning a bit of organic matter through maillard reaction, so a *tiny* bit more heat would be understandable
        air.temperature += cleaned_air * MIASTER_STERILIZATION_ENERGY;
        air.reaction_results[type] = cleaned_air;

        return REACTING;
    }
}

// Fire:

/**
 * Plasma combustion:
 *
 * Combustion of oxygen and plasma (mostly treated as hydrocarbons).
 * The reaction rate is dependent on the temperature of the gasmix.
 * May produce either tritium or carbon dioxide and water vapor depending on the fuel/oxydizer ratio of the gasmix.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:181
public partial class datum_gas_reaction_standard_plasmafire : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:189
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_plasma), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_oxygen), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", PLASMA_MINIMUM_BURN_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:196
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        // This reaction should proceed faster at higher temperatures.
        float temperature = air.temperature;
        float temperature_scale = 0;
        if (temperature > PLASMA_UPPER_TEMPERATURE)
            temperature_scale = 1;
        else
        {
            temperature_scale = (temperature - PLASMA_MINIMUM_BURN_TEMPERATURE) / (PLASMA_UPPER_TEMPERATURE - PLASMA_MINIMUM_BURN_TEMPERATURE);
            if (temperature_scale <= 0)
                return dot;
        }

        float oxygen_burn_ratio = OXYGEN_BURN_RATIO_BASE - temperature_scale;
        float plasma_burn_rate = 0;
        bool super_saturation = false; // Whether we should make tritium.
        DmList cached_moles = air.moles; //this speeds things up because accessing datum vars is slow
        float oxygen_moles = Dm.num(cached_moles[typeof(datum_gas_oxygen)]);
        float plasma_moles = Dm.num(cached_moles[typeof(datum_gas_plasma)]);
        float sw = Dm.div(oxygen_moles, plasma_moles);
        if (sw >= SUPER_SATURATION_THRESHOLD) // SUPER_SATURATION_THRESHOLD to INFINITY
        {
            plasma_burn_rate = (plasma_moles / PLASMA_BURN_RATE_DELTA) * temperature_scale;
            super_saturation = true; // Begin to form tritium
        }
        else if (sw >= PLASMA_OXYGEN_FULLBURN && sw <= SUPER_SATURATION_THRESHOLD) // PLASMA_OXYGEN_FULLBURN to SUPER_SATURATION_THRESHOLD
            plasma_burn_rate = (plasma_moles / PLASMA_BURN_RATE_DELTA) * temperature_scale;
        else
            plasma_burn_rate = ((oxygen_moles / PLASMA_OXYGEN_FULLBURN) / PLASMA_BURN_RATE_DELTA) * temperature_scale;

        if (plasma_burn_rate < MINIMUM_HEAT_CAPACITY)
            return dot;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        plasma_burn_rate = min(plasma_burn_rate, plasma_moles, oxygen_moles * INVERSE(oxygen_burn_ratio)); //Ensures matter is conserved properly
        cached_moles[typeof(datum_gas_plasma)] = QUANTIZE(plasma_moles - plasma_burn_rate);
        cached_moles[typeof(datum_gas_oxygen)] = QUANTIZE(oxygen_moles - (plasma_burn_rate * oxygen_burn_ratio));
        if (super_saturation)
            Dm.append_at(cached_moles, typeof(datum_gas_tritium), plasma_burn_rate);
        else
        {
            Dm.append_at(cached_moles, typeof(datum_gas_carbon_dioxide), plasma_burn_rate * 0.75f);
            Dm.append_at(cached_moles, typeof(datum_gas_water_vapor), plasma_burn_rate * 0.25f);
        }

        air.reaction_results[type] = (plasma_burn_rate) * (1 + oxygen_burn_ratio);
        float energy_released = FIRE_PLASMA_ENERGY_RELEASED * plasma_burn_rate;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = (temperature * old_heat_capacity + energy_released) / new_heat_capacity;

        // Let the floor know a fire is happening
        turf_open location = holder as turf_open;
        if (location != null)
        {
            temperature = air.temperature;
            if (temperature > FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
                location.hotspot_expose(temperature, CELL_VOLUME);
        }

        dot |= REACTING | VOLATILE_REACTION;
        return dot;
    }
}

/**
 * Hydrogen combustion:
 *
 * Combustion of oxygen and hydrogen.
 * Highly exothermic.
 * Creates hotspots.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:260
public partial class datum_gas_reaction_standard_h2fire : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:267
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_hydrogen), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_oxygen), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", HYDROGEN_MINIMUM_BURN_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:274
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        DmList cached_moles = air.moles; //this speeds things up because accessing datum vars is slow
        float hydrogen_moles = Dm.num(cached_moles[typeof(datum_gas_hydrogen)]);
        float oxygen_moles = Dm.num(cached_moles[typeof(datum_gas_oxygen)]);
        float old_heat_capacity = Dm.num(air.heat_capacity());
        float temperature = air.temperature;

        float burned_fuel = min(hydrogen_moles / FIRE_HYDROGEN_BURN_RATE_DELTA, oxygen_moles / (FIRE_HYDROGEN_BURN_RATE_DELTA * HYDROGEN_OXYGEN_FULLBURN), hydrogen_moles, oxygen_moles * INVERSE(0.5f));
        if (burned_fuel <= 0 || hydrogen_moles - burned_fuel < 0 || oxygen_moles - burned_fuel * 0.5f < 0) //Shouldn't produce gas from nothing.
            return dot;

        Dm.remove_at(cached_moles, typeof(datum_gas_hydrogen), burned_fuel);
        Dm.remove_at(cached_moles, typeof(datum_gas_oxygen), burned_fuel * 0.5f);
        Dm.append_at(cached_moles, typeof(datum_gas_water_vapor), burned_fuel);

        air.reaction_results[type] = burned_fuel;

        float energy_released = FIRE_HYDROGEN_ENERGY_RELEASED * burned_fuel;
        if (energy_released > 0)
        {
            float new_heat_capacity = Dm.num(air.heat_capacity());
            if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
                air.temperature = (temperature * old_heat_capacity + energy_released) / new_heat_capacity;
        }

        //let the floor know a fire is happening
        turf_open location = holder as turf_open;
        if (location != null)
        {
            temperature = air.temperature;
            if (temperature > FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
                location.hotspot_expose(temperature, CELL_VOLUME);
        }

        dot |= REACTING | VOLATILE_REACTION;
        return dot;
    }
}

/**
 * Tritium combustion:
 *
 * Combustion of oxygen and tritium (treated as hydrogen).
 * Highly exothermic.
 * Creates hotspots.
 * Creates radiation.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:316
public partial class datum_gas_reaction_standard_tritfire : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:323
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_tritium), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_oxygen), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", TRITIUM_MINIMUM_BURN_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:330
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        DmList cached_moles = air.moles; //this speeds things up because accessing datum vars is slow
        float tritium_moles = Dm.num(cached_moles[typeof(datum_gas_tritium)]);
        float oxygen_moles = Dm.num(cached_moles[typeof(datum_gas_oxygen)]);
        float old_heat_capacity = Dm.num(air.heat_capacity());
        float temperature = air.temperature;

        float burned_fuel = min(tritium_moles / FIRE_TRITIUM_BURN_RATE_DELTA, oxygen_moles / (FIRE_TRITIUM_BURN_RATE_DELTA * TRITIUM_OXYGEN_FULLBURN), tritium_moles, oxygen_moles * INVERSE(0.5f));
        if (burned_fuel <= 0 || tritium_moles - burned_fuel < 0 || oxygen_moles - burned_fuel * 0.5f < 0) //Shouldn't produce gas from nothing.
            return dot;

        Dm.remove_at(cached_moles, typeof(datum_gas_tritium), burned_fuel);
        Dm.remove_at(cached_moles, typeof(datum_gas_oxygen), burned_fuel * 0.5f);
        Dm.append_at(cached_moles, typeof(datum_gas_water_vapor), burned_fuel);

        air.reaction_results[type] = burned_fuel;

        turf_open location = null;
        if (istype(holder, typeof(datum_pipeline))) //Find the tile the reaction is occurring on, or a random part of the network if it's a pipenet.
        {
            datum_pipeline pipenet = (datum_pipeline)holder;
            location = pick(pipenet.members) as turf_open;
        }
        else if (isatom(holder))
            location = holder as turf_open;

        float energy_released = FIRE_TRITIUM_ENERGY_RELEASED * burned_fuel;
        if (location != null && burned_fuel > TRITIUM_RADIATION_MINIMUM_MOLES && energy_released > TRITIUM_RADIATION_RELEASE_THRESHOLD * Dm.pow(Dm.num(air.volume) / CELL_VOLUME, ATMOS_RADIATION_VOLUME_EXP) && prob(10))
        {
            radiation_pulse(location, max_range: min(sqrt(burned_fuel) / TRITIUM_RADIATION_RANGE_DIVISOR, GAS_REACTION_MAXIMUM_RADIATION_PULSE_RANGE), threshold: TRITIUM_RADIATION_THRESHOLD);
        }

        if (energy_released > 0)
        {
            float new_heat_capacity = Dm.num(air.heat_capacity());
            if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
                air.temperature = (temperature * old_heat_capacity + energy_released) / new_heat_capacity;
        }

        //let the floor know a fire is happening
        if (location != null)
        {
            temperature = air.temperature;
            if (temperature > FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
                location.hotspot_expose(temperature, CELL_VOLUME);
        }

        dot |= REACTING | VOLATILE_REACTION;
        return dot;
    }
}

/**
 * Freon combustion:
 *
 * Combustion of oxygen and freon.
 * Endothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:380
public partial class datum_gas_reaction_standard_freonfire : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:387
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_freon), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", FREON_TERMINAL_TEMPERATURE),
            Dm.kv("MAX_TEMP", FREON_MAXIMUM_BURN_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:395
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        float temperature = air.temperature;
        float temperature_scale;
        if (temperature < FREON_TERMINAL_TEMPERATURE) //stop the reaction when too cold
            temperature_scale = 0;
        else if (temperature < FREON_LOWER_TEMPERATURE)
            temperature_scale = 0.5f;
        else
            temperature_scale = (FREON_MAXIMUM_BURN_TEMPERATURE - temperature) / (FREON_MAXIMUM_BURN_TEMPERATURE - FREON_TERMINAL_TEMPERATURE); //calculate the scale based on the temperature
        if (temperature_scale <= 0)
            return dot;

        float oxygen_burn_ratio = OXYGEN_BURN_RATIO_BASE - temperature_scale;
        float freon_burn_rate;
        DmList cached_moles = air.moles;
        float freon_moles = Dm.num(cached_moles[typeof(datum_gas_freon)]);
        float oxygen_moles = Dm.num(cached_moles[typeof(datum_gas_oxygen)]);
        if (oxygen_moles < freon_moles * FREON_OXYGEN_FULLBURN)
            freon_burn_rate = ((oxygen_moles / FREON_OXYGEN_FULLBURN) / FREON_BURN_RATE_DELTA) * temperature_scale;
        else
            freon_burn_rate = (freon_moles / FREON_BURN_RATE_DELTA) * temperature_scale;

        if (freon_burn_rate < MINIMUM_HEAT_CAPACITY)
            return dot;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        freon_burn_rate = min(freon_burn_rate, freon_moles, oxygen_moles * INVERSE(oxygen_burn_ratio)); //Ensures matter is conserved properly
        cached_moles[typeof(datum_gas_freon)] = QUANTIZE(freon_moles - freon_burn_rate);
        cached_moles[typeof(datum_gas_oxygen)] = QUANTIZE(oxygen_moles - (freon_burn_rate * oxygen_burn_ratio));
        Dm.append_at(cached_moles, typeof(datum_gas_carbon_dioxide), freon_burn_rate);

        if (temperature < HOT_ICE_FORMATION_MAXIMUM_TEMPERATURE && temperature > HOT_ICE_FORMATION_MINIMUM_TEMPERATURE && prob(HOT_ICE_FORMATION_PROB) && isturf(holder))
            Dm.@new<obj_item_stack_sheet_hot_ice>(holder);

        air.reaction_results[type] = freon_burn_rate * (1 + oxygen_burn_ratio);
        float energy_consumed = FIRE_FREON_ENERGY_CONSUMED * freon_burn_rate;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max((temperature * old_heat_capacity - energy_consumed) / new_heat_capacity, TCMB);

        turf_open location = holder as turf_open;
        if (location != null)
        {
            temperature = air.temperature;
            if (temperature < FREON_MAXIMUM_BURN_TEMPERATURE)
                location.hotspot_expose(temperature, CELL_VOLUME);
        }

        dot |= REACTING | VOLATILE_REACTION;
        return dot;
    }
}

// N2O

/**
 * Nitrous oxide Formation:
 *
 * Formation of N2O.
 * Endothermic.
 * Requires BZ as a catalyst.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:454
public partial class datum_gas_reaction_standard_nitrousformation : datum_gas_reaction_standard //formation of n2o, exothermic, requires bz as catalyst
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:460
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), 10),
            Dm.kv(typeof(datum_gas_nitrogen), 20),
            Dm.kv(typeof(datum_gas_bz), 5),
            Dm.kv("MIN_TEMP", N2O_FORMATION_MIN_TEMPERATURE),
            Dm.kv("MAX_TEMP", N2O_FORMATION_MAX_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:469
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float oxygen_moles = Dm.num(cached_moles[typeof(datum_gas_oxygen)]);
        float nitrogen_moles = Dm.num(cached_moles[typeof(datum_gas_nitrogen)]);
        float heat_efficiency = min(oxygen_moles * INVERSE(0.5f), nitrogen_moles);
        if ((oxygen_moles - heat_efficiency * 0.5f < 0) || (nitrogen_moles - heat_efficiency < 0))
            return NO_REACTION; // Shouldn't produce gas from nothing.

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_oxygen), heat_efficiency * 0.5f);
        Dm.remove_at(cached_moles, typeof(datum_gas_nitrogen), heat_efficiency);
        Dm.append_at(cached_moles, typeof(datum_gas_nitrous_oxide), heat_efficiency);

        air.reaction_results[type] = heat_efficiency;
        float energy_released = heat_efficiency * N2O_FORMATION_ENERGY;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((air.temperature * old_heat_capacity + energy_released) / new_heat_capacity), TCMB); // The air cools down when reacting.
        return REACTING;
    }
}

/**
 * Nitrous Oxide Decomposition
 *
 * Decomposition of N2O.
 * Exothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:496
public partial class datum_gas_reaction_standard_nitrous_decomp : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:502
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_nitrous_oxide), MINIMUM_MOLE_COUNT * 2),
            Dm.kv("MIN_TEMP", N2O_DECOMPOSITION_MIN_TEMPERATURE),
            Dm.kv("MAX_TEMP", N2O_DECOMPOSITION_MAX_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:509
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        DmList cached_moles = air.moles; //this speeds things up because accessing datum vars is slow
        float nitrous_oxide_moles = Dm.num(cached_moles[typeof(datum_gas_nitrous_oxide)]);
        float temperature = air.temperature;
        float burned_fuel = (nitrous_oxide_moles / N2O_DECOMPOSITION_RATE_DIVISOR) * ((temperature - N2O_DECOMPOSITION_MIN_SCALE_TEMP) * (temperature - N2O_DECOMPOSITION_MAX_SCALE_TEMP) / (N2O_DECOMPOSITION_SCALE_DIVISOR));
        if (burned_fuel <= 0 || nitrous_oxide_moles - burned_fuel < 0)
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_nitrous_oxide), burned_fuel);
        Dm.append_at(cached_moles, typeof(datum_gas_nitrogen), burned_fuel);
        Dm.append_at(cached_moles, typeof(datum_gas_oxygen), burned_fuel / 2);

        air.reaction_results[type] = burned_fuel;
        float energy_released = N2O_DECOMPOSITION_ENERGY * burned_fuel;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = (temperature * old_heat_capacity + energy_released) / new_heat_capacity;
        dot |= REACTING;
        return dot;
    }
}

// BZ

/**
 * BZ Formation
 *
 * Formation of BZ by combining plasma and nitrous oxide at low pressures.
 * Exothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:538
public partial class datum_gas_reaction_standard_bzformation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:544
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_nitrous_oxide), 10),
            Dm.kv(typeof(datum_gas_plasma), 10),
            Dm.kv("MAX_TEMP", BZ_FORMATION_MAX_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:551
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float nitrous_oxide_moles = Dm.num(cached_moles[typeof(datum_gas_nitrous_oxide)]);
        float plasma_moles = Dm.num(cached_moles[typeof(datum_gas_plasma)]);
        float pressure = Dm.num(air.return_pressure());
        float volume = Dm.num(air.return_volume());
        float environment_effciency = Dm.div(volume, pressure); //More volume and less pressure gives better rates
        float ratio_efficency = min(Dm.div(nitrous_oxide_moles, plasma_moles), 1); //Less n2o than plasma give lower rates
        float nitrous_oxide_decomposed_factor = max(4 * (Dm.div(plasma_moles, (nitrous_oxide_moles + plasma_moles)) - 0.75f), 0); // Nitrous oxide decomposes when there are more than 3 parts plasma per n2o.
        float bz_formed = min(0.01f * ratio_efficency * environment_effciency, nitrous_oxide_moles * INVERSE(0.4f), plasma_moles * INVERSE(0.8f * (1 - nitrous_oxide_decomposed_factor)));

        if (nitrous_oxide_moles - bz_formed * 0.4f < 0 || plasma_moles - 0.8f * bz_formed * (1 - nitrous_oxide_decomposed_factor) < 0 || bz_formed <= 0)
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());

        /**
        *If n2o-plasma ratio is less than 1:3 start decomposing n2o.
        *Rate of decomposition vs BZ production increases as n2o concentration gets lower
        *Plasma acts as a catalyst on decomposition, so it doesn't get consumed in the process.
        *N2O decomposes with its normal decomposition energy
        */
        if (nitrous_oxide_decomposed_factor > 0)
        {
            float amount_decomposed = 0.4f * bz_formed * nitrous_oxide_decomposed_factor;
            Dm.append_at(cached_moles, typeof(datum_gas_nitrogen), amount_decomposed);
            Dm.append_at(cached_moles, typeof(datum_gas_oxygen), 0.5f * amount_decomposed);
        }

        Dm.append_at(cached_moles, typeof(datum_gas_bz), bz_formed * (1 - nitrous_oxide_decomposed_factor));
        Dm.remove_at(cached_moles, typeof(datum_gas_nitrous_oxide), 0.4f * bz_formed);
        Dm.remove_at(cached_moles, typeof(datum_gas_plasma), 0.8f * bz_formed * (1 - nitrous_oxide_decomposed_factor));

        air.reaction_results[type] = bz_formed;
        float energy_released = bz_formed * (BZ_FORMATION_ENERGY + nitrous_oxide_decomposed_factor * (N2O_DECOMPOSITION_ENERGY - BZ_FORMATION_ENERGY));
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((air.temperature * old_heat_capacity + energy_released) / new_heat_capacity), TCMB);
        return REACTING;
    }
}

// Pluoxium

/**
 * Pluoxium Formation:
 *
 * Consumes a tiny amount of tritium to convert CO2 and oxygen to pluoxium.
 * Exothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:598
public partial class datum_gas_reaction_standard_pluox_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:604
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_carbon_dioxide), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_oxygen), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_tritium), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", PLUOXIUM_FORMATION_MIN_TEMP),
            Dm.kv("MAX_TEMP", PLUOXIUM_FORMATION_MAX_TEMP)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:613
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float carbon_dioxide_moles = Dm.num(cached_moles[typeof(datum_gas_carbon_dioxide)]);
        float oxygen_moles = Dm.num(cached_moles[typeof(datum_gas_oxygen)]);
        float tritium_moles = Dm.num(cached_moles[typeof(datum_gas_tritium)]);
        float produced_amount = min(PLUOXIUM_FORMATION_MAX_RATE, carbon_dioxide_moles, oxygen_moles * INVERSE(0.5f), tritium_moles * INVERSE(0.01f));
        if (produced_amount <= 0 || carbon_dioxide_moles - produced_amount < 0 || oxygen_moles - produced_amount * 0.5f < 0 || tritium_moles - produced_amount * 0.01f < 0)
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_carbon_dioxide), produced_amount);
        Dm.remove_at(cached_moles, typeof(datum_gas_oxygen), produced_amount * 0.5f);
        Dm.remove_at(cached_moles, typeof(datum_gas_tritium), produced_amount * 0.01f);
        Dm.append_at(cached_moles, typeof(datum_gas_pluoxium), produced_amount);
        Dm.append_at(cached_moles, typeof(datum_gas_hydrogen), produced_amount * 0.01f);

        air.reaction_results[type] = produced_amount;
        float energy_released = produced_amount * PLUOXIUM_FORMATION_ENERGY;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max((air.temperature * old_heat_capacity + energy_released) / new_heat_capacity, TCMB);
        return REACTING;
    }
}

// Nitrium

/**
 * Nitrium Formation:
 *
 * The formation of nitrium.
 * Endothermic.
 * Requires BZ.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:646
public partial class datum_gas_reaction_standard_nitrium_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:652
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_tritium), 20),
            Dm.kv(typeof(datum_gas_nitrogen), 10),
            Dm.kv(typeof(datum_gas_bz), 5),
            Dm.kv("MIN_TEMP", NITRIUM_FORMATION_MIN_TEMP)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:660
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float tritium_moles = Dm.num(cached_moles[typeof(datum_gas_tritium)]);
        float nitrogen_moles = Dm.num(cached_moles[typeof(datum_gas_nitrogen)]);
        float bz_moles = Dm.num(cached_moles[typeof(datum_gas_bz)]);
        float temperature = air.temperature;
        float heat_efficiency = min(temperature / NITRIUM_FORMATION_TEMP_DIVISOR, tritium_moles, nitrogen_moles, bz_moles * INVERSE(0.05f));

        if (heat_efficiency <= 0 || (tritium_moles - heat_efficiency < 0) || (nitrogen_moles - heat_efficiency < 0) || (bz_moles - heat_efficiency * 0.05f < 0)) //Shouldn't produce gas from nothing.
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_tritium), heat_efficiency);
        Dm.remove_at(cached_moles, typeof(datum_gas_nitrogen), heat_efficiency);
        Dm.remove_at(cached_moles, typeof(datum_gas_bz), heat_efficiency * 0.05f); //bz gets consumed to balance the nitrium production and not make it too common and/or easy
        Dm.append_at(cached_moles, typeof(datum_gas_nitrium), heat_efficiency);

        air.reaction_results[type] = heat_efficiency;
        float energy_used = heat_efficiency * NITRIUM_FORMATION_ENERGY;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((temperature * old_heat_capacity - energy_used) / new_heat_capacity), TCMB); //the air cools down when reacting
        return REACTING;
    }
}

/**
 * Nitrium Decomposition:
 *
 * The decomposition of nitrium.
 * Exothermic.
 * Requires oxygen as catalyst.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:693
public partial class datum_gas_reaction_standard_nitrium_decomposition : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:699
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_nitrium), MINIMUM_MOLE_COUNT),
            Dm.kv("MAX_TEMP", NITRIUM_DECOMPOSITION_MAX_TEMP)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:706
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float nitrium_moles = Dm.num(cached_moles[typeof(datum_gas_nitrium)]);
        float temperature = air.temperature;

        //This reaction is aggressively slow. like, a tenth of a mole per fire slow. Keep that in mind
        float heat_efficiency = min(temperature / NITRIUM_DECOMPOSITION_TEMP_DIVISOR, nitrium_moles);

        if (heat_efficiency <= 0 || (nitrium_moles - heat_efficiency < 0)) //Shouldn't produce gas from nothing.
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        air.assert_gases(typeof(datum_gas_nitrogen), typeof(datum_gas_hydrogen));
        Dm.remove_at(cached_moles, typeof(datum_gas_nitrium), heat_efficiency);
        Dm.append_at(cached_moles, typeof(datum_gas_hydrogen), heat_efficiency);
        Dm.append_at(cached_moles, typeof(datum_gas_nitrogen), heat_efficiency);

        air.reaction_results[type] = heat_efficiency;
        float energy_released = heat_efficiency * NITRIUM_DECOMPOSITION_ENERGY;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((temperature * old_heat_capacity + energy_released) / new_heat_capacity), TCMB); //the air heats up when reacting
        return REACTING;
    }
}

/**
 * Freon formation:
 *
 * The formation of freon.
 * Endothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:737
public partial class datum_gas_reaction_standard_freonformation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:743
    public override void init_reqs() //minimum requirements for freon formation
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_plasma), MINIMUM_MOLE_COUNT * 6),
            Dm.kv(typeof(datum_gas_carbon_dioxide), MINIMUM_MOLE_COUNT * 3),
            Dm.kv(typeof(datum_gas_bz), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", FREON_FORMATION_MIN_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:751
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float plasma_moles = Dm.num(cached_moles[typeof(datum_gas_plasma)]);
        float carbon_dioxide_moles = Dm.num(cached_moles[typeof(datum_gas_carbon_dioxide)]);
        float bz_moles = Dm.num(cached_moles[typeof(datum_gas_bz)]);
        float temperature = air.temperature;
        float minimal_mole_factor = min(plasma_moles * INVERSE(0.6f), bz_moles * INVERSE(0.1f), carbon_dioxide_moles * INVERSE(0.3f));

        float equation_first_part = Dm.pow(NUM_E, (-(Dm.pow(((temperature - 800) / 200), 2))));
        float equation_second_part = 3 / (1 + Dm.pow(NUM_E, (-0.001f * (temperature - 6000))));
        float heat_factor = equation_first_part + equation_second_part;

        float freon_formed = min(heat_factor * minimal_mole_factor * 0.05f, plasma_moles * INVERSE(0.6f), carbon_dioxide_moles * INVERSE(0.3f), bz_moles * INVERSE(0.1f));
        if (freon_formed <= 0 || (plasma_moles - freon_formed * 0.6f < 0) || (carbon_dioxide_moles - freon_formed * 0.3f < 0) || (bz_moles - freon_formed * 0.1f < 0)) //Shouldn't produce gas from nothing.
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_plasma), freon_formed * 0.6f);
        Dm.remove_at(cached_moles, typeof(datum_gas_carbon_dioxide), freon_formed * 0.3f);
        Dm.remove_at(cached_moles, typeof(datum_gas_bz), freon_formed * 0.1f);
        Dm.append_at(cached_moles, typeof(datum_gas_freon), freon_formed);

        air.reaction_results[type] = freon_formed;

        float energy_consumed = (7000 / (1 + Dm.pow(NUM_E, (-0.0015f * (temperature - 6000)))) + 1000) * freon_formed * 0.1f;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((temperature * old_heat_capacity - energy_consumed) / new_heat_capacity), TCMB);
        return REACTING;
    }
}

/**
 * Hyper-Noblium Formation:
 *
 * Extremely exothermic.
 * Requires very low temperatures.
 * Due to its high mass, hyper-noblium uses large amounts of nitrogen and tritium.
 * BZ can be used as a catalyst to make it less exothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:790
public partial class datum_gas_reaction_standard_nobliumformation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:796
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_nitrogen), 10),
            Dm.kv(typeof(datum_gas_tritium), 5),
            Dm.kv("MIN_TEMP", NOBLIUM_FORMATION_MIN_TEMP),
            Dm.kv("MAX_TEMP", NOBLIUM_FORMATION_MAX_TEMP)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:804
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        DmList cached_moles = air.moles;
        float nitrogen_moles = Dm.num(cached_moles[typeof(datum_gas_nitrogen)]);
        float tritium_moles = Dm.num(cached_moles[typeof(datum_gas_tritium)]);
        /// List of gases we will assert, and possibly garbage collect.
        DmList asserted_gases = Dm.list(typeof(datum_gas_hypernoblium), typeof(datum_gas_bz));
        air.assert_gases(Dm.arglist(asserted_gases));
        float bz_moles = Dm.num(cached_moles[typeof(datum_gas_bz)]);
        float reduction_factor = clamp(Dm.div(tritium_moles, (tritium_moles + bz_moles)), 0.001f, 1); //reduces trit consumption in presence of bz upward to 0.1% reduction
        float nob_formed = min((nitrogen_moles + tritium_moles) * 0.01f, tritium_moles * INVERSE(5 * reduction_factor), nitrogen_moles * INVERSE(10));

        //calling QUANTIZE on results to round very small floating point values.
        if (QUANTIZE(nob_formed) <= 0 || (QUANTIZE(tritium_moles - 5 * nob_formed * reduction_factor) < 0) || (QUANTIZE(nitrogen_moles - 10 * nob_formed) < 0))
        {
            air.garbage_collect();
            return dot;
        }

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_tritium), 5 * nob_formed * reduction_factor);
        Dm.remove_at(cached_moles, typeof(datum_gas_nitrogen), 10 * nob_formed);
        Dm.append_at(cached_moles, typeof(datum_gas_hypernoblium), nob_formed); // I'm not going to nitpick, but N20H10 feels like it should be an explosive more than anything.

        air.reaction_results[type] = nob_formed;
        float energy_released = nob_formed * NOBLIUM_FORMATION_ENERGY / max(bz_moles, 1);
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((air.temperature * old_heat_capacity + energy_released) / new_heat_capacity), TCMB);
        dot |= REACTING | VOLATILE_REACTION;
        return dot;
    }
}

// Halon

/**
 * Halon Combustion:
 *
 * Consumes a large amount of oxygen relative to the amount of halon consumed.
 * Produces carbon dioxide.
 * Endothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:843
public partial class datum_gas_reaction_standard_halon_o2removal : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:849
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_halon), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_oxygen), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", HALON_COMBUSTION_MIN_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:856
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        DmList cached_moles = air.moles;
        float halon_moles = Dm.num(cached_moles[typeof(datum_gas_halon)]);
        float oxygen_moles = Dm.num(cached_moles[typeof(datum_gas_oxygen)]);
        float temperature = air.temperature;

        float heat_efficiency = min(temperature / HALON_COMBUSTION_TEMPERATURE_SCALE, halon_moles, oxygen_moles * INVERSE(20));
        if (heat_efficiency <= 0 || (halon_moles - heat_efficiency < 0) || (oxygen_moles - heat_efficiency * 20 < 0)) //Shouldn't produce gas from nothing.
            return dot;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_halon), heat_efficiency);
        Dm.remove_at(cached_moles, typeof(datum_gas_oxygen), heat_efficiency * 20);
        Dm.append_at(cached_moles, typeof(datum_gas_pluoxium), heat_efficiency * 2.5f);

        air.reaction_results[type] = heat_efficiency * 5;
        float energy_used = heat_efficiency * HALON_COMBUSTION_ENERGY;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((temperature * old_heat_capacity - energy_used) / new_heat_capacity), TCMB);

        // Resin foam effects.
        turf_open location = holder as turf_open;
        obj_effect_particle_effect_fluid_foam foam = Dm.locate<obj_effect_particle_effect_fluid_foam>(location);
        obj_structure_foamedmetal resin = Dm.locate<obj_structure_foamedmetal>(location);
        if (heat_efficiency > HALON_COMBUSTION_MINIMUM_RESIN_MOLES && isopenturf(location) && !Dm.truthy(foam) && !Dm.truthy(resin)) // Don't resin if there is aleady resin or we are not in an open turf.
        {
            await do_foam(amount: HALON_COMBUSTION_RESIN_VOLUME, holder: holder as atom, location: location, foam_type: typeof(datum_effect_system_fluid_spread_foam_metal_resin_halon)); // gaps F1
            dot |= VOLATILE_REACTION;
        }

        dot |= REACTING;
        return dot;
    }
}

// Healium

/**
 * Healium Formation:
 *
 * Exothermic
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:896
public partial class datum_gas_reaction_standard_healium_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:902
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_bz), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_freon), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", HEALIUM_FORMATION_MIN_TEMP),
            Dm.kv("MAX_TEMP", HEALIUM_FORMATION_MAX_TEMP)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:910
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float bz_moles = Dm.num(cached_moles[typeof(datum_gas_bz)]);
        float freon_moles = Dm.num(cached_moles[typeof(datum_gas_freon)]);
        float temperature = air.temperature;
        float heat_efficiency = min(temperature * 0.3f, freon_moles * INVERSE(2.75f), bz_moles * INVERSE(0.25f));
        if (heat_efficiency <= 0 || (freon_moles - heat_efficiency * 2.75f < 0) || (bz_moles - heat_efficiency * 0.25f < 0)) //Shouldn't produce gas from nothing.
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_freon), heat_efficiency * 2.75f);
        Dm.remove_at(cached_moles, typeof(datum_gas_bz), heat_efficiency * 0.25f);
        Dm.append_at(cached_moles, typeof(datum_gas_healium), heat_efficiency * 3);

        air.reaction_results[type] = heat_efficiency * 3;
        float energy_released = heat_efficiency * HEALIUM_FORMATION_ENERGY;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((temperature * old_heat_capacity + energy_released) / new_heat_capacity), TCMB);
        return REACTING;
    }
}

/**
 * Zauker Formation:
 *
 * Exothermic.
 * Requires Hypernoblium.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:937
public partial class datum_gas_reaction_standard_zauker_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:943
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_hypernoblium), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_nitrium), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", ZAUKER_FORMATION_MIN_TEMPERATURE),
            Dm.kv("MAX_TEMP", ZAUKER_FORMATION_MAX_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:951
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float hypernoblium_moles = Dm.num(cached_moles[typeof(datum_gas_hypernoblium)]);
        float nitrium_moles = Dm.num(cached_moles[typeof(datum_gas_nitrium)]);
        float temperature = air.temperature;

        float heat_efficiency = min(temperature * ZAUKER_FORMATION_TEMPERATURE_SCALE, hypernoblium_moles * INVERSE(0.01f), nitrium_moles * INVERSE(0.5f));
        if (heat_efficiency <= 0 || (hypernoblium_moles - heat_efficiency * 0.01f < 0) || (nitrium_moles - heat_efficiency * 0.5f < 0)) //Shouldn't produce gas from nothing.
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_hypernoblium), heat_efficiency * 0.01f);
        Dm.remove_at(cached_moles, typeof(datum_gas_nitrium), heat_efficiency * 0.5f);
        Dm.append_at(cached_moles, typeof(datum_gas_zauker), heat_efficiency * 0.5f);

        air.reaction_results[type] = heat_efficiency * 0.5f;
        float energy_used = heat_efficiency * ZAUKER_FORMATION_ENERGY;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((temperature * old_heat_capacity - energy_used) / new_heat_capacity), TCMB);
        return REACTING;
    }
}

/**
 * Zauker Decomposition:
 *
 * Occurs in the presence of nitrogen to prevent zauker floods.
 * Exothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:980
public partial class datum_gas_reaction_standard_zauker_decomp : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:986
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_nitrogen), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_zauker), MINIMUM_MOLE_COUNT)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:992
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles; //this speeds things up because accessing datum vars is slow
        float nitrogen_moles = Dm.num(cached_moles[typeof(datum_gas_nitrogen)]);
        float zauker_moles = Dm.num(cached_moles[typeof(datum_gas_zauker)]);
        float burned_fuel = min(ZAUKER_DECOMPOSITION_MAX_RATE, nitrogen_moles, zauker_moles);
        if (burned_fuel <= 0 || zauker_moles - burned_fuel < 0)
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_zauker), burned_fuel);
        Dm.append_at(cached_moles, typeof(datum_gas_oxygen), burned_fuel * 0.3f);
        Dm.append_at(cached_moles, typeof(datum_gas_nitrogen), burned_fuel * 0.7f);

        air.reaction_results[type] = burned_fuel;
        float energy_released = ZAUKER_DECOMPOSITION_ENERGY * burned_fuel;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max((air.temperature * old_heat_capacity + energy_released) / new_heat_capacity, TCMB);
        return REACTING;
    }
}

// Proto-Nitrate

/**
 * Proto-Nitrate formation:
 *
 * Exothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:1020
public partial class datum_gas_reaction_standard_proto_nitrate_formation : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1026
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_pluoxium), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_hydrogen), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", PN_FORMATION_MIN_TEMPERATURE),
            Dm.kv("MAX_TEMP", PN_FORMATION_MAX_TEMPERATURE)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1034
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float pluoxium_moles = Dm.num(cached_moles[typeof(datum_gas_pluoxium)]);
        float hydrogen_moles = Dm.num(cached_moles[typeof(datum_gas_hydrogen)]);
        float temperature = air.temperature;

        float heat_efficiency = min(temperature * 0.005f, pluoxium_moles * INVERSE(0.2f), hydrogen_moles * INVERSE(2));
        if (heat_efficiency <= 0 || (pluoxium_moles - heat_efficiency * 0.2f < 0) || (hydrogen_moles - heat_efficiency * 2 < 0)) //Shouldn't produce gas from nothing.
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_hydrogen), heat_efficiency * 2);
        Dm.remove_at(cached_moles, typeof(datum_gas_pluoxium), heat_efficiency * 0.2f);
        Dm.append_at(cached_moles, typeof(datum_gas_proto_nitrate), heat_efficiency * 2.2f);

        air.reaction_results[type] = heat_efficiency * 2.2f;
        float energy_released = heat_efficiency * PN_FORMATION_ENERGY;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(((temperature * old_heat_capacity + energy_released) / new_heat_capacity), TCMB);
        return REACTING;
    }
}

/**
 * Proto-Nitrate Hydrogen Conversion
 *
 * Converts hydrogen into proto-nitrate.
 * Endothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:1062
public partial class datum_gas_reaction_standard_proto_nitrate_hydrogen_response : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1068
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_proto_nitrate), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_hydrogen), PN_HYDROGEN_CONVERSION_THRESHOLD)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1074
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        DmList cached_moles = air.moles;
        float proto_nitrate_moles = Dm.num(cached_moles[typeof(datum_gas_proto_nitrate)]);
        float hydrogen_moles = Dm.num(cached_moles[typeof(datum_gas_hydrogen)]);
        float produced_amount = min(PN_HYDROGEN_CONVERSION_MAX_RATE, hydrogen_moles, proto_nitrate_moles);
        if (produced_amount <= 0 || hydrogen_moles - produced_amount < 0)
            return NO_REACTION;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_hydrogen), produced_amount);
        Dm.append_at(cached_moles, typeof(datum_gas_proto_nitrate), produced_amount * 0.5f);

        air.reaction_results[type] = produced_amount * 0.5f;
        float energy_used = produced_amount * PN_HYDROGEN_CONVERSION_ENERGY;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max((air.temperature * old_heat_capacity - energy_used) / new_heat_capacity, TCMB);
        return REACTING;
    }
}

/**
 * Proto-Nitrate Tritium De-irradiation
 *
 * Converts tritium to hydrogen.
 * Releases radiation.
 * Exothermic.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:1100
public partial class datum_gas_reaction_standard_proto_nitrate_tritium_response : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1106
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_proto_nitrate), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_tritium), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", PN_TRITIUM_CONVERSION_MIN_TEMP),
            Dm.kv("MAX_TEMP", PN_TRITIUM_CONVERSION_MAX_TEMP)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1114
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        DmList cached_moles = air.moles;
        float proto_nitrate_moles = Dm.num(cached_moles[typeof(datum_gas_proto_nitrate)]);
        float tritium_moles = Dm.num(cached_moles[typeof(datum_gas_tritium)]);
        float temperature = air.temperature;
        float produced_amount = min(temperature / 34 * (tritium_moles * proto_nitrate_moles) / (tritium_moles + 10 * proto_nitrate_moles), tritium_moles, proto_nitrate_moles * INVERSE(0.01f));
        if (tritium_moles - produced_amount < 0 || proto_nitrate_moles - produced_amount * 0.01f < 0)
            return dot;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_proto_nitrate), produced_amount * 0.01f);
        Dm.remove_at(cached_moles, typeof(datum_gas_tritium), produced_amount);
        Dm.append_at(cached_moles, typeof(datum_gas_hydrogen), produced_amount);

        air.reaction_results[type] = produced_amount;
        turf_open location = null;
        float energy_released = produced_amount * PN_TRITIUM_CONVERSION_ENERGY;
        if (istype(holder, typeof(datum_pipeline))) //Find the tile the reaction is occurring on, or a random part of the network if it's a pipenet.
        {
            datum_pipeline pipenet = (datum_pipeline)holder;
            location = pick(pipenet.members) as turf_open;
        }
        else if (isatom(holder))
            location = holder as turf_open;
        if (location != null && energy_released > PN_TRITIUM_CONVERSION_RAD_RELEASE_THRESHOLD * Dm.pow(Dm.num(air.volume) / CELL_VOLUME, ATMOS_RADIATION_VOLUME_EXP))
        {
            dot |= VOLATILE_REACTION;
            radiation_pulse(location, max_range: min(sqrt(produced_amount) / PN_TRITIUM_RAD_RANGE_DIVISOR, GAS_REACTION_MAXIMUM_RADIATION_PULSE_RANGE), threshold: PN_TRITIUM_RAD_THRESHOLD);
        }

        if (energy_released != 0)
        {
            float new_heat_capacity = Dm.num(air.heat_capacity());
            if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
                air.temperature = max((temperature * old_heat_capacity + energy_released) / new_heat_capacity, TCMB);

            dot |= REACTING;
        }
        return dot;
    }
}

/**
 * Proto-Nitrate BZase Action
 *
 * Breaks BZ down into nitrogen, helium, and plasma in the presence of proto-nitrate.
 */
// tg: code/modules/atmospherics/gasmixtures/reactions.dm:1153
public partial class datum_gas_reaction_standard_proto_nitrate_bz_response : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1159
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_proto_nitrate), MINIMUM_MOLE_COUNT),
            Dm.kv(typeof(datum_gas_bz), MINIMUM_MOLE_COUNT),
            Dm.kv("MIN_TEMP", PN_BZASE_MIN_TEMP),
            Dm.kv("MAX_TEMP", PN_BZASE_MAX_TEMP)
        );
    }

    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1167
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = NO_REACTION;
        DmList cached_moles = air.moles;
        float proto_nitrate_moles = Dm.num(cached_moles[typeof(datum_gas_proto_nitrate)]);
        float bz_moles = Dm.num(cached_moles[typeof(datum_gas_bz)]);
        float temperature = air.temperature;
        float consumed_amount = min(temperature / 2240 * bz_moles * proto_nitrate_moles / (bz_moles + proto_nitrate_moles), bz_moles, proto_nitrate_moles);
        if (consumed_amount <= 0 || bz_moles - consumed_amount < 0)
            return dot;

        float old_heat_capacity = Dm.num(air.heat_capacity());
        Dm.remove_at(cached_moles, typeof(datum_gas_bz), consumed_amount);
        Dm.append_at(cached_moles, typeof(datum_gas_nitrogen), consumed_amount * 0.4f);
        Dm.append_at(cached_moles, typeof(datum_gas_helium), consumed_amount * 1.6f);
        Dm.append_at(cached_moles, typeof(datum_gas_plasma), consumed_amount * 0.8f);

        air.reaction_results[type] = consumed_amount;
        turf_open location = null;
        float energy_released = consumed_amount * PN_BZASE_ENERGY;
        if (istype(holder, typeof(datum_pipeline))) //Find the tile the reaction is occurring on, or a random part of the network if it's a pipenet.
        {
            datum_pipeline pipenet = (datum_pipeline)holder;
            location = pick(pipenet.members) as turf_open;
        }
        else if (isatom(holder))
            location = holder as turf_open;
        if (location != null && energy_released > PN_BZASE_RAD_RELEASE_THRESHOLD * Dm.pow(Dm.num(air.volume) / CELL_VOLUME, ATMOS_RADIATION_VOLUME_EXP))
        {
            dot |= VOLATILE_REACTION;
            ///How many nuclear particles will fire in this reaction.
            float nuclear_particle_amount = min(round(consumed_amount / PN_BZASE_NUCLEAR_PARTICLE_DIVISOR), PN_BZASE_NUCLEAR_PARTICLE_MAXIMUM);
            // for(var/i in 1 to nuclear_particle_amount) location.fire_nuclear_particle(): the nuclear particle is a projectile
            Dm.Deferred("code/modules/atmospherics/gasmixtures/reactions.dm:1196", "react: proto-nitrate's BZ response nuclear particles (fire_nuclear_particle: projectiles, M10+)");
            radiation_pulse(location, max_range: min(sqrt(consumed_amount - nuclear_particle_amount * PN_BZASE_NUCLEAR_PARTICLE_RADIATION_ENERGY_CONVERSION) / PN_BZASE_RAD_RANGE_DIVISOR, GAS_REACTION_MAXIMUM_RADIATION_PULSE_RANGE), threshold: PN_BZASE_RAD_THRESHOLD);
            await visible_hallucination_pulse(location as atom, 1, consumed_amount * 2 * SECONDS);
        }

        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max((temperature * old_heat_capacity + energy_released) / new_heat_capacity, TCMB);
        dot |= REACTING;
        return dot;
    }
}

// tg: code/modules/atmospherics/gasmixtures/reactions.dm:1205
public partial class datum_gas_reaction_standard_antinoblium_replication : datum_gas_reaction_standard
{
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1211
    public override void init_reqs()
    {
        requirements = Dm.list(
            Dm.kv(typeof(datum_gas_antinoblium), MOLES_GAS_VISIBLE),
            Dm.kv("MIN_TEMP", REACTION_OPPRESSION_MIN_TEMP)
        );
    }

    /**
     * Anti-Noblium Replication
     *
     * Converts all gases into antinoblium.
     */
    // tg: code/modules/atmospherics/gasmixtures/reactions.dm:1222
    public override async DmTask<object> react(datum_gas_mixture air = null, object holder = null)
    {
        int dot = REACTING | VOLATILE_REACTION;
        DmList cached_moles = air.moles;
        float heat_capacity = Dm.num(air.heat_capacity());
        float total_moles = Dm.num(air.total_moles());
        float antinoblium_moles = Dm.num(cached_moles[typeof(datum_gas_antinoblium)]);
        float total_not_antinoblium_moles = total_moles - antinoblium_moles;
        float reaction_rate = min(antinoblium_moles / ANTINOBLIUM_CONVERSION_DIVISOR, total_not_antinoblium_moles);
        if (total_not_antinoblium_moles < MINIMUM_MOLE_COUNT) // Clear up the remaining gases if this condition is met.
        {
            dot = NO_REACTION;
            reaction_rate = total_not_antinoblium_moles;
        }
        foreach ((object gas_id, object value) in Dm.pairs(cached_moles))
        {
            if (Dm.eq(gas_id, typeof(datum_gas_antinoblium)))
                continue;
            if (dot == NO_REACTION) // Let the gases get properly cleared while avoiding potential division by 0.
            {
                cached_moles[gas_id] = 0;
                continue;
            }
            Dm.remove_at(cached_moles, gas_id, reaction_rate * Dm.num(value) / total_not_antinoblium_moles);
        }
        Dm.append_at(cached_moles, typeof(datum_gas_antinoblium), reaction_rate);

        air.reaction_results[type] = reaction_rate;
        float new_heat_capacity = Dm.num(air.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air.temperature = max(air.temperature * heat_capacity / new_heat_capacity, TCMB);
        return dot;
    }
}
