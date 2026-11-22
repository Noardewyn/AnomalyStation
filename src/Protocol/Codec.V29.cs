namespace Anomaly.Protocol;

// Version 29 (the eye blur, MessagesV29.cs, protocol.md section 38): the private block's game plane.
//   flags bit 6: [tableref appearance] (a varuint (index << 1) | private, 0 for none)

public static partial class Codec
{
    private const int OwnFlagGamePlane = 64;
    private const int MaxGamePlaneBytes = 5;

    private static void WriteGamePlane(ref WireWriter w, OwnBlock own)
        => WritePlainRef(ref w, own.GamePlane, true, Limits.MaxAppearanceTableEntries, "the game plane");

    private static void ReadGamePlane(ref WireReader r, OwnBlock own)
    {
        own.GamePlaneChanged = true;
        own.GamePlane = ReadPlainRef(ref r, true, Limits.MaxAppearanceTableEntries, "the game plane");
    }
}
