namespace Anomaly.Protocol;

public static partial class Codec
{
    // [varuint: count][varuint: z]*, ascending, each 1..levels
    private static long NoParallaxBytes(MapGrid m) => 5 + 5L * m.NoParallaxLevels.Count;

    private static void WriteNoParallax(ref WireWriter w, MapGrid m)
    {
        w.WriteCount(m.NoParallaxLevels.Count, m.Levels);
        int last = 0;
        foreach (int z in m.NoParallaxLevels)
        {
            if (z <= last || z > m.Levels) throw Bad("a MapGrid no-parallax level out of order or outside the map");
            w.WriteVarUInt((ulong)(last = z));
        }
    }

    private static void ReadNoParallax(ref WireReader r, MapGrid m)
    {
        int count = ReadRanged(ref r, 0, m.Levels, "a MapGrid no-parallax level count");
        int last = 0;
        for (int i = 0; i < count; i++)
        {
            int z = ReadRanged(ref r, last + 1, m.Levels, "a MapGrid no-parallax level");
            m.NoParallaxLevels.Add(last = z);
        }
    }
}
