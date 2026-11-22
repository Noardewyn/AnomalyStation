using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client.Dev;

// The dev menu's player markers (the owner, 2026-11-04: "marks of every player in the world, not on the minimap but in the game"): a
// label over the head of every player's mob of the own level (PlayerMarkerList), drawn through walls at one size on screen. The server
// sends the mobs' tiles twice a second (D315's trackers), so a mark glides to its mob's new tile.

/// <summary>The labels over the players of the own level.</summary>
public partial class PlayerMarkers : Node3D
{
    private const float AboveHead = TileSpace.CapsuleHeight + 0.35f;
    private const float GlidePerSecond = 8f;
    private const float SnapTiles = 3f; // a mark further than this from its new tile jumps (a teleport) instead of gliding

    private readonly Dictionary<string, Label3D> _labels = new();
    private readonly HashSet<string> _shown = new();

    /// <summary>Marks the players of <paramref name="mobs"/> (null hides them all), seen from the own tile.</summary>
    public void Show(MinimapMobsMessage mobs, float ownX, float ownY, double delta)
    {
        _shown.Clear();
        float glide = 1f - Mathf.Exp(-GlidePerSecond * (float)delta);
        foreach (PlayerMarkerList.Mark mark in PlayerMarkerList.Of(mobs, ownX, ownY))
        {
            _shown.Add(mark.Key);
            var at = new Vector3(TileSpace.WorldX(mark.X), TileSpace.WorldY(mobs.Z) + AboveHead, TileSpace.WorldZ(mark.Y));
            if (!_labels.TryGetValue(mark.Key, out Label3D label))
                _labels[mark.Key] = label = NewLabel(at);
            label.Text = mark.Text;
            label.Position = label.Position.DistanceTo(at) > SnapTiles * TileSpace.TileSize ? at : label.Position.Lerp(at, glide);
            label.Visible = true;
        }
        foreach ((string key, Label3D label) in _labels)
            if (!_shown.Contains(key))
                label.Visible = false;
    }

    private Label3D NewLabel(Vector3 at)
    {
        var label = new Label3D
        {
            Position = at,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            IgnoreOcclusionCulling = true, // the map's wall occluders would cull it behind a wall
            FixedSize = true,
            PixelSize = 0.0009f,
            FontSize = 32,
            OutlineSize = 10,
            Modulate = new Color(0.25f, 0.9f, 0.35f), // the minimap's player dot
            OutlineModulate = new Color(0, 0, 0, 0.9f),
            Shaded = false,
            RenderPriority = 12,
            OutlineRenderPriority = 11,
        };
        AddChild(label);
        return label;
    }
}
