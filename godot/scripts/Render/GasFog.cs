using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Light;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

// DEVIATION(DV62): the gases in the beams' fog. One fog volume over the camera's level reads a texture of the level's cells:
// a cell with a visible gas adds the gas's fog (GasOptics.Cell: its density and the colour it scatters), so a beam through
// steam glows thick and white, through plasma purple; a space cell takes the station air's fog away (a negative density),
// so a beam out of a window into space shows nothing. The client knows only tg's visible gases and the turfs: an invisible
// gas, the air's pressure and a vacuum inside the station are not seen (they would need the protocol's per-cell pressure).
// LightLayer tints and shortens the beams with the same optics (At).

/// <summary>The gases' optics: the fog volume of the camera's level and the per-cell optics the beams read.</summary>
public partial class GasFog : Node3D
{
    private const string ShaderCode = @"
shader_type fog;
uniform sampler2D gas : filter_linear, repeat_disable;
uniform vec2 cells;
uniform float tile;
void fog() {
	vec2 uv = vec2(WORLD_POSITION.x / tile, -WORLD_POSITION.z / tile) / cells;
	vec4 g = texture(gas, uv);
	DENSITY = g.a;
	ALBEDO = g.rgb;
}";

    private const double UploadEvery = 0.2;

    private ReplicaWorld _replica;
    private GasLayer _looks;
    private Camera3D _camera;
    private ILightCells _turfs;
    private int _levels;
    private readonly Dictionary<long, CellOptics> _optics = new();
    private readonly Dictionary<int, GasLook> _kinds = new();
    private FogVolume _volume;
    private ShaderMaterial _material;
    private Image _image;
    private ImageTexture _texture;
    private int _width, _height, _z;
    private bool _dirty;
    private double _uploadIn;

    /// <summary>The level the fog is for when it is not the camera's height (third person hangs above the walls); 0: from the camera.</summary>
    public int ViewLevel { get; set; }

    /// <summary>The number of cells whose gas touches light (for the debug HUD and the tests).</summary>
    public int GasCellCount => _optics.Count;

    /// <summary>Starts following <paramref name="replica"/>'s gases (the colours of <paramref name="looks"/>) for <paramref name="camera"/>'s level.</summary>
    public void Bind(ReplicaWorld replica, GasLayer looks, Camera3D camera)
    {
        _replica = replica;
        _looks = looks;
        _camera = camera;
        _replica.GasChanged += OnGasChanged;
        _replica.CellsChanged += OnCellsChanged;
        Reload();
    }

    public override void _ExitTree()
    {
        if (_replica == null) return;
        _replica.GasChanged -= OnGasChanged;
        _replica.CellsChanged -= OnCellsChanged;
    }

    /// <summary>
    /// Starts without a replica (the dev light room): a <paramref name="width"/> by <paramref name="height"/> map whose space
    /// cells <paramref name="turfs"/> tells; the caller sets the cells' gases with <see cref="SetCell"/>.
    /// </summary>
    public void AttachCells(int width, int height, int levels, ILightCells turfs, Camera3D camera)
    {
        _width = width;
        _height = height;
        _levels = levels;
        _turfs = turfs;
        _camera = camera;
        _optics.Clear();
        _z = 0;
    }

    /// <summary>Sets a cell's optics (the dev light room; the replica's cells come from its gases).</summary>
    public void SetCell(int x, int y, int z, CellOptics optics)
    {
        if (optics.Any) _optics[Key(x, y, z)] = optics;
        else _optics.Remove(Key(x, y, z));
        if (z == _z) Paint(x, y);
    }

    /// <summary>The gases' optics of a cell (clear when it shows no gas).</summary>
    public CellOptics At(int x, int y, int z) => _optics.TryGetValue(Key(x, y, z), out CellOptics c) ? c : CellOptics.Clear;

    private long Key(int x, int y, int z) => ((long)z * (_height + 2) + y) * (_width + 2) + x;

    private void Reload()
    {
        _optics.Clear();
        _kinds.Clear();
        if (_replica?.Map == null) return;
        _width = _replica.Map.Width;
        _height = _replica.Map.Height;
        _levels = _replica.Map.Levels;
        _turfs = new ReplicaLightCells(_replica, _width, _height);
        foreach ((int x, int y, int z, GasLevel[] gases) in _replica.GasCells)
            Take(x, y, z, gases);
        _z = 0; // the next frame builds the level's texture
    }

    private void OnGasChanged(IReadOnlyList<(int X, int Y, int Z)> cells)
    {
        if (_replica.Map == null) return;
        if (_replica.Map.Width != _width || _replica.Map.Height != _height)
        {
            Reload();
            return;
        }
        foreach ((int x, int y, int z) in cells)
        {
            Take(x, y, z, _replica.GasAt(x, y, z));
            if (z == _z) Paint(x, y);
        }
    }

    private void OnCellsChanged(IReadOnlyList<(int X, int Y, int Z)> cells)
    {
        foreach ((int x, int y, int z) in cells)
            if (z == _z) Paint(x, y); // a breach or a patch: space or not
    }

    private void Take(int x, int y, int z, GasLevel[] gases)
    {
        CellOptics c = GasOptics.Cell(gases, Look, TileSpace.TileSize);
        if (c.Any) _optics[Key(x, y, z)] = c;
        else _optics.Remove(Key(x, y, z));
    }

    private GasLook Look(int kind)
    {
        if (!_kinds.TryGetValue(kind, out GasLook look))
            _kinds[kind] = look = _looks?.LookOf(kind) ?? new GasLook(null, 0.8f, 0.8f, 0.8f, 0.5f);
        return look;
    }

    // One texel: the gas's fog, less the station air's over space.
    private void Paint(int x, int y)
    {
        if (_image == null || x < 1 || y < 1 || x > _width || y > _height) return;
        CellOptics c = At(x, y, _z);
        float density = c.FogDensity;
        if (_turfs.IsSpaceTurf(x, y, _z))
            density -= Quality.Environment?.VolumetricFogDensity ?? BeamFog.AirDensity;
        _image.SetPixel(x - 1, y - 1, new Color(c.AlbedoR, c.AlbedoG, c.AlbedoB, density));
        _dirty = true;
    }

    public override void _Process(double delta)
    {
        using var frameProbe = Anomaly.Client.Dev.FrameProbe.Scope("GasFog._Process");
        // Keep gas optics for surface lighting, but skip fog textures and uploads when fog is off.
        if (Quality.Environment?.VolumetricFogEnabled != true) return;
        if (_width < 1 || _height < 1 || _camera == null || !IsInstanceValid(_camera)) return;
        int levels = Math.Max(1, _levels);
        int z = Math.Clamp(ViewLevel > 0 ? ViewLevel : 1 + (int)MathF.Floor(_camera.GlobalPosition.Y / TileSpace.LevelHeight), 1, levels);
        if (z != _z)
            BuildLevel(z);
        _uploadIn -= delta;
        if (_dirty && _uploadIn <= 0)
        {
            _uploadIn = UploadEvery;
            _dirty = false;
            _texture.Update(_image);
        }
    }

    private void BuildLevel(int z)
    {
        _z = z;
        _image ??= Image.CreateEmpty(_width, _height, false, Image.Format.Rgbah);
        _image.Fill(new Color(0, 0, 0, 0));
        for (int y = 1; y <= _height; y++)
            for (int x = 1; x <= _width; x++)
                Paint(x, y);
        if (_texture == null)
        {
            _texture = ImageTexture.CreateFromImage(_image);
            _material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
            _material.SetShaderParameter("gas", _texture);
            _material.SetShaderParameter("cells", new Vector2(_width, _height));
            _material.SetShaderParameter("tile", TileSpace.TileSize);
            _volume = new FogVolume { Name = "GasFog", Shape = RenderingServer.FogVolumeShape.Box, Material = _material };
            AddChild(_volume);
        }
        else
        {
            _texture.Update(_image);
        }

        float w = _width * TileSpace.TileSize, d = _height * TileSpace.TileSize, h = WorldScale.WallHeight;
        _volume.Size = new Vector3(w, h, d);
        _volume.Position = new Vector3(w / 2f, TileSpace.WorldY(z) + h / 2f, -d / 2f);
        _dirty = false;
    }
}
