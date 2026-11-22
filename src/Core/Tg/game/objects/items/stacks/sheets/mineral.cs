// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/sheets/mineral.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of a type that the signatures of package B's turf and area files name (rules 2.3: the earliest .dme file that declares it), written by package B (wave 4); only the type is declared, and the file is translated with its module.
// M7 slice 2 (the PACMAN burns plasma sheets, uranium for its super type): uranium and plasma are translated (their recipe
// lists and the plasma's suicide); gaps DE1: sandstone (its recipe list; the brick a pillow takes); gaps SK1 (D333): the rest of
// the file: the diamond, gold, silver, bananium, titanium (with the shuttle frame rods), snow, adamantine, runite, mythril, alien
// alloy, metal hydrogen and zaukerite recipe lists, the snow's storm hating and the coal (ground into carbon, lit by a hot tool
// into a puff of carbon dioxide).

// tg: code/game/objects/items/stacks/sheets/mineral.dm:33
[DmPath("/obj/item/stack/sheet/mineral")]
public partial class obj_item_stack_sheet_mineral : obj_item_stack_sheet
{
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:300
[DmPath("/obj/item/stack/sheet/mineral/titanium")]
public partial class obj_item_stack_sheet_mineral_titanium : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:325
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.titanium_recipes);
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:329
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (tool is not obj_item_stack_rods old_rods)
            return await base.item_interaction(user, tool, modifiers, __extra);
        if (!Dm.eq(old_rods.merge_type, typeof(obj_item_stack_rods)))
        {
            to_chat(user, span_warning(I18n.T("You can't craft shuttle frame rods with this type of rod!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.num(old_rods.get_amount()) < 5 && Dm.num(get_amount()) < 1)
        {
            to_chat(user, span_warning(I18n.T("You need five rods and one sheet of titanium to make shuttle frame rods!")));
            return ITEM_INTERACT_BLOCKING;
        }

        var new_rods = Dm.@new<obj_item_stack_rods_shuttle_five>(get_turf(user));
        if (!QDELETED(new_rods))
            new_rods.add_fingerprint(user);
        bool replace = ReferenceEquals(user.get_inactive_held_item(), this);
        await old_rods.use(5);
        await use(1);
        if (QDELETED(this) && replace && !QDELETED(new_rods))
            await user.put_in_hands(new_rods);
        return ITEM_INTERACT_SUCCESS;
    }
}

/*
 * Sandstone
 */
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:26  GLOBAL_LIST_INIT(sandstone_recipes, list(...))
    public DmList sandstone_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:26  (GLOBAL_MANAGED(sandstone_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsandstone_recipes()
    {
        sandstone_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("sandstone tile", typeof(obj_item_stack_tile_mineral_sandstone), 1, 4, 20, Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("sandstone door", typeof(obj_structure_mineral_door_sandstone), 10, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("sandstone platform", typeof(obj_structure_platform_sandstone), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("Breakdown into sand", typeof(obj_item_stack_ore_glass), 1, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_NO_MATERIALS), Dm.named("category", CAT_MISC))
        );
        Dm.append(ref gvars_datum_init_order, "sandstone_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:37  (gaps DE1, D333: the brick a pillow takes)
public partial class obj_item_stack_sheet_mineral_sandstone : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:53
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.sandstone_recipes);
        return dot;
    }
}

/*
 * Uranium
 */
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:149  GLOBAL_LIST_INIT(uranium_recipes, list(...))
    public DmList uranium_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:149  (GLOBAL_MANAGED(uranium_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaluranium_recipes()
    {
        uranium_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("uranium door", typeof(obj_structure_mineral_door_uranium), 10, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("depleted uranium platform", typeof(obj_structure_platform_uranium), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("uranium tile", typeof(obj_item_stack_tile_mineral_uranium), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES))
        );
        Dm.append(ref gvars_datum_init_order, "uranium_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:137
public partial class obj_item_stack_sheet_mineral_uranium : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:155
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.uranium_recipes);
        return dot;
    }
}

/*
 * Plasma
 */
// tg: code/game/objects/items/stacks/sheets/mineral.dm:172
public partial class obj_item_stack_sheet_mineral_plasma : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:186
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins licking \\the []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        return TOXLOSS;//dont you kids know that stuff is toxic?
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:195
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.plasma_recipes);
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:190  GLOBAL_LIST_INIT(plasma_recipes, list(...))
    public DmList plasma_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:190  (GLOBAL_MANAGED(plasma_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplasma_recipes()
    {
        plasma_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("plasma door", typeof(obj_structure_mineral_door_transparent_plasma), 10, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("plasma tile", typeof(obj_item_stack_tile_mineral_plasma), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES))
        );
        Dm.append(ref gvars_datum_init_order, "plasma_recipes");
    }
}

/*
 * Plastitanium
 */
// Gaps G2 (D291): the plastitanium sheet turns live (a plastitanium wall's deconstruction drops two of them; an inert stack would
// keep its default amount, its Initialize not running).
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:377  GLOBAL_LIST_INIT(plastitanium_recipes, list(...))
    public DmList plastitanium_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:377  (GLOBAL_MANAGED(plastitanium_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplastitanium_recipes()
    {
        plastitanium_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("plastitanium tile", typeof(obj_item_stack_tile_mineral_plastitanium), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("plastitanium wall filler", typeof(obj_item_stack_wall_filling_plastitanium_basic), 2, 1, 10, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("plastitanium platform", typeof(obj_structure_platform_plastitanium), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE))
        );
        Dm.append(ref gvars_datum_init_order, "plastitanium_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:358
public partial class obj_item_stack_sheet_mineral_plastitanium : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:383
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.plastitanium_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:65  (gaps J5, D298: the sandbags of the engineering lockers)
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:73  GLOBAL_LIST_INIT(sandbag_recipes, list(...))
    public DmList sandbag_recipes;

    public virtual void InitGlobalsandbag_recipes()
    {
        sandbag_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("sandbags", typeof(obj_structure_barricade_sandbags), 1, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)));
        Dm.append(ref gvars_datum_init_order, "sandbag_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:65
public partial class obj_item_stack_sheet_mineral_sandbags : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:77
    public override object get_main_recipes()
    {
        object dot = base.get_main_recipes();
        return Dm.add(dot, GLOB.sandbag_recipes);
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:84
public partial class obj_item_emptysandbag : obj_item
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:91
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack_ore_glass)
            return NONE;
        obj_item_stack_ore_glass sand = tool as obj_item_stack_ore_glass;
        to_chat(user, span_notice(I18n.T("You fill the sandbag.")));
        obj_item_stack_sheet_mineral_sandbags filled_bag = Dm.@new<obj_item_stack_sheet_mineral_sandbags>(drop_location());
        qdel(this);
        if (Dm.truthy(await Adjacent(user)) && !issilicon(user))
            await user.put_in_hands(filled_bag);
        await sand.use(1);
        return ITEM_INTERACT_SUCCESS;
    }
}

// Gaps SK1 (D333): the recipe lists of the other minerals.
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:118  GLOBAL_LIST_INIT(diamond_recipes, list(...))
    public DmList diamond_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:118
    public virtual void InitGlobaldiamond_recipes()
    {
        diamond_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("diamond door", typeof(obj_structure_mineral_door_transparent_diamond), 10, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("diamond tile", typeof(obj_item_stack_tile_mineral_diamond), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "diamond_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:224  GLOBAL_LIST_INIT(gold_recipes, list(...))
    public DmList gold_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:224
    public virtual void InitGlobalgold_recipes()
    {
        gold_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("golden door", typeof(obj_structure_mineral_door_gold), 10, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("golden platform", typeof(obj_structure_platform_gold), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("gold tile", typeof(obj_item_stack_tile_mineral_gold), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("blank plaque", typeof(obj_item_plaque), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("Simple Crown", typeof(obj_item_clothing_head_costume_crown), 5, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)));
        Dm.append(ref gvars_datum_init_order, "gold_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:256  GLOBAL_LIST_INIT(silver_recipes, list(...))
    public DmList silver_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:256
    public virtual void InitGlobalsilver_recipes()
    {
        silver_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("silver door", typeof(obj_structure_mineral_door_silver), 10, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("silver platform", typeof(obj_structure_platform_silver), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("silver tile", typeof(obj_item_stack_tile_mineral_silver), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "silver_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:286  GLOBAL_LIST_INIT(bananium_recipes, list(...))
    public DmList bananium_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:286
    public virtual void InitGlobalbananium_recipes()
    {
        bananium_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("bananium tile", typeof(obj_item_stack_tile_mineral_bananium), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "bananium_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:318  GLOBAL_LIST_INIT(titanium_recipes, list(...))
    public DmList titanium_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:318
    public virtual void InitGlobaltitanium_recipes()
    {
        titanium_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("Titanium tile", typeof(obj_item_stack_tile_mineral_titanium), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("Titanium Platform", typeof(obj_structure_platform_titanium), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("Shuttle seat", typeof(obj_structure_chair_comfy_shuttle), 2, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("Tram door assembly", typeof(obj_structure_door_assembly_multi_tile_door_assembly_tram), 8, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)));
        Dm.append(ref gvars_datum_init_order, "titanium_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:407  GLOBAL_LIST_INIT(snow_recipes, list(...))
    public DmList snow_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:407
    public virtual void InitGlobalsnow_recipes()
    {
        snow_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("snow wall", typeof(turf_closed_wall_mineral_snow), 5, Dm.named("time", 4 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("snowman", typeof(obj_structure_statue_snow_snowman), 5, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("snowball", typeof(obj_item_toy_snowball), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_WEAPON_RANGED)),
            Dm.@new<datum_stack_recipe>("snow tile", typeof(obj_item_stack_tile_mineral_snow), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "snow_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:428  GLOBAL_LIST_INIT(adamantine_recipes, list(...))
    public DmList adamantine_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:428
    public virtual void InitGlobaladamantine_recipes()
    {
        adamantine_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("adamantine tile", typeof(obj_item_stack_tile_mineral_adamantine), 1, 4, 20, Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("incomplete servant golem shell", typeof(obj_item_golem_shell_servant), Dm.named("req_amount", 3), Dm.named("res_amount", 1), Dm.named("category", CAT_ROBOT)));
        Dm.append(ref gvars_datum_init_order, "adamantine_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:449  GLOBAL_LIST_INIT(runite_recipes, list(...))
    public DmList runite_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:449
    public virtual void InitGlobalrunite_recipes()
    {
        runite_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("runite tile", typeof(obj_item_stack_tile_mineral_runite), 1, 4, 20, Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "runite_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:471  GLOBAL_LIST_INIT(mythril_recipes, list(...))
    public DmList mythril_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:471
    public virtual void InitGlobalmythril_recipes()
    {
        mythril_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("mythril tile", typeof(obj_item_stack_tile_mineral_mythril), 1, 4, 20, Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "mythril_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:505  GLOBAL_LIST_INIT(abductor_recipes, list(...))
    public DmList abductor_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:505
    public virtual void InitGlobalabductor_recipes()
    {
        abductor_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("alien bed", typeof(obj_structure_bed_abductor), 2, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("alien locker", typeof(obj_structure_closet_abductor), 2, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("strange closet", typeof(obj_structure_closet_acloset), 2, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("alien table frame", typeof(obj_structure_table_frame_abductor), 1, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("alien airlock assembly", typeof(obj_structure_door_assembly_door_assembly_abductor), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            null,
            Dm.@new<datum_stack_recipe>("alien floor tile", typeof(obj_item_stack_tile_mineral_abductor), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "abductor_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:557  GLOBAL_LIST_INIT(metalhydrogen_recipes, list(...))
    public DmList metalhydrogen_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:557
    public virtual void InitGlobalmetalhydrogen_recipes()
    {
        metalhydrogen_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("metallic hydrogen tile", typeof(obj_item_stack_tile_mineral_metal_hydrogen), 1, 4, 20, Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("incomplete servant golem shell", typeof(obj_item_golem_shell_servant), Dm.named("req_amount", 20), Dm.named("res_amount", 1), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ROBOT)),
            Dm.@new<datum_stack_recipe>("ancient armor", typeof(obj_item_clothing_suit_armor_elder_atmosian), Dm.named("req_amount", 5), Dm.named("res_amount", 1), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("ancient helmet", typeof(obj_item_clothing_head_helmet_elder_atmosian), Dm.named("req_amount", 3), Dm.named("res_amount", 1), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("metallic hydrogen axe", typeof(obj_item_fireaxe_metal_h2_axe), Dm.named("req_amount", 15), Dm.named("res_amount", 1), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_WEAPON_MELEE)),
            Dm.@new<datum_stack_recipe>("metallic hydrogen bolts", typeof(obj_item_ammo_casing_rebar_hydrogen), Dm.named("req_amount", 1), Dm.named("res_amount", 1), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_WEAPON_AMMO)));
        Dm.append(ref gvars_datum_init_order, "metalhydrogen_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:594  GLOBAL_LIST_INIT(zaukerite_recipes, list(...))
    public DmList zaukerite_recipes;

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:594
    public virtual void InitGlobalzaukerite_recipes()
    {
        zaukerite_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("zaukerite shard", typeof(obj_item_ammo_casing_rebar_zaukerite), Dm.named("req_amount", 1), Dm.named("res_amount", 1), Dm.named("category", CAT_WEAPON_AMMO)));
        Dm.append(ref gvars_datum_init_order, "zaukerite_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:105
public partial class obj_item_stack_sheet_mineral_diamond : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:123
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.diamond_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:210
public partial class obj_item_stack_sheet_mineral_gold : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:232
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.gold_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:243
public partial class obj_item_stack_sheet_mineral_silver : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:262
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.silver_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:273
public partial class obj_item_stack_sheet_mineral_bananium : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:290
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.bananium_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:433
public partial class obj_item_stack_sheet_mineral_adamantine : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:442
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.adamantine_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:454
public partial class obj_item_stack_sheet_mineral_runite : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:464
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.runite_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:476
public partial class obj_item_stack_sheet_mineral_mythril : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:485
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.mythril_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:492
public partial class obj_item_stack_sheet_mineral_abductor : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:515
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.abductor_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:565
public partial class obj_item_stack_sheet_mineral_metal_hydrogen : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:580
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.metalhydrogen_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:598
public partial class obj_item_stack_sheet_mineral_zaukerite : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:610
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.zaukerite_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/mineral.dm:393
public partial class obj_item_stack_sheet_mineral_snow : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:414
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_storm_hating)));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:418
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.snow_recipes);
        return dot;
    }
}

/*
 * Coal
 */
// tg: code/game/objects/items/stacks/sheets/mineral.dm:521
public partial class obj_item_stack_sheet_mineral_coal : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/mineral.dm:531
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_carbon), 20));
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:534
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.num(tool.get_temperature()) < FIRE_MINIMUM_TEMPERATURE_TO_EXIST) //If the temperature of the object is hot enough to start a fire, then ignite
            return await base.item_interaction(user, tool, modifiers, __extra);
        turf jumpturf = get_turf(this);
        message_admins(Dm.text("Coal ignited by [] in []", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(jumpturf)));
        user.log_message("ignited coal", LOG_GAME);
        await fire_act(tool.get_temperature());
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/sheets/mineral.dm:544
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        atmos_spawn_air(Dm.text("[]=[];[]", GAS_CO2, Dm.num(amount) * 10, TURF_TEMPERATURE(exposed_temperature)));
        qdel(this);
        return null;
    }
}
