// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/transport_module.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D325 (multi-z, elevators): /obj/structure/transport/linear and its public, debug and lift door subtypes; the tram port
// (2026-11-12): the tram's subtypes (/obj/structure/transport/linear/tram, line 865 on). Emissive overlays are lighting (D24, DEVIATION DV12).

/**
 * Base transport structure. A single tile that can form a modular set with neighbouring tiles
 * This type holds elevators and trams
 */
// tg: code/modules/transport/transport_module.dm:5
public partial class obj_structure_transport_linear : obj_structure_transport
{
    // tg: code/modules/transport/transport_module.dm:83
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Yes if it's VV'd it won't be accurate but it probably shouldn't ever be
        if (Dm.truthy(radial_travel))
            Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_bare_hands), Dm.named("lmb_text", I18n.T("Send Transport"))));

        ADD_TRAIT(this, TRAIT_CHASM_STOPPER, INNATE_TRAIT);
        set_movement_registrations();

        //since transport_controller datums find all connected platforms when a transport structure first creates it and then
        //sets those platforms' transport_controller_datum to itself, this check will only evaluate to true once per tram platform
        if (transport_controller_datum == null && transport_controller_type != null)
        {
            transport_controller_datum = (datum_transport_controller_linear)Dm.@new(transport_controller_type, this);
            return INITIALIZE_HINT_LATELOAD;
        }
        return dot;
    }

    // tg: code/modules/transport/transport_module.dm:98
    public override void LateInitialize(params object[] __extra)
    {
        //after everything is initialized the transport controller can order everything
        Waiting.NoSleep(transport_controller_datum.order_platforms_by_z_level());
    }

    // tg: code/modules/transport/transport_module.dm:102
    public override object Destroy(float? force = null)
    {
        transport_controller_datum = null;
        return base.Destroy(force);
    }

    ///set the movement registrations to our current turf(s) so contents moving out of our tile(s) are removed from our movement lists
    // tg: code/modules/transport/transport_module.dm:108
    public virtual void set_movement_registrations(DmList turfs_to_set = null)
    {
        foreach (turf turf_loc in Dm.each<turf>(turfs_to_set ?? locs))
        {
            RegisterSignal(turf_loc, COMSIG_ATOM_EXITED, nameof(uncrossed_remove_item_from_transport));
            RegisterSignals(turf_loc, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON), nameof(add_item_on_transport));
        }
    }

    ///unset our movement registrations from turfs that no longer contain us (or every loc if turfs_to_unset is unspecified)
    // tg: code/modules/transport/transport_module.dm:114
    public virtual void unset_movement_registrations(DmList turfs_to_unset = null)
    {
        unset_movement_registrations__registrations ??= Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON);
        foreach (turf turf_loc in Dm.each<turf>(turfs_to_unset ?? locs))
            UnregisterSignal(turf_loc, unset_movement_registrations__registrations);
    }

    // tg: code/modules/transport/transport_module.dm:115  var/static/list/registrations (rules 10.6)
    private static ref DmList unset_movement_registrations__registrations => ref TgState.Current.obj_structure_transport_linear__unset_movement_registrations__registrations;

    // tg: code/modules/transport/transport_module.dm:120
    public virtual void uncrossed_remove_item_from_transport(datum source = null, atom_movable gone = null, object direction = null)
    {
        if (!Dm.@in(gone.loc, locs))
            remove_item_from_transport(gone);
    }

    // tg: code/modules/transport/transport_module.dm:125
    public virtual void remove_item_from_transport(atom_movable potential_rider = null)
    {
        if (!Dm.@in(potential_rider, transport_contents))
            return;
        if (isliving(potential_rider) && HAS_TRAIT(potential_rider, TRAIT_CANNOT_BE_UNBUCKLED))
            REMOVE_TRAIT(potential_rider, TRAIT_CANNOT_BE_UNBUCKLED, BUCKLED_TRAIT);

        Dm.remove(transport_contents, potential_rider);
        Dm.remove(changed_gliders, potential_rider);
        LevelRides.End(potential_rider); // DEVIATION(DV371): a rider who steps off rides no more

        UnregisterSignal(potential_rider, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE));
    }

    // tg: code/modules/transport/transport_module.dm:137
    public virtual object add_item_on_transport(datum source = null, atom_movable new_transport_contents = null)
    {
        add_item_on_transport__blacklisted_types ??= typecacheof(Dm.list(typeof(obj_structure_fluff_tram_rail), typeof(obj_effect_decal_cleanable), typeof(obj_structure_transport_linear), typeof(mob_eye), typeof(obj_effect_gravity_fluff_field))) as DmList;
        if (Dm.truthy(is_type_in_typecache(new_transport_contents, add_item_on_transport__blacklisted_types)) || Dm.eq(new_transport_contents.invisibility, INVISIBILITY_ABSTRACT) || HAS_TRAIT(new_transport_contents, TRAIT_UNDERFLOOR)) //prevents the tram from stealing things like landmarks
            return FALSE;
        if (Dm.@in(new_transport_contents, transport_contents))
            return FALSE;

        if (isliving(new_transport_contents) && !HAS_TRAIT(new_transport_contents, TRAIT_CANNOT_BE_UNBUCKLED))
            ADD_TRAIT(new_transport_contents, TRAIT_CANNOT_BE_UNBUCKLED, BUCKLED_TRAIT);

        Dm.append(ref transport_contents, new_transport_contents);
        RegisterSignal(new_transport_contents, COMSIG_QDELETING, nameof(remove_item_from_transport));
        LevelRides.Join(new_transport_contents, this); // DEVIATION(DV371): a rider who walks onto the next tile of a moving car rides on with it

        return TRUE;
    }

    // tg: code/modules/transport/transport_module.dm:139  var/static/list/blacklisted_types (rules 10.6)
    private static ref DmList add_item_on_transport__blacklisted_types => ref TgState.Current.obj_structure_transport_linear__add_item_on_transport__blacklisted_types;

    ///adds everything on our tile that can be added to our transport_contents and initial_contents lists when we're created
    // tg: code/modules/transport/transport_module.dm:154
    public virtual void add_initial_contents()
    {
        foreach (turf turf_loc in Dm.each<turf>(locs))
        {
            foreach (atom_movable movable_contents in Dm.each<atom_movable>(turf_loc))
            {
                if (movable_contents == this)
                    continue;

                if (Dm.truthy(add_item_on_transport(this, movable_contents)))
                {
                    datum_weakref new_initial_contents = WEAKREF(movable_contents) as datum_weakref;
                    if (new_initial_contents == null)
                        continue;

                    Dm.append(ref initial_contents, new_initial_contents);
                }
            }
        }
    }

    ///verify the movables in our list of contents are actually on our loc
    // tg: code/modules/transport/transport_module.dm:169
    public virtual void verify_transport_contents()
    {
        foreach (atom_movable movable_contents in Dm.each<atom_movable>(transport_contents.Copy()))
        {
            if (!Dm.@in(movable_contents.loc, locs))
                remove_item_from_transport(movable_contents);
        }
    }

    // tg: code/modules/transport/transport_module.dm:174
    public virtual object check_for_humans()
    {
        foreach (atom_movable movable_contents in Dm.each<atom_movable>(transport_contents))
        {
            if (ishuman(movable_contents))
                return TRUE;
        }

        return FALSE;
    }

    ///signal handler for COMSIG_MOVABLE_UPDATE_GLIDE_SIZE: when a movable in transport_contents changes its glide_size independently.
    ///adds that movable to a lazy list, movables in that list have their glide_size updated when the tram next moves
    // tg: code/modules/transport/transport_module.dm:183
    public virtual void on_changed_glide_size(atom_movable moving_contents = null, object new_glide_size = null)
    {
        if (!Dm.eq(new_glide_size, glide_size_override))
            Dm.append(ref changed_gliders, moving_contents);
    }

    ///make this tram platform multitile, expanding to cover all the tram platforms adjacent to us and deleting them. makes movement more efficient.
    ///the platform becoming multitile should be in the lower left corner since thats assumed to be the loc of multitile objects
    // tg: code/modules/transport/transport_module.dm:191
    public virtual async DmTask<object> create_modular_set__proc(object min_x = null, object min_y = null, object max_x = null, object max_y = null, object z = null)
    {
        if (!(Dm.truthy(min_x) && Dm.truthy(min_y) && Dm.truthy(max_x) && Dm.truthy(max_y) && Dm.truthy(z)))
        {
            foreach (obj_structure_transport_linear other_transport in Dm.each<obj_structure_transport_linear>(transport_controller_datum.transport_modules))
            {
                if (!Dm.eq(other_transport.z, z))
                    continue;

                min_x = Dm.min(min_x, other_transport.x);
                max_x = Dm.max(max_x, other_transport.x);

                min_y = Dm.min(min_y, other_transport.y);
                max_y = Dm.max(max_y, other_transport.y);
            }
        }

        turf lower_left_corner = locate(Dm.num(min_x), Dm.num(min_y), Dm.num(z));
        obj_structure_transport_linear primary_module = Dm.locate<obj_structure_transport_linear>(lower_left_corner);

        if (primary_module == null)
        {
            stack_trace("no lift in the lower left corner of a lift level!", "code/modules/transport/transport_module.dm", 208);
            return FALSE;
        }

        if (primary_module != this)
        {
            //the loc of a multitile object must always be the lower left corner
            return await primary_module.create_modular_set__proc();
        }

        width = Dm.num(max_x) - Dm.num(min_x) + 1;
        height = Dm.num(max_y) - Dm.num(min_y) + 1;

        ///list of turfs we dont go over. if for whatever reason we encounter an already multitile lift platform
        ///we add all of its locs to this list so we dont add that lift platform multiple times as we iterate through its locs
        DmList locs_to_skip = locs.Copy();

        bound_width = bound_width * width;
        bound_height = bound_height * height;

        //multitile movement code assumes our loc is on the lower left corner of our bounding box

        float first_x = 0;
        float first_y = 0;

        float last_x = Dm.max(Dm.num(max_x) - Dm.num(min_x), 0);
        float last_y = Dm.max(Dm.num(max_y) - Dm.num(min_y), 0);

        for (float y = first_y; y <= last_y; y++)
        {
            float y_pixel_offset = ICON_SIZE_Y * y;

            for (float x = first_x; x <= last_x; x++)
            {
                float x_pixel_offset = ICON_SIZE_X * x;

                turf set_turf = locate(x + Dm.num(min_x), y + Dm.num(min_y), Dm.num(z));

                if (set_turf == null)
                    continue;

                if (Dm.@in(set_turf, locs_to_skip))
                    continue;

                obj_structure_transport_linear other_transport = Dm.locate<obj_structure_transport_linear>(set_turf);

                if (other_transport == null)
                    continue;

                Dm.append(ref locs_to_skip, other_transport.locs.Copy());//make sure we never go over multitile platforms multiple times

                other_transport.pixel_x = x_pixel_offset;
                other_transport.pixel_y = y_pixel_offset;

                Dm.append(overlays, other_transport);
            }
        }

        //now we vore all the other lifts connected to us on our z level
        foreach (obj_structure_transport_linear other_transport in Dm.each<obj_structure_transport_linear>(transport_controller_datum.transport_modules.Copy()))
        {
            if (other_transport == this || !Dm.eq(other_transport.z, z))
                continue;

            Dm.remove(transport_controller_datum.transport_modules, other_transport);
            if (other_transport.transport_contents != null)
                Dm.combine(ref transport_contents, other_transport.transport_contents);
            if (other_transport.initial_contents != null)
                Dm.combine(ref initial_contents, other_transport.initial_contents);

            qdel(other_transport);
        }

        transport_controller_datum.create_modular_set = TRUE;

        atom old_loc = loc;

        await forceMove(locate(Dm.num(min_x), Dm.num(min_y), Dm.num(z)));//move to the lower left corner
        set_movement_registrations(Dm.sub(locs, old_loc) as DmList);
        blocks_emissive = EMISSIVE_BLOCK_GENERIC;
        update_appearance();
        return TRUE;
    }

    ///returns an unordered list of all lift platforms adjacent to us. used so our transport_controller_datum can control all connected platforms.
    ///includes platforms directly above or below us as well. only includes platforms with an identical transport_id to our own.
    // tg: code/modules/transport/transport_module.dm:286
    public virtual object module_adjacency(datum_transport_controller transport_controller_datum = null)
    {
        DmList dot = Dm.list();
        foreach (object direction in Dm.each(GLOB.cardinals_multiz))
        {
            obj_structure_transport_linear neighbor = Dm.locate<obj_structure_transport_linear>(get_step_multiz(this, direction));
            if (neighbor == null || !Dm.eq(neighbor.transport_id, transport_id))
                continue;
            Dm.append(ref dot, neighbor);
        }
        return dot;
    }

    ///main proc for moving the lift in the direction [travel_direction]. handles horizontal and/or vertical movement for multi platformed lifts and multitile lifts.
    // tg: code/modules/transport/transport_module.dm:295
    public virtual async DmTask travel(object travel_direction = null)
    {
        DmList things_to_move = transport_contents;
        turf destination;
        if (!isturf(travel_direction))
        {
            destination = get_step_multiz(this, travel_direction) as turf;
        }
        else
        {
            destination = (turf)travel_direction;
            travel_direction = get_dir_multiz(loc as turf, travel_direction as turf);
        }

        float x_offset = ROUND_UP(bound_width / ICON_SIZE_X) - 1; //how many tiles our horizontally farthest edge is from us
        float y_offset = ROUND_UP(bound_height / ICON_SIZE_Y) - 1; //how many tiles our vertically farthest edge is from us

        float destination_x = destination.x;
        float destination_y = destination.y;
        float destination_z = destination.z;
        //the x coordinate of the edge furthest from our future destination, which would be our right hand side
        float back_edge_x = destination_x + x_offset;//if we arent multitile this should just be destination.x
        float upper_edge_y = destination_y + y_offset;

        DmList dest_locs = block(
            destination_x, destination_y, destination_z,
            back_edge_x, upper_edge_y, destination_z
        );

        DmList entering_locs = Dm.sub(dest_locs, locs) as DmList;
        DmList exited_locs = Dm.sub(locs, dest_locs) as DmList;

        if (Dm.eq(travel_direction, DOWN))
        {
            foreach (turf dest_turf in Dm.each<turf>(entering_locs))
            {
                dest_turf.SEND_SIGNAL(COMSIG_TURF_INDUSTRIAL_LIFT_ENTER, Dm.list(dest_turf, things_to_move));

                if (iswallturf(dest_turf))
                {
                    turf_closed_wall hit_wall = (turf_closed_wall)dest_turf;
                    do_sparks(2, FALSE, hit_wall);
                    await hit_wall.dismantle_wall(devastated: TRUE);
                    foreach (mob nearby_witness in Dm.each<mob>(urange(8, this)))
                        shake_camera(nearby_witness, 2, 3);
                    playsound(hit_wall, Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
                }

                foreach (mob_living crushed in Dm.each<mob_living>(dest_turf.contents))
                {
                    to_chat(crushed, span_userdanger(I18n.T("You are crushed by []!", this)));
                    if (Dm.truthy(violent_landing))
                    {
                        // Violent landing = gibbed. But the nicest kind of gibbing, keeping everything intact.
                        crushed.investigate_log(Dm.text("has been gibbed by [].", this), INVESTIGATE_DEATHS);
                        await crushed.gib(DROP_ALL_REMAINS);
                    }
                    else
                    {
                        // Less violent landing simply crushes every bone in your body.
                        await crushed.Paralyze(30 * SECONDS, ignore_canstun: TRUE);
                        await crushed.apply_damage(30, BRUTE, BODY_ZONE_CHEST, wound_bonus: 30);
                        await crushed.apply_damage(20, BRUTE, BODY_ZONE_HEAD, wound_bonus: 25);
                        await crushed.apply_damage(15, BRUTE, BODY_ZONE_L_LEG, wound_bonus: 15);
                        await crushed.apply_damage(15, BRUTE, BODY_ZONE_R_LEG, wound_bonus: 15);
                        await crushed.apply_damage(15, BRUTE, BODY_ZONE_L_ARM, wound_bonus: 15);
                        await crushed.apply_damage(15, BRUTE, BODY_ZONE_R_ARM, wound_bonus: 15);
                    }
                }
            }
        }
        else if (Dm.eq(travel_direction, UP))
        {
            foreach (turf dest_turf in Dm.each<turf>(entering_locs))
            {
                ///handles any special interactions objects could have with the lift/tram, handled on the item itself
                dest_turf.SEND_SIGNAL(COMSIG_TURF_INDUSTRIAL_LIFT_ENTER, Dm.list(dest_turf, things_to_move));

                if (iswallturf(dest_turf))
                {
                    turf_closed_wall hit_wall = (turf_closed_wall)dest_turf;
                    do_sparks(2, FALSE, hit_wall);
                    await hit_wall.dismantle_wall(devastated: TRUE);
                    foreach (mob client_mob in Dm.each<mob>(SSspatial_grid.orthogonal_range_search(this, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS, 8)))
                        shake_camera(client_mob, 2, 3);
                    playsound(hit_wall, Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
                }
            }
        }
        else
        {
            ///potentially finds a spot to throw the victim at for daring to be hit by a tram. is null if we havent found anything to throw
            atom throw_target = null;

            foreach (turf dest_turf in Dm.each<turf>(entering_locs))
            {
                ///handles any special interactions objects could have with the lift/tram, handled on the item itself
                dest_turf.SEND_SIGNAL(COMSIG_TURF_INDUSTRIAL_LIFT_ENTER, Dm.list(dest_turf, things_to_move));

                if (iswallturf(dest_turf))
                {
                    turf_closed_wall collided_wall = (turf_closed_wall)dest_turf;
                    do_sparks(2, FALSE, collided_wall);
                    await collided_wall.dismantle_wall(devastated: TRUE);
                    foreach (mob client_mob in Dm.each<mob>(SSspatial_grid.orthogonal_range_search(collided_wall, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS, 8)))
                        shake_camera(client_mob, duration: 2, strength: 3);

                    playsound(collided_wall, Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
                }

                if (ismineralturf(dest_turf))
                {
                    turf_closed_mineral dest_mineral_turf = (turf_closed_mineral)dest_turf;
                    foreach (mob client_mob in Dm.each<mob>(SSspatial_grid.orthogonal_range_search(dest_mineral_turf, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS, 8)))
                        shake_camera(client_mob, duration: 2, strength: 3);
                    await dest_mineral_turf.gets_drilled();
                }

                foreach (obj_structure victim_structure in Dm.each<obj_structure>(dest_turf.contents.Copy()))
                {
                    if (QDELING(victim_structure))
                        continue;
                    if (!Dm.truthy(is_type_in_typecache(victim_structure, datum_transport_controller_linear.ignored_smashthroughs)))
                    {
                        if ((Dm.eq(PLANE_TO_TRUE(victim_structure.plane), FLOOR_PLANE) && victim_structure.layer > TRAM_RAIL_LAYER) || (Dm.eq(PLANE_TO_TRUE(victim_structure.plane), GAME_PLANE) && victim_structure.layer > LOW_OBJ_LAYER))
                        {
                            if (Dm.truthy(victim_structure.anchored) && Dm.eq(Dm.initial(victim_structure, nameof(victim_structure.anchored)), TRUE))
                            {
                                visible_message(span_danger(I18n.T("[] smashes through []!", this, victim_structure)));
                                await victim_structure.deconstruct(FALSE);
                            }
                            else
                            {
                                if (throw_target == null)
                                    throw_target = get_edge_target_turf(this, turn(Dm.num(travel_direction), Dm.num(pick(45, -45)))) as atom;
                                visible_message(span_danger(I18n.T("[] violently rams [] out of the way!", this, victim_structure)));
                                victim_structure.anchored = FALSE;
                                await victim_structure.take_damage(rand(20, 25) * collision_lethality);
                                await victim_structure.throw_at(throw_target, 200 * collision_lethality, 4 * collision_lethality);
                            }
                        }
                    }
                }

                foreach (obj_machinery victim_machine in Dm.each<obj_machinery>(dest_turf.contents.Copy()))
                {
                    if (QDELING(victim_machine))
                        continue;
                    if (Dm.truthy(is_type_in_typecache(victim_machine, datum_transport_controller_linear.ignored_smashthroughs)))
                        continue;
                    if (victim_machine is obj_machinery_field) //graceful break handles this scenario
                        continue;
                    if (victim_machine.layer >= LOW_OBJ_LAYER) //avoids stuff that is probably flush with the ground
                    {
                        playsound(this, Dm.resource("sound/effects/bang.ogg"), 50, TRUE);
                        visible_message(span_danger(I18n.T("[] smashes through []!", this, victim_machine)));
                        qdel(victim_machine);
                    }
                }

                foreach (mob_living victim_living in Dm.each<mob_living>(dest_turf.contents.Copy()))
                {
                    float damage_multiplier = Dm.num(victim_living.maxHealth) * 0.01f;
                    bool extra_ouch = false; // if emagged you're gonna have a really bad time
                    if (internal_movement_delay <= 1) // slow trams don't cause extra damage
                    {
                        foreach (obj_structure_tram_spoiler my_spoiler in Dm.each<obj_structure_tram_spoiler>(transport_contents))
                        {
                            if (victim_living.buckled is obj_structure_fluff_tram_rail)
                            {
                                extra_ouch = true;
                                break;
                            }
                            if (!Dm.eq(get_dist(my_spoiler, victim_living), 1))
                                continue;
                            if (Dm.truthy(my_spoiler.deployed))
                            {
                                extra_ouch = true;
                                break;
                            }
                        }
                    }

                    if (Dm.truthy(datum_transport_controller_linear.ignored_smashthroughs[victim_living.type]))
                        continue;
                    to_chat(victim_living, span_userdanger(I18n.T("[] collides into you!", this)));
                    victim_living.SEND_SIGNAL(COMSIG_LIVING_HIT_BY_TRAM, Dm.list(victim_living, this));
                    playsound(this, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
                    float damage = 0;

                    log_combat(this, victim_living, "collided with");
                    if (prob(15)) //sorry buddy, luck wasn't on your side
                        damage = 29 * collision_lethality * damage_multiplier;
                    else
                        damage = rand(7, 21) * collision_lethality * damage_multiplier;
                    await victim_living.apply_damage(2 * damage, BRUTE, BODY_ZONE_HEAD, wound_bonus: 7);
                    await victim_living.apply_damage(3 * damage, BRUTE, BODY_ZONE_CHEST, wound_bonus: 21);
                    await victim_living.apply_damage(0.5f * damage, BRUTE, BODY_ZONE_L_LEG, wound_bonus: 14);
                    await victim_living.apply_damage(0.5f * damage, BRUTE, BODY_ZONE_R_LEG, wound_bonus: 14);
                    await victim_living.apply_damage(0.5f * damage, BRUTE, BODY_ZONE_L_ARM, wound_bonus: 14);
                    await victim_living.apply_damage(0.5f * damage, BRUTE, BODY_ZONE_R_ARM, wound_bonus: 14);

                    if (extra_ouch)
                    {
                        playsound(this, Dm.resource("sound/effects/grillehit.ogg"), 50, TRUE);
                        obj_item_bodypart_head head = victim_living.get_bodypart("head") as obj_item_bodypart_head;
                        if (head != null)
                        {
                            log_combat(this, victim_living, "beheaded");
                            await head.dismember();
                            await victim_living.regenerate_icons();
                            add_overlay(mutable_appearance(icon, "blood_overlay"));
                            register_collision(points: 3);
                        }
                    }

                    if (QDELETED(victim_living)) //in case it was a mob that dels on death
                        continue;
                    if (throw_target == null)
                        throw_target = get_edge_target_turf(this, turn(Dm.num(travel_direction), Dm.num(pick(45, -45)))) as atom;

                    turf turf_to_bloody = get_turf(victim_living);
                    await turf_to_bloody.add_mob_blood(victim_living);

                    await victim_living.throw_at();
                    //if travel_direction EAST, will turn to the NORTHEAST or SOUTHEAST and throw the ran over guy away
                    datum_callback land_slam = Dm.@new<datum_callback>(victim_living, nameof(mob_living.tram_slam_land));
                    await victim_living.throw_at(throw_target, 200 * collision_lethality, 4 * collision_lethality, callback: land_slam);

                    //increment the hit counters
                    if (ismob(victim_living) && victim_living.client != null && transport_controller_datum is datum_transport_controller_linear_tram)
                        register_collision(points: 1);
                }
            }
        }

        unset_movement_registrations(exited_locs);
        await group_move(things_to_move, travel_direction);
        set_movement_registrations(entering_locs);
    }

    // tg: code/modules/transport/transport_module.dm:479
    public virtual void register_collision(float? points = null)
    {
        points ??= 1;
        SSpersistence.tram_hits_this_round += Dm.num(points);
        SSblackbox.record_feedback("amount", "tram_collision", points);
        datum_transport_controller_linear_tram tram_controller = transport_controller_datum as datum_transport_controller_linear_tram;
        if (!(tram_controller is datum_transport_controller_linear_tram)) throw Dm.CRASH(assertion_message("code/modules/transport/transport_module.dm", 483, "istype(tram_controller)", null), "code/modules/transport/transport_module.dm", 483);
        tram_controller.register_collision(points);
    }

    ///move the movers list of movables on our tile to destination if we successfully move there first.
    ///this is like calling forceMove() on everything in movers and ourselves, except nothing in movers
    ///has destination.Entered() and origin.Exited() called on them, as only our movement can be perceived.
    ///none of the movers are able to react to the movement of any other mover, saving a lot of needless processing cost
    ///and is more sensible. without this, if you and a banana are on the same platform, when that platform moves you will slip
    ///on the banana even if youre not moving relative to it.
    // tg: code/modules/transport/transport_module.dm:492
    public virtual async DmTask<object> group_move(DmList movers = null, object movement_direction = null)
    {
        if (Dm.eq(movement_direction, NONE))
        {
            stack_trace("a transport was told to move to somewhere it already is!", "code/modules/transport/transport_module.dm", 494);
            return FALSE;
        }

        turf our_dest = get_step(this, Dm.num(movement_direction)) as turf;

        //vars updated per mover
        atom mover_old_loc;
        atom mover_old_area;

        atom mover_new_loc;
        atom mover_new_area;

        if (!Dm.eq(glide_size, glide_size_override))
            set_glide_size(glide_size_override);

        await forceMove(our_dest);
        if (loc != our_dest || QDELETED(this))//check if our movement succeeded, if it didnt then the movers cant be moved
            return FALSE;

        foreach (atom_movable mover in Dm.each<atom_movable>(changed_gliders))
        {
            if (QDELETED(mover))
            {
                Dm.remove(movers, mover);
                continue;
            }

            if (!Dm.eq(mover.glide_size, glide_size_override))
                mover.set_glide_size(glide_size_override);
        }

        changed_gliders.Cut();

        foreach (atom_movable mover in Dm.each<atom_movable>(movers.Copy()))
        {
            if (QDELETED(mover))
            {
                Dm.remove(movers, mover);
                continue;
            }

            //another O(n) set of read operations, not ideal given datum var read times.
            //ideally we would only need to do this check once per tile with contents (which is constant per tram, while contents can scale infinitely)
            //and then the only O(n) process is calling these procs for each contents that actually changes areas
            //but that approach is probably a lot buggier. itd be nice to have it figured out though
            mover_old_loc = mover.loc;
            mover_old_area = mover_old_loc.loc;

            mover.loc = mover_new_loc = get_step(mover, Dm.num(movement_direction)) as atom;
            mover_new_area = mover_new_loc.loc;
            // DEVIATION(DV403): a tram carries a capsule by exactly its step (the predictor's carry), not a slide to the tile's centre
            if (this is obj_structure_transport_linear_tram && mover_new_loc is turf carried_to && mover_old_loc is turf carried_from && Dm.eq(carried_to.z, carried_from.z))
                LocomotionSeam.CarryByTram(mover, (int)(carried_to.x - carried_from.x), (int)(carried_to.y - carried_from.y));

            if (mover_old_area != mover_new_area)
            {
                await mover_old_area.Exited(mover, movement_direction);
                await mover_new_area.Entered(mover, mover_new_area);
            }

            await mover.Moved(mover_old_loc, movement_direction, TRUE, null, FALSE);
        }

        return TRUE;
    }

    /**
     * reset the contents of this lift platform to its original state in case someone put too much shit on it.
     * everything that is considered foreign is deleted, you can configure what is considered foreign.
     *
     * used by an admin via calling reset_lift_contents() on our transport_controller_datum.
     */
    // tg: code/modules/transport/transport_module.dm:560
    public virtual object reset_contents(float? consider_anything_past = null, float? foreign_objects = null, float? foreign_non_player_mobs = null, float? consider_player_mobs = null)
    {
        object consider_past = consider_anything_past ?? 0;
        foreign_objects ??= TRUE;
        foreign_non_player_mobs ??= TRUE;
        consider_player_mobs ??= FALSE;
        if (!Dm.truthy(foreign_objects) && !Dm.truthy(foreign_non_player_mobs) && !Dm.truthy(consider_player_mobs))
            return FALSE;

        float past = isnum(consider_past) ? Dm.max(Dm.num(consider_past), 0) : 0;
        //just in case someone fucks up the arguments

        if (past != 0 && length(transport_contents) <= past)
            return FALSE;

        ///list of resolve()'d initial_contents that are still in transport_contents
        DmList original_contents = Dm.list(this);

        ///list of objects we consider foreign according to the given arguments
        DmList foreign_contents = Dm.list();

        foreach (datum_weakref initial_contents_ref in Dm.each<datum_weakref>(initial_contents))
        {
            if (initial_contents_ref == null)
                continue;

            atom_movable resolved_contents = initial_contents_ref.resolve() as atom_movable;

            if (resolved_contents == null)
                continue;

            if (!Dm.@in(resolved_contents, transport_contents))
                continue;

            Dm.append(ref original_contents, resolved_contents);
        }

        foreach (turf turf_loc in Dm.each<turf>(locs))
        {
            DmList foreign_contents_in_loc = Dm.list();

            foreach (atom_movable foreign_movable in Dm.each<atom_movable>(Dm.sub(turf_loc.contents, original_contents)))
            {
                if (Dm.truthy(foreign_objects) && ismovable(foreign_movable) && !ismob(foreign_movable) && foreign_movable is not obj_effect_landmark_transport_nav_beacon)
                {
                    Dm.append(ref foreign_contents_in_loc, foreign_movable);
                    continue;
                }

                if (Dm.truthy(foreign_non_player_mobs) && ismob(foreign_movable))
                {
                    mob foreign_mob = (mob)foreign_movable;
                    if (Dm.truthy(consider_player_mobs) || foreign_mob.mind == null)
                    {
                        Dm.append(ref foreign_contents_in_loc, foreign_mob);
                        continue;
                    }
                }
            }

            if (past != 0)
            {
                foreign_contents_in_loc.len -= (int)past;
                //hey cool this works, neat. this takes from the opposite side of the list that youd expect but its easy so idc
                //also this means that if you use consider_anything_past then foreign mobs are less likely to be deleted than foreign objects
                //because internally the contents list is 2 linked lists of obj contents - mob contents, thus mobs are always last in the order
                //when you iterate it.
            }

            Dm.append(ref foreign_contents, foreign_contents_in_loc);
        }

        foreach (atom_movable contents_to_delete in Dm.each<atom_movable>(foreign_contents))
            qdel(contents_to_delete);

        return TRUE;
    }

    /// Callback / general proc to check if the lift is usable by the passed mob.
    // tg: code/modules/transport/transport_module.dm:620
    public virtual async DmTask<object> can_open_lift_radial(mob_living user = null, object starting_position = null)
    {
        // Gotta be a living mob
        if (!isliving(user))
            return FALSE;
        // Gotta be awake and aware
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        // Maintain the god given right to fight an elevator
        if (Dm.truthy(user.combat_mode))
            return FALSE;
        // Gotta be by the lift
        if (!Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        // If the lift moves while the radial is open close that shit
        if (starting_position != loc)
            return FALSE;

        return TRUE;
    }

    /// Opens the radial for the lift, allowing the user to move it around.
    // tg: code/modules/transport/transport_module.dm:640
    public virtual async DmTask open_lift_radial(mob_living user = null)
    {
        atom starting_position = loc;
        if (!Dm.truthy(await can_open_lift_radial(user, starting_position)))
            return;
        // One radial per person
        foreach (obj_structure_transport_linear other_platform in Dm.each<obj_structure_transport_linear>(transport_controller_datum.transport_modules))
        {
            if (Dm.@in(REF(user), other_platform.current_operators))
                return;
        }

        DmList possible_directions = Dm.list();
        if (Dm.truthy(transport_controller_datum.Check_lift_move(UP)))
        {
            open_lift_radial__up_arrow ??= image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: NORTH);

            possible_directions["Up"] = open_lift_radial__up_arrow;
        }

        if (Dm.truthy(transport_controller_datum.Check_lift_move(DOWN)))
        {
            open_lift_radial__down_arrow ??= image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: SOUTH);

            possible_directions["Down"] = open_lift_radial__down_arrow;
        }

        add_fingerprint(user);
        if (!Dm.truthy(length(possible_directions)))
        {
            balloon_alert(user, I18n.T("elevator out of service!"));
            return;
        }

        LAZYADD(ref current_operators, REF(user));
        object result = await show_radial_menu(
            user: user,
            anchor: this,
            choices: possible_directions,
            custom_check: Dm.@new<datum_callback>(this, nameof(can_open_lift_radial), user, starting_position),
            require_near: TRUE,
            tooltips: TRUE
        );

        LAZYREMOVE(ref current_operators, REF(user));
        if (!Dm.truthy(await can_open_lift_radial(user, starting_position)))
            return; //nice try
        if (result != null && !Dm.eq(result, "Cancel") && Dm.truthy(Dm.bitand(transport_controller_datum.controller_status, CONTROLS_LOCKED)))
        {
            // Only show this message if they actually wanted to move
            balloon_alert(user, I18n.T("elevator controls locked!"));
            return;
        }
        switch (result)
        {
            case "Up":
                // We have to make sure that they don't do illegal actions
                // by not having their radial menu refresh from someone else moving the lift.
                if (!Dm.truthy(await transport_controller_datum.simple_move_wrapper(UP, elevator_vertical_speed, user)))
                    return;

                show_fluff_message(UP, user);
                await open_lift_radial(user);
                break;

            case "Down":
                if (!Dm.truthy(await transport_controller_datum.simple_move_wrapper(DOWN, elevator_vertical_speed, user)))
                    return;

                show_fluff_message(DOWN, user);
                await open_lift_radial(user);
                break;

            case "Cancel":
                return;
        }
    }

    // tg: code/modules/transport/transport_module.dm:654, :661  var/static/image/up_arrow, down_arrow (rules 10.6)
    private static ref object open_lift_radial__up_arrow => ref TgState.Current.obj_structure_transport_linear__open_lift_radial__up_arrow;
    private static ref object open_lift_radial__down_arrow => ref TgState.Current.obj_structure_transport_linear__open_lift_radial__down_arrow;

    /**
     * Proc to ensure that the radial menu closes when it should.
     * Returns:
     * * boolean, FALSE if the menu should be closed, TRUE if the menu is clear to stay opened.
     */
    // tg: code/modules/transport/transport_module.dm:716
    public virtual async DmTask<object> check_menu(mob user = null, object starting_loc = null)
    {
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)) || starting_loc != loc)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/transport/transport_module.dm:721
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(radial_travel))
            return await base.attack_hand(user, modifiers, __extra);

        await open_lift_radial(user as mob_living);
        return null;
    }

    //ai probably shouldn't get to use lifts but they sure are great for admins to crush people with
    // tg: code/modules/transport/transport_module.dm:731
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(radial_travel))
            return dot;
        // isAdminGhostAI(user): admin tooling (M10+); no ghost is an admin AI ghost in the port
        return dot;
    }

    // tg: code/modules/transport/transport_module.dm:742
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(radial_travel))
            return await base.attack_paw(user, modifiers);

        await open_lift_radial(user as mob_living);
        return null;
    }

    // tg: code/modules/transport/transport_module.dm:748
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(radial_travel))
            return NONE;

        await open_lift_radial(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/transport/transport_module.dm:755
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        if (!Dm.truthy(radial_travel))
            return await base.attack_robot(user, modifiers);

        await open_lift_radial(user as mob_living);
        return null;
    }

    /**
     * Shows a message indicating that the lift has moved up or down.
     */
    // tg: code/modules/transport/transport_module.dm:767
    public virtual void show_fluff_message(object direction = null, mob user = null)
    {
        if (Dm.eq(direction, UP))
            user.visible_message(span_notice(I18n.T("[] moves the lift upwards.", user)), span_notice(I18n.T("You move the lift upwards.")));

        if (Dm.eq(direction, DOWN))
            user.visible_message(span_notice(I18n.T("[] moves the lift downwards.", user)), span_notice(I18n.T("You move the lift downwards.")));
    }
}

// tg: code/modules/transport/transport_module.dm:774
public partial class obj_machinery_door_poddoor_lift : obj_machinery_door_poddoor
{
    // tg: code/modules/transport/transport_module.dm:781
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (transport_linked_id != null) //linter and stuff
            elevator_mode = TRUE;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/transport/transport_module.dm:804
public partial class obj_structure_transport_linear_debug : obj_structure_transport_linear
{
    // tg: code/modules/transport/transport_module.dm:811
    public override async DmTask open_lift_radial(mob_living user = null)
    {
        atom starting_position = loc;
        if (!Dm.truthy(await can_open_lift_radial(user, starting_position)))
            return;
        //NORTH, SOUTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST
        open_lift_radial__tool_list ??= Dm.list(
            Dm.kv("NORTH", image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: NORTH)),
            Dm.kv("NORTHEAST", image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: NORTH)),
            Dm.kv("EAST", image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: EAST)),
            Dm.kv("SOUTHEAST", image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: EAST)),
            Dm.kv("SOUTH", image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: SOUTH)),
            Dm.kv("SOUTHWEST", image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: SOUTH)),
            Dm.kv("WEST", image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: WEST)),
            Dm.kv("NORTHWEST", image(icon: Dm.resource("icons/testing/turf_analysis.dmi"), icon_state: "red_arrow", dir: WEST))
        );

        object result = await show_radial_menu(user, this, open_lift_radial__tool_list, custom_check: Dm.@new<datum_callback>(this, nameof(can_open_lift_radial), user, starting_position), require_near: TRUE, tooltips: FALSE);
        if (!Dm.truthy(await can_open_lift_radial(user, starting_position)))
            return; // nice try
        if (result != null && !Dm.eq(result, "Cancel") && Dm.truthy(Dm.bitand(transport_controller_datum.controller_status, CONTROLS_LOCKED)))
        {
            // Only show this message if they actually wanted to move
            balloon_alert(user, I18n.T("elevator controls locked!"));
            return;
        }

        switch (result)
        {
            case "NORTH":
                await transport_controller_datum.move_transport_horizontally(NORTH);
                await open_lift_radial(user);
                break;
            case "NORTHEAST":
                await transport_controller_datum.move_transport_horizontally(NORTHEAST);
                await open_lift_radial(user);
                break;
            case "EAST":
                await transport_controller_datum.move_transport_horizontally(EAST);
                await open_lift_radial(user);
                break;
            case "SOUTHEAST":
                await transport_controller_datum.move_transport_horizontally(SOUTHEAST);
                await open_lift_radial(user);
                break;
            case "SOUTH":
                await transport_controller_datum.move_transport_horizontally(SOUTH);
                await open_lift_radial(user);
                break;
            case "SOUTHWEST":
                await transport_controller_datum.move_transport_horizontally(SOUTHWEST);
                await open_lift_radial(user);
                break;
            case "WEST":
                await transport_controller_datum.move_transport_horizontally(WEST);
                await open_lift_radial(user);
                break;
            case "NORTHWEST":
                await transport_controller_datum.move_transport_horizontally(NORTHWEST);
                await open_lift_radial(user);
                break;
            case "Cancel":
                return;
        }

        add_fingerprint(user);
    }

    // tg: code/modules/transport/transport_module.dm:816  var/static/list/tool_list (rules 10.6)
    private static ref DmList open_lift_radial__tool_list => ref TgState.Current.obj_structure_transport_linear_debug__open_lift_radial__tool_list;
}

// tg: code/modules/transport/transport_module.dm:865
public partial class obj_structure_transport_linear_tram : obj_structure_transport_linear
{
    // tg: code/modules/transport/transport_module.dm:899
    public override object add_item_on_transport(datum source = null, atom_movable item = null)
    {
        object dot = base.add_item_on_transport(source, item);
        if (Dm.truthy(travelling))
            on_changed_glide_size(item, item.glide_size);
        return dot;
    }

    // tg: code/modules/transport/transport_module.dm:904
    public virtual void set_travelling(object travelling = null)
    {
        if (Dm.eq(this.travelling, travelling))
            return;

        foreach (atom_movable glider in Dm.each<atom_movable>(transport_contents))
        {
            if (Dm.truthy(travelling))
            {
                glider.set_glide_size(glide_size_override);
                RegisterSignal(glider, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE, nameof(on_changed_glide_size));
            }
            else
            {
                Dm.remove(changed_gliders, glider);
                UnregisterSignal(glider, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE);
            }
        }

        this.travelling = Dm.num(travelling);
    }

    // tg: code/modules/transport/transport_module.dm:918
    public override object set_currently_z_moving(object new_z_moving_value = null, float? forced = null)
    {
        return FALSE; //trams can never z fall and shouldnt waste any processing time trying to do so
    }

    /**
     * Handles unlocking the tram controls for use after moving
     *
     * More safety checks to make sure the tram has actually docked properly
     * at a location before users are allowed to interact with the tram console again.
     * Tram finds its location at this point before fully unlocking controls to the user.
     */
    // tg: code/modules/transport/transport_module.dm:928
    public virtual void unlock_controls()
    {
        foreach (obj_structure_transport_linear_tram tram_part in Dm.each_cast<obj_structure_transport_linear_tram>(transport_controller_datum.transport_modules)) //only thing everyone needs to know is the new location.
        {
            tram_part.set_travelling(FALSE);
            transport_controller_datum.controls_lock(FALSE);
        }
    }

    ///debug proc to highlight the locs of the tram platform
    // tg: code/modules/transport/transport_module.dm:934
    public virtual void find_dimensions(float? iterations = null)
    {
        iterations ??= 100;
        message_admins(Dm.text("num turfs: []", length(locs)));

        Type overlay = typeof(obj_effect_overlay_ai_detect_hud);
        DmList turfs = Dm.list();

        foreach (turf our_turf in Dm.each<turf>(locs))
        {
            Dm.@new(overlay, our_turf);
            Dm.append(ref turfs, our_turf);
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_turfs), turfs, iterations), 0.1f * SECONDS, file: "code/modules/transport/transport_module.dm", line: 945);
    }

    // tg: code/modules/transport/transport_module.dm:947
    public virtual void clear_turfs(DmList turfs_to_clear = null, object iterations = null)
    {
        foreach (turf our_old_turf in Dm.each<turf>(turfs_to_clear))
        {
            obj_effect_overlay_ai_detect_hud hud = Dm.locate<obj_effect_overlay_ai_detect_hud>(our_old_turf);
            if (hud != null)
                qdel(hud);
        }

        Type overlay = typeof(obj_effect_overlay_ai_detect_hud);

        foreach (turf our_turf in Dm.each<turf>(locs))
            Dm.@new(overlay, our_turf);

        float left = Dm.num(iterations) - 1;

        DmList turfs = Dm.list();
        foreach (turf our_turf in Dm.each<turf>(locs))
            Dm.append(ref turfs, our_turf);

        if (left != 0)
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_turfs), turfs, left), 0.1f * SECONDS, file: "code/modules/transport/transport_module.dm", line: 966);
    }

    // tg: code/modules/transport/transport_module.dm:968
    public virtual async DmTask estop_throw(object throw_direction = null)
    {
        foreach (mob_living passenger in Dm.each<mob_living>(transport_contents.Copy()))
        {
            float mob_throw_chance = transport_controller_datum.throw_chance;
            if (prob(Dm.truthy(mob_throw_chance) ? mob_throw_chance : 17.5f) || HAS_TRAIT(passenger, TRAIT_CURSED)) // sometimes you go through a window, especially with bad luck
                Waiting.NoSleep(passenger.AddElement(typeof(datum_element_window_smashing), Dm.named("duration", 1.5f * SECONDS)));
            atom throw_target = get_edge_target_turf(this, throw_direction) as atom;
            await passenger.throw_at(throw_target, 30, 7, force: MOVE_FORCE_OVERPOWERING);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/modules/transport/transport_module.dm:115  var/static/list/registrations
    internal DmList obj_structure_transport_linear__unset_movement_registrations__registrations;
    // tg: code/modules/transport/transport_module.dm:139  var/static/list/blacklisted_types
    internal DmList obj_structure_transport_linear__add_item_on_transport__blacklisted_types;
    // tg: code/modules/transport/transport_module.dm:654  var/static/image/up_arrow
    internal object obj_structure_transport_linear__open_lift_radial__up_arrow;
    // tg: code/modules/transport/transport_module.dm:661  var/static/image/down_arrow
    internal object obj_structure_transport_linear__open_lift_radial__down_arrow;
    // tg: code/modules/transport/transport_module.dm:816  var/static/list/tool_list
    internal DmList obj_structure_transport_linear_debug__open_lift_radial__tool_list;
}

public static partial class GlobalProcs
{
    /// <summary>
    /// DEVIATION DV404: BYOND's <c>locate(type) in turf</c> for a transport module. A merged tram is one movable as wide as
    /// the car (bound_width, bound_height), and BYOND holds it in the contents of every turf it covers; the port holds a
    /// movable only in its loc (atom_movable.builtin.cs, locs), so a tram is also found on the other turfs of its locs.
    /// </summary>
    public static T locate_transport<T>(object where) where T : obj_structure_transport_linear
    {
        T found = Dm.locate<T>(where);
        if (found != null || where is not turf t)
            return found;
        foreach (obj_structure_transport_linear_tram module in Dm.each<obj_structure_transport_linear_tram>())
            if (module is T covering && Dm.eq(module.z, t.z) && module.loc is turf && Dm.@in(t, module.locs))
                return covering;
        return null;
    }
}
