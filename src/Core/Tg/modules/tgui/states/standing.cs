// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/standing.dm
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
    // tg: code/modules/tgui/states/standing.dm:7  GLOBAL_DATUM_INIT(standing_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_not_incapacitated_state_standing standing_state;

    // tg: code/modules/tgui/states/standing.dm:7  (GLOBAL_MANAGED(standing_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstanding_state()
    {
        standing_state = Dm.@new<datum_ui_state_not_incapacitated_state_standing>();
        Dm.append(ref gvars_datum_init_order, "standing_state");
    }
}

// tg: code/modules/tgui/states/standing.dm:9
[DmPath("/datum/ui_state/not_incapacitated_state/standing")]
public partial class datum_ui_state_not_incapacitated_state_standing : datum_ui_state_not_incapacitated_state
{
    // tg: code/modules/tgui/states/standing.dm:11
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        if (!isliving(user))
            return await base.can_use_topic(src_object, user);
        mob_living living_user = (mob_living)user;
        if (Dm.truthy(living_user.body_position))
            return UI_DISABLED;
        return await base.can_use_topic(src_object, user);
    }
}
