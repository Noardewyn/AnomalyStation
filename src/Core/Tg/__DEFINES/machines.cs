// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/machines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only DYNAMIC_TO_STATIC_CHANNEL (M3 P3: the machines' static power usage); the file's constants are emitted by
// the defines converter, its other function-like macros come with their users (rules 11.1). Gaps RD2 (D333): the fabricators'
// build types' display names (GLOB.build_types_to_string), which the R&D console and the Science Hub app read.

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/machines.dm:77  GLOBAL_LIST_INIT(build_types_to_string, list(...))
    public DmList build_types_to_string;

    // tg: code/__DEFINES/machines.dm:77  (GLOBAL_LIST_INIT, __DEFINES/_globals.dm)
    public virtual void InitGlobalbuild_types_to_string()
    {
        build_types_to_string = Dm.list(
            Dm.kv(Dm.text("[]", IMPRINTER), "Circuit Imprinter"),
            Dm.kv(Dm.text("[]", PROTOLATHE), "Protolathe"),
            Dm.kv(Dm.text("[]", AUTOLATHE), "Autolathe"),
            Dm.kv(Dm.text("[]", MECHFAB), "Exosuit Fabricator"),
            Dm.kv(Dm.text("[]", BIOGENERATOR), "Biogenerator"),
            Dm.kv(Dm.text("[]", LIMBGROWER), "Limb Grower"),
            Dm.kv(Dm.text("[]", SMELTER), "Smelter"),
            Dm.kv(Dm.text("[]", AWAY_LATHE), "Off-Grid Protolathe"),
            Dm.kv(Dm.text("[]", AWAY_IMPRINTER), "Off-Grid Circuit Imprinter"),
            Dm.kv(Dm.text("[]", COMPONENT_PRINTER), "Component Printer"));
        Dm.append(ref gvars_datum_init_order, "build_types_to_string");
    }
}

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/machines.dm:22  #define DYNAMIC_TO_STATIC_CHANNEL(dyn_channel) (dyn_channel + (AREA_USAGE_STATIC_START - AREA_USAGE_DYNAMIC_START))
    public static float DYNAMIC_TO_STATIC_CHANNEL(float dyn_channel) => dyn_channel + (AREA_USAGE_STATIC_START - AREA_USAGE_DYNAMIC_START);
}
