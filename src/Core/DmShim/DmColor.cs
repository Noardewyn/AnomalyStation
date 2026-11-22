using System.Globalization;

namespace Anomaly.Core.DmShim;

/// <summary>
/// <c>rgb()</c> and <c>rgb2num()</c> (BYOND ref <c>/proc/rgb</c>, <c>/proc/rgb2num</c>, "HTML colors", "Color space";
/// OpenDream <c>SharedOperations.ParseRgb</c>, <c>NativeProc_rgb2num</c>): RGB components 0-255, HSV and HSL with hue
/// 0-360 and the other two 0-100. Owner: values.
/// </summary>
internal static class DmColor
{
    internal static string Rgb(float r, float g, float b, float? a, float? space)
    {
        int s = space.HasValue && !float.IsNaN(space.Value) ? (int)space.Value : 0;
        float red, green, blue;
        switch (s)
        {
            case 1: // COLORSPACE_HSV
                (red, green, blue) = FromHsv(Clamp(r, 0, 360), Clamp(g, 0, 100) / 100f, Clamp(b, 0, 100) / 100f);
                break;
            case 2: // COLORSPACE_HSL
                (red, green, blue) = FromHsl(Clamp(r, 0, 360), Clamp(g, 0, 100) / 100f, Clamp(b, 0, 100) / 100f);
                break;
            default: // RGB, and "BYOND treats an unknown numeric color space as RGB" (OpenDream)
                string rgb = "#" + Hex(Truncate255(r)) + Hex(Truncate255(g)) + Hex(Truncate255(b));
                return a.HasValue ? rgb + Hex(Alpha(a.Value)) : rgb;
        }
        string hsv = "#" + Hex(Round255(red)) + Hex(Round255(green)) + Hex(Round255(blue));
        return a.HasValue ? hsv + Hex(Alpha(a.Value)) : hsv;
    }

    internal static DmList RgbToNum(string color, float space)
    {
        if (!TryParse(color ?? "#FFFFFF", out int r, out int g, out int b, out int alpha)) throw new DmRuntimeException("bad color");
        var list = new DmList();
        int s = float.IsNaN(space) ? -1 : (int)space;
        switch (s)
        {
            case 0:
                list.AppendItem((float)r);
                list.AppendItem((float)g);
                list.AppendItem((float)b);
                break;
            case 1:
            {
                (float h, float sat, float v) = ToHsv(r / 255f, g / 255f, b / 255f);
                list.AppendItem(h);
                list.AppendItem(sat * 100f);
                list.AppendItem(v * 100f);
                break;
            }
            case 2:
            {
                (float h, float sat, float l) = ToHsl(r / 255f, g / 255f, b / 255f);
                list.AppendItem(h);
                list.AppendItem(sat * 100f);
                list.AppendItem(l * 100f);
                break;
            }
            default:
                throw new DmRuntimeException("rgb2num(): unsupported color space " + Dm.num2text(space));
        }
        if (alpha >= 0) list.AppendItem((float)alpha);
        return list;
    }

    // "#rgb", "#rgba", "#rrggbb", "#rrggbbaa" or a named HTML color; alpha is -1 when the color has none.
    private static bool TryParse(string text, out int r, out int g, out int b, out int alpha)
    {
        r = g = b = 0;
        alpha = -1;
        string s = text.Trim();
        if (s.Length == 0) return false;
        if (s[0] != '#') s = Named(s);
        if (s == null) return false;
        string hex = s.Substring(1);
        if (hex.Length == 3 || hex.Length == 4)
        {
            var doubled = new char[hex.Length * 2];
            for (int i = 0; i < hex.Length; i++) doubled[2 * i] = doubled[2 * i + 1] = hex[i];
            hex = new string(doubled);
        }
        if (hex.Length != 6 && hex.Length != 8) return false;
        if (!int.TryParse(hex.AsSpan(0, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out r)) return false;
        if (!int.TryParse(hex.AsSpan(2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out g)) return false;
        if (!int.TryParse(hex.AsSpan(4, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out b)) return false;
        if (hex.Length == 8 && !int.TryParse(hex.AsSpan(6, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out alpha)) return false;
        return true;
    }

    // The named colors of BYOND ref "HTML colors".
    private static string Named(string name) => name.ToLowerInvariant() switch
    {
        "black" => "#000000",
        "silver" => "#C0C0C0",
        "gray" or "grey" => "#808080",
        "white" => "#FFFFFF",
        "maroon" => "#800000",
        "red" => "#FF0000",
        "purple" => "#800080",
        "fuchsia" or "magenta" => "#FF00FF",
        "green" => "#00C000",
        "lime" => "#00FF00",
        "olive" or "gold" => "#808000",
        "yellow" => "#FFFF00",
        "navy" => "#000080",
        "blue" => "#0000FF",
        "teal" => "#008080",
        "aqua" or "cyan" => "#00FFFF",
        _ => null,
    };

    private static (float R, float G, float B) FromHsv(float h, float s, float v)
    {
        float c = v * s;
        float hp = (h % 360f) / 60f;
        float x = c * (1f - MathF.Abs(hp % 2f - 1f));
        (float r1, float g1, float b1) = Sector(hp, c, x);
        float m = v - c;
        return (r1 + m, g1 + m, b1 + m);
    }

    private static (float R, float G, float B) FromHsl(float h, float s, float l)
    {
        float c = (1f - MathF.Abs(2f * l - 1f)) * s;
        float hp = (h % 360f) / 60f;
        float x = c * (1f - MathF.Abs(hp % 2f - 1f));
        (float r1, float g1, float b1) = Sector(hp, c, x);
        float m = l - c / 2f;
        return (r1 + m, g1 + m, b1 + m);
    }

    private static (float, float, float) Sector(float hp, float c, float x) => hp switch
    {
        < 1f => (c, x, 0f),
        < 2f => (x, c, 0f),
        < 3f => (0f, c, x),
        < 4f => (0f, x, c),
        < 5f => (x, 0f, c),
        _ => (c, 0f, x),
    };

    private static (float H, float S, float V) ToHsv(float r, float g, float b)
    {
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;
        float h = Hue(r, g, b, max, delta);
        float s = max == 0f ? 0f : delta / max;
        return (h, s, max);
    }

    private static (float H, float S, float L) ToHsl(float r, float g, float b)
    {
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;
        float h = Hue(r, g, b, max, delta);
        float l = (max + min) / 2f;
        float s = delta == 0f ? 0f : delta / (1f - MathF.Abs(2f * l - 1f));
        return (h, s, l);
    }

    private static float Hue(float r, float g, float b, float max, float delta)
    {
        if (delta == 0f) return 0f;
        float h;
        if (max == r) h = (g - b) / delta % 6f;
        else if (max == g) h = (b - r) / delta + 2f;
        else h = (r - g) / delta + 4f;
        h *= 60f;
        return h < 0f ? h + 360f : h;
    }

    private static float Clamp(float value, float low, float high) => float.IsNaN(value) ? low : Math.Clamp(value, low, high);

    private static int Truncate255(float value) => (int)Clamp(value, 0f, 255f);

    private static int Round255(float unit) => (int)Clamp(unit * 255f + 0.5f, 0f, 255f);

    private static int Alpha(float a) => float.IsNaN(a) ? 0 : (int)Math.Clamp(a, 0f, 255f);

    private static string Hex(int value) => value.ToString("x2", CultureInfo.InvariantCulture);
}
