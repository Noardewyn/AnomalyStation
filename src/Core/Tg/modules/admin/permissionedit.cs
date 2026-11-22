// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/permissionedit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice I): only auto_deadmin (the job's and the lobby's auto deadmin); the permissions panel is an admin panel (not ported).

public partial class datum_admins : datum
{
    // tg: code/modules/admin/permissionedit.dm:573
    public virtual object auto_deadmin()
    {
        if (Dm.truthy(owner.is_localhost()))
            return FALSE;
        // tg: :576-577  the bypass_deadmin_in_centcom toggle of an admin on the CentCom level: an admin preference (not ported), its default
        // FALSE

        to_chat(owner, span_interface("You are now a normal player."), confidential: TRUE);
        client old_owner = owner;
        deactivate();
        message_admins(Dm.text("[] deadmined via auto-deadmin config.", old_owner));
        log_admin(Dm.text("[] deadmined via auto-deadmin config.", old_owner));
        return TRUE;
    }
}
