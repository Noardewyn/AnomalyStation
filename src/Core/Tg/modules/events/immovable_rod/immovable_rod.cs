// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/immovable_rod/immovable_rod.dm, immovable_rod_event.dm;
// code/datums/components/deadchat_control.dm (the rod's deadchat control)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2b (D327): the immovable rod whole and its event: it flies across the station from a side toward the far edge (or homes on
// a target an admin picks), smashing every wall, thing and mob on its way, levelling up with each sentient kill; two rods meeting
// collapse into a singularity; a rod hitting a tram's front pushes the tram; a strongman with TRAIT_ROD_SUPLEX suplexes it. SSaugury
// (the ghosts' doom list) is not ported (as for the meteors, D299); the suplex's achievement waits for the achievements (owner-deferred,
// D333; its athletics experience came with gaps SL1); the event's admin setups wait for the admin event panel as the
// other events' do. The var blocks are generated (D111).

/// "What the fuck was that?!"
// tg: code/modules/events/immovable_rod/immovable_rod.dm:2
public partial class obj_effect_immovablerod : obj_effect
{
    // tg: code/modules/events/immovable_rod/immovable_rod.dm:34
    [DmParams("mapload", "target_atom", "specific_target", "force_looping")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var target_atom = Dm.arg<atom>(__extra, 0);
        var specific_target = Dm.arg<atom>(__extra, 1);
        object force_looping = Dm.arg<object>(__extra, 2) ?? FALSE;
        // if(should_augury) SSaugury.register_doom(src, SEVERITY_ROD): SSaugury is not ported (the meteors' D299)

        turf real_destination = get_turf(target_atom);
        destination_turf = real_destination;
        special_target = specific_target;
        if (!Dm.truthy(loopy_rod))
            loopy_rod = Dm.num(force_looping);

        ADD_TRAIT(this, TRAIT_FREE_HYPERSPACE_MOVEMENT, INNATE_TRAIT);

        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));

        RegisterSignals(this, Dm.list(COMSIG_ATOM_ENTERING, COMSIG_MOVABLE_TURF_INITIALIZING), nameof(on_entering_atom));

        if (special_target != null)
            GLOB.move_manager.home_onto(this, special_target);
        else
            GLOB.move_manager.move_towards(this, real_destination);
        return dot;
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:55
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(this, Dm.list(COMSIG_ATOM_ENTERING, COMSIG_MOVABLE_TURF_INITIALIZING));
        // SSaugury.unregister_doom(src): SSaugury is not ported
        destination_turf = null;
        special_target = null;
        return base.Destroy(force);
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:62
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!isobserver(user))
            return dot;

        if (!Dm.truthy(num_mobs_hit))
        {
            Dm.append(ref dot, span_notice(I18n.T("So far, this rod has not hit any mobs.")));
            return dot;
        }

        Dm.append(ref dot, I18n.T("\t<span class='notice'>So far, this rod has hit: \n\t\t[] mobs total, \n\t\t[] of which were sentient, and \n\t\t[] of which were sentient people</span>",
            num_mobs_hit, num_sentient_mobs_hit, num_sentient_people_hit));
        return dot;
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:76
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        if (Dm.truthy(href_list?["orbit"]))
        {
            var ghost = usr as mob_dead_observer;
            if (ghost != null)
                await ghost.ManualFollow(this);
        }
        return null;
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:82
    public virtual void on_entering_atom(datum source = null, atom destination = null, atom old_loc = null, DmList old_locs = null)
    {
        // SIGNAL_HANDLER
        if (Dm.truthy(destination.density) && isturf(destination))
            Waiting.Detach(Bump(destination));
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:87
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        if (loc == null)
            return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);

        foreach (atom_movable to_bump in Dm.each<atom_movable>(loc.contents.Copy()))
        {
            if ((to_bump != this) && !QDELETED(to_bump) && (Dm.truthy(to_bump.density) || isliving(to_bump)))
                await Bump(to_bump);
        }

        // If we have a special target, we should definitely make an effort to go find them.
        if (special_target != null)
        {
            turf target_turf = get_turf(special_target);

            // Did they escape the z-level? Let's see if we can chase them down!
            float z_diff = Dm.num(target_turf.z) - Dm.num(z);

            if (z_diff != 0)
            {
                object direction = z_diff > 0 ? UP : DOWN;
                var target_z_turf = get_step_multiz(this, direction) as turf;

                visible_message(span_danger(I18n.T("[] phases out of reality.", this)));

                if (!Dm.truthy(await do_teleport(this, target_z_turf)))
                {
                    // We failed to teleport. Might as well admit defeat.
                    qdel(this);
                    return null;
                }

                visible_message(span_danger(I18n.T("[] phases into reality.", this)));
                GLOB.move_manager.home_onto(this, special_target);
            }

            if (loc == target_turf)
                complete_trajectory();

            return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        }

        // If we have a destination turf, let's make sure it's also still valid.
        if (destination_turf != null)
        {
            // If the rod is a loopy_rod, run complete_trajectory() to get a new edge turf to fly to.
            // Otherwise, qdel the rod.
            if (!Dm.eq(destination_turf.z, z))
            {
                if (Dm.truthy(loopy_rod))
                {
                    complete_trajectory();
                    return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
                }

                qdel(this);
                return null;
            }

            // Did we reach our destination? We're probably on Icebox. Let's get rid of ourselves.
            // Ordinarily this won't happen as the average destination is the edge of the map and
            // the rod will auto transition to a new z-level.
            // If the rod is parallel to the destination at the world border, it is likely stuck (once again, icebox)
            float rx = Dm.num(x), ry = Dm.num(y);
            if ((loc == destination_turf) || ((ry == Dm.num(destination_turf.y) || rx == Dm.num(destination_turf.x))
                && (ry == world.maxy || rx == world.maxx || rx == 1 || ry == 1)))
            {
                qdel(this);
                return null;
            }
        }

        return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:144
    public virtual void complete_trajectory()
    {
        // We hit what we wanted to hit, time to go.
        special_target = null;
        walk_in_direction(dir);
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:149
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:152
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:155
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null) => TRUE;

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:158
    public override async DmTask<object> Bump(atom clong = null)
    {
        if (prob(10))
        {
            playsound(this, "sound/effects/bang.ogg", 50, TRUE);
            audible_message(span_danger(I18n.T("You hear a CLANG!")));
        }

        if (special_target != null && clong == special_target)
            complete_trajectory();

        // If rod meets rod, they collapse into a singularity. Yes, this means that if two wizard rods collide,
        // they ALSO collapse into a singulo.
        if (clong is obj_effect_immovablerod)
        {
            visible_message(span_danger(I18n.T("[] collides with []! This cannot end well.", this, clong)));
            await do_smoke(2, this, get_turf(this));
            var bad_luck = Dm.@new<obj_singularity>(get_turf(this));
            bad_luck.energy = 800;
            qdel(clong);
            qdel(this);
            return null;
        }

        // If we Bump into a turf, turf go boom.
        if (isturf(clong))
        {
            Dm.append(ref SSexplosions.highturf, clong);
            return await base.Bump(clong);
        }

        // If we Bump into the tram front or back, push the tram. Otherwise smash the object as usual.
        if (isobj(clong))
        {
            if (clong is obj_structure_tram && special_target == null) // istramwall(clong)
            {
                rod_vs_tram_battle();
                return await base.Bump(clong);
            }

            var clong_obj = (obj)clong;
            await clong_obj.take_damage(INFINITY, BRUTE, NONE, TRUE, dir, INFINITY);
            return await base.Bump(clong);
        }

        // If we Bump into a living thing, living thing goes splat.
        if (isliving(clong))
        {
            await penetrate((mob_living)clong);
            return await base.Bump(clong);
        }

        // If we Bump into anything else, anything goes boom.
        if (isatom(clong))
        {
            Dm.append(ref SSexplosions.high_mov_atom, clong);
            return await base.Bump(clong);
        }

        CRASH(Dm.text("[] Bump()ed into non-atom thing [] ([])", this, clong, clong?.type));
        return null;
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:204
    public virtual async DmTask penetrate(mob_living smeared_mob = null)
    {
        smeared_mob.visible_message(span_danger(I18n.T("[] is penetrated by an immovable rod!", smeared_mob)), span_userdanger(I18n.T("The rod penetrates you!")), span_danger(I18n.T("You hear a CLANG!")));

        if (smeared_mob.stat != DEAD)
        {
            num_mobs_hit++;
            if (smeared_mob.client != null)
            {
                num_sentient_mobs_hit++;
                if (iscarbon(smeared_mob))
                    num_sentient_people_hit++;
                if (Dm.truthy(dnd_style_level_up))
                {
                    transform = (transform as matrix).Scale(1.005f, 1.005f);
                    name = I18n.T("[] of sentient slaying +[]", Dm.initial(this, (atom p) => p.name), num_sentient_mobs_hit);
                }
            }
        }

        await smeared_mob.apply_damage(100, BRUTE, spread_damage: TRUE);
        await smeared_mob.apply_damage(60, BRUTE, BODY_ZONE_CHEST, wound_bonus: 20, sharpness: SHARP_POINTY);

        if (Dm.truthy(smeared_mob.density) || prob(10))
            await EX_ACT(smeared_mob, EXPLODE_HEAVY);
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:223
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (!HAS_MIND_TRAIT(user, TRAIT_ROD_SUPLEX))
            return dot;

        playsound(this, "sound/effects/meteorimpact.ogg", 100, TRUE);
        foreach (mob_living nearby_mob in Dm.each<mob_living>(urange(8, this)))
        {
            if (IS_UNCONSCIOUS_OR_CRIT(nearby_mob))
                continue;
            shake_camera(nearby_mob, 2, 3);
        }

        return await suplex_rod((mob_living)user);
    }

    /**
     * Called when someone manages to suplex the rod.
     *
     * Arguments
     * * strongman - the suplexer of the rod.
     */
    // tg: code/modules/events/immovable_rod/immovable_rod.dm:245
    public virtual async DmTask<object> suplex_rod(mob_living strongman = null)
    {
        // strongman.client?.give_award(/datum/award/achievement/jobs/feat_of_strength, strongman): the achievements (M10)
        strongman.visible_message(
            span_boldwarning(I18n.T("[] suplexes [] into the ground!", strongman, this)),
            span_warning(I18n.T("As you suplex [] into the ground, your body ripples with power!", this))
            );
        sound_to_playing_players("sound/items/handling/lead_pipe/lead_pipe_drop.ogg");
        Dm.@new<obj_structure_festivus_anchored>(drop_location());
        Dm.@new<obj_effect_anomaly_flux>(drop_location());

        bool is_heavy_gravity = Dm.num(strongman.has_gravity()) > STANDARD_GRAVITY; //If for some reason you have to suplex the rod in heavy gravity, you get the double experience here as well, why not
        float experience_gained = 100 * num_sentient_mobs_hit * (is_heavy_gravity ? 2 : 1); // We gain more expeirence the more sentient mobs the rod has taken out. The deadlier the rod, the stronger we become. At 25 sentient mobs, we instantly become a legendary athlete.
        strongman.mind?.adjust_experience(typeof(datum_skill_athletics), experience_gained);
        await strongman.apply_status_effect(typeof(datum_status_effect_exercised)); //time for a nap, you earned it

        qdel(this);
        return TRUE;
    }

    /* Below are a couple of admin helper procs when dealing with immovable rod memes. */
    /**
     * Stops your rod's automated movement. Sit... Stay... Good rod!
     */
    // tg: code/modules/events/immovable_rod/immovable_rod.dm:267
    public virtual void sit_stay_good_rod()
    {
        GLOB.move_manager.stop_looping(this);
    }

    /**
     * Allows your rod to release restraint level zero and go for a walk.
     *
     * If walkies_location is set, rod will move towards the location, chasing it across z-levels if necessary.
     * If walkies_location is not set, rod will call complete_trajectory() and follow the logic from that proc.
     *
     * Arguments:
     * * walkies_location - Any atom that the immovable rod will now chase down as a special target.
     */
    // tg: code/modules/events/immovable_rod/immovable_rod.dm:279
    public virtual void go_for_a_walk(atom walkies_location = null)
    {
        if (walkies_location != null)
        {
            special_target = walkies_location;
            GLOB.move_manager.home_onto(this, special_target);
            return;
        }

        complete_trajectory();
    }

    // tg: code/modules/events/immovable_rod/immovable_rod.dm:287
    public override async DmTask<object> deadchat_plays(float? mode = null, float? cooldown = null)
    {
        return await AddComponent(typeof(datum_component_deadchat_control_immovable_rod), mode ?? DEMOCRACY_MODE, Dm.list(), cooldown ?? 6 * SECONDS);
    }

    /**
     * Rod will walk towards edge turf in the specified direction.
     *
     * Arguments:
     * * direction - The direction to walk the rod towards: NORTH, SOUTH, EAST, WEST.
     */
    // tg: code/modules/events/immovable_rod/immovable_rod.dm:296
    public virtual void walk_in_direction(object direction = null)
    {
        destination_turf = get_edge_target_turf(this, direction) as turf;
        GLOB.move_manager.move_towards(this, destination_turf);
    }

    /**
     * Rod will push the tram to a landmark if it hits the tram from the front/back
     * while flying parallel.
     */
    // tg: code/modules/events/immovable_rod/immovable_rod.dm:304
    public virtual void rod_vs_tram_battle()
    {
        var transport_module = Dm.locate<obj_structure_transport_linear_tram>(loc);

        if (transport_module == null)
            return;

        var tram_controller = transport_module.transport_controller_datum as datum_transport_controller_linear_tram;

        if (tram_controller == null)
            return;

        object push_target = tram_controller.rod_collision(this);

        if (!Dm.truthy(push_target))
            return;

        go_for_a_walk(push_target as atom);
    }
}

/**
 * Deadchat Moves Things
 *
 * A special variant of the deadchat_control component that comes pre-baked with all the hottest inputs for spicy
 * immovable rod.
 */
// tg: code/datums/components/deadchat_control.dm:250
public partial class datum_component_deadchat_control_immovable_rod : datum_component_deadchat_control
{
    // tg: code/datums/components/deadchat_control.dm:250
    [DmParams("_deadchat_mode", "_inputs", "_input_cooldown", "_on_removal")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (parent is not obj_effect_immovablerod)
            return COMPONENT_INCOMPATIBLE;

        object dot = await base.Initialize(__extra);

        inputs["up"] = Dm.@new<datum_callback>(parent, nameof(obj_effect_immovablerod.walk_in_direction), NORTH);
        inputs["down"] = Dm.@new<datum_callback>(parent, nameof(obj_effect_immovablerod.walk_in_direction), SOUTH);
        inputs["left"] = Dm.@new<datum_callback>(parent, nameof(obj_effect_immovablerod.walk_in_direction), WEST);
        inputs["right"] = Dm.@new<datum_callback>(parent, nameof(obj_effect_immovablerod.walk_in_direction), EAST);
        return dot;
    }
}

/// Immovable rod random event.
/// The rod will spawn at some location outside the station, and travel in a straight line to the opposite side of the station
// tg: code/modules/events/immovable_rod/immovable_rod_event.dm:13
public partial class datum_round_event_immovable_rod : datum_round_event
{
    // tg: code/modules/events/immovable_rod/immovable_rod_event.dm:21
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("What the fuck was that?!", "General Alert");
    }

    // tg: code/modules/events/immovable_rod/immovable_rod_event.dm:24
    public override async DmTask<object> start()
    {
        object startside = pick(GLOB.cardinals);
        var end_turf = (turf)get_edge_target_turf((atom)get_random_station_turf(), REVERSE_DIR(Dm.num(startside)));
        turf start_turf = spaceDebrisStartLoc(startside, end_turf.z);
        var rod = Dm.@new<obj_effect_immovablerod>(start_turf, end_turf, special_target, force_looping);
        announce_to_ghosts(rod);
        return null;
    }
}
