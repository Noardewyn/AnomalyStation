using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Light;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client.Light;

// Owner: M3 P7. A dev scene: a lit corridor, a dark room with one lamp and a hull under starlight, drawn with the light
// grid's own include and no replica, so the grid, the shader and the shadowed lights can be seen without P5's server data.
//   godot --path godot res://scripts/Light/LightRoomDemo.tscn -- --shots DIR     saves three screenshots and quits
//   godot --path godot res://scripts/Light/LightRoomDemo.tscn                    fly around with WASD and the mouse; F toggles the flashlight
//   --fog 0|1|2 the volumetric fog level (default 2); the dark room has a flashlight's beam (DV62) seen from the side and head on,
//   then through a pocket of steam, plasma and miasma (beam_<gas>_side.png, beam_<gas>_head_on.png).

/// <summary>The light room: see the file header.</summary>
public partial class LightRoomDemo : Node3D
{
    private sealed class DemoCells : ILightCells
    {
        public readonly bool[,] Opaque = new bool[60, 30];
        public readonly bool[,] Space = new bool[60, 30];
        public int MaxX => 59;
        public int MaxY => 29;
        public bool IsOpaque(int x, int y, int z) => x < 1 || y < 1 || x > MaxX || y > MaxY || Opaque[x, y];
        public uint BaseLight(int x, int y, int z) => 0u;
        public bool IsSpaceTurf(int x, int y, int z) => x < 1 || y < 1 || x > MaxX || y > MaxY || Space[x, y];
    }

    private const string ShaderCode = @"
shader_type spatial;
render_mode cull_back;
#include ""res://shaders/light_grid.gdshaderinc""
uniform vec3 albedo : source_color = vec3(0.7);
varying vec3 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
	ALBEDO = albedo;
	EMISSION = albedo * anomaly_grid_light(wp);
}";

    private DemoCells _cells;
    private LightLayer _layer;
    private Camera3D _camera;
    private Shader _shader;
    private string _shotsDir;
    private int _frame;
    private int _shot;
    private float _yaw, _pitch;
    private int _fog = 2;
    private Anomaly.Client.Render.GasFog _gasFog;
    private SpotLight3D _beam;
    private LightState _flashlight;
    private (float X, float Y) _beamAt;

    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--shots")
                _shotsDir = args[i + 1];
            else if (args[i] == "--fog" && int.TryParse(args[i + 1], out int fog))
                _fog = fog;

        _shader = new Shader { Code = ShaderCode };
        _cells = new DemoCells();
        BuildMap();

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.01f, 0.01f, 0.02f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.Black,
            AmbientLightEnergy = 0f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        Anomaly.Client.Render.BeamFog.Apply(env, _fog);
        AddChild(new WorldEnvironment { Environment = env });

        _camera = new Camera3D { Fov = 78f, Current = true };
        AddChild(_camera);
        _layer = new LightLayer();
        AddChild(_layer);
        _layer.AttachCells(_cells, 1, _camera);
        _layer.SetPreset(RenderPreset.Medium with { ShadowedLights = 4 });

        // Corridor tubes (tg's light tube: range 6.5 here, white-blue), the room's lamp (a warm bulb) and the starlight cells.
        LightState tube = new(6.5f, 1.2f, 0xE6F3FFFFu, true, 360f, 0, TgLightMath.LightingHeight, 0f, 0f, TgLightMath.ComplexLight);
        for (int x = 5; x <= 17; x += 6)
            _layer.SetSource(100 + x, x, 10, 1, tube);
        LightState bulb = new(4f, 1.1f, 0xFFC080FFu, true, 360f, 0, TgLightMath.LightingHeight, 0f, 0f, TgLightMath.ComplexLight);
        _layer.SetSource(200, 29, 10, 1, bulb);
        LightState star = new(2f, 1f, 0x8589FAFFu, true, 360f, 0, TgLightMath.LightingHeightSpace, 0f, 0f, TgLightMath.ComplexLight);
        long key = -1;
        for (int x = 1; x <= _cells.MaxX; x++)
        {
            for (int y = 1; y <= _cells.MaxY; y++)
            {
                if (!_cells.Space[x, y])
                    continue;
                if (Touches(x, y))
                    _layer.SetSource(key--, x, y, 1, star);
            }
        }

        // A flashlight (tg's default: range 4, power 1, #ffc44d... here white-yellow) in a standing holder's hand at (26, 7),
        // pointing north across the dark room.
        _flashlight = new(4f, 1f, 0xFFE6B3FFu, true, 360f, 0, 1f, 0f, -2f, TgLightMath.OverlayLightDirectional);
        _beam = LightLayer.NewBeamSpot();
        LightLayer.Style(_beam, _flashlight);
        _beam.ShadowEnabled = true;
        (float bx, float by, float bh) = FlashlightBeam.Origin(26f, 7f, 0f, carried: true, lying: false);
        _beamAt = (bx, by);
        _beam.Position = new Vector3(TileSpace.WorldX(bx), bh, TileSpace.WorldZ(by));
        _beam.Rotation = new Vector3(-FlashlightBeam.Tilt, 0f, 0f);
        AddChild(_beam);

        // The gases' fog (DV62): no gas until a gas view fills the pocket.
        _gasFog = new Anomaly.Client.Render.GasFog();
        AddChild(_gasFog);
        _gasFog.AttachCells(_cells.MaxX, _cells.MaxY, 1, _cells, _camera);

        Input.MouseMode = _shotsDir == null ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        Place(3.5f, 10f, -90f, 0f);
    }

    // A pocket of one gas at overlay level 60 over the beam's first tiles (x 25..27, y 8..12): the fog and the beam's tint.
    // The sprites' mean colours as the client measures them roughly (the room has no icons).
    private void FillPocket(string gas)
    {
        GasLook look = gas switch
        {
            "water_vapor" => new GasLook(gas, 0.95f, 0.95f, 0.95f, 0.6f),
            "plasma" => new GasLook(gas, 0.75f, 0.2f, 0.85f, 0.6f),
            "miasma" => new GasLook(gas, 0.45f, 0.35f, 0.2f, 0.6f),
            _ => default,
        };
        CellOptics optics = gas == null ? CellOptics.Clear : GasOptics.Cell(new[] { new GasLevel(1, 60) }, _ => look, TileSpace.TileSize);
        for (int x = 25; x <= 27; x++)
            for (int y = 8; y <= 12; y++)
                _gasFog.SetCell(x, y, 1, optics);
        LightLayer.Tint(_beam, _flashlight, _gasFog.At, _beamAt.X, _beamAt.Y, 1, 0f, 1f);
    }

    private bool Touches(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (!_cells.IsSpaceTurf(x + dx, y + dy, 1))
                    return true;
        return false;
    }

    private void Place(float tileX, float tileY, float yawDegrees, float pitchDegrees)
    {
        _camera.Position = new Vector3(TileSpace.WorldX(tileX), TileSpace.EyeHeight, TileSpace.WorldZ(tileY));
        _yaw = Mathf.DegToRad(yawDegrees);
        _pitch = Mathf.DegToRad(pitchDegrees);
        _camera.Rotation = new Vector3(_pitch, _yaw, 0f);
    }

    private void BuildMap()
    {
        // space everywhere; the station is x 1..38, y 5..15
        for (int x = 1; x <= 59; x++)
            for (int y = 1; y <= 29; y++)
                _cells.Space[x, y] = true;
        // corridor x 2..23, y 9..11 (walls at y 9 and 11), the dark room x 24..33, y 5..15, its hull wall at x 33
        for (int x = 1; x <= 34; x++)
            for (int y = 5; y <= 15; y++)
                _cells.Space[x, y] = false;
        for (int x = 1; x <= 34; x++)
            for (int y = 5; y <= 15; y++)
                _cells.Opaque[x, y] = true;
        for (int x = 2; x <= 23; x++)
            _cells.Opaque[x, 10] = false;
        for (int x = 25; x <= 32; x++)
            for (int y = 6; y <= 14; y++)
                _cells.Opaque[x, y] = false;
        _cells.Opaque[24, 10] = false;    // the doorway
        // an outside wall column of the room also keeps the corridor's walls
        for (int x = 1; x <= 34; x++)
            for (int y = 5; y <= 15; y++)
            {
                bool wall = _cells.Opaque[x, y];
                if (wall)
                    AddBox(x, y, new Color(0.55f, 0.58f, 0.62f), WorldScale.WallHeight);
            }

        // floors
        AddFloor(2, 10, 24, 10, new Color(0.45f, 0.47f, 0.5f));
        AddFloor(25, 6, 32, 14, new Color(0.4f, 0.42f, 0.45f));
        // a few props in the dark room
        AddBox(27, 8, new Color(0.7f, 0.3f, 0.2f), 1.2f);
        AddBox(31, 12, new Color(0.3f, 0.5f, 0.7f), 1.6f);
    }

    private void AddBox(int x, int y, Color color, float height)
    {
        var mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2f, height, 2f) } };
        mesh.Position = new Vector3(TileSpace.WorldX(x), height * 0.5f, TileSpace.WorldZ(y));
        mesh.MaterialOverride = MakeMaterial(color);
        AddChild(mesh);
    }

    private void AddFloor(int x0, int y0, int x1, int y1, Color color)
    {
        float w = (x1 - x0 + 1) * 2f, d = (y1 - y0 + 1) * 2f;
        var mesh = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(w, d) } };
        mesh.Position = new Vector3(TileSpace.WorldX((x0 + x1) * 0.5f), 0f, TileSpace.WorldZ((y0 + y1) * 0.5f));
        mesh.MaterialOverride = MakeMaterial(color);
        AddChild(mesh);
    }

    private ShaderMaterial MakeMaterial(Color color)
    {
        var m = new ShaderMaterial { Shader = _shader };
        m.SetShaderParameter("albedo", new Vector3(color.R, color.G, color.B));
        return m;
    }

    public override void _Input(InputEvent e)
    {
        if (_shotsDir != null)
            return;
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= mm.Relative.X * 0.0025f;
            _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * 0.0025f, -1.4f, 1.4f);
            _camera.Rotation = new Vector3(_pitch, _yaw, 0f);
        }
        else if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.F)
                _layer.ToggleFlashlight();
            else if (k.Keycode == Key.Escape)
                GetTree().Quit();
        }
    }

    public override void _Process(double delta)
    {
        _frame++;
        if (_shotsDir != null)
        {
            // frames 30, 90, 150: one view each; the grid, the lights and their fades have settled by then
            (int At, float X, float Y, float Yaw, float Pitch, string Name)[] views =
            {
                (30, 3.5f, 10f, -90f, 0f, "corridor"),
                (90, 25.5f, 14f, -135f, -12f, "dark_room_lamp"),
                (150, 40f, 17f, 130f, 0f, "hull_starlight"),
                (210, 31f, 11.5f, 95f, -4f, "beam_side"),
                (270, 26f, 13.5f, 180f, -8f, "beam_head_on"),
                (330, 26f, 5.6f, 0f, -6f, "beam_own"),
                (390, 31f, 11.5f, 95f, -4f, "beam_water_vapor_side"),
                (450, 26f, 13.5f, 180f, -8f, "beam_water_vapor_head_on"),
                (510, 31f, 11.5f, 95f, -4f, "beam_plasma_side"),
                (570, 26f, 13.5f, 180f, -8f, "beam_plasma_head_on"),
                (630, 31f, 11.5f, 95f, -4f, "beam_miasma_side"),
                (690, 26f, 13.5f, 180f, -8f, "beam_miasma_head_on"),
            };
            if (_shot < views.Length && _frame == views[_shot].At)
            {
                var v = views[_shot];
                if (v.Name.StartsWith("beam", StringComparison.Ordinal))
                    _layer.SetSource(200, 29, 10, 1, LightState.None); // the room's lamp off: the beam in the dark
                string gas = new[] { "water_vapor", "plasma", "miasma" }.FirstOrDefault(g => v.Name.Contains(g, StringComparison.Ordinal));
                FillPocket(gas);
                Place(v.X, v.Y, v.Yaw, v.Pitch);
            }

            if (_shot < views.Length && _frame == views[_shot].At + 40)
            {
                string path = System.IO.Path.Combine(_shotsDir, views[_shot].Name + ".png");
                GetViewport().GetTexture().GetImage().SavePng(path);
                GD.Print("light-room: saved " + path + " grid sources " + _layer.GridOf(1)?.SourceCount + " shadowed " + _layer.ShadowedLightCount + " layer " + _layer.ProcessMicros.ToString("F1") + " us/frame, frame " + (Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000).ToString("F2") + " ms");
                _shot++;
            }

            if (_shot >= views.Length)
                GetTree().Quit();
            return;
        }

        Vector3 dir = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) dir -= _camera.GlobalTransform.Basis.Z;
        if (Input.IsKeyPressed(Key.S)) dir += _camera.GlobalTransform.Basis.Z;
        if (Input.IsKeyPressed(Key.A)) dir -= _camera.GlobalTransform.Basis.X;
        if (Input.IsKeyPressed(Key.D)) dir += _camera.GlobalTransform.Basis.X;
        _camera.Position += dir * 6f * (float)delta;
    }
}
