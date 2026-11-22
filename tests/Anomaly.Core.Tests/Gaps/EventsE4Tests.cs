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
/// Gaps package E4 (D327): the creatures and crew events in a booted toy room of iron floor on the station's level, with Bob as an
/// assistant. The carp swim out of their spawns; the mice migrate; a wise cow appears; Bob's heart stops and he catches a fake virus; the whole station hallucinates the same thing; a mindless pet is the first the sentience event wakes.
/// </summary>
public class EventsE4Tests
{
    private const int W = 25, H = 19;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed, mapExtras: _ =>
        {
            for (int y = 2; y < H; y++)
                for (int x = 2; x < W; x++)
                    Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
        });
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            // the station's area over the floors: change_area keeps the area's lists on the turfs the map extras made
            var station = (area)Dm.@new(typeof(area_station_hallway_primary_central));
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, W - 1, H - 1, 1)))
                t.change_area((area)t.loc, station);
            station.requires_power = FALSE;
            station.reg_in_areas_in_z(); // the map loader registers a map's areas on their levels (mapping.dm)
            GLOB.the_station_areas = Dm.list(station.type);
            bob.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job);
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Control<T>(AudioRoom r) where T : datum_round_event_control =>
        r.In(() => (T)Dm.each<datum_round_event_control>(SSevents.control).First(c => c.GetType() == typeof(T)));

    // the event as its control makes it (_event.dm:178 run_event: new typepath(TRUE, src)), but not processing: the test drives it
    private static T Event<T>(AudioRoom r) where T : datum_round_event => r.In(() =>
    {
        var control = Dm.each<datum_round_event_control>(SSevents.control).FirstOrDefault(c => c.typepath == typeof(T));
        return Dm.@new<T>(FALSE, control);
    });

    [Fact]
    public void TheE4EventsAreLiveAndInThePool()
    {
        foreach (Type type in new[]
        {
            typeof(datum_round_event_carp_migration), typeof(datum_round_event_control_carp_migration), typeof(datum_round_event_mice_migration),
            typeof(datum_round_event_wisdomcow), typeof(datum_round_event_heart_attack), typeof(datum_round_event_control_heart_attack),
            typeof(datum_round_event_brain_trauma), typeof(datum_round_event_mass_hallucination), typeof(datum_round_event_fake_virus),
            typeof(datum_status_effect_fake_virus), typeof(datum_round_event_ghost_role_sentience), typeof(datum_round_event_ghost_role_sentience_all),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        var (r, _, _) = Room(4601);
        var names = r.In(() => Dm.each<datum_round_event_control>(SSevents.control).Select(c => Dm.str(c.name)).ToList());
        foreach (string name in new[] { "Carp Migration", "Mice Migration", "Wisdom Cow", "Random Heart Attack", "Spontaneous Brain Trauma",
            "Mass Hallucination", "Fake Virus", "Random Human-level Intelligence", "Station-wide Human-level Intelligence" })
            Assert.Contains(name, names);
    }

    [Fact]
    public void TheCarpSwimOutOfTheirSpawns()
    {
        var (r, feed, _) = Room(4602);
        Place<obj_effect_landmark_carpspawn>(r, 3, 15);
        Place<obj_effect_landmark_carpspawn>(r, 20, 15);
        var carp = Event<datum_round_event_carp_migration>(r);
        r.In(() =>
        {
            Waiting.NoSleep(carp.announce__proc(FALSE));
            Waiting.NoSleep(carp.start());
        });
        Assert.True(feed.Any("Unknown biological entities have been detected near"));
        Assert.Equal(2, r.In(() => Dm.each<mob_living_basic_carp>(Dm.world).Count()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMiceAreAnnouncedAndAWiseCowAppears()
    {
        var (r, feed, _) = Room(4603);
        var mice = Event<datum_round_event_mice_migration>(r);
        r.In(() => Waiting.NoSleep(mice.announce__proc(FALSE)));
        Assert.True(feed.Any("Migration Alert") || feed.Any("into the"));
        var cow = Event<datum_round_event_wisdomcow>(r);
        r.In(() =>
        {
            Waiting.NoSleep(cow.announce__proc(FALSE));
            Waiting.NoSleep(cow.start());
        });
        Assert.True(feed.Any("A wise cow has been spotted in the area."));
        Assert.Single(r.In(() => Dm.each<mob_living_basic_cow_wisdom>(Dm.world).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BobsHeartStopsAndHeCatchesAFakeVirus()
    {
        var (r, _, bob) = Room(4604);
        var control = Control<datum_round_event_control_heart_attack>(r);
        r.In(() => control.generate_candidates());
        Assert.True(r.In(() => Dm.@in(bob, control.heart_attack_candidates)), "Bob is a candidate");
        var attack = Event<datum_round_event_heart_attack>(r);
        r.In(() => Waiting.NoSleep(attack.start()));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_heart_attack))));

        // the brain trauma event picks Bob (a living crewman with a client and a brain) and gives him a random trauma (gaps B1); it runs
        // detached, as a trauma that polls the ghosts (the split personality, the imaginary friend) waits for its poll, in the brain's
        // traumas meanwhile
        var trauma = Event<datum_round_event_brain_trauma>(r);
        r.In(() => Waiting.Detach(trauma.start()));
        r.Step(2);
        Assert.True(r.In(() => Dm.length(bob.get_traumas())) > 0, "the event's trauma");

        var virus = Event<datum_round_event_fake_virus>(r);
        r.In(() => Waiting.NoSleep(virus.start()));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_fake_virus))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStationHallucinatesTheSameThing()
    {
        var (r, _, bob) = Room(4605);
        var mass = Event<datum_round_event_mass_hallucination>(r);
        r.In(() =>
        {
            mass.admin_forced_hallucination = typeof(datum_hallucination_fire); // the flood needs a vent, which the toy room has none of
            Waiting.NoSleep(mass.start());
        });
        r.Step(2 * Second);
        Assert.True(r.In(() => Dm.each<datum_hallucination_fire>(GLOB.all_ongoing_hallucinations).Any(h => h.hallucinator == bob)), "Bob sees the fire: "
            + r.In(() => string.Join(", ", Dm.each<datum_hallucination>(GLOB.all_ongoing_hallucinations).Select(h => h.GetType().Name))) + "\n" + string.Join("\n", r.Room.RuntimeText));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePetsAreTheFirstTheSentienceEventWakes()
    {
        var (r, feed, _) = Room(4606);
        var corgi = Place<mob_living_basic_pet_dog_corgi>(r, 10, 10);
        var mouse = Place<mob_living_basic_mouse>(r, 12, 10);
        var sentience = Event<datum_round_event_ghost_role_sentience>(r);
        var (high, low) = r.In(() =>
        {
            DmList hi = Dm.list(), lo = Dm.list();
            sentience.set_mob_priority(corgi, hi, lo);
            sentience.set_mob_priority(mouse, hi, lo);
            return (hi, lo);
        });
        Assert.True(r.In(() => Dm.@in(corgi, high) && Dm.@in(mouse, low)), r.In(() => Dm.text("high [] low [] cache []", high.len, low.len, GLOB.high_priority_sentience?.len ?? -1)));
        r.In(() => Waiting.NoSleep(sentience.announce__proc(FALSE)));
        Assert.True(feed.Any("level intelligence, and the ability to communicate."));
        r.AssertNoRuntimes();
    }
}
