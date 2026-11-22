using System.Buffers.Binary;
using System.IO;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Server;

// Small stand-ins for the packages that run beside the host (docs/ai/specs/m1-ownership.md, m2-ownership.md): the codec (M1 P2,
// M2 P5), the transport, the replicator (M1 P4, M2 P5), the client layer's entry points (M1 P1) and the locomotion seam (M2
// P4). They follow the contracts' documented meaning closely enough for the host's logic; the lead's end-to-end tests run the
// real ones after the merges.

/// <summary>A codec with a layout of its own (not P2's), obeying Codec's contract: TryDecode never throws and enforces the caps.</summary>
internal sealed class FakeCodec : IServerCodec
{
    public int MaxDeltaHeaderBytes => 1 + 8 + 4 + 8 + 2;

    public bool TryPeekId(ReadOnlySpan<byte> message, out MessageId id)
    {
        id = MessageId.None;
        if (message.IsEmpty || message[0] == 0 || message[0] > (byte)MessageId.Action) return false;
        id = (MessageId)message[0];
        return true;
    }

    public static byte[] EncodeHello(string name, SessionToken token = default, ushort protocol = ProtocolVersion.Current)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(name ?? "");
        var buffer = new byte[1 + 4 + 2 + 1 + nameBytes.Length + 16];
        buffer[0] = (byte)MessageId.Hello;
        "ANOM"u8.CopyTo(buffer.AsSpan(1));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(5), protocol);
        buffer[7] = (byte)Math.Min(255, nameBytes.Length);
        nameBytes.AsSpan(0, Math.Min(255, nameBytes.Length)).CopyTo(buffer.AsSpan(8));
        int at = 8 + Math.Min(255, nameBytes.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(at), token.Low);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(at + 8), token.High);
        return buffer.AsSpan(0, at + 16).ToArray();
    }

    public bool TryDecode(ReadOnlySpan<byte> message, out Hello value, out ProtocolError error)
    {
        value = default;
        error = ProtocolError.None;
        if (message.Length < 8) { error = ProtocolError.Truncated; return false; }
        if (message[0] != (byte)MessageId.Hello) { error = TryPeekId(message, out _) ? ProtocolError.WrongMessage : ProtocolError.UnknownMessage; return false; }
        if (!message.Slice(1, 4).SequenceEqual("ANOM"u8)) { error = ProtocolError.BadMagic; return false; }
        ushort protocol = BinaryPrimitives.ReadUInt16LittleEndian(message.Slice(5));
        int nameLength = message[7];
        if (nameLength > Limits.MaxNameBytes) { error = ProtocolError.BadString; return false; }
        if (message.Length < 8 + nameLength + 16) { error = ProtocolError.Truncated; return false; }
        if (message.Length > 8 + nameLength + 16) { error = ProtocolError.TrailingBytes; return false; }
        string name;
        try { name = new UTF8Encoding(false, true).GetString(message.Slice(8, nameLength)); }
        catch (DecoderFallbackException) { error = ProtocolError.BadString; return false; }
        var token = new SessionToken(BinaryPrimitives.ReadUInt64LittleEndian(message.Slice(8 + nameLength)),
            BinaryPrimitives.ReadUInt64LittleEndian(message.Slice(16 + nameLength)));
        value = new Hello(protocol, name, token);
        return true;
    }

    public static byte[] EncodeInput(uint seq, HeldDirs dirs)
    {
        var buffer = new byte[6];
        buffer[0] = (byte)MessageId.InputFrame;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(1), seq);
        buffer[5] = (byte)dirs;
        return buffer;
    }

    // ---- version 2: a layout of this fake's own (not P5's), obeying Codec's contract: TryDecode never throws and enforces the caps ----

    /// <summary>A MotionInput message: <c>[9][count u8]</c> and per frame <c>seq u32, moveX i8, moveY i8, yaw u16, flags u8</c>.</summary>
    public static byte[] EncodeMotion(params MotionInput[] frames)
    {
        var buffer = new byte[2 + frames.Length * 9];
        buffer[0] = (byte)MessageId.MotionInput;
        buffer[1] = (byte)frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            Span<byte> at = buffer.AsSpan(2 + i * 9);
            BinaryPrimitives.WriteUInt32LittleEndian(at, frames[i].Seq);
            at[4] = (byte)frames[i].MoveX;
            at[5] = (byte)frames[i].MoveY;
            BinaryPrimitives.WriteUInt16LittleEndian(at.Slice(6), frames[i].Yaw);
            at[8] = (byte)frames[i].Flags;
        }
        return buffer;
    }

    /// <summary>One frame or several with consecutive seqs from <paramref name="firstSeq"/>: a convenience for the tests.</summary>
    public static byte[] EncodeMotion(uint firstSeq, sbyte moveX, sbyte moveY, ushort yaw = 0)
        => EncodeMotion(new MotionInput(firstSeq, moveX, moveY, yaw, MotionInputFlags.None));

    public bool TryDecode(ReadOnlySpan<byte> message, out MotionInputMessage value, out ProtocolError error)
    {
        value = null;
        error = ProtocolError.None;
        if (message.Length < 2) { error = ProtocolError.Truncated; return false; }
        if (message[0] != (byte)MessageId.MotionInput) { error = ProtocolError.WrongMessage; return false; }
        int count = message[1];
        if (count < 1 || count > Limits.MaxInputFramesPerMessage) { error = ProtocolError.BadValue; return false; }
        if (message.Length < 2 + count * 9) { error = ProtocolError.Truncated; return false; }
        if (message.Length > 2 + count * 9) { error = ProtocolError.TrailingBytes; return false; }
        var decoded = new MotionInputMessage();
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> at = message.Slice(2 + i * 9);
            if (at[8] != 0) { error = ProtocolError.BadValue; return false; }
            decoded.Frames.Add(new MotionInput(BinaryPrimitives.ReadUInt32LittleEndian(at), (sbyte)at[4], (sbyte)at[5], BinaryPrimitives.ReadUInt16LittleEndian(at.Slice(6)), (MotionInputFlags)at[8]));
        }
        value = decoded;
        return true;
    }

    /// <summary>
    /// An Action message in the fake's own layout (the host tests need values the real codec refuses):
    /// <c>[10][frameSeq u32][kind u8][target i64][button u8][modifiers u8][targetKind u8][turfX i32][turfY i32][turfZ i32][iconX u8][iconY u8][keybinding u8][keyUp u8][text len u16][radial menu i64][radial element i32][over kind u8][over target i64][over x, y, z i32][drag time u8][text][version 16's UI tail]</c>.
    /// </summary>
    public static byte[] EncodeAction(in ActionMessage action)
    {
        byte[] text = Encoding.UTF8.GetBytes(action.Text ?? "");
        // Version 16: a UiAct or UiClose has a tail after the text: [window i32][action len u16][action][params JSON len u16][params JSON].
        byte[] tail = Array.Empty<byte>();
        if (action.Kind is ActionKind.UiAct or ActionKind.UiClose || action.UiWindow != 0)
        {
            byte[] act = Encoding.UTF8.GetBytes(action.UiAction ?? "");
            byte[] json = Encoding.UTF8.GetBytes(action.UiParams?.ToJson() ?? "");
            tail = new byte[4 + 2 + act.Length + 2 + json.Length];
            BinaryPrimitives.WriteInt32LittleEndian(tail, action.UiWindow);
            BinaryPrimitives.WriteUInt16LittleEndian(tail.AsSpan(4), (ushort)act.Length);
            act.CopyTo(tail, 6);
            BinaryPrimitives.WriteUInt16LittleEndian(tail.AsSpan(6 + act.Length), (ushort)json.Length);
            json.CopyTo(tail, 8 + act.Length);
        }
        var buffer = new byte[ActionFixedBytes + text.Length + tail.Length];
        buffer[0] = (byte)MessageId.Action;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(1), action.FrameSeq);
        buffer[5] = (byte)action.Kind;
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(6), action.Target);
        buffer[14] = (byte)action.Button;
        buffer[15] = (byte)action.Modifiers;
        buffer[16] = (byte)action.TargetKind;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(17), action.TurfX);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(21), action.TurfY);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(25), action.TurfZ);
        buffer[29] = action.IconX;
        buffer[30] = action.IconY;
        buffer[31] = (byte)action.Keybinding;
        buffer[32] = action.KeyUp ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(33), (ushort)text.Length);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(35), action.RadialMenu); // version 15
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(43), action.RadialElement);
        buffer[47] = (byte)action.OverKind; // version 17
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(48), action.OverTarget);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(56), action.OverX);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(60), action.OverY);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(64), action.OverZ);
        buffer[68] = action.DragTime;
        text.CopyTo(buffer, ActionFixedBytes);
        tail.CopyTo(buffer, ActionFixedBytes + text.Length);
        return buffer;
    }

    private const int ActionFixedBytes = 69;

    public static byte[] EncodeAction(uint frameSeq, ActionKind kind, long target = 0, ClickButton button = ClickButton.None,
        ClickModifiers modifiers = ClickModifiers.None, string text = null)
        => EncodeAction(new ActionMessage(frameSeq, kind, target, button, modifiers, text));

    public bool TryDecode(ReadOnlySpan<byte> message, out ActionMessage value, out ProtocolError error)
    {
        value = default;
        error = ProtocolError.None;
        if (message.Length < ActionFixedBytes) { error = ProtocolError.Truncated; return false; }
        if (message[0] != (byte)MessageId.Action) { error = ProtocolError.WrongMessage; return false; }
        int textLength = BinaryPrimitives.ReadUInt16LittleEndian(message.Slice(33));
        if (textLength > Limits.MaxDevCommandBytes) { error = ProtocolError.BadString; return false; }
        if (message.Length < ActionFixedBytes + textLength) { error = ProtocolError.Truncated; return false; }
        int uiWindow = 0;
        string uiAction = null;
        UiValue uiParams = null;
        if (message.Length > ActionFixedBytes + textLength)
        {
            ReadOnlySpan<byte> tail = message.Slice(ActionFixedBytes + textLength);
            if (tail.Length < 8) { error = ProtocolError.Truncated; return false; }
            uiWindow = BinaryPrimitives.ReadInt32LittleEndian(tail);
            int actLength = BinaryPrimitives.ReadUInt16LittleEndian(tail.Slice(4));
            if (tail.Length < 8 + actLength) { error = ProtocolError.Truncated; return false; }
            uiAction = actLength == 0 ? null : Encoding.UTF8.GetString(tail.Slice(6, actLength));
            int jsonLength = BinaryPrimitives.ReadUInt16LittleEndian(tail.Slice(6 + actLength));
            if (tail.Length != 8 + actLength + jsonLength) { error = ProtocolError.TrailingBytes; return false; }
            if (jsonLength > 0) uiParams = UiValue.FromJson(Encoding.UTF8.GetString(tail.Slice(8 + actLength, jsonLength)));
        }
        string text = null;
        if (textLength > 0)
        {
            try { text = new UTF8Encoding(false, true).GetString(message.Slice(ActionFixedBytes, textLength)); }
            catch (DecoderFallbackException) { error = ProtocolError.BadString; return false; }
        }
        value = new ActionMessage(BinaryPrimitives.ReadUInt32LittleEndian(message.Slice(1)), (ActionKind)message[5],
            BinaryPrimitives.ReadInt64LittleEndian(message.Slice(6)), (ClickButton)message[14], (ClickModifiers)message[15], text)
        {
            TargetKind = (TargetKind)message[16],
            TurfX = BinaryPrimitives.ReadInt32LittleEndian(message.Slice(17)),
            TurfY = BinaryPrimitives.ReadInt32LittleEndian(message.Slice(21)),
            TurfZ = BinaryPrimitives.ReadInt32LittleEndian(message.Slice(25)),
            IconX = message[29],
            IconY = message[30],
            Keybinding = (KeybindingCode)message[31],
            KeyUp = message[32] != 0,
            RadialMenu = BinaryPrimitives.ReadInt64LittleEndian(message.Slice(35)),
            RadialElement = BinaryPrimitives.ReadInt32LittleEndian(message.Slice(43)),
            OverKind = (TargetKind)message[47],
            OverTarget = BinaryPrimitives.ReadInt64LittleEndian(message.Slice(48)),
            OverX = BinaryPrimitives.ReadInt32LittleEndian(message.Slice(56)),
            OverY = BinaryPrimitives.ReadInt32LittleEndian(message.Slice(60)),
            OverZ = BinaryPrimitives.ReadInt32LittleEndian(message.Slice(64)),
            DragTime = message[68],
            UiWindow = uiWindow,
            UiAction = uiAction,
            UiParams = uiParams,
        };
        return true;
    }

    public int EncodeDelta(long tick, uint lastInputSeq, long ownEntity, ReadOnlySpan<byte> body, ReadOnlySpan<byte> ownBlock, Span<byte> destination)
    {
        destination[0] = (byte)MessageId.Delta;
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(1), tick);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(9), lastInputSeq);
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(13), ownEntity);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(21), (ushort)ownBlock.Length);
        ownBlock.CopyTo(destination.Slice(23));
        body.CopyTo(destination.Slice(23 + ownBlock.Length));
        return 23 + ownBlock.Length + body.Length;
    }

    public bool TryDecode(ReadOnlySpan<byte> message, out InputFrame value, out ProtocolError error)
    {
        value = default;
        error = ProtocolError.None;
        if (message.Length < 6) { error = ProtocolError.Truncated; return false; }
        if (message[0] != (byte)MessageId.InputFrame) { error = ProtocolError.WrongMessage; return false; }
        if (message.Length > 6) { error = ProtocolError.TrailingBytes; return false; }
        if ((message[5] & ~(byte)HeldDirs.All) != 0) { error = ProtocolError.BadValue; return false; }
        value = new InputFrame(BinaryPrimitives.ReadUInt32LittleEndian(message.Slice(1)), (HeldDirs)message[5]);
        return true;
    }

    public byte[] Encode(in Welcome message)
    {
        var buffer = new byte[1 + 4 + 16 + 8 + 1 + 8];
        buffer[0] = (byte)MessageId.Welcome;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(1), message.Session);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(5), message.Token.Low);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(13), message.Token.High);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(21), message.Tick);
        buffer[29] = message.TickRate;
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(30), message.OwnEntity);
        return buffer;
    }

    public static Welcome DecodeWelcome(byte[] m)
    {
        Assert.Equal((byte)MessageId.Welcome, m[0]);
        return new Welcome(BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(1)),
            new SessionToken(BinaryPrimitives.ReadUInt64LittleEndian(m.AsSpan(5)), BinaryPrimitives.ReadUInt64LittleEndian(m.AsSpan(13))),
            BinaryPrimitives.ReadInt64LittleEndian(m.AsSpan(21)), m[29], BinaryPrimitives.ReadInt64LittleEndian(m.AsSpan(30)));
    }

    public byte[] Encode(in ServerText message)
    {
        byte[] type = Encoding.UTF8.GetBytes(message.Type ?? "");
        byte[] html = Encoding.UTF8.GetBytes(message.Html ?? "");
        Assert.True(type.Length <= Limits.MaxServerTextTypeBytes && html.Length <= Limits.MaxServerTextBytes, "a ServerText over its caps");
        var buffer = new byte[2 + type.Length + html.Length];
        buffer[0] = (byte)MessageId.ServerText;
        buffer[1] = (byte)type.Length;
        type.CopyTo(buffer, 2);
        html.CopyTo(buffer, 2 + type.Length);
        return buffer;
    }

    public static ServerText DecodeServerText(byte[] m)
    {
        Assert.Equal((byte)MessageId.ServerText, m[0]);
        int typeLength = m[1];
        return new ServerText(Encoding.UTF8.GetString(m, 2, typeLength), Encoding.UTF8.GetString(m, 2 + typeLength, m.Length - 2 - typeLength));
    }

    public byte[] Encode(in DisconnectInfo message)
    {
        byte[] text = Encoding.UTF8.GetBytes(message.Text ?? "");
        Assert.True(text.Length <= Limits.MaxDisconnectTextBytes, "a DisconnectInfo text over its cap");
        var buffer = new byte[2 + text.Length];
        buffer[0] = (byte)MessageId.Disconnect;
        buffer[1] = (byte)message.Reason;
        text.CopyTo(buffer, 2);
        return buffer;
    }

    public static DisconnectInfo DecodeDisconnect(byte[] m)
    {
        Assert.Equal((byte)MessageId.Disconnect, m[0]);
        return new DisconnectInfo((DisconnectReason)m[1], Encoding.UTF8.GetString(m, 2, m.Length - 2));
    }

    public int EncodeDelta(long tick, uint lastInputSeq, long ownEntity, ReadOnlySpan<byte> body, Span<byte> destination)
        => EncodeDelta(tick, lastInputSeq, ownEntity, body, ReadOnlySpan<byte>.Empty, destination);

    public static (long Tick, uint LastInputSeq, long OwnEntity, byte[] Body, byte[] OwnBlock) DecodeDelta(byte[] m)
    {
        Assert.Equal((byte)MessageId.Delta, m[0]);
        int ownLength = BinaryPrimitives.ReadUInt16LittleEndian(m.AsSpan(21));
        return (BinaryPrimitives.ReadInt64LittleEndian(m.AsSpan(1)), BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(9)),
            BinaryPrimitives.ReadInt64LittleEndian(m.AsSpan(13)), m.AsSpan(23 + ownLength).ToArray(), m.AsSpan(23, ownLength).ToArray());
    }
}

/// <summary>An in-memory server transport: the test schedules requests, messages and drops; Poll raises them in order.</summary>
internal sealed class FakeTransport : IServerTransport
{
    private readonly Queue<Action> _pending = new();
    private int _nextId = 1;

    public int Port { get; private set; }
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }
    public int Polls { get; private set; }
    public int Flushes { get; private set; }

    public event Action<IConnectionRequest> ConnectionRequested;
    public event Action<IConnection> Connected;
    public event ServerReceivedHandler Received;
    public event Action<IConnection, TransportDisconnect> Disconnected;

    public void Start(string bindAddress, int port)
    {
        Started = true;
        Port = port == 0 ? 50000 : port;
    }

    /// <summary>A client asks to connect with <paramref name="data"/>; answered at the next Poll.</summary>
    public FakeRemote Request(byte[] data)
    {
        var remote = new FakeRemote(this);
        _pending.Enqueue(() =>
        {
            var request = new FakeRequest(this, data, remote);
            ConnectionRequested?.Invoke(request);
            if (!request.Answered) request.Reject(ReadOnlySpan<byte>.Empty);
            if (remote.Connection != null) Connected?.Invoke(remote.Connection);
            if (remote.Connection != null && remote.DropOnAccept)
            {
                remote.Connection.Dead = true;
                remote.Connection.IsConnected = false;
                Disconnected?.Invoke(remote.Connection, new TransportDisconnect(TransportDisconnectKind.RemoteClose, Array.Empty<byte>()));
            }
        });
        return remote;
    }

    internal FakeConnection NewConnection() => new(this, _nextId++);

    internal void Deliver(FakeConnection connection, Channel channel, byte[] message)
        => _pending.Enqueue(() =>
        {
            if (connection.IsConnected) Received?.Invoke(connection, channel, message);
        });

    internal void Drop(FakeConnection connection, TransportDisconnectKind kind)
        => _pending.Enqueue(() =>
        {
            if (connection.Dead) return;
            connection.Dead = true;
            connection.IsConnected = false;
            Disconnected?.Invoke(connection, new TransportDisconnect(kind, Array.Empty<byte>()));
        });

    public void Poll()
    {
        Polls++;
        int n = _pending.Count;
        for (int i = 0; i < n; i++)
            _pending.Dequeue()();
    }

    public void Flush() => Flushes++;

    public void Stop() => Stopped = true;

    public void Dispose()
    {
    }
}

internal sealed class FakeRequest : IConnectionRequest
{
    private readonly FakeTransport _transport;
    private readonly byte[] _data;
    private readonly FakeRemote _remote;

    public FakeRequest(FakeTransport transport, byte[] data, FakeRemote remote)
    {
        _transport = transport;
        _data = data;
        _remote = remote;
    }

    public bool Answered { get; private set; }
    public ReadOnlySpan<byte> Data => _data;
    public string RemoteEndPoint => "127.0.0.1:40000";

    public IConnection Accept()
    {
        Answered = true;
        _remote.Connection = _transport.NewConnection();
        return _remote.Connection;
    }

    public void Reject(ReadOnlySpan<byte> reason)
    {
        Answered = true;
        _remote.RejectData = reason.ToArray();
    }
}

internal sealed class FakeConnection : IConnection
{
    private readonly FakeTransport _transport;

    public FakeConnection(FakeTransport transport, int id)
    {
        _transport = transport;
        Id = id;
    }

    public int Id { get; }
    public string RemoteEndPoint => "127.0.0.1:40000";
    public bool IsConnected { get; set; } = true;
    public bool Dead { get; set; }
    public List<(Channel Channel, byte[] Message)> Sent { get; } = new();
    public byte[] DisconnectData { get; private set; }
    public long BytesSent { get; private set; }
    public long BytesReceived { get; set; }
    public long MessagesSent => Sent.Count;
    public long MessagesReceived { get; set; }
    public int RoundTripMs => 1;

    public void Send(Channel channel, ReadOnlySpan<byte> message)
    {
        if (!IsConnected) return;
        Sent.Add((channel, message.ToArray()));
        BytesSent += message.Length;
    }

    public void Disconnect(ReadOnlySpan<byte> reason)
    {
        if (!IsConnected) return;
        DisconnectData = reason.ToArray();
        IsConnected = false;
        _transport.Drop(this, TransportDisconnectKind.LocalClose);
    }
}

/// <summary>The client's side of a fake connection.</summary>
internal sealed class FakeRemote
{
    private readonly FakeTransport _transport;
    private uint _seq;

    public FakeRemote(FakeTransport transport) => _transport = transport;

    public FakeConnection Connection { get; set; }
    public byte[] RejectData { get; set; }

    /// <summary>The client drops right after it was accepted, in the same poll (before its Join applies).</summary>
    public bool DropOnAccept { get; set; }
    public DisconnectInfo? Rejected => RejectData is { Length: > 0 } ? FakeCodec.DecodeDisconnect(RejectData) : null;
    public DisconnectInfo? Closed => Connection?.DisconnectData is { Length: > 0 } d ? FakeCodec.DecodeDisconnect(d) : null;
    public bool IsConnected => Connection != null && Connection.IsConnected;

    public IEnumerable<byte[]> SentOn(Channel channel) => Connection?.Sent.Where(s => s.Channel == channel).Select(s => s.Message) ?? Enumerable.Empty<byte[]>();

    public IEnumerable<byte[]> Messages(MessageId id) => Connection?.Sent.Select(s => s.Message).Where(m => m[0] == (byte)id) ?? Enumerable.Empty<byte[]>();

    public Welcome? Welcome => Messages(MessageId.Welcome).Select(FakeCodec.DecodeWelcome).Cast<Welcome?>().FirstOrDefault();

    public void Send(byte[] message, Channel channel = Channel.Control)
    {
        Connection.BytesReceived += message.Length;
        _transport.Deliver(Connection, channel, message);
    }

    /// <summary>D331: the next frame follows <paramref name="acknowledged"/>, as the client's prediction does when the server stood in for its seqs.</summary>
    public void SkipTo(uint acknowledged) => _seq = Math.Max(_seq, acknowledged);

    /// <summary>A MotionInput message on the Input channel with one frame of the next seq (the wish vector and yaw); returns the frame.</summary>
    public MotionInput Motion(sbyte moveX, sbyte moveY, ushort yaw = 0)
    {
        var frame = new MotionInput(++_seq, moveX, moveY, yaw, MotionInputFlags.None);
        Send(FakeCodec.EncodeMotion(frame), Channel.Input);
        return frame;
    }

    /// <summary>A MotionInput message repeating the newest <paramref name="count"/> frames (the client's redundancy): the frames of seqs up to the newest sent, oldest first.</summary>
    public void MotionRedundant(IReadOnlyList<MotionInput> history, int count = Limits.MaxInputFramesPerMessage)
        => Send(FakeCodec.EncodeMotion(history.Skip(Math.Max(0, history.Count - count)).ToArray()), Channel.Input);

    /// <summary>An action on the Control channel tagged with the seq of the newest frame sent (or <paramref name="frameSeq"/>).</summary>
    public void Action(ActionKind kind, long target = 0, ClickButton button = ClickButton.None, ClickModifiers modifiers = ClickModifiers.None,
        string text = null, uint? frameSeq = null)
        => Send(FakeCodec.EncodeAction(frameSeq ?? _seq, kind, target, button, modifiers, text));

    /// <summary>A version-4 action (a target kind, a turf, an icon pixel, a keybinding) on the Control channel; the frame is the newest one sent unless the message names one.</summary>
    public void ActionV4(ActionMessage action)
        => Send(FakeCodec.EncodeAction(action.FrameSeq == 0 ? action with { FrameSeq = _seq } : action));

    public uint Seq => _seq;

    /// <summary>The client closes (or its network dies: <see cref="TransportDisconnectKind.Timeout"/>).</summary>
    public void Drop(TransportDisconnectKind kind = TransportDisconnectKind.RemoteClose) => _transport.Drop(Connection, kind);
}

/// <summary>A replicator stand-in: a Delta body per tick (the tick's number) and recognizable join messages.</summary>
internal sealed class FakeReplicator : IReplicator
{
    public World Attached { get; private set; }
    public List<long> DeltaTicks { get; } = new();
    public List<(long Tick, long Own, uint Seq)> Joins { get; } = new();
    public List<int> Forgotten { get; } = new();

    public void Attach(World world) => Attached = world;

    public DeltaFrame BuildDelta(long tick)
    {
        Assert.Null(World.CurrentOrNull); // the contract: outside the world
        DeltaTicks.Add(tick);
        LastStats = new ReplicationStats(tick, 0, 0, 8);
        return new DeltaFrame(tick, BitConverter.GetBytes(tick), 0, 0, 0);
    }

    public JoinMessages BuildJoin(long tick, long ownEntity, uint lastInputSeq)
    {
        Joins.Add((tick, ownEntity, lastInputSeq));
        return new JoinMessages(new byte[] { (byte)MessageId.MapGrid, 1 }, new byte[] { (byte)MessageId.Snapshot, 2 });
    }

    // Version 2: what the host hands over for each session, recorded; the private block is [session ordinal, depth, hasMotion].
    public List<(long Tick, PrivateInputs Inputs)> JoinsV2 { get; } = new();
    public List<(long Tick, PrivateInputs Inputs)> Privates { get; } = new();

    public JoinMessages BuildJoin(long tick, in PrivateInputs inputs)
    {
        JoinsV2.Add((tick, inputs));
        return new JoinMessages(new byte[] { (byte)MessageId.MapGrid, 1 }, new byte[] { (byte)MessageId.Snapshot, 2 });
    }

    public PrivateFrame BuildPrivate(long tick, in PrivateInputs inputs)
    {
        Assert.Null(World.CurrentOrNull); // outside the world, like BuildDelta
        Privates.Add((tick, inputs));
        return new PrivateFrame(new byte[] { (byte)inputs.Session, inputs.InputBufferDepth, (byte)(inputs.HasMotion ? 1 : 0) }, 0, 0);
    }

    public void Forget(int session) => Forgotten.Add(session);

    public ReplicationStats LastStats { get; private set; }
}

/// <summary>
/// A test fixture client (rules 15): a <c>/client</c> made by the fake entry points, whose <c>New</c> does nothing and whose
/// <c>Destroy</c> does what the /client engine part does on deletion: it tells the host (BYOND: deleting a client
/// disconnects the player).
/// </summary>
[DmPath("/client/test_m1_server_client")]
public class client_test_m1_server_client : client
{
    public mob FakeMob;

    public override DmTask<object> New(object TopicData = null) => DmTask<object>.FromResult(null);

    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (FakeMob != null && ReferenceEquals(FakeMob.client, this)) FakeMob.client = null;
        ClientSeam.NotifyClientDeleted(this);
        return Defines.QDEL_HINT_IWILLGC;
    }
}

/// <summary>
/// The client layer's entry points (P1) as a test stand-in: KeyMob keys the mob, Connect makes a fixture client, attaches it
/// to the mob of its key and finishes "Login" after <see cref="LoginSleepTicks"/> (a sleeping client/New), SetHeldDirections
/// records the call and steps the mob (so inputs reach the world's state hash), Disconnect deletes the client.
/// </summary>
internal sealed class FakeClients : IClientEntryPoints
{
    public int LoginSleepTicks { get; set; }
    public bool Refuse { get; set; }
    public bool ThrowOnConnect { get; set; }
    public bool ThrowOnKeyMob { get; set; }
    /// <summary>The next spawn point for a key no mob holds (the host's, in turn); null: such a key fails the attach assert.</summary>
    public Func<SpawnPoint> SpawnAt { get; set; }
    public List<(long Tick, string Key, int Dirs, object Usr)> Held { get; } = new();
    public List<(long Tick, string Key, string Action, object Usr)> Actions { get; } = new();
    public List<(long Tick, string Key, atom Target, string Params, object Usr)> Clicks { get; } = new();
    public List<(long Tick, string Key)> Connects { get; } = new();
    public List<(long Tick, string Key)> Disconnects { get; } = new();
    public List<(long Tick, string Key, long Mob)> Keyed { get; } = new();
    private readonly Dictionary<string, long> _mobOfCkey = new(StringComparer.Ordinal);

    public void KeyMob(mob avatar, string key)
    {
        Assert.NotNull(World.CurrentOrNull);
        if (ThrowOnKeyMob) throw new NotImplementedException("M1 P1");
        avatar.key = key;
        avatar.ckey = Dm.ckey(key);
        _mobOfCkey[avatar.ckey] = avatar.dm_id;
        Keyed.Add((World.Current.Tick, key, avatar.dm_id));
    }

    public ILoginHandle Connect(string key, string address)
    {
        World world = World.Current;
        Connects.Add((world.Tick, key));
        if (ThrowOnConnect) throw new NotImplementedException("M1 P1");
        var player = Dm.@new<client_test_m1_server_client>();
        player.key = key;
        player.ckey = Dm.ckey(key);
        player.view = Dm.world.view; // BYOND's default, as the engine's ClientSeam.Connect sets it (tg's fullscreens read it)
        var login = new Login(player);
        if (Refuse)
        {
            qdel(player);
            login.Done = true;
            return login;
        }
        // BYOND's builtin attaches to the mob whose key matches.
        mob m = _mobOfCkey.TryGetValue(player.ckey, out long id) && world.Objects.TryGet(id, out object o) ? o as mob : null;
        bool made = false;
        if (m == null && SpawnAt != null)
        {
            // M10 slice F: tg's client/New makes a key no mob holds the lobby's new player, which these fakes never run; a new key
            // gets the crew member the host made before slice F instead (CrewStandIn: an assistant at the host's next spawn point)
            SpawnPoint at = SpawnAt();
            var human = Dm.@new<mob_living_carbon_human>(locate(at.X, at.Y, at.Z));
            CrewStandIn.Name(human);
            KeyMob(human, key);
            world.usr = m = human;
            made = true;
        }
        Assert.NotNull(m);
        Assert.Equal(player.ckey, m.ckey);
        Assert.Same(m, world.usr); // usr is the mob of the key (BYOND ref /client/proc/New)
        m.client = player;
        player.FakeMob = m;
        ClientSeam.NotifyClientMobChanged(player, null, m);
        if (made)
            CrewStandIn.Equip((mob_living_carbon_human)m);
        if (LoginSleepTicks <= 0)
        {
            login.Mob = m;
            login.Done = true;
        }
        else
        {
            Waiting.Detach(FinishLogin(login, m, LoginSleepTicks));
        }
        return login;
    }

    private static async DmTask FinishLogin(Login login, mob m, int ticks)
    {
        await Sleeping.SleepTicks(ticks);
        login.Mob = ((client_test_m1_server_client)login.Client).FakeMob == m && !QDELETED(login.Client) ? m : null;
        login.Done = true;
    }

    public void SetHeldDirections(client player, int dirs)
    {
        Assert.InRange(dirs, 0, 15);
        var c = (client_test_m1_server_client)player;
        Held.Add((World.Current.Tick, c.key, dirs, World.Current.usr));
    }

    public void Rest(client player) => Actions.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, "rest", World.Current.usr));

    public void ToggleMoveIntent(client player) => Actions.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, "intent", World.Current.usr));

    public void ToggleCombatMode(client player) => Actions.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, "combat", World.Current.usr));

    // Version 14: an unhover (BYOND's MouseExited of the last hovered thing).
    public void Unhover(client player) => Actions.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, "unhover", World.Current.usr));

    // Version 15: a radial pick (the slice's or the centre's Click).
    public void RadialPick(client player, long menu, int element) => Actions.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, "radial " + menu + " " + element, World.Current.usr));

    /// <summary>Version 16: logs the tgui message as "tgui WINDOW TYPE JSON" (the payload json_encoded, "-" for none).</summary>
    public void TguiMessage(client player, string window, string type, DmList payload) => Actions.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, "tgui " + window + " " + type + " " + (payload == null ? "-" : Dm.json_encode(payload)), World.Current.usr));

    public void Click(client player, atom target, string @params)
        => Clicks.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, target, @params, World.Current.usr));

    // Version 17: a drop (tg's /client/MouseDrag and /client/MouseDrop).
    public List<(long Tick, string Key, atom Dragged, atom Over, string DragParams, string DropParams, int DragTime, object Usr)> Drops { get; } = new();

    public void Drop(client player, atom dragged, atom over, string dragParams, string dropParams, int dragDeciseconds)
        => Drops.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, dragged, over, dragParams, dropParams, dragDeciseconds, World.Current.usr));

    /// <summary>Version 24: the chat links the host ran (D154).</summary>
    public List<(long Tick, string Key, string Href, object Usr)> Topics { get; } = new();

    public void Topic(client player, string href)
        => Topics.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, href, World.Current.usr));

    /// <summary>M4a: the keybindings the host ran (code, down).</summary>
    public List<(long Tick, string Key, byte Code, bool Down, object Usr)> Keybindings { get; } = new();

    /// <summary>M4a: the hovers the host ran.</summary>
    public List<(long Tick, string Key, atom Target, object Usr)> Hovers { get; } = new();

    public void Keybinding(client player, byte code, bool down)
        => Keybindings.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, code, down, World.Current.usr));

    public void Hover(client player, atom target)
        => Hovers.Add((World.Current.Tick, ((client_test_m1_server_client)player).key, target, World.Current.usr));

    public void Disconnect(client player)
    {
        Disconnects.Add((World.Current.Tick, player.key));
        qdel(player);
    }

    private sealed class Login : ILoginHandle
    {
        public Login(client c) => Client = c;
        public client Client { get; }
        public bool Done { get; set; }
        public mob Mob { get; set; }
        public Exception Error { get; set; }
        public bool Accepted => Done && Error == null && Mob != null;
    }
}

/// <summary>
/// The locomotion seam (M2 P4) as a test stand-in: records what the host does (install, speed scale, registrations, the
/// frames of each tick) and gives back the capsule states a test sets, so the host's logic is tested without the bridge.
/// </summary>
internal sealed class FakeLocomotion : ILocomotionEntryPoints
{
    private readonly HashSet<long> _registered = new();

    public bool Installed { get; private set; }
    public float SpeedScale { get; private set; } = float.NaN;
    public List<(long Tick, long Mob)> Registrations { get; } = new();
    public List<(long Tick, long Mob, MotionInput Frame)> Frames { get; } = new();
    public Dictionary<long, (MotionState State, MotionParams Params)> States { get; } = new();

    /// <summary>True (the default): a frame with a wish vector steps its mob one tile along the dominant axis, so the inputs reach the world's state hash as the bridge's would.</summary>
    public bool MovesMobs { get; set; } = true;

    public void Install(World world) => Installed = !Installed ? true : throw new InvalidOperationException("installed twice");

    public void SetSpeedScale(World world, float scale) => SpeedScale = scale;

    public void Register(mob_living mob)
    {
        Assert.NotNull(World.CurrentOrNull);
        Assert.True(_registered.Add(mob.dm_id), "a mob registered twice");
        Registrations.Add((World.Current.Tick, mob.dm_id));
    }

    public void RegisterGhost(mob_dead_observer ghost)
    {
        Assert.NotNull(World.CurrentOrNull);
        Assert.True(_registered.Add(ghost.dm_id), "a ghost registered twice");
        Registrations.Add((World.Current.Tick, ghost.dm_id));
    }

    public void Unregister(mob mob) => _registered.Remove(mob.dm_id);

    public bool IsRegistered(mob mob) => _registered.Contains(mob.dm_id);

    public void SetFrame(mob mob, in MotionInput frame)
    {
        Assert.True(_registered.Contains(mob.dm_id), "a frame for a mob that is not registered");
        Frames.Add((World.Current.Tick, mob.dm_id, frame));
        if (!MovesMobs || (frame.MoveX == 0 && frame.MoveY == 0)) return;
        int dir = Math.Abs(frame.MoveX) >= Math.Abs(frame.MoveY) ? (frame.MoveX > 0 ? MotionDir.East : MotionDir.West) : (frame.MoveY > 0 ? MotionDir.North : MotionDir.South);
        Waiting.NoSleep(step(mob, dir));
    }

    public bool TryGetState(World world, long mobId, out MotionState state, out MotionParams parameters)
    {
        Assert.Null(World.CurrentOrNull); // read outside the world, after the step
        if (States.TryGetValue(mobId, out var s) && _registered.Contains(mobId))
        {
            state = s.State;
            parameters = s.Params;
            return true;
        }
        state = default;
        parameters = default;
        return false;
    }
}

/// <summary>A server host with the fakes, booted until joins open, driven tick by tick.</summary>
internal sealed class TestServer : IDisposable
{
    public const int RoomSize = 24;

    private ulong _tokens;

    private TestServer(ServerConfig config, bool network, CommandLog replay, TextWriter record, bool strict)
    {
        Config = config;
        Transport = network ? new FakeTransport() : null;
        Host = new ServerHost(config, Transport, Replicator, Codec, Clients, Out, Err, replay, recordTo: record,
            strictNotTranslated: strict, newToken: () => new SessionToken(++_tokens, 0xA5A5), locomotion: Locomotion);
        int spawned = 0;
        Clients.SpawnAt = () => Host.SpawnPoints[spawned++ % Host.SpawnPoints.Count];
        Host.Start();
    }

    public ServerConfig Config { get; }
    public FakeTransport Transport { get; }
    public FakeCodec Codec { get; } = new();
    public FakeClients Clients { get; } = new();
    public FakeLocomotion Locomotion { get; } = new();
    public FakeReplicator Replicator { get; } = new();
    public StringWriter Out { get; } = new();
    public StringWriter Err { get; } = new();
    public ServerHost Host { get; }
    public World World => Host.World;

    // the room alone, at the map's corner (no space ring): these tests name the room's cells by their map coordinates
    public static ServerConfig DefaultConfig(ulong seed = 7, bool? devCommands = null, bool clientConsole = false)
        => new() { Seed = seed, RoomWidth = RoomSize, RoomHeight = RoomSize, RoomSpace = 0, Console = false, StatusEveryTicks = 1000, DevCommands = devCommands, ClientConsole = clientConsole };

    /// <summary>
    /// A server with the fakes, stepped until joins open. <paramref name="strict"/>: a reached <c>Dm.NotTranslated</c> throws
    /// (the default; the M1 path runs strict in tests). The console's kick and list reach tg's <c>key_name</c> of a mob with
    /// a client, whose client branch is M1 P1's, so tests of them run lenient until P1 merges.
    /// </summary>
    public static TestServer Start(ServerConfig config = null, TextWriter record = null, bool strict = true)
    {
        var server = new TestServer(config ?? DefaultConfig(), true, null, record, strict);
        server.UntilJoinsOpen();
        return server;
    }

    /// <summary>A server with the fakes at tick 0 (joins not open yet).</summary>
    public static TestServer StartWithoutBoot(ServerConfig config) => new(config, true, null, null, true);

    /// <summary>A replay of <paramref name="log"/> (no network).</summary>
    public static TestServer Replay(CommandLog log, bool strict = true)
        => new(log.ReplayConfig(), false, log, null, strict);

    public void UntilJoinsOpen()
    {
        while (!Host.JoinsOpen && Host.Tick < 200) Host.RunTick();
        Assert.True(Host.JoinsOpen, "joins did not open within 200 ticks");
    }

    public void Tick(int n = 1)
    {
        for (int i = 0; i < n && !Host.Finished; i++) Host.RunTick();
    }

    /// <summary>Asks to connect; the answer comes with the next tick.</summary>
    public FakeRemote Connect(string name, SessionToken token = default, ushort protocol = ProtocolVersion.Current)
        => Transport.Request(FakeCodec.EncodeHello(name, token, protocol));

    /// <summary>Connects and ticks until the Welcome arrived (an instant login: 2 ticks).</summary>
    public FakeRemote Join(string name, SessionToken token = default)
    {
        FakeRemote remote = Connect(name, token);
        for (int i = 0; i < 20 && remote.Welcome == null && remote.Rejected == null; i++) Tick();
        Assert.True(remote.Welcome != null, $"{name} got no Welcome: {remote.Rejected}");
        return remote;
    }

    public mob MobOf(string key)
        => World.Objects.TryGet(Host.AvatarOf(key), out object o) ? (mob)o : null;

    public (int X, int Y) PositionOf(string key)
    {
        mob m = MobOf(key);
        return ((int)m.x, (int)m.y);
    }

    public ulong Hash() => World.ComputeStateHash();

    public void Dispose() => Host.Dispose();
}
