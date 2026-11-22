// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/skub.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file (skub, which a tank holder takes: tg's holder_skub state, structures/tank_holder.dm). The var
// block is generated (D111).

// tg: code/game/objects/items/skub.dm:1
public partial class obj_item_skub : obj_item
{
    // tg: code/game/objects/items/skub.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_container_item_tank_holder), "holder_skub", FALSE));
        return dot;
    }

    // tg: code/game/objects/items/skub.dm:14
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] has declared themself as anti-skub! The skub tears them apart!", user)));
        await user.gib(DROP_ALL_REMAINS);
        playsound(this, Dm.resource("sound/items/eatfood.ogg"), 50, TRUE, -1);
        return MANUAL_SUICIDE;
    }
}
