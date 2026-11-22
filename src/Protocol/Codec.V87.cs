namespace Anomaly.Protocol;

public static partial class Codec
{
    // Version 87, the end of MapGrid:
    //   [u8 0: the game's own sky and sun | 1: the map's]
    //   the map's: [u8 sky 0..3] [u24 top 0xRRGGBB] [u24 horizon] [f32 stars 0..4] [varuint picture length 0..MaxSkyImageBytes] [bytes]
    //              [u8 sun 0|1] [u24 colour] [f32 energy 0..8] [f32 size 0.2..30] [f32 elevation 1..90]
    //   A picture is a png or a jpg file's bytes, and only the picture sky has one.
    private static long MapLookBytes(MapGrid m) => 1 + (m.Look == null ? 0 : 1 + 3 + 3 + 4 + 5 + m.Look.Image.Length + 1 + 3 + 4 + 4 + 4);

    private static void WriteMapLook(ref WireWriter w, MapGrid m)
    {
        MapLook look = m.Look;
        w.WriteByte(look == null ? (byte)0 : (byte)1);
        if (look == null)
            return;
        if (look.Sky > MapLook.SkyImage) throw Bad("a MapGrid sky kind outside 0..3");
        if (look.Image == null || look.Image.Length > Limits.MaxSkyImageBytes || (look.Image.Length > 0 && (look.Sky != MapLook.SkyImage || !IsPicture(look.Image))))
            throw Bad("a MapGrid sky picture that is not a png or a jpg of at most " + Limits.MaxSkyImageBytes + " bytes, or the sky is not a picture's");
        w.WriteByte(look.Sky);
        WriteRgb(ref w, look.SkyTop);
        WriteRgb(ref w, look.SkyHorizon);
        w.WriteSingle(Within(look.Stars, 0f, 4f, "a MapGrid sky's stars"));
        w.WriteVarUInt((ulong)look.Image.Length);
        w.WriteBytes(look.Image);
        w.WriteByte(look.Sun ? (byte)1 : (byte)0);
        WriteRgb(ref w, look.SunColour);
        w.WriteSingle(Within(look.SunEnergy, 0f, 8f, "a MapGrid sun's energy"));
        w.WriteSingle(Within(look.SunSize, 0.2f, 30f, "a MapGrid sun's size"));
        w.WriteSingle(Within(look.SunElevation, 1f, 90f, "a MapGrid sun's elevation"));
    }

    private static void ReadMapLook(ref WireReader r, MapGrid m)
    {
        byte has = r.ReadByte();
        if (has > 1) throw Bad("a MapGrid look flag outside 0..1");
        if (has == 0)
            return;
        byte sky = r.ReadByte();
        if (sky > MapLook.SkyImage) throw Bad("a MapGrid sky kind outside 0..3");
        uint top = ReadRgb(ref r), horizon = ReadRgb(ref r);
        float stars = Within(r.ReadSingle(), 0f, 4f, "a MapGrid sky's stars");
        int length = ReadRanged(ref r, 0, Limits.MaxSkyImageBytes, "a MapGrid sky picture's length");
        byte[] image = r.ReadBytes(length).ToArray();
        if (length > 0 && (sky != MapLook.SkyImage || !IsPicture(image)))
            throw Bad("a MapGrid sky picture that is not a png or a jpg, or the sky is not a picture's");
        byte sun = r.ReadByte();
        if (sun > 1) throw Bad("a MapGrid sun flag outside 0..1");
        m.Look = new MapLook
        {
            Sky = sky, SkyTop = top, SkyHorizon = horizon, Stars = stars, Image = image, Sun = sun == 1, SunColour = ReadRgb(ref r),
            SunEnergy = Within(r.ReadSingle(), 0f, 8f, "a MapGrid sun's energy"),
            SunSize = Within(r.ReadSingle(), 0.2f, 30f, "a MapGrid sun's size"),
            SunElevation = Within(r.ReadSingle(), 1f, 90f, "a MapGrid sun's elevation"),
        };
    }

    /// <summary>True for the first bytes of a png or a jpg file.</summary>
    public static bool IsPicture(ReadOnlySpan<byte> file)
        => file.Length >= 8 && (file.Slice(0, PngSignature.Length).SequenceEqual(PngSignature) || (file[0] == 0xFF && file[1] == 0xD8 && file[2] == 0xFF));

    private static void WriteRgb(ref WireWriter w, uint rgb)
    {
        if (rgb > 0xFFFFFF) throw Bad("a colour outside 0xRRGGBB");
        w.WriteByte((byte)(rgb >> 16));
        w.WriteByte((byte)(rgb >> 8));
        w.WriteByte((byte)rgb);
    }

    private static uint ReadRgb(ref WireReader r) => (uint)(r.ReadByte() << 16 | r.ReadByte() << 8 | r.ReadByte());

    private static float Within(float value, float least, float most, string what)
        => float.IsFinite(value) && value >= least && value <= most ? value : throw Bad(what + " is outside " + least.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".." + most.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
