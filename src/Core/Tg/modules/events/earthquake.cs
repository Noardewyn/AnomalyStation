// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/earthquake.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2c (D327): the whole file but the admins' epicenter picker (/datum/event_admin_setup, as the other events'): a planetary
// map's earthquake shakes a fault line near an event spawn for fifty seconds, throws whoever stands on it off their feet and then
// tears the fault open down to the level below with the explosions' damage. The earthquake victim's achievement waits for the
// achievements. The var blocks are generated (D111).

///Earthquake random event.
///Draws a curve of turfs between a high and low point. These turfs will shake and eventually "collapse", forming a cut in the station that drops to the z-level below.
///Much of the actual structural damage is done through the explosions subsystem. Objects, machines, and especially people
///that aren't moved out of the epicenter area (indicated by the wobbly tiles) will not just be thrown down a z-level, but also be destroyed/maimed in the process.
// tg: code/modules/events/earthquake.dm:5
public partial class datum_round_event_control_earthquake : datum_round_event_control
{
    // tg: code/modules/events/earthquake.dm:18
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.length(GLOB.generic_event_spawns) == 0)
            return FALSE;
        return dot;
    }
}

// tg: code/modules/events/earthquake.dm:26
public partial class datum_round_event_earthquake : datum_round_event
{
    // tg: code/modules/events/earthquake.dm:43
    public override async DmTask<object> setup()
    {
        if (special_spot != null)
            epicenter = special_spot;
        else
        {
            epicenter = get_turf(pick(GLOB.generic_event_spawns) as atom);
            // Give a bit of variance so our epicenter isn't always on the landmark.
            epicenter = locate(epicenter.x + rand(-10, 10), epicenter.y + rand(-10, 10), epicenter.z);
        }

        if (epicenter == null)
        {
            message_admins("Earthquake event failed to find a turf! generic_event_spawn landmarks may be absent or bugged. Aborting...");
            return null;
        }

        message_admins(Dm.text("An earthquake event is about to strike the [][].", get_area_name(epicenter), ADMIN_JMP(epicenter)));

        // Picks two points generally opposite from each other
        turf fracture_point_high = locate(epicenter.x + rand(4, 10), epicenter.y + rand(4, 8), epicenter.z);
        turf fracture_point_low = locate(epicenter.x - rand(4, 10), epicenter.y - rand(4, 8), epicenter.z);

        turfs_to_shred = block(fracture_point_high, fracture_point_low);

        // Now, we filter out some of the points that are a certain distance away from a rough approximation of the fault line.
        // This should create a pattern more akin to a fracture in the ground, rather than a rectangle-shaped crater of destroyed ground.
        turf high_midpoint = TURF_MIDPOINT(fracture_point_high, epicenter);
        turf low_midpoint = TURF_MIDPOINT(fracture_point_low, epicenter);

        // We populate a list with the midpoints and midpoints of midpoints to create a rough line of turfs to compare distances against.
        var turfs_to_compare = Dm.list(
            fracture_point_high,
            fracture_point_low,
            high_midpoint,
            low_midpoint,
            TURF_MIDPOINT(fracture_point_high, high_midpoint),
            TURF_MIDPOINT(fracture_point_low, low_midpoint),
            TURF_MIDPOINT(high_midpoint, epicenter),
            TURF_MIDPOINT(low_midpoint, epicenter)
        );

        // Find the shortest distance between each turf in the list and the rough fault line we've just established
        foreach (turf turf_to_check in Dm.each<turf>(turfs_to_shred.Copy()))
        {
            float nearest_distance = get_dist(turf_to_check, epicenter);
            foreach (turf turf_to_compare in Dm.each<turf>(turfs_to_compare))
                nearest_distance = min(get_dist(turf_to_check, turf_to_compare), nearest_distance);

            // If the turf is too far from any point on our fault line estimate, we remove it. If it's on the edge, we lightly damage it
            if (nearest_distance > 2)
            {
                if (nearest_distance == 3)
                    Dm.append(edges, turf_to_check);
                Dm.remove(turfs_to_shred, turf_to_check);
            }
        }

        // Grab a list of turfs below the ones we're going to destroy.
        // If we're at the bottom layer, it will just tear up the flooring a bunch (exposing it to LAVA).
        foreach (turf turf_to_quake in Dm.each<turf>(turfs_to_shred))
            Dm.append(underbelly, GET_TURF_BELOW(turf_to_quake));
        return null;
    }

    // tg: code/modules/events/earthquake.dm:91
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce("Planetary monitoring systems indicate a devastating seismic event in the near future.", "Seismic Report");
    }

    // tg: code/modules/events/earthquake.dm:94
    public override async DmTask<object> start()
    {
        notify_ghosts(
            I18n.T("The earthquake's epicenter has been located: []!", get_area_name(epicenter)),
            source: epicenter,
            header: "Rumble Rumble Rumble!"
        );
        return null;
    }

    // tg: code/modules/events/earthquake.dm:101
    public override async DmTask tick()
    {
        if (activeFor % 5 == 0) // ISMULTIPLE(activeFor, 5)
        {
            foreach (turf turf_to_quake in Dm.each<turf>(turfs_to_shred))
                turf_to_quake.Shake(pixelshiftx: 0.1f, pixelshifty: 0.1f, duration: 1 * SECONDS);
            if (activeFor % 10 == 0) // ISMULTIPLE(activeFor, 10)
            {
                foreach (mob earthquake_witness in Dm.each_cast<mob>(GLOB.player_list))
                {
                    if (!Dm.truthy(is_station_level(earthquake_witness.z)))
                        continue;
                    shake_camera(earthquake_witness, 1 * SECONDS, 1 + (activeFor % 10));
                    earthquake_witness.playsound_local(
                        earthquake_witness,
                        pick(
                            Dm.resource("sound/ambience/earth_rumble/earth_rumble_distant1.ogg"),
                            Dm.resource("sound/ambience/earth_rumble/earth_rumble_distant2.ogg"),
                            Dm.resource("sound/ambience/earth_rumble/earth_rumble_distant3.ogg"),
                            Dm.resource("sound/ambience/earth_rumble/earth_rumble_distant4.ogg")
                        ),
                        75
                    );
                }
                foreach (turf turf_to_quake in Dm.each<turf>(underbelly))
                    turf_to_quake.Shake(pixelshiftx: 0.1f, pixelshifty: 0.1f, duration: 1 * SECONDS);
            }
        }

        if (activeFor == end_when - 2)
        {
            foreach (turf turf_to_quake in Dm.each<turf>(turfs_to_shred))
            {
                turf_to_quake.Shake(pixelshiftx: 0.5f, pixelshifty: 0.5f, duration: 1 * SECONDS);
                foreach (mob_living quake_victim in Dm.each<mob_living>(turf_to_quake.contents.Copy()))
                {
                    await quake_victim.Knockdown(7 * SECONDS);
                    await quake_victim.Paralyze(5 * SECONDS);
                    to_chat(quake_victim, span_warning(I18n.T("The ground quakes violently beneath you, throwing you off your feet!")));
                }
            }
            foreach (turf turf_to_quake in Dm.each<turf>(underbelly))
            {
                turf_to_quake.Shake(pixelshiftx: 0.5f, pixelshifty: 0.5f, duration: 1 * SECONDS);
                foreach (mob_living_carbon quake_victim in Dm.each<mob_living_carbon>(turf_to_quake.contents.Copy()))
                    to_chat(quake_victim, span_warning(I18n.T("Damn, I wonder what that rumbling noise is?"))); ///You're about to find out
            }
        }

        // Step one of the destruction, which clears natural tiles out from the underbelly and does a bit of initial damage to the topside.
        if (activeFor == end_when - 1)
        {
            foreach (turf turf_to_shred in Dm.each<turf>(turfs_to_shred))
            {
                if (prob(90))
                    Dm.append(ref SSexplosions.lowturf, turf_to_shred);
            }
            foreach (turf turf_to_clear in Dm.each<turf>(underbelly))
            {
                if (ismineralturf(turf_to_clear))
                {
                    var rock_to_clear = (turf_closed_mineral)turf_to_clear;
                    await rock_to_clear.gets_drilled();
                }
            }
            foreach (turf turf_to_quake in Dm.each<turf>(edges))
                turf_to_quake.Shake(pixelshiftx: 0.5f, pixelshifty: 0.5f, duration: 1 * SECONDS);
            playsound(epicenter, "sound/misc/metal_creak.ogg", 125, TRUE);
        }
    }

    // tg: code/modules/events/earthquake.dm:146
    public override async DmTask end()
    {
        playsound(epicenter, "sound/ambience/earth_rumble/earth_rumble.ogg", 125);
        foreach (mob earthquake_witness in Dm.each_cast<mob>(GLOB.player_list))
        {
            if (!Dm.truthy(is_station_level(earthquake_witness.z)) || !Dm.truthy(is_mining_level(earthquake_witness.z)))
                continue;
            shake_camera(earthquake_witness, 2 * SECONDS, 4);
            earthquake_witness.playsound_local(earthquake_witness, Dm.resource("sound/effects/explosion/explosionfar.ogg"), 75);
        }

        // Step two of the destruction, which detonates the turfs in the earthquake zone. There is no actual explosion, meaning stuff around the earthquake zone is perfectly safe.
        // All turfs, and everything else that IS in the earthquake zone, however, will behave as if it were bombed.
        // If you are caught in the earthquake zone, you will not only die but probably be torn apart in the process.
        foreach (turf turf_to_shred in Dm.each<turf>(turfs_to_shred))
        {
            if (prob(10))
                Dm.append(ref SSexplosions.medturf, turf_to_shred);
            else
                Dm.append(ref SSexplosions.highturf, turf_to_shred);

            if (isasteroidturf(turf_to_shred) && prob(95))
                await turf_to_shred.ScrapeAway(flags: CHANGETURF_INHERIT_AIR);

            // for(var/mob/living/carbon/quake_victim in turf_to_shred) quake_victim.client?.give_award(/datum/award/achievement/misc/earthquake_victim, quake_victim):
            // the achievements (M10)
        }

        foreach (turf edge_to_damage in Dm.each<turf>(edges))
        {
            if (prob(25))
                Dm.append(ref SSexplosions.medturf, edge_to_damage);
            else
                Dm.append(ref SSexplosions.lowturf, edge_to_damage);
        }
    }
}
