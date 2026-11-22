// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/plumbing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The ducts' layers (modules/plumbing/ducts.dm: a duct's Initialize and examine). The whole file.

public partial class datum_controller_global_vars
{
    /// List of plumbing layers as name => bitflag
    // tg: code/_globalvars/lists/plumbing.dm:2  GLOBAL_LIST_INIT(plumbing_layers, list(...))
    public DmList plumbing_layers;

    // tg: code/_globalvars/lists/plumbing.dm:2  (GLOBAL_MANAGED(plumbing_layers, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplumbing_layers()
    {
        plumbing_layers = Dm.list(
            Dm.kv("First Layer", FIRST_DUCT_LAYER),
            Dm.kv("Second Layer", SECOND_DUCT_LAYER),
            Dm.kv("Default Layer", THIRD_DUCT_LAYER),
            Dm.kv("Fourth Layer", FOURTH_DUCT_LAYER),
            Dm.kv("Fifth Layer", FIFTH_DUCT_LAYER));
        Dm.append(ref gvars_datum_init_order, "plumbing_layers");
    }

    /// Reverse of plumbing_layers, as "[bitflag]" => name
    // tg: code/_globalvars/lists/plumbing.dm:11  GLOBAL_LIST_INIT(plumbing_layer_names, list(...))
    public DmList plumbing_layer_names;

    // tg: code/_globalvars/lists/plumbing.dm:11  (GLOBAL_MANAGED(plumbing_layer_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplumbing_layer_names()
    {
        plumbing_layer_names = Dm.list(
            Dm.kv(Dm.text("[]", FIRST_DUCT_LAYER), "First Layer"),
            Dm.kv(Dm.text("[]", SECOND_DUCT_LAYER), "Second Layer"),
            Dm.kv(Dm.text("[]", THIRD_DUCT_LAYER), "Default Layer"),
            Dm.kv(Dm.text("[]", FOURTH_DUCT_LAYER), "Fourth Layer"),
            Dm.kv(Dm.text("[]", FIFTH_DUCT_LAYER), "Fifth Layer"));
        Dm.append(ref gvars_datum_init_order, "plumbing_layer_names");
    }
}
