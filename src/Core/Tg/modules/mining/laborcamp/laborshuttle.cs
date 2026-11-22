// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/laborcamp/laborshuttle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mining/laborcamp/laborshuttle.dm:9
public partial class obj_machinery_computer_shuttle_labor_one_way : obj_machinery_computer_shuttle_labor
{
    // tg: code/modules/mining/laborcamp/laborshuttle.dm:16
    public override async DmTask<object> launch_check(mob user = null)
    {
        if (!Dm.truthy(await base.launch_check(user)))
            return FALSE;
        var M = SSshuttle.getShuttle("laborcamp") as obj_docking_port_mobile;
        if (M == null)
        {
            to_chat(user, span_warning("Cannot locate shuttle!"));
            return FALSE;
        }
        var S = M.get_docked() as obj_docking_port_stationary;
        if (Dm.eq(S?.name, "laborcamp_away"))
        {
            to_chat(user, span_warning("Shuttle is already at the outpost!"));
            return FALSE;
        }
        return TRUE;
    }
}
