// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/host_monitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the avatar's belt monitor that reads its bitrunner's health. The var block is generated (D111).

// tg: code/modules/bitrunning/objects/host_monitor.dm:1
public partial class obj_item_bitrunning_host_monitor : obj_item
{
    // tg: code/modules/bitrunning/objects/host_monitor.dm:20
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);

        datum_component_avatar_connection connection = user.GetComponent(typeof(datum_component_avatar_connection)) as datum_component_avatar_connection;
        if (connection == null)
        {
            balloon_alert(user, I18n.T("data not recognized"));
            return dot;
        }

        mob_living pilot = connection.old_body_ref?.resolve() as mob_living;
        if (pilot == null)
        {
            balloon_alert(user, I18n.T("host not recognized"));
            return dot;
        }

        to_chat(user, span_notice(I18n.T("Current host health: []%", pilot.health / pilot.maxHealth * 100)));
        return dot;
    }
}
