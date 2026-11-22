// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/notcontained.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file; the silicons' and the drone's overrides (:28, :31) came with the silicons (SI1, D326).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/notcontained.dm:13  GLOBAL_DATUM_INIT(notcontained_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_notcontained_state notcontained_state;

    // tg: code/modules/tgui/states/notcontained.dm:13  (GLOBAL_MANAGED(notcontained_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnotcontained_state()
    {
        notcontained_state = Dm.@new<datum_ui_state_notcontained_state>();
        Dm.append(ref gvars_datum_init_order, "notcontained_state");
    }
}

// tg: code/modules/tgui/states/notcontained.dm:15
[DmPath("/datum/ui_state/notcontained_state")]
public partial class datum_ui_state_notcontained_state : datum_ui_state
{
    // tg: code/modules/tgui/states/notcontained.dm:15
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        object dot = user.shared_ui_interaction(src_object);
        if (Dm.num(dot) > UI_CLOSE)
            return Dm.min(dot, user.notcontained_can_use_topic(src_object));
        return dot;
    }
}

public partial class mob
{
    // tg: code/modules/tgui/states/notcontained.dm:20
    public virtual object notcontained_can_use_topic(object src_object = null)
    {
        return UI_CLOSE;
    }
}

public partial class mob_living
{
    // tg: code/modules/tgui/states/notcontained.dm:23
    public override object notcontained_can_use_topic(object src_object = null)
    {
        if (Dm.truthy(((atom)src_object).contains(this)))
            return UI_CLOSE; // Close if we're inside it.
        return default_can_use_topic(src_object);
    }
}

public partial class mob_living_silicon
{
    // tg: code/modules/tgui/states/notcontained.dm:28
    public override object notcontained_can_use_topic(object src_object = null)
    {
        return default_can_use_topic(src_object); // Silicons use default bevhavior.
    }
}

public partial class mob_living_basic_drone
{
    // tg: code/modules/tgui/states/notcontained.dm:31
    public override object notcontained_can_use_topic(object src_object = null)
    {
        return default_can_use_topic(src_object); // Drones use default bevhavior.
    }
}
