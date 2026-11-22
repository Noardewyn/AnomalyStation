// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/alerts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the bitrunning alerts (a generic one, renamed by its sender) and the completed domain's,
// which asks to disconnect safely. The var blocks are generated (D111).

// tg: code/modules/bitrunning/alerts.dm:7
public partial class atom_movable_screen_alert_bitrunning_qserver_domain_complete : atom_movable_screen_alert_bitrunning
{
    // tg: code/modules/bitrunning/alerts.dm:12
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;

        mob_living living_owner = owner as mob_living;
        if (!isliving(living_owner))
            return dot;

        if (Dm.eq(await tgui_alert(living_owner, I18n.T("Disconnect safely?"), I18n.T("Server Message"), Dm.list("Exit", "Remain"), 10 * SECONDS), "Exit"))
            living_owner.SEND_SIGNAL(COMSIG_BITRUNNER_ALERT_SEVER, Dm.list(living_owner));
        return dot;
    }
}
