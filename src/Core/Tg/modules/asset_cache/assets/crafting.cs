// Translated from tgstation (snapshot 1d99bd7f): code/modules/asset_cache/assets/crafting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 8 (the crafting window, D148): a seam for tg's crafting sprite sheets (DV44: the asset transport is the client's).
// tg renders one 32x32 picture per recipe atom into the "crafting" and "cooking" sheets (create_spritesheets, :5 and :14) and
// the window shows it by its class "crafting32x32 a<id>"; the client draws the same picture from its own copy of tg's .dmi
// files, so the server sends the icon file and state that add_atom_icon picks (crafting_atom_icon, in each atom_data entry of
// the window). The tool pictures of add_tool_icons (:54-80) are a table of the client's window (PersonalCrafting.cs).

public static partial class GlobalProcs
{
    /**
     * Adds the ingredient icon to the spritesheet with given ID
     *
     * ingredient_typepath can be an obj typepath OR a reagent typepath
     *
     * If it a reagent, it will use the default container's icon state,
     * OR if it has a glass style associated, it will use that
     */
    // tg: code/modules/asset_cache/assets/crafting.dm:29  /datum/asset/spritesheet_batched/crafting/proc/add_atom_icon(ingredient_typepath, id):
    // the icon file and state it inserts as "a[id]" (insert_icon(..., uni_icon(icon_file, icon_state, SOUTH))).
    public static (object Icon, object IconState) crafting_atom_icon(Type ingredient_typepath)
    {
        object icon_file = null;
        object icon_state = null;
        Type preview_item = ingredient_typepath;
        if (ispath(ingredient_typepath, typeof(datum_reagent)))
        {
            Type reagent = ingredient_typepath;
            preview_item = Dm.initial(reagent, (datum_reagent r) => r.default_container) as Type;
            datum_glass_style style = (GLOB.glass_style_singletons[preview_item] as DmList)?[reagent] as datum_glass_style;
            if (style is datum_glass_style)
            {
                icon_file = style.icon;
                icon_state = style.icon_state;
            }
        }
        // Hardcoded for now, I suppose. Revisit later with bespoke "generic" icons for abstract types i.e. if(ingredient_typepath == ingredient_typepath::abstract_type) ?
        else if (ingredient_typepath == typeof(obj_item_food) || ingredient_typepath == typeof(obj_item_food_grown) || ingredient_typepath == typeof(obj_item_grown))
        {
            icon_file = Dm.resource("icons/effects/random_spawners.dmi");
            icon_state = "questionmark";
        }

        if (preview_item == null)
            return (icon_file, icon_state);
        if (!Dm.truthy(icon_file))
        {
            object preview = ispath(preview_item, typeof(obj)) ? Dm.initial(preview_item, (obj o) => o.icon_preview) : null;
            icon_file = Dm.truthy(preview) ? preview : Dm.initial(preview_item, (atom a) => a.icon);
        }
        if (!Dm.truthy(icon_state))
        {
            object preview = ispath(preview_item, typeof(obj)) ? Dm.initial(preview_item, (obj o) => o.icon_state_preview) : null;
            icon_state = Dm.truthy(preview) ? preview : Dm.initial(preview_item, (atom a) => a.icon_state);
        }

        // if(PERFORM_ALL_TESTS(focus_only/bad_cooking_crafting_icons)): FALSE in production (rules 1.7)
        return (icon_file, icon_state);
    }
}
