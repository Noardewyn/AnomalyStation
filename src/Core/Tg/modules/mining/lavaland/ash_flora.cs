// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/ash_flora.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package OD3, D333): Lavaland's and the ice moon's ash flora, picked by hand into their produce (the
// mushrooms' shavings, leaves, caps and stems, the cactus fruit with its seeds, seraka, the fire blossom, the glowgrowth, the
// chilly pepper, the ice moon's fonarstolbe, vahrezjia, podsneyzka, inozhakust and plasma flower, the drake beet), with their
// picked sprite, regrowth and light (the fire blossom and the plasma flower dim, the glowgrowth goes dark), the cactus's spines;
// the produce scattered where it drops and the shavings ground to mushroom powder. The seeds, the star cactus, the mushroom bowl
// and its recipe were live (hydroponics, gaps P2). Left: the raptors' food element (raptors are not ported) and the glowgrowth's
// emissive glow (DV12), not-translated.md section 220. The var blocks are generated (D111).

// tg: code/modules/mining/lavaland/ash_flora.dm:4
public partial class obj_structure_flora_ash : obj_structure_flora
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        base_icon_state = Dm.text("[][]", base_icon_state, rand(1, number_of_variants));
        icon_state = base_icon_state as string;
        return dot;
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:29
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_shavings), 1));
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:32
    public override async DmTask<object> harvest(object user = null, float? product_amount_multiplier = null)
    {
        if (!Dm.truthy(await base.harvest(user, product_amount_multiplier)))
            return FALSE;
        icon_state = Dm.text("[]p", base_icon_state);
        return TRUE;
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:38
    public override void regrow()
    {
        base.regrow();
        icon_state = base_icon_state as string;
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:45
public partial class obj_structure_flora_ash_leaf_shroom : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:60
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_mushroom_leaf), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:63
public partial class obj_structure_flora_ash_cap_shroom : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:78
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_mushroom_cap), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:81
public partial class obj_structure_flora_ash_stem_shroom : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:98
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_mushroom_stem), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:101
public partial class obj_structure_flora_ash_cacti : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:117
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_caltrop), Dm.named("min_damage", 3), Dm.named("max_damage", 6), Dm.named("probability", 70)));
        return dot;
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:121
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_cactus_fruit), 20), Dm.kv(typeof(obj_item_seeds_lavaland_cactus), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:124
public partial class obj_structure_flora_ash_seraka : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:142
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_seraka), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:145
public partial class obj_structure_flora_ash_fireblossom : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:163
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_fireblossom), 1));
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:166
    public override async DmTask after_harvest(object user = null)
    {
        // tg's own swap: the power gets the harvested range define and the range the harvested power define (kept, D86)
        set_light_power(LIGHT_RANGE_FIRE_BLOSSOM_HARVESTED);
        set_light_range(LIGHT_POWER_FIRE_BLOSSOM_HARVESTED);
        update_light();
        await base.after_harvest(user);
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:172
    public override void regrow()
    {
        set_light_power(Dm.initial(this, (atom p) => p.light_power));
        set_light_range(Dm.initial(this, (atom p) => p.light_range));
        update_light();
        base.regrow();
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:178
public partial class obj_structure_flora_ash_glowgrowth : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:197
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:201
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_glowgrowth), 1));
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:204
    public override async DmTask after_harvest(object user = null)
    {
        await set_light_on(FALSE);
        update_light();
        update_appearance();
        await base.after_harvest(user);
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:210
    public override void regrow()
    {
        Waiting.NoSleep(set_light_on(TRUE));
        update_light();
        update_appearance();
        base.regrow();
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:216
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(harvested))
            Dm.Deferred("code/modules/mining/lavaland/ash_flora.dm:219", "update_overlays: . += emissive_appearance(icon, \"[icon_state]e\", src, alpha = 120), the glowgrowth's glow on the emissive plane (DEVIATION DV12)");
        return dot;
    }
}

///Snow flora to exist on icebox.
// tg: code/modules/mining/lavaland/ash_flora.dm:222
public partial class obj_structure_flora_ash_chilly : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:240
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_icepepper), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:243
public partial class obj_structure_flora_ash_fonarstolbe : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:260
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_fonarstolbe), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:263
public partial class obj_structure_flora_ash_vahrezjia : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:278
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_vahrezjia), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:281
public partial class obj_structure_flora_ash_podsneyzka : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:296
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_podsneyzka), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:299
public partial class obj_structure_flora_ash_inozhakust : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:314
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_inozhakust), 1));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:317
public partial class obj_structure_flora_ash_plasma_flower : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:333
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_ash_flora_plasma_flower), 1));
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:336
    public override async DmTask after_harvest(object user = null)
    {
        // tg's own swap of the two harvested defines, as the fire blossom's (kept, D86)
        set_light_power(LIGHT_RANGE_FIRE_BLOSSOM_HARVESTED);
        set_light_range(LIGHT_POWER_FIRE_BLOSSOM_HARVESTED);
        update_light();
        await base.after_harvest(user);
    }

    // tg: code/modules/mining/lavaland/ash_flora.dm:342
    public override void regrow()
    {
        set_light_power(Dm.initial(this, (atom p) => p.light_power));
        set_light_range(Dm.initial(this, (atom p) => p.light_range));
        update_light();
        base.regrow();
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:348
public partial class obj_structure_flora_ash_drake_beet : obj_structure_flora_ash
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:361
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_drake_beet), 1));
    }
}

//SNACKS

// tg: code/modules/mining/lavaland/ash_flora.dm:366
public partial class obj_item_food_grown_ash_flora : obj_item_food_grown
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:373
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = Dm.num(base_pixel_x) + rand(-4, 4);
        pixel_y = Dm.num(base_pixel_y) + rand(-4, 4);
        return dot;
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:378
public partial class obj_item_food_grown_ash_flora_shavings : obj_item_food_grown_ash_flora
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:385
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_toxin_mushroom_powder), 5));
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:418
public partial class obj_item_food_grown_ash_flora_cactus_fruit : obj_item_food_grown_ash_flora
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:427
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/mining/lavaland/ash_flora.dm:429", "Initialize: AddElement(/datum/element/raptor_food, growth_modifier = 0.1, ability_modifier = -0.05), the raptors' taste for the fruit (the raptors are not ported)");
        return dot;
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:431
public partial class obj_item_food_grown_ash_flora_seraka : obj_item_food_grown_ash_flora
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:440
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/mining/lavaland/ash_flora.dm:442", "Initialize: AddElement(/datum/element/raptor_food, ability_modifier = 0.1), the raptors' taste for seraka (the raptors are not ported)");
        return dot;
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:444
public partial class obj_item_food_grown_ash_flora_fireblossom : obj_item_food_grown_ash_flora
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:454
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Fire flowers make fireproof raptors
        Dm.Deferred("code/modules/mining/lavaland/ash_flora.dm:457", "Initialize: AddElement(/datum/element/raptor_food, color_chances = string_list(list(/datum/raptor_color/blue = 5))), the fireproof raptors (the raptors are not ported)");
        return dot;
    }
}

// tg: code/modules/mining/lavaland/ash_flora.dm:494
public partial class obj_item_food_grown_ash_flora_plasma_flower : obj_item_food_grown_ash_flora
{
    // tg: code/modules/mining/lavaland/ash_flora.dm:503
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Fire flowers make fireproof raptors
        Dm.Deferred("code/modules/mining/lavaland/ash_flora.dm:506", "Initialize: AddElement(/datum/element/raptor_food, color_chances = string_list(list(/datum/raptor_color/blue = 5))), the fireproof raptors (the raptors are not ported)");
        return dot;
    }
}
