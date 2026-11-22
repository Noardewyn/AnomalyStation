namespace Anomaly.Protocol;

// Version 67 (MessagesV67.cs, docs/ai/specs/protocol.md section 68):
//
//   Minimap (server to client, State): [u8 id = 26] [varuint z] [varuint minX] [varuint minY] [varuint width] [varuint height]
//     [count colours] [u32 rgba]... [pixel runs: count, (varuint count, varuint colour index)...]
//     [count areas] [string name, string code]... [area runs: count, (varuint count, varuint area index)...]
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="MinimapMessage"/> for the State channel.</summary>
    public static byte[] Encode(MinimapMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        long bound = 1 + 5 * 5 + 5 + (long)message.Colours.Count * 4 + 5 + (long)message.PixelRuns.Count * MaxTurfRunBytes + 5
            + (long)message.AreaNames.Count * (10 + Limits.MaxMinimapAreaNameBytes + Limits.MaxMinimapAreaCodeBytes) + 5 + (long)message.AreaRuns.Count * MaxTurfRunBytes;
        return EncodeToArray(bound, (Span<byte> buffer) => Encode(message, buffer));
    }

    private static int Encode(MinimapMessage m, Span<byte> destination)
    {
        if (m.Z is < 1 or > Limits.MaxMapLevels || m.MinX < 1 || m.MinY < 1 || m.Width < 1 || m.Height < 1
            || m.MinX + m.Width - 1 > Limits.MaxMapSize || m.MinY + m.Height - 1 > Limits.MaxMapSize)
            throw Bad("a Minimap outside the map");
        if (m.AreaCodes.Count != m.AreaNames.Count)
            throw Bad("a Minimap with a department per area missing");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Minimap);
        w.WriteVarUInt((ulong)m.Z);
        w.WriteVarUInt((ulong)m.MinX);
        w.WriteVarUInt((ulong)m.MinY);
        w.WriteVarUInt((ulong)m.Width);
        w.WriteVarUInt((ulong)m.Height);
        w.WriteCount(m.Colours.Count, Limits.MaxMinimapColours);
        foreach (uint colour in m.Colours)
            w.WriteUInt32(colour);
        WriteValueRuns(ref w, m.PixelRuns, m.CellCount, m.Colours.Count, "minimap colour");
        w.WriteCount(m.AreaNames.Count, Limits.MaxMinimapAreas);
        for (int i = 0; i < m.AreaNames.Count; i++)
        {
            w.WriteString(m.AreaNames[i], Limits.MaxMinimapAreaNameBytes);
            w.WriteString(m.AreaCodes[i], Limits.MaxMinimapAreaCodeBytes);
        }
        WriteValueRuns(ref w, m.AreaRuns, m.CellCount, m.AreaNames.Count, "minimap area");
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    /// <summary>Decodes a <see cref="MinimapMessage"/>: a picture inside the map, its runs covering it, their indices in the tables.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out MinimapMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Minimap, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            var m = new MinimapMessage { Z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "a Minimap level") };
            m.MinX = ReadRanged(ref r, 1, Limits.MaxMapSize, "a Minimap's first column");
            m.MinY = ReadRanged(ref r, 1, Limits.MaxMapSize, "a Minimap's first row");
            m.Width = ReadRanged(ref r, 1, Limits.MaxMapSize - m.MinX + 1, "a Minimap's width");
            m.Height = ReadRanged(ref r, 1, Limits.MaxMapSize - m.MinY + 1, "a Minimap's height");
            int colours = r.ReadCount(Limits.MaxMinimapColours, 4);
            for (int i = 0; i < colours; i++)
                m.Colours.Add(r.ReadUInt32());
            ReadValueRuns(ref r, m.PixelRuns, m.CellCount, colours, "minimap colour");
            int areas = r.ReadCount(Limits.MaxMinimapAreas, 2);
            for (int i = 0; i < areas; i++)
            {
                m.AreaNames.Add(r.ReadString(Limits.MaxMinimapAreaNameBytes));
                m.AreaCodes.Add(r.ReadString(Limits.MaxMinimapAreaCodeBytes));
            }
            ReadValueRuns(ref r, m.AreaRuns, m.CellCount, areas, "minimap area");
            r.ExpectEnd();
            value = m;
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
