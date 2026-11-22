namespace Anomaly.Client.Shared.Light;

// Owner: M3 P7. Engine-free (compiled into the client and, linked, into tests/Anomaly.Server.Tests/Client/Light).
// tg snapshot 1d99bd7f. The numbers of tg's lighting, ported as they are (D86).

/// <summary>
/// tg's corner light math (<c>code/modules/lighting/lighting_source.dm</c>, <c>lighting_corner.dm</c>,
/// <c>__DEFINES/lighting.dm</c>) as pure functions. Single precision like DM's numbers.
/// </summary>
public static class TgLightMath
{
    /// <summary>tg: <c>LIGHTING_HEIGHT</c> (<c>__DEFINES/lighting.dm:51</c>), a light's default height.</summary>
    public const float LightingHeight = 1f;

    /// <summary>tg: <c>LIGHTING_HEIGHT_SPACE</c> (<c>__DEFINES/lighting.dm:47</c>), the height of starlight, under the floor.</summary>
    public const float LightingHeightSpace = -0.5f;

    /// <summary>tg: <c>LIGHTING_ROUND_VALUE</c> (<c>__DEFINES/lighting.dm:53</c>): a corner's value is rounded to 1/64.</summary>
    public const float RoundValue = 1f / 64f;

    /// <summary>tg: <c>COMPLEX_LIGHT</c> (<c>__DEFINES/lighting.dm:4</c>): the corner light this grid computes.</summary>
    public const byte ComplexLight = 1;

    /// <summary>tg: <c>OVERLAY_LIGHT_BEAM</c> (<c>__DEFINES/lighting.dm:10</c>), the last of tg's overlay lights (2 to 4).</summary>
    public const byte OverlayLightBeam = 4;

    /// <summary>tg: <c>OVERLAY_LIGHT_DIRECTIONAL</c> (<c>__DEFINES/lighting.dm:8</c>): a flashlight's light, cast ahead of its holder.</summary>
    public const byte OverlayLightDirectional = 3;

    /// <summary>
    /// Whether the grid draws a source of this <c>light_system</c>: tg's corner light, and tg's overlay lights (a flashlight,
    /// a welder, sparks) drawn with the same math at the centre the server gives (DV61).
    /// </summary>
    public static bool OnGrid(byte system) => system >= ComplexLight && system <= OverlayLightBeam;

    /// <summary>
    /// tg: <c>/datum/light_source/proc/falloff_at_coord</c> (<c>lighting_source.dm:271-296</c>). (<paramref name="x"/>,
    /// <paramref name="y"/>) is the corner's offset from the source in tiles, already shifted by the visual offset the way
    /// <c>generate_sheet</c> (<c>:243-258</c>) shifts it; <paramref name="z"/> the level difference (0 on one level). Returns
    /// 0 (dark) to 1 (fully lit).
    /// </summary>
    public static float FalloffAtCoord(float x, float y, float z, float range, int centerDir, float angle, float height)
    {
        float rangeDivisor = MathF.Max(1f, range);

        // lighting_source.dm:278: the distance has height under the root, not squared.
        float multiplier = 1f - Math.Clamp(MathF.Sqrt(x * x + y * y + z * z + height) / rangeDivisor, 0f, 1f);
        if (angle >= 360f || angle <= 0f)
            return multiplier;

        // :283-: the cone around the light's direction.
        float coordAngle = DeltaToAngle(x, y);
        float centerAngle = Dir2Angle(centerDir);
        float angleDelta = MathF.Abs(centerAngle - coordAngle);
        if (angleDelta > 180f)
            angleDelta = 180f - (angleDelta - 180f);
        return MathF.Max(multiplier * (1f - MathF.Max(angleDelta - angle / 2f, 0f) / 30f), 0f);
    }

    /// <summary>tg: <c>delta_to_angle</c> (<c>__HELPERS/maths.dm:10-17</c>): the compass bearing (0 north, clockwise) of an offset.</summary>
    public static float DeltaToAngle(float x, float y)
    {
        if (y == 0f)
            return x >= 0f ? 90f : 270f;
        float angle = MathF.Atan(x / y) * (180f / MathF.PI);
        if (y < 0f)
            angle += 180f;
        else if (x < 0f)
            angle += 360f;
        return angle;
    }

    /// <summary>tg: <c>dir2angle</c> (<c>__HELPERS/type2type.dm:80-99</c>) for BYOND's direction flags (N 1, S 2, E 4, W 8); null (no direction) is read as 0 like the DM arithmetic does.</summary>
    public static float Dir2Angle(int dir) => dir switch
    {
        1 => 0f,
        2 => 180f,
        4 => 90f,
        8 => 270f,
        5 => 45f,
        6 => 135f,
        9 => 315f,
        10 => 225f,
        _ => 0f,
    };

    /// <summary>
    /// tg's <c>round(value, LIGHTING_ROUND_VALUE)</c> of <c>lighting_corner.dm</c> <c>update_objects</c> (<c>:108-110</c>):
    /// DM rounds to the nearest multiple, halves up.
    /// </summary>
    public static float Round(float value) => MathF.Floor(value / RoundValue + 0.5f) * RoundValue;

    /// <summary>
    /// The displayed value of a corner's raw sums (<c>lighting_corner.dm:92-110</c>, <c>update_objects</c>): when the largest
    /// channel is above 1 all three are scaled down by it, then each is rounded to 1/64 (<c>LIGHTING_SOFT_THRESHOLD</c> is 0,
    /// so there is no soft floor). Negative sums (darkening lights) stay negative here; the consumer clamps.
    /// </summary>
    public static void Normalize(float r, float g, float b, out float cr, out float cg, out float cb)
    {
        float largest = MathF.Max(r, MathF.Max(g, b));
        float factor = largest > 1f ? 1f / largest : 1f;
        cr = Round(r * factor);
        cg = Round(g * factor);
        cb = Round(b * factor);
    }

    /// <summary>The sheet's side for a source: tg's <c>bound_range = ceil(range) + visual_offset</c> (<c>lighting_source.dm:247</c>) with <c>range = max(1, light_range)</c> (<c>:228</c>).</summary>
    public static int BoundRange(float lightRange, int visualOffset) => (int)MathF.Ceiling(MathF.Max(1f, lightRange)) + visualOffset;

    /// <summary>tg: <c>visual_offset = max(ceil(abs(offset_x)), ceil(abs(offset_y)))</c> (<c>lighting_source.dm:450</c>).</summary>
    public static int VisualOffset(float offsetX, float offsetY) => (int)MathF.Max(MathF.Ceiling(MathF.Abs(offsetX)), MathF.Ceiling(MathF.Abs(offsetY)));

    /// <summary>tg: the radius of <c>view()</c> in <c>impacted_corners</c> (<c>lighting_source.dm:471</c>): <c>ceil(light_range + visual_offset)</c>.</summary>
    public static int WorkingRange(float lightRange, int visualOffset) => (int)MathF.Ceiling(lightRange + visualOffset);
}
