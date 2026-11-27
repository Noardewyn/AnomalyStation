// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/xenobio_camera.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the slime management console with its camera eye (seen from and moved like the other camera consoles' eyes), its action
// buttons, its click shortcuts and the suction tube's visuals. The context tips are deferred (D264: live-partial stubs). The var
// blocks are generated (D111).

//Xenobio control console
// tg: code/modules/research/xenobiology/xenobio_camera.dm:2
public partial class mob_eye_camera_remote_xenobio : mob_eye_camera_remote
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:4  var/allowed_area = null
    // Declared here rather than generated: the converter types the name as a typepath (the aux base's eye gives its own
    // allowed_area one, construction_console_aux.dm:6), while this eye keeps its area's name in it.
    public string allowed_area = null;

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:3
    public mob_eye_camera_remote_xenobio()
    {
        visible_to_user = TRUE;
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:6
    [DmParams("mapload", "creator")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        area our_area = get_area(loc) as area;
        allowed_area = our_area.name;
        object dot = base.Initialize(mapload, __extra);
        return dot;
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:11
    public override void setLoc(object destination = null, float? force_update = null)
    {
        force_update ??= FALSE;
        area new_area = get_area(destination as atom) as area;

        if (new_area != null && Dm.eq(new_area.name, allowed_area) || new_area != null && Dm.truthy(Dm.bitand(new_area.area_flags, XENOBIOLOGY_COMPATIBLE)))
            base.setLoc(destination, force_update);
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:17
    public override object can_z_move(object direction = null, turf start = null, turf destination = null, float? z_move_flags = null, mob_living rider = null)
    {
        z_move_flags ??= NONE;
        object dot = base.can_z_move(direction, start, destination, z_move_flags, rider);
        if (!Dm.truthy(dot))
            return dot;
        area new_area = get_area(dot as atom) as area;
        if (!Dm.eq(new_area.name, allowed_area) && !Dm.truthy(Dm.bitand(new_area.area_flags, XENOBIOLOGY_COMPATIBLE)))
            return FALSE;
        return dot;
    }
}

// tg: code/modules/research/xenobiology/xenobio_camera.dm:28
public partial class obj_machinery_computer_camera_advanced_xenobio : obj_machinery_computer_camera_advanced
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:25  #define SUCTION_DELAY 1 DECISECONDS (file-local, #undef at :687)
    internal const float SUCTION_DELAY = 1 * DECISECONDS;
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:26  #define SUCTION_TIME 2 DECISECONDS (file-local, #undef at :686)
    internal const float SUCTION_TIME = 2 * DECISECONDS;

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref actions, Dm.@new<datum_action_innate_slime_place>(this));
        Dm.append(ref actions, Dm.@new<datum_action_innate_slime_pick_up>(this));
        Dm.append(ref actions, Dm.@new<datum_action_innate_feed_slime>(this));
        Dm.append(ref actions, Dm.@new<datum_action_innate_monkey_recycle>(this));
        Dm.append(ref actions, Dm.@new<datum_action_innate_slime_scan>(this));
        Dm.append(ref actions, Dm.@new<datum_action_innate_feed_potion>(this));
        Dm.append(ref actions, Dm.@new<datum_action_innate_hotkey_help>(this));

        stored_slimes = Dm.list();
        // register_context(): the context tips (D264)
        return dot;
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:64
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        foreach (obj_machinery_monkey_recycler recycler in Dm.each<obj_machinery_monkey_recycler>(GLOB.monkey_recyclers))
        {
            if (get_area(recycler) == get_area(this))
            {
                connected_recycler_ref = WEAKREF(recycler) as datum_weakref;
                break;
            }
        }
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:71
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref current_potion);
        foreach (object thing in Dm.each(stored_slimes))
        {
            mob_living_basic_slime stored_slime = thing as mob_living_basic_slime;
            Waiting.NoSleep(stored_slime.forceMove(drop_location() as atom));
        }
        stored_slimes.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:79  add_context: the context tips (D264, a live-partial stub)

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:95
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == current_potion)
            current_potion = null;
        if (Dm.@in(gone, stored_slimes))
            Dm.remove(ref stored_slimes, gone);
        return dot;
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:102
    public override async DmTask<object> CreateEye()
    {
        eyeobj = Dm.@new<mob_eye_camera_remote_xenobio>(get_turf(this), this);

        return TRUE;
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:107
    public override async DmTask GrantActions(mob_living user = null)
    {
        await base.GrantActions(user);
        RegisterSignal(user, COMSIG_MOB_CTRL_CLICKED, nameof(user_ctrl_click));
        RegisterSignal(user, COMSIG_MOB_ALTCLICKON, nameof(user_alt_click));
        RegisterSignal(user, COMSIG_CLICK_SHIFT, nameof(user_shift_click));
        if (user.hud_used == null)
            return;
        atom_movable_screen_xenobio_console xeno_hud = await user.hud_used.add_screen_object(typeof(atom_movable_screen_xenobio_console), HUD_XENOBIO_CONSOLE, update_screen: TRUE) as atom_movable_screen_xenobio_console;
        xeno_hud.on_update_hud(LAZYLEN(stored_slimes), stored_monkeys, max_slimes);
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:117
    public override async DmTask remove_eye_control(mob_living user = null)
    {
        UnregisterSignal(user, Dm.list(
            COMSIG_MOB_CTRL_CLICKED,
            COMSIG_MOB_ALTCLICKON,
            COMSIG_CLICK_SHIFT
        ));
        if (user.hud_used != null)
            await user.hud_used.remove_screen_object(HUD_XENOBIO_CONSOLE);
        await base.remove_eye_control(user);
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:126
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (tool is obj_item_slimepotion_slime)
            return await slimepotion_act(user, (obj_item_slimepotion_slime)tool);
        if (tool is obj_item_food_monkeycube)
            return monkeycube_act(user, (obj_item_food_monkeycube)tool);
        if (tool is obj_item_storage_bag || tool is obj_item_storage_box_monkeycubes)
            return storage_act(user, tool);
        return NONE;
    }

    /// Handles inserting a slime potion into the console, potentially swapping out an existing one.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:140
    public virtual async DmTask<object> slimepotion_act(mob_living user = null, obj_item_slimepotion_slime used_potion = null)
    {
        if (!Dm.truthy(await user.transferItemToLoc(used_potion, this)))
        {
            balloon_alert(user, I18n.T("can't insert!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!QDELETED(current_potion))
        {
            await try_put_in_hand(current_potion, user);
            balloon_alert(user, I18n.T("swapped"));
        }
        else
            balloon_alert(user, I18n.T("inserted"));

        current_potion = used_potion;
        atom_movable_screen_xenobio_console xeno_hud = user.hud_used?.screen_objects[HUD_XENOBIO_CONSOLE] as atom_movable_screen_xenobio_console;
        if (xeno_hud != null)
            xeno_hud.update_potion(current_potion);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Handles inserting a monkey cube into the console.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:158
    public virtual object monkeycube_act(mob_living user = null, obj_item_food_monkeycube used_cube = null)
    {
        stored_monkeys += 1;
        balloon_alert(user, I18n.T("[] cube[] stored", stored_monkeys, stored_monkeys == 1 ? "" : "s"));
        atom_movable_screen_xenobio_console xeno_hud = user.hud_used?.screen_objects[HUD_XENOBIO_CONSOLE] as atom_movable_screen_xenobio_console;
        if (xeno_hud != null)
            xeno_hud.on_update_hud(LAZYLEN(stored_slimes), stored_monkeys, max_slimes);
        qdel(used_cube);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Handles inserting any monkey cubes stored in the tool into the console.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:168
    public virtual object storage_act(mob_living user = null, obj_item tool = null)
    {
        bool loaded_any = false;
        foreach (obj storage_item in Dm.each<obj>(tool.contents))
        {
            if (storage_item is obj_item_food_monkeycube)
            {
                loaded_any = true;
                stored_monkeys += 1;
                qdel(storage_item);
            }
        }
        if (!loaded_any)
        {
            balloon_alert(user, I18n.T("no monkey cubes!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("[] cube[] stored", stored_monkeys, stored_monkeys == 1 ? "" : "s"));
        atom_movable_screen_xenobio_console xeno_hud = user.hud_used?.screen_objects[HUD_XENOBIO_CONSOLE] as atom_movable_screen_xenobio_console;
        if (xeno_hud != null)
            xeno_hud.on_update_hud(LAZYLEN(stored_slimes), stored_monkeys, max_slimes);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/xenobiology/xenobio_camera.dm:185
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multitool = tool as obj_item_multitool;
        if (!(multitool is obj_item_multitool)) // Needed as long as this uses a var on the multitool.
            return NONE;
        if (QDELETED(multitool.buffer))
        {
            balloon_alert(user, I18n.T("buffer empty!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!(multitool.buffer is obj_machinery_monkey_recycler))
        {
            balloon_alert(user, I18n.T("can only link recyclers!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("linked recycler"));
        connected_recycler_ref = WEAKREF(multitool.buffer) as datum_weakref;
        return ITEM_INTERACT_SUCCESS;
    }

    /// Validates whether the target turf can be interacted with.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:200
    public virtual object validate_turf(mob_living user = null, turf_open target_turf = null)
    {
        if (!SScameras.is_visible_by_cameras(target_turf))
        {
            target_turf.balloon_alert(user, I18n.T("outside of view!"));
            return FALSE;
        }

        area turfarea = get_area(target_turf) as area;
        mob_eye_camera_remote_xenobio remote_eye = user.remote_control as mob_eye_camera_remote_xenobio;
        if (!Dm.eq(turfarea.name, remote_eye.allowed_area) && !Dm.truthy(Dm.bitand(turfarea.area_flags, XENOBIOLOGY_COMPATIBLE)))
        {
            target_turf.balloon_alert(user, I18n.T("invalid area!"));
            return FALSE;
        }

        return TRUE;
    }

    ///Places every slime in storage on target turf
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:214
    public virtual async DmTask slime_place(turf_open target_turf = null, mob_living user = null)
    {
        spit_out(stored_slimes, target_turf);
        if (stored_slimes.len <= 0)
            return;

        if (stored_slimes.len == 1)
            target_turf.visible_message(span_notice(I18n.T("The slime is spat out!")));
        else
            target_turf.visible_message(span_notice(I18n.T("[] slimes are spat out!", stored_slimes.len)));

        foreach (mob_living_basic_slime stored_slime in Dm.each<mob_living_basic_slime>(stored_slimes))
        {
            await stored_slime.forceMove(target_turf);
            REMOVE_TRAIT(stored_slime, TRAIT_STASIS, XENOBIO_CONSOLE_TRAIT);
            stored_slime.handle_slime_stasis();
        }

        atom_movable_screen_xenobio_console xeno_hud = user.hud_used?.screen_objects[HUD_XENOBIO_CONSOLE] as atom_movable_screen_xenobio_console;
        if (xeno_hud != null)
            xeno_hud.on_update_hud(LAZYLEN(stored_slimes), stored_monkeys, max_slimes);
    }

    ///Places every slime not controlled by a player into the internal storage, respecting its limits
    ///Returns TRUE to signal it hitting the limit, in case its being called from a loop and we want it to stop
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:235
    public virtual async DmTask<object> slime_pickup(mob_living user = null, mob_living_basic_slime target_slime = null)
    {
        if (Dm.@in(target_slime, stored_slimes))
        {
            // It's possible for this proc to be called on a slime that's already being picked up,
            // so we need to check whether we already have to avoid duplicate entries.
            return FALSE;
        }

        if (stored_slimes.len >= max_slimes)
        {
            to_chat(user, span_warning(I18n.T("Slime storage is full.")));
            target_slime.balloon_alert(user, I18n.T("storage full"));
            return TRUE;
        }

        if (Dm.truthy(target_slime.ckey))
        {
            to_chat(user, span_warning(I18n.T("The slime wiggled free!")));
            return FALSE;
        }

        if (target_slime.buckled != null)
            target_slime.stop_feeding(silent: TRUE);
        target_slime.visible_message(span_notice(I18n.T("The slime gets sucked up!")));
        suck_up(target_slime);
        await target_slime.forceMove(this);
        Dm.append(ref stored_slimes, target_slime);
        ADD_TRAIT(target_slime, TRAIT_STASIS, XENOBIO_CONSOLE_TRAIT);

        atom_movable_screen_xenobio_console xeno_hud = user.hud_used?.screen_objects[HUD_XENOBIO_CONSOLE] as atom_movable_screen_xenobio_console;
        if (xeno_hud != null)
            xeno_hud.on_update_hud(LAZYLEN(stored_slimes), stored_monkeys, max_slimes);
        return FALSE;
    }

    ///Places one monkey, if possible
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:264
    public virtual async DmTask feed_slime(mob_living user = null, turf_open target_turf = null)
    {
        if (stored_monkeys < 1)
        {
            to_chat(user, span_warning(I18n.T("[] needs to have at least 1 monkey stored. Currently has [] monkeys stored.", this, stored_monkeys)));
            target_turf.balloon_alert(user, I18n.T("not enough monkeys"));
            return;
        }

        object cap = config.Get(typeof(datum_config_entry_number_monkeycap));
        if (LAZYLEN(datum_controller_subsystem_mobs.cubemonkeys) >= Dm.num(cap))
        {
            to_chat(user, span_warning(I18n.T("Bluespace harmonics prevent the creation of more than [] monkeys on the station at one time!", cap)));
            return;
        }

        mob_living_carbon_human_species_monkey food = Dm.@new<mob_living_carbon_human_species_monkey>(target_turf, null, TRUE);
        if (QDELETED(food))
            return;

        await food.apply_status_effect(typeof(datum_status_effect_slime_food), user);
        ADD_TRAIT(food, TRAIT_SPAWNED_MOB, INNATE_TRAIT);
        stored_monkeys--;
        stored_monkeys = round(stored_monkeys, 0.1f); //Prevents rounding errors
        spit_out(food, target_turf);

        atom_movable_screen_xenobio_console xeno_hud = user.hud_used?.screen_objects[HUD_XENOBIO_CONSOLE] as atom_movable_screen_xenobio_console;
        if (xeno_hud != null)
            xeno_hud.on_update_hud(LAZYLEN(stored_slimes), stored_monkeys, max_slimes);
    }

    /// Check whether we can recycle monkeys at all. Optionally, displays a balloon alert over a target atom for feedback.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:290
    [DmPrivate]
    public virtual object can_recycle_monkeys(mob_living user = null, atom target_atom = null)
    {
        obj_machinery_monkey_recycler connected_recycler = connected_recycler_ref?.resolve() as obj_machinery_monkey_recycler;
        if (connected_recycler == null)
        {
            to_chat(user, span_warning(I18n.T("There is no connected monkey recycler. Use a multitool to link one.")));
            if (target_atom != null)
                target_atom.balloon_alert(user, I18n.T("no recycler linked!"));
            return FALSE;
        }
        return TRUE;
    }

    /// Check whether we can recycle the target monkey. Optionally takes in a user to display errors to.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:301
    [DmPrivate]
    public virtual object can_recycle_target_monkey(mob_living_carbon_human target_human = null, mob_living user = null)
    {
        if (!ismonkey(target_human))
        {
            if (user != null)
                target_human.balloon_alert(user, I18n.T("not a monkey!"));
            return FALSE;
        }
        if (target_human.stat != DEAD)
        {
            if (user != null)
                target_human.balloon_alert(user, I18n.T("not dead!"));
            return FALSE;
        }
        return TRUE;
    }

    /// Attempts to recycle any monkeys on the targeted turf.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:314
    public virtual async DmTask<object> try_recycle_monkeys_on_turf(mob_living user = null, turf target_turf = null)
    {
        if (!Dm.truthy(can_recycle_monkeys(user, target_turf)))
            return FALSE;

        bool monkey_found = false;
        foreach (mob_living_carbon_human target_human in Dm.each<mob_living_carbon_human>(target_turf))
        {
            if (!Dm.truthy(can_recycle_target_monkey(target_human)))
                continue;
            await recycle_monkey(target_human, user);
            monkey_found = true;
        }

        return Dm.num(monkey_found);
    }

    /// Attempts to recycle the targeted human.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:328
    public virtual async DmTask<object> try_recycle_target_monkey(mob_living user = null, mob_living_carbon_human target_human = null, float? silence_errors = null)
    {
        silence_errors ??= FALSE;
        if (!Dm.truthy(can_recycle_target_monkey(target_human, user)))
            return FALSE;
        if (!Dm.truthy(can_recycle_monkeys(user, target_human)))
            return FALSE;

        await recycle_monkey(target_human, user);
        return TRUE;
    }

    /// Recycles a given monkey.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:338
    public virtual async DmTask recycle_monkey(mob_living_carbon_human target_monkey = null, mob_living user = null)
    {
        obj_machinery_monkey_recycler connected_recycler = connected_recycler_ref?.resolve() as obj_machinery_monkey_recycler;
        if (connected_recycler == null)
            return;

        suck_up(target_monkey);
        target_monkey.visible_message(span_notice(I18n.T("The monkey shoots up as [] reclaimed for recycling!", target_monkey.p_theyre())));
        await connected_recycler.use_energy(500 * JOULES);
        stored_monkeys += connected_recycler.cube_production;
        stored_monkeys = round(stored_monkeys, 0.1f); //Prevents rounding errors
        atom_movable_screen_xenobio_console xeno_hud = user.hud_used?.screen_objects[HUD_XENOBIO_CONSOLE] as atom_movable_screen_xenobio_console;
        if (xeno_hud != null)
            xeno_hud.on_update_hud(LAZYLEN(stored_slimes), stored_monkeys, max_slimes);
        qdel(target_monkey);
    }

    /// Handles console user alt-clicking, forwards to other procs based on target type.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:489
    [SignalHandler]
    public virtual object user_alt_click(mob_living user = null, atom target = null)
    {
        if (isslime(target))
        {
            alt_click_slime(user, (mob_living_basic_slime)target);
            return COMSIG_MOB_CANCEL_CLICKON;
        }
        return NONE;
    }

    ///Feeds a stored potion to a slime
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:498
    public virtual void alt_click_slime(mob_living user = null, mob_living_basic_slime target_slime = null)
    {
        turf_open slime_turf = get_turf(target_slime) as turf_open;
        if (!Dm.truthy(validate_turf(user, slime_turf)))
            return;

        if (QDELETED(current_potion))
        {
            to_chat(user, span_warning(I18n.T("No potion loaded.")));
            return;
        }

        spit_atom(current_potion, slime_turf);
        Waiting.Detach(current_potion.attack__proc(target_slime, user));
        atom_movable_screen_xenobio_console xeno_hud = user.hud_used?.screen_objects[HUD_XENOBIO_CONSOLE] as atom_movable_screen_xenobio_console;
        if (xeno_hud != null)
            xeno_hud.update_potion(current_potion);
    }

    /// Handles console user shift-clicking, forwards to other procs based on target type.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:514
    [SignalHandler]
    public virtual object user_shift_click(mob_living user = null, atom target = null)
    {
        if (isslime(target))
        {
            Waiting.NoSleep(shift_click_slime(user, (mob_living_basic_slime)target));
            return COMSIG_MOB_CANCEL_CLICKON;
        }
        else if (isopenturf(target))
        {
            Waiting.NoSleep(shift_click_turf(user, (turf_open)target));
            return COMSIG_MOB_CANCEL_CLICKON;
        }
        return NONE;
    }

    /// Picks up a slime, and places them in the internal storage.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:526
    public virtual async DmTask shift_click_slime(mob_living user = null, mob_living_basic_slime target_slime = null)
    {
        if (!Dm.truthy(validate_turf(user, get_turf(target_slime) as turf_open)))
            return;

        await slime_pickup(user, target_slime);
    }

    /// Places all slimes from the internal storage.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:533
    public virtual async DmTask shift_click_turf(mob_living user = null, turf_open target_turf = null)
    {
        if (!Dm.truthy(validate_turf(user, target_turf)))
            return;

        await slime_place(target_turf, user);
    }

    /// Handles console user ctrl-clicking, forwards to other procs based on target type.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:540
    [SignalHandler]
    public virtual object user_ctrl_click(mob_living user = null, atom target = null)
    {
        if (isopenturf(target))
        {
            Waiting.NoSleep(ctrl_click_turf(user, (turf_open)target));
            return COMSIG_MOB_CANCEL_CLICKON;
        }
        else if (ismonkey(target))
        {
            Waiting.NoSleep(ctrl_click_monkey(user, (mob_living_carbon_human)target));
            return COMSIG_MOB_CANCEL_CLICKON;
        }
        else if (isslime(target))
        {
            ctrl_click_slime(user, (mob_living_basic_slime)target);
            return COMSIG_MOB_CANCEL_CLICKON;
        }
        return NONE;
    }

    /// Attempts to recycle all monkeys on the turf, otherwise places a monkey from the internal storage.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:555
    public virtual async DmTask ctrl_click_turf(mob_living user = null, turf_open target_turf = null)
    {
        if (!Dm.truthy(validate_turf(user, target_turf)))
            return;

        // Attempt to recycle any monkeys on the turf first.
        if (Dm.truthy(await try_recycle_monkeys_on_turf(user, target_turf)))
            return;

        await feed_slime(user, target_turf);
    }

    /// Picks up a dead monkey for recycling.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:566
    public virtual async DmTask ctrl_click_monkey(mob_living user = null, mob_living_carbon_human target_human = null)
    {
        if (!Dm.truthy(validate_turf(user, get_turf(target_human) as turf_open)))
            return;

        await try_recycle_target_monkey(user, target_human);
    }

    /// Scans the target slime.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:573
    public virtual void ctrl_click_slime(mob_living user = null, mob_living_basic_slime target_slime = null)
    {
        if (!Dm.truthy(validate_turf(user, get_turf(target_slime) as turf_open)))
            return;

        slime_scan(target_slime, user);
    }

    /// Sucks the target mob up into the console.
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:580
    public virtual void suck_up(mob_living target_mob = null)
    {
        if (!isliving(target_mob))
            return;
        turf mobturf = get_turf(target_mob);
        Dm.@new<obj_effect_abstract_xenosuction>(mobturf);
        Dm.@new<obj_effect_abstract_sucked_atom>(mobturf, target_mob, TRUE);
        /// Make the mob invisible so it doesn't get seen during the animation
        target_mob.SetInvisibility(INVISIBILITY_MAXIMUM, id: XENOBIO_CONSOLE_TRAIT);
        _addtimer(Dm.@new<datum_callback>(target_mob, nameof(atom.RemoveInvisibility), XENOBIO_CONSOLE_TRAIT), SUCTION_DELAY + SUCTION_TIME, file: "code/modules/research/xenobiology/xenobio_camera.dm", line: 588);
        _addtimer(Dm.@new<datum_callback>(this, nameof(handle_xeno_sounds), mobturf, FALSE), SUCTION_DELAY, file: "code/modules/research/xenobiology/xenobio_camera.dm", line: 589);
    }

    /// Shoots the target mob(s) out of the console
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:593
    public virtual void spit_out(object mobs_to_spit = null, turf_open target_turf = null)
    {
        if (mobs_to_spit == null || target_turf == null)
            return;
        if (!islist(mobs_to_spit))
            mobs_to_spit = Dm.list(mobs_to_spit);
        if (!Dm.truthy(LAZYLEN(mobs_to_spit)))
            return;
        Dm.@new<obj_effect_abstract_xenosuction>(target_turf);
        foreach (mob_living shot_mob in Dm.each<mob_living>(mobs_to_spit))
        {
            Dm.@new<obj_effect_abstract_sucked_atom>(target_turf, shot_mob, FALSE);
            shot_mob.SetInvisibility(INVISIBILITY_MAXIMUM, id: XENOBIO_CONSOLE_TRAIT);
            _addtimer(Dm.@new<datum_callback>(shot_mob, nameof(atom.RemoveInvisibility), XENOBIO_CONSOLE_TRAIT), SUCTION_DELAY + SUCTION_TIME, file: "code/modules/research/xenobiology/xenobio_camera.dm", line: 604);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(handle_xeno_sounds), target_turf, TRUE), SUCTION_DELAY, file: "code/modules/research/xenobiology/xenobio_camera.dm", line: 605);
    }

    /// Shoots the target atom out of the tube. Used for anything that isn't a mob (I.e. potions)
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:608
    public virtual void spit_atom(atom_movable target_atom = null, turf_open target_turf = null)
    {
        if (target_atom == null)
            return;
        if (target_turf == null)
            target_turf = get_turf(target_atom) as turf_open;
        Dm.@new<obj_effect_abstract_xenosuction>(target_turf);
        bool ispot = target_atom is obj_item_slimepotion_slime;
        Dm.@new<obj_effect_abstract_sucked_atom>(target_turf, target_atom, FALSE, Dm.num(ispot));
        _addtimer(Dm.@new<datum_callback>(this, nameof(handle_xeno_sounds), target_turf, TRUE), SUCTION_DELAY, file: "code/modules/research/xenobiology/xenobio_camera.dm", line: 616);
        if (ispot)
            _addtimer(Dm.@new<datum_callback>(this, nameof(handle_shatter_sound), target_turf), SUCTION_DELAY + SUCTION_TIME, file: "code/modules/research/xenobiology/xenobio_camera.dm", line: 618);
    }

    ///Plays the sound in the given location. Easier to call w/ addtimer()
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:621
    public virtual void handle_xeno_sounds(turf_open target_turf = null, object spitting = null)
    {
        string tubesound = "sound/effects/compressed_air/air_suck.ogg";
        if (Dm.truthy(spitting))
            tubesound = "sound/effects/compressed_air/air_shoot.ogg";
        playsound(target_turf, tubesound, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
    }

    ///The sound that plays when a potion shatters. Easier to call w/ addtimer()
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:628
    public virtual void handle_shatter_sound(turf_open target_turf = null)
    {
        playsound(target_turf, SFX_SHATTER, 35, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
    }
}

// tg: code/modules/research/xenobiology/xenobio_camera.dm:353
public partial class datum_action_innate_slime_place : datum_action_innate
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:358
    public override async DmTask<object> Activate()
    {
        if (target == null || !isliving(owner))
            return null;
        mob_living living_owner = (mob_living)owner;
        turf_open eye_turf = get_turf(living_owner.remote_control) as turf_open;
        obj_machinery_computer_camera_advanced_xenobio xeno_console = target as obj_machinery_computer_camera_advanced_xenobio;

        if (!Dm.truthy(xeno_console.validate_turf(owner as mob_living, eye_turf)))
            return null;

        await xeno_console.slime_place(eye_turf, owner as mob_living);
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobio_camera.dm:370
public partial class datum_action_innate_slime_pick_up : datum_action_innate
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:375
    public override async DmTask<object> Activate()
    {
        if (target == null || !isliving(owner))
            return null;
        mob_living living_owner = (mob_living)owner;
        turf_open eye_turf = get_turf(living_owner.remote_control) as turf_open;
        obj_machinery_computer_camera_advanced_xenobio xeno_console = target as obj_machinery_computer_camera_advanced_xenobio;

        if (!Dm.truthy(xeno_console.validate_turf(living_owner, eye_turf)))
            return null;

        foreach (mob_living_basic_slime target_slime in Dm.each<mob_living_basic_slime>(eye_turf))
        {
            if (Dm.truthy(await xeno_console.slime_pickup(living_owner, target_slime))) ///Returns true if we hit our slime pickup limit
                break;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobio_camera.dm:389
public partial class datum_action_innate_feed_slime : datum_action_innate
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:394
    public override async DmTask<object> Activate()
    {
        if (target == null || !isliving(owner))
            return null;
        mob_living living_owner = (mob_living)owner;
        turf_open eye_turf = get_turf(living_owner.remote_control) as turf_open;
        obj_machinery_computer_camera_advanced_xenobio xeno_console = target as obj_machinery_computer_camera_advanced_xenobio;

        if (!Dm.truthy(xeno_console.validate_turf(living_owner, eye_turf)))
            return null;

        await xeno_console.feed_slime(living_owner, eye_turf);
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobio_camera.dm:407
public partial class datum_action_innate_monkey_recycle : datum_action_innate
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:412
    public override async DmTask<object> Activate()
    {
        if (target == null || !isliving(owner))
            return null;
        mob_living living_owner = (mob_living)owner;
        turf_open eye_turf = get_turf(living_owner.remote_control) as turf_open;
        obj_machinery_computer_camera_advanced_xenobio xeno_console = target as obj_machinery_computer_camera_advanced_xenobio;

        if (!Dm.truthy(xeno_console.validate_turf(living_owner, eye_turf)))
            return null;

        await xeno_console.try_recycle_monkeys_on_turf(living_owner, eye_turf);
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobio_camera.dm:424
public partial class datum_action_innate_slime_scan : datum_action_innate
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:429
    public override async DmTask<object> Activate()
    {
        if (target == null || !isliving(owner))
            return null;
        mob_living living_owner = (mob_living)owner;
        turf_open eye_turf = get_turf(living_owner.remote_control) as turf_open;
        obj_machinery_computer_camera_advanced_xenobio xeno_console = target as obj_machinery_computer_camera_advanced_xenobio;

        if (!Dm.truthy(xeno_console.validate_turf(living_owner, eye_turf)))
            return null;

        foreach (mob_living_basic_slime scanned_slime in Dm.each<mob_living_basic_slime>(eye_turf))
            slime_scan(scanned_slime, living_owner);
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobio_camera.dm:442
public partial class datum_action_innate_feed_potion : datum_action_innate
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:447
    public override async DmTask<object> Activate()
    {
        if (target == null || !isliving(owner))
            return null;

        mob_living living_owner = (mob_living)owner;
        turf_open eye_turf = get_turf(living_owner.remote_control) as turf_open;
        obj_machinery_computer_camera_advanced_xenobio xeno_console = target as obj_machinery_computer_camera_advanced_xenobio;

        if (!Dm.truthy(xeno_console.validate_turf(owner as mob_living, eye_turf)))
            return null;

        if (QDELETED(xeno_console.current_potion))
        {
            to_chat(owner, span_warning(I18n.T("No potion loaded.")));
            return null;
        }

        foreach (mob_living_basic_slime potioned_slime in Dm.each<mob_living_basic_slime>(eye_turf))
        {
            xeno_console.spit_atom(xeno_console.current_potion, eye_turf);
            await xeno_console.current_potion.interact_with_slime(potioned_slime, living_owner);
            atom_movable_screen_xenobio_console xeno_hud = owner.hud_used?.screen_objects[HUD_XENOBIO_CONSOLE] as atom_movable_screen_xenobio_console;
            if (xeno_hud != null)
                xeno_hud.update_potion(xeno_console.current_potion);
            break;
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/xenobio_camera.dm:470
public partial class datum_action_innate_hotkey_help : datum_action_innate
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:475
    public override async DmTask<object> Activate()
    {
        if (target == null || !isliving(owner))
            return null;

        DmList render_list = Dm.list();
        Dm.append(ref render_list, I18n.T("<b>Click shortcuts:</b>"));
        Dm.append(ref render_list, I18n.T("&bull; Shift-click a slime to pick it up, or the floor to drop all held slimes."));
        Dm.append(ref render_list, I18n.T("&bull; Ctrl-click a slime to scan it."));
        Dm.append(ref render_list, I18n.T("&bull; Alt-click a slime to feed it a potion."));
        Dm.append(ref render_list, I18n.T("&bull; Ctrl-click or a dead monkey to recycle it, or the floor to place a new monkey."));

        to_chat(owner, boxed_message(jointext(render_list, "\n")));
        return null;
    }
}

/// An abstract effect to simulate sucking the atom up or spitting it out
// tg: code/modules/research/xenobiology/xenobio_camera.dm:632
public partial class obj_effect_abstract_sucked_atom : obj_effect_abstract
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:638
    [DmParams("mapload", "copying", "sucking", "shatter")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom_movable copying = Dm.arg(__extra, 0) as atom_movable;
        object sucking = Dm.arg(__extra, 1) ?? FALSE;
        object shatter = Dm.arg(__extra, 2) ?? FALSE;
        object dot = base.Initialize(mapload, __extra);
        if (!ismovable(copying))
            return dot;
        appearance = copying.appearance;
        mob_initial_alpha = Dm.num(copying.alpha);
        layer = MOB_LAYER;
        if (Dm.truthy(sucking))
            suck_up();
        else
        {
            pixel_y = 64;
            alpha = 0;
            shoot_out(shatter);
        }
        return dot;
    }

    /// Shoots the mob visual upwards into the pipe then deletes it
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:653
    public virtual void suck_up()
    {
        float t__1 = obj_machinery_computer_camera_advanced_xenobio.SUCTION_DELAY + obj_machinery_computer_camera_advanced_xenobio.SUCTION_TIME;
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), t__1 > GC_FILTER_QUEUE ? WEAKREF(this) : this), t__1, file: "code/modules/research/xenobiology/xenobio_camera.dm", line: 654);
        animate(this, Dm.named("time", obj_machinery_computer_camera_advanced_xenobio.SUCTION_DELAY));
        animate(Dm.named("time", obj_machinery_computer_camera_advanced_xenobio.SUCTION_TIME), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_IN)), Dm.named("pixel_y", 64), Dm.named("alpha", 0));
    }

    /// Shoots the mob visual out then deletes it
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:659
    public virtual void shoot_out(object shatter = null)
    {
        float t__1 = obj_machinery_computer_camera_advanced_xenobio.SUCTION_DELAY + obj_machinery_computer_camera_advanced_xenobio.SUCTION_TIME;
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), t__1 > GC_FILTER_QUEUE ? WEAKREF(this) : this), t__1, file: "code/modules/research/xenobiology/xenobio_camera.dm", line: 660);
        animate(this, Dm.named("time", obj_machinery_computer_camera_advanced_xenobio.SUCTION_DELAY), Dm.named("flags", ANIMATION_PARALLEL));
        animate(Dm.named("time", obj_machinery_computer_camera_advanced_xenobio.SUCTION_TIME), Dm.named("easing", Dm.truthy(shatter) ? LINEAR_EASING : BOUNCE_EASING), Dm.named("pixel_y", 0), Dm.named("flags", ANIMATION_PARALLEL));

        animate(this, Dm.named("time", obj_machinery_computer_camera_advanced_xenobio.SUCTION_DELAY), Dm.named("flags", ANIMATION_PARALLEL));
        animate(Dm.named("time", obj_machinery_computer_camera_advanced_xenobio.SUCTION_TIME), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_OUT)), Dm.named("alpha", mob_initial_alpha), Dm.named("flags", ANIMATION_PARALLEL));
    }
}

/// The tube that sucks up/spits out the mob
// tg: code/modules/research/xenobiology/xenobio_camera.dm:669
public partial class obj_effect_abstract_xenosuction : obj_effect_abstract
{
    // tg: code/modules/research/xenobiology/xenobio_camera.dm:678
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_overlay(mutable_appearance(icon, "xenotube_fore", layer: ABOVE_MOB_LAYER));
        float t__1 = obj_machinery_computer_camera_advanced_xenobio.SUCTION_DELAY * 2 + obj_machinery_computer_camera_advanced_xenobio.SUCTION_TIME;
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), t__1 > GC_FILTER_QUEUE ? WEAKREF(this) : this), t__1, file: "code/modules/research/xenobiology/xenobio_camera.dm", line: 681);
        animate(this, Dm.named("time", obj_machinery_computer_camera_advanced_xenobio.SUCTION_DELAY), Dm.named("alpha", 255), Dm.named("pixel_y", 32));
        animate(Dm.named("time", obj_machinery_computer_camera_advanced_xenobio.SUCTION_TIME));
        animate(Dm.named("time", obj_machinery_computer_camera_advanced_xenobio.SUCTION_DELAY), Dm.named("alpha", 0), Dm.named("pixel_y", 48));
        return dot;
    }
}
