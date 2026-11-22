// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/stack_recipe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var blocks are generated (D111) but for category, declared here
// as an object: it holds a CAT_* text (tg's default is null).

/*
 * Recipe datum
 */
// tg: code/game/objects/items/stacks/stack_recipe.dm:5
public partial class datum_stack_recipe : datum
{
    /// Category for general crafting menu
    // tg: code/game/objects/items/stacks/stack_recipe.dm:27
    public object category;

    // tg: code/game/objects/items/stacks/stack_recipe.dm:34
    [DmParams("title", "result_type", "req_amount", "res_amount", "max_res_amount", "time", "crafting_flags", "placement_checks", "trait_booster", "trait_modifier", "category", "removed_mats")]
    public override async DmTask New(params object[] __extra)
    {
        object title = Dm.arg(__extra, 0);
        Type result_type = Dm.arg<Type>(__extra, 1);
        float? req_amount = Dm.arg<float?>(__extra, 2);
        float? res_amount = Dm.arg<float?>(__extra, 3);
        float? max_res_amount = Dm.arg<float?>(__extra, 4);
        float? time = Dm.arg<float?>(__extra, 5);
        float? crafting_flags = Dm.arg<float?>(__extra, 6);
        float? placement_checks = Dm.arg<float?>(__extra, 7);
        object trait_booster = Dm.arg(__extra, 8);
        float? trait_modifier = Dm.arg<float?>(__extra, 9);
        object category = Dm.arg(__extra, 10);
        DmList removed_mats = Dm.arg<DmList>(__extra, 11);
        req_amount ??= 1;
        res_amount ??= 1;
        max_res_amount ??= 1;
        time ??= 0 * SECONDS;
        crafting_flags ??= CRAFT_CHECK_DENSITY;
        placement_checks ??= NONE;
        trait_modifier ??= 1;

        this.title = (string)title;
        this.result_type = result_type;
        this.req_amount = Dm.num(req_amount);
        this.res_amount = Dm.num(res_amount);
        this.max_res_amount = Dm.num(max_res_amount);
        this.time = time;
        this.crafting_flags = Dm.num(crafting_flags);
        this.placement_checks = Dm.num(placement_checks);
        this.trait_booster = trait_booster;
        this.trait_modifier = Dm.num(trait_modifier);
        object or__1 = this.category;
        object or__2 = category;
        this.category = Dm.truthy(or__1) ? or__1 : Dm.truthy(or__2) ? or__2 : CAT_MISC;

        this.removed_mats = removed_mats;

        // We create base64 image only if item have color. Otherwise use icon_ref for TGUI
        object paint = result_type == null ? null : Dm.initial(result_type, (atom a) => a.color);
        if (paint != null && !Dm.eq(paint, COLOR_WHITE))
        {
            // var/icon/result_icon = icon(result::icon, result::icon_state, SOUTH, 1); Scale(32, 32); Blend(paint, ICON_MULTIPLY);
            // src.result_image = "[icon2base64(result_icon)]" (:67-70): the server renders no icons (D104), so the StackCrafting
            // window draws the result's own icon and state (build_recipe's icon, icon_state) without the tint.
            Dm.Deferred("code/game/objects/items/stacks/stack_recipe.dm:67", "New: result_image, the base64 picture of a coloured result (the server renders no icons, D104; the window shows the untinted icon)");
        }
    }
}

// tg: code/game/objects/items/stacks/stack_recipe.dm:72
public partial class datum_stack_recipe_radial : datum_stack_recipe
{
    // tg: code/game/objects/items/stacks/stack_recipe.dm:76
    [DmParams("title", "result_type", "req_amount", "res_amount", "max_res_amount", "time", "crafting_flags", "placement_checks", "trait_booster", "trait_modifier", "category", "desc", "required_noun")]
    public override async DmTask New(params object[] __extra)
    {
        object title = Dm.arg(__extra, 0);
        Type result_type = Dm.arg<Type>(__extra, 1);
        float? req_amount = Dm.arg<float?>(__extra, 2);
        float? res_amount = Dm.arg<float?>(__extra, 3);
        float? max_res_amount = Dm.arg<float?>(__extra, 4);
        float? time = Dm.arg<float?>(__extra, 5);
        float? crafting_flags = Dm.arg<float?>(__extra, 6);
        float? placement_checks = Dm.arg<float?>(__extra, 7);
        object trait_booster = Dm.arg(__extra, 8);
        float? trait_modifier = Dm.arg<float?>(__extra, 9);
        object category = Dm.arg(__extra, 10);
        object desc = Dm.arg(__extra, 11);
        object required_noun = Dm.arg(__extra, 12);
        req_amount ??= 1;
        res_amount ??= 1;
        max_res_amount ??= 1;
        time ??= 0 * SECONDS;
        crafting_flags ??= CRAFT_CHECK_DENSITY;
        placement_checks ??= NONE;
        trait_modifier ??= 1;
        if (Dm.truthy(category))
            this.category = category;
        if (Dm.truthy(desc))
            this.desc = desc;
        if (Dm.truthy(required_noun))
            this.desc = Dm.add(this.desc, span_boldnotice(I18n.T("[]Requires: [] []\\s.", Dm.truthy(desc) ? " - " : "", req_amount, required_noun)));

        // return ..(): the parent's New with the current values of all this definition's arguments (rules 7.5), so desc lands in
        // the parent's removed_mats position as in tg (a radial recipe has no removed materials).
        await base.New(title, result_type, req_amount, res_amount, max_res_amount, time, crafting_flags, placement_checks, trait_booster, trait_modifier, category, desc, required_noun);
    }
}


/*
 * Recipe list datum
 */
// tg: code/game/objects/items/stacks/stack_recipe.dm:104
public partial class datum_stack_recipe_list : datum
{
    // tg: code/game/objects/items/stacks/stack_recipe.dm:108
    [DmParams("title", "recipes")]
    public override async DmTask New(params object[] __extra)
    {
        object title = Dm.arg(__extra, 0);
        DmList recipes = Dm.arg<DmList>(__extra, 1);
        this.title = (string)title;
        this.recipes = recipes;
    }
}
