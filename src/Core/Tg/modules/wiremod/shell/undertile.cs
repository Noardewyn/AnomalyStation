// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/undertile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var block is generated.

// tg: code/modules/wiremod/shell/undertile.dm:1
public partial class obj_item_undertile_circuit : obj_item
{
    // tg: code/modules/wiremod/shell/undertile.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE, INVISIBILITY_OBSERVER, Dm.named("use_anchor", TRUE)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), null, SHELL_CAPACITY_SMALL, SHELL_FLAG_REQUIRE_ANCHOR | SHELL_FLAG_USB_PORT));
        return dot;
    }
}
