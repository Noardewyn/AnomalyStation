namespace Anomaly.Protocol;

// Version 96 (MessagesV96.cs), at the end of MapGrid (after the wall height) and of the Delta body (after the sun):
//   [count] { [cell] [u8 ceiling 0/1] [light] }*      MapGrid: every light is lit (range not 0); Delta: range 0 is a light gone out
// A cell and kind appear once per message.

public static partial class Codec
{
    private const int MinTurfLightBytes = 3 + 1 + 4;

    private static long TurfLightBytes(List<TurfLightChange> lights) => 10 + (long)lights.Count * (15 + 1 + MaxLightBytes);

    private static void WriteTurfLights(ref WireWriter w, List<TurfLightChange> lights, int max, bool allowNone)
    {
        w.WriteCount(lights.Count, max);
        var seen = new HashSet<(int, int, int, bool)>();
        foreach (TurfLightChange c in lights)
        {
            if (!seen.Add((c.X, c.Y, c.Z, c.Ceiling)))
                throw Bad("a turf light named twice");
            if (c.Light.IsNone && !allowNone)
                throw Bad("a MapGrid turf light that gives no light");
            WriteCell(ref w, c.X, c.Y, c.Z);
            w.WriteByte(c.Ceiling ? (byte)1 : (byte)0);
            WriteLight(ref w, c.Light);
        }
    }

    private static void ReadTurfLights(ref WireReader r, List<TurfLightChange> lights, int max, bool allowNone)
    {
        int n = r.ReadCount(max, MinTurfLightBytes);
        lights.Capacity = n;
        var seen = new HashSet<(int, int, int, bool)>();
        for (int i = 0; i < n; i++)
        {
            ReadCell(ref r, out int x, out int y, out int z);
            byte ceiling = r.ReadByte();
            if (ceiling > 1)
                throw Bad("a turf light's ceiling byte outside 0..1");
            LightState light = ReadLight(ref r);
            if (light.IsNone && !allowNone)
                throw Bad("a MapGrid turf light that gives no light");
            if (!seen.Add((x, y, z, ceiling == 1)))
                throw Bad("a turf light named twice");
            lights.Add(new TurfLightChange(x, y, z, ceiling == 1, light));
        }
    }
}
