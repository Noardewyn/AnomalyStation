using System.IO;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Integration;
using Anomaly.Server.Tests.Server;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.M4b;

/// <summary>
/// M4b audio slice 2, the server's half of tg's sounds (protocol version 13, docs/ai/specs/protocol.md section 22): through the real
/// server, an airlock opened near one player and far from another reaches only the near one, with tg's volume; a join gets the
/// sounds of its tick after its MapGrid; the budget drops the quietest plays and keeps the stops; the occlusion from the
/// station graph (an open line, a corner, a wall, a closed and an opened door) and the muffle in a vacuum; the lengths from the
/// sound tool's index.
/// </summary>
public class AudioServerTests
{
    private const string Airlock = "sound/machines/airlock/airlock.ogg";

    // A long low room with nothing but its rim (classic, under the featured size): x 2..25, y 2..8 are floor.
    private static ServerConfig Room() => new()
    {
        Seed = 7, RoomWidth = 26, RoomHeight = 9, RoomLayout = TestRoomLayout.Classic, RoomSpace = 0, Console = false, StatusEveryTicks = 1000,
    };

    private static IntegrationRig Start(out RawClient[] clients, params string[] names)
    {
        var rig = new IntegrationRig(Room());
        RawClient[] all = clients = names.Select(n => new RawClient(rig, n)).ToArray();
        // a connection that sends nothing would time out on the loopback: each client sends an idle frame every tick
        rig.AfterTick = () => { foreach (RawClient c in all) if (c.Welcome != null) c.Input(HeldDirs.None); };
        for (int i = 0; i < 20 && all.Any(c => c.Welcome == null); i++) rig.Tick();
        Assert.All(all, c => Assert.True(c.Welcome != null, "no Welcome: " + c.Gone + "\n" + rig.Runtimes));
        rig.Tick(2);
        return rig;
    }

    private static mob Place(IntegrationRig rig, string key, int x, int y)
    {
        mob m = rig.MobOf(key);
        using (rig.World.Enter())
            Waiting.NoSleep(m.forceMove(locate(x, y, 1)));
        return m;
    }

    private static List<SoundEvent> Heard(RawClient client)
    {
        var heard = new List<SoundEvent>();
        client.Replica.SoundsReceived += events => heard.AddRange(events);
        return heard;
    }

    private static List<Sounds> SoundsOf(RawClient client) => client.Received
        .Where(r => r.Message[0] == (byte)MessageId.Sounds)
        .Select(r =>
        {
            Assert.Equal(Channel.State, r.Channel);
            Assert.True(Codec.TryDecode(r.Message, out Sounds s, out ProtocolError error), error.ToString());
            return s;
        }).ToList();

    private static bool Holds(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    [Fact]
    public void AnAirlockFiveTilesFromAIsHeardByAWithTgsVolumeAndNeverReachesBTwentyTilesAway()
    {
        using IntegrationRig rig = Start(out RawClient[] c, "Alice", "Bob");
        RawClient a = c[0], b = c[1];
        Place(rig, "Alice", 8, 5);
        Place(rig, "Bob", 23, 5);
        obj_machinery_door_airlock door;
        using (rig.World.Enter())
            door = Dm.@new<obj_machinery_door_airlock>(locate(3, 5, 1));
        rig.Tick(20);
        List<SoundEvent> heardA = Heard(a), heardB = Heard(b);
        a.Received.Clear();
        b.Received.Clear();

        using (rig.World.Enter())
            Waiting.Detach(door.open());
        rig.Tick(40);
        using (rig.World.Enter())
            Assert.False(Dm.truthy(door.density), "the airlock opened");

        // A, 5 tiles east of it: tg's falloff of vol 30 over range 15, exponent 2.5, min_volume 5 is 13.890 = 28 half units
        SoundEvent open = Assert.Single(heardA, e => e.File == Airlock);
        Assert.Equal(SoundKind.Play, open.Kind);
        Assert.Equal(28, open.Volume);
        Assert.True(open.Positional);
        Assert.Equal((3, 5, 1), (open.X, open.Y, open.Z));
        Assert.Equal(0, open.Muffle); // the room's air
        Assert.Equal(0, open.Occlusion); // an open line along the row
        Assert.InRange(open.Frequency, 32000, 55000); // vary = TRUE
        Assert.InRange(open.Channel, 1, 1011);
        Assert.Contains(a.Received, r => r.Message[0] == (byte)MessageId.Sounds && Holds(r.Message, Encoding.UTF8.GetBytes(Airlock)));

        // B, 20 tiles away, is out of tg's range: nothing of it, and none of B's bytes hold A's event
        Assert.DoesNotContain(heardB, e => e.File == Airlock);
        Assert.All(b.Received, r => Assert.False(Holds(r.Message, Encoding.UTF8.GetBytes(Airlock)), "B's " + (MessageId)r.Message[0] + " holds the airlock's path"));
        Assert.All(SoundsOf(b), s => Assert.DoesNotContain(s.Events, e => e.Kind == SoundKind.Play && e.Volume == 28));
        Assert.Equal(0, rig.Host.SoundsDropped);
        Assert.Empty(rig.Runtimes);
    }

    [Fact]
    public void ASoundTakesTheListenersAreaEnvironmentAndUseReverbFalsePlaysItDry()
    {
        // D296: playsound_local writes the listener's area's sound_environment (sound.dm:157-165), the wire carries it + 1; with
        // use_reverb FALSE tg kills the room levels in the echo (sound.dm:166-169), which the server sends as none
        using IntegrationRig rig = Start(out RawClient[] c, "Alice");
        mob alice = Place(rig, "Alice", 8, 5);
        rig.Tick(2);
        List<SoundEvent> heard = Heard(c[0]);
        using (rig.World.Enter())
        {
            ((area)get_area(alice)).sound_environment = SOUND_ENVIRONMENT_STONEROOM;
            alice.playsound_local(locate(10, 5, 1), Airlock, 50, 0f);
            alice.playsound_local(locate(10, 5, 1), Airlock, 50, 0f, use_reverb: 0f);
        }
        rig.Tick(2);
        List<SoundEvent> plays = heard.Where(e => e.File == Airlock).ToList();
        Assert.Equal(2, plays.Count);
        Assert.Equal(SOUND_ENVIRONMENT_STONEROOM + 1, plays[0].Environment);
        Assert.Equal(0, plays[1].Environment);
    }

    [Fact]
    public void AJoinGetsTheSoundsOfItsTickAfterItsMapGrid()
    {
        using IntegrationRig rig = Start(out _, "Alice");
        // a bell for every mob that logs in (tg's COMSIG_GLOB_MOB_LOGGED_IN, the end of Login): a sound in the join's own tick
        using (rig.World.Enter())
        {
            var ringer = Dm.@new<datum_test_m4b_audio_login_bell>();
            ringer.RegisterSignal(SSdcs, COMSIG_GLOB_MOB_LOGGED_IN, nameof(ringer.on_logged_in));
        }
        var carol = new RawClient(rig, "Carol");
        for (int i = 0; i < 20 && carol.Welcome == null; i++) rig.Tick();
        Assert.NotNull(carol.Welcome);

        List<MessageId> state = carol.Received.Where(r => r.Channel == Channel.State).Select(r => (MessageId)r.Message[0]).ToList();
        int map = state.IndexOf(MessageId.MapGrid);
        int sounds = state.IndexOf(MessageId.Sounds);
        Assert.True(map >= 0 && sounds > map, "the order: " + string.Join(", ", state));
        Assert.DoesNotContain(MessageId.Delta, state.Take(sounds)); // in the join's own tick, before its first Delta
        Sounds first = SoundsOf(carol)[0];
        Assert.Equal(1u, first.FirstPath); // the join's table starts empty
        // M10 slice F: a join is tg's late join: the bell at the new player's Login, tg's lobby music (playtitlemusic, CHANNEL_LOBBYMUSIC),
        // the bell at the crew member's Login, then the lobby music's stop (AttemptLateSpawn); D296: each Login first stops the
        // ship buzz (CHANNEL_BUZZ 1017: update_ambience_area, login.dm:104), as neither area has an APC to power it (the bells take
        // SSsounds' random channels, counted as 0 here); the lobby music stops on its own channel (volumes in half units)
        string PathOf(SoundEvent e) => e.Path == 0 ? null : first.NewPaths[(int)(e.Path - first.FirstPath)];
        Assert.Equal(new[] { (SoundKind.Stop, (string)null, 0, 1017), (SoundKind.Play, Airlock, 100, 0), (SoundKind.Play, "sound/music/lobby_music/", 100, 1024),
                (SoundKind.Stop, (string)null, 0, 1017), (SoundKind.Play, Airlock, 100, 0), (SoundKind.Stop, (string)null, 0, 1024) },
            first.Events.Select(e => (e.Kind, PathOf(e) is string p && p.StartsWith("sound/music/lobby_music/", StringComparison.Ordinal) ? "sound/music/lobby_music/" : PathOf(e), (int)e.Volume, e.Channel >= 1000 ? (int)e.Channel : 0)));
    }

    [Fact]
    public void TheBudgetDropsTheQuietestPlaysAndKeepsStopsUpdatesAndMutes()
    {
        var feed = new SoundFeed(_ => null, null);
        var view = new SoundView();
        feed.Record(view, new FeedSound(SoundKind.Stop, 9, null));
        for (int i = 0; i < 64; i++)
            feed.Record(view, new FeedSound(SoundKind.Play, 100 + i, "sound/p" + (i % 4) + ".ogg", Volume: i + 1));
        feed.Record(view, new FeedSound(SoundKind.Update, 5, null, Volume: 1));
        feed.Record(view, new FeedSound(SoundKind.Play, 6, "sound/token.ogg", Volume: 0.5f, Mute: true));
        Sounds message = Assert.Single(feed.Build(view, out int dropped));
        Assert.Equal(3, dropped); // 67 events, 3 protected: the 3 quietest plays (volumes 1, 2, 3) go
        Assert.Equal(3, feed.Dropped);
        Assert.Equal(Limits.MaxSoundEvents, message.Events.Count);
        Assert.Equal(SoundKind.Stop, message.Events[0].Kind);
        Assert.Contains(message.Events, e => e is { Kind: SoundKind.Update, Channel: 5 });
        Assert.Contains(message.Events, e => e is { Kind: SoundKind.Play, Channel: 6, Mute: true, Volume: 1 });
        Assert.DoesNotContain(message.Events, e => e.Channel is 100 or 101 or 102);
        List<int> channels = message.Events.Select(e => (int)e.Channel).ToList();
        Assert.Equal(channels.Where(ch => ch >= 100).OrderBy(ch => ch), channels.Where(ch => ch >= 100)); // tg's order kept
        Assert.Equal(1u, message.FirstPath);
        Assert.Equal(5, message.NewPaths.Count); // p3, p0, p1, p2 (as they come) and the token
        Assert.Equal(5, view.PathCount);
        Assert.Empty(view.PendingSounds);
        Assert.True(Codec.Encode(message).Length <= Limits.MaxSoundsBytes);

        // a known path costs no new one; new paths are capped too: the loudest 32 of 40 new files
        feed.Record(view, new FeedSound(SoundKind.Play, 7, "sound/p0.ogg", Volume: 10));
        for (int i = 0; i < 40; i++)
            feed.Record(view, new FeedSound(SoundKind.Play, 200 + i, "sound/n" + i + ".ogg", Volume: 50 + i));
        message = Assert.Single(feed.Build(view, out dropped));
        Assert.Equal(8, dropped);
        Assert.Equal(6u, message.FirstPath);
        Assert.Equal(Limits.MaxSoundPaths, message.NewPaths.Count);
        Assert.DoesNotContain("sound/n7.ogg", message.NewPaths);
        Assert.Contains("sound/n8.ogg", message.NewPaths);
        Assert.Contains(message.Events, e => e is { Channel: 7, Path: 2 }); // p0 was the second path of the table ("p3" was dropped)

        // stops alone past the cap go out in more messages; a play then has no room
        for (int i = 0; i < 70; i++) feed.Record(view, new FeedSound(SoundKind.Stop, i + 1, null));
        feed.Record(view, new FeedSound(SoundKind.Play, 300, "sound/p0.ogg", Volume: 100));
        List<Sounds> stops = feed.Build(view, out dropped);
        Assert.Equal(new[] { 64, 6 }, stops.Select(s => s.Events.Count));
        Assert.Equal(1, dropped);
        Assert.Empty(feed.Build(view, out _)); // nothing heard: no message

        view.Restart();
        Assert.Equal(0, view.PathCount);
    }

    [Fact]
    public void TheHostSendsTheBudgetAndCountsTheDroppedPlays()
    {
        using IntegrationRig rig = Start(out RawClient[] c, "Alice");
        mob alice = Place(rig, "Alice", 10, 5);
        c[0].Received.Clear();
        using (rig.World.Enter())
        {
            for (int i = 0; i < 66; i++)
                alice.playsound_local(null, Dm.resource(Airlock), i + 1, 0f);
            alice.stop_sound_channel(1018);
        }
        rig.Tick();
        Sounds sent = Assert.Single(SoundsOf(c[0]));
        Assert.Equal(Limits.MaxSoundEvents, sent.Events.Count);
        Assert.Equal(SoundKind.Stop, sent.Events[^1].Kind);
        // 67 events, the stop kept: the plays of volume 1, 2 and 3 dropped; 4 is 8 half units
        Assert.Equal(8, sent.Events.Where(e => e.Kind == SoundKind.Play).Min(e => e.Volume));
        Assert.Equal(new[] { Airlock }, sent.NewPaths);
        Assert.Equal(3, rig.Host.SoundsDropped);
        Assert.Equal(3, rig.Host.Metrics.SoundsDropped);
    }

    private static void Wall(int x, int y) => Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_closed_wall)));

    private static byte Occlusion(SoundFeed feed, int lx, int ly, int sx, int sy)
    {
        float occlusion = feed.Occlusion(new SoundView(), locate(lx, ly, 1), locate(sx, sy, 1));
        return (byte)Math.Round(occlusion * 255);
    }

    [Fact]
    public void TheOcclusionComesFromTheStationGraph()
    {
        using IntegrationRig rig = Start(out RawClient[] c, "Alice");
        var feed = new SoundFeed(_ => null, null);
        obj_machinery_door_airlock door;
        using (rig.World.Enter())
        {
            for (int y = 2; y <= 7; y++) Wall(13, y);  // a wall with a gap at its north end: a corner to go around
            for (int y = 2; y <= 8; y++) Wall(19, y);  // a wall right across the room...
            Waiting.NoSleep(locate(19, 5, 1).ChangeTurf(typeof(turf_open_floor_iron)));
            door = Dm.@new<obj_machinery_door_airlock>(locate(19, 5, 1)); // ...with a closed airlock in it
        }
        rig.Tick(5);
        using (rig.World.Enter())
        {
            Assert.Equal(0, Occlusion(feed, 5, 5, 10, 5)); // an open line
            Assert.Equal(0, Occlusion(feed, 3, 3, 7, 7)); // a diagonal across the open room
            Assert.Equal(0, Occlusion(feed, 5, 5, 5, 5)); // the own tile
            byte corner = Occlusion(feed, 11, 3, 15, 3);
            Assert.InRange(corner, 1, 128); // around the wall's end: heard, duller
            Assert.Equal(255, Occlusion(feed, 17, 3, 21, 3)); // behind the wall
            Assert.Equal(255, Occlusion(feed, 17, 5, 21, 5)); // behind the closed door
            Assert.Equal(0, Occlusion(feed, 17, 5, 19, 5)); // the door itself, reached by its face
            Assert.True(Dm.truthy(door.density));
        }

        // through the real path: a sound behind the closed door reaches Alice sealed off
        Place(rig, "Alice", 17, 5);
        List<SoundEvent> heard = Heard(c[0]);
        using (rig.World.Enter())
            playsound(locate(21, 5, 1), Dm.resource(Airlock), 30, 0f);
        rig.Tick();
        Assert.Equal(255, Assert.Single(heard, e => e.File == Airlock).Occlusion);

        using (rig.World.Enter())
            Waiting.Detach(door.open());
        rig.Tick(40);
        using (rig.World.Enter())
        {
            Assert.False(Dm.truthy(door.density), "the airlock opened");
            Assert.Equal(0, Occlusion(feed, 17, 5, 21, 5)); // the open door joins the rooms
        }
        Assert.Empty(rig.Runtimes);
    }

    [Fact]
    public void TheMuffleIsOneMinusTgsPressureFactorAndAVacuumMufflesWhatTgStillLetsThrough()
    {
        using IntegrationRig rig = Start(out RawClient[] c, "Alice");
        mob alice = Place(rig, "Alice", 10, 5);
        List<SoundEvent> heard = Heard(c[0]);
        using (rig.World.Enter())
        {
            var here = (turf_open)locate(10, 5, 1);
            var next = (turf_open)locate(11, 5, 1);
            Assert.Equal(0f, SoundFeed.Muffle(here, next)); // the room's air
            Assert.Equal(0f, SoundFeed.Muffle(locate(1, 1, 1), locate(1, 2, 1))); // two walls: no air of their own

            here.air = SSair.parse_gas_string("o2=11;n2=41;TEMP=293.15") as datum_gas_mixture;
            float pressure = Dm.num(here.air.return_pressure());
            Assert.InRange(pressure, 45f, 55f);
            Assert.Equal(1f - (pressure - 10f) / (101.325f - 10f), SoundFeed.Muffle(here, next), 4); // the lower of the two
            Assert.Equal(SoundFeed.Muffle(here, next), SoundFeed.Muffle(here, locate(1, 5, 1)), 4); // a wall's tile does not count

            // a vacuum on both tiles: tg lets 0.15 of the sound through at a tile (touching it); the muffle is whole
            here.air = SSair.parse_gas_string("TEMP=2.7") as datum_gas_mixture;
            next.air = SSair.parse_gas_string("TEMP=2.7") as datum_gas_mixture;
            Assert.Equal(1f, SoundFeed.Muffle(here, next));
            playsound(next, Dm.resource(Airlock), 30, 0f);
        }
        rig.Tick();
        SoundEvent sound = Assert.Single(heard, e => e.File == Airlock);
        Assert.Equal(255, sound.Muffle);
        Assert.Equal(6, sound.Volume); // 21.537 x 0.15 = 3.2306: 6 half units
        Assert.Equal(0, sound.Occlusion);
    }

    [Fact]
    public void ASoundTgPlaysRegardlessOfPressureCarriesNoMuffleInAVacuum()
    {
        using IntegrationRig rig = Start(out RawClient[] c, "Alice");
        Place(rig, "Alice", 10, 5);
        List<SoundEvent> heard = Heard(c[0]);
        using (rig.World.Enter())
        {
            var here = (turf_open)locate(10, 5, 1);
            var next = (turf_open)locate(11, 5, 1);
            here.air = SSair.parse_gas_string("TEMP=2.7") as datum_gas_mixture;
            next.air = SSair.parse_gas_string("TEMP=2.7") as datum_gas_mixture;
            playsound(next, Dm.resource(Airlock), 30, 0f, pressure_affected: 0f); // tg: heard as in air (sound.dm:131)
        }
        rig.Tick();
        SoundEvent sound = Assert.Single(heard, e => e.File == Airlock);
        Assert.Equal(0, sound.Muffle);
        Assert.Equal(43, sound.Volume); // 21.537 at one tile, no pressure factor: 43 half units
    }

    [Fact]
    public void TheLengthsComeFromTheSoundIndexAndAMissingIndexGivesNone()
    {
        string dir = Path.Combine(Path.GetTempPath(), "anomaly-sound-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string index = Path.Combine(dir, "index.json");
            File.WriteAllText(index, "{\"version\":1,\"defaultLicence\":\"x\",\"files\":[\n"
                + "{\"path\":\"sound/machines/airlock/airlock.ogg\",\"ds\":12.5,\"rate\":44100,\"channels\":1,\"licence\":\"x\",\"nc\":false},\n"
                + "{\"path\":\"sound/effects/a.ogg\",\"ds\":3,\"rate\":22050,\"channels\":1},\n"
                + "{\"path\":\"sound/effects/bad.ogg\",\"ds\":\"n/a\"}\n]}");
            IReadOnlyDictionary<string, float> read = ServerHost.ReadSoundIndex(index, out string error);
            Assert.Null(error);
            Assert.Equal(2, read.Count);
            Assert.Equal(3f, read["sound/effects/a.ogg"]);

            ServerConfig config = TestServer.DefaultConfig();
            using (TestServer server = TestServer.Start(new ServerConfig
                   {
                       Seed = config.Seed, RoomWidth = 12, RoomHeight = 12, RoomSpace = 0, Console = false, StatusEveryTicks = 1000, SoundIndex = index,
                   }))
            {
                using (server.World.Enter())
                {
                    Assert.Equal(12.5f, Dm.num(SSsounds.get_sound_length(Airlock)));
                    Assert.Equal(3f, Dm.num(SSsounds.get_sound_length(Dm.resource("sound/effects/a.ogg"))));
                }
                Assert.DoesNotContain("sounds: the index", server.Out.ToString());
            }

            string missing = Path.Combine(dir, "none.json");
            using (TestServer server = TestServer.Start(new ServerConfig
                   {
                       Seed = config.Seed, RoomWidth = 12, RoomHeight = 12, RoomSpace = 0, Console = false, StatusEveryTicks = 1000, SoundIndex = missing,
                   }, strict: false))
            {
                using (server.World.Enter())
                    Assert.Equal(0f, Dm.num(SSsounds.get_sound_length("sound/effects/a.ogg")));
                Assert.Single(server.Out.ToString().Split('\n'), l => l.StartsWith("sounds: the index", StringComparison.Ordinal));
            }

            File.WriteAllText(index, "{\"version\":1}");
            Assert.Null(ServerHost.ReadSoundIndex(index, out error));
            Assert.NotNull(error);
            File.WriteAllText(index, "not json");
            Assert.Null(ServerHost.ReadSoundIndex(index, out error));
            Assert.NotNull(error);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TheSoundIndexOptionIsReadFromTheCommandLine()
    {
        ServerConfig config = ServerConfig.Parse(new[] { "--ticks", "5", "--sound-index", "x/index.json" }, _ => null, out string error);
        Assert.Null(error);
        Assert.Equal("x/index.json", config.SoundIndex);
        Assert.Equal(ServerConfig.DefaultSoundIndex, ServerConfig.Parse(new[] { "--ticks", "5" }, _ => null, out _).SoundIndex);
        Assert.Contains("--sound-index", ServerConfig.Usage);
    }
}

/// <summary>A test fixture (rules 15): plays a sound to each mob that logs in, from tg's global login signal.</summary>
[DmPath("/datum/test_m4b_audio_login_bell")]
public class datum_test_m4b_audio_login_bell : datum
{
    [SignalHandler]
    public object on_logged_in(object source = null, object who = null)
    {
        (who as mob)?.playsound_local(null, Dm.resource("sound/machines/airlock/airlock.ogg"), 50, 0f);
        return null;
    }
}
