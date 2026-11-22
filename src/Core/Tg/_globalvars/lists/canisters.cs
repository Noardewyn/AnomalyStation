// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/canisters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 leftovers (2026-10-16): the labels of the canister's relabel (canister.dm:623). The whole file.

public partial class datum_controller_global_vars
{
    ///List of all the gases, used in labelling the canisters
    // tg: code/_globalvars/lists/canisters.dm:2  GLOBAL_LIST_INIT(gas_id_to_canister, init_gas_id_to_canister())
    public DmList gas_id_to_canister;

    // tg: code/_globalvars/lists/canisters.dm:2  (GLOBAL_MANAGED(gas_id_to_canister, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgas_id_to_canister()
    {
        gas_id_to_canister = (DmList)init_gas_id_to_canister();
        Dm.append(ref gvars_datum_init_order, "gas_id_to_canister");
    }
}

public static partial class GlobalProcs
{
    ///Returns a map of canister id to its type path
    // tg: code/_globalvars/lists/canisters.dm:5
    public static object init_gas_id_to_canister()
    {
        return sort_list(Dm.list(
            Dm.kv(GAS_N2, typeof(obj_machinery_portable_atmospherics_canister_nitrogen)),
            Dm.kv(GAS_O2, typeof(obj_machinery_portable_atmospherics_canister_oxygen)),
            Dm.kv(GAS_CO2, typeof(obj_machinery_portable_atmospherics_canister_carbon_dioxide)),
            Dm.kv(GAS_PLASMA, typeof(obj_machinery_portable_atmospherics_canister_plasma)),
            Dm.kv(GAS_N2O, typeof(obj_machinery_portable_atmospherics_canister_nitrous_oxide)),
            Dm.kv(GAS_NITRIUM, typeof(obj_machinery_portable_atmospherics_canister_nitrium)),
            Dm.kv(GAS_BZ, typeof(obj_machinery_portable_atmospherics_canister_bz)),
            Dm.kv(GAS_AIR, typeof(obj_machinery_portable_atmospherics_canister_air)),
            Dm.kv(GAS_WATER_VAPOR, typeof(obj_machinery_portable_atmospherics_canister_water_vapor)),
            Dm.kv(GAS_TRITIUM, typeof(obj_machinery_portable_atmospherics_canister_tritium)),
            Dm.kv(GAS_HYPER_NOBLIUM, typeof(obj_machinery_portable_atmospherics_canister_nob)),
            Dm.kv(GAS_PLUOXIUM, typeof(obj_machinery_portable_atmospherics_canister_pluoxium)),
            Dm.kv("caution", typeof(obj_machinery_portable_atmospherics_canister)),
            Dm.kv(GAS_MIASMA, typeof(obj_machinery_portable_atmospherics_canister_miasma)),
            Dm.kv(GAS_FREON, typeof(obj_machinery_portable_atmospherics_canister_freon)),
            Dm.kv(GAS_HYDROGEN, typeof(obj_machinery_portable_atmospherics_canister_hydrogen)),
            Dm.kv(GAS_HEALIUM, typeof(obj_machinery_portable_atmospherics_canister_healium)),
            Dm.kv(GAS_PROTO_NITRATE, typeof(obj_machinery_portable_atmospherics_canister_proto_nitrate)),
            Dm.kv(GAS_ZAUKER, typeof(obj_machinery_portable_atmospherics_canister_zauker)),
            Dm.kv(GAS_HELIUM, typeof(obj_machinery_portable_atmospherics_canister_helium)),
            Dm.kv(GAS_ANTINOBLIUM, typeof(obj_machinery_portable_atmospherics_canister_antinoblium)),
            Dm.kv(GAS_HALON, typeof(obj_machinery_portable_atmospherics_canister_halon))));
    }
}
