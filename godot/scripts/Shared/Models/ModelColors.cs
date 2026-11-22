using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared.Models;

/// <summary>
/// The colour of a plain (untextured) model material (docs/ai/specs/models.md, "Textures"): one named <c>Gags1</c>, <c>Gags2</c> ... takes that colour of the
/// thing's GAGS key (<c>gags:/datum/greyscale_config/canister/stripe|#2786e5#e8fefe</c>: a canister's shell and stripes, by its gas); one named <c>Flat</c> is
/// its authored colour (glTF's linear factor back to sRGB); any other keeps the linear factor as its swatch, as the glass and the doors' edges were made for.
/// One named <c>Tint_</c> is blended toward the part's tint by the tint's alpha (a canister's held tank toward the tank's own colour, its window toward the
/// gases inside; <see cref="Blend"/>); the tint <c>0xFFFFFFFF</c> leaves it as it is.
/// </summary>
public static class ModelColors
{
    /// <summary>The tint that changes nothing.</summary>
    public const uint NoTint = 0xFFFFFFFFu;

    /// <summary>The <c>0xRRGGBBAA</c> (sRGB) the material's swatch is made of, by its name, its glTF factor, the thing's icon and the part's tint.</summary>
    public static uint Swatch(string material, System.Numerics.Vector4 linear, string icon, uint tint = NoTint, System.Numerics.Vector3? color = null)
    {
        if (color is System.Numerics.Vector3 rgb)
            return Rgba(rgb.X, rgb.Y, rgb.Z);
        if (TryGags(material, icon, out uint gags))
            return gags;
        bool flat = material != null && material.Contains("Flat", StringComparison.Ordinal);
        float r = flat ? Srgb(linear.X) : linear.X, g = flat ? Srgb(linear.Y) : linear.Y, b = flat ? Srgb(linear.Z) : linear.Z;
        if (tint != NoTint && material != null && material.Contains("Tint_", StringComparison.Ordinal))
        {
            float t = (tint & 0xFF) / 255f;
            r += ((tint >> 24) / 255f - r) * t;
            g += (((tint >> 16) & 0xFF) / 255f - g) * t;
            b += (((tint >> 8) & 0xFF) / 255f - b) * t;
        }
        return Rgba(r, g, b);
    }

    /// <summary>
    /// Pictures laid over each other, the first at the bottom (sRGB colours, each with its own opacity): the colour they make and, in the alpha, how much
    /// they cover; rounded (an eighth of a step) so that a part built for it is shared by nearly equal ones. Nothing: alpha 0.
    /// </summary>
    public static uint Blend(IEnumerable<(float R, float G, float B, float A)> layers)
    {
        float r = 0, g = 0, b = 0, a = 0;
        foreach ((float lr, float lg, float lb, float la) in layers)
        {
            float k = Math.Clamp(la, 0f, 1f);
            float over = a * (1f - k);
            float total = k + over;
            if (total <= 0f)
                continue;
            r = (lr * k + r * over) / total;
            g = (lg * k + g * over) / total;
            b = (lb * k + b * over) / total;
            a = total;
        }
        static uint Step(float v, int steps) => (uint)(MathF.Round(Math.Clamp(v, 0f, 1f) * steps) * 255f / steps);
        return (Step(r, 32) << 24) | (Step(g, 32) << 16) | (Step(b, 32) << 8) | Step(a, 16);
    }

    /// <summary>The GAGS key's colour a material named <c>Gags</c>N takes (N from 1); false for another material, an icon that is no GAGS key, or a missing colour.</summary>
    public static bool TryGags(string material, string icon, out uint rgba)
    {
        rgba = 0;
        int at = material?.IndexOf("Gags", StringComparison.Ordinal) ?? -1;
        if (at < 0 || at + 4 >= material.Length || !char.IsAsciiDigit(material[at + 4]) || icon == null
            || !icon.StartsWith("gags:", StringComparison.Ordinal) || icon.IndexOf('|') is var bar && bar < 0)
            return false;
        List<uint> colors = GagsColors.Parse(icon[(bar + 1)..]);
        int index = material[at + 4] - '1';
        if (index < 0 || index >= colors.Count)
            return false;
        rgba = colors[index] | 0xFFu;
        return true;
    }

    private static uint Rgba(float r, float g, float b) => (Byte(r) << 24) | (Byte(g) << 16) | (Byte(b) << 8) | 0xFFu;

    private static uint Byte(float v) => (uint)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);

    private static float Srgb(float linear)
        => linear <= 0.0031308f ? 12.92f * linear : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
}
