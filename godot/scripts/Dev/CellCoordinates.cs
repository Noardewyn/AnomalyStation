using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Dev;

// The dev menu's cell coordinates (the owner, 2026-10-13: "a setting in the debug menu that shows the coordinates of the
// cells"; D139): BYOND's x,y of every floor cell within a radius of the camera's cell, written flat on the floor (north is the
// text's up), so a dev command's X Y can be read off the map. Walls are skipped (the text would be inside them).

/// <summary>The coordinate labels on the floor around a cell.</summary>
public partial class CellCoordinates : Node3D
{
    private const float FloorLift = 0.03f;

    private readonly List<Label3D> _labels = new();
    private (int X, int Y, int Z) _centre = (int.MinValue, 0, 0);
    private int _radius = -1;

    /// <summary>The cells drawn around the centre, in cells (0 to <see cref="MaxRadius"/>).</summary>
    public int Radius { get; set; } = 6;

    public const int MaxRadius = 15;

    /// <summary>True for the cells that get no label (walls).</summary>
    public Func<int, int, int, bool> Skip { get; set; }

    /// <summary>The map's size (cells outside it get no label).</summary>
    public (int Width, int Height) MapSize { get; set; }

    /// <summary>Labels the cells around <paramref name="centre"/> (only when it or the radius changed).</summary>
    public void Show((int X, int Y, int Z) centre)
    {
        if (centre == _centre && Radius == _radius)
            return;
        _centre = centre;
        _radius = Radius;
        int used = 0;
        for (int y = centre.Y - Radius; y <= centre.Y + Radius; y++)
        {
            for (int x = centre.X - Radius; x <= centre.X + Radius; x++)
            {
                if (x < 1 || y < 1 || x > MapSize.Width || y > MapSize.Height || (Skip?.Invoke(x, y, centre.Z) ?? false))
                    continue;
                Label3D label = LabelAt(used++);
                label.Text = x + "," + y;
                label.Position = new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(centre.Z) + FloorLift, TileSpace.WorldZ(y));
                label.Visible = true;
            }
        }
        for (int i = used; i < _labels.Count; i++)
            _labels[i].Visible = false;
    }

    /// <summary>Draws again at the next <see cref="Show"/> (the map changed).</summary>
    public void Refresh() => _centre = (int.MinValue, 0, 0);

    private Label3D LabelAt(int index)
    {
        while (_labels.Count <= index)
        {
            var label = new Label3D
            {
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                FontSize = 32,
                PixelSize = 0.007f,
                OutlineSize = 8,
                Modulate = new Color(1f, 0.95f, 0.55f),
                OutlineModulate = new Color(0, 0, 0, 0.9f),
                Shaded = false,
                DoubleSided = true,
                RenderPriority = 11,
                OutlineRenderPriority = 10,
                // flat on the floor, facing up; the text's up is north (-Z)
                Rotation = new Vector3(-Mathf.Pi / 2f, 0, 0),
            };
            AddChild(label);
            _labels.Add(label);
        }
        return _labels[index];
    }
}
