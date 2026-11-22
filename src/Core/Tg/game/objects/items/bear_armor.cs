// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/bear_armor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package OD1, D333): the pile of bear armor straps onto a space bear for more health, armour penetration and
// claws. The var block is generated (D111).

// tg: code/game/objects/items/bear_armor.dm:2
public partial class obj_item_bear_armor : obj_item
{
    // tg: code/game/objects/items/bear_armor.dm:8
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is not mob_living_basic_bear bear)
            return NONE;
        if (Dm.truthy(bear.armored))
        {
            to_chat(user, span_warning(I18n.T("[] has already been armored up!", bear)));
            return ITEM_INTERACT_BLOCKING;
        }
        bear.armored = TRUE;
        bear.maxHealth += 60;
        bear.health += 60;
        bear.armour_penetration += 20;
        bear.melee_damage_lower += 3;
        bear.melee_damage_upper += 5;
        bear.wound_bonus += 5;
        await bear.update_icons();
        to_chat(user, span_info(I18n.T("You strap the armor plating to [] and sharpen [] claws with the nail filer. This was a great idea.", bear, bear.p_their())));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}
