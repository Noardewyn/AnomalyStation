using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4bAudio;

/// <summary>
/// D296: tg's area ambience (controllers/subsystem/ambience.dm). SSambience plays a listening client one file of its area's
/// ambience list on CHANNEL_AMBIENCE at volume 27, the first 10 s after the client came (update_ambience_pref), then each one
/// its length plus 4 to 10 s after the last; the area's buzz loops on CHANNEL_BUZZ while the area's APC powers its environment
/// and stops when it does not; a dark maintenance area sometimes plays a cave noise instead.
/// </summary>
public class AmbienceTests
{
    private const int Second = 20;
    private const float ChannelAmbience = 1018, ChannelBuzz = 1017;
    private const string ShipBuzz = "sound/ambience/general/shipambience.ogg";

    private static List<HeardSound> On(AudioRoom room, mob who, float channel)
        => room.Sounds.Heard.Where(h => ReferenceEquals(h.Target, who) && h.Channel == channel).ToList();

    [Fact]
    public void AJoinedPlayerHearsItsAreasAmbienceTenSecondsInAndThenAfterEachFilesLengthAndACooldown()
    {
        var room = new AudioRoom(10, 8, seed: 29601);
        mob_living_carbon_human listener = room.Join(4, 4, "Listener");
        long joined = room.World.Tick;
        Assert.True(room.In(() => SSambience.ambience_listening_clients.Contains(listener.client)));
        HashSet<string> files = room.In(() => Dm.each(((area)get_area(listener)).ambientsounds).Select(f => Dm.str(f)).ToHashSet());
        Assert.NotEmpty(files);

        room.Step(60 * Second);
        List<HeardSound> plays = On(room, listener, ChannelAmbience).Where(h => h.File != null).ToList();
        Assert.True(plays.Count >= 2, room.Sounds.Dump());
        Assert.All(plays, p => Assert.Contains(p.File, files));
        Assert.All(plays, p => Assert.Equal(27f, p.Volume)); // play_ambience's volume, the preference at its default 100
        Assert.All(plays, p => Assert.Equal(0f, p.Repeat));
        // update_ambience_pref: world.time + 10 SECONDS, then SSambience's next fire (wait 1 s)
        Assert.InRange(plays[0].Tick - joined, 10 * Second, 12 * Second);
        for (int i = 1; i < plays.Count; i++)
        {
            float length = TestSoundLengths.Lengths[plays[i - 1].File]; // deciseconds
            long gap = plays[i].Tick - plays[i - 1].Tick;
            Assert.InRange(gap, (long)(length * 2) + 4 * Second, (long)(length * 2) + 12 * Second);
        }
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheShipBuzzLoopsWhileTheApcPowersTheEnvironmentAndStopsWhenItDoesNot()
    {
        obj_machinery_power_apc apc = null;
        var room = new AudioRoom(10, 8, seed: 29602, mapExtras: w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(3, 2, 1));
        });
        room.Step(Second);
        mob_living_carbon_human listener = room.Join(4, 4, "Listener");
        room.Step(Second);

        List<HeardSound> buzz = On(room, listener, ChannelBuzz);
        HeardSound loop = Assert.Single(buzz, h => h.File != null);
        Assert.Equal(ShipBuzz, loop.File); // /area's ambient_buzz (areas.dm:100)
        Assert.Equal(1f, loop.Repeat);
        Assert.Equal(35f, loop.Volume); // ambient_buzz_vol x the ship ambience preference at 100

        // the environment channel off: the area's power change stops the buzz (refresh_looping_ambience, ambience.dm:136)
        room.Sounds.Heard.Clear();
        room.In(() => { apc.environ = APC_CHANNEL_OFF; apc.update(); });
        room.Step(Second);
        HeardSound stop = Assert.Single(On(room, listener, ChannelBuzz));
        Assert.Null(stop.File);

        // and back on: the loop starts again, once
        room.Sounds.Heard.Clear();
        room.In(() => { apc.environ = APC_CHANNEL_ON; apc.update(); });
        room.Step(Second);
        Assert.Equal(ShipBuzz, Assert.Single(On(room, listener, ChannelBuzz)).File);

        // moving inside the area keeps the loop (it does not restart a playing buzz)
        room.Sounds.Heard.Clear();
        room.In(() => Waiting.NoSleep(listener.forceMove(locate(6, 4, 1))));
        room.Step(Second);
        Assert.Empty(On(room, listener, ChannelBuzz));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnpoweredAreaHasNoBuzz()
    {
        // the test room has no APC: tg's station ambience needs one (ambience.dm:136)
        var room = new AudioRoom(10, 8, seed: 29603);
        mob_living_carbon_human listener = room.Join(4, 4, "Listener");
        room.Step(Second);
        Assert.DoesNotContain(On(room, listener, ChannelBuzz), h => h.File != null);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADarkMaintenanceSometimesPlaysACaveNoiseInsteadOfItsAmbience()
    {
        var room = new AudioRoom(10, 8, seed: 29604, mapExtras: w =>
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).static_lighting = TRUE); // no lamp: every tile is dark
        mob_living_carbon_human listener = room.Join(4, 4, "Listener");
        room.Step(Second);
        Assert.False(room.In(() => Dm.truthy(listener.has_light_nearby())));

        HashSet<string> cave = room.In(() => Dm.each(area_station_maintenance.minecraft_cave_noises).Select(f => Dm.str(f)).ToHashSet());
        HashSet<string> maint = room.In(() => Dm.each(GLOB.maint_ambience).Select(f => Dm.str(f)).ToHashSet());
        room.Sounds.Heard.Clear();
        room.In(() =>
        {
            area_station_maintenance maintenance = Dm.@new<area_station_maintenance>();
            for (int i = 0; i < 2000; i++)
                maintenance.play_ambience(listener);
        });
        List<string> played = On(room, listener, ChannelAmbience).Select(h => h.File).ToList();
        Assert.Equal(2000, played.Count);
        Assert.All(played, f => Assert.True(cave.Contains(f) || maint.Contains(f), f));
        int noises = played.Count(cave.Contains);
        Assert.InRange(noises, 1, 40); // prob(0.5): about 10 of 2000
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AForcedAreasTrackWaitsForThePlayingOnesEnd()
    {
        // DV229 (the owner, 2026-10-30): tg's forced ambience (a maintenance area entered) starts a track at once with prob(5) a
        // second, over the playing one; here it waits for that one's end
        var room = new AudioRoom(10, 8, seed: 29605);
        mob_living_carbon_human listener = room.Join(4, 4, "Listener");
        room.In(() => ((area)get_area(listener)).forced_ambience = TRUE);
        room.Step(13 * Second);
        HeardSound first = Assert.Single(On(room, listener, ChannelAmbience), h => h.File != null);
        long end = first.Tick + (long)(TestSoundLengths.Lengths[first.File] * 2);
        // as if the listener just came from another area, every tick until the track ends
        while (room.World.Tick < end - 1)
        {
            room.In(() => SSambience.client_old_areas[listener.client] = null);
            room.Step();
        }
        Assert.Single(On(room, listener, ChannelAmbience), h => h.File != null);
        // after its end the forced area plays again before tg's cooldown would (prob 5 a second)
        for (int i = 0; i < 200 && On(room, listener, ChannelAmbience).Count(h => h.File != null) < 2; i++)
        {
            room.In(() => SSambience.client_old_areas[listener.client] = null);
            room.Step(Second / 2);
        }
        Assert.Equal(2, On(room, listener, ChannelAmbience).Count(h => h.File != null));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AKnockedDownHumanFallsWithABodyfallAndDropsWhatHeHolds()
    {
        // D296's sound audit: tg's carbon on_fall and turf handle_fall (carbon.dm:148, turf.dm:661), reached when a human is floored
        var room = new AudioRoom(10, 8, seed: 29606);
        mob_living_carbon_human listener = room.Join(3, 4, "Listener");
        mob_living_carbon_human victim = room.Dummy(5, 4);
        obj_item_wrench wrench = room.In(() =>
        {
            var w = Dm.@new<obj_item_wrench>(locate(5, 4, 1));
            Waiting.NoSleep(victim.put_in_hands(w));
            return w;
        });
        Assert.Same(victim, room.In(() => wrench.loc));
        room.Sounds.Heard.Clear();
        room.In(() => Waiting.NoSleep(victim.Paralyze(2 * Second * 5)));
        room.Step(2);
        Assert.Equal(LYING_DOWN, room.In(() => Dm.num(victim.body_position)));
        Assert.Same(room.In(() => locate(5, 4, 1)), room.In(() => wrench.loc));
        Assert.Contains(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener) && h.File?.Contains("bodyfall") == true);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheCombatModeToggleTheCoatZipAndALandingLimbSoundAsInTg()
    {
        // D296's sound audit: set_combat_mode's toggle sound (living_defense.dm:257), the winter coat's zip (wintercoats.dm:54) and
        // a thrown limb's splort (_bodyparts.dm:624)
        var room = new AudioRoom(10, 8, seed: 29607);
        mob_living_carbon_human player = room.Join(4, 4, "Player");
        room.Sounds.Heard.Clear();
        room.In(() => player.set_combat_mode(TRUE, silent: FALSE));
        room.In(() => player.set_combat_mode(FALSE, silent: FALSE));
        Assert.Equal(new[] { "sound/misc/ui_togglecombat.ogg", "sound/misc/ui_toggleoffcombat.ogg" },
            room.Sounds.Heard.Where(h => ReferenceEquals(h.Target, player) && h.File?.StartsWith("sound/misc/ui_toggle", StringComparison.Ordinal) == true).Select(h => h.File));

        obj_item_clothing_suit_hooded_wintercoat coat = room.In(() => Dm.@new<obj_item_clothing_suit_hooded_wintercoat>(locate(4, 4, 1)));
        Assert.Equal(CLICK_ACTION_SUCCESS, room.In(() => Dm.num(Waiting.NoSleep(coat.click_alt(player)))));
        Assert.True(room.In(() => Dm.truthy(coat.zipped)));
        Assert.EndsWith("_t", room.In(() => coat.worn_icon_state as string));
        Assert.Contains(room.Sounds.Heard, h => h.File == "sound/items/zip/zip_up.ogg");

        obj_item_bodypart arm = room.In(() => Dm.@new<obj_item_bodypart_arm_left>(locate(6, 4, 1)));
        room.In(() => arm.throw_impact(locate(6, 4, 1)));
        Assert.Contains(room.Sounds.Heard, h => h.File == "sound/misc/splort.ogg");
        room.AssertNoRuntimes();
    }
}
