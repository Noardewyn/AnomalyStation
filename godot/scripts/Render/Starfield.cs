using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// The background of space (D116): a black sky with stars; the sky gives no light (no ambient, no reflections). Over the stars, tg's parallax pictures
/// (icons/effects/parallax.dmi) as far bodies: the lava planet with its ice moon and rocks below the station, the ice moon above, single rocks of its asteroids state scattered as a field beside it,
/// and the sun at tg's <c>SSsun.azimuth</c>, <see cref="ModelRules.SunElevationDegrees"/> high (the solar panels' heads face it).
/// </summary>
public static class Starfield
{
    // the sun walks to tg's azimuth no faster than this: SSsun steps 6 degrees (up to 12) once a minute (sun.dm), the sky glides
    private const float SunDegreesPerSecond = 0.25f;

    private static ShaderMaterial _material;
    private static float _sunShown = float.NaN;
    private static int _planetIndex;
    private static ShaderMaterial _flare;
    private static MeshInstance3D _flareNode;

    /// <summary>Where the sky draws the sun (a unit vector, the world's frame).</summary>
    public static Vector3 SunDirection { get; private set; } = Vector3.Up;

    // The planet below, and the ice moon's orbit about it (arbitrary units, the station at the origin): a circle through the sky above the station
    // and behind the planet below, so the moon grows as it nears the station and shrinks away, and the planet hides it there.
    private static readonly Vector3 PlanetDirection = Vector3.Down; // straight below the station
    private const float PlanetHalfDegrees = 54.6f, PlanetDistance = 6f, OrbitRadius = 10f, MoonRadius = 1f, MoonOrbitSeconds = 1200f;
    // the icemoon state's disc fills this much of its picture's half
    private const float MoonDiscOfPicture = 0.475f;
    private static readonly Vector3 OrbitAcross = new Vector3(1f, 0f, 0.6f).Normalized();

    private static Godot.Environment _environment;
    private static bool _ashen;
    private static Sky _spaceSky; // the game's own sky: the shader's
    private static ShaderMaterial _colours; // a map's sky of two colours (sky_colours.gdshader), while it is the sky

    /// <summary>The sun is shown at the next azimuth at once (the map editor moved it; a round's sun glides).</summary>
    public static void SnapSun() => _sunShown = float.NaN;
    private static MapLook _look; // the map's own sky and sun (protocol 87); null: the game's
    private static bool _lookSet;

    /// <summary>How high the sun stands over the map's plane, in degrees: the map's own, else the game's.</summary>
    public static float SunElevation => _look?.SunElevation ?? ModelRules.SunElevationDegrees;

    /// <summary>Whether the map has a sun (a map may have none).</summary>
    public static bool SunOn => _look?.Sun ?? true;

    /// <summary>
    /// The map's own sky and sun (protocol 87, docs/ai/specs/map-editor.md 5.10; null: the game's): the stars alone, a planet's sky of two colours or
    /// the mapper's picture in the place of space; how bright the stars are; the sun's disc, its colour and its height, or no sun.
    /// </summary>
    public static void SetLook(MapLook look)
    {
        if (_environment == null || _material == null || (_lookSet && Equals(look, _look)))
            return;
        (_look, _lookSet) = (look, true);
        byte kind = look?.Sky ?? MapLook.SkySpace;
        Color sun = look == null ? new Color(1f, 0.92f, 0.78f) : Rgb(look.SunColour);
        _material.SetShaderParameter("brightness", look?.Stars ?? 1f);
        _material.SetShaderParameter("sun_size", look == null ? 0.035f : Mathf.DegToRad(look.SunSize));
        _material.SetShaderParameter("sun_on", SunOn);
        _material.SetShaderParameter("sun_glow", new Vector3(sun.R, sun.G, sun.B));
        _material.SetShaderParameter("sun_disc", look == null ? new Vector3(4f, 3.7f, 3.2f) : new Vector3(sun.R, sun.G, sun.B) * 4f);
        _material.SetShaderParameter("bodies_on", kind == MapLook.SkySpace);
        Sky sky = _spaceSky;
        _colours = null;
        if (kind == MapLook.SkyColours)
        {
            Color top = Rgb(look.SkyTop), horizon = Rgb(look.SkyHorizon), ground = horizon.Darkened(0.45f);
            _colours = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/sky_colours.gdshader") };
            _colours.SetShaderParameter("top_color", new Vector3(top.R, top.G, top.B));
            _colours.SetShaderParameter("horizon_color", new Vector3(horizon.R, horizon.G, horizon.B));
            _colours.SetShaderParameter("ground_color", new Vector3(ground.R, ground.G, ground.B));
            _colours.SetShaderParameter("sun_size", Mathf.DegToRad(look.SunSize));
            _colours.SetShaderParameter("sun_on", look.Sun);
            _colours.SetShaderParameter("sun_glow", new Vector3(sun.R, sun.G, sun.B));
            _colours.SetShaderParameter("sun_disc", new Vector3(sun.R, sun.G, sun.B) * 4f);
            _colours.SetShaderParameter("sun_dir", SunDirection);
            sky = new Sky { SkyMaterial = _colours, RadianceSize = Sky.RadianceSizeEnum.Size32 };
        }
        else if (kind == MapLook.SkyImage && Picture(look.Image) is { } picture)
        {
            sky = new Sky { SkyMaterial = new PanoramaSkyMaterial { Panorama = picture }, RadianceSize = Sky.RadianceSizeEnum.Size32 };
        }
        _environment.Sky = sky;
        if (!float.IsNaN(_sunShown))
        {
            SunDirection = Direction(_sunShown, SunElevation);
            _material.SetShaderParameter("sun_dir", SunDirection);
            _colours?.SetShaderParameter("sun_dir", SunDirection);
        }
    }

    private static Color Rgb(uint rgb) => new((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f);

    // A sky's picture from a png or a jpg file's bytes; null for bytes the engine does not read.
    private static ImageTexture Picture(byte[] file)
    {
        if (file == null || file.Length < 8)
            return null;
        var image = new Image();
        Error read = file[0] == 0xFF ? image.LoadJpgFromBuffer(file) : image.LoadPngFromBuffer(file);
        if (read != Error.Ok || image.IsEmpty())
        {
            GD.PushWarning("Starfield: the map's sky picture could not be read (" + read + "); the sky is the game's own.");
            return null;
        }
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>D305: Lavaland's sky, the ash clouds over the lava (tg draws no space behind a ZTRAIT_NOPARALLAX level).</summary>
    private static readonly Color AshSky = new(0.075f, 0.045f, 0.035f);

    private static float _hyperAmount;
    private static float _hyperPhase;

    /// <summary>Hyperspace outside (<paramref name="flow"/> is where the view streams, the world's frame; null: normal space): the sky fades over a second.</summary>
    public static void SetHyperspace(Vector3? flow, double dt)
    {
        _hyperAmount = Mathf.MoveToward(_hyperAmount, flow.HasValue ? 1f : 0f, (float)dt);
        _hyperPhase = (_hyperPhase + (float)dt) % 1000f;
        if (flow.HasValue)
            _material.SetShaderParameter("hyper_flow", flow.Value);
        _material.SetShaderParameter("hyper_amount", _hyperAmount);
        _material.SetShaderParameter("hyper_phase", _hyperPhase);
    }

    /// <summary>Replaces the environment's background with the starfield and switches the ambient and reflected light off.</summary>
    public static void Apply(Godot.Environment environment)
    {
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/starfield.gdshader") };
        _sunShown = float.NaN;
        PlaceBodies(_material);
        SunDirection = Direction(0f, ModelRules.SunElevationDegrees); // a placeholder: the first azimuth the server sends is taken at once (SetSun)
        _material.SetShaderParameter("sun_dir", SunDirection);
        SetMoon(0.0);
        _environment = environment;
        _ashen = false;
        (_look, _lookSet) = (null, false);
        environment.BackgroundColor = AshSky;
        environment.Sky = _spaceSky = new Sky { SkyMaterial = _material, RadianceSize = Sky.RadianceSizeEnum.Size32 }; // its uniforms change every frame; nothing reflects it (D116)
        environment.BackgroundMode = Godot.Environment.BGMode.Sky;
        environment.AmbientLightSource = Godot.Environment.AmbientSource.Disabled;
        environment.ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled;
    }

    /// <summary>D305: on a level with tg's ZTRAIT_NOPARALLAX (Lavaland) the sky is the dark ash in place of the starfield, with no sun and no flare.</summary>
    public static void SetAshen(bool ashen)
    {
        if (_ashen == ashen || _environment == null)
            return;
        _ashen = ashen;
        _environment.BackgroundMode = ashen ? Godot.Environment.BGMode.Color : Godot.Environment.BGMode.Sky;
    }

    /// <summary>Moves the sun toward tg's <paramref name="azimuth"/> (degrees clockwise from north; NaN: no sun yet, it stays); the first one is taken at once.</summary>
    public static void SetSun(float azimuth, double dt)
    {
        if (_material == null || float.IsNaN(azimuth))
            return;
        if (float.IsNaN(_sunShown))
            _sunShown = azimuth;
        float diff = Mathf.Wrap(azimuth - _sunShown, -180f, 180f);
        _sunShown = Mathf.Wrap(_sunShown + Math.Clamp(diff, -SunDegreesPerSecond * (float)dt, SunDegreesPerSecond * (float)dt), 0f, 360f);
        SunDirection = Direction(_sunShown, SunElevation);
        _material.SetShaderParameter("sun_dir", SunDirection);
        _colours?.SetShaderParameter("sun_dir", SunDirection);
    }

    /// <summary>Places the ice moon on its orbit at <paramref name="seconds"/> of the round's clock (the server's ticks: every client sees it at one place).</summary>
    public static void SetMoon(double seconds)
    {
        if (_material == null)
            return;
        double angle = seconds / MoonOrbitSeconds * Math.Tau;
        Vector3 planet = PlanetDirection * PlanetDistance;
        Vector3 at = planet + OrbitRadius * ((float)Math.Cos(angle) * Vector3.Up + (float)Math.Sin(angle) * OrbitAcross);
        float distance = at.Length();
        float scale = MoonDiscOfPicture * distance / MoonRadius; // 1 / tan of the picture's half angle
        _material.SetShaderParameter("moon_center", at / distance);
        _material.SetShaderParameter("moon_scale", new Vector2(scale, scale));
        _material.SetShaderParameter("moon_before", distance > PlanetDistance ? _planetIndex : int.MaxValue);
    }

    /// <summary>
    /// The sun's lens flare (sun_flare.gdshader): a quad that fills the screen whatever the camera, drawn last and added over the picture, as
    /// FovShadow's is placed.
    /// </summary>
    public static void AddFlare(Node parent)
    {
        _flare = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/sun_flare.gdshader"), RenderPriority = (int)Material.RenderPriorityMax };
        parent.AddChild(_flareNode = new MeshInstance3D
        {
            Name = "SunFlare",
            Layers = Light.LightLayer.MainViewLayer,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
            CustomAabb = new Aabb(new Vector3(-1e5f, -1e5f, -1e5f), new Vector3(2e5f, 2e5f, 2e5f)), // placed by its shader: never culled
            MaterialOverride = _flare,
        });
    }

    /// <summary>
    /// The flare follows the sun the sky shows: the whole of it in the first-person view, its streak only in the third-person one (its ghosts strung
    /// through the screen's centre look wrong from behind the mob), none in the free camera.
    /// </summary>
    public static void UpdateFlare(CameraMode mode)
    {
        if (_flareNode == null || !GodotObject.IsInstanceValid(_flareNode))
            return;
        _flareNode.Visible = mode != CameraMode.Free && _hyperAmount <= 0f && !_ashen && SunOn; // no sun in the hyperspace tunnel or under Lavaland's ash, nor on a map that has none

        _flare.SetShaderParameter("ghosts", mode == CameraMode.FirstPerson);
        _flare.SetShaderParameter("sun_dir", SunDirection);
    }

    // tg's bearing (clockwise from north) and a height over the station's plane: the map's north is -Z, east +X (TileSpace).
    private static Vector3 Direction(float azimuthDegrees, float elevationDegrees)
    {
        float a = Mathf.DegToRad(azimuthDegrees), e = Mathf.DegToRad(elevationDegrees);
        return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), -Mathf.Cos(a) * Mathf.Cos(e));
    }

    // parallax.dmi is a 3x3 sheet of 480 px states: asteroids (1, 1), icemoon (2, 1), planet (0, 2). Its picture, in sheet pixels.
    private static Rect2 State(int col, int row) => new(col * 480, row * 480, 480, 480);

    // The whole rocks of the asteroids state (its pixel boxes; the tile cuts the others at its edges), each drawn alone in the field.
    private static readonly Rect2[] Rocks =
    {
        new(33, 230, 106, 111), new(161, 35, 89, 89), new(147, 365, 71, 56), new(354, 353, 58, 50), new(55, 105, 58, 50),
        new(284, 43, 59, 51), new(219, 270, 59, 51), new(109, 35, 19, 18), new(255, 97, 19, 18), new(438, 109, 19, 18),
        new(109, 182, 19, 18), new(195, 223, 19, 18), new(348, 293, 19, 18), new(438, 363, 19, 18), new(277, 408, 19, 18),
        new(55, 50, 17, 16), new(412, 75, 17, 16), new(433, 314, 17, 16),
    };

    // the asteroid field: east of the station, a little above its plane
    private const float FieldAzimuth = 95f, FieldElevation = 4f, FieldWidth = 44f, FieldHeight = 14f, RockDegreesPerPixel = 0.08f;

    private static void PlaceBodies(ShaderMaterial material)
    {
        string sheet = Path.Combine(DataPaths.Find("assets/tg"), "icons", "effects", "parallax.png");
        Image image = File.Exists(sheet) ? Image.LoadFromFile(sheet) : null;
        if (image == null)
        {
            GD.PushWarning($"Starfield: {sheet} is missing; the sky has stars and the sun only.");
            return;
        }
        material.SetShaderParameter("sheet", ImageTexture.CreateFromImage(image));
        var bodies = new List<(Vector3 Center, Vector2 HalfDegrees, Rect2 Pixels, float Gain)>();
        // the rocks first: the planet and the moon are drawn over them
        var random = new Random(1997); // a fixed field: every client sees the same sky
        foreach (Rect2 rock in Rocks)
        {
            float azimuth = FieldAzimuth + ((float)random.NextDouble() - 0.5f) * FieldWidth;
            float elevation = FieldElevation + ((float)random.NextDouble() - 0.5f) * FieldHeight;
            float scale = RockDegreesPerPixel * (0.7f + 0.6f * (float)random.NextDouble()); // nearer and farther rocks
            bodies.Add((Direction(azimuth, elevation), rock.Size * scale / 2f, new Rect2(rock.Position + State(1, 1).Position, rock.Size), 1.8f));
        }
        bodies.Add((PlanetDirection, new Vector2(PlanetHalfDegrees, PlanetHalfDegrees), State(0, 2), 1f)); // the planet (lavaland) with its own small moon, below
        _planetIndex = bodies.Count - 1;
        Rect2 moon = State(2, 1);
        material.SetShaderParameter("moon_rect", new Vector4(moon.Position.X, moon.Position.Y, moon.Size.X, moon.Size.Y) / image.GetWidth());
        var centers = new Vector3[bodies.Count];
        var scales = new Vector2[bodies.Count];
        var rects = new Vector4[bodies.Count];
        var gains = new float[bodies.Count];
        for (int i = 0; i < bodies.Count; i++)
        {
            centers[i] = bodies[i].Center;
            scales[i] = new Vector2(1f / Mathf.Tan(Mathf.DegToRad(bodies[i].HalfDegrees.X)), 1f / Mathf.Tan(Mathf.DegToRad(bodies[i].HalfDegrees.Y)));
            Rect2 px = bodies[i].Pixels;
            rects[i] = new Vector4(px.Position.X, px.Position.Y, px.Size.X, px.Size.Y) / image.GetWidth();
            gains[i] = bodies[i].Gain;
        }
        material.SetShaderParameter("body_count", bodies.Count);
        material.SetShaderParameter("body_center", centers);
        material.SetShaderParameter("body_scale", scales);
        material.SetShaderParameter("body_rect", rects);
        material.SetShaderParameter("body_gain", gains);
    }
}
