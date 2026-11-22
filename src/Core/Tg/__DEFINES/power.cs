// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/power.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: GLOBAL_LIST_INIT(cable_layer_to_name) translated (2026-10-18: a cable terminal's examine reads it) and
// GLOBAL_LIST_INIT(cable_name_to_layer) (M7 slice 1: a power machine's cable layer choice); the constants of power.dm are
// generated into Defines.

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/power.dm:37  GLOBAL_LIST_INIT(cable_layer_to_name, list(...))
    public DmList cable_layer_to_name;

    // tg: code/__DEFINES/power.dm:37  (GLOBAL_MANAGED(cable_layer_to_name, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcable_layer_to_name()
    {
        cable_layer_to_name = Dm.list(
            Dm.kv(Dm.text("[]", CABLE_LAYER_1), CABLE_LAYER_1_NAME),
            Dm.kv(Dm.text("[]", CABLE_LAYER_2), CABLE_LAYER_2_NAME),
            Dm.kv(Dm.text("[]", CABLE_LAYER_3), CABLE_LAYER_3_NAME));
        Dm.append(ref gvars_datum_init_order, "cable_layer_to_name");
    }

    // Converts cable color name to its layer
    // tg: code/__DEFINES/power.dm:44  GLOBAL_LIST_INIT(cable_name_to_layer, list(...)) (the keys are the names the defines expand to)
    public DmList cable_name_to_layer;

    // tg: code/__DEFINES/power.dm:44  (GLOBAL_MANAGED(cable_name_to_layer, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcable_name_to_layer()
    {
        cable_name_to_layer = Dm.list(
            Dm.kv(CABLE_LAYER_1_NAME, CABLE_LAYER_1),
            Dm.kv(CABLE_LAYER_2_NAME, CABLE_LAYER_2),
            Dm.kv(CABLE_LAYER_3_NAME, CABLE_LAYER_3));
        Dm.append(ref gvars_datum_init_order, "cable_name_to_layer");
    }
}
