// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/hands.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file; the cyborg's override (:27) came with the cyborgs (silicons SI2, D326).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/hands.dm:12  GLOBAL_DATUM_INIT(hands_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_hands_state hands_state;

    // tg: code/modules/tgui/states/hands.dm:12  (GLOBAL_MANAGED(hands_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhands_state()
    {
        hands_state = Dm.@new<datum_ui_state_hands_state>();
        Dm.append(ref gvars_datum_init_order, "hands_state");
    }
}

// tg: code/modules/tgui/states/hands.dm:14
[DmPath("/datum/ui_state/hands_state")]
public partial class datum_ui_state_hands_state : datum_ui_state
{
    // tg: code/modules/tgui/states/hands.dm:14
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        object dot = user.shared_ui_interaction(src_object);
        if (Dm.num(dot) > UI_CLOSE)
            return Dm.min(dot, user.hands_can_use_topic(src_object));
        return dot;
    }
}

public partial class mob
{
    // tg: code/modules/tgui/states/hands.dm:19
    public virtual object hands_can_use_topic(object src_object = null)
    {
        return UI_CLOSE;
    }
}

public partial class mob_living
{
    // tg: code/modules/tgui/states/hands.dm:22
    public override object hands_can_use_topic(object src_object = null)
    {
        // is_holding is typed obj/item; a src_object that is not an item is never held (DM's get_held_index_of_item finds 0)
        if (src_object is obj_item held && Dm.truthy(is_holding(held)))
            return UI_INTERACTIVE;
        return UI_CLOSE;
    }
}

// tg: code/modules/tgui/states/hands.dm:27
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/tgui/states/hands.dm:27
    public override object hands_can_use_topic(object src_object = null)
    {
        if (Dm.truthy(activated(src_object as obj_item)))
            return UI_INTERACTIVE;
        return UI_CLOSE;
    }
}
