// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/not_incapacitated.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file. The var block (turf_check) is generated.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/not_incapacitated.dm:12  GLOBAL_DATUM_INIT(not_incapacitated_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_not_incapacitated_state not_incapacitated_state;

    // tg: code/modules/tgui/states/not_incapacitated.dm:12  (GLOBAL_MANAGED(not_incapacitated_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnot_incapacitated_state()
    {
        not_incapacitated_state = Dm.@new<datum_ui_state_not_incapacitated_state>();
        Dm.append(ref gvars_datum_init_order, "not_incapacitated_state");
    }
}

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/not_incapacitated.dm:20  GLOBAL_DATUM_INIT(not_incapacitated_turf_state, ...) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_not_incapacitated_state not_incapacitated_turf_state;

    // tg: code/modules/tgui/states/not_incapacitated.dm:20  (GLOBAL_MANAGED(not_incapacitated_turf_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnot_incapacitated_turf_state()
    {
        not_incapacitated_turf_state = Dm.@new<datum_ui_state_not_incapacitated_state>(null, TRUE);
        Dm.append(ref gvars_datum_init_order, "not_incapacitated_turf_state");
    }
}

/**
 * tgui state: not_incapacitated_state
 *
 * Checks that the user isn't incapacitated
 */
// tg: code/modules/tgui/states/not_incapacitated.dm:22
[DmPath("/datum/ui_state/not_incapacitated_state")]
public partial class datum_ui_state_not_incapacitated_state : datum_ui_state
{
    // tg: code/modules/tgui/states/not_incapacitated.dm:25
    [DmParams("loc", "no_turfs")]
    public override async DmTask New(params object[] __extra)
    {
        object no_turfs = Dm.arg(__extra, 1) ?? FALSE;
        await base.New(__extra);
        turf_check = Dm.num(no_turfs);
    }

    // tg: code/modules/tgui/states/not_incapacitated.dm:29
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user))
            return UI_CLOSE;
        if (HAS_TRAIT(this, TRAIT_UI_BLOCKED) || Dm.truthy(user.incapacitated) || (Dm.truthy(turf_check) && !isturf(user.loc)))
            return UI_DISABLED;
        return UI_INTERACTIVE;
    }
}
