// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter_gas.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice (G5, D163): the gases' effects on the crystal; sm_gas_data, the gas metadata of the monitor windows,
// came with the engineering package E2 (D287). The var blocks are generated (D111).

public static partial class GlobalProcs
{
    // tg: code/modules/power/supermatter/supermatter_gas.dm:1
    public static object init_sm_gas()
    {
        DmList gas_list = Dm.list();
        foreach (object sm_gas_path in Dm.each(subtypesof(typeof(datum_sm_gas))))
        {
            datum_sm_gas sm_gas = (datum_sm_gas)Dm.@new((Type)sm_gas_path);
            gas_list[sm_gas.gas_path] = sm_gas;
        }
        return gas_list;
    }

    /// Return a list info of the SM gases.
    /// Can only run after init_sm_gas
    // tg: code/modules/power/supermatter/supermatter_gas.dm:10
    public static object sm_gas_data()
    {
        DmList data = Dm.list();
        foreach (object gas_path in Dm.each(GLOB.sm_gas_behavior))
        {
            datum_sm_gas sm_gas = GLOB.sm_gas_behavior[gas_path] as datum_sm_gas;
            DmList singular_gas_data = Dm.list();
            singular_gas_data["desc"] = sm_gas.desc;

            // Positive is true if more of the amount is a good thing.
            DmList numeric_data = Dm.list();
            if (Dm.truthy(sm_gas.power_transmission))
            {
                DmList si_derived_data = siunit_isolated(Dm.num(sm_gas.power_transmission) * BASE_POWER_TRANSMISSION_RATE, "W/MeV", 2) as DmList;
                Dm.append(ref numeric_data, Dm.list(Dm.list(
                    Dm.kv("name", "Power Transmission Bonus"),
                    Dm.kv("amount", si_derived_data["coefficient"]),
                    Dm.kv("unit", si_derived_data["unit"]),
                    Dm.kv("positive", TRUE))));
            }
            if (Dm.truthy(sm_gas.heat_modifier))
            {
                Dm.append(ref numeric_data, Dm.list(Dm.list(
                    Dm.kv("name", "Waste Multiplier"),
                    Dm.kv("amount", 100 * Dm.num(sm_gas.heat_modifier)),
                    Dm.kv("unit", "%"),
                    Dm.kv("positive", FALSE))));
            }
            if (Dm.truthy(sm_gas.heat_resistance))
            {
                Dm.append(ref numeric_data, Dm.list(Dm.list(
                    Dm.kv("name", "Heat Resistance"),
                    Dm.kv("amount", 100 * Dm.num(sm_gas.heat_resistance)),
                    Dm.kv("unit", "%"),
                    Dm.kv("positive", TRUE))));
            }
            if (Dm.truthy(sm_gas.heat_power_generation))
            {
                DmList si_derived_data = siunit_isolated(Dm.num(sm_gas.heat_power_generation) * GAS_HEAT_POWER_SCALING_COEFFICIENT * MEGA * SECONDS / Dm.num(SSair.wait), "eV/K/s", 2) as DmList;
                Dm.append(ref numeric_data, Dm.list(Dm.list(
                    Dm.kv("name", "Heat Power Gain"),
                    Dm.kv("amount", si_derived_data["coefficient"]),
                    Dm.kv("unit", si_derived_data["unit"]),
                    Dm.kv("positive", TRUE))));
            }
            if (Dm.truthy(sm_gas.powerloss_inhibition))
            {
                Dm.append(ref numeric_data, Dm.list(Dm.list(
                    Dm.kv("name", "Power Decay Negation"),
                    Dm.kv("amount", 100 * Dm.num(sm_gas.powerloss_inhibition)),
                    Dm.kv("unit", "%"),
                    Dm.kv("positive", TRUE))));
            }
            singular_gas_data["numeric_data"] = numeric_data;
            data[gas_path] = singular_gas_data;
        }
        return data;
    }
}

/// Assoc of sm_gas_behavior[/datum/gas (path)] = datum/sm_gas (instance)
// tg: code/modules/power/supermatter/supermatter_gas.dm:61  GLOBAL_LIST_INIT(sm_gas_behavior, init_sm_gas())
public partial class datum_controller_global_vars
{
    // tg: code/modules/power/supermatter/supermatter_gas.dm:61  GLOBAL_LIST_INIT(sm_gas_behavior, init_sm_gas())
    public DmList sm_gas_behavior;

    // tg: code/modules/power/supermatter/supermatter_gas.dm:61  (GLOBAL_MANAGED(sm_gas_behavior, init_sm_gas()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsm_gas_behavior()
    {
        sm_gas_behavior = init_sm_gas() as DmList;
        Dm.append(ref gvars_datum_init_order, "sm_gas_behavior");
    }
}

/// Contains effects of gases when absorbed by the sm.
/// If the gas has no effects you do not need to add another sm_gas subtype,
/// We already guard for nulls in [/obj/machinery/power/supermatter_crystal/proc/calculate_gases]
// tg: code/modules/power/supermatter/supermatter_gas.dm:66
public partial class datum_sm_gas : datum
{
    // tg: code/modules/power/supermatter/supermatter_gas.dm:82
    public virtual async DmTask extra_effects(obj_machinery_power_supermatter_crystal sm = null)
    {
        return;
    }
}

// tg: code/modules/power/supermatter/supermatter_gas.dm:95
public partial class datum_sm_gas_carbon_dioxide : datum_sm_gas
{
    /// Can be on Oxygen or CO2, but better lump it here since CO2 is rarer.
    // tg: code/modules/power/supermatter/supermatter_gas.dm:103
    public override async DmTask extra_effects(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (!Dm.truthy(sm.gas_percentage[typeof(datum_gas_carbon_dioxide)]) || !Dm.truthy(sm.gas_percentage[typeof(datum_gas_oxygen)]))
            return;
        float co2_pp = Dm.num(sm.absorbed_gasmix.return_pressure()) * Dm.num(sm.gas_percentage[typeof(datum_gas_carbon_dioxide)]);
        float co2_ratio = clamp((1f / 2 * (co2_pp - CO2_CONSUMPTION_PP) / (co2_pp + CO2_PRESSURE_SCALING)), 0, 1);
        float consumed_co2 = Dm.num(sm.absorbed_gasmix.moles[typeof(datum_gas_carbon_dioxide)]) * co2_ratio;
        consumed_co2 = min(
            consumed_co2,
            Dm.num(sm.absorbed_gasmix.moles[typeof(datum_gas_carbon_dioxide)]),
            Dm.num(sm.absorbed_gasmix.moles[typeof(datum_gas_oxygen)])
        );
        if (!Dm.truthy(consumed_co2))
            return;
        sm.absorbed_gasmix.adjust_gas(typeof(datum_gas_carbon_dioxide), -consumed_co2);
        sm.absorbed_gasmix.adjust_gas(typeof(datum_gas_oxygen), -consumed_co2);

        sm.absorbed_gasmix.adjust_gas(typeof(datum_gas_pluoxium), consumed_co2);
    }
}

// tg: code/modules/power/supermatter/supermatter_gas.dm:150
public partial class datum_sm_gas_bz : datum_sm_gas
{
    /// Start to emit radballs at a maximum of 30% chance per tick
    // tg: code/modules/power/supermatter/supermatter_gas.dm:157
    public override async DmTask extra_effects(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (Dm.num(sm.gas_percentage[typeof(datum_gas_bz)]) > 0.4f && prob(30 * Dm.num(sm.gas_percentage[typeof(datum_gas_bz)])))
            await sm.fire_nuclear_particle();
    }
}

// tg: code/modules/power/supermatter/supermatter_gas.dm:167
public partial class datum_sm_gas_miasma : datum_sm_gas
{
    ///Miasma is really just microscopic particulate. It gets consumed like anything else that touches the crystal.
    // tg: code/modules/power/supermatter/supermatter_gas.dm:173
    public override async DmTask extra_effects(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (!Dm.truthy(sm.gas_percentage[typeof(datum_gas_miasma)]))
            return;
        float miasma_pp = Dm.num(sm.absorbed_gasmix.return_pressure()) * Dm.num(sm.gas_percentage[typeof(datum_gas_miasma)]);
        float miasma_ratio = clamp(((miasma_pp - MIASMA_CONSUMPTION_PP) / (miasma_pp + MIASMA_PRESSURE_SCALING)) * (1 + (sm.gas_heat_power_generation * MIASMA_GASMIX_SCALING)), 0, 1);
        float consumed_miasma = Dm.num(sm.absorbed_gasmix.moles[typeof(datum_gas_miasma)]) * miasma_ratio;
        if (!Dm.truthy(consumed_miasma))
            return;
        sm.absorbed_gasmix.adjust_gas(typeof(datum_gas_miasma), -consumed_miasma);
        sm.external_power_trickle += consumed_miasma * MIASMA_POWER_GAIN;
        sm.log_activation("miasma absorption");
    }
}

// tg: code/modules/power/supermatter/supermatter_gas.dm:211
public partial class datum_sm_gas_zauker : datum_sm_gas
{
    // tg: code/modules/power/supermatter/supermatter_gas.dm:218
    public override async DmTask extra_effects(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (!prob(Dm.num(sm.gas_percentage[typeof(datum_gas_zauker)]) * 100))
            return;
        playsound(sm.loc as atom, Dm.resource("sound/items/weapons/emitter2.ogg"), 100, TRUE, extrarange: 10);
        await sm.supermatter_zap(
            sm,
            range: 6,
            zap_str: clamp(sm.internal_energy * 1.6f * KILO * JOULES, 3.2f * MEGA * JOULES, 16 * MEGA * JOULES),
            zap_flags: ZAP_MOB_STUN,
            zap_cutoff: sm.zap_cutoff,
            power_level: sm.internal_energy,
            zap_icon: sm.zap_icon
        );
    }
}
