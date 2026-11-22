using System.Linq;
namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free. The graphics presets (scope 2i, D114, D168, D288): the numbers the renderer applies, and the settings the
// options' Graphics tab offers one by one (GraphicsSetting); a changed setting makes the player's own set, "custom".

/// <summary>One set of graphics settings: a named preset (<see cref="All"/>) or the player's own (<see cref="CustomName"/>).</summary>
public sealed record RenderPreset
{
    public string Name { get; init; } = CustomName;

    /// <summary>How far from the camera lamps are drawn, in tiles: their Godot lights fade out beyond (D288).</summary>
    public int LightDistance { get; init; } = 18;

    /// <summary>How many lamps cast shadows, the nearest first; a lamp beyond the budget is drawn without one only while the camera sees it (D288).</summary>
    public int ShadowedLights { get; init; } = 8;

    /// <summary>The positional shadow atlas's side in pixels (<see cref="ShadowAtlasPlan"/>).</summary>
    public int ShadowAtlas { get; init; } = 4096;

    /// <summary>Godot's soft shadow filter: 0 hard, 1 very low, 2 low, 3 medium, 4 high, 5 ultra (<c>RenderingServer.ShadowQuality</c>).</summary>
    public int ShadowFilter { get; init; } = 2;

    /// <summary>A lamp's shadow is a cube map (six passes); false: a dual paraboloid (two passes, seams).</summary>
    public bool CubeShadows { get; init; } = true;

    /// <summary>The sun's shadow, 0 off to 4 ultra (<see cref="SunShadowPlan"/>).</summary>
    public int SunShadow { get; init; } = 2;

    /// <summary>Which shadows hold the machines and the furniture (D288): 0 none (the walls alone), 1 the nearest lamps', 2 those and the sun's.
    /// Every shadow pass draws whole chunks of them, so they are the dearest shadows.</summary>
    public int ObjectShadows { get; init; } = 1;

    /// <summary>How many of the shadowed lamps nearest the camera hold the things and the characters in their shadows (the others' hold the walls
    /// alone; the owner, 2026-10-29: on max the crates ten metres off had none).</summary>
    public int ObjectShadowLamps { get; init; } = 4;

    /// <summary>The characters' pictures cast shadows too (each moving one makes the lamps near it redraw their shadows every frame).</summary>
    public bool CharacterShadows { get; init; } = true;

    /// <summary>Screen-space ambient occlusion (the client's AmbientOcclusionPass, over the grid-lit view): 0 off, 1 low, 2 medium, 3 high.</summary>
    public int Ssao { get; init; }

    /// <summary>Screen-space indirect light (the lamps' light bounced off what they light).</summary>
    public bool Ssil { get; init; }

    /// <summary>Screen-space reflections on the wet and the polished floors (TurfStyle.Polished).</summary>
    public bool Reflections { get; init; }

    /// <summary>The bloom around bright lamps and screens.</summary>
    public bool Glow { get; init; } = true;

    /// <summary>Multisample anti-aliasing: 0 off, 2, 4, 8.</summary>
    public int Msaa { get; init; } = 2;

    /// <summary>The 3D render scale in per cent; below 100 FSR upscales it.</summary>
    public int RenderScalePercent { get; init; } = 100;

    /// <summary>The camera's far plane in metres.</summary>
    public float ViewDistance { get; init; } = 100f;

    /// <summary>The volumetric fog that shows the beams of light (DV62): 0 off, 1 a coarse froxel grid, 2 a fine one. Off in every preset (the owner, 2026-10-24).</summary>
    public int Fog { get; init; }

    public float RenderScale => RenderScalePercent / 100f;

    public bool Fsr => RenderScalePercent < 100;

    public const string CustomName = "custom";

    public static readonly RenderPreset Low = new()
    {
        Name = "low", LightDistance = 12, ShadowedLights = 4, ShadowAtlas = 2048, ShadowFilter = 0, CubeShadows = false, SunShadow = 1, ObjectShadows = 0,
        CharacterShadows = false, Msaa = 0, RenderScalePercent = 75, ViewDistance = 60f,
    };

    public static readonly RenderPreset Medium = new() { Name = "medium", Ssao = 1 };

    public static readonly RenderPreset High = new()
    {
        Name = "high", LightDistance = 24, ShadowedLights = 16, ShadowFilter = 3, SunShadow = 3, ObjectShadowLamps = 8, Ssao = 2, Reflections = true, Msaa = 4, ViewDistance = 200f,
    };

    public static readonly RenderPreset Max = new()
    {
        Name = "max", LightDistance = 32, ShadowedLights = 32, ShadowAtlas = 8192, ShadowFilter = 4, SunShadow = 4, ObjectShadows = 2, ObjectShadowLamps = 16, Ssao = 3, Ssil = true, Reflections = true, Msaa = 4,
        ViewDistance = 300f,
    };

    public static readonly RenderPreset[] All = { Low, Medium, High, Max };

    /// <summary>The named preset (case does not matter); false for an unknown name and for <see cref="CustomName"/>.</summary>
    public static bool TryParse(string name, out RenderPreset preset)
    {
        preset = All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        return preset != null;
    }

    /// <summary>The named preset whose settings these are, else <see cref="CustomName"/>.</summary>
    public string Matching => All.FirstOrDefault(p => p with { Name = Name } == this)?.Name ?? CustomName;
}

/// <summary>One setting of the Graphics tab: its file id (<c>gfx.ID</c>), its title, the values it offers and how it reads and sets a <see cref="RenderPreset"/>.</summary>
public sealed record GraphicsSetting(string Id, string Title, (string Label, int Value)[] Choices, Func<RenderPreset, int> Get, Func<RenderPreset, int, RenderPreset> Set)
{
    private static readonly (string, int)[] OffOn = { ("off", 0), ("on", 1) };
    private static readonly (string, int)[] Levels = { ("off", 0), ("low", 1), ("medium", 2), ("high", 3) };

    public static readonly GraphicsSetting[] All =
    {
        new("light_distance", "Light distance", new[] { ("12 tiles", 12), ("18 tiles", 18), ("24 tiles", 24), ("32 tiles", 32), ("48 tiles", 48) },
            p => p.LightDistance, (p, v) => p with { LightDistance = v }),
        new("shadowed_lights", "Lamps with shadows", new[] { 0, 2, 4, 6, 8, 10, 12, 16, 24, 32 }.Select(n => (n.ToString(System.Globalization.CultureInfo.InvariantCulture), n)).ToArray(),
            p => p.ShadowedLights, (p, v) => p with { ShadowedLights = v }),
        new("shadow_atlas", "Shadow resolution", new[] { ("low", 1024), ("medium", 2048), ("high", 4096), ("ultra", 8192) },
            p => p.ShadowAtlas, (p, v) => p with { ShadowAtlas = v }),
        new("shadow_filter", "Shadow softness", new[] { ("hard", 0), ("very low", 1), ("low", 2), ("medium", 3), ("high", 4), ("ultra", 5) },
            p => p.ShadowFilter, (p, v) => p with { ShadowFilter = v }),
        new("cube_shadows", "Lamp shadows", new[] { ("fast (dual paraboloid)", 0), ("accurate (cube)", 1) },
            p => p.CubeShadows ? 1 : 0, (p, v) => p with { CubeShadows = v != 0 }),
        new("object_shadows", "Object shadows", new[] { ("off", 0), ("nearest lamps", 1), ("lamps and sun", 2) },
            p => p.ObjectShadows, (p, v) => p with { ObjectShadows = v }),
        new("object_shadow_lamps", "Lamps with object shadows", new[] { 2, 4, 8, 16, 32 }.Select(n => (n.ToString(System.Globalization.CultureInfo.InvariantCulture), n)).ToArray(),
            p => p.ObjectShadowLamps, (p, v) => p with { ObjectShadowLamps = v }),
        new("character_shadows", "Character shadows", OffOn, p => p.CharacterShadows ? 1 : 0, (p, v) => p with { CharacterShadows = v != 0 }),
        new("sun_shadow", "Sun shadows", new[] { ("off", 0), ("low", 1), ("medium", 2), ("high", 3), ("ultra", 4) },
            p => p.SunShadow, (p, v) => p with { SunShadow = v }),
        new("ssao", "Ambient occlusion", Levels, p => p.Ssao, (p, v) => p with { Ssao = v }),
        new("ssil", "Indirect light", OffOn, p => p.Ssil ? 1 : 0, (p, v) => p with { Ssil = v != 0 }),
        new("reflections", "Floor reflections", OffOn, p => p.Reflections ? 1 : 0, (p, v) => p with { Reflections = v != 0 }),
        new("glow", "Bloom", OffOn, p => p.Glow ? 1 : 0, (p, v) => p with { Glow = v != 0 }),
        new("msaa", "Anti-aliasing", new[] { ("off", 0), ("MSAA 2x", 2), ("MSAA 4x", 4), ("MSAA 8x", 8) }, p => p.Msaa, (p, v) => p with { Msaa = v }),
        new("render_scale", "Render scale", new[] { ("50% (FSR)", 50), ("67% (FSR)", 67), ("75% (FSR)", 75), ("100%", 100) },
            p => p.RenderScalePercent, (p, v) => p with { RenderScalePercent = v }),
        new("view_distance", "View distance", new[] { ("60 m", 60), ("100 m", 100), ("150 m", 150), ("200 m", 200), ("300 m", 300) },
            p => (int)p.ViewDistance, (p, v) => p with { ViewDistance = v }),
    };

    /// <summary>The setting with this id, or null.</summary>
    public static GraphicsSetting Find(string id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>Whether <paramref name="value"/> is one of the choices.</summary>
    public bool Offers(int value) => Choices.Any(c => c.Value == value);
}

/// <summary>
/// The positional shadow atlas for a count of shadowed lamps (D168, D288): <see cref="Size"/> pixels square, each of its four quadrants split
/// into 4, 16, 64 or 256 slots (<see cref="Quadrants"/>, Godot's <c>Viewport.PositionalShadowAtlasQuadrantSubdiv</c>). Godot puts an omni
/// light's shadow in two slots of one quadrant (its two halves) and a spot's in one, a light big on screen in a big slot, a small one in a
/// small slot or else a bigger one; a light with no free slot has no shadow. So the atlas holds two slots a lamp and one for the camera's own
/// flashlight spot, the last quadrants cut finer in turn until they do, and the nearest lamps keep the biggest slots.
/// </summary>
public sealed record ShadowAtlasPlan(int Size, int[] Quadrants)
{
    /// <summary>The slots of the whole atlas.</summary>
    public int Slots => Quadrants.Sum();

    /// <summary>The biggest slot's side in pixels.</summary>
    public int SlotPixels => Size / 2 / (int)Math.Sqrt(Quadrants.Min());

    /// <summary>The smallest slot's side in pixels.</summary>
    public int SmallestSlotPixels => Size / 2 / (int)Math.Sqrt(Quadrants.Max());

    /// <summary>The plan for <paramref name="shadowedLights"/> budgeted lamps in an atlas of <paramref name="size"/> pixels.</summary>
    public static ShadowAtlasPlan For(int shadowedLights, int size)
    {
        int need = 2 * Math.Max(0, shadowedLights) + 1;
        var quadrants = new[] { 4, 4, 4, 4 };
        for (int i = 3; quadrants.Sum() < need && quadrants[0] < 256; i = i == 0 ? 3 : i - 1)
            quadrants[i] *= 4;
        return new ShadowAtlasPlan(size, quadrants);
    }
}

/// <summary>The sun's shadow for a <see cref="RenderPreset.SunShadow"/> level: Godot's directional shadow atlas side, its splits (1, 2 or 4) and how far from the camera it reaches (metres); 0 splits: no shadow.</summary>
public readonly record struct SunShadowPlan(int Size, int Splits, float Distance)
{
    public static SunShadowPlan For(int level) => level switch
    {
        <= 0 => new SunShadowPlan(1024, 0, 0f),
        1 => new SunShadowPlan(2048, 2, 40f),
        2 => new SunShadowPlan(4096, 2, 60f),
        3 => new SunShadowPlan(4096, 4, 100f),
        _ => new SunShadowPlan(8192, 4, 150f),
    };
}
