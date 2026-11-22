using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// D176 (the owner, 2026-10-22: tg's flat projectile sprites do not work in first person): the client draws its own 3D shots, built from
// meshes and shaders (Render/ShotVisual.cs, ShotFlash.cs). One generic rule, no table per type: the kind comes from the type path's
// prefix, the colour from tg's light_color, else its color, else the mean colour of its own picture (docs/ai/specs/weapons.md section 4).

/// <summary>How a shot or a shot's effect is drawn (D176).</summary>
public enum ShotKind : byte
{
    /// <summary>Not a shot drawn in 3D: the picture (DV77, DV78) or a billboard.</summary>
    None,

    /// <summary>A laser or disabler bolt (<c>/obj/projectile/beam</c>): an elongated glowing capsule, white core and coloured rim.</summary>
    Bolt,

    /// <summary>The practice and rapid lasers: the same, shorter.</summary>
    ShortBolt,

    /// <summary>The electrode (<c>/obj/projectile/energy/electrode</c>): a jittering spark ball.</summary>
    Spark,

    /// <summary>The ion bolt (<c>/obj/projectile/ion</c>): a pulsing, crackling sphere.</summary>
    Ion,

    /// <summary>The temperature gun's bolt (<c>/obj/projectile/temp</c>, cold and hot): a sphere with a trail of motes.</summary>
    Temp,

    /// <summary>A bullet (<c>/obj/projectile/bullet</c>): a thin bright tracer streak, no light.</summary>
    Bullet,

    /// <summary>A shotgun pellet (<c>/obj/projectile/bullet/pellet</c>): thinner.</summary>
    Pellet,

    /// <summary>A hitscan tracer (<c>/obj/effect/projectile/tracer</c>, the emitter's beam): a glowing cylinder between its two ends.</summary>
    Tracer,

    /// <summary>A hitscan's muzzle flash (<c>/obj/effect/projectile/muzzle</c>): a star and a short light.</summary>
    Muzzle,

    /// <summary>An impact (<c>/obj/effect/projectile/impact</c>, <c>/obj/effect/temp_visual/impact_effect</c>): a burst and a short light.</summary>
    Impact,

    /// <summary>A gun's firing effect (<c>/obj/effect/temp_visual/dir_setting/firing_effect</c>): a muzzle flash, on the own gun for the own shot.</summary>
    Firing,
}

/// <summary>The kind, colour and size of a 3D shot (D176), and the own gun's kick and muzzle point.</summary>
public static class ShotLook
{
    /// <summary>
    /// The owner (2026-11-21): a launcher's shell, a 40mm grenade or a rocket (tg's <c>/obj/projectile/bullet/rocket</c>), is drawn as its own picture,
    /// <see cref="ShellSizeShare"/> times a projectile picture's size, with a fire trail behind it.
    /// </summary>
    public static bool IsShell(string typePath) => typePath != null && (Is(typePath, "/obj/projectile/bullet/a40mm") || Is(typePath, "/obj/projectile/bullet/rocket"));

    /// <summary>How much bigger a shell's picture is drawn than another projectile's picture.</summary>
    public const float ShellSizeShare = 3f;

    /// <summary>The kind of a thing of <paramref name="typePath"/>; <see cref="ShotKind.None"/> for any other.</summary>
    public static ShotKind KindOf(string typePath)
    {
        if (typePath == null)
            return ShotKind.None;
        if (Is(typePath, "/obj/projectile"))
        {
            if (Is(typePath, "/obj/projectile/beam/practice") || Is(typePath, "/obj/projectile/beam/laser/rapid"))
                return ShotKind.ShortBolt;
            if (Is(typePath, "/obj/projectile/beam"))
                return ShotKind.Bolt;
            if (Is(typePath, "/obj/projectile/energy/electrode"))
                return ShotKind.Spark;
            if (Is(typePath, "/obj/projectile/ion"))
                return ShotKind.Ion;
            if (Is(typePath, "/obj/projectile/temp"))
                return ShotKind.Temp;
            if (IsShell(typePath))
                return ShotKind.None; // a 40mm grenade or a rocket is its own picture flying its line, not a tracer streak
            if (Is(typePath, "/obj/projectile/bullet/pellet"))
                return ShotKind.Pellet;
            if (Is(typePath, "/obj/projectile/bullet"))
                return ShotKind.Bullet;
            return ShotKind.None;
        }
        if (Is(typePath, "/obj/effect/projectile/tracer"))
            return ShotKind.Tracer;
        if (Is(typePath, "/obj/effect/projectile/muzzle"))
            return ShotKind.Muzzle;
        if (Is(typePath, "/obj/effect/projectile/impact") || Is(typePath, "/obj/effect/temp_visual/impact_effect"))
            return ShotKind.Impact;
        if (Is(typePath, "/obj/effect/temp_visual/dir_setting/firing_effect"))
            return ShotKind.Firing;
        return ShotKind.None;
    }

    /// <summary>Whether the kind is a moving projectile (drawn on its line, DV78), not an effect.</summary>
    public static bool IsProjectile(ShotKind kind) => kind is >= ShotKind.Bolt and <= ShotKind.Pellet;

    // The type itself or one of its subtypes (a path that only starts with the same letters is not).
    private static bool Is(string path, string type)
        => path.StartsWith(type, StringComparison.Ordinal) && (path.Length == type.Length || path[type.Length] == '/');

    /// <summary>
    /// The shot's colour (0..1): tg's <c>light_color</c> when it has a light, else its <c>color</c> (<paramref name="appearanceRgba"/>,
    /// 0xRRGGBBAA, white for none), else the mean colour of its own picture's drawn pixels (<paramref name="picture"/>, R, G, B, A).
    /// </summary>
    public static (float R, float G, float B) Colour(in LightState light, uint appearanceRgba, byte[] picture)
    {
        if (!light.IsNone)
            return Unpack(light.Rgba);
        if ((appearanceRgba | 0xFF) != 0xFFFFFFFF)
            return Unpack(appearanceRgba);
        if (picture == null)
            return (1f, 1f, 1f);
        (float r, float g, float b, _) = SpriteColors.Mean(picture);
        return (r, g, b);
    }

    private static (float R, float G, float B) Unpack(uint rgba) => ((rgba >> 24) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f);

    /// <summary>
    /// A projectile's length along its flight and its radius, metres; a ball's length is its diameter. A tracer's length is its own (its two
    /// ends). The owner (2026-11-11): the first sizes (a 0.5 m bolt, a 0.3 m streak) were dots at DV211's speed, over a metre a frame.
    /// </summary>
    public static (float Length, float Radius) Size(ShotKind kind) => kind switch
    {
        ShotKind.Bolt => (1.2f, 0.045f),
        ShotKind.ShortBolt => (0.7f, 0.035f),
        ShotKind.Spark => (0.18f, 0.09f),
        ShotKind.Ion => (0.24f, 0.12f),
        ShotKind.Temp => (0.22f, 0.11f),
        ShotKind.Bullet => (0.6f, 0.018f),
        ShotKind.Pellet => (0.4f, 0.012f),
        ShotKind.Tracer => (0f, 0.035f),
        _ => (0f, 0f),
    };

    /// <summary>
    /// How big a bullet of <paramref name="typePath"/> is drawn beside the plain one (the owner, 2026-11-12: a pistol's and a rifle's bullet
    /// looked the same): its body's and its trail's sizes are multiplied by it. By tg's calibre in the type's name; 1 for any other shot.
    /// </summary>
    public static float Calibre(string typePath)
    {
        const string bullets = "/obj/projectile/bullet/";
        if (typePath == null || !typePath.StartsWith(bullets, StringComparison.Ordinal))
            return 1f;
        int end = typePath.IndexOf('/', bullets.Length);
        return (end < 0 ? typePath[bullets.Length..] : typePath[bullets.Length..end]) switch
        {
            "c9mm" or "c10mm" or "c38" or "c45" or "c46x30mm" or "c160smart" or "c3d" or "foam_dart" or "pea" => 0.7f,
            "a223" or "a7mm" or "n762" or "strilka310" or "c357" or "a50ae" or "lmg" or "rebar" => 1.4f,
            "shotgun_slug" or "shotgun_beanbag" or "shotgun_stunslug" or "shotgun_frag12" or "shotgun_breaching" => 1.7f,
            "p50" or "mm20x138" or "mm40" or "a40mm" or "cannonball" or "gyro" or "rocket" or "harpoon" or "ballista_spear" => 2.2f,
            _ => 1f,
        };
    }

    /// <summary>
    /// How far along its line a projectile is drawn at <paramref name="renderTick"/> (the owner, 2026-11-12 and 2026-11-20: a shot went in
    /// jerks, then started slow and sped up). <paramref name="samples"/> are the server's exact places of the shot (its tile and its pixel
    /// offsets, tg's <c>entry_x</c>/<c>entry_y</c>), oldest first, as (tick, distance along the line): the shot flies them at an even pace, so
    /// the drawn place is their straight line, run on before the first one and up to a tick past the last at its end's pace. Null while one
    /// sample does not yet give the pace.
    /// </summary>
    public static double? AlongAt(ReadOnlySpan<(double Tick, double Along)> samples, double renderTick)
    {
        int n = samples.Length;
        if (n == 0 || n == 1 && renderTick < samples[0].Tick)
            return null;
        if (n == 1)
            return samples[0].Along;
        int i = 0;
        while (i < n - 2 && renderTick > samples[i + 1].Tick)
            i++;
        (double t0, double a0) = samples[i];
        (double t1, double a1) = samples[i + 1];
        double at = Math.Min(renderTick, samples[n - 1].Tick + 1);
        return t1 <= t0 ? a1 : a0 + (a1 - a0) * (at - t0) / (t1 - t0);
    }

    /// <summary>A projectile's body is not drawn this near its line's start (the eye): the muzzle flash stands there (the owner, 2026-11-20).</summary>
    public const float HiddenNearMetres = 1.5f;

    /// <summary>
    /// How far from its line's start a projectile is not drawn, metres, when its line leaves the level's floor or ceiling <paramref name="exitMetres"/>
    /// out: <see cref="HiddenNearMetres"/>, but no more than half the way there, so a shot that hits a low ceiling or the floor near the shooter is seen
    /// flying into it (the owner, 2026-11-20: shots never seemed to reach the ceiling).
    /// </summary>
    public static float HiddenNear(double exitMetres) => (float)Math.Min(HiddenNearMetres, exitMetres / 2);

    /// <summary>The largest turn of a shot's line that is still the same flight: a homing shot steered (DV96), a glancing ricochet.</summary>
    public const double SameFlightDegrees = 30;

    /// <summary>
    /// A shot's line turned from <paramref name="fromDegrees"/> to <paramref name="toDegrees"/> (tg's angles) and the flight goes on: its places
    /// before the turn still give its pace and it stays drawn; a larger turn (a ricochet, a reflection) is a new flight.
    /// </summary>
    public static bool SameFlight(double fromDegrees, double toDegrees) => Math.Abs(Math.IEEERemainder(toDegrees - fromDegrees, 360)) <= SameFlightDegrees;

    /// <summary>A level's height in the server's tile pixels (32 to the 2 m tile): the wall height (the server's, protocol 91).</summary>
    public static double LevelPixels => WorldScale.WallHeight / TileSpace.TileSize * 32.0;

    /// <summary>
    /// The place of a projectile on its line, horizontal tile pixels from the line's start: <paramref name="along"/> (its tile's or its gliding
    /// node's nearest point of the line) kept ahead of the start and between the level's floor and its ceiling (<paramref name="ceilingPixels"/>,
    /// by default the level's height, the server's wall height; the line's height is <paramref name="heightPixels"/> at the start and rises by
    /// <paramref name="slope"/> a pixel). A tile's middle is up to half a tile off the line's start, so the nearest point lay behind the shooter or,
    /// on a steep shot, metres over the ceiling.
    /// </summary>
    public static double ClampAlong(double along, double heightPixels, double slope, double ceilingPixels = double.NaN)
    {
        if (double.IsNaN(ceilingPixels))
            ceilingPixels = LevelPixels;
        if (Math.Abs(slope) < 1e-6)
            return Math.Max(0, along);
        double a = -heightPixels / slope, b = (ceilingPixels - heightPixels) / slope;
        double low = Math.Max(0, Math.Min(a, b)), high = Math.Max(low, Math.Max(a, b));
        return Math.Clamp(along, low, high);
    }

    /// <summary>
    /// A projectile's trail from its gun to its head (Render/ShotTrace.cs; the owner, 2026-11-11: every shot has one, so its way is seen): its
    /// width in metres and how long it takes to die after the shot. An energy shot's is wider and stays longer than a bullet's.
    /// </summary>
    public static (float Width, float FadeSeconds) Trail(ShotKind kind) => kind switch
    {
        ShotKind.Bolt => (0.07f, 0.25f),
        ShotKind.ShortBolt => (0.05f, 0.22f),
        ShotKind.Spark or ShotKind.Ion or ShotKind.Temp => (0.06f, 0.22f),
        ShotKind.Pellet => (0.02f, 0.12f),
        _ => (0.03f, 0.15f), // the tail by the gun dissolves first (shot_trace.gdshader), so a shot fired on the run does not hang beside the runner (the owner, 2026-11-13)
    };

    /// <summary>The longest a tracer reaches back from its shot's head, metres (the owner, 2026-11-20: a long one showed a runner where the shots came from).</summary>
    public const float TrailMaxMetres = 4f;

    /// <summary>The mark a shot leaves where it hit (Render/ShotMark.cs): its radius in metres, a bullet's hole or an energy shot's scorch.</summary>
    public static float MarkRadius(bool bullet) => bullet ? 0.07f : 0.13f;

    /// <summary>tg's firing effect's <c>duration</c> (3 deciseconds, miscellaneous.dm:62): the muzzle flash's life.</summary>
    public const double FlashSeconds = 0.3;

    /// <summary>An impact's star and light.</summary>
    public const double ImpactLightSeconds = 0.25;

    /// <summary>A flash's strength (1 to 0) <paramref name="ageSeconds"/> into its <paramref name="lifeSeconds"/>: at once full, then fading.</summary>
    public static float Fade(double ageSeconds, double lifeSeconds)
        => ageSeconds < 0 || ageSeconds >= lifeSeconds ? 0f : (float)(1 - ageSeconds / lifeSeconds) * (float)(1 - ageSeconds / lifeSeconds);

    /// <summary>The own gun's fire kick: how far it is pushed back (0 at rest, 1 at the peak) <paramref name="ageSeconds"/> after the firing effect.</summary>
    public static float Kick(double ageSeconds) => Kick(ageSeconds, 0.03, 0.15);

    /// <summary>The kick's curve with its own times: out in <paramref name="rise"/> seconds, back over <paramref name="back"/> (DV96: a heavy gun's slower).</summary>
    public static float Kick(double ageSeconds, double rise, double back)
    {
        if (ageSeconds <= 0 || ageSeconds >= rise + back)
            return 0f;
        if (ageSeconds < rise)
            return (float)(ageSeconds / rise);
        double u = 1 - (ageSeconds - rise) / back;
        return (float)(u * u);
    }

    /// <summary>
    /// The own gun's muzzle in its picture (<paramref name="rgba"/>, rows from the top): the middle of the drawn pixels of the rightmost
    /// column that has any (tg's guns point east in the hand slot); false for an empty picture.
    /// </summary>
    public static bool TryMuzzle(byte[] rgba, int width, int height, out float x, out float y)
    {
        for (int column = width - 1; column >= 0; column--)
        {
            int first = -1, last = -1;
            for (int row = 0; row < height; row++)
            {
                if (rgba[(row * width + column) * 4 + 3] < 128)
                    continue;
                if (first < 0)
                    first = row;
                last = row;
            }
            if (first >= 0)
            {
                x = column + 1f;
                y = (first + last + 1) / 2f;
                return true;
            }
        }
        x = y = 0f;
        return false;
    }
}
