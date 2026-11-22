// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/atmos_computers/__identifiers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Package E5 (the atmospherics control consoles): the whole file. Its #defines are global (never #undef'd) outside
// code/__DEFINES, so they are GlobalProcs constants here (translation-rules.md 11.4).

// ATMOSIA GAS MONITOR SUITE TAGS
// Things that use these include atmos control monitors, sensors, inputs, and outlets.
// They last three adds _sensor, _in, and _out respectively to the id_tag variable.
// Don't put underscores here, we use them as delimiters.
public static partial class GlobalProcs
{
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:6
    public const string ATMOS_GAS_MONITOR_O2 = GAS_O2;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:7
    public const string ATMOS_GAS_MONITOR_PLAS = GAS_PLASMA;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:8
    public const string ATMOS_GAS_MONITOR_AIR = GAS_AIR;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:9
    public const string ATMOS_GAS_MONITOR_MIX = "mix";
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:10
    public const string ATMOS_GAS_MONITOR_N2O = GAS_N2O;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:11
    public const string ATMOS_GAS_MONITOR_N2 = GAS_N2;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:12
    public const string ATMOS_GAS_MONITOR_CO2 = GAS_CO2;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:13
    public const string ATMOS_GAS_MONITOR_BZ = GAS_BZ;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:14
    public const string ATMOS_GAS_MONITOR_FREON = GAS_FREON;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:15
    public const string ATMOS_GAS_MONITOR_HALON = GAS_HALON;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:16
    public const string ATMOS_GAS_MONITOR_HEALIUM = GAS_HEALIUM;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:17
    public const string ATMOS_GAS_MONITOR_H2 = GAS_HYDROGEN;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:18
    public const string ATMOS_GAS_MONITOR_HYPERNOBLIUM = GAS_HYPER_NOBLIUM;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:19
    public const string ATMOS_GAS_MONITOR_MIASMA = GAS_MIASMA;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:20
    public const string ATMOS_GAS_MONITOR_NITRIUM = GAS_NITRIUM;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:21
    public const string ATMOS_GAS_MONITOR_PLUOXIUM = GAS_PLUOXIUM;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:22
    public const string ATMOS_GAS_MONITOR_PROTO_NITRATE = GAS_PROTO_NITRATE;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:23
    public const string ATMOS_GAS_MONITOR_TRITIUM = GAS_TRITIUM;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:24
    public const string ATMOS_GAS_MONITOR_H2O = GAS_WATER_VAPOR;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:25
    public const string ATMOS_GAS_MONITOR_ZAUKER = GAS_ZAUKER;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:26  (tg's own value: the healium id)
    public const string ATMOS_GAS_MONITOR_HELIUM = GAS_HEALIUM;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:27
    public const string ATMOS_GAS_MONITOR_ANTINOBLIUM = GAS_ANTINOBLIUM;
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:28
    public const string ATMOS_GAS_MONITOR_INCINERATOR = "incinerator";
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:29
    public const string ATMOS_GAS_MONITOR_ORDNANCE_BURN = "ordnanceburn";
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:30
    public const string ATMOS_GAS_MONITOR_ORDNANCE_FREEZER = "ordnancefreezer";
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:31
    public const string ATMOS_GAS_MONITOR_DISTRO = "distro";
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:32
    public const string ATMOS_GAS_MONITOR_WASTE = "waste";
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:33
    public const string ATMOS_GAS_MONITOR_ENGINE = "engine";
}

public partial class datum_controller_global_vars
{
    // Human-readble names of these funny tags.
    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:36  GLOBAL_LIST_INIT(station_gas_chambers, list(...))
    public DmList station_gas_chambers;

    // tg: code/game/machinery/computer/atmos_computers/__identifiers.dm:36
    public virtual void InitGlobalstation_gas_chambers()
    {
        station_gas_chambers = Dm.list(
            Dm.kv(ATMOS_GAS_MONITOR_O2, "Oxygen Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_PLAS, "Plasma Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_AIR, "Mixed Air Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_N2O, "Nitrous Oxide Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_N2, "Nitrogen Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_CO2, "Carbon Dioxide Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_BZ, "BZ Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_FREON, "Freon Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_HALON, "Halon Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_HEALIUM, "Healium Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_H2, "Hydrogen Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_HYPERNOBLIUM, "Hypernoblium Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_MIASMA, "Miasma Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_NITRIUM, "Nitrium Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_PLUOXIUM, "Pluoxium Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_PROTO_NITRATE, "Proto-Nitrate Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_TRITIUM, "Tritium Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_H2O, "Water Vapor Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_ZAUKER, "Zauker Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_HELIUM, "Helium Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_ANTINOBLIUM, "Antinoblium Supply"),
            Dm.kv(ATMOS_GAS_MONITOR_MIX, "Mix Chamber"),
            Dm.kv(ATMOS_GAS_MONITOR_INCINERATOR, "Incinerator Chamber"),
            Dm.kv(ATMOS_GAS_MONITOR_ORDNANCE_BURN, "Ordnance Burn Chamber"),
            Dm.kv(ATMOS_GAS_MONITOR_ORDNANCE_FREEZER, "Ordnance Freezer Chamber"),
            Dm.kv(ATMOS_GAS_MONITOR_DISTRO, "Distribution Loop"),
            Dm.kv(ATMOS_GAS_MONITOR_WASTE, "Waste Loop"),
            Dm.kv(ATMOS_GAS_MONITOR_ENGINE, "Supermatter Engine Chamber"));
        Dm.append(ref gvars_datum_init_order, "station_gas_chambers");
    }
}
