// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/ballistic/harpoon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the harpoon, a reusable caseless round (the harpoon quiver's; its gun is the uplink's harpoon gun,
// not-translated.md section 215). The var block is generated (D111).

// tg: code/modules/projectiles/ammunition/ballistic/harpoon.dm:1
public partial class obj_item_ammo_casing_harpoon : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/ballistic/harpoon.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless), TRUE));
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/harpoon.dm:13
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]", base_icon_state);
        return dot;
    }
}
