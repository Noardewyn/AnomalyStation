namespace Anomaly.Protocol;

// Version 31 (M7 slice T3, MessagesV31.cs, protocol.md section 40): the private block's vision.
//   flags bit 7: [varuint sight] [f32 lighting cutoff] [f32 red] [f32 green] [f32 blue]   (the cutoffs in percent, 0 to 100)

public static partial class Codec
{
    private const int OwnFlagVision = 128;
    private const int MaxVisionBytes = 5 + 4 * 4;
    private const int MaxSight = (1 << 24) - 1;

    private static void WriteVision(ref WireWriter w, OwnBlock own)
    {
        VisionState v = own.Vision;
        if (v.Sight < 0 || v.Sight > MaxSight)
            throw new ProtocolException(ProtocolError.BadValue, "a sight of " + v.Sight);
        w.WriteVarUInt((ulong)v.Sight);
        foreach (float cutoff in new[] { v.LightingCutoff, v.CutoffRed, v.CutoffGreen, v.CutoffBlue })
        {
            if (!(cutoff >= 0f && cutoff <= 100f))
                throw new ProtocolException(ProtocolError.BadValue, "a lighting cutoff of " + cutoff);
            w.WriteSingle(cutoff);
        }
    }

    private static void ReadVision(ref WireReader r, OwnBlock own)
    {
        own.VisionChanged = true;
        ulong sight = r.ReadVarUInt();
        if (sight > MaxSight)
            throw Bad("a sight above 24 bits");
        float lighting = ReadCutoff(ref r), red = ReadCutoff(ref r), green = ReadCutoff(ref r), blue = ReadCutoff(ref r);
        own.Vision = new VisionState((int)sight, lighting, red, green, blue);
    }

    private static float ReadCutoff(ref WireReader r)
    {
        float cutoff = r.ReadSingle();
        if (!(cutoff >= 0f && cutoff <= 100f))
            throw Bad("a lighting cutoff outside 0 to 100");
        return cutoff;
    }
}
