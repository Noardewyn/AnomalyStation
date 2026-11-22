namespace Anomaly.Protocol;

// Version 40 (MessagesV40.cs, docs/ai/specs/protocol.md section 49):
//
//   RadioChannels (server to client, State): [u8 id = 21] [u8 count 0..32] { [str(3) prefix ":x "] }*   (no prefix twice)
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="RadioChannelsMessage"/> for the State channel.</summary>
    public static int Encode(RadioChannelsMessage message, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Prefixes.Count > Limits.MaxRadioChannels)
            throw new ProtocolException(ProtocolError.CountTooLarge, "radio channels over " + Limits.MaxRadioChannels);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.RadioChannels);
        w.WriteByte((byte)message.Prefixes.Count);
        foreach (string prefix in message.Prefixes)
        {
            if (!RadioPrefixes.IsValid(prefix))
                throw Bad("a radio prefix that is not \":x \"");
            if (!seen.Add(prefix))
                throw Bad("a radio prefix listed twice");
            w.WriteString(prefix, 3);
        }
        return Finish(ref w, Limits.MaxRadioChannelsBytes);
    }

    /// <summary>The encoded <see cref="RadioChannelsMessage"/>.</summary>
    public static byte[] Encode(RadioChannelsMessage message)
    {
        Span<byte> buffer = stackalloc byte[Limits.MaxRadioChannelsBytes + 8];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    /// <summary>Decodes a <see cref="RadioChannelsMessage"/>: at most the cap, valid prefixes, none twice.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out RadioChannelsMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.RadioChannels, Limits.MaxRadioChannelsBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            int count = r.ReadByte();
            if (count > Limits.MaxRadioChannels)
                throw new ProtocolException(ProtocolError.CountTooLarge, "radio channels over " + Limits.MaxRadioChannels);
            var decoded = new RadioChannelsMessage();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string prefix = r.ReadString(3);
                if (!RadioPrefixes.IsValid(prefix))
                    throw Bad("a radio prefix that is not \":x \"");
                if (!seen.Add(prefix))
                    throw Bad("a radio prefix listed twice");
                decoded.Prefixes.Add(prefix);
            }
            r.ExpectEnd();
            value = decoded;
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }
}
