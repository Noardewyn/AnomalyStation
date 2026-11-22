// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/boxes/food_boxes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The box of Donk pockets and its flavours (syndicate N2b, D316: the Donk pinata drops it), the coffee beans (gaps H2, D333); the rest
// of the file (gaps SR1, D333): the paper sack (its designs drawn with a pen, eyeholes cut into it), the ingredient boxes, the gum
// packets and their wrappers, the Tiziran and Mothic packs, the condiment bottles and the beef ramen. The var blocks are generated
// (D111).

// This contains all boxes with edible stuffs or stuff related to edible stuffs.

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:3
public partial class obj_item_storage_box_donkpockets : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:12
    public override async DmTask PopulateContents()
    {
        for (int i = 1; i <= 6; i++)
            Dm.@new(donktype, this);
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:447 (gaps H2, D333: the impressa coffeemaker's coffee beans)
public partial class obj_item_storage_box_coffeepack : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:456
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 5; i++)
        {
            obj_item_food_grown_coffee bean = Dm.@new(beantype, this) as obj_item_food_grown_coffee;
            ADD_TRAIT(bean, TRAIT_DRIED, ELEMENT_TRAIT(type));
            await bean.add_atom_colour(COLOR_DRIED_TAN, FIXED_COLOUR_PRIORITY); //give them the tan just like from the drying rack
        }
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:147
public partial class obj_item_storage_box_papersack_meat : obj_item_storage_box_papersack
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:147
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_food_meat_slab>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:154
public partial class obj_item_storage_box_papersack_wheat : obj_item_storage_box_papersack
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:154
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            Dm.@new<obj_item_food_grown_wheat>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:197
public partial class obj_item_storage_box_ingredients_american : obj_item_storage_box_ingredients
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:197
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 2; i++)
        {
            Dm.@new<obj_item_food_grown_corn>(this);
            Dm.@new<obj_item_food_grown_potato>(this);
            Dm.@new<obj_item_food_grown_tomato>(this);
        }
        Dm.@new<obj_item_food_meatball>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:442
public partial class obj_item_storage_box_condimentbottles : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:442
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 6; i++)
        {
            Dm.@new<obj_item_reagent_containers_condiment>(this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:476
public partial class obj_item_storage_box_ramen_beef : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:476
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_reagent_containers_condiment_pack_beef_flavour>(this);
        Dm.@new<obj_item_food_spaghetti_ramen_dry>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:53
public partial class obj_item_storage_box_papersack : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:68
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        papersack_designs = sort_list(Dm.list(
            Dm.kv("None", image(icon: this.icon, icon_state: "paperbag_None")),
            Dm.kv("NanotrasenStandard", image(icon: this.icon, icon_state: "paperbag_NanotrasenStandard")),
            Dm.kv("SyndiSnacks", image(icon: this.icon, icon_state: "paperbag_SyndiSnacks")),
            Dm.kv("Heart", image(icon: this.icon, icon_state: "paperbag_Heart")),
            Dm.kv("SmileyFace", image(icon: this.icon, icon_state: "paperbag_SmileyFace")))) as DmList;
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:79
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(design_choice)))
            update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:84
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("paperbag_[][]", design_choice, (contents.len == 0) ? null : "_closed");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:88
    public override object update_desc(float? updates = null)
    {
        switch (design_choice)
        {
            case "None":
                desc = I18n.T("A sack neatly crafted out of paper.");
                break;
            case "NanotrasenStandard":
                desc = I18n.T("A standard Nanotrasen paper lunch sack for loyal employees on the go.");
                break;
            case "SyndiSnacks":
                desc = I18n.T("The design on this paper sack is a remnant of the notorious 'SyndieSnacks' program.");
                break;
            case "Heart":
                desc = I18n.T("A paper sack with a heart etched onto the side.");
                break;
            case "SmileyFace":
                desc = I18n.T("A paper sack with a crude smile etched onto the side.");
                break;
        }
        return base.update_desc(updates);
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:102
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (IS_WRITING_UTENSIL(tool))
        {
            object choice = await show_radial_menu(user, this, papersack_designs, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user, tool), radius: 36, require_near: TRUE);
            if (!Dm.truthy(choice) || Dm.eq(choice, design_choice))
                return ITEM_INTERACT_BLOCKING;
            design_choice = (string)choice;
            balloon_alert(user, I18n.T("modified"));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(tool.get_sharpness()) && !Dm.truthy(contents.len))
        {
            if (design_choice == "None")
            {
                user.show_message(span_notice(I18n.T("You cut eyeholes into [].", this)), MSG_VISUAL);
                Dm.@new<obj_item_clothing_head_costume_papersack>(drop_location());
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
            }
            else if (design_choice == "SmileyFace")
            {
                user.show_message(span_notice(I18n.T("You cut eyeholes into [] and modify the design.", this)), MSG_VISUAL);
                Dm.@new<obj_item_clothing_head_costume_papersack_smiley>(drop_location());
                qdel(this);
                return ITEM_INTERACT_SUCCESS;
            }
        }
        return await base.tool_act(user, tool, modifiers);
    }

    /**
     * check_menu: Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The mob interacting with a menu
     * * P The pen used to interact with a menu
     */
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:131
    public virtual object check_menu(mob user = null, obj_item P = null)
    {
        if (!istype(user, typeof(mob)))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        if (Dm.truthy(contents.len))
        {
            balloon_alert(user, I18n.T("items inside!"));
            return FALSE;
        }
        if (!Dm.truthy(P) || !Dm.truthy(user.is_holding(P)))
        {
            balloon_alert(user, I18n.T("needs pen!"));
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:159
public partial class obj_item_storage_box_ingredients : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:163
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(theme_name))
        {
            name = I18n.T("[] ([])", name, theme_name);
            desc = I18n.T("A box containing supplementary ingredients for the aspiring chef. The box's theme is '[]'.", theme_name);
            inhand_icon_state = "syringe_kit";
        }
        return dot;
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:170
public partial class obj_item_storage_box_ingredients_wildcard : obj_item_storage_box_ingredients
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:173
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 7; i++)
        {
            object random_food = pick(
                typeof(obj_item_food_chocolatebar),
                typeof(obj_item_food_grown_apple),
                typeof(obj_item_food_grown_banana),
                typeof(obj_item_food_grown_cabbage),
                typeof(obj_item_food_grown_carrotlike_carrot),
                typeof(obj_item_food_grown_cherries),
                typeof(obj_item_food_grown_chili),
                typeof(obj_item_food_grown_corn),
                typeof(obj_item_food_grown_cucumber),
                typeof(obj_item_food_grown_mushroom_chanterelle),
                typeof(obj_item_food_grown_mushroom_plumphelmet),
                typeof(obj_item_food_grown_potato),
                typeof(obj_item_food_grown_potato_sweet),
                typeof(obj_item_food_grown_soybeans),
                typeof(obj_item_food_grown_tomato));
            Dm.@new((Type)random_food, this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:205
public partial class obj_item_storage_box_gum : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:218
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 4; i++)
            Dm.@new<obj_item_storage_bubblegum_wrapper>(this, spawning_gum_type);
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:222
public partial class obj_item_storage_box_gum_wake_up : obj_item_storage_box_gum
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:229
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        Dm.append(ref dot, span_notice(I18n.T("<i>You read some of the health and safety information...</i>")));
        Dm.append(ref dot, Dm.text("\t[]", span_info(I18n.T("For the relief of tiredness and drowsiness while working."))));
        Dm.append(ref dot, Dm.text("\t[]", span_info(I18n.T("Do not chew more than one strip every 12 hours. Do not use as a complete substitute for sleep."))));
        Dm.append(ref dot, Dm.text("\t[]", span_info(I18n.T("Do not give to children under 16. Do not exceed the maximum dosage. Do not ingest. Do not take for more than 3 days consecutively. Do not take in conjunction with other medication. May cause adverse reactions in patients with pre-existing heart conditions."))));
        Dm.append(ref dot, Dm.text("\t[]", span_info(I18n.T("Side effects of Activin use may include twitchy antennae, overactive wings, loss of keratin sheen, loss of setae coverage, arrythmia, blurred vision, and euphoria. Cease taking the medication if side effects occur."))));
        Dm.append(ref dot, Dm.text("\t[]", span_info(I18n.T("Repeated use may cause addiction."))));
        Dm.append(ref dot, Dm.text("\t[]", span_info(I18n.T("If the maximum dosage is exceeded, inform a member of your assigned vessel's medical staff immediately. Do not induce vomiting."))));
        Dm.append(ref dot, Dm.text("\t[]", span_info(I18n.T("Ingredients: each strip contains 500mg of Activin (dextro-methamphetamine). Other ingredients include Green Dye 450 (Verdant Meadow) and artificial herb flavouring."))));
        Dm.append(ref dot, Dm.text("\t[]", span_info(I18n.T("Storage: keep in a cool dry place. Do not use after the use-by date: 32/4/350."))));
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:242
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 4; i++)
            Dm.@new<obj_item_food_bubblegum_wake_up>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:246
public partial class obj_item_storage_bubblegum_wrapper : obj_item_storage
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:258
    [DmParams("mapload", "spawning_gum_type")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object spawning_gum_type = Dm.arg(__extra, 0);
        if (spawning_gum_type != null)
            gum_to_spawn = (Type)spawning_gum_type;
        object dot = base.Initialize(mapload, __extra);
        atom_storage.set_holdable(typeof(obj_item_food_bubblegum));
        atom_storage.max_slots = 1;
        atom_storage.display_contents = FALSE;
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:267
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_aluminium), 1));
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:270
    public override async DmTask PopulateContents()
    {
        Dm.@new(gum_to_spawn, this);
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:273
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        obj_item_food_bubblegum gum_inside = Dm.locate<obj_item_food_bubblegum>(contents);
        if (gum_inside == null)
            return dot;
        mutable_appearance gum_overlay = mutable_appearance(gum_inside.icon, gum_inside.icon_state, layer: this.layer - 0.01f) as mutable_appearance;
        gum_overlay.color = gum_inside.color;
        Dm.append(ref dot, gum_overlay);
        return dot;
    }

    //These procs are copied over from cigarette packets
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:283
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (istype(interacting_with, typeof(obj_item_food_bubblegum)))
        {
            atom_storage.item_interact_insert(user, interacting_with as obj_item);
            return ITEM_INTERACT_SUCCESS;
        }
        if (interacting_with != user)
            return await base.interact_with_atom(interacting_with, user, modifiers);
        await quick_remove_item(Dm.locate<obj_item_food_bubblegum>(contents), user, equip_to_mouth: TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:293
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await base.attack_hand_secondary(user, modifiers);
        await quick_remove_item(Dm.locate<obj_item_food_bubblegum>(contents), user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:298
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await quick_remove_item(Dm.locate<obj_item_food_bubblegum>(contents), user);
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:309
public partial class obj_item_storage_box_gum_happiness : obj_item_storage_box_gum
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:317
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(25))
            desc += I18n.T(" You can faintly make out the word 'Hemopagopril' was once scribbled on it.");
        return dot;
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:334
public partial class obj_item_storage_box_mothic_rations : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:334
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++)
        {
            object random_food = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_food_sustenance_bar), 10),
                Dm.kv(typeof(obj_item_food_sustenance_bar_cheese), 5),
                Dm.kv(typeof(obj_item_food_sustenance_bar_mint), 5),
                Dm.kv(typeof(obj_item_food_sustenance_bar_neapolitan), 5),
                Dm.kv(typeof(obj_item_food_sustenance_bar_wonka), 1)));
            Dm.@new((Type)random_food, this);
        }
        Dm.@new<obj_item_storage_box_gum_wake_up>(this);
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:352
public partial class obj_item_storage_box_tiziran_goods : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:352
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 12; i++)
        {
            object random_food = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_food_bread_root), 2),
                Dm.kv(typeof(obj_item_food_grown_ash_flora_seraka), 2),
                Dm.kv(typeof(obj_item_food_grown_korta_nut), 10),
                Dm.kv(typeof(obj_item_food_grown_korta_nut_sweet), 2),
                Dm.kv(typeof(obj_item_food_liver_pate), 5),
                Dm.kv(typeof(obj_item_food_lizard_dumplings), 5),
                Dm.kv(typeof(obj_item_food_moonfish_caviar), 5),
                Dm.kv(typeof(obj_item_food_root_flatbread), 5),
                Dm.kv(typeof(obj_item_food_rootroll), 5),
                Dm.kv(typeof(obj_item_food_spaghetti_nizaya), 5)));
            Dm.@new((Type)random_food, this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:374
public partial class obj_item_storage_box_tiziran_cans : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:374
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 8; i++)
        {
            object random_food = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_food_canned_jellyfish), 5),
                Dm.kv(typeof(obj_item_food_canned_desert_snails), 5),
                Dm.kv(typeof(obj_item_food_canned_larvae), 5)));
            Dm.@new((Type)random_food, this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:389
public partial class obj_item_storage_box_tiziran_meats : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:389
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 10; i++)
        {
            object random_food = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_food_fishmeat_armorfish), 5),
                Dm.kv(typeof(obj_item_food_fishmeat_gunner_jellyfish), 5),
                Dm.kv(typeof(obj_item_food_fishmeat_moonfish), 5),
                Dm.kv(typeof(obj_item_food_meat_slab), 5)));
            Dm.@new((Type)random_food, this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:405
public partial class obj_item_storage_box_mothic_goods : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:405
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 12; i++)
        {
            object random_food = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_food_cheese_cheese_curds), 5),
                Dm.kv(typeof(obj_item_food_cheese_curd_cheese), 5),
                Dm.kv(typeof(obj_item_food_cheese_firm_cheese), 5),
                Dm.kv(typeof(obj_item_food_cheese_mozzarella), 5),
                Dm.kv(typeof(obj_item_food_cheese_wheel), 5),
                Dm.kv(typeof(obj_item_food_grown_toechtauese), 10),
                Dm.kv(typeof(obj_item_reagent_containers_condiment_cornmeal), 5),
                Dm.kv(typeof(obj_item_reagent_containers_condiment_olive_oil), 5),
                Dm.kv(typeof(obj_item_reagent_containers_condiment_yoghurt), 5)));
            Dm.@new((Type)random_food, this);
        }
    }
}

// tg: code/game/objects/items/storage/boxes/food_boxes.dm:426
public partial class obj_item_storage_box_mothic_cans_sauces : obj_item_storage_box
{
    // tg: code/game/objects/items/storage/boxes/food_boxes.dm:426
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 8; i++)
        {
            object random_food = pick_weight(Dm.list(
                Dm.kv(typeof(obj_item_food_bechamel_sauce), 5),
                Dm.kv(typeof(obj_item_food_canned_pine_nuts), 5),
                Dm.kv(typeof(obj_item_food_canned_tomatoes), 5),
                Dm.kv(typeof(obj_item_food_pesto), 5),
                Dm.kv(typeof(obj_item_food_tomato_sauce), 5)));
            Dm.@new((Type)random_food, this);
        }
    }
}

