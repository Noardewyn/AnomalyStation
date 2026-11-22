namespace Anomaly.Protocol;

public static partial class Codec
{
    // Version 91, the end of MapGrid: [u16 the wall height in millimetres, 2000..4000]
    private const long WallHeightBytes = 2;

    private static void WriteWallHeight(ref WireWriter w, MapGrid m)
    {
        if (m.WallHeightMillimetres < MapGrid.MinWallHeightMillimetres || m.WallHeightMillimetres > MapGrid.MaxWallHeightMillimetres)
            throw Bad("a MapGrid wall height outside 2000..4000 mm");
        w.WriteUInt16((ushort)m.WallHeightMillimetres);
    }

    private static void ReadWallHeight(ref WireReader r, MapGrid m)
    {
        int mm = r.ReadUInt16();
        if (mm < MapGrid.MinWallHeightMillimetres || mm > MapGrid.MaxWallHeightMillimetres)
            throw Bad("a MapGrid wall height outside 2000..4000 mm");
        m.WallHeightMillimetres = mm;
    }
}
