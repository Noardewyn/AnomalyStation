// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/atmospherics/atmos_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the function-like macros THERMAL_ENERGY (M2 P2: /datum/gas_mixture/proc/thermal_energy),
// PIPING_LAYER_DOUBLE_SHIFT (M3 I1b: the bridge pipe's centre), PIPING_LAYER_SHIFT and PIPING_FORWARD_SHIFT (the devices' pipe
// pictures, the owner's play test 2026-10-09) translated, and (M4b slice 1, the air that moves) the LINDA helpers CANATMOSPASS,
// CALCULATE_ADJACENT_TURFS, TURFS_CAN_SHARE, TURF_SHARES, LINDA_CYCLE_ARCHIVE and CALCULATE_CONDUCTION_ENERGY; and (M4b slice
// 3, the gas visuals) GAS_OVERLAYS and GLOB.nonoverlaying_gases.

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:56  GLOBAL_LIST_INIT(nonoverlaying_gases, typecache_of_gases_with_no_overlays())
    public DmList nonoverlaying_gases;

    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:56  (GLOBAL_MANAGED(nonoverlaying_gases, ...), __DEFINES/_globals.dm)
    public virtual void InitGlobalnonoverlaying_gases()
    {
        nonoverlaying_gases = typecache_of_gases_with_no_overlays() as DmList;
        Dm.append(ref gvars_datum_init_order, "nonoverlaying_gases");
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:58  #define GAS_OVERLAYS(moles, out_var, z_layer_turf): a statement macro
    // that fills out_var; here a member that returns it (rules 11.1). The overlay objects are generate_gas_overlays' (gas_types.cs).
    public static DmList GAS_OVERLAYS(DmList moles, object z_layer_turf)
    {
        DmList out_var = Dm.list();
        float offset = Dm.num(GET_TURF_PLANE_OFFSET(z_layer_turf)) + 1;
        DmList _META_MOLES_VISIBLE = (DmList)datum_gas_mixture.gas_meta[META_GAS_MOLES_VISIBLE];
        DmList _META_GAS_OVERLAY = (DmList)datum_gas_mixture.gas_meta[META_GAS_OVERLAY];
        foreach ((object gas_id, object amount) in Dm.pairs(moles))
        {
            if (Dm.truthy(GLOB.nonoverlaying_gases[gas_id])) continue;
            if (Dm.num(amount) <= Dm.num(_META_MOLES_VISIBLE[gas_id])) continue;
            DmList _GAS_OVERLAY = (DmList)((DmList)_META_GAS_OVERLAY[gas_id])[offset];
            Dm.append(out_var, _GAS_OVERLAY[Math.Min(TOTAL_VISIBLE_STATES, CEILING(Dm.num(amount) / MOLES_GAS_VISIBLE_STEP, 1))]);
        }
        return out_var;
    }

    ///Calculate the thermal energy of the selected gas (J)
    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:54  #define THERMAL_ENERGY(gas) (gas.temperature * gas.heat_capacity())
    public static float THERMAL_ENERGY(datum_gas_mixture gas) => gas.temperature * Dm.num(gas.heat_capacity());

    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:44  #define PIPING_LAYER_DOUBLE_SHIFT(T, PipingLayer)
    public static void PIPING_LAYER_DOUBLE_SHIFT(image T, float PipingLayer) // an image or mutable_appearance (rules 11.1)
    {
        if (T.layer > -1)
        {
            T.pixel_x = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X;
            T.pixel_y = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_Y;
        }
        else
        {
            T.pixel_w = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X;
            T.pixel_z = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_Y;
        }
    }

    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:44  #define PIPING_LAYER_DOUBLE_SHIFT(T, PipingLayer) (an atom; rules 11.1; the meter and its fitting, M7 slice 4)
    public static void PIPING_LAYER_DOUBLE_SHIFT(atom T, float PipingLayer)
    {
        if (T.layer > -1)
        {
            T.pixel_x = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X;
            T.pixel_y = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_Y;
        }
        else
        {
            T.pixel_w = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X;
            T.pixel_z = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_Y;
        }
    }

    ///Moves the icon of the device based on the piping layer and on the direction
    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:8  #define PIPING_LAYER_SHIFT(T, PipingLayer) (an atom; rules 11.1)
    public static void PIPING_LAYER_SHIFT(atom T, float PipingLayer)
    {
        if (T.layer > -1)
        {
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(NORTH, SOUTH))))
                T.pixel_x = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X;
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(EAST, WEST))))
                T.pixel_y = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_Y;
        }
        else
        {
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(NORTH, SOUTH))))
                T.pixel_w = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X;
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(EAST, WEST))))
                T.pixel_z = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_Y;
        }
    }

    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:8  #define PIPING_LAYER_SHIFT(T, PipingLayer) (an image)
    public static void PIPING_LAYER_SHIFT(image T, float PipingLayer)
    {
        if (T.layer > -1)
        {
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(NORTH, SOUTH))))
                T.pixel_x = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X;
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(EAST, WEST))))
                T.pixel_y = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_Y;
        }
        else
        {
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(NORTH, SOUTH))))
                T.pixel_w = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_X;
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(EAST, WEST))))
                T.pixel_z = (PipingLayer - PIPING_LAYER_DEFAULT) * PIPING_LAYER_P_Y;
        }
    }

    ///Moves the icon of the device based on the piping layer and on the direction, the shift amount is dictated by more_shift
    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:25  #define PIPING_FORWARD_SHIFT(T, PipingLayer, more_shift) (an image)
    public static void PIPING_FORWARD_SHIFT(image T, float PipingLayer, float more_shift)
    {
        if (T.layer > -1)
        {
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(NORTH, SOUTH))))
                T.pixel_y += more_shift * (PipingLayer - PIPING_LAYER_DEFAULT);
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(EAST, WEST))))
                T.pixel_x += more_shift * (PipingLayer - PIPING_LAYER_DEFAULT);
        }
        else
        {
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(NORTH, SOUTH))))
                T.pixel_z += more_shift * (PipingLayer - PIPING_LAYER_DEFAULT);
            if (Dm.truthy(Dm.bitand(T.dir, Dm.bitor(EAST, WEST))))
                T.pixel_w += more_shift * (PipingLayer - PIPING_LAYER_DEFAULT);
        }
    }

    //DO NOT USE THESE FOR ACCESSING ATMOS DATA, THEY MUTATE THINGS WHEN CALLED. I WILL BEAT YOU WITH A STICK. See the actual proc for more details
    ///Check if an atom (A) and a turf (O) allow gas passage based on the atom's can_atmos_pass var, do not use.
    ///(V) is if the share is vertical or not. True or False
    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:4  #define CANATMOSPASS(A, O, V)
    public static object CANATMOSPASS(atom A, turf O, float V)
        => (A.can_atmos_pass == ATMOS_PASS_PROC ? A.can_atmos_pass__proc(O, V) : (A.can_atmos_pass == ATMOS_PASS_DENSITY ? Dm.not(A.density) : A.can_atmos_pass));

    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:78  #define CALCULATE_ADJACENT_TURFS(T, state) SSair.adjacent_rebuild[T] = state
    // (the #ifdef TESTING form is inactive in production, rules 1.7)
    public static void CALCULATE_ADJACENT_TURFS(turf T, object state) => SSair.adjacent_rebuild[T] = state;

    //If you're doing spreading things related to atmos, DO NOT USE CANATMOSPASS, IT IS NOT CHEAP. use this instead, the info is cached after all. it's tweaked just a bit to allow for circular checks
    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:82  #define TURFS_CAN_SHARE(T1, T2)
    public static bool TURFS_CAN_SHARE(turf T1, turf T2)
        => Dm.truthy(LAZYACCESS(T2.atmos_adjacent_turfs, T1)) || Dm.truthy(LAZYLEN(Dm.bitand(T1.atmos_adjacent_turfs, T2.atmos_adjacent_turfs)));

    //Use this to see if a turf is fully blocked or not, think windows or firelocks. Fails with 1x1 non full tile windows, but it's not worth the cost.
    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:84  #define TURF_SHARES(T) (LAZYLEN(T.atmos_adjacent_turfs))
    public static float TURF_SHARES(turf T) => LAZYLEN(T.atmos_adjacent_turfs);

    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:86  #define LINDA_CYCLE_ARCHIVE(turf) (a statement)
    public static void LINDA_CYCLE_ARCHIVE(turf_open turf)
    {
        turf.air.archive();
        turf.archived_cycle = SSair.times_fired;
        turf.temperature_archived = turf.temperature;
    }

    // tg: code/__DEFINES/atmospherics/atmos_helpers.dm:127  #define CALCULATE_CONDUCTION_ENERGY(temperature_delta, heat_capacity_one, heat_capacity_two)
    public static float CALCULATE_CONDUCTION_ENERGY(float temperature_delta, float heat_capacity_one, float heat_capacity_two)
        => ((temperature_delta) * ((heat_capacity_one) * ((heat_capacity_two) / ((heat_capacity_one) + (heat_capacity_two)))));
}
