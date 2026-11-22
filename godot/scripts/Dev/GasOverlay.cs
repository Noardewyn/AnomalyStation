using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Dev;

// The dev menu's gas overlay (the owner, 2026-10-13: "see on the map where and which gases are in each cell"): the air of
// the cells around the player as the server's DevGasReport gives it, drawn as a tinted square on each floor tile (by the
// mix of gases, one gas, the pressure or the temperature) and, near the player, a label with the cell's main gases and
// pressure. A dev view: it shows what the server has, nothing tg draws.

/// <summary>What the gas overlay's tiles show.</summary>
public enum GasOverlayMode
{
    /// <summary>The mix: the gases' colours by their moles, stronger with more air (a vacuum is dark).</summary>
    Mix,

    /// <summary>One gas: its colour, stronger with more of it.</summary>
    OneGas,

    /// <summary>The pressure: dark at a vacuum, blue, green at one atmosphere, yellow, red above two.</summary>
    Pressure,

    /// <summary>The temperature: blue when cold, green at room temperature, yellow, red when hot.</summary>
    Temperature,
}

/// <summary>Tiles and labels over the floor from the newest <see cref="DevGasReport"/>.</summary>
public partial class GasOverlay : Node3D
{
    /// <summary>tg's <c>MOLES_CELLSTANDARD</c>: the moles of one tile of air at one atmosphere and 20 C.</summary>
    public const float MolesCellStandard = 103.98f;

    private const float FloorLift = 0.035f;

    private MultiMeshInstance3D _tiles;
    private MultiMesh _multimesh;
    private readonly List<Label3D> _labels = new();
    private bool _dirty;

    /// <summary>The newest report (null before the first).</summary>
    public DevGasReport Report { get; private set; }

    /// <summary>When the newest report came (seconds, the app clock).</summary>
    public double ReportAt { get; private set; }

    public GasOverlayMode Mode { get; set; } = GasOverlayMode.Mix;

    /// <summary>The gas of <see cref="GasOverlayMode.OneGas"/> (tg's id: "plasma").</summary>
    public string Gas { get; set; } = "plasma";

    /// <summary>The tiles' strongest alpha.</summary>
    public float Opacity { get; set; } = 0.5f;

    /// <summary>Labels on the cells within <see cref="LabelRadius"/> of the report's centre.</summary>
    public bool Labels { get; set; } = true;

    public int LabelRadius { get; set; } = 3;

    public override void _Ready()
    {
        Name = "DevGasOverlay";
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new PlaneMesh { Size = new Vector2(TileSpace.TileSize * 0.94f, TileSpace.TileSize * 0.94f) },
        };
        _tiles = new MultiMeshInstance3D
        {
            Multimesh = _multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                DisableReceiveShadows = true,
                RenderPriority = 10, // over the gas haze (both are transparent)
            },
        };
        AddChild(_tiles);
    }

    /// <summary>Takes a new report (redrawn on the next frame).</summary>
    public void Take(DevGasReport report, double now)
    {
        Report = report;
        ReportAt = now;
        _dirty = true;
    }

    /// <summary>Redraws with the current settings (after a setting changed).</summary>
    public void Refresh() => _dirty = true;

    /// <summary>Hides everything until the next report.</summary>
    public void Clear()
    {
        Report = null;
        _dirty = true;
    }

    public override void _Process(double delta)
    {
        using var frameProbe = Anomaly.Client.Dev.FrameProbe.Scope("GasOverlay._Process");
        if (!_dirty || _multimesh == null) return;
        _dirty = false;
        Rebuild();
    }

    private void Rebuild()
    {
        DevGasReport report = Visible ? Report : null;
        int count = report?.Cells.Count ?? 0;
        _multimesh.InstanceCount = count;
        int labelsUsed = 0;
        if (report != null)
        {
            float maxGas = MaxMolesOf(report, Gas);
            int cx = report.X0 + report.Width / 2, cy = report.Y0 + report.Height / 2;
            for (int i = 0; i < count; i++)
            {
                DevGasCell cell = report.Cells[i];
                int x = report.X0 + i % report.Width, y = report.Y0 + i / report.Width;
                var at = new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(report.Z) + FloorLift, TileSpace.WorldZ(y));
                _multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, at));
                _multimesh.SetInstanceColor(i, TileColor(report, cell, maxGas));
                if (Labels && cell.Kind is DevCellKind.Open or DevCellKind.Space && Math.Abs(x - cx) <= LabelRadius && Math.Abs(y - cy) <= LabelRadius)
                {
                    Label3D label = LabelAt(labelsUsed++);
                    label.Text = LabelText(report, cell);
                    label.Position = at + new Vector3(0, 0.3f, 0);
                    label.Visible = true;
                }
            }
        }
        for (int i = labelsUsed; i < _labels.Count; i++)
            _labels[i].Visible = false;
    }

    private Color TileColor(DevGasReport report, DevGasCell cell, float maxGas)
    {
        if (cell.Kind is not (DevCellKind.Open or DevCellKind.Space))
            return new Color(0, 0, 0, 0);
        float total = 0;
        foreach (DevGasAmount amount in cell.Moles)
            total += amount.Moles;
        switch (Mode)
        {
            case GasOverlayMode.OneGas:
            {
                float moles = MolesOf(report, cell, Gas, out uint rgb);
                if (moles <= 0 || maxGas <= 0) return new Color(0, 0, 0, 0);
                return WithAlpha(Rgb(rgb), Opacity * (0.15f + 0.85f * Math.Clamp(moles / maxGas, 0f, 1f)));
            }
            case GasOverlayMode.Pressure:
                return WithAlpha(Ramp(cell.Pressure, PressureStops), Opacity);
            case GasOverlayMode.Temperature:
                return WithAlpha(Ramp(cell.Temperature, TemperatureStops), Opacity);
            default:
            {
                if (total < 0.01f) return new Color(0, 0, 0, Opacity); // a vacuum
                float r = 0, g = 0, b = 0;
                foreach (DevGasAmount amount in cell.Moles)
                {
                    Color c = Rgb(report.Gases[amount.Gas].Rgb);
                    float share = amount.Moles / total;
                    r += c.R * share;
                    g += c.G * share;
                    b += c.B * share;
                }
                return new Color(r, g, b, Opacity * (0.25f + 0.75f * Math.Clamp(total / MolesCellStandard, 0f, 1f)));
            }
        }
    }

    // (value, colour) stops, linear between them.
    private static readonly (float At, Color Color)[] PressureStops =
    {
        (0f, new Color(0.05f, 0f, 0.1f)), (40f, new Color(0.1f, 0.2f, 0.9f)), (101.325f, new Color(0.1f, 0.85f, 0.2f)),
        (160f, new Color(0.95f, 0.9f, 0.1f)), (300f, new Color(0.95f, 0.1f, 0.05f)),
    };

    private static readonly (float At, Color Color)[] TemperatureStops =
    {
        (0f, new Color(0.3f, 0.1f, 0.6f)), (200f, new Color(0.1f, 0.3f, 0.95f)), (293.15f, new Color(0.1f, 0.85f, 0.2f)),
        (373.15f, new Color(0.95f, 0.9f, 0.1f)), (1000f, new Color(0.95f, 0.15f, 0.05f)), (5000f, new Color(1f, 1f, 1f)),
    };

    private static Color Ramp(float value, (float At, Color Color)[] stops)
    {
        if (value <= stops[0].At) return stops[0].Color;
        for (int i = 1; i < stops.Length; i++)
        {
            if (value <= stops[i].At)
                return stops[i - 1].Color.Lerp(stops[i].Color, (value - stops[i - 1].At) / (stops[i].At - stops[i - 1].At));
        }
        return stops[^1].Color;
    }

    /// <summary>The colour of one of the ramps at a value (for the legend).</summary>
    public static Color PressureColor(float kpa) => Ramp(kpa, PressureStops);

    public static Color TemperatureColor(float kelvin) => Ramp(kelvin, TemperatureStops);

    private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, a);

    /// <summary>A report's 0xRRGGBB as a colour.</summary>
    public static Color Rgb(uint rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

    private static float MolesOf(DevGasReport report, DevGasCell cell, string id, out uint rgb)
    {
        rgb = 0xFFFFFF;
        foreach (DevGasAmount amount in cell.Moles)
        {
            DevGasInfo info = report.Gases[amount.Gas];
            if (info.Id != id) continue;
            rgb = info.Rgb;
            return amount.Moles;
        }
        return 0;
    }

    private static float MaxMolesOf(DevGasReport report, string id)
    {
        float max = 0;
        foreach (DevGasCell cell in report.Cells)
            max = Math.Max(max, MolesOf(report, cell, id, out _));
        return max;
    }

    // The cell's three largest gases (tg's ids) and its pressure.
    private static string LabelText(DevGasReport report, DevGasCell cell)
    {
        var sorted = new List<DevGasAmount>(cell.Moles);
        sorted.Sort((a, b) => b.Moles.CompareTo(a.Moles));
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < sorted.Count && i < 3; i++)
            text.Append(report.Gases[sorted[i].Gas].Id).Append(' ').Append(sorted[i].Moles.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        text.Append(cell.Pressure.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(" kPa");
        return text.ToString();
    }

    private Label3D LabelAt(int index)
    {
        while (_labels.Count <= index)
        {
            var label = new Label3D
            {
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 30,
                PixelSize = 0.0035f,
                OutlineSize = 8,
                Modulate = new Color(1, 1, 1),
                OutlineModulate = new Color(0, 0, 0, 0.85f),
                Shaded = false,
                DoubleSided = true,
                NoDepthTest = false,
                RenderPriority = 11,
                OutlineRenderPriority = 10,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            AddChild(label);
            _labels.Add(label);
        }
        return _labels[index];
    }
}
