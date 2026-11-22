// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/server/map_handling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the domain's boot (the template lazily loaded into a reservation, its landmarks read, the
// cache and curiosities placed), its shutdown and the scrub. The domain maps are converted .as3dmap files mounted at tg's
// _maps/virtual_domains (MapSeam).

// tg: code/modules/bitrunning/server/map_handling.dm:1
public partial class obj_machinery_quantum_server : obj_machinery
{
    // tg: code/modules/bitrunning/server/map_handling.dm:1  (#undef at :248)
    private const float POLLING_COOLDOWN_TIME = 2 * MINUTES;

    /// Gives all current occupants a notification that the server is going down
    // tg: code/modules/bitrunning/server/map_handling.dm:4
    public virtual async DmTask begin_shutdown(mob user = null)
    {
        if (generated_domain == null)
            return;

        if (!Dm.truthy(length(avatar_connection_refs)))
        {
            balloon_alert_to_viewers(I18n.T("powering down domain..."));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 40, vary: TRUE);
            reset();
            return;
        }

        balloon_alert_to_viewers(I18n.T("notifying clients..."));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 100, vary: TRUE);
        user.visible_message(
            span_danger(I18n.T("[] begins depowering the server!", user)),
            span_notice(I18n.T("You start disconnecting clients...")),
            span_danger(I18n.T("You hear frantic keying on a keyboard.")));

        SEND_SIGNAL(COMSIG_BITRUNNER_SHUTDOWN_ALERT, Dm.list(this, user));

        if (!Dm.truthy(await do_after(user, 20 * SECONDS, this)))
            return;

        reset();
    }

    /// Links all the loading processes together - does validation for booting a map
    // tg: code/modules/bitrunning/server/map_handling.dm:31
    public virtual async DmTask<object> cold_boot_map(object map_key = null, object was_random_selection = null)
    {
        if (!Dm.truthy(is_ready))
            return FALSE;

        if (map_key == null)
        {
            balloon_alert_to_viewers(I18n.T("no domain specified!"));
            return FALSE;
        }

        if (generated_domain != null)
        {
            balloon_alert_to_viewers(I18n.T("stop the current domain first!"));
            return FALSE;
        }

        if (Dm.truthy(length(avatar_connection_refs)))
        {
            balloon_alert_to_viewers(I18n.T("all clients must disconnect!"));
            return FALSE;
        }

        is_ready = FALSE;
        playsound(this, Dm.resource("sound/machines/terminal/terminal_processing.ogg"), 30, 2);

        /// If any one of these fail, it reverts the entire process
        if (!Dm.truthy(await load_domain(map_key)) || !Dm.truthy(await load_map_items()) || !Dm.truthy(await load_mob_segments()))
        {
            balloon_alert_to_viewers(I18n.T("initialization failed!"));
            scrub_vdom();
            is_ready = TRUE;
            return FALSE;
        }

        //We will want to record how the domain was selected. Either entirely randomly, with the name redacted, or with full information.
        //Without this, it is difficult to determine what domains are selected more often intentionallly, vs unintentionally.
        string selection_type;

        if (Dm.truthy(was_random_selection))
            selection_type = "random selection";
        else
        {
            if (Dm.truthy(generated_domain.can_view_name(scanner_tier, points)))
                selection_type = "full information";
            else
                selection_type = "redacted information";
        }
        SSblackbox.record_feedback("nested tally", "bitrunning_domain_loaded", 1, Dm.list(selection_type, map_key));

        is_ready = TRUE;

        float spawn_chance = clamp((threat * glitch_chance), 5, threat_prob_max);
        if (prob(spawn_chance))
            await setup_glitch();

        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 30, vary: TRUE);
        balloon_alert_to_viewers(I18n.T("domain loaded."));
        generated_domain.start_time = world.time;
        points -= generated_domain.cost;
        update_use_power(ACTIVE_POWER_USE);
        update_appearance();

        if (Dm.truthy(broadcasting))
            start_broadcasting_network(BITRUNNER_CAMERA_NET);

        if (Dm.truthy(generated_domain.announce_to_ghosts))
        {
            notify_ghosts(I18n.T("Bitrunners have loaded a domain that offers ghost interactions. Check the spawners menu for more information."),
                this,
                I18n.T("Matrix Glitch"));
        }

        return TRUE;
    }

    /// Initializes a new domain if the given key is valid and the user has enough points
    // tg: code/modules/bitrunning/server/map_handling.dm:97
    public virtual async DmTask<object> load_domain(object map_key = null)
    {
        foreach (datum_lazy_template_virtual_domain available in Dm.each<datum_lazy_template_virtual_domain>(SSbitrunning.all_domains))
        {
            if (Dm.eq(map_key, available.key) && points >= available.cost)
            {
                generated_domain = available;
                break;
            }
        }

        if (generated_domain == null)
            return FALSE;

        if (Dm.truthy(generated_domain.mission_min_candidates) && !(polling_cooldown <= world.time) /* COOLDOWN_FINISHED */)
        {
            await say(I18n.T("Advanced NPC algorithms resetting, please wait [] or load a different domain.", DisplayTimeText(polling_cooldown)));
            playsound(this, Dm.text("sound/machines/buzz-[].ogg", pick("sigh", "two")), 50, TRUE);
            return FALSE;
        }

        DmList lucky_ghosts = null;
        if (Dm.truthy(generated_domain.mission_min_candidates))
        {
            playsound(this, Dm.resource("sound/machines/chime.ogg"), 50, TRUE);
            await say(I18n.T("Loading advanced NPCs..."));
            DmList candidates = await SSpolling.poll_ghost_candidates(I18n.T("Do you want to play as a virtual [] in a bitrunner domain?", generated_domain.spawner_role), ROLE_GHOST_ROLE, ROLE_GHOST_ROLE, 15 * SECONDS, POLL_IGNORE_SHUTTLE_DENIZENS, TRUE) as DmList;
            for (float amount = 1, amount__end = generated_domain.mission_max_candidates; amount <= amount__end; amount++)
            {
                if (Dm.truthy(length(candidates))) // If no candidates, fails in code below anyways
                    LAZYADD(ref lucky_ghosts, pick_n_take(candidates));
            }

            if (Dm.num(length(lucky_ghosts)) < generated_domain.mission_min_candidates)
            {
                notify_ghosts(I18n.T("Not enough candidates for []! Aborting mission!", generated_domain.spawner_role));
                playsound(this, Dm.text("sound/machines/buzz-[].ogg", pick("sigh", "two")), 50, TRUE);
                await say(I18n.T("Error! Unable to load advanced NPCs. Please try again or select different domain."));
                polling_cooldown = world.time + POLLING_COOLDOWN_TIME; // COOLDOWN_START
                return FALSE;
            }

            playsound(this, Dm.resource("sound/machines/ping.ogg"), 50, TRUE);
            await say(I18n.T("Success!"));
        }

        generated_domain.load_advanced_npcs(lucky_ghosts);
        RegisterSignal(generated_domain, COMSIG_LAZY_TEMPLATE_LOADED, nameof(on_template_loaded));
        await generated_domain.lazy_load();
        // DEVIATION(DV564): the domain's modular map roots (its safehouse) wait in Master.StartLoadingMap while the domain itself loads and
        // load on a later tick; tg's map load overruns its tick and sleeps in CHECK_TICK (lazy_template.dm:119), which lets them load
        // before load_map_items reads the safehouse's landmarks. With the port's virtual tick usage (DV3) nothing sleeps, so the server waits
        // here until they have loaded (at most 10 seconds).
        float roots_deadline = world.time + 10 * SECONDS;
        while (world.time < roots_deadline && domain_roots_loading__dv564())
            await stoplag();

        return TRUE;
    }

    // DEVIATION(DV564): whether a modular map root of the domain has not loaded its module yet. Not a tg proc.
    private static bool domain_roots_loading__dv564()
    {
        foreach (obj_modular_map_root root in Dm.each<obj_modular_map_root>())
        {
            if (!QDELETED(root) && is_area_virtual(get_area(root)))
                return true;
        }
        return false;
    }

    /// Loads in necessary map items like hololadder spawns, caches, etc
    // tg: code/modules/bitrunning/server/map_handling.dm:137
    public virtual async DmTask<object> load_map_items()
    {
        DmList goal_turfs = Dm.list();
        DmList cache_turfs = Dm.list();
        DmList curiosity_turfs = Dm.list();

        foreach (obj_effect_landmark_bitrunning thing in Dm.each<obj_effect_landmark_bitrunning>(GLOB.landmarks_list))
        {
            if (thing is obj_effect_landmark_bitrunning_hololadder_spawn)
            {
                Dm.append(ref exit_turfs, get_turf(thing));
                qdel(thing); // i'm worried about multiple servers getting confused so lets clean em up
                continue;
            }

            if (thing is obj_effect_landmark_bitrunning_cache_goal_turf)
            {
                turf tile = get_turf(thing);
                Dm.append(ref goal_turfs, tile);
                RegisterSignal(tile, COMSIG_ATOM_ENTERED, nameof(on_goal_turf_entered));
                RegisterSignal(tile, COMSIG_ATOM_EXAMINE, nameof(on_goal_turf_examined));
                qdel(thing);
                continue;
            }

            if (thing is obj_effect_landmark_bitrunning_cache_spawn)
            {
                Dm.append(ref cache_turfs, get_turf(thing));
                qdel(thing);
                continue;
            }

            if ((object)thing is obj_effect_mob_spawn)
            {
                Dm.append(ref generated_domain.ghost_spawners, thing);
                continue;
            }

            if (thing is obj_effect_landmark_bitrunning_curiosity_spawn)
            {
                Dm.append(ref curiosity_turfs, get_turf(thing));
                qdel(thing);
                continue;
            }

            if (thing is obj_effect_landmark_bitrunning_loot_signal)
            {
                generated_domain.main_crate_loc = thing.loc;
                qdel(thing);
                continue;
            }

            if (thing is obj_effect_landmark_bitrunning_permanent_exit)
            {
                turf tile = get_turf(thing);
                Dm.append(ref exit_turfs, tile);
                qdel(thing);

                Dm.@new<obj_structure_hololadder>(tile);
            }
        }

        if (!Dm.truthy(length(exit_turfs)))
            throw Dm.CRASH("Failed to find exit turfs on generated domain.", "code/modules/bitrunning/server/map_handling.dm", 186);
        if (!Dm.truthy(length(goal_turfs)))
            throw Dm.CRASH("Failed to find send turfs on generated domain.", "code/modules/bitrunning/server/map_handling.dm", 188);
        if (!Dm.truthy(await attempt_spawn_cache(cache_turfs)))
            return FALSE;

        while (Dm.truthy(length(curiosity_turfs)))
        {
            turf picked_turf = await attempt_spawn_curiosity(curiosity_turfs) as turf;
            if (picked_turf == null)
                break;
            generated_domain.secondary_loot_generated = Dm.add(generated_domain.secondary_loot_generated, 1);
            Dm.remove(ref curiosity_turfs, picked_turf);
        }

        return TRUE;
    }

    /// Stops the current virtual domain and disconnects all users
    // tg: code/modules/bitrunning/server/map_handling.dm:200
    public virtual void reset(float? fast = null)
    {
        fast ??= FALSE;
        is_ready = FALSE;
        domain_complete = FALSE;

        sever_connections();

        if (!Dm.truthy(fast))
        {
            notify_spawned_threats();
            _addtimer(Dm.@new<datum_callback>(this, nameof(scrub_vdom)), 15 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/modules/bitrunning/server/map_handling.dm", line: 209);
        }
        else
            scrub_vdom(); // used in unit testing, no need to wait for callbacks

        _addtimer(Dm.@new<datum_callback>(this, nameof(cool_off)), ROUND_UP(server_cooldown_time * capacitor_coefficient), Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), TIMER_DELETE_ME), file: "code/modules/bitrunning/server/map_handling.dm", line: 213);
        update_appearance();

        update_use_power(IDLE_POWER_USE);
        domain_randomized = FALSE;
        retries_spent = 0;

        stop_broadcasting_network(BITRUNNER_CAMERA_NET);
    }

    /// Tries to clean up everything in the domain
    // tg: code/modules/bitrunning/server/map_handling.dm:223
    public virtual void scrub_vdom()
    {
        sever_connections(); /// just in case someone's connected
        SEND_SIGNAL(COMSIG_BITRUNNER_DOMAIN_SCRUBBED, Dm.list(this)); // avatar cleanup just in case

        if (Dm.truthy(length(generated_domain.reservations)))
        {
            datum_turf_reservation res = generated_domain.reservations[1] as datum_turf_reservation;
            res.Release();
        }

        DmList creatures = Dm.add(spawned_threat_refs, mutation_candidate_refs) as DmList;
        foreach (datum_weakref creature_ref in Dm.each_cast<datum_weakref>(creatures))
        {
            mob_living creature = creature_ref?.resolve() as mob_living;
            if (creature == null)
                continue;

            qdel(creature);
        }

        generated_domain.secondary_loot_generated = 0;
        generated_domain.main_crate_points = 0;

        avatar_connection_refs.Cut();
        exit_turfs = Dm.list();
        generated_domain = null;
        mutation_candidate_refs.Cut();
        spawned_threat_refs.Cut();
    }
}
