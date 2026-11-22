// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/linear_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z, elevators): the whole file.

///coordinate and control movement across linked transport_controllers. allows moving large single multitile platforms and many 1 tile platforms.
///also is capable of linking platforms across linked z levels
// tg: code/modules/transport/linear_controller.dm:6
public partial class datum_transport_controller_linear : datum_transport_controller
{
    /// If anyone changes the hydraulic sound effect I sure hope they update this...
    // tg: code/modules/transport/linear_controller.dm:2  #define HYDRAULIC_SFX_DURATION (2 SECONDS) (file-local, rules 11)
    private const float HYDRAULIC_SFX_DURATION = 2 * SECONDS;

    /// Typepath list of what to ignore smashing through, controls all lifts
    // tg: code/modules/transport/linear_controller.dm:12  var/static/list/ignored_smashthroughs (rules 10.6: a TgState field, below)
    public static ref DmList ignored_smashthroughs => ref TgState.Current.datum_transport_controller_linear__ignored_smashthroughs;

    // tg: code/modules/transport/linear_controller.dm:42
    [DmParams("transport_module")]
    public override async DmTask New(params object[] __extra)
    {
        obj_structure_transport_linear transport_module = Dm.arg<obj_structure_transport_linear>(__extra, 0);
        transport_id = transport_module.transport_id;
        create_modular_set = transport_module.create_modular_set;

        await link_transport_modules(transport_module);
        ignored_smashthroughs = typecacheof(ignored_smashthroughs) as DmList;

        LAZYADDASSOCLIST(ref SStransport.transports_by_type, transport_id, this);

        foreach (obj_structure_transport_linear lift in Dm.each<obj_structure_transport_linear>(transport_modules))
            lift.add_initial_contents();
    }

    // tg: code/modules/transport/linear_controller.dm:54
    public override object Destroy(float? force = null)
    {
        foreach (obj_structure_transport_linear transport_module in Dm.each<obj_structure_transport_linear>(transport_modules))
            transport_module.transport_controller_datum = null;
        transport_modules = null;

        LAZYREMOVEASSOC(ref SStransport.transports_by_type, transport_id, this);
        if (SStransport.transports_by_type == null)
            SStransport.transports_by_type = Dm.list();

        return base.Destroy(force);
    }

    // tg: code/modules/transport/linear_controller.dm:65
    public virtual async DmTask add_transport_modules(obj_structure_transport_linear new_transport_module = null)
    {
        if (Dm.@in(new_transport_module, transport_modules))
            return;
        foreach (obj_structure_transport_linear other_module in Dm.each<obj_structure_transport_linear>(new_transport_module.loc))
        {
            if (other_module != new_transport_module)
            {
                stack_trace("there is more than one transport module on a tile when a controller adds it. this causes problems", "code/modules/transport/linear_controller.dm", 70);
                qdel(other_module);
            }
        }

        new_transport_module.transport_controller_datum = this;
        LAZYADD(ref transport_modules, new_transport_module);
        RegisterSignal(new_transport_module, COMSIG_QDELETING, nameof(remove_transport_modules));

        check_for_landmarks(new_transport_module);

        if (Dm.truthy(z_sorted))//make sure we dont lose z ordering if we get additional platforms after init
            await order_platforms_by_z_level();
    }

    // tg: code/modules/transport/linear_controller.dm:82
    public virtual void remove_transport_modules(obj_structure_transport_linear old_transport_module = null)
    {
        if (!Dm.@in(old_transport_module, transport_modules))
            return;

        old_transport_module.transport_controller_datum = null;
        LAZYREMOVE(ref transport_modules, old_transport_module);
        UnregisterSignal(old_transport_module, COMSIG_QDELETING);
        if (!Dm.truthy(length(transport_modules)))
            qdel(this);
    }

    ///Collect all bordered platforms via a simple floodfill algorithm. allows multiz trams because its funny
    // tg: code/modules/transport/linear_controller.dm:95
    public virtual async DmTask link_transport_modules(obj_structure_transport_linear base_transport_module = null)
    {
        await add_transport_modules(base_transport_module);
        DmList possible_expansions = Dm.list(base_transport_module);

        while (Dm.truthy(possible_expansions.len))
        {
            foreach (obj_structure_transport_linear borderline in Dm.each<obj_structure_transport_linear>(possible_expansions.Copy()))
            {
                DmList result = borderline.module_adjacency(this) as DmList;
                if (Dm.truthy(length(result)))
                {
                    foreach (obj_structure_transport_linear transport_module in Dm.each<obj_structure_transport_linear>(result))
                    {
                        if (Dm.truthy(transport_modules.Find(transport_module)))
                            continue;

                        await add_transport_modules(transport_module);
                        Dm.combine(ref possible_expansions, transport_module);
                    }
                }

                Dm.remove(possible_expansions, borderline);
            }
        }
    }

    ///check for any landmarks placed inside the locs of the given transport_module
    // tg: code/modules/transport/linear_controller.dm:113
    public virtual void check_for_landmarks(obj_structure_transport_linear new_transport_module = null)
    {
        foreach (turf platform_loc in Dm.each<turf>(new_transport_module.locs))
        {
            obj_effect_landmark_transport_transport_id id_giver = Dm.locate<obj_effect_landmark_transport_transport_id>(platform_loc);

            if (id_giver != null)
                set_info_from_id_landmark(id_giver);
        }
    }

    ///set vars and such given an overriding transport_id landmark
    // tg: code/modules/transport/linear_controller.dm:123
    public virtual void set_info_from_id_landmark(obj_effect_landmark_transport_transport_id landmark = null)
    {
        if (landmark is not obj_effect_landmark_transport_transport_id)//transport_controller subtypes can want differnet id's than the base type wants
            return;

        if (Dm.truthy(landmark.specific_transport_id))
            specific_transport_id = landmark.specific_transport_id;

        qdel(landmark);
    }

    ///orders the lift platforms in order of lowest z level to highest z level.
    // tg: code/modules/transport/linear_controller.dm:135
    public virtual async DmTask order_platforms_by_z_level()
    {
        //contains nested lists for every z level in the world. why? because its really easy to sort
        DmList platforms_by_z = Dm.list();
        platforms_by_z.len = (int)Dm.num(world.maxz);

        for (int z = 1; z <= Dm.num(world.maxz); z++)
            platforms_by_z[z] = Dm.list();

        foreach (obj_structure_transport_linear transport_module in Dm.each<obj_structure_transport_linear>(transport_modules.Copy()))
        {
            if (QDELETED(transport_module) || !Dm.truthy(transport_module.z))
            {
                Dm.remove(transport_modules, transport_module);
                continue;
            }

            DmList z_list__1 = (DmList)platforms_by_z[(int)transport_module.z];
            Dm.append(ref z_list__1, transport_module);
        }

        if (Dm.truthy(create_modular_set))
        {
            foreach (DmList z_list in Dm.each<DmList>(platforms_by_z))
            {
                if (!Dm.truthy(length(z_list)))
                    continue;

                await create_modular_set_for_z_level(z_list);//this will subtract all but one platform from the list
            }
        }

        DmList output = Dm.list();

        foreach (DmList z_list in Dm.each<DmList>(platforms_by_z))
            Dm.append(ref output, z_list);

        transport_modules = output;

        z_sorted = TRUE;
    }

    ///goes through all platforms in the given list and finds the one in the lower left corner
    // tg: code/modules/transport/linear_controller.dm:167
    public virtual async DmTask create_modular_set_for_z_level(DmList platforms_in_z = null)
    {
        float min_x = INFINITY;
        float max_x = 0;

        float min_y = INFINITY;
        float max_y = 0;

        float z = 0;

        foreach (obj_structure_transport_linear module_to_sort in Dm.each<obj_structure_transport_linear>(platforms_in_z.Copy()))
        {
            if (z == 0)
            {
                if (!Dm.truthy(module_to_sort.z))
                {
                    stack_trace("create_modular_set_for_z_level() was given a platform in nullspace or not on a turf!", "code/modules/transport/linear_controller.dm", 179);
                    Dm.remove(platforms_in_z, module_to_sort);
                    continue;
                }

                z = module_to_sort.z;
            }

            if (z != module_to_sort.z)
            {
                stack_trace("create_modular_set_for_z_level() was given lifts on different z levels!", "code/modules/transport/linear_controller.dm", 186);
                Dm.remove(platforms_in_z, module_to_sort);
                continue;
            }

            min_x = Dm.min(min_x, module_to_sort.x);
            max_x = Dm.max(max_x, module_to_sort.x);

            min_y = Dm.min(min_y, module_to_sort.y);
            max_y = Dm.max(max_y, module_to_sort.y);
        }

        turf lower_left_corner_loc = locate(min_x, min_y, z);
        if (lower_left_corner_loc == null)
            throw Dm.CRASH("was unable to find a turf at the lower left corner of this z", "code/modules/transport/linear_controller.dm", 198);

        obj_structure_transport_linear lower_left_corner_transport = Dm.locate<obj_structure_transport_linear>(lower_left_corner_loc);

        if (lower_left_corner_transport == null)
            throw Dm.CRASH("there was no transport in the lower left corner of the given transport", "code/modules/transport/linear_controller.dm", 203);

        platforms_in_z.Cut();
        Dm.append(ref platforms_in_z, lower_left_corner_transport);//we want to change the list given to us not create a new one. so we do this

        await lower_left_corner_transport.create_modular_set__proc(min_x, min_y, max_x, max_y, z);
    }

    ///returns the closest transport to the specified atom, prioritizing transports on the same z level. used for comparing distance
    // tg: code/modules/transport/linear_controller.dm:211
    public virtual object return_closest_platform_to(atom comparison = null, float? allow_multiple_answers = null)
    {
        allow_multiple_answers ??= FALSE;
        if (comparison is not atom || !Dm.truthy(comparison.z))
            return FALSE;

        DmList candidate_platforms = Dm.list();

        foreach (obj_structure_transport_linear platform in Dm.each<obj_structure_transport_linear>(transport_modules))
        {
            if (Dm.eq(platform.z, comparison.z))
                Dm.append(ref candidate_platforms, platform);
        }

        obj_structure_transport_linear winner = candidate_platforms[1] as obj_structure_transport_linear;
        object winner_distance = get_dist(comparison, winner);

        DmList tied_winners = Dm.list(winner);

        foreach (obj_structure_transport_linear platform_to_sort in Dm.each<obj_structure_transport_linear>(candidate_platforms))
        {
            object platform_distance = get_dist(comparison, platform_to_sort);

            if (Dm.num(platform_distance) < Dm.num(winner_distance))
            {
                winner = platform_to_sort;
                winner_distance = platform_distance;

                if (Dm.truthy(allow_multiple_answers))
                    tied_winners = Dm.list(winner);
            }
            else if (Dm.eq(platform_distance, winner_distance) && Dm.truthy(allow_multiple_answers))
            {
                Dm.append(ref tied_winners, platform_to_sort);
            }
        }

        if (Dm.truthy(allow_multiple_answers))
            return tied_winners;

        return winner;
    }

    /// Returns a platform on the z-level which is vertically closest to the passed target_z
    // tg: code/modules/transport/linear_controller.dm:245
    public virtual object return_closest_platform_to_z(object target_z = null)
    {
        obj_structure_transport_linear found_platform = null;
        foreach (obj_structure_transport_linear lift in Dm.each<obj_structure_transport_linear>(transport_modules))
        {
            // Already at the same Z-level, we can stop
            if (Dm.eq(lift.z, target_z))
            {
                found_platform = lift;
                break;
            }

            // Set up an initial lift to compare to
            if (found_platform == null)
            {
                found_platform = lift;
                continue;
            }

            // Same level, we can go with the one we currently have
            if (Dm.eq(lift.z, found_platform.z))
                continue;

            // If the difference between the current found platform and the target
            // if less than the distance between the next lift and the target,
            // our current platform is closer to the target than the next one, so we can skip it
            if (Dm.abs(found_platform.z - Dm.num(target_z)) < Dm.abs(lift.z - Dm.num(target_z)))
                continue;

            // The difference is smaller for this lift, so it's closer
            found_platform = lift;
        }

        return found_platform;
    }

    /// Returns a list of all the z-levels our transport is currently on.
    // tg: code/modules/transport/linear_controller.dm:274
    public virtual object get_zs_we_are_on()
    {
        DmList zs_we_are_present_on = Dm.list();
        foreach (obj_structure_transport_linear lift in Dm.each<obj_structure_transport_linear>(transport_modules))
            Dm.combine(ref zs_we_are_present_on, lift.z);
        return zs_we_are_present_on;
    }

    ///returns all transport modules associated with this transport on the given z level or given atoms z level
    // tg: code/modules/transport/linear_controller.dm:281
    public virtual object get_platforms_on_level(atom atom_reference_OR_z_level_number = null)
    {
        object z = atom_reference_OR_z_level_number;
        if (isatom(atom_reference_OR_z_level_number))
            z = atom_reference_OR_z_level_number.z;

        if (!isnum(z) || Dm.num(z) < 0 || Dm.num(z) > Dm.num(world.maxz))
            return null;

        DmList platforms_in_z = Dm.list();

        foreach (obj_structure_transport_linear lift_to_check in Dm.each<obj_structure_transport_linear>(transport_modules))
        {
            if (Dm.truthy(lift_to_check.z))
                Dm.append(ref platforms_in_z, lift_to_check);
        }

        return platforms_in_z;
    }

    /**
     * Moves the platform UP or DOWN, this is what users invoke with their hand.
     * This is a SAFE proc, ensuring every part of it moves SANELY.
     */
    // tg: code/modules/transport/linear_controller.dm:305
    public virtual async DmTask move_lift_vertically(object going = null, mob user = null)
    {
        //transport_modules are sorted in order of lowest z to highest z, so going upwards we need to move them in reverse order to not collide
        if (Dm.eq(going, UP))
        {
            obj_structure_transport_linear platform_to_move;
            int current_index = (int)length(transport_modules);

            while (current_index > 0)
            {
                platform_to_move = transport_modules[current_index] as obj_structure_transport_linear;
                current_index--;

                await platform_to_move.travel(going);
            }
        }
        else if (Dm.eq(going, DOWN))
        {
            foreach (obj_structure_transport_linear transport_module in Dm.each<obj_structure_transport_linear>(transport_modules.Copy()))
                await transport_module.travel(going);
        }
        // DEVIATION(DV371): arrived: the announced ride ends with the level change, in the same tick
        foreach (obj_structure_transport_linear transport_module in Dm.each<obj_structure_transport_linear>(transport_modules))
        {
            LevelRides.End(transport_module);
            foreach (atom_movable rider in Dm.each<atom_movable>(transport_module.transport_contents))
                LevelRides.End(rider);
        }
    }

    // DEVIATION(DV371): the clients are told the ride when it starts, so they draw the car and its riders crossing the level over the wait
    private void announce_ride(object direction, float deciseconds)
    {
        int dz = Dm.eq(direction, UP) ? 1 : Dm.eq(direction, DOWN) ? -1 : 0;
        foreach (obj_structure_transport_linear transport_module in Dm.each<obj_structure_transport_linear>(transport_modules))
        {
            LevelRides.Start(transport_module, dz, deciseconds);
            foreach (atom_movable rider in Dm.each<atom_movable>(transport_module.transport_contents))
                LevelRides.Start(rider, dz, deciseconds);
        }
    }

    /**
     * Moves the platform after a passed delay.
     *
     * This is a more "user friendly" or "realistic" move.
     * It includes things like:
     * - Allowing platform "travel time"
     * - Shutting elevator safety doors
     * - Sound effects while moving
     * - Safety warnings for anyone below the platform (while it's moving downwards)
     */
    // tg: code/modules/transport/linear_controller.dm:337
    public virtual async DmTask move_after_delay(object lift_move_duration = null, object door_duration = null, object direction = null, mob user = null)
    {
        if (!isnum(lift_move_duration))
            throw Dm.CRASH(Dm.text("[] move_after_delay called with invalid duration ([]).", type, lift_move_duration), "code/modules/transport/linear_controller.dm", 339);
        if (Dm.num(lift_move_duration) <= 0 * SECONDS)
        {
            await move_lift_vertically(direction, user);
            return;
        }

        // Get the lowest or highest platform according to which direction we're moving
        obj_structure_transport_linear prime_lift = return_closest_platform_to_z(Dm.eq(direction, UP) ? world.maxz : 0) as obj_structure_transport_linear;

        // ...because we use the duration of the sound effect to make it last for roughly the duration of the lift travel
        playsound(prime_lift, Dm.resource("sound/vehicles/mecha/hydraulic.ogg"), 25, vary: TRUE, frequency: Dm.clamp(HYDRAULIC_SFX_DURATION / Dm.num(lift_move_duration), 0.33f, 3));

        // Move the platform after a timer
        _addtimer(Dm.@new<datum_callback>(this, nameof(move_lift_vertically), direction, user), Dm.num(lift_move_duration), TIMER_UNIQUE, file: "code/modules/transport/linear_controller.dm", line: 351);
        announce_ride(direction, Dm.num(lift_move_duration)); // DEVIATION(DV371)
        // Open doors after the set duration if supplied
        if (isnum(door_duration))
            _addtimer(Dm.@new<datum_callback>(this, nameof(open_lift_doors_callback)), Dm.num(door_duration), TIMER_UNIQUE, file: "code/modules/transport/linear_controller.dm", line: 354);

        // DEVIATION(DV427): tg's warning signs on the turfs under a platform going down (telegraphing/lift_travel, linear_controller.dm:356-376)
        // are not shown: in 3D they stood in the shaft the car rides down through, drawn inside the car (the owner, 2026-11-14)
    }

    /**
     * Simple wrapper for checking if we can move 1 zlevel, and if we can, do said move.
     * Locks controls, closes all doors, then moves the platform and re-opens the doors afterwards.
     */
    // tg: code/modules/transport/linear_controller.dm:388
    public virtual async DmTask<object> simple_move_wrapper(object direction = null, object lift_move_duration = null, mob user = null)
    {
        if (!Dm.truthy(Check_lift_move(direction)))
            return FALSE;

        // Lock controls, to prevent moving-while-moving memes
        controls_lock(TRUE);
        // Send out a signal that we're going
        SEND_SIGNAL(COMSIG_LIFT_SET_DIRECTION, Dm.list(this, direction));
        // Close all lift doors
        update_lift_doors(action: CYCLE_CLOSED);

        if (lift_move_duration == null || Dm.num(lift_move_duration) <= 0 * SECONDS)
        {
            // Do an instant move
            await move_lift_vertically(direction, user);
            // Open doors on the zs we arrive at
            update_lift_doors(get_zs_we_are_on(), action: CYCLE_OPEN);
            // And unlock the controls after
            controls_lock(FALSE);
            return TRUE;
        }

        // Do a delayed move
        await move_after_delay(
            lift_move_duration: lift_move_duration,
            door_duration: Dm.num(lift_move_duration) * 1.5f,
            direction: direction,
            user: user
        );

        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_simple_move_wrapper)), Dm.num(lift_move_duration) * 1.5f, file: "code/modules/transport/linear_controller.dm", line: 416);
        return TRUE;
    }

    /**
     * Wrap everything up from simple_move_wrapper finishing its movement
     */
    // tg: code/modules/transport/linear_controller.dm:422
    public virtual void finish_simple_move_wrapper()
    {
        SEND_SIGNAL(COMSIG_LIFT_SET_DIRECTION, Dm.list(this, 0));
        controls_lock(FALSE);
    }

    /**
     * Moves the platform to the passed z-level.
     *
     * Checks for validity of the move: Are we moving to the same z-level, can we actually move to that z-level?
     * Does NOT check if the controls are currently locked.
     *
     * Moves to the passed z-level by calling move_after_delay repeatedly until the passed z-level is reached.
     * This proc sleeps as it moves.
     */
    // tg: code/modules/transport/linear_controller.dm:440
    public virtual async DmTask<object> move_to_zlevel(object target_z = null, datum_callback loop_callback = null, mob user = null)
    {
        if (!isnum(target_z) || Dm.num(target_z) <= 0)
            throw Dm.CRASH(Dm.text("[] move_to_zlevel was passed an invalid target_z ([]).", type, target_z), "code/modules/transport/linear_controller.dm", 442);

        obj_structure_transport_linear prime_lift = return_closest_platform_to_z(target_z) as obj_structure_transport_linear;
        float lift_z = prime_lift.z;
        // We're already at the desired z-level!
        if (Dm.eq(target_z, lift_z))
            return FALSE;

        // The amount of z levels between the our and target_z
        float z_difference = Dm.abs(Dm.num(target_z) - lift_z);
        // Direction (up/down) needed to go to reach target_z
        float direction = lift_z < Dm.num(target_z) ? UP : DOWN;

        // We can't go that way anymore, or possibly ever
        if (!Dm.truthy(Check_lift_move(direction)))
            return FALSE;

        // Okay we're ready to start moving now.
        controls_lock(TRUE);
        // Send out a signal that we're going
        SEND_SIGNAL(COMSIG_LIFT_SET_DIRECTION, Dm.list(this, direction));
        float travel_speed = prime_lift.elevator_vertical_speed;

        // Close all lift doors
        update_lift_doors(action: CYCLE_CLOSED);
        await sleep(1.1f * SECONDS);
        // Approach the desired z-level one step at a time
        for (int i = 1; i <= z_difference; i++)
        {
            if (!Dm.truthy(Check_lift_move(direction)))
                break;
            if (loop_callback != null && !Dm.truthy(await loop_callback.Invoke()))
                break;
            // move_after_delay will set up a timer and cause us to move after a time
            await move_after_delay(
                lift_move_duration: travel_speed,
                direction: direction,
                user: user
            );
            // and we don't want to send another request until the timer's done
            await stoplag(travel_speed + 0.1f * SECONDS);
            if (QDELETED(this) || QDELETED(prime_lift))
                return null;
        }

        update_lift_doors(get_zs_we_are_on(), action: CYCLE_OPEN);
        SEND_SIGNAL(COMSIG_LIFT_SET_DIRECTION, Dm.list(this, 0));
        controls_lock(FALSE);
        return TRUE;
    }

    /**
     * Updates all blast doors and shutters that share an ID with our lift.
     */
    // tg: code/modules/transport/linear_controller.dm:497
    public virtual void update_lift_doors(object on_z_level = null, object action = null)
    {
        if (on_z_level != null && !islist(on_z_level))
            on_z_level = Dm.list(on_z_level);

        bool played_ding = false;
        foreach (obj_machinery_door elevator_door in Dm.each<obj_machinery_door>(GLOB.elevator_doors))
        {
            if (!Dm.eq(elevator_door.transport_linked_id, specific_transport_id))
                continue;
            if (on_z_level != null && !Dm.@in(elevator_door.z, on_z_level))
                continue;
            if (Dm.eq(action, CYCLE_OPEN))
            {
                elevator_door.elevator_status = LIFT_PLATFORM_UNLOCKED;
                if (!played_ding)
                {
                    playsound(elevator_door, Dm.resource("sound/machines/ping.ogg"), 50, TRUE);
                    played_ding = true;
                }
                _addtimer(Dm.@new<datum_callback>(elevator_door, nameof(obj_machinery_door.open)), 0.7f * SECONDS, file: "code/modules/transport/linear_controller.dm", line: 514);
            }
            else if (Dm.eq(action, CYCLE_CLOSED))
            {
                elevator_door.elevator_status = LIFT_PLATFORM_LOCKED;
                Waiting.Detach(elevator_door.close());
            }
            else
            {
                stack_trace(Dm.text("Elevator lift update_lift_doors called with an improper action ([]).", action), "code/modules/transport/linear_controller.dm", 519);
            }
        }
    }

    /// Helper used in callbacks to open all the doors our platform is on
    // tg: code/modules/transport/linear_controller.dm:522
    public virtual void open_lift_doors_callback()
    {
        update_lift_doors(get_zs_we_are_on(), action: CYCLE_OPEN);
    }

    /**
     * Moves the platform, this is what users invoke with their hand.
     * This is a SAFE proc, ensuring every part of the lift moves SANELY.
     * It also locks controls for the (miniscule) duration of the movement, so the elevator cannot be broken by spamming.
     */
    // tg: code/modules/transport/linear_controller.dm:530
    public virtual async DmTask move_transport_horizontally(object going = null)
    {
        if (Dm.truthy(modular_set))
        {
            controls_lock(TRUE);
            foreach (obj_structure_transport_linear module_to_move in Dm.each<obj_structure_transport_linear>(transport_modules))
                await module_to_move.travel(going);

            controls_lock(FALSE);
            return;
        }

        float max_x = 0;
        float max_y = 0;
        float max_z = 0;
        float min_x = Dm.num(world.maxx);
        float min_y = Dm.num(world.maxy);
        float min_z = Dm.num(world.maxz);

        foreach (obj_structure_transport_linear transport_module in Dm.each<obj_structure_transport_linear>(transport_modules))
        {
            max_z = Dm.max(max_z, transport_module.z);
            min_z = Dm.min(min_z, transport_module.z);

            min_x = Dm.min(min_x, transport_module.x);
            max_x = Dm.max(max_x, transport_module.x);
            //this assumes that all z levels have identical horizontal bounding boxes
            //but if youre still using a non multitile  platform at this point
            //then its your own problem. it wont runtime it will just be slower than it needs to be if this assumption isnt
            //the case

            min_y = Dm.min(min_y, transport_module.y);
            max_y = Dm.max(max_y, transport_module.y);
        }

        float dir = Dm.num(going);
        for (float z = min_z; z <= max_z; z++)
        {
            //This must be safe way to border tile to tile move of bordered platforms, that excludes platform overlapping.
            if (Dm.truthy(Dm.bitand(dir, WEST)))
            {
                //Go along the X axis from min to max, from left to right
                for (float x = min_x; x <= max_x; x++)
                    await move_column(x, z);
            }
            else
            {
                //Go along the X axis from max to min, from right to left
                for (float x = max_x; x >= min_x; x--)
                    await move_column(x, z);
            }
        }

        // the three y loops of each x branch (linear_controller.dm:552-596), shared by both x orders
        async DmTask move_column(float x, float z)
        {
            if (Dm.truthy(Dm.bitand(dir, NORTH)))
            {
                //Go along the Y axis from max to min, from up to down
                for (float y = max_y; y >= min_y; y--)
                {
                    obj_structure_transport_linear transport_module = Dm.locate<obj_structure_transport_linear>(locate(x, y, z));
                    if (transport_module != null)
                        await transport_module.travel(going);
                }
            }
            else if (Dm.truthy(Dm.bitand(dir, SOUTH)))
            {
                //Go along the Y axis from min to max, from down to up
                for (float y = min_y; y <= max_y; y++)
                {
                    obj_structure_transport_linear transport_module = Dm.locate<obj_structure_transport_linear>(locate(x, y, z));
                    if (transport_module != null)
                        await transport_module.travel(going);
                }
            }
            else
            {
                //Go along the Y axis from min to max, from down to up
                for (float y = min_y; y <= max_y; y++)
                {
                    obj_structure_transport_linear transport_module = Dm.locate<obj_structure_transport_linear>(locate(x, y, z));
                    if (transport_module != null)
                        await transport_module.travel(going);
                }
            }
        }
    }

    ///Check destination turfs
    // tg: code/modules/transport/linear_controller.dm:602
    public virtual object Check_lift_move(object check_dir = null)
    {
        foreach (obj_structure_transport_linear transport_module in Dm.each<obj_structure_transport_linear>(transport_modules))
        {
            foreach (turf bound_turf in Dm.each<turf>(transport_module.locs))
            {
                turf T = get_step_multiz(transport_module, check_dir) as turf;
                if (T == null)//the edges of multi-z maps
                    return FALSE;
                if (Dm.eq(check_dir, UP) && T is not turf_open_openspace) // We don't want to go through the ceiling!
                    return FALSE;
                if (Dm.eq(check_dir, DOWN) && get_turf(transport_module) is not turf_open_openspace) // No going through the floor!
                    return FALSE;
            }
        }
        return TRUE;
    }

    /**
     * Sets transport controls_locked state. Used to prevent moving mid movement, or cooldowns.
     */
    // tg: code/modules/transport/linear_controller.dm:617
    public virtual void controls_lock(object state = null)
    {
        if (!Dm.truthy(state))
            controller_status = Dm.num(Dm.bitand(controller_status, ~CONTROLS_LOCKED));
        else
            controller_status = Dm.num(Dm.bitor(controller_status, CONTROLS_LOCKED));
    }

    /**
     * resets the contents of all platforms to their original state in case someone put a bunch of shit onto the platform.
     * intended to be called by admins. passes all arguments to reset_contents() for each of our platforms.
     */
    // tg: code/modules/transport/linear_controller.dm:634
    public virtual object reset_lift_contents(float? consider_anything_past = null, float? foreign_objects = null, float? foreign_non_player_mobs = null, float? consider_player_mobs = null)
    {
        consider_anything_past ??= 0;
        foreign_objects ??= TRUE;
        foreign_non_player_mobs ??= TRUE;
        consider_player_mobs ??= FALSE;
        foreach (obj_structure_transport_linear lift_to_reset in Dm.each<obj_structure_transport_linear>(transport_modules))
            lift_to_reset.reset_contents(consider_anything_past, foreign_objects, foreign_non_player_mobs, consider_player_mobs);

        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/transport/linear_controller.dm:12  var/static/list/ignored_smashthroughs
    internal DmList datum_transport_controller_linear__ignored_smashthroughs = Dm.list(
        typeof(obj_machinery_power_supermatter_crystal),
        typeof(obj_structure_holosign),
        typeof(obj_machinery_field),
        typeof(obj_structure_fluff_tram_rail));
}
