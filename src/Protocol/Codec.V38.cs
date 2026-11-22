namespace Anomaly.Protocol;

// Version 38 (MessagesV38.cs, docs/ai/specs/protocol.md section 47):
//
//   Say (Action kind 18, client to server, Control): [str(1..16) type, a SayTypes word] [str(0..16) channel or none, a SayChannels word]
//     [str(0..3072) entry or none, at most 1024 UTF-16 code units] [u8 visible 0..1]
//   SayModal (server to client, Control): [u8 id = 19] [u8 command 1..4] (Props: [varuint max length 1..1024] [u8 light 0..1])
//   Runechat (server to client, Control): [u8 id = 20] [varuint target >= 1] [str(1..4096) maptext] [varuint lifespan 1..600]
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    private static void WriteSay(ref WireWriter w, in ActionMessage message)
    {
        if (!SayTypes.IsKnown(message.SayType))
            throw Bad("a say of an unknown type");
        if (message.SayChannel != null && !SayChannels.IsKnown(message.SayChannel))
            throw Bad("a say on an unknown channel");
        if (message.Text != null && message.Text.Length > Limits.MaxSayEntryChars)
            throw new ProtocolException(ProtocolError.TooLong, "a say entry over " + Limits.MaxSayEntryChars + " characters");
        if (message.SayVisible && message.SayType != SayTypes.Thinking)
            throw Bad("a say's visible flag on a type that is not thinking");
        w.WriteString(message.SayType, Limits.MaxSayWordBytes);
        w.WriteString(message.SayChannel, Limits.MaxSayWordBytes);
        w.WriteString(message.Text, Limits.MaxSayEntryBytes);
        w.WriteByte(message.SayVisible ? (byte)1 : (byte)0);
    }

    private static void ReadSay(ref WireReader r, out string type, out string channel, out string entry, out bool visible)
    {
        type = r.ReadString(Limits.MaxSayWordBytes);
        if (!SayTypes.IsKnown(type))
            throw Bad("a say of an unknown type");
        channel = r.ReadString(Limits.MaxSayWordBytes);
        if (channel != null && !SayChannels.IsKnown(channel))
            throw Bad("a say on an unknown channel");
        entry = r.ReadString(Limits.MaxSayEntryBytes);
        if (entry != null && entry.Length > Limits.MaxSayEntryChars)
            throw new ProtocolException(ProtocolError.TooLong, "a say entry over " + Limits.MaxSayEntryChars + " characters");
        byte flag = r.ReadByte();
        if (flag > 1 || flag == 1 && type != SayTypes.Thinking)
            throw Bad("a say's visible flag that is not 0, or 1 on a thinking message");
        visible = flag == 1;
    }

    /// <summary>Encodes a <see cref="SayModalMessage"/> for the Control channel.</summary>
    public static int Encode(in SayModalMessage message, Span<byte> destination)
    {
        if (message.Command < SayModalCommand.Props || message.Command > SayModalCommand.Last)
            throw Bad("a say window message of an unknown command");
        bool props = message.Command == SayModalCommand.Props;
        if (props && (message.MaxLength < 1 || message.MaxLength > Limits.MaxSayEntryChars))
            throw Bad("a say window's length cap out of range");
        if (!props && (message.MaxLength != 0 || message.LightMode))
            throw Bad("a say window's props on a command that is not props");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.SayModal);
        w.WriteByte((byte)message.Command);
        if (props)
        {
            w.WriteVarUInt((ulong)message.MaxLength);
            w.WriteByte(message.LightMode ? (byte)1 : (byte)0);
        }
        return Finish(ref w, Limits.MaxSayModalBytes);
    }

    /// <summary>The encoded <see cref="SayModalMessage"/>.</summary>
    public static byte[] Encode(in SayModalMessage message)
    {
        Span<byte> buffer = stackalloc byte[Limits.MaxSayModalBytes + 8];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    /// <summary>Decodes a <see cref="SayModalMessage"/>: a known command; a props message's length cap in range.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out SayModalMessage value, out ProtocolError error)
    {
        value = default;
        if (!Begin(message, MessageId.SayModal, Limits.MaxSayModalBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte command = r.ReadByte();
            if (command < (byte)SayModalCommand.Props || command > (byte)SayModalCommand.Last)
                throw Bad("a say window message of an unknown command");
            int maxLength = 0;
            bool light = false;
            if (command == (byte)SayModalCommand.Props)
            {
                ulong length = r.ReadVarUInt();
                if (length < 1 || length > Limits.MaxSayEntryChars)
                    throw Bad("a say window's length cap out of range");
                maxLength = (int)length;
                byte lightByte = r.ReadByte();
                if (lightByte > 1)
                    throw Bad("a say window's light flag that is not 0 or 1");
                light = lightByte == 1;
            }
            r.ExpectEnd();
            value = new SayModalMessage((SayModalCommand)command, maxLength, light);
            return true;
        }
        catch (ProtocolException e)
        {
            value = default;
            error = e.Error;
            return false;
        }
    }

    /// <summary>Encodes a <see cref="RunechatMessage"/> for the Control channel.</summary>
    public static int Encode(in RunechatMessage message, Span<byte> destination)
    {
        if (message.Target < 1)
            throw Bad("a runechat line without a target");
        if (string.IsNullOrEmpty(message.Maptext))
            throw Bad("a runechat line without text");
        if (message.Lifespan < 1 || message.Lifespan > Limits.MaxRunechatLifespan)
            throw Bad("a runechat line's lifespan out of range");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Runechat);
        w.WriteVarUInt((ulong)message.Target);
        w.WriteString(message.Maptext, Limits.MaxRunechatBytes);
        w.WriteVarUInt((ulong)message.Lifespan);
        return Finish(ref w, Limits.MaxRunechatMessageBytes);
    }

    /// <summary>The encoded <see cref="RunechatMessage"/>.</summary>
    public static byte[] Encode(in RunechatMessage message)
    {
        RunechatMessage copy = message;
        return EncodeToArray(Limits.MaxRunechatMessageBytes, (Span<byte> buffer) => Encode(copy, buffer));
    }

    /// <summary>Decodes a <see cref="RunechatMessage"/>: a target above 0, a text, a lifespan in range.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out RunechatMessage value, out ProtocolError error)
    {
        value = default;
        if (!Begin(message, MessageId.Runechat, Limits.MaxRunechatMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            long target = ReadNonNegativeLong(ref r, "a runechat line's target");
            if (target < 1)
                throw Bad("a runechat line without a target");
            string maptext = r.ReadString(Limits.MaxRunechatBytes);
            if (string.IsNullOrEmpty(maptext))
                throw Bad("a runechat line without text");
            ulong lifespan = r.ReadVarUInt();
            if (lifespan < 1 || lifespan > Limits.MaxRunechatLifespan)
                throw Bad("a runechat line's lifespan out of range");
            r.ExpectEnd();
            value = new RunechatMessage(target, maptext, (int)lifespan);
            return true;
        }
        catch (ProtocolException e)
        {
            value = default;
            error = e.Error;
            return false;
        }
    }
}
