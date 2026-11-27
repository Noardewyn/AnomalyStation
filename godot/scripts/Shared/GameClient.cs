using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// The real feed: one connection to the server through an <see cref="IClientTransport"/> (LiteNetLib in the game, the
/// loopback in tests), the wire protocol (<see cref="Codec"/>) and the client replica (<see cref="ReplicaWorld"/>). No
/// engine API: the Godot scenes poll it from <c>_Process</c>.
/// <para>
/// Flow (protocol.md, scope 2e): <see cref="Connect"/> sends the <see cref="Hello"/> in the transport's connection request
/// and returns at once; the transport raises <c>Connected</c> or <c>Disconnected</c> (a reject with its
/// <see cref="DisconnectInfo"/>, a timeout, a failed connect) inside <see cref="Poll"/>; then the State channel delivers
/// Welcome, MapGrid, Snapshot, and a Delta per tick, which go to <see cref="ReplicaWorld"/>; the feed is
/// <see cref="FeedPhase.Playing"/> once the Snapshot is in. Any protocol error or desync ends the connection with a
/// reason (the client does not try to repair: it leaves, and the connect screen shows why).
/// </para>
/// </summary>
public sealed class GameClient : IWorldFeed, IDisposable
{
    private readonly IClientTransport _transport;
    private ReplicaWorld _replica; // D306: a new one for each round's world
    private readonly string _host;
    private readonly int _port;
    private readonly string _name;
    private SessionToken _token;
    private readonly string _loginToken;
    private readonly bool _waitsForLoad;

    /// <summary>Version 97 (DV597): the Hello said this client loads the world first; its game scene sends Loaded when it has.</summary>
    public bool WaitsForLoad => _waitsForLoad;
    private DisconnectReason _closeCode;
    private Welcome? _welcome;
    private FeedPhase _phase = FeedPhase.Idle;
    private string _closeReason;
    private bool _rejected;
    private uint _seq;
    private long _deltas;
    private bool _disposed;

    /// <param name="transport">The transport (a <c>LiteNetClientTransport</c> in normal mode in the game).</param>
    /// <param name="host">The server host (already an address the transport can use).</param>
    /// <param name="port">The server port.</param>
    /// <param name="name">The player name (the server's BYOND key).</param>
    /// <param name="token">An earlier session's token to take it back, or none.</param>
    /// <param name="loginToken">D335: the hub's game token the launcher started the client with, or null: sent in the Hello, where a server that takes the launcher's login reads the player's name.</param>
    /// <param name="waitsForLoad">Version 97 (DV597): the game's client loads the world before it plays and sends <see cref="ActionKind.Loaded"/> then.</param>
    public GameClient(IClientTransport transport, string host, int port, string name, SessionToken token = default, string loginToken = null, bool waitsForLoad = false)
    {
        _waitsForLoad = waitsForLoad;
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _host = host;
        _port = port;
        _name = name;
        _token = token;
        _loginToken = string.IsNullOrEmpty(loginToken) ? null : loginToken;
        _transport.Connected += OnConnected;
        _transport.Received += OnReceived;
        _transport.Disconnected += OnDisconnected;
        NewReplica();
    }

    private void NewReplica()
    {
        _replica = new ReplicaWorld();
        _minimaps.Clear(); // D306: the next round's levels send their own
        MinimapMobs = null;
        Weather = null;
        BuildPreview = BuildPreviewMessage.None; // version 70: the next join sends its own
        GunSight = GunSightMessage.None; // version 92: and its own gun sight, whose hits are not new ones
        _gunSightSeen = false;
        _replica.Spawned += e => Spawned?.Invoke(e);
        _replica.Despawned += e => Despawned?.Invoke(e);
    }

    /// <summary>D306: the server's status line while it builds the next round's world (<see cref="FeedPhase.BetweenRounds"/>), else null.</summary>
    public string BetweenRoundsStatus { get; private set; }

    /// <summary>D306: tg's sound path of the title track to play between rounds (the ended round's), or null.</summary>
    public string LobbyMusic { get; private set; }

    /// <summary>D322: whether the server lets this player open the dev menu; null until it has said so.</summary>
    public bool? DevAccessAllowed { get; private set; }

    public FeedPhase Phase => _phase;

    public string CloseReason => _closeReason;

    public bool Rejected => _rejected;

    /// <summary>M3 R4: the server's reason when the connection ended with one (a refusal, a kick, a replacement), else None.</summary>
    public DisconnectReason CloseCode => _closeCode;

    public string Description => $"{_host}:{_port} as {_name}";

    /// <summary>The session's ordinal from <see cref="Protocol.Welcome"/> (0 before it).</summary>
    public int Session => _welcome?.Session ?? 0;

    /// <summary>The reconnect token (the server's, once Welcome arrived; else the one given).</summary>
    public SessionToken Token => _token;

    public uint LastSentSeq => _seq;

    public uint LastAppliedSeq => _replica.HasSnapshot ? _replica.LastInputSeq : 0;

    public MapGrid Map => _replica.Map;

    public long Tick => _replica.HasSnapshot ? _replica.Tick : -1;

    public long DeltaCount => _deltas;

    public long OwnEntity => _replica.HasSnapshot ? _replica.OwnEntity : 0;

    public int Count => _replica.Count;

    public IEnumerable<ReplicaEntity> Entities => _replica.Entities;

    public int RoundTripMs => _transport.RoundTripMs;

    public long BytesSent => _transport.BytesSent;

    public long BytesReceived => _transport.BytesReceived;

    public event Action<ReplicaEntity> Spawned;

    public event Action<ReplicaEntity> Despawned;

    public event Action<ServerText> ServerTextReceived;

    /// <summary>M4a: tg's screentip for the hovered target (<see cref="MessageId.ContextHints"/> on the Control channel, the answer to a Hover).</summary>
    public event Action<ContextHintsMessage> ContextHintsReceived;

    /// <summary>M4b slice 3: the air around a point (<see cref="MessageId.DevGasReport"/> on the Control channel, the answer to a DevGasProbe).</summary>
    public event Action<DevGasReport> DevGasReportReceived;

    /// <summary>Version 38: the say window's messages from the server (<see cref="MessageId.SayModal"/>).</summary>
    public event Action<SayModalMessage> SayModalReceived;

    /// <summary>Version 40: the radio prefixes of the worn headset's channels changed (<see cref="MessageId.RadioChannels"/>, the replica's RadioChannelsChanged).</summary>
    public event Action<IReadOnlyList<string>> RadioChannelsReceived
    {
        add => _replica.RadioChannelsChanged += value;
        remove => _replica.RadioChannelsChanged -= value;
    }

    /// <summary>Version 38 (DV160): a runechat line (<see cref="MessageId.Runechat"/>).</summary>
    public event Action<RunechatMessage> RunechatReceived;

    /// <summary>Version 63 (D307): a line to voice (<see cref="MessageId.Tts"/>).</summary>
    public event Action<TtsMessage> TtsReceived;

    private readonly Dictionary<int, MinimapMessage> _minimaps = new();

    /// <summary>Version 67 (D315): the levels' minimaps of this join (<see cref="MessageId.Minimap"/>, one per level the own mob stood on).</summary>
    public IReadOnlyDictionary<int, MinimapMessage> Minimaps => _minimaps;

    /// <inheritdoc/>
    public MinimapMessage MinimapOf(int z) => _minimaps.GetValueOrDefault(z);

    /// <inheritdoc/>
    public MinimapMobsMessage MinimapMobs { get; private set; }

    /// <summary>Version 84 (gaps E2a): the weather of the own mob's level (<see cref="MessageId.Weather"/>), null before the first.</summary>
    public WeatherMessage Weather { get; private set; }

    /// <inheritdoc/>
    public BuildPreviewMessage BuildPreview { get; private set; } = BuildPreviewMessage.None;

    /// <inheritdoc/>
    public GunSightMessage GunSight { get; private set; } = GunSightMessage.None;

    /// <inheritdoc/>
    public int GunSightHits { get; private set; }

    private bool _gunSightSeen;

    /// <inheritdoc/>
    public HotbarMessage Hotbar => _replica.Hotbar;

    /// <inheritdoc/>
    public int HotbarRefusals => _replica.HotbarRefusals;

    /// <inheritdoc/>
    public int HotbarRefusedCell => _replica.HotbarRefusedCell;

    /// <summary>How many connection attempts a connect makes before it gives up (LiteNetLib's <c>MaxConnectAttempts</c>).</summary>
    public const int ConnectAttempts = 6;

    /// <summary>The wait between two attempts, in milliseconds (LiteNetLib's <c>ReconnectDelay</c>): a connect to nobody fails after about <c>ConnectAttempts * ConnectRetryMs</c>, 1.2 s, instead of LiteNetLib's 5 s (10 x 500 ms).</summary>
    public const int ConnectRetryMs = 200;

    /// <summary>
    /// The transport of the Godot client: LiteNetLib in normal mode (its own threads move bytes; events come only in
    /// <see cref="Poll"/>), with a connect that fails fast.
    /// </summary>
    public static LiteNetClientTransport CreateTransport() => new(manualMode: false)
    {
        MaxConnectAttempts = ConnectAttempts,
        ReconnectDelayMs = ConnectRetryMs,
    };

    /// <summary>Starts connecting (returns at once; progress and failure come through <see cref="Poll"/>).</summary>
    public void Connect()
    {
        if (_phase != FeedPhase.Idle)
            throw new InvalidOperationException("already started");
        _phase = FeedPhase.Connecting;
        try
        {
            byte[] hello = Codec.Encode(new Hello(ProtocolVersion.Current, _name, _token, _loginToken, _waitsForLoad));
            _transport.Connect(_host, _port, hello);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Close($"could not start the connection: {ex.Message}", false);
        }
    }

    public void Poll(double nowSeconds)
    {
        if (_phase == FeedPhase.Idle || _phase == FeedPhase.Closed || _disposed)
            return;
        try
        {
            _transport.Poll();
        }
        catch (ProtocolException ex)
        {
            Fail($"protocol error: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A transport that fails is a closed connection, not a frame that dies.
            Fail($"network error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public ReplicaWorld Replica => _replica;

    public void SendMotion(MotionInputMessage frames)
    {
        if (_phase != FeedPhase.Playing || frames == null || frames.Frames.Count == 0)
            return;
        Span<byte> buffer = stackalloc byte[Limits.MaxMotionInputMessageBytes];
        int length = Codec.Encode(frames, buffer);
        // The Input channel is unreliable and carries the newest frames of every tick (scope 2d).
        _transport.Send(Channel.Input, buffer.Slice(0, length));
        _seq = frames.Frames[frames.Frames.Count - 1].Seq;
    }

    public void SendAction(in ActionMessage action)
    {
        if (_phase != FeedPhase.Playing)
            return;
        Span<byte> buffer = stackalloc byte[action.Kind == ActionKind.Say ? Limits.MaxSayActionBytes : Limits.MaxActionMessageBytes]; // version 38: a say entry is longer
        int length = Codec.Encode(action, buffer);
        _transport.Send(Channel.Control, buffer.Slice(0, length));
    }

    public void SendBugReport(BugReportMessage report)
    {
        if (_phase != FeedPhase.Playing)
            return;
        // version 53: in parts the server takes (8 fragments at most); the client sends nothing else on State, so a screenshot
        // holds back no command on Control
        foreach (byte[] part in Codec.EncodeBugReportParts(report))
            _transport.Send(Channel.State, part);
    }

    public void Disconnect()
    {
        if (_phase == FeedPhase.Closed || _phase == FeedPhase.Idle)
            return;
        try
        {
            _transport.Disconnect();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Already gone.
        }
        Close("you disconnected", false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _transport.Connected -= OnConnected;
        _transport.Received -= OnReceived;
        _transport.Disconnected -= OnDisconnected;
        try
        {
            _transport.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Disposing never fails the caller: the connection is gone either way.
        }
    }

    private void OnConnected()
    {
        if (_phase == FeedPhase.Connecting)
            _phase = FeedPhase.Joining;
    }

    private void OnReceived(Channel channel, ReadOnlySpan<byte> message)
    {
        if (_phase == FeedPhase.Closed)
            return;
        // A message proves the connection is up, whatever order the transport raised its events in.
        if (_phase == FeedPhase.Connecting)
            _phase = FeedPhase.Joining;
        if (!Codec.TryPeekId(message, out MessageId id))
        {
            Fail("protocol error: a message with no valid id");
            return;
        }
        ProtocolError error;
        switch (id)
        {
            case MessageId.Welcome:
                if (Codec.TryDecode(message, out Welcome welcome, out error))
                {
                    if (_phase == FeedPhase.BetweenRounds)
                    {
                        // D306: the next round's world: a fresh replica for its join sequence
                        NewReplica();
                        BetweenRoundsStatus = null;
                        _phase = FeedPhase.Joining;
                    }
                    _welcome = welcome;
                    _token = welcome.Token;
                }
                else
                    Fail($"protocol error: bad Welcome ({error})");
                break;

            case MessageId.MapGrid:
            case MessageId.Snapshot:
            case MessageId.Delta:
            case MessageId.Airflow:
            case MessageId.Sounds: // version 13: the replica raises SoundsReceived (the audio layer plays them, slice 3)
            case MessageId.Radial: // version 15: the replica keeps the radial menus and raises RadialChanged (Hud/Radial.cs draws them)
            case MessageId.UiWindow: // version 16: the replica keeps the tgui windows and raises UiWindowChanged (Hud/Tgui draws them)
            case MessageId.Tooltip: // version 19: the replica keeps the tooltip and raises TooltipChanged (Hud/TooltipBox.cs draws it)
            case MessageId.CameraShake: // version 21: the replica raises CameraShakeReceived (the play session's camera follows the path)
            case MessageId.RadioChannels: // version 40: the replica keeps the prefixes and raises RadioChannelsChanged (RadioChannelsReceived)
            case MessageId.Motion: // version 42: the capsule mobs' positions on the unreliable channel
            case MessageId.HeldMotion: // version 54: the replica raises HeldMotionReceived (the first-person gun moves, Hud/ViewModel)
            case MessageId.Hotbar: // version 99 also (DV689): the replica keeps the own hotbar and counts its refusals
                if (_phase == FeedPhase.BetweenRounds)
                    break; // D306: the old world's last messages
                try
                {
                    if (!_replica.TryApply(message, out _, out error))
                    {
                        Fail($"protocol error: bad {id} ({error})");
                        break;
                    }
                }
                catch (ProtocolException ex)
                {
                    Fail($"out of sync with the server: {ex.Message}");
                    break;
                }
                if (id == MessageId.Delta)
                    _deltas++;
                if (_phase == FeedPhase.Joining && _replica.HasSnapshot && _replica.Map != null)
                    _phase = FeedPhase.Playing;
                break;

            case MessageId.ServerText:
                if (Codec.TryDecode(message, out ServerText text, out error))
                {
                    if (text.Type == BetweenRounds.Type)
                    {
                        // D306: the round ended; the connection stays while the server builds the next world
                        _phase = FeedPhase.BetweenRounds;
                        BetweenRoundsStatus = text.Html ?? "";
                        LobbyMusic = null;
                    }
                    else if (text.Type == BetweenRounds.MusicType)
                        LobbyMusic = text.Html;
                    else if (text.Type == DevAccess.Type)
                        DevAccessAllowed = text.Html == DevAccess.Allowed;
                    else
                        ServerTextReceived?.Invoke(text);
                }
                else
                    Fail($"protocol error: bad ServerText ({error})");
                break;

            case MessageId.ContextHints:
                if (Codec.TryDecode(message, out ContextHintsMessage hints, out error))
                    ContextHintsReceived?.Invoke(hints);
                else
                    Fail($"protocol error: bad ContextHints ({error})");
                break;

            case MessageId.DevGasReport:
                if (Codec.TryDecode(message, out DevGasReport gasReport, out error))
                    DevGasReportReceived?.Invoke(gasReport);
                else
                    Fail($"protocol error: bad DevGasReport ({error})");
                break;

            case MessageId.SayModal:
                if (Codec.TryDecode(message, out SayModalMessage sayModal, out error))
                    SayModalReceived?.Invoke(sayModal);
                else
                    Fail($"protocol error: bad SayModal ({error})");
                break;

            case MessageId.Runechat:
                if (Codec.TryDecode(message, out RunechatMessage runechat, out error))
                    RunechatReceived?.Invoke(runechat);
                else
                    Fail($"protocol error: bad Runechat ({error})");
                break;

            case MessageId.Tts:
                if (Codec.TryDecode(message, out TtsMessage tts, out error))
                    TtsReceived?.Invoke(tts);
                else
                    Fail($"protocol error: bad Tts ({error})");
                break;

            case MessageId.Minimap:
                if (Codec.TryDecode(message, out MinimapMessage minimap, out error))
                    _minimaps[minimap.Z] = minimap;
                else
                    Fail($"protocol error: bad Minimap ({error})");
                break;

            case MessageId.Weather:
                if (Codec.TryDecode(message, out WeatherMessage weather, out error))
                    Weather = weather;
                else
                    Fail($"protocol error: bad Weather ({error})");
                break;

            case MessageId.MinimapMobs:
                if (Codec.TryDecode(message, out MinimapMobsMessage mobs, out error))
                    MinimapMobs = mobs;
                else
                    Fail($"protocol error: bad MinimapMobs ({error})");
                break;

            case MessageId.RuntimeResource: // version 79 (silicons SI4d): an image the world made, before the messages that name it
                if (Codec.TryDecode(message, out RuntimeResourceMessage runtime, out error))
                    Anomaly.Client.Shared.Icons.RuntimeImages.Add(runtime.Path, runtime.Png);
                else
                    Fail($"protocol error: bad RuntimeResource ({error})");
                break;

            case MessageId.GunSight: // version 92 (DV96): the own gun's cone and the own shots' hits, for the crosshair
                if (Codec.TryDecode(message, out GunSightMessage sight, out error))
                {
                    if (_gunSightSeen && sight.Hits != GunSight.Hits)
                        GunSightHits++;
                    GunSight = sight;
                    _gunSightSeen = true;
                }
                else
                    Fail($"protocol error: bad GunSight ({error})");
                break;

            case MessageId.BuildPreview: // version 70 (D320): what the held tool would build, for the ghost on the aimed tile
                if (Codec.TryDecode(message, out BuildPreviewMessage preview, out error))
                    BuildPreview = preview;
                else
                    Fail($"protocol error: bad BuildPreview ({error})");
                break;

            default:
                Fail($"protocol error: unexpected {id} from the server");
                break;
        }
    }

    private void OnDisconnected(TransportDisconnect disconnect)
    {
        if (_phase == FeedPhase.Closed)
            return;
        string detail = null;
        DisconnectReason reason = DisconnectReason.None;
        if (disconnect.Data != null && disconnect.Data.Length > 0
            && Codec.TryDecode(disconnect.Data, out DisconnectInfo info, out _))
        {
            reason = info.Reason;
            detail = string.IsNullOrWhiteSpace(info.Text) ? Describe(info.Reason) : $"{Describe(info.Reason)} ({info.Text})";
        }
        _closeCode = reason;
        switch (disconnect.Kind)
        {
            case TransportDisconnectKind.Rejected:
                Close("rejected by the server: " + (detail ?? "no reason given"), true);
                break;
            case TransportDisconnectKind.ConnectFailed:
                Close($"could not connect to {_host}:{_port}", false);
                break;
            case TransportDisconnectKind.Timeout:
                Close(_phase == FeedPhase.Connecting ? $"no answer from {_host}:{_port} (timed out)" : "the connection timed out", false);
                break;
            case TransportDisconnectKind.LocalClose:
                Close("you disconnected", false);
                break;
            default:
                // A drop by the server: a reject while connecting, else a close with its reason (a kick, a shutdown).
                Close(_phase == FeedPhase.Connecting && detail != null
                    ? "rejected by the server: " + detail
                    : "disconnected by the server: " + (detail ?? "the server closed the connection"),
                    _phase == FeedPhase.Connecting && reason != DisconnectReason.None);
                break;
        }
    }

    private void Fail(string reason)
    {
        if (_phase == FeedPhase.Closed)
            return;
        try
        {
            _transport.Disconnect();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Closing a connection that is already gone: nothing more to do.
        }
        Close(reason, false);
    }

    private void Close(string reason, bool rejected)
    {
        if (_phase == FeedPhase.Closed)
            return;
        _phase = FeedPhase.Closed;
        _closeReason = reason;
        _rejected = rejected;
    }

    /// <summary>The words for a <see cref="DisconnectReason"/>.</summary>
    public static string Describe(DisconnectReason reason) => reason switch
    {
        DisconnectReason.None => "no reason given",
        DisconnectReason.ServerFull => "the server is full",
        DisconnectReason.WrongVersion => "the client and the server speak different protocol versions",
        DisconnectReason.BadHello => "the server did not understand the connection request",
        DisconnectReason.InvalidName => "the name is not allowed",
        DisconnectReason.NameInUse => "that name is in use",
        DisconnectReason.BadToken => "the reconnect token is not valid",
        DisconnectReason.LoginRequired => "this server takes players through the launcher",
        DisconnectReason.NotReady => "the server is still starting",
        DisconnectReason.Kicked => "you were kicked",
        DisconnectReason.Flooding => "too many messages",
        DisconnectReason.ProtocolViolation => "protocol violations",
        DisconnectReason.ServerShutdown => "the server is shutting down",
        DisconnectReason.Replaced => "you connected again from elsewhere",
        DisconnectReason.Refused => "the game refused the connection",
        DisconnectReason.ClientDeleted => "the game removed your client",
        DisconnectReason.RoundRestart => "the round has ended; the server is starting the next one",
        DisconnectReason.Banned => "you are banned from this server",
        _ => reason.ToString(),
    };
}
