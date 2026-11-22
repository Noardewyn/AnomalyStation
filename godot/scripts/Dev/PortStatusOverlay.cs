using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Dev;
using Anomaly.Protocol;

namespace Anomaly.Client.Dev;

// The port status overlay (the owner, 2026-10-28; the Settings window's Developer tab): a ball over every thing within a radius of
// the camera's cell, green, yellow or red by PortIndex's level of its type, stacked when a tile holds several. Effects and types
// the index does not know get none. The balls are depth tested, so a wall hides what is behind it.

/// <summary>The coloured balls over the things around a cell.</summary>
public partial class PortStatusOverlay : MultiMeshInstance3D
{
    private const int Radius = 12;
    private const float ObjHeight = 0.9f, OverHead = 0.3f, Stack = 0.2f;

    private static readonly Color Green = new(0.2f, 0.9f, 0.3f), Yellow = new(1f, 0.85f, 0.15f), Red = new(1f, 0.2f, 0.15f);

    private readonly List<(Vector3 At, Color Colour)> _balls = new();
    private readonly Dictionary<(int, int, int), int> _perTile = new();

    public PortStatusOverlay()
    {
        CastShadow = ShadowCastingSetting.Off;
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new SphereMesh
            {
                Radius = 0.08f,
                Height = 0.16f,
                RadialSegments = 12,
                Rings = 6,
                Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true },
            },
        };
    }

    /// <summary>Places the balls over the things around <paramref name="centre"/>, the own mob (<paramref name="ownId"/>) left out.</summary>
    public void Show(PortIndex index, IEnumerable<ReplicaEntity> entities, (int X, int Y, int Z) centre, long ownId)
    {
        _balls.Clear();
        _perTile.Clear();
        foreach (ReplicaEntity e in entities)
        {
            EntityState s = e.State;
            if (s.Z != centre.Z || Math.Abs(s.X - centre.X) > Radius || Math.Abs(s.Y - centre.Y) > Radius || s.IsVisual || e.Id == ownId
                || e.TypePath == null || e.TypePath.StartsWith("/obj/effect", StringComparison.Ordinal))
                continue;
            Color colour;
            switch (index.Of(e.TypePath).Level)
            {
                case PortLevel.Green: colour = Green; break;
                case PortLevel.Yellow: colour = Yellow; break;
                case PortLevel.Red: colour = Red; break;
                default: continue;
            }
            int below = _perTile.GetValueOrDefault((s.X, s.Y, s.Z));
            _perTile[(s.X, s.Y, s.Z)] = below + 1;
            float x = s.HasPos ? s.PosX / 256f : s.X, y = s.HasPos ? s.PosY / 256f : s.Y;
            // over a mob's head, under the ceiling
            float up = Math.Min((e.IsMob() ? TileSpace.EyeHeight + OverHead : ObjHeight) + below * Stack, WorldScale.WallHeight - 0.1f);
            _balls.Add((new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(s.Z) + up, TileSpace.WorldZ(y)), colour));
        }
        Multimesh.InstanceCount = _balls.Count;
        for (int i = 0; i < _balls.Count; i++)
        {
            Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, _balls[i].At));
            Multimesh.SetInstanceColor(i, _balls[i].Colour);
        }
    }
}
