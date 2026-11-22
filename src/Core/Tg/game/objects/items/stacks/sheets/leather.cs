// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/sheets/leather.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SK1 (D333): the file whole. The hides and their recipes (the human, mothroach, gondola, corgi, monkey, lizard, alien, carp,
// polar bear and bear ones), the carbon hides' skin colour (a lizard's hide keeps the lizard's colour and only merges with its own
// colour), leather and its recipes (the wallet, the belts, the cowboy hats, the sheaths), the watcher sinew and its slapcrafting,
// the ash drake hide, and the tanning: a sharp tool shaves a hide into hairless hide, washing makes it wet hide, and heat (a hot
// room, a drying rack, a microwave, a grill or an oven) dries it into leather. tg's /obj/item/stack/sheet/animalhide/carbon
// can_merge returns null for a carbon hide that does not use its skin colour (the human and monkey hides), so those never merge:
// translated as written. The var blocks are generated (D111).

// tg: code/game/objects/items/stacks/sheets/leather.dm:1
[DmPath("/obj/item/stack/sheet/animalhide")]
public partial class obj_item_stack_sheet_animalhide : obj_item_stack_sheet
{
    //Step one - dehairing.
    // tg: code/game/objects/items/stacks/sheets/leather.dm:438
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(tool.get_sharpness()))
            return await base.item_interaction(user, tool, modifiers, __extra);
        playsound(loc, Dm.resource("sound/items/weapons/slice.ogg"), 50, TRUE, -1);
        user.visible_message(span_notice(I18n.T("[] starts cutting hair off \\the [].", user, this)), span_notice(I18n.T("You start cutting the hair off \\the []...", this)), span_hear(I18n.T("You hear the sound of a knife rubbing against flesh.")));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You cut the hair from [].", name)));
        Dm.@new<obj_item_stack_sheet_hairlesshide>(user.drop_location(), amount);
        await use(amount);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:450
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You can remove the hair with any sharp object.")));
        return dot;
    }
}

/// Subtype of skin to be dropped by carbon mobs as a result of being butchered, potentially inheriting their body color
// tg: code/game/objects/items/stacks/sheets/leather.dm:14
[DmPath("/obj/item/stack/sheet/animalhide/carbon")]
public partial class obj_item_stack_sheet_animalhide_carbon : obj_item_stack_sheet_animalhide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:21
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt", "new_skin_color")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_skin_color = Dm.arg(__extra, 4);
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(skin_color))
            Waiting.NoSleep(set_skin_color(Dm.truthy(new_skin_color) ? new_skin_color : get_random_skin_color()));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:26
    public override object can_merge(obj_item_stack check = null, float? inhand = null)
    {
        object dot = base.can_merge(check, inhand);
        if (!Dm.truthy(dot) || !Dm.truthy(uses_skin_color))
            return null;
        return Dm.eq((check as obj_item_stack_sheet_animalhide_carbon)?.skin_color, skin_color) ? TRUE : FALSE; // segregation, in my human butcher shop? how queer!
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:32
    public virtual async DmTask set_skin_color(object new_skin_color = null)
    {
        skin_color = new_skin_color;
        if (Dm.truthy(skin_color) && Dm.truthy(uses_skin_color))
            await add_atom_colour(skin_color, FIXED_COLOUR_PRIORITY);
        else
            await remove_atom_colour(FIXED_COLOUR_PRIORITY);
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:39
    public override async DmTask<object> split_stack(object amount = null)
    {
        var new_stack = await base.split_stack(amount) as obj_item_stack_sheet_animalhide_carbon;
        if (new_stack == null)
            return null;
        await new_stack.set_skin_color(skin_color);
        return new_stack;
    }

    /// Select a random skin color to spawn
    // tg: code/game/objects/items/stacks/sheets/leather.dm:47
    public virtual object get_random_skin_color()
    {
        return null;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:57  GLOBAL_LIST_INIT(human_recipes, list(...))
    public DmList human_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:57
    public virtual void InitGlobalhuman_recipes()
    {
        human_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("human skin carpet", typeof(obj_item_stack_tile_carpet_human), 1, 4, 20, Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("bloated human costume", typeof(obj_item_clothing_suit_hooded_bloated_human), 5, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("human skin hat", typeof(obj_item_clothing_head_fedora_human_leather), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)));
        Dm.append(ref gvars_datum_init_order, "human_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:99  GLOBAL_LIST_INIT(mothroach_recipes, list(...))
    public DmList mothroach_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:99
    public virtual void InitGlobalmothroach_recipes()
    {
        mothroach_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("moth fur carpet", typeof(obj_item_stack_tile_carpet_moth), 1, 4, 20, Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "mothroach_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:107  GLOBAL_LIST_INIT(gondola_recipes, list(...))
    public DmList gondola_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:107
    public virtual void InitGlobalgondola_recipes()
    {
        gondola_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("gondola mask", typeof(obj_item_clothing_mask_gondola), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("gondola suit", typeof(obj_item_clothing_under_costume_gondola), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("gondola bedsheet", typeof(obj_item_bedsheet_gondola), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)));
        Dm.append(ref gvars_datum_init_order, "gondola_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:125  GLOBAL_LIST_INIT(corgi_recipes, list(...))
    public DmList corgi_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:125
    public virtual void InitGlobalcorgi_recipes()
    {
        corgi_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("corgi costume", typeof(obj_item_clothing_suit_hooded_ian_costume), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)));
        Dm.append(ref gvars_datum_init_order, "corgi_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:152  GLOBAL_LIST_INIT(monkey_recipes, list(...))
    public DmList monkey_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:152
    public virtual void InitGlobalmonkey_recipes()
    {
        monkey_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("monkey mask", typeof(obj_item_clothing_mask_gas_monkeymask), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("monkey suit", typeof(obj_item_clothing_suit_costume_monkeysuit), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)));
        Dm.append(ref gvars_datum_init_order, "monkey_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:179  GLOBAL_LIST_INIT(lizard_recipes, list(...))
    public DmList lizard_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:179
    public virtual void InitGloballizard_recipes()
    {
        lizard_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("lizard scale carpet", typeof(obj_item_stack_tile_carpet_lizard), 1, 4, 20, Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "lizard_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:195  GLOBAL_LIST_INIT(xeno_recipes, list(...))
    public DmList xeno_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:195
    public virtual void InitGlobalxeno_recipes()
    {
        xeno_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("alien helmet", typeof(obj_item_clothing_head_costume_xenos), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("alien suit", typeof(obj_item_clothing_suit_costume_xenos), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)));
        Dm.append(ref gvars_datum_init_order, "xeno_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:220  GLOBAL_LIST_INIT(carp_recipes, list(...))
    public DmList carp_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:220
    public virtual void InitGlobalcarp_recipes()
    {
        carp_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("carp scale carpet", typeof(obj_item_stack_tile_carpet_carp), 1, 4, 20, Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("carp costume", typeof(obj_item_clothing_suit_hooded_carp_costume), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("carp mask", typeof(obj_item_clothing_mask_gas_carp), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("carpskin chair", typeof(obj_structure_chair_comfy_carp), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("carpskin suit", typeof(obj_item_clothing_under_suit_carpskin), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("carpskin fedora", typeof(obj_item_clothing_head_fedora_carpskin), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("carpskin fishing bag", typeof(obj_item_storage_bag_fishing_carpskin), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)));
        Dm.append(ref gvars_datum_init_order, "carp_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:257  GLOBAL_LIST_INIT(leather_recipes, list(...))
    public DmList leather_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:257
    public virtual void InitGloballeather_recipes()
    {
        leather_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("wallet", typeof(obj_item_storage_wallet), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("muzzle", typeof(obj_item_clothing_mask_muzzle), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("basketball", typeof(obj_item_toy_basketball), 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("baseball", typeof(obj_item_toy_beach_ball_baseball), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("saddle", typeof(obj_item_goliath_saddle), 5, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_EQUIPMENT)),
            Dm.@new<datum_stack_recipe>("leather shoes", typeof(obj_item_clothing_shoes_laceup), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("cowboy boots", typeof(obj_item_clothing_shoes_cowboy), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("botany gloves", typeof(obj_item_clothing_gloves_botanic_leather), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("leather satchel", typeof(obj_item_storage_backpack_satchel_leather), 5, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("sheriff vest", typeof(obj_item_clothing_accessory_vest_sheriff), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("leather jacket", typeof(obj_item_clothing_suit_jacket_leather), 7, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("biker jacket", typeof(obj_item_clothing_suit_jacket_leather_biker), 7, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe_list>("belts", Dm.list(
                Dm.@new<datum_stack_recipe>("tool belt", typeof(obj_item_storage_belt_utility), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("botanical belt", typeof(obj_item_storage_belt_plant), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("janitorial belt", typeof(obj_item_storage_belt_janitor), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("medical belt", typeof(obj_item_storage_belt_medical), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("security belt", typeof(obj_item_storage_belt_security), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("shoulder holster", typeof(obj_item_storage_belt_holster), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("bandolier", typeof(obj_item_storage_belt_bandolier), 5, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)))),
            Dm.@new<datum_stack_recipe_list>("cowboy hats", Dm.list(
                Dm.@new<datum_stack_recipe>("sheriff hat", typeof(obj_item_clothing_head_cowboy_brown), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
                Dm.@new<datum_stack_recipe>("desperado hat", typeof(obj_item_clothing_head_cowboy_black), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
                Dm.@new<datum_stack_recipe>("ten-gallon hat", typeof(obj_item_clothing_head_cowboy_white), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
                Dm.@new<datum_stack_recipe>("deputy hat", typeof(obj_item_clothing_head_cowboy_red), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
                Dm.@new<datum_stack_recipe>("drifter hat", typeof(obj_item_clothing_head_cowboy_grey), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)))),
            Dm.@new<datum_stack_recipe_list>("sword sheaths", Dm.list(
                Dm.@new<datum_stack_recipe>("katana sheath", typeof(obj_item_storage_belt_sheath_katana_empty), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
                Dm.@new<datum_stack_recipe>("hanzo katana sheath", typeof(obj_item_storage_belt_sheath_hanzo_katana_empty), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
                Dm.@new<datum_stack_recipe>("toy katana sheath", typeof(obj_item_storage_belt_sheath_katana_toy_empty), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)))));
        Dm.append(ref gvars_datum_init_order, "leather_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:358  GLOBAL_LIST_INIT(sinew_recipes, list(...))
    public DmList sinew_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:358
    public virtual void InitGlobalsinew_recipes()
    {
        sinew_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("sinew restraints", typeof(obj_item_restraints_handcuffs_cable_sinew), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_EQUIPMENT)));
        Dm.append(ref gvars_datum_init_order, "sinew_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:388  GLOBAL_LIST_INIT(polar_bear_recipes, list(...))
    public DmList polar_bear_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:388
    public virtual void InitGlobalpolar_bear_recipes()
    {
        polar_bear_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("polar bear fur carpet", typeof(obj_item_stack_tile_carpet_polar_bear), 1, 4, 20, Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "polar_bear_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:427  GLOBAL_LIST_INIT(bear_pelt_recipes, list(...))
    public DmList bear_pelt_recipes;

    // tg: code/game/objects/items/stacks/sheets/leather.dm:427
    public virtual void InitGlobalbear_pelt_recipes()
    {
        bear_pelt_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>(" bear fur carpet", typeof(obj_item_stack_tile_carpet_bear), 1, 4, 20, Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("bear costume", typeof(obj_item_clothing_suit_costume_bear_suit), 5, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("bear hat", typeof(obj_item_clothing_head_costume_bearpelt), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)));
        Dm.append(ref gvars_datum_init_order, "bear_pelt_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:50
[DmPath("/obj/item/stack/sheet/animalhide/carbon/human")]
public partial class obj_item_stack_sheet_animalhide_carbon_human : obj_item_stack_sheet_animalhide_carbon
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:63
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.human_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:88
public partial class obj_item_stack_sheet_animalhide_mothroach : obj_item_stack_sheet_animalhide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:103
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.mothroach_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:113
public partial class obj_item_stack_sheet_animalhide_gondola : obj_item_stack_sheet_animalhide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:121
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.gondola_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:77
public partial class obj_item_stack_sheet_animalhide_corgi : obj_item_stack_sheet_animalhide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:129
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.corgi_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:144
public partial class obj_item_stack_sheet_animalhide_carbon_monkey : obj_item_stack_sheet_animalhide_carbon
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:157
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.monkey_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:164
public partial class obj_item_stack_sheet_animalhide_carbon_lizard : obj_item_stack_sheet_animalhide_carbon
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:173
    public override object get_random_skin_color()
    {
        return sanitize_hexcolor(Dm.text("[][][]", pick("7F", "FF"), pick("7F", "FF"), pick("7F", "FF")));
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:183
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.lizard_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:187
public partial class obj_item_stack_sheet_animalhide_xeno : obj_item_stack_sheet_animalhide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:200
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.xeno_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:207
public partial class obj_item_stack_sheet_animalhide_carp : obj_item_stack_sheet_animalhide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:230
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.carp_recipes);
        return dot;
    }
}

/*
 * Leather SHeet
 */
// tg: code/game/objects/items/stacks/sheets/leather.dm:246
public partial class obj_item_stack_sheet_leather : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:293
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.leather_recipes);
        return dot;
    }
}

/*
 * Sinew
 */
// tg: code/game/objects/items/stacks/sheets/leather.dm:303
public partial class obj_item_stack_sheet_sinew : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:317
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // As bone and sinew have just a little too many recipes for this, we'll just split them up.
        // Sinew slapcrafting will mostly-sinew recipes, and bones will have mostly-bones recipes.
        DmList slapcraft_recipe_list = Dm.list(
            typeof(datum_crafting_recipe_goliathcloak),
            typeof(datum_crafting_recipe_skilt),
            typeof(datum_crafting_recipe_drakecloak));
        if (trophy_type != null)
            Dm.append(ref slapcraft_recipe_list, trophy_type);
        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", string_list(slapcraft_recipe_list))));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:362
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.sinew_recipes);
        return dot;
    }
}

/*Plates*/
// tg: code/game/objects/items/stacks/sheets/leather.dm:381
public partial class obj_item_stack_sheet_animalhide_goliath_hide_polar_bear_hide : obj_item_stack_sheet_animalhide_goliath_hide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:392
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.polar_bear_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:396
public partial class obj_item_stack_sheet_animalhide_ashdrake : obj_item_stack_sheet_animalhide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:409
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", TgState.Current.obj_item_stack_sheet_animalhide_ashdrake__Initialize__slapcraft_recipe_list)));
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/leather.dm:419
public partial class obj_item_stack_sheet_animalhide_bear : obj_item_stack_sheet_animalhide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:433
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.bear_pelt_recipes);
        return dot;
    }
}

//Step two - washing..... it's actually in washing machine code.

// tg: code/game/objects/items/stacks/sheets/leather.dm:456
public partial class obj_item_stack_sheet_hairlesshide
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:467
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You can clean it up by washing in the water.")));
        return dot;
    }
}

//Step three - drying
// tg: code/game/objects/items/stacks/sheets/leather.dm:472
public partial class obj_item_stack_sheet_wethide : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:487
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You can dry it up to make leather.")));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:491
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        Waiting.NoSleep(AddElement(typeof(datum_element_dryable), typeof(obj_item_stack_sheet_leather)));
        Waiting.NoSleep(AddElement(typeof(datum_element_microwavable), typeof(obj_item_stack_sheet_leather)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_grillable), typeof(obj_item_stack_sheet_leather), rand(1 * SECONDS, 3 * SECONDS), TRUE));
        Waiting.NoSleep(AddComponent(typeof(datum_component_bakeable), typeof(obj_item_stack_sheet_leather), rand(15 * SECONDS, 20 * SECONDS), TRUE, TRUE));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:499
    public override async DmTask burn__proc()
    {
        visible_message(span_notice(I18n.T("[] dries up!", this)));
        Dm.@new<obj_item_stack_sheet_leather>(loc, amount); // all the sheets to incentivize not losing your whole stack by accident
        qdel(this);
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:504
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(exposed_temperature) > drying_threshold_temperature ? TRUE : FALSE;
    }

    // tg: code/game/objects/items/stacks/sheets/leather.dm:507
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        wetness--;
        if (wetness == 0)
        {
            Dm.@new<obj_item_stack_sheet_leather>(drop_location(), amount);
            wetness = Dm.num(Dm.initial(this, (obj_item_stack_sheet_wethide p) => p.wetness));
            await use(1);
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/stacks/sheets/leather.dm:412  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/drakecloak)
    internal DmList obj_item_stack_sheet_animalhide_ashdrake__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_drakecloak));
}
