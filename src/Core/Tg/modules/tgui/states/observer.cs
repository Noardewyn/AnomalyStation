// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/observer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice C (D281): the whole file (the orbit menu's state).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/observer.dm:12  GLOBAL_DATUM_INIT(observer_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_observer_state observer_state;

    // tg: code/modules/tgui/states/observer.dm:12  (GLOBAL_MANAGED(observer_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalobserver_state()
    {
        observer_state = Dm.@new<datum_ui_state_observer_state>();
        Dm.append(ref gvars_datum_init_order, "observer_state");
    }
}

/**
 * tgui state: observer_state
 *
 * Checks that the user is an observer/ghost.
 */
// tg: code/modules/tgui/states/observer.dm:14
[DmPath("/datum/ui_state/observer_state")]
public partial class datum_ui_state_observer_state : datum_ui_state
{
    // tg: code/modules/tgui/states/observer.dm:14
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        if (isobserver(user))
            return UI_INTERACTIVE;
        return UI_CLOSE;
    }
}
