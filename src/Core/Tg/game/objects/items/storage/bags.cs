// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/bags.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the mining satchel (lines 98-249): it picks up the ore on its wearer's tile as they walk and what lands there. Gaps
// P2 (D298): the plant bag's storage and the portable seed extractor. Syndicate G4b (D316): the rebar quivers and their storage. The
// other bags are their own packages' (the sheet snatcher); silicons SI2: the money bag, the trash bag and the tray (the cyborgs' modules).

// tg: code/game/objects/items/storage/bags.dm:98
public partial class obj_item_storage_bag_ore : obj_item_storage_bag
{
    // tg: code/game/objects/items/storage/bags.dm:1  #define ORE_BAG_BALOON_COOLDOWN (2 SECONDS)
    private const float ORE_BAG_BALOON_COOLDOWN = 2 * SECONDS;

    // tg: code/game/objects/items/storage/bags.dm:115
    public override object Destroy(float? force = null)
    {
        listening_to = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/storage/bags.dm:119
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (listening_to != null)
            return;
        RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(on_user_moved));
        if (isturf(user.loc))
        {
            RegisterSignal(user.loc, COMSIG_ATOM_ENTERED, nameof(on_obj_entered));
            RegisterSignal(user.loc, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_atom_initialized_on));
        }
        listening_to = user;
    }

    // tg: code/game/objects/items/storage/bags.dm:129
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (listening_to == null)
            return dot;
        UnregisterSignal(listening_to, COMSIG_MOVABLE_MOVED);
        if (listening_to.loc != null)
            UnregisterSignal(listening_to.loc, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));
        listening_to = null;
        return dot;
    }

    // Ensure we don't suck up ores that we've just dropped off
    // tg: code/game/objects/items/storage/bags.dm:139
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        dropping_ores = TRUE;
        object dot = await base.attack_self(user, modifiers, __extra);
        dropping_ores = FALSE;
        return dot;
    }

    // tg: code/game/objects/items/storage/bags.dm:144
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_boulder)))
        {
            to_chat(user, span_warning(I18n.T("You can't fit [] into []. Perhaps you should break it down first, or find an ore box.", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }

    // tg: code/game/objects/items/storage/bags.dm:151
    public virtual void on_user_moved(mob_living user = null, atom old_loc = null, object dir = null, object forced = null, params object[] __extra)
    {
        if (old_loc != null)
            UnregisterSignal(old_loc, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));

        turf tile = get_turf(user);
        if (!isturf(tile))
            return;

        RegisterSignal(tile, COMSIG_ATOM_ENTERED, nameof(on_obj_entered));
        RegisterSignal(tile, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(on_atom_initialized_on));
        Waiting.Detach(handle_move(user));
    }

    // tg: code/game/objects/items/storage/bags.dm:165
    public virtual async DmTask handle_move(mob_living user = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user))
            return;
        turf tile = get_turf(user);
        obj_structure_ore_box box = null;
        if (istype(user.pulling, typeof(obj_structure_ore_box)))
            box = user.pulling as obj_structure_ore_box;

        bool show_message = false;
        foreach (atom thing in Dm.each<atom>(tile.contents.Copy()))
        {
            if (!Dm.truthy(is_type_in_typecache(thing, atom_storage.can_hold)))
                continue;
            if (Dm.truthy(await pickup_ore(thing as obj_item, user, box)))
                show_message = true;
        }

        if (!show_message)
        {
            spam_protection = FALSE;
            return;
        }

        playsound(user, SFX_RUSTLE, 50, TRUE);
        if (!(ore_bag_balloon_cooldown <= world.time)) // COOLDOWN_FINISHED
            return;

        spam_protection = FALSE;
        ore_bag_balloon_cooldown = world.time + ORE_BAG_BALOON_COOLDOWN; // COOLDOWN_START

        if (box != null)
        {
            balloon_alert(user, I18n.T("scoops ore into box"));
            user.visible_message(span_notice(I18n.T("[] offloads the ores beneath [] into [].", user, user.p_them(), box)), ignored_mobs: user);
            return;
        }

        balloon_alert(user, I18n.T("scoops ore into bag"));
        user.visible_message(span_notice(I18n.T("[] scoops up the ores beneath [].", user, user.p_them())), ignored_mobs: user);
    }

    // tg: code/game/objects/items/storage/bags.dm:205
    public virtual async DmTask<object> pickup_ore(obj_item ore = null, mob user = null, obj_structure_ore_box box = null)
    {
        if (box == null && istype(user.pulling, typeof(obj_structure_ore_box)))
            box = user.pulling as obj_structure_ore_box;

        if (box != null)
        {
            await user.transferItemToLoc(ore, box, animated: FALSE);
            return TRUE;
        }

        if (istype(ore, typeof(obj_item_stack_ore)))
        {
            obj_item_stack_ore real_ore = ore as obj_item_stack_ore;
            foreach (obj_item_stack_ore stored_ore in Dm.each<obj_item_stack_ore>(this))
            {
                if (!Dm.truthy(real_ore.can_merge(stored_ore)))
                    continue;
                await real_ore.merge(stored_ore);
                if (QDELETED(real_ore))
                    return TRUE;
            }
        }

        if (Dm.truthy(atom_storage.attempt_insert(ore, user)))
            return TRUE;

        if (!Dm.truthy(spam_protection))
        {
            balloon_alert(user, I18n.T("bag full!"));
            spam_protection = TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/storage/bags.dm:230
    public virtual void on_obj_entered(atom new_loc = null, atom_movable arrived = null, atom old_loc = null, params object[] __extra)
    {
        if (Dm.truthy(is_type_in_list(arrived, atom_storage.can_hold)) && !Dm.truthy(dropping_ores) && old_loc != loc)
            Waiting.Detach(pickup_ore(arrived as obj_item, listening_to));
    }

    // tg: code/game/objects/items/storage/bags.dm:235
    public virtual void on_atom_initialized_on(atom loc = null, atom new_atom = null)
    {
        if (Dm.truthy(is_type_in_list(new_atom, atom_storage.can_hold)))
            Waiting.Detach(pickup_ore(new_atom as obj_item, listening_to));
    }
}

// tg: code/datums/storage/subtypes/bags.dm:35
public partial class datum_storage_bag_ore : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:39
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_stack_ore));
    }
}

// Gaps J6 (D298): the quartermaster's garment bag.
// tg: code/game/objects/items/storage/garment.dm:39
public partial class obj_item_storage_bag_garment_quartermaster : obj_item_storage_bag_garment
{
    // tg: code/game/objects/items/storage/garment.dm:155
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_under_rank_cargo_qm>(this);
        Dm.@new<obj_item_clothing_under_rank_cargo_qm_skirt>(this);
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_cargo_qm>(this);
        Dm.@new<obj_item_clothing_suit_utility_fire_firefighter>(this);
        Dm.@new<obj_item_clothing_gloves_fingerless>(this);
        Dm.@new<obj_item_clothing_suit_jacket_quartermaster>(this);
        Dm.@new<obj_item_clothing_head_soft>(this);
        Dm.@new<obj_item_clothing_mask_gas>(this);
        Dm.@new<obj_item_clothing_neck_cloak_qm>(this);
        Dm.@new<obj_item_clothing_shoes_sneakers_brown>(this);
    }
}

// Gaps P2 (D298): the portable seed extractor (its context tip is a row, D264).
// tg: code/game/objects/items/storage/bags.dm:258
public partial class obj_item_storage_bag_plants_portaseeder : obj_item_storage_bag_plants
{
    // tg: code/game/objects/items/storage/bags.dm:264
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/storage/bags.dm:278
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Ctrl-click to activate seed extraction.")));
        return dot;
    }

    // tg: code/game/objects/items/storage/bags.dm:282
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        foreach (obj_item plant in Dm.each<obj_item>(contents))
            await seedify(plant, 1);
        return CLICK_ACTION_SUCCESS;
    }
}

// Gaps P2 (D298): the plant bag's storage.
// tg: code/datums/storage/subtypes/bags.dm:50
public partial class datum_storage_bag_plants : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:54
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_food_grown),
            typeof(obj_item_graft),
            typeof(obj_item_grown),
            typeof(obj_item_food_honeycomb),
            typeof(obj_item_seeds)));
    }
}

// Syndicate G4a (D316): the mail bag's storage.
// tg: code/datums/storage/subtypes/bags.dm:254
public partial class datum_storage_bag_mail : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:259
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_mail),
            typeof(obj_item_delivery_small),
            typeof(obj_item_paper),
            typeof(obj_item_bounty_cube)));
    }
}

// tg: code/game/objects/items/storage/bags.dm:513
public partial class obj_item_storage_bag_rebar_quiver : obj_item_storage_bag
{
    // tg: code/game/objects/items/storage/bags.dm:527
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/items/storage/bags.dm:531
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (actiontype is datum_action_item_action_reload_rebar)
            await reload_held_rebar(user);
        return null;
    }

    // tg: code/game/objects/items/storage/bags.dm:535
    public virtual async DmTask reload_held_rebar(mob user = null)
    {
        if (!Dm.truthy(contents.len))
        {
            user.balloon_alert(user, I18n.T("no bolts left!"));
            return;
        }
        obj held_item = user.get_active_held_item() as obj;
        if (held_item == null || held_item is not obj_item_gun_ballistic_rifle_rebarxbow held_crossbow)
        {
            user.balloon_alert(user, I18n.T("no held crossbow!"));
            return;
        }
        if (held_crossbow.magazine.contents.len >= held_crossbow.magazine.max_ammo)
        {
            user.balloon_alert(user, I18n.T("no more room!"));
            return;
        }
        if (!Dm.truthy(await do_after(user, held_crossbow.reload_time, user, timed_action_flags: held_crossbow.doafter_flags)))
            return;

        obj_item_ammo_casing_rebar ammo_to_load = contents[1] as obj_item_ammo_casing_rebar;
        await held_crossbow.item_interaction(user as mob_living, ammo_to_load);
    }
}

// tg: code/game/objects/items/storage/bags.dm:553
public partial class obj_item_storage_bag_rebar_quiver_syndicate : obj_item_storage_bag_rebar_quiver
{
    // tg: code/game/objects/items/storage/bags.dm:565
    public override async DmTask PopulateContents()
    {
        for (int to_fill = 1; to_fill <= 20; to_fill++)
            Dm.@new<obj_item_ammo_casing_rebar_syndie>(this);
    }

    // tg: code/game/objects/items/storage/bags.dm:569
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        switch (contents.len)
        {
            case 0:
                icon_state = "syndie_quiver_0";
                break;
            case >= 1 and <= 7:
                icon_state = "syndie_quiver_1";
                break;
            case >= 8 and <= 13:
                icon_state = "syndie_quiver_2";
                break;
            case >= 14 and <= 20:
                icon_state = "syndie_quiver_3";
                break;
        }
        return dot;
    }
}

///Rebar quiver bag
// tg: code/datums/storage/subtypes/bags.dm:237
public partial class datum_storage_bag_rebar_quiver : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:242
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(Dm.list(
            typeof(obj_item_ammo_casing_rebar)));
    }
}

// tg: code/game/objects/items/storage/bags.dm:438
public partial class obj_item_storage_bag_money : obj_item_storage_bag
{
    // tg: code/game/objects/items/storage/bags.dm:450
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(20))
            icon_state = "moneybagalt";
        return dot;
    }
}

// tg: code/game/objects/items/storage/bags.dm:27
public partial class obj_item_storage_bag_trash : obj_item_storage_bag
{
    // tg: code/game/objects/items/storage/bags.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        RegisterSignal(atom_storage, COMSIG_STORAGE_DUMP_POST_TRANSFER, nameof(post_insertion));
        return dot;
    }

    /// If you dump a trash bag into something, anything that doesn't get inserted will spill out onto your feet
    // tg: code/game/objects/items/storage/bags.dm:47
    [SignalHandler]
    public virtual void post_insertion(datum_storage source = null, atom dest_object = null, mob user = null)
    {
        // If there's no item in there, don't do anything
        if (Dm.locate<obj_item>(this) == null)
            return;

        // Otherwise, we're gonna dump into the dest object
        turf dump_onto = get_turf(dest_object);
        user.visible_message(
            span_notice(I18n.T("[] dumps the contents of [] all out on \\the []", user, this, dump_onto)),
            span_notice(I18n.T("The remaining trash in \\the [] falls out onto \\the []", this, dump_onto)));
        Waiting.NoSleep(source.remove_all(dump_onto));
    }

    // tg: code/game/objects/items/storage/bags.dm:61
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] puts [] over [] head and starts chomping at the insides! Disgusting!", user, this, user.p_their())));
        playsound(loc, Dm.resource("sound/items/eatfood.ogg"), 50, TRUE, -1);
        return TOXLOSS;
    }

    // tg: code/game/objects/items/storage/bags.dm:66
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        float len = contents.len;
        string initial_state = Dm.initial(this, (obj_item_storage_bag_trash p) => p.icon_state);
        if (len >= 20)
            icon_state = Dm.text("[]3", initial_state);
        else if (len >= 11)
            icon_state = Dm.text("[]2", initial_state);
        else if (len >= 1)
            icon_state = Dm.text("[]1", initial_state);
        else
            icon_state = Dm.text("[]", initial_state);
        return await base.update_icon_state(__extra);
    }
}

// tg: code/game/objects/items/storage/bags.dm:352
public partial class obj_item_storage_bag_tray : obj_item_storage_bag
{
    // tg: code/game/objects/items/storage/bags.dm:371
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living M = target_mob;
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        // Drop all the things. All of them.
        DmList oldContents = contents.Copy();
        await atom_storage.remove_all(user);
        // Make each item scatter a bit
        foreach (obj_item tray_item in Dm.each<obj_item>(oldContents))
            do_scatter(tray_item);

        if (prob(50))
            playsound(M, Dm.resource("sound/items/trayhit/trayhit1.ogg"), 50, TRUE);
        else
            playsound(M, Dm.resource("sound/items/trayhit/trayhit2.ogg"), 50, TRUE);

        if (ishuman(M))
        {
            if (prob(10))
                await M.Paralyze(40);
        }
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/storage/bags.dm:390
    public virtual void do_scatter(obj_item tray_item = null)
    {
        float delay = rand(2, 4);
        datum_move_loop loop = GLOB.move_manager.move_rand(tray_item, Dm.list(NORTH, SOUTH, EAST, WEST), delay, Dm.named("timeout", rand(1, 2) * delay), Dm.named("flags", MOVEMENT_LOOP_START_FAST)) as datum_move_loop;
        //This does mean scattering is tied to the tray. Not sure how better to handle it
        if (loop != null)
            RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(change_speed));
    }

    // tg: code/game/objects/items/storage/bags.dm:396
    [SignalHandler]
    public virtual void change_speed(datum_move_loop source = null)
    {
        float new_delay = rand(2, 4);
        float count = Dm.num(source.lifetime) / Dm.num(source.delay);
        source.lifetime = count * new_delay;
        source.delay = new_delay;
    }

    // tg: code/game/objects/items/storage/bags.dm:403
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        foreach (obj_item I in Dm.each<obj_item>(contents))
        {
            mutable_appearance I_copy = Dm.@new<mutable_appearance>(I);
            I_copy.plane = FLOAT_PLANE;
            I_copy.layer = FLOAT_LAYER;
            Dm.append(ref dot, I_copy);
        }
        return dot;
    }

    // tg: code/game/objects/items/storage/bags.dm:411
    public override async DmTask cyborg_unequip(mob user = null)
    {
        await base.cyborg_unequip(user);
        await atom_storage.remove_all(drop_location() as atom);
    }

    // tg: code/game/objects/items/storage/bags.dm:415
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/storage/bags.dm:419
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        update_appearance();
        return dot;
    }
}

///Trash Bag
// tg: code/datums/storage/subtypes/bags.dm:8
public partial class datum_storage_bag_trash : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:14
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(cant_hold_list: typeof(obj_item_disk_nuclear));
    }

    // tg: code/datums/storage/subtypes/bags.dm:18
    public override async DmTask<object> remove_single(mob removing = null, obj_item thing = null, atom remove_to_loc = null, float? silent = null)
    {
        real_location.visible_message(
            span_notice(I18n.T("[] starts fishing around inside [].", removing, parent)),
            span_notice(I18n.T("You start digging around in [] to try and pull something out.", parent)));
        if (!Dm.truthy(await do_after(removing, 1.5f * SECONDS, parent)))
            return FALSE;

        return await base.remove_single(removing, thing, remove_to_loc, silent);
    }
}

///Tray bag
// tg: code/datums/storage/subtypes/bags.dm:111
public partial class datum_storage_bag_tray : datum_storage_bag
{
    // tg: code/datums/storage/subtypes/bags.dm:117
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(
            can_hold_list: Dm.list(
                typeof(obj_item_cigarette),
                typeof(obj_item_food),
                typeof(obj_item_kitchen),
                typeof(obj_item_lighter),
                typeof(obj_item_organ),
                typeof(obj_item_plate),
                typeof(obj_item_reagent_containers_condiment),
                typeof(obj_item_reagent_containers_cup),
                typeof(obj_item_rollingpaper),
                typeof(obj_item_storage_box_gum),
                typeof(obj_item_storage_box_matches),
                typeof(obj_item_storage_fancy),
                typeof(obj_item_trash)),
            cant_hold_list: Dm.list(
                typeof(obj_item_plate_oven_tray),
                typeof(obj_item_reagent_containers_cup_soup_pot))); //Should cover: Bottles, Beakers, Bowls, Booze, Glasses, Food, Food Containers, Food Trash, Organs, Tobacco Products, Lighters, and Kitchen Tools.
    }
}

// Gaps SR1 (D333): the filled trash bag, the debug sheet snatcher, the vault's and the Dutchman's money bags, the harpoon quiver and
// the full, divine and endless quivers; the bags' storage kinds are in datums/storage/subtypes/bags.cs.

// tg: code/game/objects/items/storage/bags.dm:455
public partial class obj_item_storage_bag_money_vault : obj_item_storage_bag_money
{
    // tg: code/game/objects/items/storage/bags.dm:455
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_coin_silver>(this);
        Dm.@new<obj_item_coin_silver>(this);
        Dm.@new<obj_item_coin_silver>(this);
        Dm.@new<obj_item_coin_silver>(this);
        Dm.@new<obj_item_coin_gold>(this);
        Dm.@new<obj_item_coin_gold>(this);
        Dm.@new<obj_item_coin_adamantine>(this);
    }
}

// tg: code/game/objects/items/storage/bags.dm:465
public partial class obj_item_storage_bag_money_dutchmen : obj_item_storage_bag_money
{
    // tg: code/game/objects/items/storage/bags.dm:465
    public override async DmTask PopulateContents()
    {
        for (float iteration = 1; iteration <= 9; iteration++)
        {
            Dm.@new<obj_item_coin_silver_doubloon>(this);
        }
        for (float iteration = 1; iteration <= 9; iteration++)
        {
            Dm.@new<obj_item_coin_gold_doubloon>(this);
        }
        Dm.@new<obj_item_coin_adamantine_doubloon>(this);
    }
}

// tg: code/game/objects/items/storage/bags.dm:509
public partial class obj_item_storage_bag_harpoon_quiver : obj_item_storage_bag
{
    // tg: code/game/objects/items/storage/bags.dm:509
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 40; i++)
        {
            Dm.@new<obj_item_ammo_casing_harpoon>(this);
        }
    }
}

// tg: code/game/objects/items/storage/bags.dm:78
public partial class obj_item_storage_bag_trash_filled : obj_item_storage_bag_trash
{
    // tg: code/game/objects/items/storage/bags.dm:78
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        float count = rand(1, 7);
        for (float i = 1; i <= count; i++)
            Dm.@new<obj_effect_spawner_random_trash_garbage>(this);
        await update_icon_state();
    }
}

// tg: code/game/objects/items/storage/bags.dm:303
public partial class obj_item_storage_bag_sheetsnatcher_debug : obj_item_storage_bag_sheetsnatcher
{
    // Copy-pasted from the former /obj/item/storage/box/material, w/ small additions like rods, cardboard, plastic.
    // "Only 20 uranium 'cause of radiation"
    // tg: code/game/objects/items/storage/bags.dm:312
    public override async DmTask PopulateContents()
    {
        // amount should be null if it should spawn with the type's default amount
        DmList items_inside = Dm.list(
            Dm.kv(typeof(obj_item_stack_sheet_iron_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_glass_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_rglass_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_plasmaglass_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_titaniumglass_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_plastitaniumglass_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_plasteel_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_titanium_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_gold), 50),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_silver), 50),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_plasma), 50),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_uranium), 20),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_diamond), 50),
            Dm.kv(typeof(obj_item_stack_sheet_bluespace_crystal), 50),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_bananium), 50),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_wood_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_plastic_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_runed_metal_fifty), null),
            Dm.kv(typeof(obj_item_stack_rods_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_plastitanium), 50),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_abductor), 50),
            Dm.kv(typeof(obj_item_stack_sheet_mineral_sandbags_fifty), null),
            Dm.kv(typeof(obj_item_stack_sheet_cardboard_fifty), null));
        foreach (object stack_type in Dm.each(items_inside))
        {
            object amt = items_inside[stack_type];
            Dm.@new((Type)stack_type, this, amt, FALSE);
        }
    }
}

// tg: code/game/objects/items/storage/bags.dm:582
public partial class obj_item_storage_bag_quiver_full : obj_item_storage_bag_quiver
{
    // tg: code/game/objects/items/storage/bags.dm:597
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        for (float i = 1; i <= 10; i++)
            Dm.@new(arrow_path, this);
    }
}

// tg: code/game/objects/items/storage/bags.dm:602
public partial class obj_item_storage_bag_quiver_holy : obj_item_storage_bag_quiver
{
    // tg: code/game/objects/items/storage/bags.dm:610
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        for (float i = 1; i <= 10; i++)
            Dm.@new(arrow_path, this);
    }
}

// tg: code/game/objects/items/storage/bags.dm:615
public partial class obj_item_storage_bag_quiver_endless : obj_item_storage_bag_quiver
{
    // tg: code/game/objects/items/storage/bags.dm:620
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        Dm.@new(arrow_path, this);
    }
}
