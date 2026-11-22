// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/crafting/combat_baking.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4a, D316: the anarchist's cookbook of the combat bakery kit). The var block is generated (D111).

// tg: code/game/objects/items/granters/crafting/combat_baking.dm:1
public partial class obj_item_book_granter_crafting_recipe_combat_baking : obj_item_book_granter_crafting_recipe
{
    // tg: code/game/objects/items/granters/crafting/combat_baking.dm:16
    public override async DmTask recoil(mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("The book dissolves into burnt flour!")));
        Dm.@new<obj_effect_decal_cleanable_ash>(get_turf(this));
        qdel(this);
    }
}
