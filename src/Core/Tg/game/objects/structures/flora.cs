// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/flora.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST1 (D333's station order: section 9's decor): the whole file. The flora: trees chopped down with a saw into logs and a
// stump, bushes and grass picked by hand, rocks mined with a pickaxe into sand, each with its sound; a shovel uproots and replants
// any of them (the turned sprite); what is harvested regrows after a while; the random styles pick their look at init; the
// Christmas tree with presents hands each player one gift (the holidays' tree spawner, D332); the festivus pole and the volcanic
// pore that leaves lava. Leaves: the trees' and the siderite growth's see-through component (DV499: it fades a big sprite for a
// top-down viewer standing behind it, which the first-person view has no use for) and the volcanic pore's emissive glow (DV12).
// The var blocks are generated (D111).

// tg: code/game/objects/structures/flora.dm:1
public partial class obj_structure_flora : obj_structure
{
    // tg: code/game/objects/structures/flora.dm:52
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
        {
            balloon_alert(user, I18n.T("it goes right through!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(can_uproot) && Dm.eq(tool.tool_behaviour, TOOL_SHOVEL))
        {
            if (Dm.truthy(uprooted))
                user.visible_message(span_notice(I18n.T("[] starts to replant []...", user, this)),
                                    span_notice(I18n.T("You start to replant []...", this)));
            else
                user.visible_message(span_notice(I18n.T("[] starts to uproot []...", user, this)),
                                    span_notice(I18n.T("You start to uproot []...", this)));
            tool.play_tool_sound(this, 50);
            if (!Dm.truthy(await do_after(user, harvest_time, this)))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.truthy(uprooted))
            {
                user.visible_message(span_notice(I18n.T("[] replants [].", user, this)),
                                    span_notice(I18n.T("You replant [].", this)));
                replant(user);
            }
            else
            {
                user.visible_message(span_notice(I18n.T("[] uproots [].", user, this)),
                                    span_notice(I18n.T("You uproot [].", this)));
                uproot(user);
            }
            tool.play_tool_sound(this, 50);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(can_harvest(user, tool)))
            return NONE;

        user.visible_message(span_notice(I18n.T("[] starts to [] []...", user, harvest_verb, this)),
                            span_notice(I18n.T("You start to [] [] with []...", harvest_verb, this, tool)));
        play_attack_sound(tool.force);
        if (!Dm.truthy(await do_after(user, harvest_time * tool.toolspeed, this)))
            return ITEM_INTERACT_BLOCKING;

        visible_message(span_notice(I18n.T("[] [][] [].", user, harvest_verb, harvest_verb_suffix, this)),
                        span_notice(I18n.T("You [] [].", harvest_verb, this)));
        play_attack_sound(tool.force);
        if (Dm.truthy(await harvest(user)))
            await after_harvest(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/flora.dm:97
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(can_harvest(user)))
            return dot;

        user.visible_message(span_notice(I18n.T("[] starts to [] []...", user, harvest_verb, this)),
            span_notice(I18n.T("You start to [] []...", harvest_verb, this)));
        play_attack_sound();
        if (!Dm.truthy(await do_after(user, harvest_time, this)))
            return dot;
        visible_message(span_notice(I18n.T("[] [][] [].", user, harvest_verb, harvest_verb_suffix, this)),
            ignored_mobs: Dm.list(user));
        play_attack_sound();

        if (Dm.truthy(await harvest(user)))
            await after_harvest(user);
        return dot;
    }

    // tg: code/game/objects/structures/flora.dm:116
    public override async DmTask<object> run_atom_armor(object damage_amount = null, object damage_type = null, object damage_flag = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_flag ??= 0;
        if (Dm.eq(damage_flag, MELEE))
        {
            if (Dm.eq(damage_type, BURN))
                damage_amount = Dm.num(damage_amount) * 4;
        }
        return await base.run_atom_armor(damage_amount, damage_type, damage_flag, attack_dir, armour_penetration);
    }

    // tg: code/game/objects/structures/flora.dm:122
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        float use_default_sound = TRUE; //Because I don't wanna do unnecessary bitflag checks in a single if statement, while also allowing for multiple sounds to be played
        if (Dm.truthy(Dm.bitand(flora_flags, FLORA_HERBAL)))
        {
            playsound(this, SFX_CRUNCHY_BUSH_WHACK, 50, vary: FALSE);
            use_default_sound = FALSE;
        }
        if (Dm.truthy(Dm.bitand(flora_flags, FLORA_WOODEN)))
        {
            playsound(this, SFX_TREE_CHOP, 50, vary: FALSE);
            use_default_sound = FALSE;
        }
        if (Dm.truthy(Dm.bitand(flora_flags, FLORA_STONE)))
        {
            playsound(this, SFX_ROCK_TAP, 50, vary: FALSE);
            use_default_sound = FALSE;
        }
        if (Dm.truthy(use_default_sound))
            return base.play_attack_sound(damage_amount, damage_type, damage_flag, __extra);
        return null;
    }

    /**
     * A helper proc for getting the products that could be created when harvesting this flora, list syntax is (type = weight)
     * Because of how this works, it can spawn in anomalies if you want it to. Or wall girders
     * Returns: An assoc list of obj typepaths and their weights e.g. list(/obj/item/food/grass = 1), or null
     */
    // tg: code/game/objects/structures/flora.dm:141
    public virtual object get_potential_products()
    {
        return null;
    }

    /**
     * A helper proc for getting a random amount of products, associated with the flora's product list.
     * Returns: A list where each value is (harvested_item_typepath = amount_of_products)
     */
    // tg: code/game/objects/structures/flora.dm:148
    public virtual object get_products_list()
    {
        DmList potential_product_list = get_potential_products() as DmList;
        if (potential_product_list == null)
            return null;

        DmList product_list = Dm.list();

        float harvest_amount = rand(harvest_amount_low, harvest_amount_high);
        for (float iteration = 1, iteration__end = harvest_amount; iteration <= iteration__end; iteration++)
        {
            object chosen_product = pick_weight(potential_product_list);
            if (!Dm.truthy(product_list[chosen_product]))
                product_list[chosen_product] = 0f;
            product_list[chosen_product] = Dm.num(product_list[chosen_product]) + 1;
        }
        return product_list;
    }

    /**
     * A helper proc that determines if a user can currently harvest this flora with whatever tool they're trying to use.
     * Returns: TRUE if they can harvest, FALSE if not. Null if it's not harvestable at all.
     */
    // tg: code/game/objects/structures/flora.dm:167
    public virtual object can_harvest(mob user = null, obj_item harvesting_item = null)
    {
        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            return FALSE;
        if (Dm.truthy(harvested) || !Dm.truthy(harvestable))
            return null;

        if (harvesting_item != null)
        {
            //Check to see if wooden flora is being attacked by a saw item (letting the items on/off state control this is better than putting them in the list)
            if (Dm.truthy(Dm.bitand(flora_flags, FLORA_WOODEN)) && Dm.eq(harvesting_item.tool_behaviour, TOOL_SAW))
                return TRUE;
            //Check to see if stone flora is being attacked by a mining item (same reason as above)
            if (Dm.truthy(Dm.bitand(flora_flags, FLORA_STONE)) && Dm.eq(harvesting_item.tool_behaviour, TOOL_MINING))
                return TRUE;
            //We checked all item interactions and could not harvest, lets return
            return FALSE;
        }

        //If there was no harvesting item supplied, check if it is hand harvestable
        if (Dm.truthy(harvest_with_hands))
            return TRUE;

        return FALSE;
    }

    /**
     * This gets called after a mob tries to harvest this flora with the correct tool.
     * It displays a flavor message to whoever's harvesting this flora, then creates new products depending on the flora's product list.
     * Also renames the flora if harvested_name or harvested_desc is set in the variables
     * Returns: FALSE if nothing was made, otherwise a list of created products
     */
    // tg: code/game/objects/structures/flora.dm:196
    public virtual async DmTask<object> harvest(object user = null, float? product_amount_multiplier = null)
    {
        product_amount_multiplier ??= 1;
        object dot = FALSE;
        if (Dm.truthy(harvested))
            return FALSE;

        DmList products_to_create = get_products_list() as DmList;
        if (!Dm.truthy(LAZYLEN(products_to_create)))
            return FALSE;

        float products_created = 0;
        turf turf_below = get_turf(this);

        //This loop creates new products on the turf of our flora, but checks if it's an item stack
        //If it *is* an item stack, we don't want to go through 50 different iterations of a new object where it just gets qdeleted after the first
        dot = Dm.list();
        foreach (object product in Dm.each(products_to_create))
        {
            float amount_to_create = round(Dm.num(products_to_create[product]) * product_amount_multiplier.Value, 1);
            products_created += amount_to_create;
            if (ispath(product, typeof(obj_item_stack)))
            {
                float product_left = amount_to_create;
                while (product_left > 0)
                {
                    obj_item_stack new_stack = Dm.@new((Type)product, turf_below) as obj_item_stack;
                    product_left -= (float)(new_stack.amount = min(product_left, new_stack.max_amount));
                    Dm.append(ref dot, new_stack);
                }
            }
            else
            {
                for (float iteration = 1, iteration__end = amount_to_create; iteration <= iteration__end; iteration++)
                    Dm.append(ref dot, Dm.@new((Type)product, turf_below));
            }
        }

        //This bit of code determines what should be shown to the user when this is harvested
        object message = (harvest_message_med is var or__1 && Dm.truthy(or__1) ? or__1 : (harvest_message_high is var or__2 && Dm.truthy(or__2) ? or__2 : harvest_message_low));
        if (Dm.truthy(user) && Dm.truthy(message))
        {
            if (Dm.truthy(harvest_message_true_thresholds)) //Old method of how the harvest messages worked. Useful depending on the context you want to implement
            {
                if (products_created == harvest_amount_low && Dm.truthy(harvest_message_low))
                    message = harvest_message_low;
                if (products_created == harvest_amount_high && Dm.truthy(harvest_message_high))
                    message = harvest_message_high;
            }
            else //New method of determining the message to display. Separates the messages into 3 different viable "regions"
            {
                //[   ][   ][   ] the default message depends on whether or not something's nulled out
                float comparison = products_created - harvest_amount_low; //avoiding unnecessary math
                float middle_value = round((harvest_amount_high - harvest_amount_low) / 2) + harvest_amount_low; //mathy shit, gets the middle between two values
                //[***][   ][   ]
                if (comparison < (middle_value - harvest_amount_low) / 2 && Dm.truthy(harvest_message_low)) //more mathy shit, gets the middle between middle_value and harvest_amount_low
                    message = harvest_message_low;
                //[   ][   ][***]
                if (comparison > (harvest_amount_high - middle_value) / 2 && Dm.truthy(harvest_message_high)) //the middle between middle_value and harvest_amount_high
                    message = harvest_message_high;
                //[   ][***][   ] use the default message if none of the above applies
            }

            to_chat(user, span_notice(Dm.str(message)));
        }

        if (Dm.truthy(harvested_name))
            name = Dm.str(harvested_name);
        if (Dm.truthy(harvested_desc))
            desc = Dm.str(harvested_desc);
        harvested = TRUE;
        if (!Dm.truthy(delete_on_harvest) && regrowth_time_high > 0)
            _addtimer(Dm.@new<datum_callback>(this, nameof(regrow)), rand(regrowth_time_low, regrowth_time_high), file: "code/game/objects/structures/flora.dm", line: 252);
        await DmTask.CompletedTask;
        return dot;
    }

    // tg: code/game/objects/structures/flora.dm:254
    public virtual async DmTask after_harvest(object user = null)
    {
        if (Dm.truthy(delete_on_harvest))
            qdel(this);
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/flora.dm:258
    public virtual void regrow()
    {
        name = Dm.initial(this, (atom p) => p.name);
        desc = Dm.initial(this, (atom p) => p.desc);
        harvested = FALSE;
    }

    /**
     * Called after the user uproots the flora with a shovel.
     */
    // tg: code/game/objects/structures/flora.dm:266
    public virtual void uproot(mob_living user = null)
    {
        anchored = FALSE;
        uprooted = TRUE;
        matrix M = Dm.matrix(transform);
        previous_rotation = Dm.num(pick(-90, 90));
        transform = M.Turn(previous_rotation);
    }

    /**
     * Called after the user plants the flora back into the ground after uprooted
     */
    // tg: code/game/objects/structures/flora.dm:276
    public virtual void replant(mob_living user = null)
    {
        anchored = Dm.initial(this, (atom_movable p) => p.anchored);
        uprooted = FALSE;
        matrix M = Dm.matrix(transform);
        transform = M.Turn(-previous_rotation);
    }

    // tg: code/game/objects/structures/flora.dm:282
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(harvested))
        {
            await base.atom_deconstruct(disassembled);
            return;
        }

        await harvest(product_amount_multiplier: 0.6f);
    }
}

/*********
 * Trees *
 *********/
//Can *you* speak their language?

// tg: code/game/objects/structures/flora.dm:296
public partial class obj_structure_flora_tree : obj_structure_flora
{
    /// The amount of extra range (in tiles) given to trees upon falling.
    // tg: code/game/objects/structures/flora.dm:294  #define TREE_FALL_EXTRARANGE 5 (file-local)
    private const float TREE_FALL_EXTRARANGE = 5;

    // tg: code/game/objects/structures/flora.dm:315
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(get_seethrough_map()))
            // DEVIATION(DV499): AddComponent(/datum/component/seethrough, get_seethrough_map()) fades the tree's big sprite for a
            // top-down viewer standing behind it; the first-person view sees the tree from its own eye, so it is not added.
            Dm.Deferred("code/game/objects/structures/flora.dm:318", "Initialize: AddComponent(/datum/component/seethrough), the tree's fade for a top-down viewer behind it (DV499)");
        return dot;
    }

    // tg: code/game/objects/structures/flora.dm:320
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_grown_log_tree), 1));
    }

    ///Return a see_through_map, examples in seethrough.dm
    // tg: code/game/objects/structures/flora.dm:324
    public virtual object get_seethrough_map()
    {
        return SEE_THROUGH_MAP_DEFAULT;
    }

    // tg: code/game/objects/structures/flora.dm:327
    public override async DmTask<object> harvest(object user = null, float? product_amount_multiplier = null)
    {
        object dot = await base.harvest(user, product_amount_multiplier);
        turf my_turf = get_turf(this);
        if (Dm.truthy(has_gravity(my_turf))) // If a tree falls in the forest, it makes a sound unless it doesn't have gravity.
            playsound(my_turf, Dm.resource("sound/effects/meteorimpact.ogg"), 100, FALSE, extrarange: TREE_FALL_EXTRARANGE);
        obj_structure_flora_tree_stump new_stump = Dm.@new<obj_structure_flora_tree_stump>(my_turf);
        new_stump.name = I18n.T("[] stump", name);
        return dot;
    }

    // tg: code/game/objects/structures/flora.dm:335
    public override void uproot(mob_living user = null)
    {
        base.uproot(user);
        if (Dm.truthy(has_gravity(get_turf(this))))
            playsound(get_turf(this), Dm.resource("sound/effects/meteorimpact.ogg"), 100, FALSE, extrarange: TREE_FALL_EXTRARANGE);
    }
}

// tg: code/game/objects/structures/flora.dm:340
public partial class obj_structure_flora_tree_stump : obj_structure_flora_tree
{
    // tg: code/game/objects/structures/flora.dm:348
    public override async DmTask<object> harvest(object user = null, float? product_amount_multiplier = null)
    {
        to_chat(user, span_notice(I18n.T("You manage to remove [].", this)));
        qdel(this);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/structures/flora.dm:352
    public override void uproot(mob_living user = null)
    {
        base.uproot(user);
        to_chat(user, span_notice(I18n.T("You manage to remove [].", this)));
        qdel(this);
    }

    // tg: code/game/objects/structures/flora.dm:357
    public override object get_seethrough_map()
    {
        return FALSE;
    }
}

// tg: code/game/objects/structures/flora.dm:383
public partial class obj_structure_flora_tree_dead_style_random : obj_structure_flora_tree_dead
{
    // tg: code/game/objects/structures/flora.dm:383
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("tree_[]", rand(1, 6));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:388
public partial class obj_structure_flora_tree_jungle : obj_structure_flora_tree
{
    // tg: code/game/objects/structures/flora.dm:395
    public override object get_seethrough_map()
    {
        return SEE_THROUGH_MAP_THREE_X_THREE;
    }
}

// tg: code/game/objects/structures/flora.dm:413
public partial class obj_structure_flora_tree_jungle_style_random : obj_structure_flora_tree_jungle
{
    // tg: code/game/objects/structures/flora.dm:413
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("tree[]", rand(1, 6));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:418
public partial class obj_structure_flora_tree_jungle_small : obj_structure_flora_tree_jungle
{
    // tg: code/game/objects/structures/flora.dm:424
    public override object get_seethrough_map()
    {
        return SEE_THROUGH_MAP_THREE_X_TWO;
    }
}

// tg: code/game/objects/structures/flora.dm:442
public partial class obj_structure_flora_tree_jungle_small_style_random : obj_structure_flora_tree_jungle_small
{
    // tg: code/game/objects/structures/flora.dm:442
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("tree[]", rand(1, 6));
        update_appearance();
        return dot;
    }
}

/**************
 * Pine Trees *
 **************/

// tg: code/game/objects/structures/flora.dm:451
public partial class obj_structure_flora_tree_pine : obj_structure_flora_tree
{
    // tg: code/game/objects/structures/flora.dm:457
    public override object get_seethrough_map()
    {
        return SEE_THROUGH_MAP_DEFAULT_TWO_TALL;
    }
}

// tg: code/game/objects/structures/flora.dm:466
public partial class obj_structure_flora_tree_pine_style_random : obj_structure_flora_tree_pine
{
    // tg: code/game/objects/structures/flora.dm:466
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("pine_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:480
public partial class obj_structure_flora_tree_pine_xmas_presents : obj_structure_flora_tree_pine_xmas
{
    // tg: code/game/objects/structures/flora.dm:486  var/static/list/took_presents (shared between all xmas trees)
    public static ref DmList took_presents => ref TgState.Current.obj_structure_flora_tree_pine_xmas_presents__took_presents;

    // tg: code/game/objects/structures/flora.dm:488
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(took_presents))
            took_presents = Dm.list();
        return dot;
    }

    // tg: code/game/objects/structures/flora.dm:493
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(user.ckey))
            return dot;

        if (Dm.truthy(took_presents[user.ckey]) && !Dm.truthy(unlimited))
        {
            to_chat(user, span_warning(I18n.T("There are no presents with your name on.")));
            return dot;
        }
        to_chat(user, span_warning(I18n.T("After a bit of rummaging, you locate a gift with your name on it!")));

        if (!Dm.truthy(unlimited))
            took_presents[user.ckey] = TRUE;

        obj_item G = Dm.@new(gift_type, this) as obj_item;
        await user.put_in_hands(G);
        return dot;
    }
}

/**************
 * Palm Trees *
 **************/

// tg: code/game/objects/structures/flora.dm:547
public partial class obj_structure_flora_tree_palm_style_random : obj_structure_flora_tree_palm
{
    // tg: code/game/objects/structures/flora.dm:547
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("palm[]", rand(1, 2));
        update_appearance();
        return dot;
    }
}

/*********
 * Grass *
 *********/

// tg: code/game/objects/structures/flora.dm:557
public partial class obj_structure_flora_grass : obj_structure_flora
{
    // tg: code/game/objects/structures/flora.dm:571
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_food_grown_grass), 10), Dm.kv(typeof(obj_item_seeds_grass), 1));
    }
}

// tg: code/game/objects/structures/flora.dm:583
public partial class obj_structure_flora_grass_brown_style_random : obj_structure_flora_grass_brown
{
    // tg: code/game/objects/structures/flora.dm:583
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("snowgrass[]bb", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:597
public partial class obj_structure_flora_grass_green_style_random : obj_structure_flora_grass_green
{
    // tg: code/game/objects/structures/flora.dm:597
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("snowgrass[]gb", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:611
public partial class obj_structure_flora_grass_both_style_random : obj_structure_flora_grass_both
{
    // tg: code/game/objects/structures/flora.dm:611
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("snowgrassall[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:634
public partial class obj_structure_flora_grass_jungle_a_style_random : obj_structure_flora_grass_jungle_a
{
    // tg: code/game/objects/structures/flora.dm:634
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("grassa[]", rand(1, 5));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:654
public partial class obj_structure_flora_grass_jungle_b_style_random : obj_structure_flora_grass_jungle_b
{
    // tg: code/game/objects/structures/flora.dm:654
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("grassb[]", rand(1, 5));
        update_appearance();
        return dot;
    }
}

/**********
 * Bushes *
 **********/

// tg: code/game/objects/structures/flora.dm:679
public partial class obj_structure_flora_bush_style_random : obj_structure_flora_bush
{
    // tg: code/game/objects/structures/flora.dm:679
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("firstbush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:696
public partial class obj_structure_flora_bush_reed_style_random : obj_structure_flora_bush_reed
{
    // tg: code/game/objects/structures/flora.dm:696
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("reedbush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:710
public partial class obj_structure_flora_bush_leavy_style_random : obj_structure_flora_bush_leavy
{
    // tg: code/game/objects/structures/flora.dm:710
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("leafybush_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:727
public partial class obj_structure_flora_bush_pale_style_random : obj_structure_flora_bush_pale
{
    // tg: code/game/objects/structures/flora.dm:727
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("palebush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:741
public partial class obj_structure_flora_bush_stalky_style_random : obj_structure_flora_bush_stalky
{
    // tg: code/game/objects/structures/flora.dm:741
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("stalkybush_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:758
public partial class obj_structure_flora_bush_grassy_style_random : obj_structure_flora_bush_grassy
{
    // tg: code/game/objects/structures/flora.dm:758
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("grassybush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:772
public partial class obj_structure_flora_bush_sparsegrass_style_random : obj_structure_flora_bush_sparsegrass
{
    // tg: code/game/objects/structures/flora.dm:772
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("sparsegrass_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:786
public partial class obj_structure_flora_bush_fullgrass_style_random : obj_structure_flora_bush_fullgrass
{
    // tg: code/game/objects/structures/flora.dm:786
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("fullgrass_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:800
public partial class obj_structure_flora_bush_ferny_style_random : obj_structure_flora_bush_ferny
{
    // tg: code/game/objects/structures/flora.dm:800
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("fernybush_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:814
public partial class obj_structure_flora_bush_sunny_style_random : obj_structure_flora_bush_sunny
{
    // tg: code/game/objects/structures/flora.dm:814
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("sunnybush_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:831
public partial class obj_structure_flora_bush_generic_style_random : obj_structure_flora_bush_generic
{
    // tg: code/game/objects/structures/flora.dm:831
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("genericbush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:848
public partial class obj_structure_flora_bush_pointy_style_random : obj_structure_flora_bush_pointy
{
    // tg: code/game/objects/structures/flora.dm:848
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("pointybush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:865
public partial class obj_structure_flora_bush_lavendergrass_style_random : obj_structure_flora_bush_lavendergrass
{
    // tg: code/game/objects/structures/flora.dm:865
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("lavendergrass_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:879
public partial class obj_structure_flora_bush_flowers_yw_style_random : obj_structure_flora_bush_flowers_yw
{
    // tg: code/game/objects/structures/flora.dm:879
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("ywflowers_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:893
public partial class obj_structure_flora_bush_flowers_br_style_random : obj_structure_flora_bush_flowers_br
{
    // tg: code/game/objects/structures/flora.dm:893
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("brflowers_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:907
public partial class obj_structure_flora_bush_flowers_pp_style_random : obj_structure_flora_bush_flowers_pp
{
    // tg: code/game/objects/structures/flora.dm:907
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("ppflowers_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:931
public partial class obj_structure_flora_bush_snow_style_random : obj_structure_flora_bush_snow
{
    // tg: code/game/objects/structures/flora.dm:931
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("snowbush[]", rand(1, 6));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:948
public partial class obj_structure_flora_bush_jungle_a_style_random : obj_structure_flora_bush_jungle_a
{
    // tg: code/game/objects/structures/flora.dm:948
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("busha[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:962
public partial class obj_structure_flora_bush_jungle_b_style_random : obj_structure_flora_bush_jungle_b
{
    // tg: code/game/objects/structures/flora.dm:962
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("bushb[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:976
public partial class obj_structure_flora_bush_jungle_c_style_random : obj_structure_flora_bush_jungle_c
{
    // tg: code/game/objects/structures/flora.dm:976
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("bushc[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:995
public partial class obj_structure_flora_bush_large_style_random : obj_structure_flora_bush_large
{
    // tg: code/game/objects/structures/flora.dm:995
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("bush[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:1000
public partial class obj_structure_flora_lunar_plant : obj_structure_flora
{
    // tg: code/game/objects/structures/flora.dm:1009
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("lunar_plant[]", rand(1, 3));
        return dot;
    }
}

/*********
 * Rocks *
 *********/
// (I know these aren't plants)

// tg: code/game/objects/structures/flora.dm:1027
public partial class obj_structure_flora_rock : obj_structure_flora
{
    // tg: code/game/objects/structures/flora.dm:1042
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_stack_ore_glass_basalt), 1));
    }
}

// tg: code/game/objects/structures/flora.dm:1054
public partial class obj_structure_flora_rock_style_random : obj_structure_flora_rock
{
    // tg: code/game/objects/structures/flora.dm:1054
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("basalt[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:1074
public partial class obj_structure_flora_rock_pile_style_random : obj_structure_flora_rock_pile
{
    // tg: code/game/objects/structures/flora.dm:1074
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("lavarocks[]", Dm.pick_weighted((3, 1), (3, 2), (1, 3)));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:1079
public partial class obj_structure_flora_rock_pile_siderite : obj_structure_flora_rock_pile
{
    // tg: code/game/objects/structures/flora.dm:1082
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_stack_ore_glass_siderite), 1));
    }
}

// tg: code/game/objects/structures/flora.dm:1091
public partial class obj_structure_flora_rock_pile_siderite_style_random : obj_structure_flora_rock_pile_siderite
{
    // tg: code/game/objects/structures/flora.dm:1091
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("lavarocks_siderite[]", Dm.pick_weighted((3, 1), (3, 2), (1, 3)));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:1105
public partial class obj_structure_flora_rock_pile_shale_style_random : obj_structure_flora_rock_pile_shale
{
    // tg: code/game/objects/structures/flora.dm:1105
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("lavarocks_shale[]", Dm.pick_weighted((3, 1), (3, 2), (1, 3)));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:1110
public partial class obj_structure_flora_rock_siderite_growth : obj_structure_flora_rock
{
    // tg: code/game/objects/structures/flora.dm:1118
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("[][]", base_icon_state, Dm.pick_weighted((3, 1), (3, 2), (1, 3)));
        update_appearance();
        // DEVIATION(DV499): AddComponent(/datum/component/seethrough, SEE_THROUGH_MAP_DEFAULT), the fade for a top-down viewer behind it
        Dm.Deferred("code/game/objects/structures/flora.dm:1122", "Initialize: AddComponent(/datum/component/seethrough), the siderite growth's fade for a top-down viewer behind it (DV499)");
        return dot;
    }

    // tg: code/game/objects/structures/flora.dm:1124
    public override object get_potential_products()
    {
        return Dm.list(Dm.kv(typeof(obj_item_stack_ore_glass_siderite), 1));
    }
}

// tg: code/game/objects/structures/flora.dm:1127
public partial class obj_structure_flora_rock_volcano : obj_structure_flora_rock
{
    // tg: code/game/objects/structures/flora.dm:1137
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("[]_[]", base_icon_state, rand(1, 4));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/flora.dm:1142
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // . += emissive_appearance(icon, "[icon_state]_e", src, alpha = 200): the lava's glow on the emissive plane (DEVIATION DV12)
        Dm.Deferred("code/game/objects/structures/flora.dm:1144", "update_overlays: emissive_appearance of the volcanic pore's lava (DEVIATION DV12)");
        return dot;
    }

    // tg: code/game/objects/structures/flora.dm:1146
    public override async DmTask after_harvest(object user = null)
    {
        if (loc is turf_open our_turf)
            await our_turf.ChangeTurf(typeof(turf_open_lava_smooth), flags: CHANGETURF_INHERIT_AIR);
        await base.after_harvest(user);
    }
}

// tg: code/game/objects/structures/flora.dm:1168
public partial class obj_structure_flora_rock_pile_jungle_style_random : obj_structure_flora_rock_pile_jungle
{
    // tg: code/game/objects/structures/flora.dm:1168
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("rock[]", rand(1, 5));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:1188
public partial class obj_structure_flora_rock_pile_jungle_large_style_random : obj_structure_flora_rock_pile_jungle_large
{
    // tg: code/game/objects/structures/flora.dm:1188
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("rocks[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:1205
public partial class obj_structure_flora_rock_icy_style_random : obj_structure_flora_rock_icy
{
    // tg: code/game/objects/structures/flora.dm:1205
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("basalt[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/flora.dm:1221
public partial class obj_structure_flora_rock_pile_icy_style_random : obj_structure_flora_rock_pile_icy
{
    // tg: code/game/objects/structures/flora.dm:1221
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("lavarocks[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/flora.dm:486
    internal DmList obj_structure_flora_tree_pine_xmas_presents__took_presents;
}
