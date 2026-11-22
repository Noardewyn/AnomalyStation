// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/netpod/tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the netpod's crowbar (the pry, or a punch in combat mode) and screwdriver.

// tg: code/modules/bitrunning/netpod/tools.dm:1
public partial class obj_machinery_netpod : obj_machinery
{
    // tg: code/modules/bitrunning/netpod/tools.dm:1
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
        {
            await attack_hand(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return await default_pry_open(user, tool, deconstruct_on_fail: TRUE);
    }

    // tg: code/modules/bitrunning/netpod/tools.dm:8
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        if (occupant != null)
        {
            balloon_alert(user, I18n.T("in use!"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(state_open))
        {
            balloon_alert(user, I18n.T("close it first!"));
            return ITEM_INTERACT_SUCCESS;
        }

        return default_deconstruction_screwdriver(user, tool);
    }
}
