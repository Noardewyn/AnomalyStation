using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>Where a feed is in its life.</summary>
public enum FeedPhase : byte
{
    /// <summary>Not started.</summary>
    Idle,

    /// <summary>The transport is connecting (or the host is being resolved).</summary>
    Connecting,

    /// <summary>Connected; the join sequence (Welcome, MapGrid, Snapshot) has not finished.</summary>
    Joining,

    /// <summary>The map and the first snapshot are in; the world is drawn.</summary>
    Playing,

    /// <summary>Over: rejected, failed, dropped or disconnected (<see cref="IWorldFeed.CloseReason"/> says why).</summary>
    Closed,

    /// <summary>D306: the round ended and the server builds the next world; the connection stays, the old world is gone, and the next join sequence makes it <see cref="Joining"/>, then <see cref="Playing"/> again.</summary>
    BetweenRounds,
}

/// <summary>Helpers for the entities the renderer reads (they are <see cref="ReplicaEntity"/> objects of the replica).</summary>
public static class ReplicaEntityExtensions
{
    /// <summary>True for a mob, or an exosuit, which walks and turns as one (DV358).</summary>
    public static bool IsMob(this ReplicaEntity entity) => entity.TypePath != null
        && (entity.TypePath.StartsWith("/mob", StringComparison.Ordinal) || entity.IsExosuit());

    /// <summary>True for an exosuit (<c>/obj/vehicle/sealed/mecha</c>).</summary>
    public static bool IsExosuit(this ReplicaEntity entity) => entity.TypePath != null && entity.TypePath.StartsWith("/obj/vehicle/sealed/mecha", StringComparison.Ordinal);

    /// <summary>The scale a mob's picture is drawn at: <see cref="WorldScale.MobScale"/>, but an exosuit fills its tile, a head taller than its pilot.</summary>
    public static float MobDrawScale(this ReplicaEntity entity) => entity.IsExosuit() ? 1f : WorldScale.MobScale;
}

/// <summary>
/// What the game scene needs from "the server": a connection that is polled once per frame, takes the held directions
/// and shows the replicated world as <see cref="ReplicaEntity"/> objects, which the renderer draws through
/// <see cref="ReplicaEntity.TryInterpolate"/> (the one source of truth for the glide). <see cref="GameClient"/> is the real one (LiteNetLib transport, wire protocol,
/// <c>ReplicaWorld</c>); <see cref="FakeFeed"/> is the scripted world behind <c>--fake-server</c>: a small server whose messages go through the
/// real codec into a real <see cref="ReplicaWorld"/> (development: the scenes, the camera and the interpolation can be seen
/// and checked without a server).
/// </summary>
public interface IWorldFeed
{
    /// <summary>Where the connection is.</summary>
    FeedPhase Phase { get; }

    /// <summary>Why the feed is <see cref="FeedPhase.Closed"/> (a sentence for the status label), null before.</summary>
    string CloseReason { get; }

    /// <summary>The server's reason code of a closed feed (M10 slice H: <see cref="DisconnectReason.RoundRestart"/> asks for a rejoin); none for a feed with no server.</summary>
    DisconnectReason CloseCode => DisconnectReason.None;

    /// <summary>True when the server refused the connection (a reject, not a drop).</summary>
    bool Rejected { get; }

    /// <summary>A short text for the HUD: "127.0.0.1:7777 as Alice" or "fake server".</summary>
    string Description { get; }

    /// <summary>Does the transport's work and applies what arrived; call once per frame on the main thread, never blocks. <paramref name="nowSeconds"/> is the monotonic time.</summary>
    void Poll(double nowSeconds);

    /// <summary>The seq of the last frame sent (0: none).</summary>
    uint LastSentSeq { get; }

    /// <summary>The seq of the last frame the server reported as applied (0: none yet).</summary>
    uint LastAppliedSeq { get; }

    /// <summary>The map of the join sequence; null before it arrived.</summary>
    MapGrid Map { get; }

    /// <summary>The server tick of the newest applied Snapshot or Delta; -1 before the first.</summary>
    long Tick { get; }

    /// <summary>How many Deltas were applied (for the HUD and the soak).</summary>
    long DeltaCount { get; }

    /// <summary>The id of the connection's own mob; 0 when it has none.</summary>
    long OwnEntity { get; }

    /// <summary>The number of entities.</summary>
    int Count { get; }

    /// <summary>Every entity, in increasing id order (for the scene's first fill: valid until the next poll; later changes arrive as events).</summary>
    IEnumerable<ReplicaEntity> Entities { get; }

    /// <summary>An entity appeared (after a Snapshot or a Delta applied).</summary>
    event Action<ReplicaEntity> Spawned;

    /// <summary>An entity disappeared.</summary>
    event Action<ReplicaEntity> Despawned;

    /// <summary>The server's text for the player (<c>to_chat</c>): the HTML as sent.</summary>
    event Action<ServerText> ServerTextReceived;

    /// <summary>
    /// M4a (the feed version 4, the M4a contracts step; GameClient and FakeFeed implement it, M4a P7): tg's screentip for the
    /// hovered target (<see cref="ContextHintsMessage"/>, the answer to a Hover). The default accessors keep the version-3
    /// feeds building; a feed that never raises it gives no hints.
    /// </summary>
    event Action<ContextHintsMessage> ContextHintsReceived
    {
        add { }
        remove { }
    }

    /// <summary>
    /// M4b slice 3 (protocol version 5, the dev menu's gas overlay): the air around a point (<see cref="DevGasReport"/>, the
    /// answer to a <see cref="ActionKind.DevGasProbe"/>). A feed that never raises it shows no gas overlay.
    /// </summary>
    event Action<DevGasReport> DevGasReportReceived
    {
        add { }
        remove { }
    }

    /// <summary>Protocol version 40: the radio prefixes of the worn headset's channels (the chat window's Radio list). A feed that never raises it lists none.</summary>
    event Action<IReadOnlyList<string>> RadioChannelsReceived
    {
        add { }
        remove { }
    }

    /// <summary>Protocol version 38: a message of the server's say window datum to the say window (<see cref="SayModalMessage"/>). A feed that never raises it sends no props.</summary>
    event Action<SayModalMessage> SayModalReceived
    {
        add { }
        remove { }
    }

    /// <summary>Protocol version 38 (DV160): one runechat line to draw over a speaker (<see cref="RunechatMessage"/>).</summary>
    event Action<RunechatMessage> RunechatReceived
    {
        add { }
        remove { }
    }

    /// <summary>Protocol version 63 (D307): one line for the client's text to speech (<see cref="TtsMessage"/>).</summary>
    event Action<TtsMessage> TtsReceived
    {
        add { }
        remove { }
    }

    /// <summary>Protocol version 67 (D315): the minimap of level <paramref name="z"/>, null until the server sent it.</summary>
    MinimapMessage MinimapOf(int z) => null;

    /// <summary>Protocol version 68 (D315): the last living mobs of the own level the dev trackers got, null without them.</summary>
    MinimapMobsMessage MinimapMobs => null;

    /// <summary>Protocol version 84 (gaps E2a): the weather of the own mob's level the server last sent, null before the first.</summary>
    WeatherMessage Weather => null;

    /// <summary>Protocol version 70 (D320): what the held tool would build (the ghost on the aimed tile), <see cref="BuildPreviewMessage.None"/> for nothing.</summary>
    BuildPreviewMessage BuildPreview => BuildPreviewMessage.None;

    /// <summary>Protocol version 92 (DV96): the own gun's sight the server last sent (whether a gun is held, its cone), <see cref="GunSightMessage.None"/> before the first.</summary>
    GunSightMessage GunSight => GunSightMessage.None;

    /// <summary>Protocol version 92 (DV96): how many times the own shots' hit count changed since the join (each change is a hit marker).</summary>
    int GunSightHits => 0;

    /// <summary>The transport's round trip in milliseconds.</summary>
    int RoundTripMs { get; }

    /// <summary>Payload bytes sent and received so far.</summary>
    long BytesSent { get; }

    long BytesReceived { get; }

    /// <summary>Leaves: closes the connection. The feed becomes Closed on a following poll.</summary>
    void Disconnect();

    // ---- the feed version 2 (M2 contracts; GameClient and FakeFeed implement it, M2 P8) ----

    /// <summary>
    /// M2: the replica behind the feed (protocol version 2: the appearance, resource and string tables, the own private
    /// state, the screen entries, the collision view for the predictor). M2 P7's renderers read the tables from it; M2 P8's
    /// predictor, HUD and camera the rest.
    /// </summary>
    ReplicaWorld Replica => throw new NotImplementedException("M2 P8");

    /// <summary>M2: sends the newest input frames (the predictor's <c>CopyRecentFrames</c>) on the Input channel; a no-op unless playing.</summary>
    void SendMotion(MotionInputMessage frames) => throw new NotImplementedException("M2 P8");

    /// <summary>M2: sends an action (rest, walk/run, combat mode, a click, a dev command line) on the Control channel; a no-op unless playing.</summary>
    void SendAction(in ActionMessage action) => throw new NotImplementedException("M2 P8");

    /// <summary>Protocol version 53: sends the Esc menu's bug report on the State channel; a no-op unless playing (and in a fake world).</summary>
    void SendBugReport(BugReportMessage report)
    {
    }
}
