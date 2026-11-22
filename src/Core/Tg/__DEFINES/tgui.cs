// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/tgui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate package A3, D316): ADMIN_STATE, the macro members of the file (rules 11.1); its constants are generated.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/tgui.dm:66  #define ADMIN_STATE(required_perms) (GLOB.admin_states[required_perms] ||= new /datum/ui_state/admin_state(required_perms))
    public static datum_ui_state_admin_state ADMIN_STATE(float required_perms)
    {
        if (!Dm.truthy(GLOB.admin_states[required_perms]))
            GLOB.admin_states[required_perms] = Dm.@new<datum_ui_state_admin_state>(required_perms);
        return GLOB.admin_states[required_perms] as datum_ui_state_admin_state;
    }
}
