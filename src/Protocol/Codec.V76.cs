namespace Anomaly.Protocol;

public static partial class Codec
{
    // [varuint: count][varuint: z]*, ascending, each 2..levels
    private static long LinkedBelowBytes(MapGrid m) => 5 + 5L * m.LinkedBelowLevels.Count;

    private static void WriteLinkedBelow(ref WireWriter w, MapGrid m)
    {
        w.WriteCount(m.LinkedBelowLevels.Count, m.Levels);
        int last = 1;
        foreach (int z in m.LinkedBelowLevels)
        {
            if (z <= last || z > m.Levels) throw Bad("a MapGrid linked level out of order, the lowest level or outside the map");
            w.WriteVarUInt((ulong)(last = z));
        }
    }

    private static void ReadLinkedBelow(ref WireReader r, MapGrid m)
    {
        int count = ReadRanged(ref r, 0, m.Levels, "a MapGrid linked level count");
        int last = 1;
        for (int i = 0; i < count; i++)
        {
            int z = ReadRanged(ref r, last + 1, m.Levels, "a MapGrid linked level");
            m.LinkedBelowLevels.Add(last = z);
        }
    }
}
