using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

// Owner: M1 P5 (bots and soak), protocol version 2 by M2 P6. What the server tests (the end-to-end run of bots over the
// loopback transport) and the soak script use. The M1 members keep their names; the M2 additions are marked.

/// <summary>What a bot does once it has joined (scope 3, P5: wander, idle, churn, and the abusive modes the tests need; M2 P6: rest, shove, mixed and the version-2 abuse).</summary>
public enum BotBehaviour : byte
{
    /// <summary>Holds a heading for a random time in continuous space, steers away from solid tiles, re-picks when blocked (scope risk 12).</summary>
    Wander,

    /// <summary>Joins and sends nothing.</summary>
    Idle,

    /// <summary>Joins, wanders, leaves and reconnects with its token, again and again.</summary>
    Churn,

    /// <summary>Abuse: MotionInput messages as fast as it is polled (the host's rate limit must drop them and close the session as flooding).</summary>
    Flood,

    /// <summary>Abuse: undecodable messages (strikes must disconnect it).</summary>
    Malformed,

    /// <summary>Abuse: messages over <see cref="Limits.MaxInboundMessageBytes"/>.</summary>
    Oversized,

    /// <summary>P5 addition. Abuse: MotionInput messages sent as soon as the transport connects, before the Welcome (a command before Welcome is a strike).</summary>
    PreWelcomeCommand,

    /// <summary>P5 addition. Abuse: a Hello with a protocol version the server does not speak (it must be refused with <see cref="DisconnectReason.WrongVersion"/>).</summary>
    WrongVersion,

    /// <summary>M2 P6. Wanders, and every 5 to 15 s lies down (Rest), stays 3 to 6 s without moving, and gets up (Rest). Presses walk/run and combat mode now and then.</summary>
    Rest,

    /// <summary>M2 P6. Walks to the nearest other standing body and right-clicks it within 1.5 tiles (the shove), one click per 20 ticks at most; wanders when none is in sight.</summary>
    Shove,

    /// <summary>M2 P6. A runner-level mix: bot <c>i</c> runs wander, rest, shove or wander for <c>i % 4</c> = 0, 1, 2, 3 (<see cref="BotBehaviourNames.Resolve"/>).</summary>
    Mixed,

    /// <summary>M3 P5. Walks a door-aware route to the nearest tile no bot has reached yet (a tour of the whole map), presses into closed doors so that tg's Bumped opens them, never enters space, and re-plans when it stands still.</summary>
    Tour,

    /// <summary>M2 P6. Abuse: Action messages above <see cref="Limits.ActionRate"/> (dropped, and 600 drops in 200 ticks close the session as flooding).</summary>
    ActionFlood,

    /// <summary>M2 P6. Abuse: a valid MotionInput message whose seq is more than <see cref="Limits.MaxInputSeqLead"/> ahead of the last (a strike).</summary>
    ForgedSeq,

    /// <summary>M2 P6. Abuse: Action messages that are garbage (strikes), and legal clicks on an entity that does not exist (dropped silently).</summary>
    BadAction,

    /// <summary>M2 P6. Abuse: a legal dev command with text at the cap (answered, a refusal when dev commands are off) and one over the cap (a strike).</summary>
    DevAbuse,

    // ---- protocol version 4 (M4a P6) ----

    /// <summary>M4a P6. Wanders and fiddles with what is near for load and soak: hovers and clicks the nearest entity (an item first) with a random icon pixel, presses the tg keybindings (swap hands, activate in hand, drop, quick equip, throw mode), clicks its own HUD entries (screen targets) and throws at a random cell (a turf click).</summary>
    Fiddle,

    /// <summary>M4a P6. Abuse: well-formed clicks and hovers on targets that are not there: an entity id no world has, an entity too far away, a turf on another level or outside the view, a screen id the session does not own (all dropped silently and counted, no strike).</summary>
    ForgedTarget,

    /// <summary>M4a P6. Abuse: hovers faster than <see cref="ActionLimits"/>' hover rate but under the overall action rate (the surplus is dropped silently; the session stays).</summary>
    HoverFlood,

    /// <summary>M4a P6. Abuse: version-4 Action messages that break the format (an unknown keybinding code, a key-up flag of 2, an unknown target kind, a turf at 0, an icon pixel over 32, a hover with a button, an unknown action kind): strikes, three of which close the session.</summary>
    BadV4,
}

/// <summary>A bot's settings.</summary>
public sealed class BotOptions
{
    /// <summary>The player name (its BYOND key).</summary>
    public string Name { get; init; } = "Bot";

    /// <summary>The seed of the bot's own random choices (never the world's).</summary>
    public ulong Seed { get; init; } = 1;

    public BotBehaviour Behaviour { get; init; } = BotBehaviour.Wander;

    /// <summary>P5 addition. The wander knobs (also used by <see cref="BotBehaviour.Churn"/>).</summary>
    public WanderSettings Wander { get; init; } = new();

    /// <summary>M3 P5. The tour behaviour's knobs.</summary>
    public TourSettings Tour { get; init; } = new();

    /// <summary>M3 P5. The tiles the tour has reached, shared by the bots of a run (null: the bot keeps its own).</summary>
    public TourCoverage Coverage { get; init; }

    /// <summary>
    /// P5 addition. After a connection ended that the bot did not end itself, connect again (with the token) after
    /// <see cref="RetryDelayUpdates"/> updates, unless the reason is final (a wrong version, a bad hello or name). Abusive
    /// behaviours never do. Default off: a test sees what happened, the soak turns it on.
    /// </summary>
    public bool ReconnectOnDisconnect { get; init; }

    /// <summary>
    /// P5 addition. How long, in updates, a bot that has not joined yet keeps trying after a refusal that can pass (the
    /// server is full or still booting, the connection could not be made): 0 tries once. The budget counts from the first
    /// attempt and is spent when the bot joins.
    /// </summary>
    public int JoinRetryUpdates { get; init; }

    /// <summary>P5 addition. Updates between a failed attempt and the next (plus up to as much again, so that 150 bots do not retry in step).</summary>
    public int RetryDelayUpdates { get; init; } = 20;

    /// <summary>P5 addition. Churn: the fewest updates a session lasts (default 5 s at 20 Hz).</summary>
    public int ChurnMinUpdates { get; init; } = 100;

    /// <summary>P5 addition. Churn: the most updates a session lasts (20 s).</summary>
    public int ChurnMaxUpdates { get; init; } = 400;

    /// <summary>P5 addition. Churn: the longest pause between leaving and connecting again.</summary>
    public int ChurnReconnectMaxUpdates { get; init; } = 20;

    /// <summary>P5 addition. Flood: MotionInput messages per update (50 at 20 updates a second is 1000 a second; the limit is 30).</summary>
    public int FloodFramesPerUpdate { get; init; } = 50;

    /// <summary>P5 addition. PreWelcomeCommand: MotionInput messages sent per update while connecting, and in all.</summary>
    public int PreWelcomeFramesPerUpdate { get; init; } = 3;

    public int PreWelcomeFramesMax { get; init; } = 12;

    // ---- M2 P6 ----

    /// <summary>
    /// The bot predicts its own capsule with the motion core (the headless prediction test client): a <see cref="MotionPredictor"/>
    /// started from the first private block, an <see cref="InputClock"/> that sends the frames, a reconciliation on every
    /// Delta. Off by default; a bot that does not predict still sends frames, from a small ring of its own.
    /// </summary>
    public bool Predict { get; init; }

    /// <summary>The frames the server's jitter buffer should hold for a predicting bot (the input clock's target): 1 on localhost.</summary>
    public int InputClockDepth { get; init; } = 1;

    /// <summary>The bot's number in its run: <see cref="BotBehaviour.Mixed"/> picks its behaviour from it (the runner sets it).</summary>
    public int Index { get; init; }

    /// <summary>Press walk/run and combat mode now and then (rarely, in pairs). Null: on for <see cref="BotBehaviour.Rest"/> and <see cref="BotBehaviour.Mixed"/>.</summary>
    public bool? Toggles { get; init; }

    /// <summary>The timings of the rest behaviour.</summary>
    public RestSettings Rest { get; init; } = new();

    /// <summary>ActionFlood: Action messages per update (30 at 20 updates a second is 600 a second; the limit is 20).</summary>
    public int ActionFloodPerUpdate { get; init; } = 30;

    /// <summary>ForgedSeq: ordinary frames sent after the join before the forged ones start.</summary>
    public int ForgedSeqWarmupUpdates { get; init; } = 5;
}

/// <summary>The state of a <see cref="Bot"/>.</summary>
public enum BotState : byte
{
    Idle,
    Connecting,

    /// <summary>The join sequence completed: Welcome, MapGrid and Snapshot applied, so <see cref="Bot.Replica"/> is usable.</summary>
    Joined,
    Disconnected,
}

/// <summary>
/// One headless protocol client (D55: bots are protocol clients): it connects with a Hello, applies the join sequence and
/// the Deltas to its <see cref="Replica"/>, and acts by its <see cref="BotOptions.Behaviour"/>. Single-threaded: its owner
/// calls <see cref="Update"/> regularly (the soak runner every 50 ms, the in-process tests once per server tick). Every
/// timer of a bot counts <see cref="Update"/> calls, never a wall clock, so a bot behaves the same at any speed.
/// </summary>
public sealed partial class Bot : IDisposable
{
    /// <summary>P5 addition. The update rate the options' update counts assume (20 a second, as the server's tick).</summary>
    public const int UpdatesPerSecond = 20;

    private const int RttSamplePeriod = UpdatesPerSecond;
    private const int LeaveTimeoutUpdates = 5 * UpdatesPerSecond;

    private enum Leave : byte
    {
        None,
        Requested,
        Churn,
        Desync,
    }

    private readonly Func<IClientTransport> _factory;
    private readonly BotRandom _random;
    private readonly BotBehaviour _effective;
    private readonly BotDriver _driver;
    private bool _transportUsed;
    private string _host;
    private int _port;
    private SessionToken _token;
    private bool _transportConnected;
    private bool _welcomed;
    private bool _snapshotApplied;
    private ISolidity _solidity;
    private ReplicaRouteMap _routeMap; // the tour's map (null for the other behaviours)
    private int _preWelcomeSent;
    private long _joinedAt;
    private Leave _leave;
    private long _leaveAt;
    private long _churnAt;
    private long _reconnectAt = -1;
    private long _attemptStart = -1;

    public Bot(IClientTransport transport, BotOptions options)
    {
        Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        Options = options ?? new BotOptions();
        _random = new BotRandom(Options.Seed);
        _effective = BotBehaviourNames.Resolve(Options.Behaviour, Options.Index);
        bool toggles = Options.Toggles ?? (Options.Behaviour == BotBehaviour.Rest || Options.Behaviour == BotBehaviour.Mixed);
        _driver = new BotDriver(DriverBehaviour(_effective), toggles, _random, Options.Wander, Options.Rest, Options.Coverage, Options.Tour);
        Stats = new BotStats(Options.Name) { Behaviour = BotBehaviourNames.ToName(_effective) };
        Replica = new ReplicaWorld();
        _solidity = MakeSolidity();
        Attach(Transport);
    }

    /// <summary>
    /// P5 addition. A bot that makes a fresh transport for every connection (a churning bot, or one that reconnects, with
    /// LiteNetLib's manual mode: the old client is disposed once its connection ended). The first one is made here.
    /// </summary>
    public Bot(Func<IClientTransport> transportFactory, BotOptions options)
        : this((transportFactory ?? throw new ArgumentNullException(nameof(transportFactory)))(), options)
    {
        _factory = transportFactory;
    }

    /// <summary>The transport of the current (or last) connection.</summary>
    public IClientTransport Transport { get; private set; }

    public BotOptions Options { get; }

    /// <summary>M2 P6. The behaviour the bot actually runs: <see cref="BotOptions.Behaviour"/>, with <see cref="BotBehaviour.Mixed"/> resolved by <see cref="BotOptions.Index"/>.</summary>
    public BotBehaviour Behaviour => _effective;

    /// <summary>The bot's copy of the world (a new one for every connection).</summary>
    public ReplicaWorld Replica { get; private set; }

    public BotState State { get; private set; }

    /// <summary>The Welcome of the current session (default before it).</summary>
    public Welcome Welcome { get; private set; }

    /// <summary>Why the last connection ended (default while connected).</summary>
    public DisconnectInfo LastDisconnect { get; private set; }

    /// <summary>P5 addition. How the last connection ended, as the transport saw it.</summary>
    public TransportDisconnectKind LastDisconnectKind { get; private set; }

    /// <summary>P5 addition. What the bot counted (the report's input).</summary>
    public BotStats Stats { get; }

    /// <summary>P5 addition. <see cref="Update"/> calls so far: the bot's clock.</summary>
    public long UpdateCount { get; private set; }

    /// <summary>P5 addition. The reconnect token of the session (<see cref="SessionToken.None"/> before the first Welcome).</summary>
    public SessionToken Token => _token;

    /// <summary>M2 P6. The decisions behind the bot's walking (its planners, for tests and the report).</summary>
    public BotDriver Driver => _driver;

    /// <summary>Starts connecting to the server (with the token of the last session, if there was one).</summary>
    public void Connect(string host, int port)
    {
        if (State == BotState.Connecting || State == BotState.Joined)
        {
            throw new InvalidOperationException("the bot is already connecting or connected");
        }
        if (host == null) throw new ArgumentNullException(nameof(host));

        _host = host;
        _port = port;
        _reconnectAt = -1;
        if (_attemptStart < 0) _attemptStart = UpdateCount;

        if (_transportUsed && _factory != null)
        {
            Detach(Transport);
            Transport.Dispose();
            Transport = _factory();
            Attach(Transport);
        }
        _transportUsed = true;

        Replica = new ReplicaWorld();
        _betweenRounds = false;
        _solidity = MakeSolidity();
        Welcome = default;
        LastDisconnect = default;
        _transportConnected = false;
        _welcomed = false;
        _snapshotApplied = false;
        ResetMotion();
        _preWelcomeSent = 0;
        _leave = Leave.None;
        _lobbyActedAt = -LobbyActPeriod;
        _driver.Reset();

        ushort version = Options.Behaviour == BotBehaviour.WrongVersion ? (ushort)(ProtocolVersion.Current + 1) : ProtocolVersion.Current;
        byte[] hello = Codec.Encode(new Hello(version, Options.Name, _token));
        State = BotState.Connecting;
        Stats.ConnectAttempts++;
        Transport.Connect(host, port, hello);
        Stats.BytesOut += hello.Length;
        Stats.MessagesOut++;
    }

    /// <summary>P5 addition. Leaves the server on purpose (the transport's <c>Disconnected</c> follows in a later <see cref="Update"/>).</summary>
    public void Disconnect()
    {
        _reconnectAt = -1;
        if (State == BotState.Connecting || State == BotState.Joined)
        {
            BeginLeave(Leave.Requested);
        }
    }

    /// <summary>Polls the transport, applies what arrived and acts.</summary>
    public void Update()
    {
        UpdateCount++;
        Stats.Updates++;

        if (State == BotState.Connecting || State == BotState.Joined)
        {
            Transport.Poll();
        }

        switch (State)
        {
            case BotState.Connecting:
                ActConnecting();
                break;
            case BotState.Joined:
                Stats.ConnectedUpdates++;
                ActJoined();
                break;
            case BotState.Disconnected:
                if (_reconnectAt >= 0 && UpdateCount >= _reconnectAt) Connect(_host, _port);
                break;
        }

        if ((State == BotState.Connecting || State == BotState.Joined) && _leave != Leave.None && UpdateCount - _leaveAt > LeaveTimeoutUpdates)
        {
            // The transport never reported the end of a connection this bot closed: do not wait for it for ever.
            Finish(new TransportDisconnect(TransportDisconnectKind.LocalClose, Array.Empty<byte>()));
        }
    }

    /// <summary>P5 addition. Disposes the transport when the bot made it (the factory constructor); a transport the caller passed in stays the caller's.</summary>
    public void Dispose()
    {
        Detach(Transport);
        if (_factory != null) Transport.Dispose();
    }

    // ---- transport events (raised inside Poll, on this thread) ----

    private void Attach(IClientTransport transport)
    {
        transport.Connected += OnConnected;
        transport.Received += OnReceived;
        transport.Disconnected += OnDisconnected;
    }

    private void Detach(IClientTransport transport)
    {
        transport.Connected -= OnConnected;
        transport.Received -= OnReceived;
        transport.Disconnected -= OnDisconnected;
    }

    private void OnConnected()
    {
        _transportConnected = true;
    }

    private void OnDisconnected(TransportDisconnect disconnect)
    {
        Finish(disconnect);
    }

    private void OnReceived(Channel channel, ReadOnlySpan<byte> message)
    {
        // A message that arrives after the connection ended (a transport that reuses its instance) is not this session's.
        if (State != BotState.Connecting && State != BotState.Joined) return;

        Stats.BytesIn += message.Length;
        Stats.MessagesIn++;

        if (!Codec.TryPeekId(message, out MessageId id))
        {
            Stats.DecodeErrors++;
            return;
        }

        if (_betweenRounds && id != MessageId.Welcome && id != MessageId.ServerText) return; // D306: the old world's last messages

        switch (id)
        {
            case MessageId.Welcome:
                if (Codec.TryDecode(message, out Welcome welcome, out _))
                {
                    _betweenRounds = false;
                    Welcome = welcome;
                    _token = welcome.Token;
                    _welcomed = true;
                    TryCompleteJoin();
                }
                else
                {
                    Desync("undecodable Welcome");
                }
                break;

            case MessageId.MapGrid:
                if (Codec.TryDecode(message, out MapGrid grid, out ProtocolError gridError))
                {
                    Guarded(() => Replica.Apply(grid), "MapGrid");
                }
                else
                {
                    Desync("undecodable MapGrid: " + gridError);
                }
                break;

            case MessageId.Snapshot:
                if (Codec.TryDecode(message, out Snapshot snapshot, out ProtocolError snapshotError))
                {
                    if (!Guarded(() => Replica.Apply(snapshot), "Snapshot")) break;
                    _snapshotApplied = true;
                    Stats.SnapshotsApplied++;
                    TryCompleteJoin();
                }
                else
                {
                    Desync("undecodable Snapshot: " + snapshotError);
                }
                break;

            case MessageId.Delta:
                ApplyDelta(message);
                break;

            case MessageId.ServerText:
                if (Codec.TryDecode(message, out ServerText text, out _))
                {
                    Stats.ServerTexts++;
                    if (text.Type == BetweenRounds.Type) BetweenRoundsBegan();
                }
                else Stats.DecodeErrors++;
                break;

            case MessageId.Airflow:
                // version 6 (D141): the air flow for the client's streams; a bot draws none
                if (!Codec.TryDecode(message, out AirflowUpdate _, out _)) Stats.DecodeErrors++;
                break;

            case MessageId.Sounds:
                // version 13: tg's sounds for this session; a bot plays none, but a sound it cannot read is a decode error
                if (!Codec.TryDecode(message, out Sounds _, out _)) Stats.DecodeErrors++;
                break;

            case MessageId.Radial:
                // version 15: a radial menu of this session (a bot opens none, but one it cannot read is a decode error)
                if (!Codec.TryDecode(message, out RadialMenuMessage _, out _)) Stats.DecodeErrors++;
                break;

            case MessageId.UiWindow:
                // version 16: a tgui window of this session (the bot reads tg's lobby and late join menu from them, M10 slice J)
                if (Codec.TryDecode(message, out UiWindowMessage window, out _)) Guarded(() => Replica.Apply(window), "UiWindow");
                else Stats.DecodeErrors++;
                break;

            case MessageId.Tooltip:
                // version 19: the tooltip of a hovered HUD object (a bot draws none, but one it cannot read is a decode error)
                if (!Codec.TryDecode(message, out TooltipMessage _, out _)) Stats.DecodeErrors++;
                break;

            case MessageId.RadioChannels:
                // version 40: the worn headset's radio prefixes (a bot talks on none, but one it cannot read is a decode error)
                if (!Codec.TryDecode(message, out RadioChannelsMessage _, out _)) Stats.DecodeErrors++;
                break;

            case MessageId.Hotbar:
                // version 99 also (DV689): the own human's hotbar cells (a bot presses none, but one it cannot read is a decode error)
                if (Codec.TryDecode(message, out HotbarMessage hotbar, out _)) Replica.Apply(hotbar);
                else Stats.DecodeErrors++;
                break;

            case MessageId.Motion:
                // version 42: the capsule mobs' positions on the unreliable channel (the move counters count its entries, resends included)
                if (!_snapshotApplied) break;
                if (Codec.TryDecode(message, out MotionMessage motion, out _))
                {
                    if (!Guarded(() => Replica.Apply(motion), "Motion")) break;
                    foreach (MotionEntry e in motion.Entries)
                    {
                        if (e.Id == Replica.OwnEntity) Stats.OwnMoves++;
                        else Stats.OtherMoves++;
                    }
                }
                else Stats.DecodeErrors++;
                break;

            case MessageId.SayModal:
                // version 38: the say window's messages (a bot types nothing, but one it cannot read is a decode error)
                if (!Codec.TryDecode(message, out SayModalMessage _, out _)) Stats.DecodeErrors++;
                break;

            case MessageId.Runechat:
                // version 38: a runechat line over a speaker (a bot draws none)
                if (!Codec.TryDecode(message, out RunechatMessage _, out _)) Stats.DecodeErrors++;
                break;

            default:
                // An id that is not the server's: nothing to apply, nothing broken.
                Stats.DecodeErrors++;
                break;
        }
    }

    private void ApplyDelta(ReadOnlySpan<byte> message)
    {
        if (!_snapshotApplied)
        {
            Desync("a Delta before the Snapshot");
            return;
        }
        if (!Codec.TryDecode(message, out Delta delta, out ProtocolError error))
        {
            Desync("undecodable Delta: " + error);
            return;
        }

        long previous = Replica.Tick;
        try
        {
            Replica.Apply(delta);
        }
        catch (ProtocolException e)
        {
            Desync("the replica refused the Delta of tick " + delta.Tick + ": " + e.Message);
            return;
        }
        if (delta.Tick != previous + 1 || Replica.Tick != delta.Tick)
        {
            Desync("tick gap: the replica was at " + previous + ", the Delta is of " + delta.Tick);
            return;
        }

        Stats.DeltasApplied++;
        Stats.SpawnsSeen += delta.Spawned.Count;
        Stats.DespawnsSeen += delta.Despawned.Count;
        const NetField position = NetField.X | NetField.Y | NetField.Z | NetField.Pos;
        for (int i = 0; i < delta.Changed.Count; i++)
        {
            EntityChange change = delta.Changed[i];
            if ((change.Mask & position) == 0) continue;
            if (change.Id == delta.OwnEntity) Stats.OwnMoves++;
            else Stats.OtherMoves++;
        }

        ReconcilePrediction();
    }

    /// <summary>Runs a replica call; a <see cref="ProtocolException"/> is a desync (the bot leaves and, if it reconnects, joins again).</summary>
    private bool Guarded(Action apply, string what)
    {
        try
        {
            apply();
            return true;
        }
        catch (ProtocolException e)
        {
            Desync("the replica refused the " + what + ": " + e.Message);
            return false;
        }
    }

    private void Desync(string text)
    {
        Stats.Desyncs++;
        Stats.LastDesync = text;
        if (State == BotState.Connecting || State == BotState.Joined) BeginLeave(Leave.Desync);
    }

    // D306: the round ended and the connection stays: wait for the next world's join sequence, into a fresh replica
    private bool _betweenRounds;

    private void BetweenRoundsBegan()
    {
        Stats.RoundRestarts++;
        _betweenRounds = true;
        State = BotState.Connecting;
        Replica = new ReplicaWorld();
        _solidity = MakeSolidity();
        Welcome = default;
        _welcomed = false;
        _snapshotApplied = false;
        ResetMotion();
        _lobbyActedAt = -LobbyActPeriod;
        _driver.Reset();
    }

    private void TryCompleteJoin()
    {
        if (State != BotState.Connecting || !_welcomed || !_snapshotApplied) return;
        State = BotState.Joined;
        Stats.Joins++;
        _attemptStart = -1;
        _reconnectAt = -1;
        _joinedAt = UpdateCount;
        _driver.Reset();
        _churnAt = UpdateCount + _random.Next(Options.ChurnMinUpdates, Math.Max(Options.ChurnMinUpdates, Options.ChurnMaxUpdates));
    }

    // ---- ending a connection ----

    private void BeginLeave(Leave reason)
    {
        if (_leave != Leave.None) return;
        _leave = reason;
        _leaveAt = UpdateCount;
        Transport.Disconnect();
    }

    private void Finish(TransportDisconnect disconnect)
    {
        if (State == BotState.Disconnected) return;

        bool wasJoined = State == BotState.Joined;
        Leave leave = _leave;
        _leave = Leave.None;
        State = BotState.Disconnected;
        _transportConnected = false;

        DisconnectInfo info = default;
        if (disconnect.Data != null && disconnect.Data.Length > 0)
        {
            if (!Codec.TryDecode(disconnect.Data, out info, out _)) info = default;
        }
        else if (disconnect.Kind != TransportDisconnectKind.RemoteClose && disconnect.Kind != TransportDisconnectKind.LocalClose)
        {
            info = new DisconnectInfo(DisconnectReason.None, disconnect.Kind.ToString());
        }

        LastDisconnect = info;
        LastDisconnectKind = disconnect.Kind;
        Stats.AddDisconnect(disconnect.Kind, info.Reason);
        Stats.LastDisconnect = disconnect.Kind + ":" + info.Reason + (string.IsNullOrEmpty(info.Text) ? string.Empty : " " + info.Text);

        PlanNext(info, disconnect.Kind, wasJoined, leave);
    }

    private void PlanNext(DisconnectInfo info, TransportDisconnectKind kind, bool wasJoined, Leave leave)
    {
        if (info.Reason == DisconnectReason.BadToken) _token = SessionToken.None;
        if (BotBehaviourNames.IsAbuse(Options.Behaviour)) return;

        bool final = info.Reason == DisconnectReason.WrongVersion
            || info.Reason == DisconnectReason.BadHello
            || info.Reason == DisconnectReason.InvalidName;
        if (final) return;

        if (leave == Leave.Churn)
        {
            _reconnectAt = UpdateCount + _random.Next(0, Math.Max(0, Options.ChurnReconnectMaxUpdates));
            return;
        }
        if (leave == Leave.Requested) return;

        if (!wasJoined && Options.JoinRetryUpdates > 0 && IsRetryable(info.Reason, kind) && UpdateCount - _attemptStart < Options.JoinRetryUpdates)
        {
            ScheduleRetry();
            return;
        }
        if (Options.ReconnectOnDisconnect)
        {
            _attemptStart = UpdateCount;
            ScheduleRetry();
        }
    }

    private void ScheduleRetry()
    {
        int delay = Math.Max(0, Options.RetryDelayUpdates);
        _reconnectAt = UpdateCount + delay + _random.Next(0, delay);
    }

    private static bool IsRetryable(DisconnectReason reason, TransportDisconnectKind kind)
    {
        switch (reason)
        {
            case DisconnectReason.None:
                return kind != TransportDisconnectKind.LocalClose;
            case DisconnectReason.ServerFull:
            case DisconnectReason.NotReady:
            case DisconnectReason.NameInUse:
            case DisconnectReason.BadToken:
                return true;
            default:
                return false;
        }
    }

    // The tour reads faces, doors and turf types; every other behaviour only solidity.
    private ISolidity MakeSolidity()
    {
        if (_effective == BotBehaviour.Tour)
        {
            _routeMap = new ReplicaRouteMap(Replica);
            return _routeMap;
        }

        return new ReplicaSolidity(Replica);
    }

    /// <summary>The behaviour the driver runs: Churn walks like Wander, the abusive ones have no driver of their own.</summary>
    private static BotBehaviour DriverBehaviour(BotBehaviour behaviour)
    {
        switch (behaviour)
        {
            case BotBehaviour.Rest:
            case BotBehaviour.Shove:
            case BotBehaviour.Tour:
                return behaviour;
            default:
                return BotBehaviour.Wander; // Fiddle walks like Wander
        }
    }
}
