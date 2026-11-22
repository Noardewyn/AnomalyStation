// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A3 (D316): the whole file (the Dynamic panel's and tester's state).

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/admin.dm:12  GLOBAL_DATUM_INIT(admin_states, /alist, alist())
    public DmList admin_states;

    // tg: code/modules/tgui/states/admin.dm:12  (GLOBAL_MANAGED(admin_states, alist()); the earlier of two definitions on this type, rules 7.5)
    public void InitGlobaladmin_states__d0()
    {
        admin_states = Dm.alist();
        Dm.append(ref gvars_datum_init_order, "admin_states");
    }

    // tg: code/modules/tgui/states/admin.dm:13  GLOBAL_PROTECT(admin_states) (__DEFINES/__globals.dm:15)
    public virtual void InitGlobaladmin_states()
    {
        InitGlobaladmin_states__d0();
        gvars_datum_protected_varlist["admin_states"] = TRUE;
    }
}

/**
 * tgui state: admin_state
 *
 * Checks if the user has specific admin permissions.
 */
// tg: code/modules/tgui/states/admin.dm:17
public partial class datum_ui_state_admin_state : datum_ui_state
{
    // tg: code/modules/tgui/states/admin.dm:19
    public override async DmTask New(params object[] __extra)
    {
        object required_perms = __extra.Length > 0 ? __extra[0] : null;
        required_perms ??= R_ADMIN;
        await base.New();
        this.required_perms = Dm.num(required_perms);
    }

    // tg: code/modules/tgui/states/admin.dm:23
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        if (Dm.truthy(check_rights_for(user.client, required_perms)))
            return UI_INTERACTIVE;
        return UI_CLOSE;
    }
}
