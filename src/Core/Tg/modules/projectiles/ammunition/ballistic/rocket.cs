// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/ballistic/rocket.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the Dardo rockets and the gyrojet's .75). The var blocks are generated (D111).

// tg: code/modules/projectiles/ammunition/ballistic/rocket.dm:1
public partial class obj_item_ammo_casing_rocket : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/ballistic/rocket.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless)));
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/rocket.dm:14
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]", base_icon_state);
        return dot;
    }
}

// tg: code/modules/projectiles/ammunition/ballistic/rocket.dm:35
public partial class obj_item_ammo_casing_a75 : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/ballistic/rocket.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless)));
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/ballistic/rocket.dm:46
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]", base_icon_state);
        return dot;
    }
}
