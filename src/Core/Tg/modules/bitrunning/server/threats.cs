// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/server/threats.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the server's threats: the glitch's poll and spawn, the threat alerts and the emagged
// server's station spawn. The glitch's antagonists (antagonists/) are gaps BR2's.

// tg: code/modules/bitrunning/server/threats.dm:1
public partial class obj_machinery_quantum_server : obj_machinery
{
    /// Adds threats to the list and notifies players
    // tg: code/modules/bitrunning/server/threats.dm:2
    public virtual async DmTask add_threats(mob_living threat = null)
    {
        spawned_threat_refs.Add(WEAKREF(threat));
        SEND_SIGNAL(COMSIG_BITRUNNER_THREAT_CREATED, Dm.list(this));
        await threat.AddComponent(typeof(datum_component_virtual_entity), this);
    }

    /// Choses which antagonist role is spawned based on threat
    // tg: code/modules/bitrunning/server/threats.dm:9
    public virtual object get_antagonist_role()
    {
        DmList available = Dm.list();

        foreach (Type subtype in Dm.each<Type>(subtypesof(typeof(datum_antagonist_bitrunning_glitch))))
        {
            if (threat >= Dm.initial(subtype, (datum_antagonist_bitrunning_glitch p) => p.threat))
                Dm.append(ref available, subtype);
        }

        shuffle_inplace(available);
        Type chosen = pick(available) as Type;

        threat -= Dm.initial(chosen, (datum_antagonist_bitrunning_glitch p) => p.threat) * 0.5f;

        return chosen;
    }

    /// Selects a target to mutate. Gives two attempts, then crashes if it fails.
    // tg: code/modules/bitrunning/server/threats.dm:25
    public virtual object get_mutation_target()
    {
        datum_weakref target_ref = pick(mutation_candidate_refs) as datum_weakref;
        mob_living resolved = target_ref.resolve() as mob_living;

        if (resolved != null)
            return resolved;

        mutation_candidate_refs.Remove(target_ref);
        if (!Dm.truthy(length(mutation_candidate_refs)))
            return null;

        target_ref = pick(mutation_candidate_refs) as datum_weakref;
        resolved = target_ref.resolve() as mob_living;
        return resolved;
    }

    /// Finds any mobs with minds in the zones and gives them the bad news
    // tg: code/modules/bitrunning/server/threats.dm:42
    public virtual void notify_spawned_threats()
    {
        foreach (datum_weakref baddie_ref in Dm.each_cast<datum_weakref>(spawned_threat_refs))
        {
            mob_living baddie = baddie_ref.resolve() as mob_living;
            if (baddie?.mind == null || IS_UNCONSCIOUS(baddie))
                continue;

            atom_movable_screen_alert_bitrunning alert = baddie.throw_alert(
                ALERT_BITRUNNER_RESET,
                typeof(atom_movable_screen_alert_bitrunning),
                new_master: this) as atom_movable_screen_alert_bitrunning;
            alert.name = "Queue Deletion";
            alert.desc = "The server is resetting. Oblivion awaits.";

            to_chat(baddie, span_userdanger(I18n.T("You have been flagged for deletion! Thank you for your service.")));
        }
    }

    /// Removes a specific threat - used when station spawning
    // tg: code/modules/bitrunning/server/threats.dm:60
    public virtual void remove_threat(mob_living threat = null)
    {
        spawned_threat_refs.Remove(WEAKREF(threat));
    }

    /// Selects the role and waits for a ghost orbiter
    // tg: code/modules/bitrunning/server/threats.dm:65
    public virtual async DmTask<object> setup_glitch(Type forced_role = null)
    {
        if (!Dm.truthy(validate_mutation_candidates()))
            return null;

        mob_living mutation_target = get_mutation_target() as mob_living;
        if (mutation_target == null)
            throw Dm.CRASH("vdom: After two attempts, no valid mutation target was found.", "code/modules/bitrunning/server/threats.dm", 71);

        atom thing = mutation_target;
        await thing.create_digital_aura();

        Type chosen_role = forced_role ?? get_antagonist_role() as Type;
        string role_name = Dm.initial(chosen_role, (datum_antagonist_bitrunning_glitch p) => p.name);
        mob chosen_one = await SSpolling.poll_ghosts_for_target(
            question: "<span class='ooc'>A temporary antagonist role is spawning in the virtual domain.</span>\n<span class='boldnotice'>You will return to your previous body on conclusion.</span>",
            check_jobban: ROLE_GLITCH,
            poll_time: 20 * SECONDS,
            checked_target: mutation_target,
            ignore_category: POLL_IGNORE_GLITCH,
            alert_pic: mutation_target,
            role_name_text: Dm.text("Malfunction: []", role_name)) as mob;
        await spawn_glitch(chosen_role, mutation_target, chosen_one as mob_dead_observer);
        return mutation_target;
    }

    /// Orbit poll has concluded - spawn the antag
    // tg: code/modules/bitrunning/server/threats.dm:93
    public virtual async DmTask spawn_glitch(Type chosen_role = null, mob_living mutation_target = null, mob_dead_observer ghost = null)
    {
        if (QDELETED(mutation_target))
            return;

        if (QDELETED(this) || ghost == null || generated_domain == null || !Dm.truthy(is_ready) || !Dm.truthy(is_operational))
        {
            atom thing = mutation_target;
            await thing.remove_digital_aura();
            return;
        }

        string role_name = Dm.initial(chosen_role, (datum_antagonist_bitrunning_glitch p) => p.name);

        mob_living new_mob;
        if (Dm.eq(role_name, ROLE_NETGUARDIAN))
            new_mob = await Dm.new_async<mob_living_basic_netguardian>(mutation_target.loc);
        else // any other humanoid mob
            new_mob = await Dm.new_async<mob_living_carbon_human>(mutation_target.loc);

        await mutation_target.gib(DROP_ALL_REMAINS);

        datum_mind ghost_mind = ghost.mind;
        new_mob.PossessByPlayer(ghost.key);

        if (ghost_mind != null)
            await new_mob.AddComponent(typeof(datum_component_temporary_body), ghost_mind, Dm.named("return_on_death", TRUE));

        datum_mind antag_mind = new_mob.mind;
        await antag_mind.add_antag_datum(chosen_role);
        antag_mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_bitrunning_glitch)) as datum_job);

        playsound(new_mob, Dm.resource("sound/effects/magic/ethereal_exit.ogg"), 50, vary: TRUE);
        message_admins(Dm.text("[] has been made into virtual antagonist by an event.", ADMIN_LOOKUPFLW(new_mob)));
        new_mob.log_message("was spawned as a virtual antagonist by an event.", LOG_GAME);

        await add_threats(new_mob);
    }

    /// Oh boy - transports the antag station side
    // tg: code/modules/bitrunning/server/threats.dm:131
    public virtual async DmTask station_spawn(mob_living antag = null, obj_machinery_byteforge chosen_forge = null, turf goal_turf = null)
    {
        antag.balloon_alert(antag, I18n.T("scanning..."));
        chosen_forge.setup_particles(angry: TRUE);
        obj_machinery_announcement_system aas = get_announcement_system(null, this, Dm.list(RADIO_CHANNEL_SUPPLY));
        if (aas != null)
            await aas.broadcast(I18n.T("QUANTUM SERVER ALERT: Security breach detected. Unauthorized entry sequence in progress..."), Dm.list(RADIO_CHANNEL_SUPPLY));
        SEND_SIGNAL(COMSIG_BITRUNNER_STATION_SPAWN, Dm.list(this));

        float timeout = 2 * SECONDS;
        if (!ishuman(antag))
        {
            if (aas != null)
                await aas.broadcast(I18n.T("QUANTUM SERVER ALERT: Fabrication protocols have crashed unexpectedly. Please evacuate the area."), Dm.list(RADIO_CHANNEL_SUPPLY));
            timeout = 10 * SECONDS;
        }

        float bitrunners_alive = 0;
        bool island_brawl_exception = generated_domain is datum_lazy_template_virtual_domain_island_brawl;
        foreach (datum_weakref bitrunner_ref in Dm.each<datum_weakref>(avatar_connection_refs))
        {
            mob_living bitrunner = (bitrunner_ref.resolve() as datum_component_avatar_connection)?.parent as mob_living;
            if (bitrunner == null)
                continue;
            if (IS_UNCONSCIOUS_OR_CRIT(bitrunner) || bitrunner.client == null)
                continue;
            if (island_brawl_exception)
            {
                timeout *= Math.Max(5 - generated_domain.main_crate_points, 1);
                continue;
            }
            bitrunners_alive++;
            timeout *= 5;
        }
        if (Dm.truthy(bitrunners_alive))
            to_chat(antag, span_warning(I18n.T("[] criminals still remain here, pilfering your domain. It will be more difficult to leave until they are handled.", bitrunners_alive)));

        timeout -= 0.5f * SECONDS;

        if (!Dm.truthy(await do_after(antag, 0.5f * SECONDS, timed_action_flags: IGNORE_USER_LOC_CHANGE)) || QDELETED(antag) || antag.loc != goal_turf || QDELETED(chosen_forge) || QDELETED(this))
            return;
        if (!Dm.truthy(await do_after(antag, timeout)) || QDELETED(chosen_forge) || QDELETED(antag) || QDELETED(this) || !Dm.truthy(is_ready) || !Dm.truthy(is_operational))
        {
            chosen_forge.setup_particles();
            return;
        }

        datum_component_glitch effect = await antag.AddComponent(typeof(datum_component_glitch),
            Dm.named("server", this),
            Dm.named("forge", chosen_forge)) as datum_component_glitch;

        await chosen_forge.flicker(angry: TRUE);
        if (!Dm.truthy(await do_after(antag, 1 * SECONDS)))
        {
            chosen_forge.setup_particles();
            qdel(effect);
            return;
        }

        await chosen_forge.flash();

        if (ishuman(antag))
            await reset_equipment(antag as mob_living_carbon_human);
        else if (aas != null)
            await aas.broadcast(I18n.T("QUANTUM SERVER CRITICAL ALERT: Unregistered mechanical entity deployed."), Dm.list());

        datum_antagonist antag_datum = antag.mind?.has_antag_datum(typeof(datum_antagonist_bitrunning_glitch)) as datum_antagonist;
        if (antag_datum != null)
            antag_datum.show_in_roundend = TRUE;

        datum_component temp_body = antag.GetComponent(typeof(datum_component_temporary_body)) as datum_component;
        if (temp_body != null)
            qdel(temp_body);

        await do_teleport(antag, get_turf(chosen_forge), forced: TRUE, asoundin: Dm.resource("sound/effects/magic/ethereal_enter.ogg"), asoundout: Dm.resource("sound/effects/magic/ethereal_exit.ogg"), channel: TELEPORT_CHANNEL_QUANTUM);
        Dm.Deferred("code/modules/bitrunning/server/threats.dm:197", "station_spawn: antag.client?.give_award(/datum/award/achievement/misc/red_pill, antag), the achievements (D332)");
    }

    /// Removes any invalid candidates from the list
    // tg: code/modules/bitrunning/server/threats.dm:199
    public virtual object validate_mutation_candidates()
    {
        foreach (datum_weakref creature_ref in Dm.each_cast<datum_weakref>(mutation_candidate_refs))
        {
            mob_living creature = creature_ref?.resolve() as mob_living;
            if (creature == null || creature.mind != null)
                mutation_candidate_refs.Remove(creature_ref);
        }

        if (!Dm.truthy(length(mutation_candidate_refs)))
            return FALSE;

        shuffle_inplace(mutation_candidate_refs);

        return TRUE;
    }
}
