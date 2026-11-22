using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage N1c (D316) in a booted toy room: the leader's war declaration (refused for a small crew; declared, it announces the
/// war, shares 280 telecrystals among the operatives' uplinks, starts the infiltrator's challenge timer, delays the shuttle and is
/// used up); the round's events (SSevents: only events of ported types are in its pool, DV380); the beer nuke's every-vent flood of
/// beer through the station's scrubbers; the nuclear disk's weight on the lone operative event, and the event polling the ghosts and
/// dropping the one who signs up in space as the lone operative.
/// </summary>
public class SyndicateN1cTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 13)
    {
        var r = new AudioRoom(width, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() =>
        {
            // the toy room's level is the station's (as SyndicateNukeTests)
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            GLOB.the_station_areas = Dm.list(((area)get_area(bob)).type);
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    [Fact]
    public void TheN1cPiecesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_nuclear_challenge), typeof(datum_controller_subsystem_events), typeof(datum_round_event_control), typeof(datum_round_event),
            typeof(datum_round_event_ghost_role), typeof(datum_round_event_control_operative), typeof(datum_round_event_ghost_role_operative),
            typeof(datum_round_event_control_scrubber_overflow), typeof(datum_round_event_scrubber_overflow_every_vent),
            typeof(datum_dynamic_ruleset_midround_from_ghosts_nukies),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheWarDeclarationIsRefusedForASmallCrewAndDeclaredSharesTheTelecrystalsAndStartsTheChallenge()
    {
        var (r, feed, bob) = Room(1511);
        var board = r.In(() => Dm.@new<obj_item_circuitboard_computer_syndicate_shuttle>(locate(8, 4, 1)));
        r.In(() =>
        {
            var op = Dm.@new<datum_antagonist_nukeop>();
            op.send_to_spawnpoint = FALSE;
            Waiting.NoSleep(bob.mind.add_antag_datum(op));
        });
        r.Step(Second);
        var uplink = r.In(() => bob.mind.find_syndicate_uplink()) as datum_component_uplink;
        Assert.NotNull(uplink);
        float before = r.In(() => Dm.num(uplink.uplink_handler.telecrystals));
        var challenge = r.In(() =>
        {
            var made = Dm.@new<obj_item_nuclear_challenge>(bob.loc);
            foreach (obj_item held in Dm.each<obj_item>(bob.held_items).ToList())
                Waiting.NoSleep(bob.dropItemToGround(held));
            Waiting.NoSleep(bob.put_in_active_hand(made));
            return made;
        });

        // two players: too few to be worth a war
        r.In(() => Waiting.Detach(challenge.attack_self(bob)));
        r.Step(2);
        Assert.True(feed.Any("The enemy crew is too small to be worth declaring war on."));
        Assert.False(r.In(() => QDELETED(challenge)));

        // declared (what force_war ends in): the announcement, the bonus, the challenge timer, the delayed shuttle; the item is used up
        r.In(() => Waiting.Detach(challenge.war_was_declared(bob, "Surrender the disk or burn.")));
        r.Step(2);
        Assert.True(feed.Any("Surrender the disk or burn."));
        Assert.True(feed.Any("A bonus bundle of telecrystals has been granted to your team."));
        Assert.Equal(before + 280, r.In(() => Dm.num(uplink.uplink_handler.telecrystals)));
        Assert.True(r.In(() => Dm.num(board.challenge_start_time)) > 0);
        Assert.True(r.In(() => Dm.num(config.Get(typeof(datum_config_entry_number_shuttle_refuel_delay)))) >= 25 * MINUTES);
        Assert.True(r.In(() => QDELETED(challenge)));
    }

    [Fact]
    public void OnlyEventsOfPortedTypesAreInTheRoundsPool()
    {
        var (r, _, _) = Room(1512);
        var controls = r.In(() => Dm.each<datum_round_event_control>(SSevents.control).ToList());
        Assert.NotEmpty(controls);
        Assert.Contains(controls, c => c is datum_round_event_control_operative);
        Assert.Contains(controls, c => c is datum_round_event_control_scrubber_overflow_every_vent);
        Assert.All(controls, c => Assert.False(InertSeam.IsInertType(c.GetType()) || InertSeam.IsInertType(c.typepath), c.GetType().Name));
        // an event of a type not ported is set aside, as tg sets aside an event of another kind of map (DV380)
        Assert.True(r.In(() => SSevents.nonrunning_events_by_name.len) > 0);
        Assert.Contains("Robeless Casting", r.In(() => Dm.each(SSevents.nonrunning_events_by_name).Select(Dm.str).ToList())); // a wizard event (the meteors came with gaps E2b)
    }

    [Fact]
    public void TheBeerNukeFloodsTheStationsScrubbersWithBeer()
    {
        var (r, feed, _) = Room(1513);
        var scrubber = r.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_vent_scrubber_on>(locate(9, 6, 1)));
        var nuke = r.In(() => Dm.@new<obj_machinery_nuclearbomb_beer>(locate(6, 6, 1)));
        r.Step(Second);
        Assert.NotNull(r.In(() => nuke.overflow_control));

        r.In(() => Waiting.Detach(nuke.really_actually_explode(DETONATION_HIT_STATION)));
        r.Step(2);
        var flood = r.In(() => Dm.each<datum_round_event_scrubber_overflow_every_vent>(SSevents.running).FirstOrDefault());
        Assert.NotNull(flood);
        Assert.Equal(typeof(datum_reagent_consumable_ethanol_beer), r.In(() => flood.forced_reagent_type));
        // its start, five event ticks on (SSevents ticks every 2 seconds, its phase set by the MC's start): the scrubber foams beer, a
        // short foam that is gone a few seconds later, so the test looks for it each second
        obj_effect_particle_effect_fluid_foam foam = null;
        for (int sec = 0; sec < 16 && foam == null; sec++)
        {
            r.Step(Second);
            foam = r.In(() => Dm.each<obj_effect_particle_effect_fluid_foam>(Dm.world).FirstOrDefault());
        }
        Assert.True(feed.Any("The scrubbers network is experiencing a backpressure surge."));
        Assert.NotNull(foam);
        Assert.True(r.In(() => Dm.truthy((foam.reagents as datum_reagents)?.has_reagent(typeof(datum_reagent_consumable_ethanol_beer)))), "beer foam");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADiskLeftAloneRaisesTheLoneOperativeAndItsEventMakesASignedUpGhostTheLoneOperative()
    {
        var (r, feed, bob) = Room(1514, width: 24);
        var nuke = r.In(() => Dm.@new<obj_machinery_nuclearbomb_selfdestruct>(locate(10, 4, 1)));
        var disk = r.In(() => Dm.@new<obj_item_disk_nuclear>(locate(5, 6, 1)));
        r.In(() => Dm.@new<obj_effect_landmark_carpspawn>(locate(22, 7, 1)));
        r.Step(Second);
        var loneop = r.In(() => Dm.locate<datum_round_event_control_operative>(SSevents.control));
        Assert.NotNull(loneop);

        // left alone long enough the disk raises the event's weight; secured, it lowers it
        r.In(() => { loneop.weight = 0; disk.unsecured_process(-1_000_000f); });
        Assert.Equal(1f, r.In(() => loneop.weight));
        r.In(() => { loneop.weight = 100; disk.secured_process(); });
        Assert.Equal(99f, r.In(() => loneop.weight));

        // the event polls the ghosts; Gina signs up and wakes in space as the lone operative
        mob_living_carbon_human body = r.Join(20, 2, "Gina");
        client gina = body.client;
        // the lone operative on in her antagonist preferences (the lobby's Antagonists page): tg polls only the ghosts who want the role
        r.In(() => Dm.append(ref gina.prefs.be_special, ROLE_LONE_OPERATIVE));
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        var ghost = Assert.IsType<mob_dead_observer>(r.In(() => gina.mob));
        r.In(() => force_event_async(typeof(datum_round_event_control_operative), "a test"));
        atom_movable_screen_alert_poll_alert alert = null;
        for (int i = 0; i < 20 && alert == null; i++)
        {
            r.Step(Second / 2);
            alert = r.In(() => Dm.each(ghost.alerts).Select(k => ghost.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());
        }
        Assert.NotNull(alert);
        Click(r, ghost, alert);
        r.Step(32 * Second);
        var lone = r.In(() => gina.mob as mob_living_carbon_human);
        Assert.NotNull(lone);
        Assert.Same(r.In(() => locate(22, 7, 1)), r.In(() => lone.loc));
        var antag = r.In(() => lone.mind.has_antag_datum(typeof(datum_antagonist_nukeop_lone)) as datum_antagonist_nukeop_lone);
        Assert.NotNull(antag);
        // the event assigns the lone operative's job, which the operative's datum then replaces with its job_type (tg's order)
        Assert.IsType<datum_job_nuclear_operative>(r.In(() => lone.mind.assigned_role));
        // the lone operative's team targets the station's own self-destruct terminal and learns its code
        r.Step(6 * Second);
        Assert.Same(nuke, r.In(() => antag.nuke_team.tracked_nuke));
        Assert.Equal(r.In(() => Dm.str(nuke.r_code)), r.In(() => Dm.str(antag.nuke_team.memorized_code)));
        // the only runtime: the operative's outfit kicks off loading the syndicate base, whose map the toy room has no _maps folder for
        Assert.All(r.Room.RuntimeText, text => Assert.Contains("nukie_base.dmm", text));
    }
}
