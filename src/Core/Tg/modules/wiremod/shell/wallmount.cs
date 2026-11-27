// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/wallmount.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/wiremod/shell/wallmount.dm:1
public partial class obj_structure_wallmount_circuit : obj_structure
{
    // tg: code/modules/wiremod/shell/wallmount.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), null, SHELL_CAPACITY_LARGE, SHELL_FLAG_REQUIRE_ANCHOR | SHELL_FLAG_USB_PORT));
        return dot;
    }

    // tg: code/modules/wiremod/shell/wallmount.dm:15
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        datum_component_shell shell_comp = GetComponent(typeof(datum_component_shell)) as datum_component_shell;
        if (Dm.truthy(shell_comp.locked))
        {
            balloon_alert(user, I18n.T("locked!"));
            return ITEM_INTERACT_FAILURE;
        }
        to_chat(user, span_notice(I18n.T("You start unsecuring the circuit box...")));
        if (Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
        {
            to_chat(user, span_notice(I18n.T("You unsecure the circuit box.")));
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            await deconstruct(TRUE);
        }
        return ITEM_INTERACT_SUCCESS;
    }
}
