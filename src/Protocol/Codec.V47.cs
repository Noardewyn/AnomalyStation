namespace Anomaly.Protocol;

public static partial class Codec
{
    // [varuint: 0 without a sun, else 1 + the azimuth in hundredths of a degree (0..35999)]
    private static void WriteSun(ref WireWriter w, float azimuth)
        => w.WriteVarUInt(float.IsNaN(azimuth) ? 0u : 1u + (uint)Math.Clamp(MathF.Round(azimuth * 100f), 0f, 35999f));

    private static float ReadSun(ref WireReader r)
    {
        ulong v = r.ReadVarUInt();
        if (v == 0) return float.NaN;
        if (v > 36000) throw Bad("sun azimuth out of range");
        return (v - 1) / 100f;
    }
}
