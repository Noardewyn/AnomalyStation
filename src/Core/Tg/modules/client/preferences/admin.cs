// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/admin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate X2): only the ghost roles' opt-out while adminned (SSpolling reads it); the other admin preferences come with the
// admin tools that read them.

/// When enabled, prevents any and all ghost role pop-ups WHILE ADMINNED.
// tg: code/modules/client/preferences/admin.dm:55
public partial class datum_preference_toggle_ghost_roles_as_admin : datum_preference_toggle
{
    // tg: code/modules/client/preferences/admin.dm:60
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;

        return is_admin(preferences.parent);
    }
}
