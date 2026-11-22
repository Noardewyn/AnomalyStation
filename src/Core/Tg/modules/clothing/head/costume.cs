// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/costume.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the lobster hat's crustacean speech and the cardborg helmet's disguise with the cardborg suit
// (suits/costume.cs); the rest of the file is var-only. The var blocks are generated (D111).

// tg: code/modules/clothing/head/costume.dm:94
public partial class obj_item_clothing_head_costume_lobsterhat : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/costume.dm:101
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", strings("crustacean_replacement.json", "crustacean"))));
        return dot;
    }
}

// tg: code/modules/clothing/head/costume.dm:119
public partial class obj_item_clothing_head_costume_cardborg : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/costume.dm:131
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (ishuman(user) && Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
        {
            mob_living_carbon_human human_user = (mob_living_carbon_human)user;
            if (human_user.wear_suit is obj_item_clothing_suit_costume_cardborg)
            {
                obj_item_clothing_suit_costume_cardborg suit = (obj_item_clothing_suit_costume_cardborg)human_user.wear_suit;
                suit.disguise(human_user, this);
            }
        }
    }
}
