// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/deep_inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/deep_inventory.dm:13  GLOBAL_DATUM_INIT(deep_inventory_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_deep_inventory_state deep_inventory_state;

    // tg: code/modules/tgui/states/deep_inventory.dm:13  (GLOBAL_MANAGED(deep_inventory_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldeep_inventory_state()
    {
        deep_inventory_state = Dm.@new<datum_ui_state_deep_inventory_state>();
        Dm.append(ref gvars_datum_init_order, "deep_inventory_state");
    }
}

// tg: code/modules/tgui/states/deep_inventory.dm:15
[DmPath("/datum/ui_state/deep_inventory_state")]
public partial class datum_ui_state_deep_inventory_state : datum_ui_state
{
    // tg: code/modules/tgui/states/deep_inventory.dm:15
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        if (!Dm.truthy(user.contains(src_object as atom)))
            return UI_CLOSE;
        return user.shared_ui_interaction(src_object);
    }
}
