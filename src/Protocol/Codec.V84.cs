namespace Anomaly.Protocol;

// Version 84 (MessagesV84.cs):
//
//   Weather (server to client, State): [u8 id = 30] [varuint z] [count layers]
//     ([varuint id] [u8 stage] [string state] [u32 rgba] [u8 alpha] [u8 glow] [string particles] [u8 severity] [u8 wind + 1]
//      [varuint minX] [varuint minY] [varuint width] [varuint height] [cell runs: count, (varuint count, varuint 0 or 1)...])...
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="WeatherMessage"/> for the State channel.</summary>
    public static byte[] Encode(WeatherMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        long bound = 1 + 5 + 5;
        foreach (WeatherLayer l in message.Layers)
            bound += 5 + 1 + 5 + Limits.MaxWeatherStateBytes + 4 + 1 + 1 + 5 + Limits.MaxWeatherStateBytes + 1 + 1 + 4 * 5 + 5 + (long)l.Cells.Count * MaxTurfRunBytes;
        return EncodeToArray(bound, (Span<byte> buffer) => Encode(message, buffer));
    }

    private static int Encode(WeatherMessage m, Span<byte> destination)
    {
        if (m.Z is < 1 or > Limits.MaxMapLevels)
            throw Bad("a Weather level outside the map");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Weather);
        w.WriteVarUInt((ulong)m.Z);
        w.WriteCount(m.Layers.Count, Limits.MaxWeatherLayers);
        foreach (WeatherLayer l in m.Layers)
        {
            if (l.Id < 0 || l.Stage is < 1 or > 3 || l.Alpha is < 0 or > 255 || l.Severity is < 0 or > 100 || l.Wind is < -1 or > 1)
                throw Bad("a Weather layer with a value outside its range");
            if (l.MinX < 1 || l.MinY < 1 || l.Width < 1 || l.Height < 1 || l.MinX + l.Width - 1 > Limits.MaxMapSize || l.MinY + l.Height - 1 > Limits.MaxMapSize)
                throw Bad("a Weather layer's box outside the map");
            w.WriteVarUInt((ulong)l.Id);
            w.WriteByte((byte)l.Stage);
            w.WriteString(l.State ?? "", Limits.MaxWeatherStateBytes);
            w.WriteUInt32(l.Colour);
            w.WriteByte((byte)l.Alpha);
            w.WriteByte(l.Glow ? (byte)1 : (byte)0);
            w.WriteString(l.Particles ?? "", Limits.MaxWeatherStateBytes);
            w.WriteByte((byte)l.Severity);
            w.WriteByte((byte)(l.Wind + 1));
            w.WriteVarUInt((ulong)l.MinX);
            w.WriteVarUInt((ulong)l.MinY);
            w.WriteVarUInt((ulong)l.Width);
            w.WriteVarUInt((ulong)l.Height);
            WriteValueRuns(ref w, l.Cells, l.CellCount, 1, "weather cell");
        }
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    /// <summary>Decodes a <see cref="WeatherMessage"/>: its layers' stages, values and boxes in range, their runs covering their boxes.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out WeatherMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Weather, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            var m = new WeatherMessage { Z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "a Weather level") };
            int layers = r.ReadCount(Limits.MaxWeatherLayers, 16);
            for (int i = 0; i < layers; i++)
            {
                var l = new WeatherLayer { Id = ReadRanged(ref r, 0, int.MaxValue, "a Weather id") };
                l.Stage = r.ReadByte();
                if (l.Stage is < 1 or > 3)
                    throw Bad("a Weather stage outside 1 to 3");
                l.State = r.ReadString(Limits.MaxWeatherStateBytes);
                l.Colour = r.ReadUInt32();
                l.Alpha = r.ReadByte();
                int glow = r.ReadByte();
                if (glow > 1)
                    throw Bad("a Weather glow that is not 0 or 1");
                l.Glow = glow == 1;
                l.Particles = r.ReadString(Limits.MaxWeatherStateBytes);
                l.Severity = r.ReadByte();
                if (l.Severity > 100)
                    throw Bad("a Weather severity above 100");
                int wind = r.ReadByte();
                if (wind > 2)
                    throw Bad("a Weather wind outside -1 to 1");
                l.Wind = wind - 1;
                l.MinX = ReadRanged(ref r, 1, Limits.MaxMapSize, "a Weather box's first column");
                l.MinY = ReadRanged(ref r, 1, Limits.MaxMapSize, "a Weather box's first row");
                l.Width = ReadRanged(ref r, 1, Limits.MaxMapSize - l.MinX + 1, "a Weather box's width");
                l.Height = ReadRanged(ref r, 1, Limits.MaxMapSize - l.MinY + 1, "a Weather box's height");
                ReadValueRuns(ref r, l.Cells, l.CellCount, 1, "weather cell");
                m.Layers.Add(l);
            }
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
