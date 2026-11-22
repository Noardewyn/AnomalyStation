using System;
using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Render;

// tg draws its fluid smoke (/obj/effect/particle_effect/fluid/smoke) as a 96-pixel sprite on every tile, which overlap into a cloud. A flat sprite reads as paper in first
// person and thousands of blended pixels cost frames (D303), so the client draws the same state as a shared mesh of pixel puffs (shaders/smoke_puff.gdshader): the colour
// and alpha are tg's, and tg's opacity makes the thick kind dense; what it hides from the viewer is the cell's opaque flag.

/// <summary>A smoky tile: a shared mesh of puffs that follows tg's colour and alpha.</summary>
public partial class SmokeVolume : MeshInstance3D
{
    /// <summary>tg's smoke, every kind.</summary>
    public const string SmokeType = "/obj/effect/particle_effect/fluid/smoke";

    private const int Puffs = 7;
    private const float ThinDensity = 0.55f; // tg's see-through kinds (opacity = FALSE), which the 96-pixel sprite's own holes show through

    private static ArrayMesh _mesh;
    private static ShaderMaterial _material;

    private Color _tint = new(-1, -1, -1);
    private float _density = -1f;

    /// <summary>Whether this type is tg's smoke.</summary>
    public static bool Applies(string typePath) => typePath != null && typePath.StartsWith(SmokeType, StringComparison.Ordinal);

    /// <summary>tg's kinds that leave the view open (opacity = FALSE in effects_smoke.dm).</summary>
    public static bool IsThin(string typePath) => typePath.EndsWith("/transparent", StringComparison.Ordinal) || typePath.EndsWith("/quick", StringComparison.Ordinal)
        || typePath.EndsWith("/green", StringComparison.Ordinal) || typePath.EndsWith("/black", StringComparison.Ordinal) || typePath.EndsWith("/freezing", StringComparison.Ordinal);

    /// <summary>Makes the puffs of one tile under <paramref name="parent"/> (the smoke's node, at the tile's floor).</summary>
    public static SmokeVolume Create(Node3D parent)
    {
        var smoke = new SmokeVolume
        {
            Name = "Smoke",
            Mesh = _mesh ??= Cloud(),
            MaterialOverride = _material ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/smoke_puff.gdshader") },
            CastShadow = ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-3, -1, -3), new Vector3(6, 5, 6)), // the shader stands the puffs up, and the eye within it draws the full-screen quad
        };
        parent.AddChild(smoke);
        return smoke;
    }

    /// <summary>Follows tg's colour and the smoke's alpha (0 to 1, tg's fade); cheap when neither changed.</summary>
    public void Set(Color tint, float alpha, bool thin)
    {
        if (!tint.IsEqualApprox(_tint))
        {
            _tint = tint;
            SetInstanceShaderParameter("tint", new Vector3(tint.R, tint.G, tint.B));
        }
        float density = Mathf.Snapped(alpha * (thin ? ThinDensity : 1f), 1f / 16f);
        if (density != _density)
        {
            _density = density;
            SetInstanceShaderParameter("density", density);
        }
    }

    // Each puff a quad whose corners all lie on its centre (the shader spreads them by UV): red numbers it, green is its size over 2 m; the last quad, blue, is the full-screen one.
    private static ArrayMesh Cloud()
    {
        var random = new RandomNumberGenerator { Seed = 7 };
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Quad(Vector3 at, Color color)
        {
            foreach (Vector2 corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1) })
            {
                surface.SetColor(color);
                surface.SetUV(corner);
                surface.AddVertex(at);
            }
        }
        float half = TileSpace.TileSize / 2f;
        for (int puff = 0; puff < Puffs; puff++)
        {
            var at = new Vector3(random.RandfRange(-half, half), random.RandfRange(0.3f, TileSpace.TileSize * 0.9f), random.RandfRange(-half, half));
            Quad(at, new Color(puff / 255f, random.RandfRange(0.7f, 1.1f) / 2f, 0f));
        }
        Quad(Vector3.Zero, new Color(Puffs / 255f, 0f, 1f));
        return surface.Commit();
    }
}
