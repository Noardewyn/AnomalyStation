// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/physical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file; the silicons' overrides (/mob/living/silicon :25, :51 and /ai :28, :54) came with the
// silicons (SI1, D326).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/physical.dm:12  GLOBAL_DATUM_INIT(physical_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_physical physical_state;

    // tg: code/modules/tgui/states/physical.dm:12  (GLOBAL_MANAGED(physical_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalphysical_state()
    {
        physical_state = Dm.@new<datum_ui_state_physical>();
        Dm.append(ref gvars_datum_init_order, "physical_state");
    }
}

// tg: code/modules/tgui/states/physical.dm:14
[DmPath("/datum/ui_state/physical")]
public partial class datum_ui_state_physical : datum_ui_state
{
    // tg: code/modules/tgui/states/physical.dm:14
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        object dot = user.shared_ui_interaction(src_object);
        if (Dm.num(dot) > UI_CLOSE)
            return Dm.min(dot, user.physical_can_use_topic(src_object));
        return dot;
    }
}

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/physical.dm:38  GLOBAL_DATUM_INIT(physical_obscured_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_physical_obscured_state physical_obscured_state;

    // tg: code/modules/tgui/states/physical.dm:38  (GLOBAL_MANAGED(physical_obscured_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalphysical_obscured_state()
    {
        physical_obscured_state = Dm.@new<datum_ui_state_physical_obscured_state>();
        Dm.append(ref gvars_datum_init_order, "physical_obscured_state");
    }
}

// tg: code/modules/tgui/states/physical.dm:40
[DmPath("/datum/ui_state/physical_obscured_state")]
public partial class datum_ui_state_physical_obscured_state : datum_ui_state
{
    // tg: code/modules/tgui/states/physical.dm:40
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        object dot = user.shared_ui_interaction(src_object);
        if (Dm.num(dot) > UI_CLOSE)
            return Dm.min(dot, user.physical_obscured_can_use_topic(src_object));
        return dot;
    }
}

public partial class mob
{
    // tg: code/modules/tgui/states/physical.dm:19
    public virtual object physical_can_use_topic(object src_object = null)
    {
        return UI_CLOSE;
    }

    // tg: code/modules/tgui/states/physical.dm:45
    public virtual object physical_obscured_can_use_topic(object src_object = null)
    {
        return UI_CLOSE;
    }
}

public partial class mob_living
{
    // tg: code/modules/tgui/states/physical.dm:22
    public override object physical_can_use_topic(object src_object = null)
    {
        return shared_living_ui_distance(src_object as atom_movable);
    }

    // tg: code/modules/tgui/states/physical.dm:48
    public override object physical_obscured_can_use_topic(object src_object = null)
    {
        return shared_living_ui_distance(src_object as atom_movable, viewcheck: FALSE);
    }
}

public partial class mob_living_silicon
{
    // tg: code/modules/tgui/states/physical.dm:25
    public override object physical_can_use_topic(object src_object = null)
    {
        return max(UI_UPDATE, shared_living_ui_distance(src_object as atom_movable)); // Silicons can always see.
    }

    // tg: code/modules/tgui/states/physical.dm:51
    public override object physical_obscured_can_use_topic(object src_object = null)
    {
        return max(UI_UPDATE, shared_living_ui_distance(src_object as atom_movable, viewcheck: FALSE)); // Silicons can always see.
    }
}

public partial class mob_living_silicon_ai
{
    // tg: code/modules/tgui/states/physical.dm:28
    public override object physical_can_use_topic(object src_object = null)
    {
        return UI_UPDATE; // AIs are not physical.
    }

    // tg: code/modules/tgui/states/physical.dm:54
    public override object physical_obscured_can_use_topic(object src_object = null)
    {
        return UI_UPDATE; // AIs are not physical.
    }
}
