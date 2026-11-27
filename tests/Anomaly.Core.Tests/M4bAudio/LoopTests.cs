using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4bAudio;

/// <summary>
/// M4b audio slice 5: tg's looping sounds (datums/looping_sounds/_looping_sound.dm) on SSsound_loops, the sound tokens
/// (datums/sound_token.dm) updated by SSsound_tokens, and the loops of the live machines: the fire alarm's siren
/// (machinery_sounds.dm:167, a token loop that plays a new file every 2.4 s), the firelock's siren, the plasma fire's crackle
/// (/datum/hot_group/update_sound, LINDA_fire.dm:448, a plain playsound loop from the group's centre) and the overclocked
/// vent's fan (vents.dm:1, a start sound, then a natively repeating loop, then an end sound on a one-shot token).
/// </summary>
public class LoopTests
{
    private const int Second = 20;
    private const string AlarmFiles = "sound/machines/fire_alarm/fire_alarm";

    private static obj_machinery_firealarm MapAlarm(AudioRoom room, int x, int y) => room.In(() =>
    {
        // mapload: a built alarm is an empty frame with its panel open (firealarm.dm:62), as Meta's are not
        SSatoms.set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, "test map");
        var a = Dm.@new<obj_machinery_firealarm>(locate(x, y, 1));
        SSatoms.clear_tracked_initalize("test map");
        return a;
    });

    private static List<HeardSound> Sirens(AudioRoom room, mob who)
        => room.Sounds.Heard.Where(h => ReferenceEquals(h.Target, who) && h.File != null && h.File.StartsWith(AlarmFiles, StringComparison.Ordinal)).ToList();

    private static void MoveTo(AudioRoom room, mob who, int x, int y)
        => room.In(() => Waiting.NoSleep(who.forceMove(locate(x, y, 1))));

    [Fact]
    public void AFireAlarmPlaysANewSirenFileEveryTwoPointFourSecondsOnOneChannel()
    {
        var room = new AudioRoom(30, 8, seed: 5601);
        mob_living_carbon_human listener = room.Join(8, 4, "Listener");
        obj_machinery_firealarm alarm = MapAlarm(room, 3, 4);
        room.Step(Second);
        room.Sounds.Heard.Clear();

        long started = room.World.Tick;
        room.In(() => alarm.alarm());
        Assert.True(room.In(() => Dm.truthy(alarm.soundloop.is_active())));
        datum_sound_token token = room.In(() => alarm.soundloop.sound_token_instance);
        Assert.NotNull(token);
        float channel = room.In(() => Dm.num(token.sound_channel));
        Assert.Equal(1010f, channel); // reserve_sound_channel_for_datum: the top free channel (sounds.dm:146), under the hear-something dream's 1011

        room.Step(10 * Second);
        List<HeardSound> plays = Sirens(room, listener);
        // the loop plays at once (start_sound_loop: no start sound) and then every mid_length = 2.4 SECONDS on SSsound_loops
        // (client time, TIMER_LOOP): 0, 2.4, 4.8, 7.2, 9.6 s
        Assert.Equal(5, plays.Count);
        Assert.Equal(started, plays[0].Tick);
        for (int i = 1; i < plays.Count; i++)
            Assert.InRange(plays[i].Tick - plays[i - 1].Tick, 47, 49); // 24 ds of REALTIMEOFDAY (whole deciseconds, DV3), 48 ticks of 0.5 ds
        Assert.InRange(plays[^1].Tick - plays[0].Tick, 4 * 48 - 2, 4 * 48 + 2);
        Assert.All(plays, p => Assert.Equal(channel, p.Channel));
        Assert.All(plays, p => Assert.Equal(0f, p.Status)); // a new file plays (update_sound -> force_update_all_listeners(FALSE))
        Assert.All(plays, p => Assert.Equal(0f, p.Offset)); // the file just started
        Assert.All(plays, p => Assert.Equal(13.890, p.Volume, 3)); // vol 30, 5 tiles of 15, SOUND_FALLOFF_EXPONENT, min 5
        Assert.All(plays, p => Assert.Equal(-5, p.X));
        Assert.True(plays.Select(p => p.File).Distinct().Count() > 1, "the siren picks among its four files");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AListenerLeavingTheRangeIsMutedAndUpdatedWhenBack()
    {
        var room = new AudioRoom(30, 8, seed: 5602);
        mob_living_carbon_human listener = room.Join(8, 4, "Listener");
        obj_machinery_firealarm alarm = MapAlarm(room, 3, 4);
        room.Step(Second);
        room.In(() => alarm.alarm());
        float channel = room.In(() => Dm.num(alarm.soundloop.sound_token_instance.sound_channel));
        room.Step(Second);
        room.Sounds.Heard.Clear();

        // 22 tiles away (range 15): SSsound_tokens updates the moved client (mob_movement.dm:591) and the token mutes it
        MoveTo(room, listener, 25, 4);
        room.Step(2);
        HeardSound mute = Assert.Single(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener));
        Assert.Equal(channel, mute.Channel);
        Assert.Equal(SOUND_MUTE + SOUND_UPDATE, mute.Status);
        Assert.True(room.In(() => Dm.truthy(alarm.soundloop.sound_token_instance.listeners[listener])), "still a listener (its cell is tracked)");

        // the new files are not sent to a muted listener (update_listener returns early)
        room.Sounds.Heard.Clear();
        room.Step(5 * Second);
        Assert.DoesNotContain(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener));

        // back in range: an update of the playing sound, unmuted, at the new distance
        MoveTo(room, listener, 8, 4);
        room.Step(2);
        HeardSound back = Assert.Single(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener));
        Assert.Equal(channel, back.Channel);
        Assert.Equal(SOUND_UPDATE, back.Status);
        Assert.Equal(13.890, back.Volume, 3);
        Assert.StartsWith(AlarmFiles, back.File);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ResettingTheAlarmSendsANullSoundOnItsChannel()
    {
        var room = new AudioRoom(20, 8, seed: 5603);
        mob_living_carbon_human listener = room.Join(8, 4, "Listener");
        obj_machinery_firealarm alarm = MapAlarm(room, 3, 4);
        room.Step(Second);
        room.In(() => alarm.alarm());
        datum_sound_token token = room.In(() => alarm.soundloop.sound_token_instance);
        float channel = room.In(() => Dm.num(token.sound_channel));
        room.Step(Second);
        room.Sounds.Heard.Clear();

        room.In(() => alarm.reset());
        HeardSound stop = Assert.Single(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener));
        Assert.Null(stop.File); // the token's null_sound (sound_token.dm:63, :130)
        Assert.Equal(channel, stop.Channel);
        room.In(() =>
        {
            Assert.False(Dm.truthy(alarm.soundloop.is_active()));
            Assert.Null(alarm.soundloop.sound_token_instance);
            Assert.True(QDELETED(token));
            Assert.Null(listener.sound_tokens);
            Assert.Null(SSsounds.using_channels[Dm.text("[]", channel)]); // freed with the token (sounds.dm:268)
        });
        room.Sounds.Heard.Clear();
        room.Step(5 * Second);
        Assert.Empty(Sirens(room, listener));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void APlayerLoggingInMidLoopGetsAnOffset()
    {
        var room = new AudioRoom(20, 8, seed: 5604);
        obj_machinery_firealarm alarm = MapAlarm(room, 3, 4);
        room.Step(Second);
        room.In(() => alarm.alarm());
        room.Step(6 * Second); // 6 s: the third file started at 4.8 s
        float started = room.In(() => Dm.num(alarm.soundloop.sound_token_instance.start_time));
        mob_living_carbon_human late = room.Join(8, 4, "Late");
        float now = room.In(() => REALTIMEOFDAY());

        HeardSound first = Sirens(room, late).First();
        Assert.Equal(0f, first.Status);
        Assert.True(first.Offset > 0, "a late listener starts the file where it is");
        Assert.Equal(now - started, first.Offset); // calculate_offset: REALTIMEOFDAY - start_time, frequency and pitch factors 1
        Assert.InRange(first.Offset, 10, 15);
        Assert.True(room.In(() => alarm.soundloop.sound_token_instance.sound.offset) == 0, "the master sound's offset is cleared after the send");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ACoveredFirelockSoundsTheSirenUntilTheAirIsWarmAgain()
    {
        var room = new AudioRoom(16, 8, seed: 5605);
        // a wall at x = 8 with a doorway at (8, 4) holding a firelock; the listener on the west side
        room.In(() =>
        {
            for (int y = 2; y < 8; y++)
                if (y != 4)
                    Waiting.NoSleep(locate(8, y, 1).ChangeTurf(typeof(turf_closed_indestructible)));
        });
        var door = room.In(() => Dm.@new<obj_machinery_door_firedoor>(locate(8, 4, 1)));
        mob_living_carbon_human listener = room.Join(5, 4, "Listener");
        room.Step(Second);
        room.Sounds.Heard.Clear();

        // a cold block east of the doorway (80 K, Tg/M4b/FirelockTests.cs): the firelock's atmos check starts its activation
        SetTemperature(room, 9, 11, 80);
        room.Step(3 * Second);
        Assert.True(room.In(() => Dm.truthy(door.density)), "the cold air closed the firelock");
        Assert.True(room.In(() => Dm.truthy(door.is_playing_alarm)));
        datum_sound_token token = room.In(() => door.soundloop.sound_token_instance);
        Assert.NotNull(token);
        HeardSound siren = Sirens(room, listener).First();
        Assert.Equal(3, siren.X); // from the firelock's tile
        Assert.Equal(room.In(() => Dm.num(token.sound_channel)), siren.Channel);

        // warm air again: start_deactivation_process stops the siren (firedoor.dm:346)
        room.Sounds.Heard.Clear();
        SetTemperature(room, 9, 15, T20C);
        room.Step(10 * Second);
        Assert.False(room.In(() => Dm.truthy(door.density)), "warm air opens the firelock again");
        Assert.Contains(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener) && h.File == null && h.Channel == siren.Channel);
        Assert.False(room.In(() => Dm.truthy(door.soundloop.is_active())));
        Assert.DoesNotContain(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener) && h.File != null && h.File.StartsWith(AlarmFiles, StringComparison.Ordinal) && h.Tick > room.World.Tick - 5 * Second);
        room.AssertNoRuntimes();
    }

    private static void SetTemperature(AudioRoom room, int fromX, int toX, float kelvin) => room.In(() =>
    {
        for (int x = fromX; x <= toX; x++)
            for (int y = 2; y < 7; y++)
                if (locate(x, y, 1) is turf_open t)
                {
                    t.air.temperature = kelvin;
                    SSair.add_to_active(t);
                }
    });

    [Fact]
    public void TheOverclockedVentPlaysItsStartLoopAndStopSounds()
    {
        var room = new AudioRoom(16, 8, seed: 5606);
        var vent = room.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_vent_pump>(locate(5, 4, 1)));
        mob_living_carbon_human listener = room.Join(8, 4, "Listener");
        room.Step(Second);
        room.Sounds.Heard.Clear();

        long started = room.World.Tick;
        room.In(() => vent.toggle_overclock());
        Assert.True(room.In(() => Dm.truthy(vent.fan_overclocked)));
        HeardSound start = Assert.Single(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener));
        Assert.Equal("sound/machines/fan/fan_start.ogg", start.File);
        Assert.Equal(0f, start.Repeat);
        Assert.Equal(-3, start.X);

        // start_length 1.5 SECONDS later the mid sound starts on the same token, repeating natively (can_native_repeat:
        // a token loop of one file)
        room.Step(2 * Second);
        HeardSound loop = Assert.Single(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener) && h.File == "sound/machines/fan/fan_loop.ogg");
        Assert.Equal(1f, loop.Repeat);
        Assert.Equal(start.Channel, loop.Channel);
        Assert.InRange(loop.Tick - started, 29, 31); // 15 ds of client time (whole deciseconds, DV3)
        Assert.True(room.In(() => Dm.truthy(vent.sound_loop.native_repeat_active)));
        room.Step(5 * Second);
        Assert.Single(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener) && h.File == "sound/machines/fan/fan_loop.ogg"); // no timer re-plays it

        room.Sounds.Heard.Clear();
        room.In(() => vent.toggle_overclock());
        List<HeardSound> heard = room.Sounds.Heard.Where(h => ReferenceEquals(h.Target, listener)).ToList();
        Assert.Equal(2, heard.Count);
        Assert.Null(heard[0].File); // stop_current: the loop's token is deleted, a null sound on its channel
        Assert.Equal(start.Channel, heard[0].Channel);
        Assert.Equal("sound/machines/fan/fan_stop.ogg", heard[1].File); // on_stop: the end sound on a one-shot token
        // stop_current deletes the loop's token before on_stop (_looping_sound.dm:128, :133), so the freed top channel is the
        // one the new token reserves (sounds.dm:146)
        Assert.Equal(start.Channel, heard[1].Channel);
        Assert.Equal(0f, heard[1].Repeat);

        // the one-shot token deletes itself when the file ends (24.56 ds), stopping its channel
        room.Sounds.Heard.Clear();
        room.Step(3 * Second);
        HeardSound end = Assert.Single(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener) && h.Channel is not (1017 or 1018)); // D296: not the area's ambience
        Assert.Null(end.File);
        Assert.Equal(heard[1].Channel, end.Channel);
        Assert.Null(room.In(() => listener.sound_tokens));
        room.AssertNoRuntimes();
    }

    // 20 moles of plasma per tile in normal air (Tg/M4b/FireTests.cs)
    private const string PlasmaAir = "o2=22;n2=82;plasma=20;TEMP=293.15";

    [Fact]
    public void APlasmaFireCracklesFromItsCentreMovingAtMostEveryFiveSecondsUntilItIsOut()
    {
        var room = new AudioRoom(16, 16, seed: 5607);
        mob_living_carbon_human listener = room.Join(3, 3, "Listener");
        room.Step(Second);
        room.In(() =>
        {
            for (int x = 6; x <= 10; x++)
                for (int y = 6; y <= 10; y++)
                {
                    var t = (turf_open)locate(x, y, 1);
                    t.air = SSair.parse_gas_string(PlasmaAir) as datum_gas_mixture;
                    SSair.add_to_active(t);
                }
        });
        room.Sounds.Heard.Clear();
        room.In(() => DevCommands.Ignite(locate(8, 8, 1)));

        // per group: its sound's parent over time
        var moves = new Dictionary<datum_hot_group, List<(long Tick, atom Parent)>>();
        var parents = new Dictionary<long, HashSet<atom>>();
        bool burned = false;
        long outTick = -1;
        for (int i = 0; i < 120 * Second && outTick < 0; i++)
        {
            room.Step();
            long tick = room.World.Tick;
            room.In(() =>
            {
                var now = new HashSet<atom>();
                foreach (obj_effect_hotspot spot in Dm.each_cast<obj_effect_hotspot>(SSair.hotspots))
                {
                    datum_hot_group group = spot.our_hot_group;
                    if (group?.sound == null || QDELETED(group))
                        continue;
                    now.Add(group.sound.parent);
                    if (!moves.TryGetValue(group, out var list))
                    {
                        // the sound starts once the group has more than MIN_SIZE_SOUND (2) hotspots, at the centre of its tiles
                        Assert.True(Dm.num(length(group.spot_list)) > 2, "the crackle started for a group of " + length(group.spot_list));
                        Assert.Same(locate(Dm.num(group.average_x), Dm.num(group.average_y), Dm.num(group.average_Z)), group.sound.parent);
                        Assert.Same(group.current_sound_loc, group.sound.parent);
                        moves[group] = list = new List<(long, atom)>();
                    }
                    if (list.Count == 0 || !ReferenceEquals(list[^1].Parent, group.sound.parent))
                        list.Add((tick, group.sound.parent));
                }
                parents[tick] = now;
                burned |= SSair.hotspots.len > 0;
                if (burned && SSair.hotspots.len == 0)
                    outTick = tick;
            });
        }
        Assert.True(outTick > 0, "the fire burned out");
        Assert.NotEmpty(moves);

        // the group re-centres its sound at most every 5 SECONDS (COOLDOWN update_sound_center)
        Assert.True(moves.Values.Any(l => l.Count > 1), "the crackle moved as the fire spread: " + string.Join("; ", moves.Values.Select(l => string.Join(",", l.Select(m => m.Tick)))));
        foreach (var list in moves.Values)
            for (int i = 1; i < list.Count; i++)
                Assert.True(list[i].Tick - list[i - 1].Tick >= 5 * Second, $"the crackle moved after {list[i].Tick - list[i - 1].Tick} ticks");

        // every crackle the listener heard came from a group's sound turf (that tick's or the tick before's)
        List<HeardSound> crackles = room.Sounds.Heard.Where(h => ReferenceEquals(h.Target, listener) && h.File != null && h.File.StartsWith("sound/effects/fireclip", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(crackles);
        foreach (HeardSound c in crackles)
        {
            atom source = room.In(() => locate(3 + c.X, 3 + c.Z, 1));
            bool known = (parents.TryGetValue(c.Tick, out var a) && a.Contains(source)) || (parents.TryGetValue(c.Tick - 1, out var b) && b.Contains(source));
            Assert.True(known, $"a crackle at tick {c.Tick} from ({3 + c.X}, {3 + c.Z})");
        }

        // out: every group is gone with its sound, and no crackle follows
        room.In(() =>
        {
            foreach (datum_hot_group group in moves.Keys)
            {
                Assert.True(QDELETED(group));
                Assert.True(group.sound == null || QDELETED(group.sound));
            }
        });
        room.Step(5 * Second);
        Assert.DoesNotContain(room.Sounds.Heard, h => ReferenceEquals(h.Target, listener) && h.File != null && h.File.StartsWith("sound/effects/fireclip", StringComparison.Ordinal) && h.Tick > outTick);
        room.AssertNoRuntimes();
    }
}
