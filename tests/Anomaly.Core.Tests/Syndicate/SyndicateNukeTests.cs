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
/// Syndicate stage N1a (D316): the nuke. The self-destruct terminal takes the real disk (a fake one is refused), its code and a time on
/// its keypad, drops its safety, and armed counts down at delta alert with the station's circuit floors in its threat list; armed again it
/// is disarmed and the alert goes back. Left to run out on a station level it plays the self-destruct cinematic to everyone (their
/// screens, locked in place) and marks the station nuked. The disk goes back to the station when taken off it. The theft kit's
/// screwdriver, a crowbar and a welder open the terminal to its plutonium core, which the container takes and seals.
/// </summary>
public class SyndicateNukeTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, obj_machinery_nuclearbomb_selfdestruct Nuke) Room(ulong seed, int levels = 1)
    {
        var r = new AudioRoom(11, 9, seed: seed, levels: levels);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.In(() =>
        {
            // the toy room has no z-level traits: its first level is the station (is_station_level's cache), its area the station's
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            GLOB.the_station_areas = Dm.list(((area)get_area(bob)).type);
        });
        var nuke = r.In(() => Dm.@new<obj_machinery_nuclearbomb_selfdestruct>(locate(4, 4, 1)));
        r.Step(Second);
        return (r, feed, bob, nuke);
    }

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    // an act in the NuclearBomb window Bob has open (as his client sends it)
    private static void Press(AudioRoom r, mob who, string action, string digit = null)
    {
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == "NuclearBomb").window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, digit == null ? Dm.list() : Dm.list(Dm.kv("digit", digit))));
        r.Step(2);
    }

    private static void Type(AudioRoom r, mob who, string keys)
    {
        foreach (char c in keys)
            Press(r, who, "keypad", c.ToString());
    }

    [Fact]
    public void TheNukeAndItsPiecesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_machinery_nuclearbomb), typeof(obj_machinery_nuclearbomb_selfdestruct), typeof(obj_machinery_nuclearbomb_syndicate),
            typeof(obj_machinery_nuclearbomb_beer), typeof(obj_item_disk_nuclear), typeof(obj_item_disk_nuclear_fake), typeof(obj_item_nuke_core),
            typeof(obj_item_nuke_core_container), typeof(datum_component_stationloving), typeof(datum_cinematic_nuke_self_destruct),
            typeof(datum_cinematic_nuke_ops_victory), typeof(obj_effect_countdown_nuclearbomb), typeof(datum_objective_item_steal_nukedisc),
            typeof(datum_objective_item_steal_nuke_core),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheSelfDestructIsArmedWithTheDiskAndTheCodeAndDisarmed()
    {
        var (r, feed, bob, nuke) = Room(161);
        r.In(() => nuke.r_code = "12345");
        // a fake disk is refused
        var fake = Held<obj_item_disk_nuclear_fake>(r, bob);
        r.In(() => Waiting.NoSleep(nuke.item_interaction(bob, fake)));
        r.Step(2);
        Assert.True(feed.Any("Authentication failure; disk not recognised."));
        Assert.Null(r.In(() => nuke.auth));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(fake)));

        var disk = Held<obj_item_disk_nuclear>(r, bob);
        r.In(() => Waiting.NoSleep(nuke.item_interaction(bob, disk)));
        Assert.Same(disk, r.In(() => nuke.auth));
        Assert.Same(nuke, r.In(() => disk.loc));
        Assert.Equal((float)NUKEUI_AWAIT_CODE, r.In(() => nuke.ui_mode));

        r.In(() => Waiting.NoSleep(nuke.ui_interact(bob)));
        r.Step(2);
        // a wrong code, cleared, then the right one; a time of 120 seconds drops the safety
        Type(r, bob, "11111E");
        Assert.Equal("ERROR", r.In(() => nuke.numeric_input));
        Type(r, bob, "C12345E");
        Assert.True(r.In(() => Dm.truthy(nuke.yes_code)));
        Assert.Equal((float)NUKEUI_AWAIT_TIMER, r.In(() => nuke.ui_mode));
        Type(r, bob, "120E");
        Assert.Equal(120f, r.In(() => nuke.timer_set));
        Assert.False(r.In(() => Dm.truthy(nuke.safety)));
        Assert.Equal((float)NUKEUI_AWAIT_ARM, r.In(() => nuke.ui_mode));
        DmList data = r.In(() => Waiting.NoSleep(nuke.ui_data(bob)) as DmList);
        Assert.Equal("DEVICE READY", r.In(() => Dm.str(data["status1"])));
        Assert.Equal("TIME: 120", r.In(() => Dm.str(data["status2"])));
        Assert.Contains("lights-safety", r.In(() => Dm.each(nuke.overlays).Select(o => o is string s ? s : Dm.str((o as DmAppearance)?.icon_state)).ToList()));

        // armed: the countdown, delta alert, the self-destruct among the nuke threats
        Press(r, bob, "arm");
        Assert.True(r.In(() => Dm.truthy(nuke.timing)));
        Assert.Equal("nuclearbomb_timing", r.In(() => nuke.icon_state));
        Assert.Equal((float)SEC_LEVEL_DELTA, r.In(() => Dm.num(SSsecurity_level.get_current_level_as_number())));
        Assert.True(r.In(() => Dm.@in(nuke, SSmapping.nuke_threats)));
        Assert.True(r.In(() => Dm.truthy(nuke.countdown.started)));
        r.Step(5 * Second);
        Assert.InRange(r.In(() => Dm.num(nuke.get_time_left())), 113f, 116f);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/timer.ogg");

        // armed again: disarmed, the alert back to green
        Press(r, bob, "arm");
        Assert.False(r.In(() => Dm.truthy(nuke.timing)));
        Assert.Equal((float)SEC_LEVEL_GREEN, r.In(() => Dm.num(SSsecurity_level.get_current_level_as_number())));
        Assert.False(r.In(() => Dm.@in(nuke, SSmapping.nuke_threats)));

        // the disk out again
        Press(r, bob, "eject_disk");
        Assert.Null(r.In(() => nuke.auth));
        Assert.Same(r.In(() => get_turf(nuke)), r.In(() => disk.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSelfDestructRunningOutNukesTheStationWithItsCinematic()
    {
        var (r, feed, bob, nuke) = Room(162);
        r.In(() =>
        {
            nuke.r_code = "12345";
            var disk = Dm.@new<obj_item_disk_nuclear>(nuke);
            nuke.auth = disk;
            nuke.yes_code = TRUE;
            nuke.timer_set = 90;
            nuke.toggle_nuke_safety();
            nuke.toggle_nuke_armed();
        });
        Assert.True(r.In(() => Dm.truthy(nuke.timing)));
        r.Step(93 * Second); // SSmachines' two-second fire finds the timer run out
        Assert.True(r.In(() => Dm.truthy(nuke.exploding)));
        Assert.Equal("nuclearbomb_exploding", r.In(() => nuke.icon_state));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/announcer/alarm/nuke_alarm.ogg");

        // ten seconds on: the cinematic on Bob's screen, Bob held still
        r.Step(10 * Second + 2);
        var screen = r.In(() => Dm.each(bob.client.screen).OfType<atom_movable_screen_cinematic>().SingleOrDefault());
        Assert.NotNull(screen);
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_NO_TRANSFORM)));
        Assert.True(r.In(() => Dm.truthy(bob.screens?["cinematic"])));
        r.Step(4 * Second);
        Assert.Equal("summary_selfdes", r.In(() => screen.icon_state));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/explosion/explosion_distant.ogg");
        Assert.True(r.In(() => Dm.truthy(GLOB.station_was_nuked)));
        Assert.Equal((float)DETONATION_HIT_STATION, r.In(() => Dm.num(GLOB.station_nuke_source)));
        Assert.True(r.In(() => QDELETED(nuke)));

        // a ghost now (as the nuke's gibbed are): the ghost's login clears the screen, but the cinematic plays on (DV379)
        client bobsClient = r.In(() => bob.client);
        r.In(() => bob.ghostize(TRUE));
        r.Step(2);
        Assert.IsType<mob_dead_observer>(r.In(() => bobsClient.mob));
        Assert.Contains(r.In(() => Dm.each(bobsClient.screen).ToList()), o => o is atom_movable_screen_cinematic);

        // thirty seconds on the summary, then the screen goes and Bob can move again
        r.Step(31 * Second);
        Assert.DoesNotContain(r.In(() => Dm.each(bobsClient.screen).ToList()), o => o is atom_movable_screen_cinematic);
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_NO_TRANSFORM)));
        r.In(() => GLOB.station_was_nuked = FALSE);
    }

    [Fact]
    public void TheDiskGoesBackToTheStationWhenTakenOffIt()
    {
        var (r, feed, bob, _) = Room(163, levels: 2);
        var disk = Held<obj_item_disk_nuclear>(r, bob);
        Assert.NotNull(r.In(() => disk.GetComponent(typeof(datum_component_stationloving))));
        Assert.True(r.In(() => Dm.@in(disk, SSpoints_of_interest.real_nuclear_disks)));
        // Bob carries it to the second level, which is no station
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(5, 5, 2))));
        r.Step(2);
        Assert.True(feed.Any("You can't help but feel that you just lost something back there..."));
        Assert.Equal(1f, r.In(() => (float)((turf)get_turf(disk)).z));
        Assert.False(r.In(() => Dm.truthy(bob.is_holding(disk))));
        // and it cannot be destroyed: it turns up on the station again
        r.In(() => qdel(disk));
        r.Step(2);
        Assert.False(r.In(() => QDELETED(disk)));
        Assert.NotNull(r.In(() => get_turf(disk)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTheftKitOpensTheTerminalToItsCore()
    {
        var (r, feed, bob, nuke) = Room(164);
        var kit = Held<obj_item_storage_box_syndie_kit_nuke>(r, bob);
        var contents = r.In(() => Dm.each(kit.contents).ToList());
        Assert.Contains(contents, o => o is obj_item_screwdriver_nuke);
        Assert.Contains(contents, o => o is obj_item_nuke_core_container);
        Assert.Contains(contents, o => o is obj_item_paper_guides_antag_nuke_instructions);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(kit)));

        // an ordinary screwdriver does nothing; the kit's takes the panel's screws out
        var plain = Held<obj_item_screwdriver>(r, bob);
        Assert.Equal((object)ITEM_INTERACT_SKIP_TO_ATTACK, r.In(() => Waiting.NoSleep(nuke.screwdriver_act(bob, plain))));
        r.In(() => qdel(plain));
        var driver = Held<obj_item_screwdriver_nuke>(r, bob);
        r.In(() => Waiting.Detach(nuke.screwdriver_act(bob, driver)));
        r.Step(4 * Second);
        Assert.Equal((float)NUKESTATE_UNSCREWED, r.In(() => nuke.deconstruction_state));
        r.In(() => qdel(driver));
        var crowbar = Held<obj_item_crowbar>(r, bob);
        r.In(() => Waiting.Detach(nuke.crowbar_act(bob, crowbar)));
        r.Step(4 * Second);
        Assert.Equal((float)NUKESTATE_PANEL_REMOVED, r.In(() => nuke.deconstruction_state));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(crowbar)));
        var welder = Held<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        r.In(() => Waiting.Detach(nuke.welder_act(bob, welder)));
        r.Step(9 * Second);
        Assert.Equal((float)NUKESTATE_WELDED, r.In(() => nuke.deconstruction_state));
        r.In(() => qdel(welder));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(crowbar)));
        r.In(() => Waiting.Detach(nuke.crowbar_act(bob, crowbar)));
        r.Step(4 * Second);
        Assert.Equal((float)NUKESTATE_CORE_EXPOSED, r.In(() => nuke.deconstruction_state));
        Assert.True(feed.Any("You can see the core's green glow!"));
        Assert.Contains("plate-removed", r.In(() => Dm.each(nuke.overlays).Select(o => o is string s ? s : Dm.str((o as DmAppearance)?.icon_state)).ToList()));
        r.In(() => qdel(crowbar));

        // the container takes the core and seals in five seconds
        var core = r.In(() => nuke.core);
        var container = Held<obj_item_nuke_core_container>(r, bob);
        r.In(() => Waiting.Detach(nuke.item_interaction(bob, container)));
        r.Step(6 * Second);
        Assert.Equal((float)NUKESTATE_CORE_REMOVED, r.In(() => nuke.deconstruction_state));
        Assert.Same(core, r.In(() => container.core));
        Assert.Equal("core_container_loaded", r.In(() => container.icon_state));
        r.Step(6 * Second);
        Assert.Equal("core_container_sealed", r.In(() => container.icon_state));
        Assert.True(feed.Any("radiation is contained"));
        r.AssertNoRuntimes();
    }
}
