namespace Anomaly.Protocol;

// Version 70 (MessagesV70.cs, docs/ai/specs/protocol.md section 71):
//
//   BuildPreview (server to client, State): [u8 id = 28] [u8 set 0|1] then if set: [str(256) icon] [str(256) state] [u8 dir 0..10]
//                                           [u32 colour RGBA] [i8 pixel x] [i8 pixel y]
//                                           version 71: [varuint x 1..255] [varuint y 1..255] [varuint z 1..16] (version 70: [u8 reach])
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="BuildPreviewMessage"/> for the State channel.</summary>
    public static byte[] Encode(BuildPreviewMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return EncodeToArray(32 + 2 * (5 + Limits.MaxBuildPreviewTextBytes), (Span<byte> buffer) => Encode(message, buffer));
    }

    private static int Encode(BuildPreviewMessage m, Span<byte> destination)
    {
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.BuildPreview);
        w.WriteByte(m.IsSet ? (byte)1 : (byte)0);
        if (m.IsSet)
        {
            if (m.Dir > 10)
                throw Bad("a BuildPreview dir over 10");
            if (m.X is < 1 or > Limits.MaxMapSize || m.Y is < 1 or > Limits.MaxMapSize || m.Z is < 1 or > Limits.MaxMapLevels)
                throw Bad("a BuildPreview tile outside the map");
            w.WriteString(m.Icon, Limits.MaxBuildPreviewTextBytes);
            w.WriteString(m.State ?? "", Limits.MaxBuildPreviewTextBytes);
            w.WriteByte(m.Dir);
            w.WriteUInt32(m.Color);
            w.WriteByte((byte)m.PixelX);
            w.WriteByte((byte)m.PixelY);
            w.WriteVarUInt((ulong)m.X);
            w.WriteVarUInt((ulong)m.Y);
            w.WriteVarUInt((ulong)m.Z);
        }
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    /// <summary>Decodes a <see cref="BuildPreviewMessage"/>.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out BuildPreviewMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.BuildPreview, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte set = r.ReadByte();
            if (set > 1)
                throw Bad("a BuildPreview set flag is not 0 or 1");
            if (set == 0)
            {
                value = BuildPreviewMessage.None;
            }
            else
            {
                string icon = r.ReadString(Limits.MaxBuildPreviewTextBytes);
                if (string.IsNullOrEmpty(icon))
                    throw Bad("a BuildPreview without an icon");
                string state = r.ReadString(Limits.MaxBuildPreviewTextBytes) ?? "";
                byte dir = r.ReadByte();
                if (dir > 10)
                    throw Bad("a BuildPreview dir over 10");
                uint color = r.ReadUInt32();
                sbyte px = (sbyte)r.ReadByte(), py = (sbyte)r.ReadByte();
                int x = ReadRanged(ref r, 1, Limits.MaxMapSize, "a BuildPreview tile's x");
                int y = ReadRanged(ref r, 1, Limits.MaxMapSize, "a BuildPreview tile's y");
                int z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "a BuildPreview tile's z");
                value = new BuildPreviewMessage(icon, state, dir, color, px, py, x, y, z);
            }
            r.ExpectEnd();
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
