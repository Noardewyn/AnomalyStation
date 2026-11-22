using System.Text;
using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

/// <summary>
/// The bytes of the abusive behaviours (M1 scope 2j "Session fuzz and abuse", extended to the version-2 commands): each
/// builder makes exactly the violation its name claims and nothing else, so the server tests can say which strike or which
/// drop a message must cause. What the server does (M2 P6's host): MotionInput is accepted only on the Input channel and
/// Action only on the Control channel (anything else, and the version-1 InputFrame, is a strike); a message over 256 bytes
/// is a strike; a message that does not decode is a strike; a MotionInput seq more than
/// <see cref="Limits.MaxInputSeqLead"/> ahead of the last applied one is a strike; a command before the Welcome is a
/// strike; three strikes within 200 ticks close the session. Messages over the rate limits are dropped, not strikes, and
/// 30 drops within 200 ticks close the session with <see cref="DisconnectReason.Flooding"/>. A dev command while the
/// server has dev commands off is answered with a ServerText, and a click on an unknown target is dropped silently.
/// None of the byte builders depends on the codec's layout beyond the first byte (the message id); the ones that need a
/// valid message use the codec's encoder.
/// </summary>
public static class AbusePayloads
{
    /// <summary>The longest garbage message (the first byte, the id, included).</summary>
    public const int MaxGarbageBytes = 48;

    /// <summary>The ids no client may send: 0, the server-to-client ones, and those past <see cref="MessageId.ContextHints"/>.</summary>
    private static readonly byte[] ForbiddenIds =
    {
        0,
        (byte)MessageId.Welcome, (byte)MessageId.MapGrid, (byte)MessageId.Snapshot, (byte)MessageId.Delta,
        (byte)MessageId.ServerText, (byte)MessageId.Disconnect,
        (byte)MessageId.ContextHints, 100, 200, 255,
    };

    /// <summary>The channel a message belongs on: MotionInput (and anything that starts like one) travels on the Input channel, everything else on Control.</summary>
    public static Channel ChannelFor(byte[] message)
        => message != null && message.Length > 0 && message[0] == (byte)MessageId.MotionInput ? Channel.Input : Channel.Control;

    /// <summary>
    /// A message the server cannot decode: an id that is none or not a client's, a MotionInput or an Action cut off after its
    /// id, one whose varint never ends, or a Delta (a server message). At most <see cref="MaxGarbageBytes"/> bytes, at least
    /// 1. A message that starts with the MotionInput id is meant for the Input channel (see <see cref="ChannelFor"/>), so
    /// that the only thing wrong with it is its content.
    /// </summary>
    public static byte[] Garbage(BotRandom random)
    {
        byte clientId = random.Chance(0.5) ? (byte)MessageId.MotionInput : (byte)MessageId.Action;
        switch (random.Next(0, 3))
        {
            case 0:
            {
                // An id outside the protocol or a server's id, then noise.
                var message = new byte[random.Next(1, MaxGarbageBytes)];
                random.Fill(message);
                message[0] = ForbiddenIds[random.Next(0, ForbiddenIds.Length - 1)];
                return message;
            }
            case 1:
                // A command cut off after its id.
                return new[] { clientId };
            case 2:
            {
                // A command whose first varints never end (a varint is at most 10 bytes; the run is longer than any
                // layout's fixed fields and varints together).
                var message = new byte[25];
                message[0] = clientId;
                for (int i = 1; i < message.Length; i++) message[i] = 0xFF;
                return message;
            }
            default:
            {
                var message = new byte[random.Next(2, MaxGarbageBytes)];
                random.Fill(message);
                message[0] = (byte)MessageId.Delta;
                return message;
            }
        }
    }

    /// <summary>An Action-looking message longer than <see cref="Limits.MaxInboundMessageBytes"/> (257 to 1000 bytes: reliable channels fragment, unreliable ones would refuse it at the MTU), meant for the Control channel.</summary>
    public static byte[] Oversized(BotRandom random)
    {
        // an Action may be as long as a say (version 38), so the oversized one is longer than that
        var message = new byte[random.Next(Limits.MaxSayActionBytes + 1, Limits.MaxSayActionBytes + 1000)];
        random.Fill(message);
        message[0] = (byte)MessageId.Action;
        return message;
    }

    /// <summary>
    /// A valid MotionInput message of <paramref name="count"/> frames (1 to <see cref="Limits.MaxInputFramesPerMessage"/>) whose
    /// first seq is <paramref name="firstSeq"/>, for the Input channel. The flood and early-command bots send these.
    /// </summary>
    public static byte[] MotionInputBytes(uint firstSeq, int count, sbyte moveX, sbyte moveY, ushort yaw)
    {
        var message = new MotionInputMessage();
        for (int i = 0; i < count; i++) message.Frames.Add(new MotionInput(firstSeq + (uint)i, moveX, moveY, yaw, MotionInputFlags.None));
        return Codec.Encode(message);
    }

    /// <summary>
    /// A valid single-frame MotionInput message whose seq is more than <see cref="Limits.MaxInputSeqLead"/> ahead of
    /// <paramref name="lastSeq"/>, the last frame the bot sent (so ahead of whatever the server applied): the forged seq.
    /// </summary>
    public static byte[] ForgedSeq(uint lastSeq, BotRandom random)
    {
        uint seq = lastSeq + (uint)Limits.MaxInputSeqLead + 1u + (uint)random.Next(0, 1000);
        return MotionInputBytes(seq, 1, RandomMove(random), 0, (ushort)random.Next(0, 65535));
    }

    private static sbyte RandomMove(BotRandom random) => (sbyte)random.Next(-127, 127);

    /// <summary>
    /// An Action message that is garbage: the Action id and noise, or cut off, or with a varint that never ends. For the
    /// Control channel. At most <see cref="MaxGarbageBytes"/> bytes.
    /// </summary>
    public static byte[] BadAction(BotRandom random)
    {
        switch (random.Next(0, 2))
        {
            case 0:
                return new[] { (byte)MessageId.Action };
            case 1:
            {
                var message = new byte[25];
                message[0] = (byte)MessageId.Action;
                for (int i = 1; i < message.Length; i++) message[i] = 0xFF;
                return message;
            }
            default:
            {
                // Noise after the id; the odds that a layout reads it as a valid action are far below one in a million.
                var message = new byte[random.Next(8, MaxGarbageBytes)];
                random.Fill(message);
                message[0] = (byte)MessageId.Action;
                return message;
            }
        }
    }

    /// <summary>An entity id no world has: the base of <see cref="MissingTargetClick"/>.</summary>
    public const long MissingEntityBase = 1_000_000_000L;

    /// <summary>A legal right click on an entity that does not exist: the server drops it silently (no strike, no reply).</summary>
    public static byte[] MissingTargetClick(uint frameSeq, BotRandom random)
        => Codec.Encode(new ActionMessage(frameSeq, ActionKind.Click, MissingEntityBase + random.Next(0, 1_000_000), ClickButton.Right, ClickModifiers.None, null));

    // ---- protocol version 4 (M4a P6): forged targets (legal messages, dropped silently) and format violations (strikes) ----

    /// <summary>A legal left click on the entity <paramref name="id"/> (a real one, when the caller picked it from its replica).</summary>
    public static byte[] EntityClick(uint frameSeq, long id, ClickButton button)
        => Codec.Encode(new ActionMessage(frameSeq, ActionKind.Click, id, button, ClickModifiers.None, null) { TargetKind = TargetKind.Entity });

    /// <summary>A legal hover on the entity <paramref name="id"/>.</summary>
    public static byte[] HoverOnEntity(uint frameSeq, long id)
        => Codec.Encode(new ActionMessage(frameSeq, ActionKind.Hover, id, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Entity });

    /// <summary>A legal click on a turf of another level (z 2 to 16) at a random cell: no map the server runs has it in the mob's view, so the server drops it silently.</summary>
    public static byte[] ForgedTurfClick(uint frameSeq, BotRandom random)
        => Codec.Encode(new ActionMessage(frameSeq, ActionKind.Click, 0, ClickButton.Left, ClickModifiers.None, null)
        {
            TargetKind = TargetKind.Turf,
            TurfX = random.Next(1, Limits.MaxMapSize),
            TurfY = random.Next(1, Limits.MaxMapSize),
            TurfZ = random.Next(2, Limits.MaxMapLevels),
            IconX = (byte)random.Next(1, Limits.MaxIconPixel),
            IconY = (byte)random.Next(1, Limits.MaxIconPixel),
        });

    /// <summary>A legal hover on a turf of another level.</summary>
    public static byte[] ForgedTurfHover(uint frameSeq, BotRandom random)
        => Codec.Encode(new ActionMessage(frameSeq, ActionKind.Hover, 0, ClickButton.None, ClickModifiers.None, null)
        {
            TargetKind = TargetKind.Turf,
            TurfX = random.Next(1, Limits.MaxMapSize),
            TurfY = random.Next(1, Limits.MaxMapSize),
            TurfZ = random.Next(2, Limits.MaxMapLevels),
        });

    /// <summary>A legal click on a screen id the session does not own (an id no world has).</summary>
    public static byte[] ForgedScreenClick(uint frameSeq, BotRandom random)
        => Codec.Encode(new ActionMessage(frameSeq, ActionKind.Click, MissingEntityBase + random.Next(0, 1_000_000), ClickButton.Left, ClickModifiers.None, null)
        {
            TargetKind = TargetKind.Screen,
            IconX = (byte)random.Next(1, Limits.MaxIconPixel),
            IconY = (byte)random.Next(1, Limits.MaxIconPixel),
        });

    /// <summary>A legal hover on an entity or screen id that does not exist.</summary>
    public static byte[] ForgedHover(uint frameSeq, BotRandom random)
        => Codec.Encode(new ActionMessage(frameSeq, ActionKind.Hover, MissingEntityBase + random.Next(0, 1_000_000), ClickButton.None, ClickModifiers.None, null)
        {
            TargetKind = random.Chance(0.5) ? TargetKind.Entity : TargetKind.Screen,
        });

    /// <summary>
    /// A version-4 Action that breaks the format, built from raw bytes (the codec's encoder refuses each): an unknown keybinding
    /// code (0, 9 or 255), a key-up flag of 2, an unknown target kind on a click, a turf at x or z 0, an icon pixel over 32, a
    /// hover that carries a button, and an action kind past the last one. Each is a strike.
    /// </summary>
    public static byte[] BadV4Action(uint frameSeq, BotRandom random)
    {
        byte[] body;
        switch (random.Next(0, 6))
        {
            case 0:
                body = new byte[] { (byte)ActionKind.Keybinding, new byte[] { 0, (byte)(KeybindingCode.Last + 1), 255 }[random.Next(0, 2)], 0 };
                break;
            case 1:
                body = new byte[] { (byte)ActionKind.Keybinding, (byte)KeybindingCode.DropItem, 2 };
                break;
            case 2:
                body = new byte[] { (byte)ActionKind.Click, new byte[] { 0, 4, 255 }[random.Next(0, 2)], 1, (byte)ClickButton.Left, 0, 0, 0 };
                break;
            case 3:
                body = random.Chance(0.5)
                    ? new byte[] { (byte)ActionKind.Click, (byte)TargetKind.Turf, 0, 5, 1, (byte)ClickButton.Left, 0, 0, 0 }
                    : new byte[] { (byte)ActionKind.Click, (byte)TargetKind.Turf, 5, 5, 0, (byte)ClickButton.Left, 0, 0, 0 };
                break;
            case 4:
                body = new byte[] { (byte)ActionKind.Click, (byte)TargetKind.Entity, 1, (byte)ClickButton.Left, 0, (byte)random.Next(Limits.MaxIconPixel + 1, 255), 1 };
                break;
            case 5:
                body = new byte[] { (byte)ActionKind.Hover, (byte)TargetKind.Entity, 1, (byte)ClickButton.Left, 0 };
                break;
            default:
                body = new byte[] { (byte)ActionKind.Hover + 1 };
                break;
        }

        var message = new List<byte> { (byte)MessageId.Action };
        uint seq = frameSeq;
        while (seq >= 0x80)
        {
            message.Add((byte)(seq | 0x80u));
            seq >>= 7;
        }

        message.Add((byte)seq);
        message.AddRange(body);
        return message.ToArray();
    }

    /// <summary>
    /// A legal dev command whose text is exactly <see cref="Limits.MaxDevCommandBytes"/> bytes: <c>abuse xxx...</c>, a command
    /// no table has, so that a server with dev commands on answers "unknown" and one with them off answers "off".
    /// </summary>
    public static byte[] DevCommandAtCap(uint frameSeq)
        => Codec.Encode(new ActionMessage(frameSeq, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, DevText(Limits.MaxDevCommandBytes)));

    /// <summary>
    /// A dev command whose text is <paramref name="extra"/> bytes (1 to 20 when random) over <see cref="Limits.MaxDevCommandBytes"/>,
    /// built from raw bytes because the codec's encoder refuses it. It keeps the message under 256 bytes, so the only thing
    /// wrong with it is the text length. The layout is read off the codec's own encoding of a short and a full text
    /// (the text is the last field, a varuint length and then the bytes); a layout that is not that gets
    /// <see cref="Limits.MaxDevCommandBytes"/> bytes of text plus <paramref name="extra"/> trailing bytes instead, which
    /// does not decode either.
    /// </summary>
    public static byte[] DevCommandOverCap(uint frameSeq, BotRandom random)
    {
        int extra = random.Next(1, 20);
        byte[] one = Codec.Encode(new ActionMessage(frameSeq, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, "a"));
        byte[] full = Codec.Encode(new ActionMessage(frameSeq, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, DevText(Limits.MaxDevCommandBytes)));

        int cap = Limits.MaxDevCommandBytes;
        int header = one.Length - 2; // [header][length 1][1 text byte]
        int lengthBytes = VarUIntSize((uint)cap);
        bool shape = header > 0 && full.Length == header + lengthBytes + cap && one[header] == 1 && full.AsSpan(0, header).SequenceEqual(one.AsSpan(0, header));
        if (!shape)
        {
            var padded = new byte[full.Length + extra];
            full.CopyTo(padded, 0);
            for (int i = full.Length; i < padded.Length; i++) padded[i] = (byte)'x';
            return padded;
        }

        int length = cap + extra;
        int total = header + VarUIntSize((uint)length) + length;
        if (total > Limits.MaxInboundMessageBytes)
        {
            length -= total - Limits.MaxInboundMessageBytes;
            total = Limits.MaxInboundMessageBytes;
        }
        var message = new byte[total];
        one.AsSpan(0, header).CopyTo(message);
        int at = header;
        uint remaining = (uint)length;
        while (remaining >= 0x80)
        {
            message[at++] = (byte)(remaining | 0x80);
            remaining >>= 7;
        }
        message[at++] = (byte)remaining;
        for (; at < message.Length; at++) message[at] = (byte)'x';
        return message;
    }

    /// <summary>The text of a dev command of exactly <paramref name="bytes"/> ASCII bytes: <c>abuse xxx...</c>.</summary>
    public static string DevText(int bytes)
    {
        const string prefix = "abuse ";
        return bytes <= prefix.Length ? prefix.Substring(0, bytes) : prefix + new string('x', bytes - prefix.Length);
    }

    private static int VarUIntSize(uint value)
    {
        int n = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            n++;
        }
        return n;
    }
}
