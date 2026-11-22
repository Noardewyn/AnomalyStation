// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/hold_or_view.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4d (D326): the whole file (a finished painting's window).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/hold_or_view.dm:7  GLOBAL_DATUM_INIT(hold_or_view_state, ...)
    public datum_ui_state_hold_or_view_state hold_or_view_state;

    // tg: code/modules/tgui/states/hold_or_view.dm:7  (GLOBAL_MANAGED(hold_or_view_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhold_or_view_state()
    {
        hold_or_view_state = Dm.@new<datum_ui_state_hold_or_view_state>();
        Dm.append(ref gvars_datum_init_order, "hold_or_view_state");
    }
}

/**
 * tgui state: view
 *
 * Checks if the object is in view or the mob is holding it, otherwise close the UI
 */
// tg: code/modules/tgui/states/hold_or_view.dm:9
public partial class datum_ui_state_hold_or_view_state : datum_ui_state
{
    // tg: code/modules/tgui/states/hold_or_view.dm:9
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        if (Dm.@in(user, viewers(user.client?.view, src_object)) || Dm.truthy(user.is_holding(src_object as obj_item)))
            return UI_INTERACTIVE;
        await DmTask.CompletedTask;
        return UI_CLOSE;
    }
}
