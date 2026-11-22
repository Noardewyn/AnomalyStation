namespace Anomaly.Protocol;

// Version 92 (MessagesV92.cs, docs/ai/specs/protocol.md section 89):
//
//   GunSight (server to client, State): [u8 id = 31] [u8 gun 0|1] [f32 cone 0..90, 0 without a gun] [u8 hits]
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="GunSightMessage"/> for the State channel.</summary>
    public static byte[] Encode(GunSightMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        CheckGunSight(message.Gun, message.ConeDegrees);
        var bytes = new byte[Limits.MaxGunSightBytes];
        var w = new WireWriter(bytes);
        w.WriteMessageId(MessageId.GunSight);
        w.WriteByte(message.Gun ? (byte)1 : (byte)0);
        w.WriteSingle(message.ConeDegrees);
        w.WriteByte(message.Hits);
        return bytes;
    }

    /// <summary>Decodes a <see cref="GunSightMessage"/>.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out GunSightMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.GunSight, Limits.MaxGunSightBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte gun = r.ReadByte();
            if (gun > 1)
                throw Bad("a GunSight gun flag outside 0..1");
            float cone = r.ReadSingle();
            CheckGunSight(gun == 1, cone);
            byte hits = r.ReadByte();
            r.ExpectEnd();
            value = new GunSightMessage(gun == 1, cone, hits);
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    private static void CheckGunSight(bool gun, float cone)
    {
        Within(cone, 0f, GunSightMessage.MaxConeDegrees, "a GunSight cone");
        if (!gun && cone != 0f)
            throw Bad("a GunSight cone without a gun");
        if (BitConverter.SingleToInt32Bits(cone) < 0)
            throw Bad("a GunSight cone of -0"); // decodes and encodes back to the same bytes
    }
}
