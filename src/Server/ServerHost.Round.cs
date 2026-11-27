using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// M10 slice H (D15): the round's restart. tg's world/Reboot ends in BYOND's own reboot, which restarts the world and makes every client
// reconnect; the port's host builds a fresh world in-process. D306 (protocol 62, the host's lobby): the connections stay; each live
// session is closed in the old world and a fresh session of its key takes its connection, waiting between rounds (the BetweenRounds
// texts; whatever it sends meanwhile is dropped) while the next world is built on a worker thread; once that world opens its joins every
// waiting session joins it, as a reconnect does, into the new round's lobby. A replay does not restart (its log is one round).
public sealed partial class ServerHost
{
    /// <summary>How many times tg's <c>world/Reboot</c> replaced the world (the rounds after the first).</summary>
    public int Restarts { get; private set; }

    /// <summary>The status line the waiting clients get (<see cref="BetweenRounds.Type"/>).</summary>
    public const string RoundRestartText = "The server is restarting...";

    /// <summary>D306: the next round's world while a worker builds it (null otherwise); the tick only pumps the network meanwhile.</summary>
    private Task<World> _nextWorld;

    /// <summary>D306: the sessions waiting between rounds, their Joins held until the new world opens its joins.</summary>
    private readonly List<Session> _waiting = new();

    /// <summary>D306: whether the next round's world is still being built.</summary>
    public bool BuildingNextWorld => _nextWorld != null;

    /// <summary>D306: blocks until the worker's next world is built (a fast-forward test's tick does not wait for real time); the next tick takes it.</summary>
    public void WaitForNextWorld() => _nextWorld?.Wait();

    /// <summary>D306: the sessions waiting between rounds.</summary>
    public IReadOnlyList<Session> WaitingSessions => _waiting;

    /// <summary>tg's <c>SSmap_vote.next_map_config</c> set by the admin Change Map verb: the map every later round builds (null: the config's map).</summary>
    private volatile string _nextMap;

    /// <summary>The map the next world is built from: an admin's Change Map, else the config's <c>map</c> (null: the test room).</summary>
    public string MapName => _nextMap ?? _config.Map;

    // tg: admin/verbs/maprotation.dm:1 ADMIN_VERB(admin_change_map): the choices are the maps of the maps folder (tg's config.maplist), the
    // chosen one loads at the next round. No custom .dmm upload (the port loads only converted maps), no player-count ranges (no maps.txt).
    private string ChangeMap(string map, mob user)
    {
        string folder = ResolveMapsFolder(_config.MapsFolder, MapName ?? "metastation");
        string[] maps = Directory.Exists(Path.Combine(folder, "map_jsons"))
            ? Directory.GetFiles(Path.Combine(folder, "map_jsons"), "*.json").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal).ToArray()
            : [];
        if (map == null)
            return "maps: " + string.Join(", ", maps.Select(m => m + (m == _config.Map ? " (Default)" : "") + (m == MapName ? " (next)" : "")));
        string chosen = maps.FirstOrDefault(m => m.Equals(map, StringComparison.OrdinalIgnoreCase));
        if (chosen == null)
            return "changemap: no map '" + map + "' (maps: " + string.Join(", ", maps) + ")";
        string by = user != null ? Dm.str(key_name_admin(user)) : ConsoleName;
        log_admin(Dm.text("[] is changing the map to []", by, chosen));
        _nextMap = chosen; // tg: SSmap_vote.set_next_map(virtual_map); admin_override = TRUE
        message_admins(Dm.text("[] has changed the map to []", by, chosen));
        return "the next round loads " + chosen + " (reboot to start it)";
    }

    private void RestartWorld()
    {
        long endedAt = World.Tick;
        Restarts++;
        _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"restart at tick {endedAt}: tg's world/Reboot, round #{Restarts + 1}"));

        // the ended round's title track, for the waiting screen
        string music = null;
        if (!_config.Empty)
        {
            using (World.Enter())
                music = SSticker != null && SSticker.login_music is string track && track.Length > 0 ? track : null;
        }

        // BYOND's reboot reconnects every client: here each live session is closed in the old world and a fresh one of its key keeps
        // the connection (D306). The old world is dropped whole, so no Leave runs in it (BYOND runs no client/Del either, ref
        // /world/proc/Reboot).
        foreach (Session s in _sessions.ToArray())
        {
            // nothing of the old world stays with a session: its client would be deleted in the new world when its key joins again
            // (one client per key), and its objects would keep the whole old world alive (M10 slice J)
            s.Client = null;
            s.Login = null;
            s.HoverOutOfReach = null; // DV691: a thing of the old world
            s.Sounds.Discard();
            s.Outbox.Clear();
            s.ControlOutbox.Clear();
            s.SpeechOutbox.Clear();
            s.PendingActions.Clear();
            s.PendingStamps.Clear();
            if (s.State is SessionState.Leaving or SessionState.Closed)
                continue;
            s.State = SessionState.Closed;
            if (s.Connection == null || !s.Connection.IsConnected)
            {
                if (s.Connection != null)
                    _byConnection.Remove(s.Connection.Id);
                continue;
            }
            s.TransportCounted = true; // the connection's bytes from the window's mark on are the waiting session's
            var next = new Session(++_lastOrdinal, s.Key, s.Token, s.Connection)
            {
                State = SessionState.Joining,
                Address = s.Address,
                LastSeenBefore = s.LastSeenBefore,
                WindowSentMark = s.WindowSentMark,
                WindowReceivedMark = s.WindowReceivedMark,
            };
            AddSession(next);
            _byConnection[s.Connection.Id] = next;
            _waiting.Add(next);
            Send(next, Channel.Control, _codec.Encode(new ServerText(BetweenRounds.Type, RoundRestartText)));
            if (music != null)
                Send(next, Channel.Control, _codec.Encode(new ServerText(BetweenRounds.MusicType, Truncate(music, Limits.MaxServerTextBytes))));
            _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"between rounds #{s.Ordinal} {s.Key} -> #{next.Ordinal}"));
        }
        _transport?.Flush();

        // Gaps R2: BYOND's data folder outlives the reboot, the world's file seam does not: the next round's station traits that an
        // admin planned (tg's FUTURE_STATION_TRAITS_FILE, written by the station traits panel) go over to the new world.
        if (!_config.Empty)
        {
            using (World.Enter())
                _futureStationTraits = DmWorldState.Of(World).Files.ReadText(Defines.FUTURE_STATION_TRAITS_FILE);
            // Gaps R6 (D332): the next round's date goes on from where this one ended (world.realtime keeps running over BYOND's reboot)
            if (_realtimeStart != 0d)
                _realtimeStart = DmWorldState.Of(World).RealtimeStart + World.Time;
        }

        // The host's state of the old world.
        _queue.Clear();
        _due.Clear();
        _joinedThisTick.Clear();
        _joins.Clear();
        _forget.Clear();
        _directJoins.Clear();
        ForgetRespawns();
        _avatarIds.Clear();
        _possessReturn.Clear(); // D346: the old world's bodies
        _ceilingEdits.Clear();
        _spawnCursor = 0;
        _connecting = null;
        _consoleRequester = null;
        _ssMobs = null;
        _ssMobsFired = 0;
        _screentipsInstalled = false;
        _minimapWorld = null; // the minimap cache's key would keep the whole old world alive until the next minimap
        JoinsOpen = false;
        JoinsOpenedAt = 0;
        Room = null;
        MapLoaded = false;

        _replicator = _replicator.ForNewWorld();
        // D306: a stand-in until the worker's world is ready (the old one goes now). CreateWorld touches only the new world, the new
        // replicator and the host's map fields, which the waiting tick (PumpBetweenRounds) leaves alone, and the core's one ambient
        // state, World.Current, is per thread.
        World = new World(_config.Seed);
        _nextWorld = Task.Run(CreateWorld);
    }

    // D306: a tick while the next world builds: the network only (LiteNetLib's own threads keep the pings), then the
    // new world once it is ready. A stop waits for the build, so the shutdown runs in a whole world.
    private void PumpBetweenRounds()
    {
        _transport?.Poll();
        _transport?.Flush();
        if (!_nextWorld.IsCompleted && !_stopRequested)
            return;
        World = _nextWorld.GetAwaiter().GetResult();
        _nextWorld = null;
        _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"next world built: {_waiting.Count} session(s) wait for its joins"));
    }

    // D306: the new world opened its joins: each waiting session joins it as a reconnect of its key does, into the lobby.
    private void JoinWaiting(World world)
    {
        foreach (Session s in _waiting)
        {
            if (s.State != SessionState.Joining) continue; // its connection dropped while it waited
            _db?.UpdatePlayerSeen(s.Ckey, s.Address, RoundId);
            _queue.Enqueue(new QueuedCommand(world.Tick + 1, s.Ordinal, QueuedCommand.JoinSeq, CommandKind.Join, s.Key, WaitsForLoad: s.WaitsForLoad)); // its client builds the next round's world first (DV597)
            _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"join #{s.Ordinal} {s.Key} at tick {world.Tick} (waited between rounds)"));
        }
        _waiting.Clear();
    }
}
