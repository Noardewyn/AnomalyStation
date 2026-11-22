// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/variants/ferry.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/shuttle/mobile_port/variants/ferry.dm:1
public partial class obj_machinery_computer_shuttle_ferry : obj_machinery_computer_shuttle
{
    // tg: code/modules/shuttle/mobile_port/variants/ferry.dm:11
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (!Dm.truthy(allow_emag))
        {
            balloon_alert(user, "firewall too powerful!");
            return FALSE;
        }
        return await base.emag_act(user, emag_card, __extra);
    }

    // tg: code/modules/shuttle/mobile_port/variants/ferry.dm:17
    public override async DmTask<object> attack_ai(mob user = null) => Dm.truthy(allow_silicons) ? await base.attack_ai(user) : FALSE;

    // tg: code/modules/shuttle/mobile_port/variants/ferry.dm:20
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null) => Dm.truthy(allow_silicons) ? await base.attack_robot(user, modifiers) : FALSE;
}
