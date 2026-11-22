using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package E2b (D327): the meteors and the space events in a booted toy station: a walled room of iron floor in the middle of
/// space, on the station's level. tg's meteors start on a space turf 13 tiles in from an edge (TRANSITIONEDGE and the pad) and fly
/// to the far side. Space dust crosses the station; a meteor wave announces itself and rains meteors; the sandstorm comes from the
/// side it names; the stray meteor is announced by its signature; the immovable rod tears through the wall and the crewman in its
/// way and levels up; two rods meeting collapse into a singularity; the dark matt-eor raises the alert to red until it misses.
/// </summary>
public class MeteorsE2bTests
{
    private const int Size = 41;
    private const int Low = 17, High = 25; // the station's walls
    private const int Edge = TRANSITIONEDGE + 5; // spaceDebrisStartLoc's pad: the start lines are x or y = 13 and Size - 13

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(Size, Size, seed: seed, mapExtras: _ =>
        {
            for (int y = 2; y < Size; y++)
            {
                for (int x = 2; x < Size; x++)
                {
                    bool inStation = x >= Low && x <= High && y >= Low && y <= High;
                    if (!inStation)
                        Dm.@new<turf_open_space>(locate(x, y, 1));
                    else if (x == Low || x == High || y == Low || y == High)
                        Dm.@new<turf_closed_wall>(locate(x, y, 1));
                    else
                        Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
                }
            }
        });
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(21, 21, "Bob");
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            // the station's area over the walled room: change_area keeps the area's lists on the turfs the map extras made
            var station = (area)Dm.@new(typeof(area_station_hallway_primary_central));
            foreach (turf t in Dm.each<turf>(block(Low, Low, 1, High, High, 1)))
                t.change_area((area)t.loc, station);
            station.requires_power = FALSE;
            GLOB.the_station_areas = Dm.list(station.type);
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Control<T>(AudioRoom r) where T : datum_round_event_control =>
        r.In(() => (T)Dm.each<datum_round_event_control>(SSevents.control).First(c => c.GetType() == typeof(T)));

    private static bool OnAStartLine(atom a) =>
        (a.x == Edge || a.x == Size - Edge || a.y == Edge || a.y == Size - Edge);

    [Fact]
    public void TheMeteorsAreLiveAndTheirEventsAreInThePool()
    {
        foreach (Type type in new[]
        {
            typeof(datum_round_event_meteor_wave), typeof(datum_round_event_meteor_wave_threatening), typeof(datum_round_event_meteor_wave_catastrophic),
            typeof(datum_round_event_meteor_wave_meaty), typeof(datum_round_event_meteor_wave_dust_storm), typeof(datum_round_event_stray_meteor),
            typeof(datum_round_event_dark_matteor), typeof(datum_round_event_space_dust), typeof(datum_round_event_sandstorm),
            typeof(datum_round_event_sandstorm_classic), typeof(datum_round_event_immovable_rod), typeof(obj_effect_immovablerod),
            typeof(obj_effect_meteor_dark_matteor), typeof(datum_meteor_mode_controller), typeof(datum_component_deadchat_control_immovable_rod),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        var (r, _, _) = Room(4401);
        var names = r.In(() => Dm.each<datum_round_event_control>(SSevents.control).Select(c => Dm.str(c.name)).ToList());
        foreach (string name in new[] { "Meteor Wave: Normal", "Meteor Wave: Threatening", "Meteor Wave: Catastrophic", "Meteor Wave: Meaty",
            "Major Space Dust", "Stray Meteor", "Dark Matt-eor", "Space Dust: Minor", "Sandstorm: Directional", "Sandstorm: Classic", "Immovable Rod" })
            Assert.Contains(name, names);
    }

    [Fact]
    public void SpaceDustFliesFromSpaceTowardTheFarSide()
    {
        var (r, _, _) = Room(4402);
        r.In(() => spawn_meteors(1, GLOB.meteors_dust));
        var dust = r.In(() => Dm.each<obj_effect_meteor>(GLOB.meteor_list).Single());
        Assert.IsType<obj_effect_meteor_dust>(dust);
        (float x, float y) start = r.In(() => (dust.x, dust.y));
        Assert.True(r.In(() => get_turf(dust) is turf_open_space && OnAStartLine(dust)), "it starts in space on a start line: " + start);
        r.Step(3 * Second);
        Assert.True(r.In(() => QDELETED(dust) || dust.x != start.x || dust.y != start.y), "it flies");
        r.Step(30 * Second);
        Assert.True(r.In(() => QDELETED(dust)), "it is gone, broken up or past the far side");
        Assert.Equal(0, r.In(() => Dm.length(GLOB.meteor_list)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMeteorWaveAnnouncesItselfAndRainsMeteors()
    {
        var (r, feed, _) = Room(4403);
        var control = Control<datum_round_event_control_meteor_wave>(r);
        r.In(() => Waiting.Detach(control.run_event()));
        r.Step(5 * Second);
        Assert.True(feed.Any("Meteors have been detected on collision course with the station."));
        var wave = r.In(() => Dm.each<datum_round_event_meteor_wave>(SSevents.running).First());
        Assert.Same(r.In(() => GLOB.meteors_normal), r.In(() => wave.wave_type));
        int before = r.In(() => (int)Dm.length(GLOB.meteor_list));
        r.In(() =>
        {
            wave.activeFor = 9;
            Waiting.NoSleep(wave.tick());
        });
        Assert.Equal(before + 5, r.In(() => (int)Dm.length(GLOB.meteor_list))); // a wave's tick every third throws five meteors
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSandstormComesFromTheSideItNames()
    {
        var (r, feed, _) = Room(4404);
        var storm = r.In(() =>
        {
            var e = Dm.@new<datum_round_event_sandstorm>(FALSE);
            Waiting.NoSleep(e.announce__proc(FALSE));
            return e;
        });
        float side = r.In(() => Dm.num(storm.start_side));
        string named = side switch { NORTH => "fore", SOUTH => "aft", EAST => "starboard", _ => "port" };
        Assert.True(feed.Any("A large wave of space dust is approaching from the " + named + " side of the station."));
        r.In(() => Waiting.NoSleep(storm.tick()));
        var meteors = r.In(() => Dm.each<obj_effect_meteor>(GLOB.meteor_list).ToList());
        Assert.Equal(15, meteors.Count);
        Assert.All(meteors, m => Assert.True(m is obj_effect_meteor_sand || m is obj_effect_meteor_dust));
        Assert.All(meteors, m => Assert.True(r.In(() => side switch
        {
            NORTH => m.y == Size - Edge,
            SOUTH => m.y == Edge,
            EAST => m.x == Size - Edge,
            _ => m.x == Edge,
        }), "every meteor starts on the named side: " + r.In(() => (m.x, m.y))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStrayMeteorIsAnnouncedByItsSignature()
    {
        var (r, feed, _) = Room(4405);
        var control = Control<datum_round_event_control_stray_meteor>(r);
        // the meteor crosses the room in about a second and the event announces a tick later, only while a meteor still flies
        // (stray_meteor_event.dm:30): whether one does leans on the seed's stream, so the event starts and announces in one tick
        r.In(() =>
        {
            var stray = Dm.@new<datum_round_event_stray_meteor>(FALSE, control);
            Waiting.NoSleep(stray.start());
            Assert.NotEmpty(Dm.each<obj_effect_meteor>(GLOB.meteor_list));
            Waiting.NoSleep(stray.announce__proc(FALSE));
        });
        r.Step(Second);
        Assert.True(feed.Any("sensors have detected an incoming signature approaching"), string.Join("\n", r.Room.RuntimeText));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheImmovableRodTearsThroughTheWallAndTheCrewmanInItsWay()
    {
        var (r, feed, bob) = Room(4406);
        var rod = r.In(() => Dm.@new<obj_effect_immovablerod>(locate(Edge, 21, 1), locate(Size - 2, 21, 1)));
        for (int i = 0; i < 20 * Second && !r.In(() => QDELETED(rod)); i++)
            r.Step(1);
        r.In(() => Waiting.NoSleep(SSexplosions.fire(null))); // the explosions' queue the rod filled, at once
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(rod)), "the rod leaves the map's far side");
        Assert.False(r.In(() => locate(Low, 21, 1) is turf_closed_wall), "the wall in its way is gone");
        Assert.True(feed.Any("The rod penetrates you!"));
        Assert.True(r.In(() => QDELETED(bob) || bob.stat == DEAD || Dm.num(bob.get_brute_loss()) >= 100), "Bob took the rod");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARodLevelsUpWithASentientKillAndShowsItToGhosts()
    {
        var (r, _, bob) = Room(4407);
        var rod = r.In(() =>
        {
            var made = Dm.@new<obj_effect_immovablerod>(locate(20, 22, 1), locate(20, 22, 1));
            made.sit_stay_good_rod();
            return made;
        });
        r.In(() => Waiting.NoSleep(rod.penetrate(bob)));
        Assert.Equal("immovable rod of sentient slaying +1", r.In(() => Dm.str(rod.name)));
        Assert.Equal(1, r.In(() => (int)rod.num_sentient_people_hit));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoRodsMeetingCollapseIntoASingularity()
    {
        var (r, feed, _) = Room(4408);
        r.In(() =>
        {
            var still = Dm.@new<obj_effect_immovablerod>(locate(20, 10, 1), locate(20, 10, 1));
            still.sit_stay_good_rod();
            Dm.@new<obj_effect_immovablerod>(locate(14, 10, 1), locate(Size - 2, 10, 1));
        });
        r.Step(5 * Second);
        var singularity = r.In(() => Dm.each<obj_singularity>(Dm.world).FirstOrDefault());
        Assert.NotNull(singularity);
        Assert.Empty(r.In(() => Dm.each<obj_effect_immovablerod>(Dm.world).ToList()));
        r.In(() => qdel(singularity));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRodEventAnnouncesWhatItWas()
    {
        var (r, feed, _) = Room(4409);
        var control = Control<datum_round_event_control_immovable_rod>(r);
        r.In(() => Waiting.Detach(control.run_event()));
        r.Step(14 * Second);
        Assert.True(feed.Any("What the fuck was that?!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDarkMatteorRaisesTheAlertUntilItMisses()
    {
        var (r, feed, _) = Room(4410);
        var matteor = r.In(() => Dm.@new<obj_effect_meteor_dark_matteor>(locate(Edge, Edge + 1, 1), locate(Size - Edge, Edge + 1, 1)));
        Assert.Equal(SEC_LEVEL_RED, r.In(() => (int)Dm.num(SSsecurity_level.get_current_level_as_number())));
        r.Step(2 * Second);
        Assert.NotEqual((float)Edge, r.In(() => matteor.x)); // it flies (and sparks)
        // it leaves the station's level past the far edge: the toy room has no level transition, so the test moves it off
        r.In(() => matteor.moved_off_z());
        Assert.True(r.In(() => QDELETED(matteor)));
        Assert.True(feed.Any("Wow. The Dark Matt-eor actually missed your station."));
        Assert.Equal(SEC_LEVEL_GREEN, r.In(() => (int)Dm.num(SSsecurity_level.get_current_level_as_number())));
        r.AssertNoRuntimes();
    }
}
