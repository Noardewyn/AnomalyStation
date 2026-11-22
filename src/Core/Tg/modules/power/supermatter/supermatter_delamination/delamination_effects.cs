// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 delamination slice (G6, D163): the whole file. Left out as marked leaves: the hallucinations of effect_demoralize
// (M8, D166). The delta alert since gaps R4. The announcements came with M9
// slice D, the farewell, which names the station, with slice D2 (station_name).

// These are supposed to be discrete effects so we can tell at a glance what does each override
// of [/datum/sm_delam/proc/delaminate] does.
// Please keep them discrete and give them proper, descriptive function names.
// Oh and all of them returns true if the effect succeeded.

// tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:1
public partial class datum_sm_delam : datum
{
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:1  #define DELAM_MAX_DEVASTATION 17.5 (file-local, rules 11.4)
    private const float DELAM_MAX_DEVASTATION = 17.5f;

    /// Irradiates mobs around 20 tiles of the sm.
    /// Just the mobs apparently.
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:10
    public virtual async DmTask<object> effect_irradiate(obj_machinery_power_supermatter_crystal sm = null)
    {
        turf sm_turf = get_turf(sm);
        foreach (mob_living victim in Dm.each<mob_living>(range(DETONATION_RADIATION_RANGE, sm)))
        {
            if (!Dm.truthy(is_valid_z_level(get_turf(victim), sm_turf)))
                continue;
            if (victim.z == 0)
                continue;
            SSradiation.irradiate(victim);
        }
        return TRUE;
    }

    /// Hallucinates and makes mobs in Z level sad.
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:21
    public virtual async DmTask<object> effect_demoralize(obj_machinery_power_supermatter_crystal sm = null)
    {
        turf sm_turf = get_turf(sm);
        foreach (object victim__it in Dm.each(GLOB.alive_mob_list))
        {
            mob_living victim = victim__it as mob_living;
            if (!(victim is mob_living) || !Dm.truthy(is_valid_z_level(get_turf(victim), sm_turf)))
                continue;
            if (victim.z == 0)
                continue;

            //Hilariously enough, running into a closet should make you get hit the hardest.
            //duration between min and max, calculated by distance from the supermatter and size of the delam explosion
            float hallucination_amount = LERP(DETONATION_HALLUCINATION_MIN, DETONATION_HALLUCINATION_MAX, 1 - get_dist(victim, sm) / 128) * LERP(0.75f, 1.25f, Dm.num(calculate_explosion(sm)) * 0.5f / DELAM_MAX_DEVASTATION);
            await victim.adjust_hallucinations(hallucination_amount);
        }

        foreach (mob victim in Dm.each<mob>(GLOB.player_list))
        {
            turf victim_turf = get_turf(victim);
            if (!Dm.truthy(is_valid_z_level(victim_turf, sm_turf)))
                continue;
            victim.playsound_local(victim_turf, Dm.resource("sound/effects/magic/charge.ogg"));
            if (victim.z == 0) //victim is inside an object, this is to maintain an old bug turned feature with lockers n shit i guess. tg issue #69687
            {
                string message = "";
                atom location = victim.loc;
                if (location is obj_structure_disposalholder) // sometimes your loc can be a disposalsholder when you're inside a disposals type, so let's just pass a message that makes sense.
                    message = I18n.T("You hear a lot of rattling in the disposal pipes around you as reality itself distorts. Yet, you feel safe.");
                else
                    message = I18n.T("You hold onto \\the [] as hard as you can, as reality distorts around you. You feel safe.", victim.loc);
                to_chat(victim, span_bolddanger(message));
                continue;
            }
            to_chat(victim, span_bolddanger(I18n.T("You feel reality distort for a moment...")));
            if (isliving(victim))
            {
                mob_living living_victim = (mob_living)victim;
                await living_victim.add_mood_event("delam", typeof(datum_mood_event_delam));
            }
        }
        return TRUE;
    }

    /// Spawns anomalies all over the station. Half instantly, the other half over time.
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:55
    public virtual object effect_anomaly(obj_machinery_power_supermatter_crystal sm = null)
    {
        float anomalies = 10;
        DmList anomaly_types = Dm.list(Dm.kv(GRAVITATIONAL_ANOMALY, 55), Dm.kv(HALLUCINATION_ANOMALY, 45), Dm.kv(DIMENSIONAL_ANOMALY, 35), Dm.kv(BIOSCRAMBLER_ANOMALY, 35), Dm.kv(FLUX_ANOMALY, 25), Dm.kv(PYRO_ANOMALY, 5), Dm.kv(VORTEX_ANOMALY, 1));
        DmList anomaly_places = GLOB.generic_event_spawns;

        // Spawns this many anomalies instantly. Spawns the rest with callbacks.
        float cutoff_point = round(anomalies * 0.5f, 1);

        for (float i = 1, i__end = anomalies; i <= i__end; i++)
        {
            object anomaly_to_spawn = pick_weight(anomaly_types);
            object anomaly_location = pick_n_take(anomaly_places);

            if (i < cutoff_point)
            {
                supermatter_anomaly_gen(anomaly_location, (string)anomaly_to_spawn, has_changed_lifespan: FALSE);
                continue;
            }

            float current_spawn = rand(5 * SECONDS, 10 * SECONDS);
            float next_spawn = rand(5 * SECONDS, 10 * SECONDS);
            float extended_spawn = 0;
            if (SPT_PROB(1, next_spawn))
                extended_spawn = rand(5 * MINUTES, 15 * MINUTES);
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.supermatter_anomaly_gen), anomaly_location, anomaly_to_spawn, TRUE), current_spawn + extended_spawn, file: "code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm", line: 76);
        }
        return TRUE;
    }

    /// Explodes
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:80
    public virtual async DmTask<object> effect_explosion(obj_machinery_power_supermatter_crystal sm = null)
    {
        turf sm_turf = get_turf(sm);
        await explosion(origin: sm_turf,
            devastation_range: Dm.num(calculate_explosion(sm)) * 0.5f, // max 17.5
            heavy_impact_range: Dm.num(calculate_explosion(sm)) + 2, // max 37
            light_impact_range: Dm.num(calculate_explosion(sm)) + 4, // max 39
            flash_range: Dm.num(calculate_explosion(sm)) + 6, //max 41
            adminlog: TRUE,
            ignorecap: TRUE
        );
        return TRUE;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:92
    public virtual object calculate_explosion(obj_machinery_power_supermatter_crystal sm = null)
    {
        return sm.explosion_power * max(sm.gas_heat_power_generation, 0.205f);
    }

    /// Spawns a scrung and eat the SM.
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:96
    public virtual async DmTask<object> effect_singulo(obj_machinery_power_supermatter_crystal sm = null)
    {
        turf sm_turf = get_turf(sm);
        if (!Dm.truthy(sm_turf))
        {
            stack_trace(Dm.text("Supermatter [] failed to spawn singularity, cant get current turf.", sm), "code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm", 99);
            return FALSE;
        }
        obj_singularity created_singularity = Dm.@new<obj_singularity>(sm_turf);
        created_singularity.energy = 800;
        await created_singularity.consume(sm);
        return TRUE;
    }

    /// Teslas
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:107
    public virtual object effect_tesla(obj_machinery_power_supermatter_crystal sm = null)
    {
        turf sm_turf = get_turf(sm);
        if (!Dm.truthy(sm_turf))
        {
            stack_trace(Dm.text("Supermatter [] failed to spawn tesla, cant get current turf.", sm), "code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm", 110);
            return FALSE;
        }
        obj_energy_ball created_tesla = Dm.@new<obj_energy_ball>(sm_turf);
        created_tesla.energy = 200; //Gets us about 9 balls
        return TRUE;
    }

    /// Mail the shuttle off to buy milk.
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:117  (set waitfor = FALSE: the caller detaches it)
    public virtual async DmTask effect_strand_shuttle()
    {
        // set timer to infinity, so shuttle never arrives
        SSshuttle.emergency.setTimer(INFINITY);
        // disallow shuttle recalls, so people cannot cheese the timer
        SSshuttle.emergency_no_recall = TRUE;
        // set supermatter cascade to true, to prevent auto evacuation due to no way of calling the shuttle
        SSshuttle.supermatter_cascade = TRUE;
        // set hijack completion timer to infinity, so that you cant prematurely end the round with a hijack
        foreach (obj_machinery_computer_emergency_shuttle console in Dm.each_cast<obj_machinery_computer_emergency_shuttle>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_emergency_shuttle))))
            console.hijack_completion_flight_time_set = INFINITY;

        /* This logic is to keep uncalled shuttles uncalled
        In SSshuttle, there is not much of a way to prevent shuttle calls, unless we mess with admin panel vars
        SHUTTLE_STRANDED is different here, because it *can* block the shuttle from being called, however if we don't register a hostile
        environment, it gets unset immediately. Internally, it checks if the count of HEs is zero
        and that the shuttle is in stranded mode, then frees it with an announcement.
        This is a botched solution to a problem that could be solved with a small change in shuttle code, however-
        */
        if (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_IDLE))
        {
            SSshuttle.emergency.mode = SHUTTLE_STRANDED;
            SSshuttle.registerHostileEnvironment(this);
            return;
        }

        // say goodbye to that shuttle of yours
        if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE))
        {
            priority_announce(
                text: I18n.T("Fatal error occurred in emergency shuttle uplink during transit. Unable to reestablish connection."),
                title: I18n.T("Shuttle Failure"),
                sound: Dm.resource("sound/announcer/announcement/announce_dig.ogg"),
                sender_override: I18n.T("Emergency Shuttle Uplink Alert"),
                color_override: "grey");
        }
        else
        {
            // except if you are on it already, then you are safe c:
            minor_announce(I18n.T("ERROR: Corruption detected in navigation protocols. Connection with Transponder #XCC-P5831-ES13 lost. Backup exit route protocol decrypted. Calibrating route..."),
                I18n.T("Emergency Shuttle"), TRUE); // wait out until the rift on the station gets destroyed and the final message plays
            DmList mobs = mobs_in_area_type(Dm.list(typeof(area_shuttle_escape))) as DmList;
            foreach (mob_living mob in Dm.each_cast<mob_living>(mobs)) // emulate mob/living/lateShuttleMove() behaviour
            {
                if (mob.buckled != null)
                    continue;
                if (mob.client != null)
                    shake_camera(mob, 3 * SECONDS * 0.25f, 1);
                await mob.Paralyze(3 * SECONDS, TRUE);
            }
        }
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:163
    public virtual async DmTask effect_cascade_demoralize()
    {
        foreach (mob player in Dm.each<mob>(GLOB.player_list))
        {
            if (!isdead(player))
            {
                mob_living living_player = (mob_living)player;
                to_chat(player, span_bolddanger(I18n.T("Everything around you is resonating with a powerful energy. This can't be good.")));
                await living_player.add_mood_event("cascade", typeof(datum_mood_event_cascade));
            }
            // SEND_SOUND(player, 'sound/effects/magic/charge.ogg'): BYOND plays a sound file output to a mob as sound(file)
            // (SHIM-GAP: Dm.output_to plays a /sound only)
            Dm.output_to(player, sound(Dm.resource("sound/effects/magic/charge.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        }
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:171
    public virtual async DmTask effect_emergency_state()
    {
        if (!Dm.eq(SSsecurity_level.get_current_level_as_number(), SEC_LEVEL_DELTA))
            SSsecurity_level.set_level(SEC_LEVEL_DELTA);
        await make_maint_all_access();
        foreach (obj_machinery_light light_to_break in Dm.each<obj_machinery_light>(SSmachines?.get_machines_by_type_and_subtypes(typeof(obj_machinery_light))))
        {
            if (prob(35))
            {
                await light_to_break.set_major_emergency_light();
                continue;
            }
            await light_to_break.break_light_tube();
        }
    }

    /// Spawn an evacuation rift for people to go through.
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:182
    public virtual async DmTask<object> effect_evac_rift_start()
    {
        obj_cascade_portal rift = Dm.@new<obj_cascade_portal>(get_turf(pick(GLOB.generic_event_spawns)));
        priority_announce(I18n.T("We have been hit by a sector-wide electromagnetic pulse. All of our systems are heavily damaged, including those required for shuttle navigation. We can only reasonably conclude that a supermatter cascade is occurring on or near your station.\n\nEvacuation is no longer possible by conventional means; however, we managed to open a rift near the []. All personnel are hereby required to enter the rift by any means available.\n\n[] []",
            get_area_name(rift),
            Gibberish(I18n.T("Retrieval of survivors will be conducted upon recovery of necessary facilities."), FALSE, 5),
            Gibberish(I18n.T("Good luck--"), FALSE, 25)));
        return rift;
    }

    /// Announce the destruction of the rift and end the round.
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:193
    public virtual async DmTask effect_evac_rift_end()
    {
        priority_announce(Dm.text("[]", Gibberish(I18n.T("The rift has been destroyed, we can no longer help you."), FALSE, 5)));

        await sleep(25 * SECONDS);

        priority_announce(I18n.T("Reports indicate formation of crystalline seeds following resonance shift event. Rapid expansion of crystal mass proportional to rising gravitational force. Matter collapse due to gravitational pull foreseeable."),
            I18n.T("Nanotrasen Star Observation Association"));

        await sleep(25 * SECONDS);

        priority_announce(Dm.text("[]", Gibberish(I18n.T("All attempts at evacuation have now ceased, and all assets have been retrieved from your sector.\n To the remaining survivors of [], farewell.", station_name()), FALSE, 5)));

        if (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ESCAPE))
        {
            // special message for hijacks
            string shuttle_msg = Dm.text("Navigation protocol set to []. Reorienting bluespace vessel to exit vector. ETA 15 seconds.",
                Dm.truthy(SSshuttle.emergency.is_hijacked()) ? "[ERROR]" : "backup route");
            // garble the special message
            if (Dm.truthy(SSshuttle.emergency.is_hijacked()))
                shuttle_msg = Dm.str(Gibberish(shuttle_msg, TRUE, 15));
            minor_announce(shuttle_msg, I18n.T("Emergency Shuttle"), TRUE);
            SSshuttle.emergency.setTimer(15 * SECONDS);
            return;
        }

        await sleep(10 * SECONDS);

        SSticker.news_report = SUPERMATTER_CASCADE;
        SSticker.force_ending = FORCE_END_ROUND;
    }

    /// Scatters crystal mass over the event spawns as long as they are at least 30 tiles away from whatever we want to avoid.
    // tg: code/modules/power/supermatter/supermatter_delamination/delamination_effects.dm:225
    public virtual void effect_crystal_mass(obj_machinery_power_supermatter_crystal sm = null, object avoid = null)
    {
        Dm.@new<obj_crystal_mass>(get_turf(sm));
        DmList possible_spawns = GLOB.generic_event_spawns.Copy();
        for (float i = 1, i__end = rand(4, 6); i <= i__end; i++)
        {
            object spawn_location = null;
            do
            {
                spawn_location = pick_n_take(possible_spawns);
            }
            while (get_dist(spawn_location, avoid) < 30);
            Dm.@new<obj_crystal_mass>(get_turf(spawn_location));
        }
    }
}
