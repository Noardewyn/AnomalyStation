// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/server.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

/**
 * # Server
 *
 * Immobile (but not dense) shells that can interact with
 * world.
 */
// tg: code/modules/wiremod/shell/server.dm:7
public partial class obj_structure_server : obj_structure
{
    // tg: code/modules/wiremod/shell/server.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), null, SHELL_CAPACITY_VERY_LARGE, SHELL_FLAG_REQUIRE_ANCHOR | SHELL_FLAG_USB_PORT));
        return dot;
    }

    // tg: code/modules/wiremod/shell/server.dm:20
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await set_anchored(Dm.not(anchored));
        tool.play_tool_sound(this);
        balloon_alert(user, Dm.truthy(anchored) ? I18n.T("secured") : I18n.T("unsecured"));
        return TRUE;
    }
}
