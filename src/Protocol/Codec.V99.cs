namespace Anomaly.Protocol;

// Version 99 (MessagesV99.cs, docs/ai/specs/protocol.md section 94):
//
//   PhotoRequest (server to client, State): [u8 id = 32] [varuint photo id >= 1] [varint x] [varint y] [varint z] [u8 tiles x 1..15]
//     [u8 tiles y 1..15] [u16 width 1..MaxPhotoSide] [u16 height 1..MaxPhotoSide]
//   PhotoPart (client to server, State): [u8 id = 33] [varuint photo id >= 1] [varuint total 1..MaxPhotoBytes] [varuint offset < total]
//     [varuint length 1..4096, offset + length <= total] [bytes]: a part of the PNG, sent in order from offset 0.
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="PhotoRequestMessage"/> for the State channel.</summary>
    public static byte[] Encode(PhotoRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        CheckPhotoRequest(message.Id, message.TilesX, message.TilesY, message.Width, message.Height);
        Span<byte> buffer = stackalloc byte[Limits.MaxPhotoRequestBytes];
        var w = new WireWriter(buffer);
        w.WriteMessageId(MessageId.PhotoRequest);
        w.WriteVarUInt(message.Id);
        w.WriteVarInt(message.X);
        w.WriteVarInt(message.Y);
        w.WriteVarInt(message.Z);
        w.WriteByte(message.TilesX);
        w.WriteByte(message.TilesY);
        w.WriteUInt16(message.Width);
        w.WriteUInt16(message.Height);
        return buffer.Slice(0, Finish(ref w, Limits.MaxPhotoRequestBytes)).ToArray();
    }

    /// <summary>Decodes a <see cref="PhotoRequestMessage"/>.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out PhotoRequestMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.PhotoRequest, Limits.MaxPhotoRequestBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            ulong id = r.ReadVarUInt();
            if (id == 0 || id > uint.MaxValue)
                throw Bad("a PhotoRequest id outside 1..2^32-1");
            long x = r.ReadVarInt(), y = r.ReadVarInt(), z = r.ReadVarInt();
            if (x is < int.MinValue or > int.MaxValue || y is < int.MinValue or > int.MaxValue || z is < int.MinValue or > int.MaxValue)
                throw Bad("a PhotoRequest tile out of range");
            byte tilesX = r.ReadByte(), tilesY = r.ReadByte();
            ushort width = r.ReadUInt16(), height = r.ReadUInt16();
            CheckPhotoRequest((uint)id, tilesX, tilesY, width, height);
            r.ExpectEnd();
            value = new PhotoRequestMessage((uint)id, (int)x, (int)y, (int)z, tilesX, tilesY, width, height);
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    private static void CheckPhotoRequest(uint id, byte tilesX, byte tilesY, ushort width, ushort height)
    {
        if (id == 0)
            throw Bad("a PhotoRequest id of 0");
        if (tilesX is < 1 or > Limits.MaxPhotoTiles || tilesY is < 1 or > Limits.MaxPhotoTiles)
            throw Bad("a PhotoRequest frame outside 1.." + Limits.MaxPhotoTiles + " tiles");
        if (width is < 1 or > Limits.MaxPhotoSide || height is < 1 or > Limits.MaxPhotoSide)
            throw Bad("a PhotoRequest size outside 1.." + Limits.MaxPhotoSide + " pixels");
    }

    /// <summary>The <see cref="PhotoPart"/> messages of a picture's PNG, in the order they are sent.</summary>
    public static List<byte[]> EncodePhotoParts(uint id, byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (png.Length < 1 || png.Length > Limits.MaxPhotoBytes)
            throw new ProtocolException(ProtocolError.TooLong, "a photo of " + png.Length + " bytes, not 1 to " + Limits.MaxPhotoBytes);
        var parts = new List<byte[]>();
        for (int offset = 0; offset < png.Length; offset += Limits.MaxPhotoPartBytes)
            parts.Add(Encode(new PhotoPart(id, png.Length, offset, png.AsSpan(offset, Math.Min(Limits.MaxPhotoPartBytes, png.Length - offset)).ToArray())));
        return parts;
    }

    /// <summary>Encodes a <see cref="PhotoPart"/> for the State channel.</summary>
    public static byte[] Encode(PhotoPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (part.Id == 0 || !ValidPhotoPart(part.Total, part.Offset, part.Bytes?.Length ?? 0))
            throw Bad("a photo part out of range");
        Span<byte> buffer = stackalloc byte[Limits.MaxPhotoPartMessageBytes];
        var w = new WireWriter(buffer);
        w.WriteMessageId(MessageId.PhotoPart);
        w.WriteVarUInt(part.Id);
        w.WriteVarUInt((ulong)part.Total);
        w.WriteVarUInt((ulong)part.Offset);
        w.WriteVarUInt((ulong)part.Bytes.Length);
        w.WriteBytes(part.Bytes);
        return buffer.Slice(0, Finish(ref w, Limits.MaxPhotoPartMessageBytes)).ToArray();
    }

    private static bool ValidPhotoPart(long total, long offset, long length)
        => total >= 1 && total <= Limits.MaxPhotoBytes && offset >= 0 && offset < total && length >= 1 && length <= Limits.MaxPhotoPartBytes && offset + length <= total;

    /// <summary>Decodes a <see cref="PhotoPart"/>: an id, a total within the photo's cap, the part inside it, at most 4096 bytes.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out PhotoPart value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.PhotoPart, Limits.MaxPhotoPartMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            ulong id = r.ReadVarUInt();
            if (id == 0 || id > uint.MaxValue)
                throw Bad("a PhotoPart id outside 1..2^32-1");
            ulong total = r.ReadVarUInt(), offset = r.ReadVarUInt(), length = r.ReadVarUInt();
            if (total > int.MaxValue || offset > int.MaxValue || length > int.MaxValue || !ValidPhotoPart((long)total, (long)offset, (long)length))
                throw Bad("a photo part out of range");
            byte[] bytes = r.ReadBytes((int)length).ToArray();
            r.ExpectEnd();
            value = new PhotoPart((uint)id, (int)total, (int)offset, bytes);
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
