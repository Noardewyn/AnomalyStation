using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Light;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

// M4b slice 3 (the gas visuals, D18): tg draws a turf's visible gases as /obj/effect/overlay/gas sprites over it
// (update_visuals, GAS_OVERLAYS): the gas's sprite of icons/effects/atmospherics.dmi at the alpha of its level. The server
// sends each cell's gases and levels (protocol version 5, ReplicaWorld.GasAt); this draws them as a haze that fills the
// cell (shaders/gas_fog.gdshader). A gas's colour and how much of the tile its sprite covers come from tg's own sprite (the
// mean of its frames); the cell's gases combine as tg's stacked overlays do (one over the other). tg's viewer looks down
// through one layer of overlay wherever they look; a first-person view looks along the gas, so the haze reaches the stack's
// opacity over a few tiles (TilesToOpacity) rather than one, or a room of gas would hide everything past a few metres. A
// change fades over a fraction of a second.

/// <summary>The haze of every cell that shows gas.</summary>
public partial class GasLayer : Node3D
{
    private const string GasIcon = "icons/effects/atmospherics.dmi";
    private const float FadePerSecond = 4f; // the share of the way to a cell's new look covered per second (exponential)
    private const float MaxOpacity = 0.9f; // the thickest a stack counts as (an alpha of 1 would be an infinite density)
    // DEVIATION(DV32): tg's overlay stack is seen once from above; here its opacity is reached over this many tiles of gas.
    private const float TilesToOpacity = 4f;

    private readonly Dictionary<int, Cell> _cells = new();
    private readonly List<int> _gone = new();
    private ReplicaWorld _replica;
    private IconLibrary _icons;
    private readonly Dictionary<int, (Color Color, float Coverage)> _kinds = new();
    private MultiMeshInstance3D _instance;
    private MultiMesh _multimesh;
    private float[] _buffer = Array.Empty<float>();
    private bool _dirty;
    private int _width, _height;
    private int _sightVersion = -1;

    /// <summary>Third person (FovMask): the haze of a cell with nothing in the own mob's sight is not drawn (its chunk is not either, so the shade has no surface to cover it). Null: every cell.</summary>
    public FovMask Sight { get; set; }

    private sealed class Cell
    {
        public int X, Y, Z;
        public Color Color, TargetColor;
        public float Density, TargetDensity;
    }

    /// <summary>The number of cells drawn now (for the debug HUD).</summary>
    public int CellCount => _cells.Count;

    /// <summary>
    /// The haze of a cell as drawn now: its colour and the opacity of tg's overlay stack (0 when the cell shows no gas). The
    /// airflow streams take their colour from it (D141).
    /// </summary>
    public bool TryGetLook(int x, int y, int z, out Color color, out float opacity)
    {
        color = new Color(0, 0, 0);
        opacity = 0f;
        if (_width < 1 || _height < 1 || !_cells.TryGetValue(Key(x, y, z), out Cell cell) || cell.Density <= 0) return false;
        color = cell.Color;
        opacity = 1f - MathF.Exp(-cell.Density * TilesToOpacity * TileSpace.TileSize);
        return true;
    }

    public void Bind(ReplicaWorld replica, IconLibrary icons)
    {
        _replica = replica;
        _icons = icons;
        _replica.GasChanged += OnGasChanged;
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new BoxMesh { Size = Vector3.One },
        };
        _instance = new MultiMeshInstance3D
        {
            Name = "GasHaze",
            Multimesh = _multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = Material(),
        };
        AddChild(_instance);
        Reload();
    }

    public override void _ExitTree()
    {
        if (_replica != null)
            _replica.GasChanged -= OnGasChanged;
    }

    /// <summary>Takes every cell from the replica again (after a map arrived).</summary>
    public void Reload()
    {
        _cells.Clear();
        if (_replica?.Map == null) return;
        _width = _replica.Map.Width;
        _height = _replica.Map.Height;
        foreach ((int x, int y, int z, GasLevel[] gases) in _replica.GasCells)
            Take(x, y, z, gases, fade: false);
        _dirty = true;
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
            Take(x, y, z, _replica.GasAt(x, y, z), fade: true);
        _dirty = true;
    }

    // A cell's target look from its gases: tg's overlays stacked in order (the later over the earlier), each at its level's
    // alpha times how much of the tile its sprite covers.
    private void Take(int x, int y, int z, GasLevel[] gases, bool fade)
    {
        int key = Key(x, y, z);
        Color color = new(0, 0, 0, 0);
        float clear = 1f; // the share of light the stack lets through
        foreach (GasLevel gas in gases)
        {
            (Color kind, float coverage) = KindLook(gas.Kind);
            float a = GasVisual.Alpha(gas.Level) / 255f * coverage;
            color = new Color(color.R * (1 - a) + kind.R * a, color.G * (1 - a) + kind.G * a, color.B * (1 - a) + kind.B * a);
            clear *= 1 - a;
        }
        float opacity = Math.Min(1 - clear, MaxOpacity);
        Color look = new(0, 0, 0);
        if (opacity > 0)
        {
            // the stack's colour without the share of the floor seen through it
            float seen = 1 - clear;
            look = new Color(color.R / seen, color.G / seen, color.B / seen);
        }
        float density = opacity > 0 ? -MathF.Log(1 - opacity) / (TilesToOpacity * TileSpace.TileSize) : 0f;
        if (!_cells.TryGetValue(key, out Cell cell))
        {
            if (density <= 0) return;
            cell = new Cell { X = x, Y = y, Z = z, Color = look, Density = fade ? 0f : density };
            _cells[key] = cell;
        }
        cell.TargetColor = density > 0 ? look : cell.Color;
        cell.TargetDensity = density;
        if (!fade)
        {
            cell.Color = cell.TargetColor;
            cell.Density = density;
        }
    }

    public override void _Process(double delta)
    {
        using var frameProbe = Anomaly.Client.Dev.FrameProbe.Scope("GasLayer._Process");
        if (_multimesh == null) return;
        int sight = Sight?.Version ?? -1;
        if (sight != _sightVersion)
        {
            _sightVersion = sight;
            _dirty = true;
        }
        float k = 1f - MathF.Exp(-FadePerSecond * (float)delta);
        _gone.Clear();
        foreach ((int key, Cell cell) in _cells)
        {
            if (cell.Density == cell.TargetDensity && cell.Color == cell.TargetColor) continue;
            cell.Density += (cell.TargetDensity - cell.Density) * k;
            cell.Color = cell.Color.Lerp(cell.TargetColor, k);
            if (Math.Abs(cell.Density - cell.TargetDensity) < 0.002f)
            {
                cell.Density = cell.TargetDensity;
                cell.Color = cell.TargetColor;
            }
            if (cell.Density <= 0 && cell.TargetDensity <= 0) _gone.Add(key);
            _dirty = true;
        }
        foreach (int key in _gone)
            _cells.Remove(key);
        if (_dirty)
            Upload();
    }

    // The instance buffer: per cell a box over the tile from the floor to the ceiling, its colour and density.
    private void Upload()
    {
        _dirty = false;
        int count = 0;
        if (_buffer.Length < _cells.Count * 16)
            _buffer = new float[Math.Max(_cells.Count * 16, _buffer.Length * 2)];
        int i = 0;
        float tile = TileSpace.TileSize, height = TileSpace.LevelHeight;
        foreach (Cell cell in _cells.Values)
        {
            if (Sight != null && !Sight.CellVisible(cell.X, cell.Y, cell.Z))
                continue;
            float cx = TileSpace.WorldX(cell.X), cy = TileSpace.WorldY(cell.Z) + height / 2f, cz = TileSpace.WorldZ(cell.Y);
            int o = i * 16;
            // Transform3D rows: basis x, origin x; basis y, origin y; basis z, origin z
            _buffer[o + 0] = tile; _buffer[o + 1] = 0; _buffer[o + 2] = 0; _buffer[o + 3] = cx;
            _buffer[o + 4] = 0; _buffer[o + 5] = height; _buffer[o + 6] = 0; _buffer[o + 7] = cy;
            _buffer[o + 8] = 0; _buffer[o + 9] = 0; _buffer[o + 10] = tile; _buffer[o + 11] = cz;
            _buffer[o + 12] = cell.Color.R; _buffer[o + 13] = cell.Color.G; _buffer[o + 14] = cell.Color.B; _buffer[o + 15] = cell.Density;
            i++;
        }
        count = i;
        _multimesh.InstanceCount = count;
        if (count > 0)
            _multimesh.Buffer = count * 16 == _buffer.Length ? _buffer : _buffer[..(count * 16)];
        _instance.Visible = count > 0;
    }

    // A gas kind's colour and coverage: the mean of its sprite's frames (colour weighted by alpha, coverage the mean alpha).
    /// <summary>A gas kind as the beams' optics see it (DV62): its overlay state, its sprite's mean colour and coverage.</summary>
    public GasLook LookOf(int kind)
    {
        (Color color, float coverage) = KindLook(kind);
        string name = _replica != null && kind >= 1 && kind <= _replica.GasKinds.Count ? _replica.GasKinds[kind - 1] : null;
        return new GasLook(name, color.R, color.G, color.B, coverage);
    }

    private (Color Color, float Coverage) KindLook(int kind)
    {
        if (_kinds.TryGetValue(kind, out (Color, float) look)) return look;
        look = (new Color(0.8f, 0.8f, 0.8f), 0.5f);
        string name = kind >= 1 && kind <= _replica.GasKinds.Count ? _replica.GasKinds[kind - 1] : null;
        if (name != null && _icons != null && _icons.TryGetIcon(GasIcon, out IIcon icon) && icon.TryGetState(name, out IIconState state))
        {
            (float r, float g, float b, float coverage) = SpriteColors.Mean(state);
            if (coverage > 0)
                look = (new Color(r, g, b), coverage);
        }
        _kinds[kind] = look;
        return look;
    }

    private int Key(int x, int y, int z) => (x - 1) + (y - 1) * _width + (z - 1) * _width * _height;

    private static ShaderMaterial Material()
    {
        var noise = new NoiseTexture3D
        {
            Width = 48,
            Height = 48,
            Depth = 48,
            Seamless = true,
            Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.08f, FractalOctaves = 3 },
        };
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/gas_fog.gdshader") };
        material.SetShaderParameter("noise_tex", noise);
        return material;
    }
}
