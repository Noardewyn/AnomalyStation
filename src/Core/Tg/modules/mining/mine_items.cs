// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/mine_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole: the light emitters, the miners' lockers (their PopulateContents translated with the help of m4/p3w/popgen2.py), the
// mining shuttle console (its warnings to a head revolutionary, a free golem off the station and an exile), and the mine carts on
// their rails, which roll on when bumped, turn along the track, run down whoever lies in their way, keep their speed on a powered
// rail, stop at a powered brake and spill what they carry when they hit the end of the line.

/**********************Light************************/

//this item is intended to give the effect of entering the mine, so that light gradually fades. we also use the base effect for certain lighting effects while mapping.
// tg: code/modules/mining/mine_items.dm:4
public partial class obj_effect_light_emitter : obj_effect
{
    // tg: code/modules/mining/mine_items.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_light(set_luminosity, set_cap));
        return dot;
    }

    // tg: code/modules/mining/mine_items.dm:16
    public override DmTask<object> singularity_pull(atom singularity = null, object current_size = null) => DmTask<object>.FromResult(null);

    // tg: code/modules/mining/mine_items.dm:19
    public override DmTask<object> singularity_act() => DmTask<object>.FromResult(null);
}

/**********************Miner Lockers**************************/

// tg: code/modules/mining/mine_items.dm:39
public partial class obj_structure_closet_wardrobe_miner : obj_structure_closet_wardrobe
{
    // tg: code/modules/mining/mine_items.dm:39
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_storage_backpack_duffelbag_explorer>(this); // tg: code/modules/mining/mine_items.dm:40
        Dm.@new<obj_item_storage_backpack_explorer>(this); // tg: code/modules/mining/mine_items.dm:41
        Dm.@new<obj_item_storage_backpack_satchel_explorer>(this); // tg: code/modules/mining/mine_items.dm:42
        Dm.@new<obj_item_storage_backpack_messenger_explorer>(this); // tg: code/modules/mining/mine_items.dm:43
        Dm.@new<obj_item_clothing_under_rank_cargo_miner_lavaland>(this); // tg: code/modules/mining/mine_items.dm:44
        Dm.@new<obj_item_clothing_under_rank_cargo_miner_lavaland>(this); // tg: code/modules/mining/mine_items.dm:45
        Dm.@new<obj_item_clothing_under_rank_cargo_miner_lavaland>(this); // tg: code/modules/mining/mine_items.dm:46
        Dm.@new<obj_item_clothing_shoes_workboots_mining>(this); // tg: code/modules/mining/mine_items.dm:47
        Dm.@new<obj_item_clothing_shoes_workboots_mining>(this); // tg: code/modules/mining/mine_items.dm:48
        Dm.@new<obj_item_clothing_shoes_workboots_mining>(this); // tg: code/modules/mining/mine_items.dm:49
        Dm.@new<obj_item_clothing_gloves_color_black>(this); // tg: code/modules/mining/mine_items.dm:50
        Dm.@new<obj_item_clothing_gloves_color_black>(this); // tg: code/modules/mining/mine_items.dm:51
        Dm.@new<obj_item_clothing_gloves_color_black>(this); // tg: code/modules/mining/mine_items.dm:52
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_miner>(this); // tg: code/modules/mining/mine_items.dm:53
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_miner>(this); // tg: code/modules/mining/mine_items.dm:54
        Dm.@new<obj_item_clothing_suit_hooded_wintercoat_miner>(this); // tg: code/modules/mining/mine_items.dm:55
    }
}

// tg: code/modules/mining/mine_items.dm:65
public partial class obj_structure_closet_secure_closet_miner : obj_structure_closet_secure_closet
{
    // tg: code/modules/mining/mine_items.dm:65
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        Dm.@new<obj_item_stack_sheet_mineral_sandbags>(this, 5);
        Dm.@new<obj_item_storage_box_emptysandbags>(this);
        Dm.@new<obj_item_card_mining_point_card>(this);
        Dm.@new<obj_item_shovel>(this);
        Dm.@new<obj_item_pickaxe_mini>(this);
        Dm.@new<obj_item_radio_headset_headset_cargo_mining>(this);
        Dm.@new<obj_item_flashlight_seclite>(this);
        Dm.@new<obj_item_storage_bag_plants>(this);
        Dm.@new<obj_item_storage_bag_ore>(this);
        Dm.@new<obj_item_mining_scanner>(this);
        Dm.@new<obj_item_clothing_glasses_meson>(this);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_SMALLER_PODS))
            Dm.@new<obj_item_survivalcapsule_bathroom>(this);
        else
            Dm.@new<obj_item_survivalcapsule>(this);
        Dm.@new<obj_item_assault_pod_mining>(this);
    }

    // tg: code/modules/mining/mine_items.dm:85
    public override async DmTask populate_contents_immediate()
    {
        await base.populate_contents_immediate();

        Dm.@new<obj_item_gun_energy_recharge_kinetic_accelerator>(this);
    }
}

// tg: code/modules/mining/mine_items.dm:92
public partial class obj_machinery_computer_shuttle_mining : obj_machinery_computer_shuttle
{
    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/mining/mine_items.dm:101
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        // IS_HEAD_REVOLUTIONARY(user) (__DEFINES/antagonists.dm:326), expanded inline
        if (Dm.truthy(is_station_level(user.z)) && user.mind != null && Dm.truthy(user.mind.has_antag_datum(typeof(datum_antagonist_rev_head))) && !Dm.@in(user.mind, dumb_rev_heads))
        {
            to_chat(user, span_warning(I18n.T("You get a feeling that leaving the station might be a REALLY dumb idea...")));
            Dm.append(ref dumb_rev_heads, user.mind);
            return null;
        }

        if (HAS_TRAIT(user, TRAIT_FORBID_MINING_SHUTTLE_CONSOLE_OUTSIDE_STATION) && !Dm.truthy(is_station_level(user.z)))
        {
            to_chat(user, span_warning(I18n.T("You get the feeling you shouldn't mess with this.")));
            return null;
        }

        if (user is mob_living living_user)
        {
            foreach (obj_item_implant_exile exile_implant in Dm.each<obj_item_implant_exile>(living_user.implants))
            {
                to_chat(living_user, span_warning(I18n.T("A warning flashes across the screen, and the shuttle controls lock in response to your exile implant.")));
                return null;
            }
        }

        return await base.attack_hand(user, modifiers, __extra);
    }
}

// tg: code/modules/mining/mine_items.dm:157
public partial class obj_structure_closet_crate_miningcar : obj_structure_closet_crate
{
    // tg: code/modules/mining/mine_items.dm:174
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noisy_movement), Dm.resource("sound/effects/tank_treads.ogg"), 50));
        if (Dm.locate<obj_structure_minecart_rail>(loc) != null)
            update_rail_state(TRUE);
        return dot;
    }

    // tg: code/modules/mining/mine_items.dm:180
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        var dot = (DmList)await base.examine(user, __extra);
        if (Dm.truthy(on_rails))
            Dm.append(ref dot, span_notice(I18n.T("You can give this a bump to send it on its way, or drag it off the rails to drag it around.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("Drag this onto a mine cart rail to set it on its way.")));
        return dot;
    }

    // We don't want the locked crate overlay show up.
    // tg: code/modules/mining/mine_items.dm:188
    public override object closet_update_overlays(DmList new_overlays = null)
    {
        return null;
    }

    // tg: code/modules/mining/mine_items.dm:191
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        if (newloc == null)
            return await base.Move(newloc, direct, glide_size_override, update_dir);
        if (!Dm.truthy(on_rails))
            return await base.Move(newloc, direct, glide_size_override, update_dir);
        // Allows people to drag minecarts along the rails rather than solely shoving it
        if (Dm.truthy(can_travel_on_turf(get_turf(newloc), direct)))
            return await base.Move(newloc, direct, glide_size_override, update_dir);
        momentum = 0;
        return FALSE;
    }

    // tg: code/modules/mining/mine_items.dm:202
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!Dm.truthy(on_rails) || momentum <= 0)
            return dot;

        // Handling running OVER people
        foreach (mob_living smacked in Dm.each<mob_living>(loc))
        {
            if (!Dm.eq(smacked.body_position, LYING_DOWN))
                continue;
            if (momentum <= 8)
            {
                momentum = Dm.floor(momentum / 2);
                break;
            }
            await smack(smacked, 3, 1.5f);
            if (QDELETED(this))
                break;
        }
        return dot;
    }

    // tg: code/modules/mining/mine_items.dm:218
    public override async DmTask<object> is_buckle_possible(mob_living target = null, float? force = null, float? check_loc = null)
    {
        return Dm.truthy(opened) ? FALSE : await base.is_buckle_possible(target, force, check_loc);
    }

    // tg: code/modules/mining/mine_items.dm:221
    public override async DmTask after_open(mob_living user = null, float? force = null)
    {
        await base.after_open(user, force);
        unbuckle_all_mobs();
    }

    // Hack: If a mob is buckled onto the cart, bumping the cart will instead bump the mob (because higher layer)
    // So if we want to allow people to shove carts people are riding, we gotta check the mob for bumped and redirect it
    // tg: code/modules/mining/mine_items.dm:227
    public override async DmTask post_buckle_mob(mob_living buckled_mob = null)
    {
        RegisterSignal(buckled_mob, COMSIG_ATOM_BUMPED, nameof(buckled_bumped));
        RegisterSignal(buckled_mob, COMSIG_MOVABLE_BUMP_PUSHED, nameof(block_bump_push));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mining/mine_items.dm:231
    public override async DmTask post_unbuckle_mob(mob_living unbuckled_mob = null)
    {
        UnregisterSignal(unbuckled_mob, Dm.list(COMSIG_ATOM_BUMPED, COMSIG_MOVABLE_BUMP_PUSHED));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/mining/mine_items.dm:234
    [SignalHandler]
    public virtual void buckled_bumped(datum source = null, atom bumper = null)
    {
        Waiting.Detach(shove_off(bumper as atom_movable));
    }

    /**
     * Called when the minecart smacks into someone.
     *
     * * smacked - The mob that was smacked.
     * * damage_mod - How much to multiply the momentum by to get the damage.
     * * momentum_mod - How much to divide the momentum by after the smack.
     */
    // tg: code/modules/mining/mine_items.dm:245
    public virtual async DmTask smack(mob_living smacked = null, float? damage_mod = null, float? momentum_mod = null)
    {
        damage_mod ??= 2;
        momentum_mod ??= 2;
        // ASSERT(momentum_mod >= 1) (__DEFINES/assert.dm:4), expanded inline (rules 9.5)
        if (!(momentum_mod >= 1))
            throw Dm.CRASH("assertion failed: momentum_mod >= 1", "code/modules/mining/mine_items.dm", 246);
        if (!Dm.truthy(await smacked.apply_damage(damage_mod * momentum, BRUTE, BODY_ZONE_CHEST, wound_bonus: damage_mod * 10, attack_direction: dir)))
            return;
        if (Dm.num(get_integrity()) <= max_integrity * 0.05f)
        {
            smacked.visible_message(
                span_danger(I18n.T("[] smashes into [], breaking into pieces!", this, smacked)),
                span_userdanger(I18n.T("You are smacked by [] as it breaks into pieces!", this)));
            playsound(this, Dm.resource("sound/effects/break_stone.ogg"), 50, vary: TRUE);
            momentum = 0;
        }
        else
        {
            smacked.visible_message(
                span_danger(I18n.T("[] smashes into []!", this, smacked)),
                span_userdanger(I18n.T("You are smacked by []!", this)));
        }
        playsound(this, Dm.resource("sound/effects/bang.ogg"), 50, vary: TRUE);
        await take_damage(max_integrity * 0.05f);
        momentum = Dm.floor(momentum / Dm.num(momentum_mod));
        if (Dm.eq(smacked.body_position, LYING_DOWN))
        {
            await smacked.Paralyze(4 * SECONDS);
            return;
        }

        await smacked.Knockdown(5 * SECONDS);
        foreach (object side_dir in Dm.each(shuffle(GLOB.alldirs)))
        {
            // Don't throw people in front of the cart, and
            // don't throw people in any direction behind us
            if (Dm.eq(side_dir, dir) || Dm.truthy(Dm.bitand(side_dir, REVERSE_DIR(dir))))
                continue;
            var open_turf = get_step(this, Dm.num(side_dir)) as turf_open;
            if (open_turf == null)
                continue;
            await smacked.safe_throw_at(open_turf, 1, 3, spin: FALSE, gentle: TRUE);
        }
    }

    /**
     * Updates the state of the minecart to be on or off rails.
     */
    // tg: code/modules/mining/mine_items.dm:283
    public virtual void update_rail_state(object new_state = null)
    {
        if (Dm.eq(on_rails, new_state))
            return;
        on_rails = Dm.num(new_state);
        if (Dm.truthy(on_rails))
        {
            drag_slowdown = 0.5f;
            RegisterSignal(this, COMSIG_MOVABLE_BUMP_PUSHED, nameof(block_bump_push));
        }
        else
        {
            drag_slowdown = 2;
            UnregisterSignal(this, COMSIG_MOVABLE_BUMP_PUSHED);
        }
    }

    // We want a low move resistance so people can drag it along the tracks
    // But we also don't want people to nudge it with a push (since it requires a do_after to set off)
    // tg: code/modules/mining/mine_items.dm:296
    [SignalHandler]
    public virtual object block_bump_push(datum source = null, mob_living bumper = null, object force = null)
    {
        if (Dm.truthy(on_rails))
            return COMPONENT_NO_PUSH;
        if (Dm.num(force) < MOVE_FORCE_STRONG)
            return COMPONENT_NO_PUSH;
        return NONE;
    }

    // tg: code/modules/mining/mine_items.dm:304
    public override async DmTask<object> forceMove(atom destination = null, params object[] __extra)
    {
        update_rail_state(FALSE);
        return await base.forceMove(destination, __extra);
    }

    // tg: code/modules/mining/mine_items.dm:308
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (!isliving(user))
            return null;
        if (Dm.truthy(on_rails))
        {
            if (isopenturf(over))
                await try_take_off_rails((mob_living)user, (turf_open)over);
            return null;
        }

        if (over is obj_structure_minecart_rail || (isopenturf(over) && Dm.locate<obj_structure_minecart_rail>(over) != null))
        {
            await try_put_on_rails((mob_living)user, get_turf(over) as turf_open);
            return null;
        }
        return null;
    }

    /**
     * Attempt to remove the cart from rails
     *
     * * user - The user attempting to remove the cart from the rails.
     * * new_destination - The turf the cart will be moved to.
     */
    // tg: code/modules/mining/mine_items.dm:326
    public virtual async DmTask try_take_off_rails(mob_living user = null, turf_open new_destination = null)
    {
        balloon_alert(user, "removing from rails...");
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            return;
        update_rail_state(FALSE);
        await Move(new_destination);
        sound thud_sound = sound(Dm.resource("sound/items/weapons/thudswoosh.ogg"));
        thud_sound.pitch = 0.5f;
        playsound(this, thud_sound, 50, TRUE);
    }

    /**
     * Attempt to put the cart on rails
     *
     * * user - The user attempting to put the cart on the rails.
     * * new_destination - The turf the cart will be moved to.
     */
    // tg: code/modules/mining/mine_items.dm:342
    public virtual async DmTask try_put_on_rails(mob_living user = null, turf_open new_destination = null)
    {
        balloon_alert(user, "putting on rails...");
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            return;
        var set_rail = Dm.locate<obj_structure_minecart_rail>(new_destination);
        if (set_rail == null)
            return;
        await Move(new_destination);
        await setDir(set_rail.dir);
        update_rail_state(TRUE);
        sound click_sound = sound(Dm.resource("sound/machines/click.ogg"));
        click_sound.pitch = 0.5f;
        playsound(this, click_sound, 50, TRUE);
    }

    // tg: code/modules/mining/mine_items.dm:356
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        object dot = await base.Bump(bumped_atom);
        if (Dm.truthy(dot))
            return dot;

        // Handling running INTO people
        if (!isliving(bumped_atom) || momentum <= 0)
            return dot;
        if (momentum <= 8)
        {
            momentum = Dm.floor(momentum / 2);
            return dot;
        }
        await smack((mob_living)bumped_atom);
        return dot;
    }

    // tg: code/modules/mining/mine_items.dm:369
    public override object Bumped(atom_movable bumped_atom = null)
    {
        object dot = base.Bumped(bumped_atom);
        Waiting.Detach(shove_off(bumped_atom));
        return dot;
    }

    /// Starts the cart moving automatically.
    // tg: code/modules/mining/mine_items.dm:374
    public virtual async DmTask shove_off(atom_movable bumped_atom = null)
    {
        if (!Dm.truthy(on_rails) || momentum > 0)
            return;

        float movedir = bumped_atom.dir;
        turf next_turf = get_step(this, movedir);
        if (!Dm.truthy(can_travel_on_turf(next_turf, movedir)))
            return;

        if (isliving(bumped_atom))
        {
            var rail = Dm.locate<obj_structure_minecart_rail>(loc);
            var bumper = (mob_living)bumped_atom;
            if (bumper.mob_size <= MOB_SIZE_SMALL)
                return;
            if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(bumper, this)))
                return;
            balloon_alert(bumper, "setting off...");
            if (!Dm.truthy(await do_after(bumper, 1.5f * SECONDS, this)))
                return;
            if (QDELETED(rail) || !Dm.truthy(on_rails) || !Dm.truthy(can_travel_on_turf(next_turf, movedir)))
                return;
            momentum += 20;
        }
        else if (isitem(bumped_atom))
        {
            var bumped_item = (obj_item)bumped_atom;
            if (bumped_item.w_class <= WEIGHT_CLASS_SMALL)
                return;
            momentum += bumped_item.w_class;
        }
        else if (bumped_atom is obj_structure_closet_crate_miningcar bumped_car)
        {
            if (bumped_car.momentum <= 0)
                return;
            momentum += bumped_car.momentum;
            bumped_car.momentum = 0;
        }

        if (momentum <= 0)
            return;

        await setDir(movedir);
        var loop = GLOB.move_manager.move(this, dir, delay: calculate_delay(), subsystem: SSconveyors, flags: Dm.bitor(MOVEMENT_LOOP_START_INSTANT, MOVEMENT_LOOP_IGNORE_PRIORITY)) as datum_move_loop;
        RegisterSignal(loop, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(check_rail));
        RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(decay_momentum));
    }

    // tg: code/modules/mining/mine_items.dm:418
    [SignalHandler]
    public virtual object check_rail(datum_move_loop_move source = null)
    {
        if (momentum <= 0)
        {
            stack_trace("Mine cart moving on 0 momentum!", "code/modules/mining/mine_items.dm", 422);
            GLOB.move_manager.stop_looping(this, SSconveyors);
            return MOVELOOP_SKIP_STEP;
        }
        // Forced to not move
        if (Dm.truthy(anchored) || !Dm.truthy(has_gravity()))
            return MOVELOOP_SKIP_STEP;
        // Going straight
        if (Dm.truthy(can_travel_on_turf(get_step(this, dir))))
            return NONE;
        // Trying to turn
        foreach (object next_dir in Dm.each(shuffle(Dm.list(turn(dir, 90), turn(dir, -90)))))
        {
            if (!Dm.truthy(can_travel_on_turf(get_step(this, Dm.num(next_dir)), Dm.bitor(dir, Dm.num(next_dir)))))
                continue;
            momentum -= 1; // Extra cost for turning
            if (momentum <= 0)
                break;
            source.direction = Dm.num(next_dir);
            return NONE;
        }
        // Can't go straight and cant turn = STOP
        GLOB.move_manager.stop_looping(this, SSconveyors);
        if (momentum >= 8)
        {
            visible_message(span_warning(I18n.T("[] comes to a halt!", this)));
            Waiting.NoSleep(throw_contents());
        }
        else
        {
            visible_message(span_notice(I18n.T("[] comes to a slow stop.", this)));
        }
        momentum = 0;
        return MOVELOOP_SKIP_STEP;
    }

    // tg: code/modules/mining/mine_items.dm:450
    [SignalHandler]
    public virtual void decay_momentum(datum_move_loop_move source = null)
    {
        if (momentum > 0)
        {
            var stop_break = Dm.locate<obj_structure_minecart_rail_railbreak>(loc);
            var cable = Dm.locate<obj_structure_cable>(loc);
            // There is a break and it is powered, so STOP
            if (stop_break != null && Dm.truthy(cable?.avail(10 * KILO * JOULES)))
            {
                if (momentum >= 8)
                    visible_message(span_notice(I18n.T("[] comes to a sudden stop.", this)));
                else
                    visible_message(span_notice(I18n.T("[] comes to a stop.", this)));
                momentum = 0;
                GLOB.move_manager.stop_looping(this, SSconveyors);
                cable.add_delayedload(10 * KILO * JOULES);
                return;
            }
            // This is a powered rail, so maintain speed
            if (Dm.truthy(cable?.avail(1 * KILO * JOULES)))
            {
                // Speeds up the cart to 5 or 10, then stops decay
                if (momentum <= 5)
                {
                    momentum = 5;
                    cable.add_delayedload(0.5f * KILO * JOULES);
                }
                else if (momentum <= 10)
                {
                    momentum = 10;
                    cable.add_delayedload(1 * KILO * JOULES);
                }
                return;
            }
            // Here is where actual slowdown happens
            momentum -= 1;
        }

        // No more momentum = STOP
        if (momentum <= 0)
        {
            GLOB.move_manager.stop_looping(this, SSconveyors);
            visible_message(span_notice(I18n.T("[] comes to a slow stop.", this)));
            return;
        }

        // Handles slowing down the move loop / cart
        datum_move_loop loop = GLOB.move_manager.processing_on(this, SSconveyors);
        loop?.set_delay(calculate_delay());
    }

    /// Calculates how fast the cart is going
    // tg: code/modules/mining/mine_items.dm:490
    public virtual object calculate_delay()
    {
        return (-0.05f * SECONDS * momentum) + 1.1f * SECONDS;
    }

    /// Checks if we can travel on the passed turf
    // tg: code/modules/mining/mine_items.dm:494
    public virtual object can_travel_on_turf(turf next_turf = null, object dir_to_check = null)
    {
        dir_to_check ??= dir;
        foreach (obj_structure_minecart_rail rail in Dm.each<obj_structure_minecart_rail>(next_turf))
        {
            if (Dm.truthy(Dm.bitand(rail.dir, Dm.bitor(Dm.num(dir_to_check), REVERSE_DIR(Dm.num(dir_to_check))))))
                return TRUE;
        }

        return FALSE;
    }

    /// Throws all the contents of the cart out ahead
    // tg: code/modules/mining/mine_items.dm:502
    public virtual async DmTask throw_contents()
    {
        object was_open = opened;
        DmList to_yeet = contents.Copy();
        bool yeet_rider = Dm.truthy(has_buckled_mobs());
        if (yeet_rider)
        {
            Dm.append(ref to_yeet, buckled_mobs);
            unbuckle_all_mobs();
        }

        bust_open();
        if (!Dm.truthy(opened))
            return;

        if (!Dm.truthy(length(to_yeet)))
        {
            if (!Dm.truthy(was_open))
                visible_message(span_warning(I18n.T("[] breaks open!", this)));
            return;
        }

        float throw_distance = clamp(Dm.ceil(momentum / 3) - 4, 1, 5);
        var some_distant_turf = get_edge_target_turf(this, dir) as atom;
        foreach (atom_movable yeeten in Dm.each<atom_movable>(to_yeet))
            await yeeten.throw_at(some_distant_turf, throw_distance, 3, quickstart: TRUE);

        if (Dm.truthy(was_open))
            visible_message(span_warning(I18n.T("[] spills its contents!", this)));
        else
            // Update this message if someone allows multiple people to ride one minecart
            visible_message(span_warning(I18n.T("[] breaks open, spilling its contents[]!", this, yeet_rider ? I18n.T(" and throwing its rider") : "")));
    }
}

// tg: code/modules/mining/mine_items.dm:530
public partial class obj_structure_minecart_rail : obj_structure
{
    // tg: code/modules/mining/mine_items.dm:540
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_TURF_IGNORE_SLOWDOWN))));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep_override), Dm.named("footstep", FOOTSTEP_CATWALK)));
        foreach (obj_structure_closet_crate_miningcar cart in Dm.each<obj_structure_closet_crate_miningcar>(loc))
            cart.update_rail_state(TRUE);
        return dot;
    }

    // tg: code/modules/mining/mine_items.dm:547
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        var dot = (DmList)await base.examine(user, __extra);
        Dm.append(ref dot, rail_examine());
        return dot;
    }

    // tg: code/modules/mining/mine_items.dm:551
    public virtual object rail_examine()
    {
        return span_notice(I18n.T("Run a powered cable underneath it to power carts as they travel, maintaining their speed."));
    }
}

// tg: code/modules/mining/mine_items.dm:554
public partial class obj_structure_minecart_rail_railbreak : obj_structure_minecart_rail
{
    // tg: code/modules/mining/mine_items.dm:562
    public override object rail_examine()
    {
        return span_notice(I18n.T("Run a powered cable underneath it to stop carts that pass over it."));
    }
}
