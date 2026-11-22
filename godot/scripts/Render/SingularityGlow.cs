using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Render;

// DEVIATION(DV67): the owner's request (2026-10-19). tg's singularity lights its surroundings white (light_range 6) and is drawn above the lighting;
// in a dark first-person station it should be seen from afar in its own colours, and cast shadows. The client draws its light as a shadowed point
// light at its picture's middle instead of the grid's (LightLayer leaves its tg light off the grid, whose light casts no shadow of a thing): a long
// range, its colours the sprite's own. A point light has one colour, so the colours come from a projector: a coarse panorama of blobs in the
// sprite's hues, turning slowly, so the walls are washed in the swirl's reds, violets and blues; the light's own colour is their channels'
// brightest. Godot draws a light's projector only with its shadows on. It lights no fog (the owner: it looked poor).

/// <summary>The coloured light of one singularity, a child of its entity node.</summary>
public partial class SingularityGlow : OmniLight3D
{
    /// <summary>How far it lights (tiles).</summary>
    public const float RangeTiles = 40f;

    /// <summary>Its energy (the projector's colours are about a third as bright as white).</summary>
    public const float Energy = 10f;

    /// <summary>How strongly it lights the beam fog: not at all (the owner's play test, 2026-10-19: its fog looked poor, a lit sphere cut at the fog's reach).</summary>
    public const float FogEnergy = 0f;

    /// <summary>How many times as bright as its colours its picture glows (tg's sprite is mostly black with a dark blue or red swirl: drawn only as
    /// bright as itself it was a black blot in the dark, the owner's play test of 2026-10-19).</summary>
    public const float PictureGain = 1.5f;

    /// <summary>How fast its colours turn (radians a second).</summary>
    public const float TurnPerSecond = 0.25f;

    // The colours of the stage sprites (singularity.dmi s1, 96x96 s3, 160x160 s5, 224x224 s7: their main hues), for a picture with none.
    private static readonly Color[] Fallback =
    {
        new(1f, 0f, 0.05f), new(1f, 0f, 0.92f), new(0.89f, 0f, 1f), new(0.24f, 0f, 1f), new(0.03f, 0f, 1f),
    };

    private const int PanoramaWidth = 12, PanoramaHeight = 6;

    // The least share of the sprite's coloured pixels a hue needs to light a patch (a stray pixel's hue does not).
    private const float MinHueShare = 0.02f;

    private (int W, int H) _builtFor = (-1, -1);
    private ulong _seed;
    private float _turn;

    /// <summary>Makes the light under <paramref name="parent"/>; <paramref name="seed"/> picks its blobs (the entity's id: the same for every viewer).</summary>
    public static SingularityGlow Create(Node3D parent, long seed)
    {
        var glow = new SingularityGlow
        {
            Name = "Glow",
            ShadowEnabled = true,
            LightSpecular = 0.2f,
            OmniRange = RangeTiles * TileSpace.TileSize,
            OmniAttenuation = 0.8f, // a flatter falloff than a lamp's: it is seen down a lit corridor
            LightEnergy = Energy,
            LightVolumetricFogEnergy = FogEnergy,
            LightColor = Colors.White,
            _seed = (ulong)seed,
        };
        parent.AddChild(glow);
        return glow;
    }

    /// <summary>Places the light at the picture's middle and colours it from <paramref name="picture"/> (again when the stage, its size, changes); true when it was coloured anew.</summary>
    public bool Set(Vector3 pictureMiddle, Raster picture)
    {
        Position = pictureMiddle;
        if (picture == null || (picture.Width, picture.Height) == _builtFor)
            return false;
        _builtFor = (picture.Width, picture.Height);
        // The beam fog takes the light's own colour, not its projector's: the light is the hues' brightest of each channel (a red and a blue
        // make magenta), and the projector each hue over it, so the walls get the hues and the fog their sum.
        List<Color> colours = Colours(picture);
        var top = new Color(0f, 0f, 0f);
        foreach (Color c in colours)
            top = new Color(Mathf.Max(top.R, c.R), Mathf.Max(top.G, c.G), Mathf.Max(top.B, c.B));
        LightColor = top;
        for (int i = 0; i < colours.Count; i++)
        {
            Color c = colours[i];
            colours[i] = new Color(Over(c.R, top.R), Over(c.G, top.G), Over(c.B, top.B));
        }
        LightProjector = Panorama(colours, _seed);
        return true;
    }

    private static float Over(float part, float whole) => whole > 1e-3f ? Mathf.Min(part / whole, 1f) : 0f;

    public override void _Process(double delta)
    {
        // The swirl turns the blobs around the light, tilted a little so they also rise and fall on the walls.
        _turn = (_turn + TurnPerSecond * (float)delta) % Mathf.Tau;
        Rotation = new Vector3(0.3f * Mathf.Sin(_turn * 0.7f), _turn, 0.2f * Mathf.Cos(_turn * 0.5f));
    }

    // The sprite's hues: its coloured pixels (opaque enough, saturated, not near black) in twelve bins of hue, each bin with a share of them
    // (MinHueShare) one colour, their mean brightened to full strength; so a sprite mostly red with a violet and a blue rim lights red, violet
    // and blue patches alike (by its pixels' count the light would be one red).
    private static List<Color> Colours(Raster picture)
    {
        const int bins = 12;
        var sum = new Vector3[bins];
        var count = new int[bins];
        int total = 0;
        byte[] px = picture.Pixels;
        for (int i = 0; i + 3 < px.Length; i += 4)
        {
            if (px[i + 3] < 128)
                continue;
            var c = new Color(px[i] / 255f, px[i + 1] / 255f, px[i + 2] / 255f);
            float top = Mathf.Max(c.R, Mathf.Max(c.G, c.B)), bottom = Mathf.Min(c.R, Mathf.Min(c.G, c.B));
            if (top < 0.15f || (top - bottom) / top < 0.35f)
                continue;
            int bin = Mathf.PosMod((int)(c.H * bins), bins);
            sum[bin] += new Vector3(c.R / top, c.G / top, c.B / top);
            count[bin]++;
            total++;
        }
        var colours = new List<Color>();
        for (int bin = 0; bin < bins; bin++)
        {
            if (count[bin] == 0 || count[bin] < total * MinHueShare)
                continue;
            Vector3 mean = sum[bin] / count[bin];
            float top = Mathf.Max(mean.X, Mathf.Max(mean.Y, mean.Z));
            colours.Add(new Color(mean.X / top, mean.Y / top, mean.Z / top));
        }
        if (colours.Count == 0)
            colours.AddRange(Fallback);
        return colours;
    }

    // A coarse panorama of blobs: each texel one of the sprite's hues (the filter blends them into soft patches), a few of them dark so the
    // light is patchy rather than an even purple.
    private static ImageTexture Panorama(List<Color> colours, ulong seed)
    {
        var random = new RandomNumberGenerator { Seed = seed * 2654435761UL + 1UL };
        var image = Image.CreateEmpty(PanoramaWidth, PanoramaHeight, false, Image.Format.Rgb8);
        for (int y = 0; y < PanoramaHeight; y++)
            for (int x = 0; x < PanoramaWidth; x++)
            {
                Color c = colours[random.RandiRange(0, colours.Count - 1)];
                float strength = random.Randf() < 0.2f ? 0.25f : 1f;
                image.SetPixel(x, y, c * strength);
            }
        return ImageTexture.CreateFromImage(image);
    }
}
