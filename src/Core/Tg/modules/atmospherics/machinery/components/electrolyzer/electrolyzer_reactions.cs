// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 zaps slice: the whole file (a tesla zap electrolyses the air of the tile it strikes, energy_ball.dm:357, through
// gas_mixture.electrolyze). The var blocks are generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:1  GLOBAL_LIST_INIT(electrolyzer_reactions, electrolyzer_reactions_list())
    public DmList electrolyzer_reactions;

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:1  (GLOBAL_MANAGED(electrolyzer_reactions, electrolyzer_reactions_list()), __DEFINES/_globals.dm)
    public virtual void InitGlobalelectrolyzer_reactions()
    {
        electrolyzer_reactions = electrolyzer_reactions_list() as DmList;
        Dm.append(ref gvars_datum_init_order, "electrolyzer_reactions");
    }
}

public static partial class GlobalProcs
{
    /*
     * Global proc to build the electrolyzer reactions list
     */
    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:6
    public static object electrolyzer_reactions_list()
    {
        DmList built_reaction_list = Dm.list();
        foreach (Type reaction_path in Dm.each_cast<Type>(subtypesof(typeof(datum_gas_reaction_electrolyzer))))
        {
            datum_gas_reaction_electrolyzer reaction = Dm.@new(reaction_path) as datum_gas_reaction_electrolyzer;

            built_reaction_list[reaction.id] = reaction;
        }

        return built_reaction_list;
    }
}

// tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:16
public partial class datum_gas_reaction_electrolyzer : datum_gas_reaction
{
    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:19
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.truthy(factor))
            factor = Dm.list();
        if (!Dm.truthy(factor["Location"]))
            factor["Location"] = "Can only happen on tiles with an active Electrolyzer.";
    }

    /**
     * Electrolyzer reaction.
     * Args:
     * * air_mixture: The gas_mixture receiving the electrolysis.
     * * working_power: How much energy to put into the electrolysis, in electrolyzer units. A value of 1 is what a tier 1 electrolyzer would put in.
     * * electrolyzer_args: Additional arguments for alternative methods of electrolysis.
     */
    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:31
    public virtual void react(datum_gas_mixture air_mixture = null, object working_power = null, DmList electrolyzer_args = null)
    {
        electrolyzer_args ??= Dm.list();
        return;
    }

    /**
     * Checks whether the requirements are met for a reaction.
     * Args:
     * * air_mixture: The air mixture to check the requirements for.
     * * electrolyzer_args: Additional arguments for alternative methods of electrolysis.
     */
    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:40
    public virtual object reaction_check(datum_gas_mixture air_mixture = null, DmList electrolyzer_args = null)
    {
        electrolyzer_args ??= Dm.list();
        float temp = air_mixture.temperature;
        DmList cached_moles = air_mixture.moles;
        if ((Dm.truthy(requirements["MIN_TEMP"]) && temp < Dm.num(requirements["MIN_TEMP"])) || (Dm.truthy(requirements["MAX_TEMP"]) && temp > Dm.num(requirements["MAX_TEMP"])))
            return FALSE;
        foreach (object id in Dm.each(requirements))
        {
            if (Dm.eq(id, "MIN_TEMP") || Dm.eq(id, "MAX_TEMP"))
                continue;
            if (Dm.lt(cached_moles[id], requirements[id]))
                return FALSE;
        }
        return TRUE;
    }
}

// tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:52
public partial class datum_gas_reaction_electrolyzer_h2o_conversion : datum_gas_reaction_electrolyzer
{
    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:65
    public override void react(datum_gas_mixture air_mixture = null, object working_power = null, DmList electrolyzer_args = null)
    {
        electrolyzer_args ??= Dm.list();

        float old_heat_capacity = Dm.num(air_mixture.heat_capacity());

        float proportion = min(Dm.num(air_mixture.moles[typeof(datum_gas_water_vapor)]) * INVERSE(2), (2.5f * (Dm.pow(Dm.num(working_power), 2))));
        air_mixture.adjust_gas(typeof(datum_gas_water_vapor), -proportion * 2);
        air_mixture.adjust_gas(typeof(datum_gas_oxygen), proportion);
        air_mixture.adjust_gas(typeof(datum_gas_hydrogen), proportion * 2);
        float new_heat_capacity = Dm.num(air_mixture.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air_mixture.temperature = max(air_mixture.temperature * old_heat_capacity / new_heat_capacity, TCMB);
    }
}

// tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:77
public partial class datum_gas_reaction_electrolyzer_nob_conversion : datum_gas_reaction_electrolyzer
{
    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:90
    public override object reaction_check(datum_gas_mixture air_mixture = null, DmList electrolyzer_args = null)
    {
        electrolyzer_args ??= Dm.list();
        if (!Dm.truthy(electrolyzer_args[ELECTROLYSIS_ARGUMENT_SUPERMATTER_POWER]) || Dm.num(electrolyzer_args[ELECTROLYSIS_ARGUMENT_SUPERMATTER_POWER]) <= POWER_PENALTY_THRESHOLD)
            return FALSE;
        return base.reaction_check(air_mixture, electrolyzer_args);
    }

    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:95
    public override void react(datum_gas_mixture air_mixture = null, object working_power = null, DmList electrolyzer_args = null)
    {
        electrolyzer_args ??= Dm.list();
        /// The supermatter zap power_level.
        float supermatter_power = Dm.num(electrolyzer_args[ELECTROLYSIS_ARGUMENT_SUPERMATTER_POWER]);
        DmList cached_moles = air_mixture.moles;
        float old_heat_capacity = Dm.num(air_mixture.heat_capacity());
        air_mixture.assert_gases(typeof(datum_gas_hypernoblium), typeof(datum_gas_antinoblium));
        float electrolysed = Dm.num(cached_moles[typeof(datum_gas_hypernoblium)]) * clamp(supermatter_power - POWER_PENALTY_THRESHOLD, 0, CRITICAL_POWER_PENALTY_THRESHOLD - POWER_PENALTY_THRESHOLD) / (CRITICAL_POWER_PENALTY_THRESHOLD - POWER_PENALTY_THRESHOLD);

        air_mixture.adjust_gas(typeof(datum_gas_hypernoblium), -electrolysed);
        air_mixture.adjust_gas(typeof(datum_gas_antinoblium), electrolysed);

        DmList cached_specific_heat = datum_gas_mixture.gas_meta[META_GAS_SPECIFIC_HEAT] as DmList; // GAS_META: /datum/gas_mixture::gas_meta (atmos_core.dm:181)
        float new_heat_capacity = old_heat_capacity + electrolysed * (Dm.num(cached_specific_heat[typeof(datum_gas_antinoblium)]) - Dm.num(cached_specific_heat[typeof(datum_gas_hypernoblium)]));
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air_mixture.temperature = max(air_mixture.temperature * old_heat_capacity / new_heat_capacity, TCMB);
    }
}

// tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:111
public partial class datum_gas_reaction_electrolyzer_halon_generation : datum_gas_reaction_electrolyzer
{
    // tg: code/modules/atmospherics/machinery/components/electrolyzer/electrolyzer_reactions.dm:125
    public override void react(datum_gas_mixture air_mixture = null, object working_power = null, DmList electrolyzer_args = null)
    {
        electrolyzer_args ??= Dm.list();
        float old_heat_capacity = Dm.num(air_mixture.heat_capacity());
        air_mixture.assert_gases(typeof(datum_gas_bz), typeof(datum_gas_oxygen), typeof(datum_gas_halon));
        float bz_moles = Dm.num(air_mixture.moles[typeof(datum_gas_bz)]);
        float reaction_efficency = min(bz_moles * (1 - Dm.pow(NUM_E, (-0.5f * air_mixture.temperature * Dm.num(working_power) / FIRE_MINIMUM_TEMPERATURE_TO_EXIST))), bz_moles);

        air_mixture.adjust_gas(typeof(datum_gas_bz), -reaction_efficency);
        air_mixture.adjust_gas(typeof(datum_gas_oxygen), reaction_efficency * 0.2f);
        air_mixture.adjust_gas(typeof(datum_gas_halon), reaction_efficency * 2);

        float energy_used = reaction_efficency * HALON_FORMATION_ENERGY;
        float new_heat_capacity = Dm.num(air_mixture.heat_capacity());
        if (new_heat_capacity > MINIMUM_HEAT_CAPACITY)
            air_mixture.temperature = max(((air_mixture.temperature * old_heat_capacity + energy_used) / new_heat_capacity), TCMB);
    }
}
