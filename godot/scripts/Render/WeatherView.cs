using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// Gaps E2a: the weather of the own mob's level (protocol 84, tg's SSweather) drawn in 3D. tg tints each impacted area with its stage's
/// overlay (weather_effects.dmi: ash, rain, snow, dust, lava, coloured by the weather: the radiation storm's green ash storm) and the
/// particle weathers rain or blow their particles over it. Here each weather is a haze in its covered cells (weather.gdshader, marched along
/// the view rays, so a storm is seen from inside and from outside its rooms) and, while the camera is near it, its precipitation
/// (weather_particles.gdshader, shown only inside the covered cells). The stage's state picks the look; the severity of the particle
/// weathers sets how much falls.
/// </summary>
public partial class WeatherView : Node3D
{
    private enum Kind { Ash, Rain, Snow, Dust, Lava }

    private static Shader _haze, _fall;
    private IWorldFeed _feed;
    private Camera3D _camera;
    private WeatherMessage _shown;
    private readonly List<(MeshInstance3D Haze, GpuParticles3D Fall)> _layers = new();

    /// <summary>Draws the weather the feed holds, the haze under the camera.</summary>
    public void Bind(IWorldFeed feed, Camera3D camera)
    {
        _feed = feed;
        _camera = camera;
    }

    public override void _Process(double delta)
    {
        WeatherMessage weather = _feed?.Weather;
        if (!ReferenceEquals(weather, _shown))
            Show(weather);
        if (_camera != null)
        {
            foreach ((MeshInstance3D _, GpuParticles3D fall) in _layers)
            {
                if (fall != null)
                    fall.GlobalPosition = _camera.GlobalPosition;
            }
        }
    }

    private void Show(WeatherMessage weather)
    {
        _shown = weather;
        foreach ((MeshInstance3D haze, GpuParticles3D fall) in _layers)
        {
            haze.QueueFree();
            fall?.QueueFree();
        }
        _layers.Clear();
        if (weather == null || _camera == null)
            return;
        foreach (WeatherLayer layer in weather.Layers)
        {
            if (Look(layer) is not { } look)
                continue;
            ImageTexture cells = CellTexture(layer);
            var box = new Vector4(layer.MinX, layer.MinY, layer.Width, layer.Height);
            float floor = TileSpace.WorldY(weather.Z);
            var haze = new MeshInstance3D
            {
                Name = "WeatherHaze" + layer.Id,
                Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
                ExtraCullMargin = 16384f, // the shader places it, not its transform
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                GIMode = GeometryInstance3D.GIModeEnum.Disabled,
            };
            var material = new ShaderMaterial { Shader = _haze ??= GD.Load<Shader>("res://shaders/weather.gdshader"), RenderPriority = (int)Material.RenderPriorityMin };
            material.SetShaderParameter("cells", cells);
            material.SetShaderParameter("box", box);
            material.SetShaderParameter("tile_size", TileSpace.TileSize);
            material.SetShaderParameter("floor_y", floor);
            material.SetShaderParameter("level_height", TileSpace.LevelHeight);
            material.SetShaderParameter("colour", look.Colour);
            material.SetShaderParameter("density", look.Density);
            material.SetShaderParameter("wind", (float)layer.Wind);
            material.SetShaderParameter("drift", look.Kind == Kind.Lava ? 0.15f : 1f);
            material.SetShaderParameter("grain", look.Kind is Kind.Rain or Kind.Snow ? 0.9f : 0.55f);
            material.SetShaderParameter("floor_only", look.Kind == Kind.Lava ? 1f : 0f);
            material.SetShaderParameter("glow", layer.Glow || look.Kind == Kind.Lava ? 1f : 0f);
            haze.MaterialOverride = material;
            _camera.AddChild(haze);
            _layers.Add((haze, Precipitation(layer, look, cells, box, floor)));
        }
    }

    private readonly record struct WeatherLook(Kind Kind, Color Colour, float Density, float Amount);

    // the stage's overlay state (tg's weather_effects.dmi) as a look; null for a stage tg draws nothing for
    private static WeatherLook? Look(WeatherLayer layer)
    {
        string state = layer.State ?? "";
        if (state.Length == 0)
            return null;
        Kind kind = state.Contains("lava") ? Kind.Lava : state.Contains("rain") ? Kind.Rain : state.Contains("snow") ? Kind.Snow
            : state.Contains("dust") || state.Contains("sand") ? Kind.Dust : Kind.Ash;
        float strength = state.Contains("light") || state.Contains("low") ? 0.45f : state.Contains("med") ? 0.7f : 1f;
        Color colour = kind switch
        {
            Kind.Rain => new Color(0.55f, 0.66f, 0.82f),
            Kind.Snow => new Color(0.92f, 0.95f, 1f),
            Kind.Dust => new Color(0.82f, 0.66f, 0.4f),
            Kind.Lava => new Color(1f, 0.38f, 0.06f),
            _ => new Color(0.36f, 0.31f, 0.28f),
        };
        if (layer.Colour != 0)
            colour = new Color(layer.Colour);
        colour.A = layer.Alpha / 255f;
        float density = kind switch
        {
            Kind.Rain => 0.07f,
            Kind.Snow => 0.22f,
            Kind.Dust => 0.3f,
            Kind.Lava => 1.6f,
            _ => 0.14f,
        } * strength;
        float amount = layer.Particles.Length > 0 ? Math.Clamp(layer.Severity / 100f, 0f, 1f) : strength;
        return new WeatherLook(kind, colour, density, amount);
    }

    // the covered cells as a red-channel texture over the layer's box, row 0 its first row
    private static ImageTexture CellTexture(WeatherLayer layer)
    {
        bool[] cells = layer.ExpandCells();
        var image = Image.CreateEmpty(layer.Width, layer.Height, false, Image.Format.R8);
        for (int y = 0; y < layer.Height; y++)
        {
            for (int x = 0; x < layer.Width; x++)
            {
                if (cells[y * layer.Width + x])
                    image.SetPixel(x, y, Colors.White);
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    // the rain, snow, ash or dust around the camera, only in the covered cells; none for the lava
    private GpuParticles3D Precipitation(WeatherLayer layer, WeatherLook look, ImageTexture cells, Vector4 box, float floor)
    {
        if (look.Kind == Kind.Lava || look.Amount <= 0.01f)
            return null;
        (int amount, float lifetime, Vector3 fall, float wobble, Vector2 size, Color tint) = look.Kind switch
        {
            Kind.Rain => (1600, 0.3f, new Vector3(layer.Wind * 1.5f, -9f, 0f), 0f, new Vector2(0.015f, 0.45f), new Color(0.75f, 0.85f, 1f, 0.55f)),
            Kind.Snow => (1200, 2.6f, new Vector3(layer.Wind * 0.8f, -0.9f, 0f), 0.5f, new Vector2(0.05f, 0.05f), new Color(1f, 1f, 1f, 0.9f)),
            Kind.Dust => (1000, 1.6f, new Vector3(layer.Wind == 0 ? 6f : layer.Wind * 6f, -0.4f, 0.8f), 0.8f, new Vector2(0.03f, 0.03f), new Color(0.85f, 0.7f, 0.45f, 0.8f)),
            _ => (900, 2.0f, new Vector3(layer.Wind * 3.5f, -0.8f, 0.3f), 0.6f, new Vector2(0.045f, 0.045f), new Color(0.25f, 0.22f, 0.2f, 0.85f)),
        };
        if (layer.Colour != 0 && look.Kind == Kind.Ash)
            tint = new Color(look.Colour.R, look.Colour.G, look.Colour.B, 0.8f);
        var process = new ShaderMaterial { Shader = _fall ??= GD.Load<Shader>("res://shaders/weather_particles.gdshader") };
        process.SetShaderParameter("cells", cells);
        process.SetShaderParameter("box", box);
        process.SetShaderParameter("tile_size", TileSpace.TileSize);
        process.SetShaderParameter("floor_y", floor);
        process.SetShaderParameter("level_height", TileSpace.LevelHeight);
        process.SetShaderParameter("fall", fall);
        process.SetShaderParameter("wobble", wobble);
        process.SetShaderParameter("tint", tint);
        var drop = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            BillboardMode = look.Kind == Kind.Rain ? BaseMaterial3D.BillboardModeEnum.FixedY : BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale = true,
            DisableReceiveShadows = true,
        };
        var particles = new GpuParticles3D
        {
            Name = "WeatherFall" + layer.Id,
            Amount = amount,
            AmountRatio = look.Amount,
            Lifetime = lifetime,
            Preprocess = lifetime,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = size, Material = drop },
            VisibilityAabb = new Aabb(new Vector3(-12f, -4f, -12f), new Vector3(24f, 8f, 24f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            LocalCoords = false,
        };
        AddChild(particles);
        return particles;
    }
}
