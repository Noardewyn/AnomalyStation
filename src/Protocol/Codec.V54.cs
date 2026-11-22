namespace Anomaly.Protocol;

// Version 54 (MessagesV54.cs, docs/ai/specs/protocol.md section 59):
//
//   HeldMotion (server to client, State): [u8 id = 24] [u8 kind 0..2 (HeldMotionKind)]
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="HeldMotionMessage"/> for the State channel.</summary>
    public static byte[] Encode(HeldMotionMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Kind > HeldMotionKind.Rack)
            throw Bad("a held motion of an unknown kind");
        return new[] { (byte)MessageId.HeldMotion, (byte)message.Kind };
    }

    /// <summary>Decodes a <see cref="HeldMotionMessage"/>.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out HeldMotionMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.HeldMotion, Limits.MaxHeldMotionBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte kind = r.ReadByte();
            if (kind > (byte)HeldMotionKind.Rack)
                throw Bad("a held motion of an unknown kind");
            r.ExpectEnd();
            value = new HeldMotionMessage((HeldMotionKind)kind);
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
