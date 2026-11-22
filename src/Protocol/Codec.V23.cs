namespace Anomaly.Protocol;

// Version 23 (DV54): the private block's ghost body (MessagesV23.cs, protocol.md section 32).
//   flags bit 5: [u8 has] then, when has is 1, [varuint x] [varuint y] [varuint z] (a cell, as the Delta's cell records)

public static partial class Codec
{
    private const int OwnFlagBody = 32;
    private const int MaxBodyBytes = 1 + 3 * 5;

    private static void WriteBody(ref WireWriter w, OwnBlock own)
    {
        if (own.Body is not MapCell body)
        {
            w.WriteByte(0);
            return;
        }

        w.WriteByte(1);
        WriteCell(ref w, body.X, body.Y, body.Z);
    }

    private static void ReadBody(ref WireReader r, OwnBlock own)
    {
        own.BodyChanged = true;
        byte has = r.ReadByte();
        if (has > 1)
            throw Bad("a private block's body flag other than 0 or 1");
        if (has == 0)
        {
            own.Body = null;
            return;
        }

        ReadCell(ref r, out int x, out int y, out int z);
        own.Body = new MapCell(x, y, z);
    }
}
