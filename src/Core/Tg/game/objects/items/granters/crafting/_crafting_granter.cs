// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/crafting/_crafting_granter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4a, D316: the books that teach crafting recipes). The var blocks are generated (D111).

// tg: code/game/objects/items/granters/crafting/_crafting_granter.dm:1
public partial class obj_item_book_granter_crafting_recipe : obj_item_book_granter
{
    // tg: code/game/objects/items/granters/crafting/_crafting_granter.dm:5
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        await base.on_reading_finished(user);
        if (user.mind == null)
            return;
        foreach (object crafting_recipe_type in Dm.each(crafting_recipe_types))
        {
            user.mind.teach_crafting_recipe(crafting_recipe_type);
            datum_crafting_recipe recipe = (GLOB.cooking_recipes_by_typepath[crafting_recipe_type] ?? GLOB.crafting_recipes_by_typepath[crafting_recipe_type]) as datum_crafting_recipe;
            to_chat(user, span_notice(I18n.T("You learned how to make [].", recipe.name)));
        }
    }
}

// tg: code/game/objects/items/granters/crafting/_crafting_granter.dm:14
public partial class obj_item_book_granter_crafting_recipe_dusting : obj_item_book_granter_crafting_recipe
{
    // tg: code/game/objects/items/granters/crafting/_crafting_granter.dm:17
    public override async DmTask recoil(mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("The book turns to dust in your hands.")));
        qdel(this);
    }
}
