// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/never.dm
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
    // tg: code/modules/tgui/states/never.dm:12  GLOBAL_DATUM_INIT(never_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_never_state never_state;

    // tg: code/modules/tgui/states/never.dm:12  (GLOBAL_MANAGED(never_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnever_state()
    {
        never_state = Dm.@new<datum_ui_state_never_state>();
        Dm.append(ref gvars_datum_init_order, "never_state");
    }
}

// tg: code/modules/tgui/states/never.dm:14
[DmPath("/datum/ui_state/never_state")]
public partial class datum_ui_state_never_state : datum_ui_state
{
    // tg: code/modules/tgui/states/never.dm:14
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        return UI_CLOSE;
    }
}
