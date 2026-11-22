// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E12b (docs/ai/specs/engineering.md): the whole file, the fusion reaction (the instability, the moderators' energy, power, heat and
// radiation modifiers, the internal power, the core temperature, the heat output, the fuel use and the gases made at each fusion level),
// the damage and healing of the core, the nuclear particles, the lightning arcs, the gravity pulse, the waste removal, the cooling loop
// and the injection from the side ports.

/**
 * Main Fusion processes
 * process() Organizes all other calls, and is the best starting point for top-level logic.
 * fusion_process() handles all the main fusion reaction logic and consequences (lightning, radiation, particles) from an active fusion reaction.
 */
public partial class obj_machinery_atmospherics_components_unary_hypertorus_core
{
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:7
    public override object process_atmos(object seconds_per_tick = null)
    {
        return Waiting.WaitforFalse(process_atmos__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body(object seconds_per_tick)
    {
        /*
         *Pre-checks
         */
        //first check if the machine is active
        if (!Dm.truthy(active))
            return null;

        //then check if the other machines are still there
        if (!Dm.truthy(check_part_connectivity()))
        {
            deactivate();
            return null;
        }

        assert_gases();

        // Run the reaction if it is either live or being started
        if (Dm.truthy(start_power) || Dm.truthy(power_level))
        {
            play_ambience(seconds_per_tick);
            await fusion_process(seconds_per_tick);
            // Note that we process damage/healing even if the fusion process aborts.
            // Running out of fuel won't save you if your moderator and coolant are exploding on their own.
            await process_moderator_overflow(seconds_per_tick);
            process_damageheal(seconds_per_tick);
            check_alert();
        }
        if (Dm.truthy(start_power))
            await remove_waste(seconds_per_tick);
        update_pipenets();

        check_deconstructable();

        if (linked_interface != null)
        {
            // The interface MUST be updated after every atmos tick to show accurate information.
            SStgui.update_uis(linked_interface);
        }
        return null;
    }

    /**
     * Called by process()
     * Contains the main fusion calculations and checks, for more informations check the comments along the code.
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:45
    public virtual async DmTask fusion_process(object seconds_per_tick = null)
    {
        //fusion: a terrible idea that was fun but broken. Now reworked to be less broken and more interesting. Again (and again, and again). Again! Again but with machine!
        //Fusion Rework Counter: Please increment this if you make a major overhaul to this system again.
        //7 reworks

        if (Dm.truthy(check_power_use()))
        {
            if (Dm.truthy(start_cooling))
            {
                inject_from_side_components(seconds_per_tick);
                process_internal_cooling(seconds_per_tick);
            }
        }
        else
        {
            // No power forces bad settings
            magnetic_constrictor = 100;
            heating_conductor = 500;
            current_damper = 0;
            fuel_injection_rate = 20;
            moderator_injection_rate = 50;
            waste_remove = FALSE;
            iron_content += 0.02f * power_level * Dm.num(seconds_per_tick);
        }

        update_temperature_status(seconds_per_tick);

        //Store the temperature of the gases after one cicle of the fusion reaction
        float archived_heat = internal_fusion.temperature;
        //Store the volume of the fusion reaction multiplied by the force of the magnets that controls how big it will be
        float volume = Dm.num(internal_fusion.volume) * (Dm.num(magnetic_constrictor) * 0.01f);

        float energy_concentration_multiplier = 1;
        float positive_temperature_multiplier = 1;
        float negative_temperature_multiplier = 1;

        //We scale it down by volume/2 because for fusion conditions, moles roughly = 2*volume, but we want it to be based off something constant between reactions.
        float scale_factor = volume * 0.5f;

        /// Store the fuel gases and the byproduct gas quantities
        DmList fuel_list = Dm.list();
        /// Scaled down moles of gases, no less than 0
        DmList scaled_fuel_list = Dm.list();

        if (selected_fuel != null)
        {
            energy_concentration_multiplier = selected_fuel.energy_concentration_multiplier;
            positive_temperature_multiplier = selected_fuel.positive_temperature_multiplier;
            negative_temperature_multiplier = selected_fuel.negative_temperature_multiplier;

            foreach (object gas_id in Dm.each(Dm.bitor(selected_fuel.requirements, selected_fuel.primary_products)))
            {
                float amount = Dm.num(internal_fusion.moles[gas_id]);
                fuel_list[gas_id] = amount;
                scaled_fuel_list[gas_id] = max((amount - FUSION_MOLE_THRESHOLD) / scale_factor, 0);
            }
        }

        /// Store the moderators gases quantities
        DmList moderator_list = Dm.list();
        /// Scaled down moles of gases, no less than 0
        DmList scaled_moderator_list = Dm.list();
        foreach (object gas_id in Dm.each(moderator_internal.moles)) // for(var/gas_id, amount in moderator_internal.moles)
        {
            float amount = Dm.num(moderator_internal.moles[gas_id]);
            moderator_list[gas_id] = amount;
            scaled_moderator_list[gas_id] = max((amount - FUSION_MOLE_THRESHOLD) / scale_factor, 0);
        }

        /*
         *FUSION MAIN PROCESS
         */
        //This section is used for the instability calculation for the fusion reaction
        //The size of the phase space hypertorus
        float toroidal_size = (2 * PI) + TORADIANS(arctan((volume - TOROID_VOLUME_BREAKEVEN) / TOROID_VOLUME_BREAKEVEN));
        //Calculation of the gas power, only for theoretical instability calculations
        DmList cached_fusion_power = GLOB.meta_gas_info[META_GAS_FUSION_POWER] as DmList;
        float gas_power = values_dot(cached_fusion_power, internal_fusion.moles);
        gas_power += 0.75f * values_dot(cached_fusion_power, moderator_internal.moles);

        instability = MODULUS(Dm.pow(gas_power * INSTABILITY_GAS_POWER_FACTOR, 2), toroidal_size) + (Dm.num(current_damper) * 0.01f) - iron_content * 0.05f;
        //Effective reaction instability (determines if the energy is used/released)
        float internal_instability = 0;
        if (instability * 0.5f < FUSION_INSTABILITY_ENDOTHERMALITY)
            internal_instability = 1;
        else
            internal_instability = -1;

        /*
         *Modifiers
         */
        ///Those are the scaled gases that gets consumed and adjust energy
        // Gases that increase the amount of energy
        float energy_modifiers = Dm.num(scaled_moderator_list[typeof(datum_gas_nitrogen)]) * 0.35f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_carbon_dioxide)]) * 0.55f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_nitrous_oxide)]) * 0.95f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_zauker)]) * 1.55f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_antinoblium)]) * 20;
        // Gases that decrease the amount of energy
        energy_modifiers -= Dm.num(scaled_moderator_list[typeof(datum_gas_hypernoblium)]) * 10 +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_water_vapor)]) * 0.75f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_nitrium)]) * 0.15f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_healium)]) * 0.45f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_freon)]) * 1.15f;
        ///Between 0.25 and 100, this value is used to modify the behaviour of the internal energy and the core temperature based on the gases present in the mix
        float power_modifier = Dm.num(scaled_moderator_list[typeof(datum_gas_oxygen)]) * 0.55f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_carbon_dioxide)]) * 0.95f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_nitrium)]) * 1.45f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_zauker)]) * 5.55f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_plasma)]) * 0.05f -
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_nitrous_oxide)]) * 0.05f -
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_freon)]) * 0.75f;
        ///Minimum 0.25, this value is used to modify the behaviour of the energy emission based on the gases present in the mix
        float heat_modifier = Dm.num(scaled_moderator_list[typeof(datum_gas_plasma)]) * 1.25f -
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_nitrogen)]) * 0.75f -
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_nitrous_oxide)]) * 1.45f -
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_freon)]) * 0.95f;
        ///Between 0.005 and 1000, this value modify the radiation emission of the reaction, higher values increase the emission
        float radiation_modifier = Dm.num(scaled_moderator_list[typeof(datum_gas_freon)]) * 1.15f -
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_nitrogen)]) * 0.45f -
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_plasma)]) * 0.95f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_bz)]) * 1.9f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_proto_nitrate)]) * 0.1f +
                                    Dm.num(scaled_moderator_list[typeof(datum_gas_antinoblium)]) * 10;

        if (selected_fuel != null)
        {
            // These should probably be static coefficients read from a table rather than things that depend on the current recipe
            // the same is true for the effects above
            energy_modifiers += Dm.num(scaled_fuel_list[selected_fuel.requirements[1]]) +
                                    Dm.num(scaled_fuel_list[selected_fuel.requirements[2]]);
            energy_modifiers -= Dm.num(scaled_fuel_list[selected_fuel.primary_products[1]]);

            power_modifier += Dm.num(scaled_fuel_list[selected_fuel.requirements[2]]) * 1.05f -
                                    Dm.num(scaled_fuel_list[selected_fuel.primary_products[1]]) * 0.55f;

            heat_modifier += Dm.num(scaled_fuel_list[selected_fuel.requirements[1]]) * 1.15f +
                                    Dm.num(scaled_fuel_list[selected_fuel.primary_products[1]]) * 1.05f;

            radiation_modifier += Dm.num(scaled_fuel_list[selected_fuel.primary_products[1]]);
        }

        power_modifier = clamp(power_modifier, 0.25f, 100);
        heat_modifier = clamp(heat_modifier, 0.25f, 100);
        radiation_modifier = clamp(radiation_modifier, 0.005f, 1000);

        /*
         *Main calculations (energy, internal power, core temperature, delta temperature,
         *conduction, radiation, efficiency, power output, heat limiter modifier and heat output)
         */
        internal_power = 0;
        efficiency = VOID_CONDUCTION * 1;

        if (selected_fuel != null)
        {
            float fuel_1 = Dm.num(scaled_fuel_list[selected_fuel.requirements[1]]);
            float fuel_2 = Dm.num(scaled_fuel_list[selected_fuel.requirements[2]]);
            // Power of the gas mixture
            internal_power = (fuel_1 * power_modifier / 100) * (fuel_2 * power_modifier / 100) * (PI * Dm.pow(2 * (fuel_1 * CALCULATED_H2RADIUS) * (fuel_2 * CALCULATED_TRITRADIUS), 2)) * energy;

            // Efficiency of the reaction, it increases with the amount of byproduct
            efficiency = VOID_CONDUCTION * clamp(Dm.num(scaled_fuel_list[selected_fuel.primary_products[1]]), 1, 100);
        }

        //Can go either positive or negative depending on the instability and the negative energy modifiers
        //E=mc^2 with some changes for gameplay purposes
        energy = (energy_modifiers * Dm.pow(LIGHT_SPEED, 2)) * max(internal_fusion.temperature * heat_modifier / 100, 1);
        energy = energy / energy_concentration_multiplier;
        energy = clamp(energy, 0, 1e35f); //ugly way to prevent NaN error
        //Temperature inside the center of the gas mixture
        core_temperature = internal_power * power_modifier / 1000;
        core_temperature = max(TCMB, core_temperature);
        //Difference between the gases temperature and the internal temperature of the reaction
        delta_temperature = archived_heat - core_temperature;
        //Energy from the reaction lost from the molecule colliding between themselves.
        conduction = -delta_temperature * (Dm.num(magnetic_constrictor) * 0.001f);
        //The remaining wavelength that actually can do damage to mobs.
        radiation = max(-(PLANCK_LIGHT_CONSTANT / 5e-18f) * radiation_modifier * delta_temperature, 0);
        power_output = efficiency * (internal_power - conduction - radiation);
        //Hotter air is easier to heat up and cool down
        heat_limiter_modifier = 5 * Dm.pow(10, power_level) * (Dm.num(heating_conductor) / 100);
        //The amount of heat that is finally emitted, based on the power output. Min and max are variables that depends of the modifier
        heat_output_min = -heat_limiter_modifier * 0.01f * negative_temperature_multiplier;
        heat_output_max = heat_limiter_modifier * positive_temperature_multiplier;
        heat_output = clamp(internal_instability * power_output * heat_modifier / 200, heat_output_min, heat_output_max);

        // Is the fusion process actually going to run?
        // Note we have to always perform the above calculations to keep the UI updated, so we can't use this to early return.
        if (!Dm.truthy(check_fuel()))
            return;

        // Phew. Lets calculate what this means in practice.
        float fuel_consumption_rate = clamp(Dm.num(fuel_injection_rate) * 0.01f * 5 * power_level, 0.05f, 30);
        float consumption_amount = fuel_consumption_rate * Dm.num(seconds_per_tick);
        float production_amount;
        switch (power_level)
        {
            case 3:
            case 4:
                production_amount = clamp(heat_output / 1000, 0, fuel_consumption_rate) * Dm.num(seconds_per_tick);
                break;
            default:
                production_amount = clamp(heat_output * 2 / Dm.pow(10, power_level + 1), 0, fuel_consumption_rate) * Dm.num(seconds_per_tick);
                break;
        }

        // antinob production is special, and uses its own calculations from how stale the fusion mix is (via byproduct ratio and fresh fuel rate)
        float dirty_production_rate = Dm.num(scaled_fuel_list[scaled_fuel_list[3]]) / Dm.num(fuel_injection_rate);

        // Run the effects of our selected fuel recipe

        datum_gas_mixture internal_output = Dm.@new<datum_gas_mixture>();
        moderator_fuel_process(seconds_per_tick, production_amount, consumption_amount, internal_output, moderator_list, selected_fuel, fuel_list);

        // Run the common effects, committing changes where applicable

        // This is repetition, but is here as a placeholder for what will need to be done to allow concurrently running multiple recipes
        float common_production_amount = production_amount * selected_fuel.gas_production_multiplier;
        await moderator_common_process(seconds_per_tick, common_production_amount, internal_output, moderator_list, dirty_production_rate, heat_output, radiation_modifier);
    }

    /**
     * Perform recipe specific actions. Fuel consumption and recipe based gas production happens here.
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:244
    public virtual void moderator_fuel_process(object seconds_per_tick = null, object production_amount = null, object consumption_amount = null, datum_gas_mixture internal_output = null, object moderator_list = null, datum_hfr_fuel fuel = null, object fuel_list = null)
    {
        DmList moderators = moderator_list as DmList;
        DmList fuels = fuel_list as DmList;
        // Adjust fusion consumption/production based on this recipe's characteristics
        float fuel_consumption = Dm.num(consumption_amount) * 0.85f * selected_fuel.fuel_consumption_multiplier;
        float scaled_production = Dm.num(production_amount) * selected_fuel.gas_production_multiplier;

        foreach (object gas_id in Dm.each(fuel.requirements))
            internal_fusion.adjust_gas(gas_id, -min(Dm.num(fuels[gas_id]), fuel_consumption));
        foreach (object gas_id in Dm.each(fuel.primary_products))
            internal_fusion.adjust_gas(gas_id, fuel_consumption * 0.5f);

        // Each recipe provides a tier list of six output gases.
        // Which gases are produced depend on what the fusion level is.
        DmList tier = fuel.secondary_products;
        switch (power_level)
        {
            case 1:
                moderator_internal.adjust_gas(tier[1], scaled_production * 0.95f);
                moderator_internal.adjust_gas(tier[2], scaled_production * 0.75f);
                break;
            case 2:
                moderator_internal.adjust_gas(tier[1], scaled_production * 1.65f);
                moderator_internal.adjust_gas(tier[2], scaled_production);
                if (Dm.num(moderators[typeof(datum_gas_plasma)]) > 50)
                    moderator_internal.adjust_gas(tier[3], scaled_production * 1.15f);
                break;
            case 3:
                moderator_internal.adjust_gas(tier[2], scaled_production * 0.5f);
                moderator_internal.adjust_gas(tier[3], scaled_production * 0.45f);
                break;
            case 4:
                moderator_internal.adjust_gas(tier[3], scaled_production * 1.65f);
                moderator_internal.adjust_gas(tier[4], scaled_production * 1.25f);
                if (Dm.num(moderators[typeof(datum_gas_plasma)]) > 50)
                    moderator_internal.adjust_gas(tier[5], scaled_production * 1.15f);
                break;
            case 5:
                moderator_internal.adjust_gas(tier[4], scaled_production * 0.65f);
                moderator_internal.adjust_gas(tier[5], scaled_production);
                moderator_internal.adjust_gas(tier[6], scaled_production * 0.75f);
                break;
            case 6:
                moderator_internal.adjust_gas(tier[5], scaled_production * 0.35f);
                moderator_internal.adjust_gas(tier[6], scaled_production);
                break;
        }
    }

    /**
     * Perform common fusion actions:
     *
     * - Gases that get produced irrespective of recipe
     * - Temperature modifiers, radiation modifiers, and the application of each
     * - Committing staged output, performing filtering, and making !FUN! emissions
     */
    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:289
    // heat_output is the parameter, as tg's: the proc's own changes to it (the proto-nitrate and freon factors) shadow the core's var.
    public virtual async DmTask moderator_common_process(object seconds_per_tick = null, object scaled_production = null, datum_gas_mixture internal_output = null, object moderator_list = null, object dirty_production_rate = null, object heat_output = null, object radiation_modifier = null)
    {
        DmList moderators = moderator_list as DmList;
        float production = Dm.num(scaled_production);
        float dirty_rate = Dm.num(dirty_production_rate);
        float spt = Dm.num(seconds_per_tick);
        switch (power_level)
        {
            case 1:
                if (Dm.num(moderators[typeof(datum_gas_plasma)]) > 100)
                {
                    internal_output.adjust_gas(typeof(datum_gas_nitrous_oxide), production * 0.5f);
                    moderator_internal.adjust_gas(typeof(datum_gas_plasma), min(Dm.num(moderator_internal.moles[typeof(datum_gas_plasma)]), production * 0.85f));
                }
                if (Dm.num(moderators[typeof(datum_gas_bz)]) > 150)
                {
                    internal_output.adjust_gas(typeof(datum_gas_halon), production * 0.55f);
                    moderator_internal.adjust_gas(typeof(datum_gas_bz), min(Dm.num(moderator_internal.moles[typeof(datum_gas_bz)]), production * 0.95f));
                }
                break;
            case 2:
                if (Dm.num(moderators[typeof(datum_gas_plasma)]) > 50)
                {
                    internal_output.adjust_gas(typeof(datum_gas_bz), production * 1.8f);
                    moderator_internal.adjust_gas(typeof(datum_gas_plasma), min(Dm.num(moderator_internal.moles[typeof(datum_gas_plasma)]), production * 1.75f));
                }
                if (Dm.num(moderators[typeof(datum_gas_proto_nitrate)]) > 20)
                {
                    radiation *= 1.55f;
                    heat_output = Dm.num(heat_output) * 1.025f;
                    internal_output.adjust_gas(typeof(datum_gas_nitrium), production * 1.05f);
                    moderator_internal.adjust_gas(typeof(datum_gas_proto_nitrate), min(Dm.num(moderator_internal.moles[typeof(datum_gas_proto_nitrate)]), production * 1.35f));
                }
                break;
            case 3:
            case 4:
                if (Dm.num(moderators[typeof(datum_gas_plasma)]) > 10)
                {
                    DmList new_gases = Dm.list(Dm.kv(typeof(datum_gas_freon), production * 0.15f), Dm.kv(typeof(datum_gas_nitrium), production * 1.05f));
                    internal_output.adjust_multiple_gases(new_gases);
                    moderator_internal.adjust_gas(typeof(datum_gas_plasma), min(Dm.num(moderator_internal.moles[typeof(datum_gas_plasma)]), production * 0.45f));
                }
                if (Dm.num(moderators[typeof(datum_gas_freon)]) > 50)
                {
                    heat_output = Dm.num(heat_output) * 0.9f;
                    radiation *= 0.8f;
                }
                if (Dm.num(moderators[typeof(datum_gas_proto_nitrate)]) > 15)
                {
                    DmList new_gases = Dm.list(Dm.kv(typeof(datum_gas_nitrium), production * 1.25f), Dm.kv(typeof(datum_gas_halon), production * 1.15f));
                    internal_output.adjust_multiple_gases(new_gases);
                    moderator_internal.adjust_gas(typeof(datum_gas_proto_nitrate), min(Dm.num(moderator_internal.moles[typeof(datum_gas_proto_nitrate)]), production * 1.55f));
                    radiation *= 1.95f;
                    heat_output = Dm.num(heat_output) * 1.25f;
                }
                if (Dm.num(moderators[typeof(datum_gas_bz)]) > 100)
                {
                    DmList new_gases = Dm.list(Dm.kv(typeof(datum_gas_healium), production * 1.5f), Dm.kv(typeof(datum_gas_proto_nitrate), production * 1.5f));
                    internal_output.adjust_multiple_gases(new_gases);
                    await visible_hallucination_pulse(this, HALLUCINATION_HFR(Dm.num(heat_output)), 100 * SECONDS * power_level * spt);
                }
                break;

            case 5:
                if (Dm.num(moderators[typeof(datum_gas_plasma)]) > 15)
                {
                    internal_output.adjust_gas(typeof(datum_gas_freon), production * 0.25f);
                    moderator_internal.adjust_gas(typeof(datum_gas_plasma), min(Dm.num(moderator_internal.moles[typeof(datum_gas_plasma)]), production * 1.45f));
                }
                if (Dm.num(moderators[typeof(datum_gas_freon)]) > 500)
                {
                    heat_output = Dm.num(heat_output) * 0.5f;
                    radiation *= 0.2f;
                }
                if (Dm.num(moderators[typeof(datum_gas_proto_nitrate)]) > 50)
                {
                    DmList new_gases = Dm.list(Dm.kv(typeof(datum_gas_nitrium), production * 1.95f), Dm.kv(typeof(datum_gas_pluoxium), production));
                    internal_output.adjust_multiple_gases(new_gases);
                    moderator_internal.adjust_gas(typeof(datum_gas_proto_nitrate), min(Dm.num(moderator_internal.moles[typeof(datum_gas_proto_nitrate)]), production * 1.35f));
                    radiation *= 1.95f;
                    heat_output = Dm.num(heat_output) * 1.25f;
                }
                if (Dm.num(moderators[typeof(datum_gas_bz)]) > 100)
                {
                    DmList new_gases = Dm.list(Dm.kv(typeof(datum_gas_healium), production), Dm.kv(typeof(datum_gas_freon), production * 1.15f));
                    internal_output.adjust_multiple_gases(new_gases);
                    await visible_hallucination_pulse(this, HALLUCINATION_HFR(Dm.num(heat_output)), 100 * SECONDS * power_level * spt);
                }
                if (Dm.num(moderators[typeof(datum_gas_healium)]) > 100)
                {
                    if (critical_threshold_proximity > 400)
                    {
                        critical_threshold_proximity = max(critical_threshold_proximity - (Dm.num(moderators[typeof(datum_gas_healium)]) / 100 * spt), 0);
                        moderator_internal.adjust_gas(typeof(datum_gas_healium), -min(Dm.num(moderator_internal.moles[typeof(datum_gas_healium)]), production * 20));
                    }
                }
                if (moderator_internal.temperature < 1e7f || (Dm.num(moderators[typeof(datum_gas_plasma)]) > 100 && Dm.num(moderators[typeof(datum_gas_bz)]) > 50))
                    internal_output.adjust_gas(typeof(datum_gas_antinoblium), dirty_rate * 0.9f / 0.065f * spt);
                break;
            case 6:
                internal_output.assert_gases(typeof(datum_gas_antinoblium));
                if (Dm.num(moderators[typeof(datum_gas_plasma)]) > 30)
                {
                    internal_output.adjust_gas(typeof(datum_gas_bz), production * 1.15f);
                    moderator_internal.adjust_gas(typeof(datum_gas_plasma), -min(Dm.num(moderator_internal.moles[typeof(datum_gas_plasma)]), production * 1.45f));
                }
                if (Dm.truthy(moderators[typeof(datum_gas_proto_nitrate)]))
                {
                    DmList new_gases = Dm.list(Dm.kv(typeof(datum_gas_zauker), production * 5.35f), Dm.kv(typeof(datum_gas_nitrium), production * 2.15f));
                    internal_output.adjust_multiple_gases(new_gases);
                    moderator_internal.adjust_gas(typeof(datum_gas_proto_nitrate), -min(Dm.num(moderator_internal.moles[typeof(datum_gas_proto_nitrate)]), production * 3.35f));
                    radiation *= 2;
                    heat_output = Dm.num(heat_output) * 2.25f;
                }
                if (Dm.truthy(moderators[typeof(datum_gas_bz)]))
                {
                    await visible_hallucination_pulse(this, HALLUCINATION_HFR(Dm.num(heat_output)), 100 * SECONDS * power_level * spt);
                    internal_output.adjust_gas(typeof(datum_gas_antinoblium), clamp(dirty_rate / 0.045f, 0, 10) * spt);
                }
                if (Dm.num(moderators[typeof(datum_gas_healium)]) > 100)
                {
                    if (critical_threshold_proximity > 400)
                    {
                        critical_threshold_proximity = max(critical_threshold_proximity - (Dm.num(moderators[typeof(datum_gas_healium)]) / 100 * spt), 0);
                        moderator_internal.adjust_gas(typeof(datum_gas_healium), -min(Dm.num(moderator_internal.moles[typeof(datum_gas_healium)]), production * 20));
                    }
                }
                internal_fusion.adjust_gas(typeof(datum_gas_antinoblium), dirty_rate * 0.01f / 0.095f * spt);
                break;
        }

        //Modifies the internal_fusion temperature with the amount of heat output
        float temperature_modifier = selected_fuel.temperature_change_multiplier;
        if (internal_fusion.temperature <= FUSION_MAXIMUM_TEMPERATURE * temperature_modifier)
        {
            internal_fusion.temperature = clamp(
                internal_fusion.temperature + Dm.num(heat_output) * spt,
                TCMB,
                FUSION_MAXIMUM_TEMPERATURE * temperature_modifier
            );
        }
        else
            internal_fusion.temperature -= heat_limiter_modifier * 0.01f * spt;

        //heat up and output what's in the internal_output into the linked_output port
        if (Dm.num(internal_output.total_moles()) > 0)
        {
            if (Dm.num(moderator_internal.total_moles()) > 0)
                internal_output.temperature = moderator_internal.temperature * HIGH_EFFICIENCY_CONDUCTIVITY;
            else
                internal_output.temperature = internal_fusion.temperature * METALLIC_VOID_CONDUCTIVITY;
            ((datum_gas_mixture)linked_output.airs[1]).merge(internal_output);
        }

        evaporate_moderator(seconds_per_tick);

        await check_nuclear_particles(moderator_list);

        await check_lightning_arcs(moderator_list);

        // Oxygen burns away iron content rapidly
        if (Dm.num(moderators[typeof(datum_gas_oxygen)]) > 150)
        {
            if (iron_content > 0)
            {
                float max_iron_removable = IRON_OXYGEN_HEAL_PER_SECOND;
                float iron_removed = min(max_iron_removable * spt, iron_content);
                iron_content -= iron_removed;
                moderator_internal.adjust_gas(typeof(datum_gas_oxygen), -iron_removed * OXYGEN_MOLES_CONSUMED_PER_IRON_HEAL);
            }
        }

        await check_gravity_pulse(seconds_per_tick);

        radiation_pulse(this, max_range: 6, threshold: 0.3f);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:406
    public virtual void evaporate_moderator(object seconds_per_tick = null)
    {
        // Don't evaporate if the reaction is dead
        if (!Dm.truthy(power_level))
            return;
        // All gases in the moderator slowly burn away over time, whether used for production or not
        if (Dm.num(moderator_internal.total_moles()) > 0)
            moderator_internal.remove(Dm.num(moderator_internal.total_moles()) * (1 - Dm.pow(1 - 0.0005f * power_level, Dm.num(seconds_per_tick))));
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:414
    public virtual void process_damageheal(object seconds_per_tick = null)
    {
        float spt = Dm.num(seconds_per_tick);
        // Archive current health for damage cap purposes
        critical_threshold_proximity_archived = critical_threshold_proximity;

        //reset damage check flags
        warning_damage_flags = Dm.bitand(warning_damage_flags, HYPERTORUS_FLAG_EMPED);

        // If we're operating at an extreme power level, take increasing damage for the amount of fusion mass over a low threshold
        if (power_level >= HYPERTORUS_OVERFULL_MIN_POWER_LEVEL)
        {
            float overfull_damage_taken = HYPERTORUS_OVERFULL_MOLAR_SLOPE * Dm.num(internal_fusion.total_moles()) + HYPERTORUS_OVERFULL_TEMPERATURE_SLOPE * coolant_temperature + HYPERTORUS_OVERFULL_CONSTANT;
            critical_threshold_proximity = max(critical_threshold_proximity + max(overfull_damage_taken * spt, 0), 0);
            warning_damage_flags = Dm.bitor(warning_damage_flags, HYPERTORUS_FLAG_HIGH_POWER_DAMAGE);
        }

        // If we're running on a thin fusion mix, heal up
        if (Dm.num(internal_fusion.total_moles()) < HYPERTORUS_SUBCRITICAL_MOLES && power_level <= 5)
        {
            float subcritical_heal_restore = (Dm.num(internal_fusion.total_moles()) - HYPERTORUS_SUBCRITICAL_MOLES) / HYPERTORUS_SUBCRITICAL_SCALE;
            critical_threshold_proximity = max(critical_threshold_proximity + min(subcritical_heal_restore * spt, 0), 0);
        }

        // If coolant is sufficiently cold, heal up
        if (Dm.num(internal_fusion.total_moles()) > 0 && (Dm.truthy(((datum_gas_mixture)airs[1]).total_moles()) && coolant_temperature < HYPERTORUS_COLD_COOLANT_THRESHOLD) && power_level <= 4)
        {
            float cold_coolant_heal_restore = Dm.log(10, max(coolant_temperature, 1) * HYPERTORUS_COLD_COOLANT_SCALE) - (HYPERTORUS_COLD_COOLANT_MAX_RESTORE * 2);
            critical_threshold_proximity = max(critical_threshold_proximity + min(cold_coolant_heal_restore * spt, 0), 0);
        }

        critical_threshold_proximity += max(iron_content - HYPERTORUS_MAX_SAFE_IRON, 0) * spt;
        if (iron_content - HYPERTORUS_MAX_SAFE_IRON > 0)
            warning_damage_flags = Dm.bitor(warning_damage_flags, HYPERTORUS_FLAG_IRON_CONTENT_DAMAGE);

        // Apply damage cap
        critical_threshold_proximity = min(critical_threshold_proximity_archived + (spt * DAMAGE_CAP_MULTIPLIER * melting_point), critical_threshold_proximity);

        // If we have a preposterous amount of mass in the fusion mix, things get bad extremely fast
        if (Dm.num(internal_fusion.total_moles()) >= HYPERTORUS_HYPERCRITICAL_MOLES)
        {
            float moles_over_limit = Dm.num(internal_fusion.total_moles()) - HYPERTORUS_HYPERCRITICAL_MOLES;
            float hypercritical_damage_taken = max(moles_over_limit * HYPERTORUS_HYPERCRITICAL_SCALE, 0);
            hypercritical_damage_taken = min(hypercritical_damage_taken, HYPERTORUS_HYPERCRITICAL_MAX_DAMAGE) * spt;
            critical_threshold_proximity = max(critical_threshold_proximity + hypercritical_damage_taken, 0);
            warning_damage_flags = Dm.bitor(warning_damage_flags, HYPERTORUS_FLAG_HIGH_FUEL_MIX_MOLE);
        }

        // High power fusion might create other matter other than helium, iron is dangerous inside the machine, damage can be seen
        if (power_level > 4 && prob(IRON_CHANCE_PER_FUSION_LEVEL * power_level)) //at power level 6 is 100%
        {
            iron_content += IRON_ACCUMULATED_PER_SECOND * spt;
            warning_damage_flags = Dm.bitor(warning_damage_flags, HYPERTORUS_FLAG_IRON_CONTENT_INCREASE);
        }
        if (iron_content > 0 && power_level <= 4 && prob(25 / (power_level + 1)))
            iron_content = max(iron_content - 0.01f * spt, 0);
        iron_content = clamp(iron_content, 0, 1);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:460
    public virtual async DmTask check_nuclear_particles(object moderator_list = null)
    {
        // New nuclear particle emission sytem.
        if (power_level < 4)
            return;
        if (Dm.num(((DmList)moderator_list)[typeof(datum_gas_bz)]) < (150 / power_level))
            return;
        obj_machinery_hypertorus_corner picked_corner = pick(corners) as obj_machinery_hypertorus_corner;
        await picked_corner.loc.fire_nuclear_particle(REVERSE_DIR(picked_corner.dir));
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:469
    public virtual async DmTask check_lightning_arcs(object moderator_list = null)
    {
        if (power_level < 4)
            return;
        if (Dm.num(((DmList)moderator_list)[typeof(datum_gas_antinoblium)]) <= 50 && critical_threshold_proximity <= 500)
            return;
        float zap_number = power_level - 2;

        if (critical_threshold_proximity > 650 && prob(20))
            zap_number += 1;

        float cutoff = 1.2e6f;
        cutoff = clamp(2.4e6f - (power_level * (Dm.num(internal_fusion.total_moles()) * 360)), 3.6e5f, 2.4e6f);

        string zaps_aspect = DEFAULT_ZAP_ICON_STATE;
        float flags = ZAP_SUPERMATTER_FLAGS;
        switch (power_level)
        {
            case 5:
                zaps_aspect = SLIGHTLY_CHARGED_ZAP_ICON_STATE;
                Dm.combine(ref flags, ZAP_MOB_DAMAGE);
                break;
            case 6:
                zaps_aspect = OVER_9000_ZAP_ICON_STATE;
                Dm.combine(ref flags, Dm.bitor(ZAP_MOB_DAMAGE, ZAP_OBJ_DAMAGE));
                break;
        }

        playsound(loc, Dm.resource("sound/items/weapons/emitter2.ogg"), 100, TRUE, extrarange: 10);
        for (float i = 1, i__end = zap_number; i <= i__end; i++)
            await supermatter_zap(this, 5, power_level * 2.4e5f, flags, zap_cutoff: cutoff, power_level: this.power_level * 1000, zap_icon: zaps_aspect);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:496
    public virtual async DmTask check_gravity_pulse(object seconds_per_tick = null)
    {
        if (SPT_PROB(100 - critical_threshold_proximity / 15, seconds_per_tick))
            return;
        float grav_range = round(Dm.log(2.5f, critical_threshold_proximity));
        foreach (mob alive_mob in Dm.each<mob>(GLOB.alive_mob_list))
        {
            if (alive_mob.z != z || get_dist(alive_mob, this) > grav_range || Dm.truthy(alive_mob.mob_negates_gravity()))
                continue;
            await LocomotionSeam.PullOrStep(alive_mob, loc); // DEVIATION(DV357)
        }
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:505
    public virtual async DmTask remove_waste(object seconds_per_tick = null)
    {
        //Gases can be removed from the moderator internal by using the interface.
        if (!Dm.truthy(waste_remove))
            return;
        float filtering_amount = moderator_scrubbing.len;
        foreach (object gas in Dm.each(Dm.bitand(moderator_internal.moles, moderator_scrubbing)))
        {
            datum_gas_mixture removed = moderator_internal.remove_specific(gas, (moderator_filtering_rate / filtering_amount) * Dm.num(seconds_per_tick)) as datum_gas_mixture;
            if (removed != null)
                ((datum_gas_mixture)linked_output.airs[1]).merge(removed);
        }

        if (selected_fuel != null)
        {
            datum_gas_mixture internal_remove = null;
            foreach (object gas_id in Dm.each(selected_fuel.primary_products))
            {
                if (Dm.num(internal_fusion.moles[gas_id]) > 0)
                {
                    internal_remove = internal_fusion.remove_specific(gas_id, Dm.num(internal_fusion.moles[gas_id]) * (1 - Dm.pow(1 - 0.25f, Dm.num(seconds_per_tick)))) as datum_gas_mixture;
                    ((datum_gas_mixture)linked_output.airs[1]).merge(internal_remove);
                }
            }
        }
        internal_fusion.garbage_collect();
        moderator_internal.garbage_collect();
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:524
    public virtual void process_internal_cooling(object seconds_per_tick = null)
    {
        float spt = Dm.num(seconds_per_tick);
        if (Dm.num(moderator_internal.total_moles()) > 0 && Dm.num(internal_fusion.total_moles()) > 0)
        {
            //Modifies the moderator_internal temperature based on energy conduction and also the fusion by the same amount
            float fusion_temperature_delta = internal_fusion.temperature - moderator_internal.temperature;
            float fusion_heat_amount = (1 - Dm.pow(1 - METALLIC_VOID_CONDUCTIVITY, spt)) * fusion_temperature_delta * (Dm.num(internal_fusion.heat_capacity()) * Dm.num(moderator_internal.heat_capacity()) / (Dm.num(internal_fusion.heat_capacity()) + Dm.num(moderator_internal.heat_capacity())));
            internal_fusion.temperature = max(internal_fusion.temperature - fusion_heat_amount / Dm.num(internal_fusion.heat_capacity()), TCMB);
            moderator_internal.temperature = max(moderator_internal.temperature + fusion_heat_amount / Dm.num(moderator_internal.heat_capacity()), TCMB);
        }

        if (Dm.num(((datum_gas_mixture)airs[1]).total_moles()) * 0.05f <= MINIMUM_MOLE_COUNT)
            return;
        datum_gas_mixture cooling_port = airs[1] as datum_gas_mixture;
        datum_gas_mixture cooling_remove = cooling_port.remove(0.05f * Dm.num(cooling_port.total_moles())) as datum_gas_mixture;
        //Cooling of the moderator gases with the cooling loop in and out the core
        if (Dm.num(moderator_internal.total_moles()) > 0)
        {
            float coolant_temperature_delta = cooling_remove.temperature - moderator_internal.temperature;
            float cooling_heat_amount = (1 - Dm.pow(1 - HIGH_EFFICIENCY_CONDUCTIVITY, spt)) * coolant_temperature_delta * (Dm.num(cooling_remove.heat_capacity()) * Dm.num(moderator_internal.heat_capacity()) / (Dm.num(cooling_remove.heat_capacity()) + Dm.num(moderator_internal.heat_capacity())));
            cooling_remove.temperature = max(cooling_remove.temperature - cooling_heat_amount / Dm.num(cooling_remove.heat_capacity()), TCMB);
            moderator_internal.temperature = max(moderator_internal.temperature + cooling_heat_amount / Dm.num(moderator_internal.heat_capacity()), TCMB);
        }

        else if (Dm.num(internal_fusion.total_moles()) > 0)
        {
            float coolant_temperature_delta = cooling_remove.temperature - internal_fusion.temperature;
            float cooling_heat_amount = (1 - Dm.pow(1 - METALLIC_VOID_CONDUCTIVITY, spt)) * coolant_temperature_delta * (Dm.num(cooling_remove.heat_capacity()) * Dm.num(internal_fusion.heat_capacity()) / (Dm.num(cooling_remove.heat_capacity()) + Dm.num(internal_fusion.heat_capacity())));
            cooling_remove.temperature = max(cooling_remove.temperature - cooling_heat_amount / Dm.num(cooling_remove.heat_capacity()), TCMB);
            internal_fusion.temperature = max(internal_fusion.temperature + cooling_heat_amount / Dm.num(internal_fusion.heat_capacity()), TCMB);
        }
        cooling_port.merge(cooling_remove);
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:550
    public virtual void inject_from_side_components(object seconds_per_tick = null)
    {
        update_pipenets();

        //Check and stores the gases from the moderator input in the moderator internal gasmix
        datum_gas_mixture moderator_port = linked_moderator.airs[1] as datum_gas_mixture;
        if (Dm.truthy(start_moderator) && Dm.truthy(moderator_port.total_moles()))
        {
            moderator_internal.merge(moderator_port.remove(Dm.num(moderator_injection_rate) * Dm.num(seconds_per_tick)) as datum_gas_mixture);
            linked_moderator.update_parents();
        }

        //Check if the fuels are present and move them inside the fuel internal gasmix
        if (!Dm.truthy(start_fuel) || selected_fuel == null || !Dm.truthy(check_gas_requirements()))
            return;

        datum_gas_mixture fuel_port = linked_input.airs[1] as datum_gas_mixture;
        foreach (object gas_type in Dm.each(selected_fuel.requirements))
        {
            internal_fusion.assert_gas(gas_type);
            internal_fusion.merge(fuel_port.remove_specific(gas_type, Dm.num(fuel_injection_rate) * Dm.num(seconds_per_tick) / length(selected_fuel.requirements)) as datum_gas_mixture);
            linked_input.update_parents();
        }
    }

    // tg: code/modules/atmospherics/machinery/components/fusion/hfr_main_processes.dm:569
    public virtual void check_deconstructable()
    {
        if (!Dm.truthy(active))
            return;
        if (power_level > 0)
        {
            fusion_started = TRUE;
            linked_input.fusion_started = TRUE;
            linked_output.fusion_started = TRUE;
            linked_moderator.fusion_started = TRUE;
            linked_interface.fusion_started = TRUE;
            foreach (obj_machinery_hypertorus_corner corner in Dm.each<obj_machinery_hypertorus_corner>(corners))
                corner.fusion_started = TRUE;
        }
        else
        {
            fusion_started = FALSE;
            linked_input.fusion_started = FALSE;
            linked_output.fusion_started = FALSE;
            linked_moderator.fusion_started = FALSE;
            linked_interface.fusion_started = FALSE;
            foreach (obj_machinery_hypertorus_corner corner in Dm.each<obj_machinery_hypertorus_corner>(corners))
                corner.fusion_started = FALSE;
        }
    }
}
