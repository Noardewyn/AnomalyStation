using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

// DEVIATION(DV77): tg draws a beam (/datum/beam, code/datums/beam.dm: a tesla zap's lightning, energy_ball.dm:325) as segments of
// /obj/effect/ebeam lying on the map plane, each a tile long, turned by its transform toward the target and placed by its pixel
// offsets. Seen in first person a picture on the floor's plane is a line on the ground, and a billboard turned by the transform is
// a sprite tilted in the view. The client draws each segment as a tile-long quad along the segment's way (the transform's
// direction on the map: BYOND's Turn is clockwise from north) at the height of a sprite's middle, turned about that way to face
// the camera, unshaded (tg's beams glow on the emissive plane, DV12): the picture is the segment's own appearance without its
// transform (the cut terminal segment's icon recipe included).
// The M7 projectiles slice: a hitscan's tracer, muzzle flash and impact (/obj/effect/projectile, projectile_effects.dm) lie on the map
// plane turned by their transform too, and are drawn the same way, a tracer as long as its transform's scale (its length in tiles,
// generate_tracer: Scale(1, length / 32) then Turn); a moving projectile (/obj/projectile) is drawn so along its flight (DV78).
// D176 (M7x slice F): the projectiles and effects of ShotLook's kinds (lasers, disablers, electrodes, ions, temperature bolts, bullets, hitscan
// tracers, muzzles, impacts) are the client's own 3D shots instead (ShotVisual, ShotFlash); the rest of them, and tg's beams, stay these quads.

/// <summary>One segment of a tg beam, drawn along its way and turned to the camera.</summary>
public partial class BeamSegment : MeshInstance3D
{
    /// <summary>tg's ebeam type and its subtypes.</summary>
    public const string Type = "/obj/effect/ebeam";

    /// <summary>tg's tracer, muzzle and impact effects of a projectile and their subtypes.</summary>
    public const string ProjectileEffectType = "/obj/effect/projectile";

    /// <summary>tg's projectile and its subtypes.</summary>
    public const string ProjectileType = "/obj/projectile";

    /// <summary>Whether a thing of <paramref name="typePath"/> is drawn as a segment.</summary>
    public static bool Applies(string typePath) => typePath != null && (typePath.StartsWith(Type, StringComparison.Ordinal)
        || typePath.StartsWith(ProjectileEffectType, StringComparison.Ordinal) || typePath.StartsWith(ProjectileType, StringComparison.Ordinal));

    private static QuadMesh _quad;
    private StandardMaterial3D _material;
    private Vector3 _way = Vector3.Forward;
    private float _length = 1f;
    private Texture2D _texture;

    /// <summary>A segment under <paramref name="parent"/> (the entity's node, at its tile's floor).</summary>
    public static BeamSegment Create(Node3D parent)
    {
        var segment = new BeamSegment
        {
            Name = "BeamSegment",
            Mesh = _quad ??= new QuadMesh { Size = new Vector2(TileSpace.TileSize, TileSpace.TileSize) },
            CastShadow = ShadowCastingSetting.Off,
        };
        segment._material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        };
        segment.MaterialOverride = segment._material;
        parent.AddChild(segment);
        return segment;
    }

    /// <summary>
    /// The way of a BYOND transform (a, b, c, d, e, f: x' = a x + b y + c, y' = d x + e y + f) on the map: where it takes the
    /// sprite's north, (b, e), with east = +x and north = -z in the world (EntityLayer's ground reading).
    /// </summary>
    public static Vector3 WayOf(float[] transform)
    {
        if (transform == null || transform.Length < 6)
            return Vector3.Forward;
        var way = new Vector3(transform[1], 0f, -transform[4]);
        return way.LengthSquared() < 1e-6f ? Vector3.Forward : way.Normalized();
    }

    /// <summary>How long a BYOND transform makes the sprite's north axis, in tiles (1 for a turn alone).</summary>
    public static float LengthOf(float[] transform)
    {
        if (transform == null || transform.Length < 6)
            return 1f;
        float length = new Vector2(transform[1], transform[4]).Length();
        return length < 1e-3f ? 1f : length;
    }

    /// <summary>The picture, where the segment's middle is (relative to its node), its way and its length in tiles.</summary>
    public void Set(Texture2D texture, Vector3 middle, Vector3 way, float length, float alpha)
    {
        if (!ReferenceEquals(texture, _texture))
        {
            _texture = texture;
            _material.AlbedoTexture = texture;
        }
        _material.AlbedoColor = new Color(1f, 1f, 1f, alpha);
        Position = middle;
        _way = way;
        _length = length;
        Face();
    }

    /// <summary>
    /// The owner (2026-11-20): a flying projectile's picture faces the eye whole, its picture's north turned to its way as the eye sees it, and
    /// <paramref name="size"/> tiles across (tg's flat sprite seen edge-on about its way was a sliver from behind, and a whole tile was too big).
    /// </summary>
    public void SetFacing(Texture2D texture, Vector3 middle, Vector3 way, float size, float alpha)
    {
        _facing = true;
        Set(texture, middle, way, size, alpha);
    }

    private bool _facing;

    public override void _Process(double delta) => Face();

    // The quad's height (its y) runs along the way; its face turns about the way toward the camera (facing: turns to the camera, its y along
    // the way as seen, else the camera's up).
    private void Face()
    {
        if (!IsInsideTree())
            return;
        Camera3D camera = GetViewport()?.GetCamera3D();
        Vector3 origin = GlobalPosition;
        Vector3 toCamera = camera != null ? camera.GlobalPosition - origin : Vector3.Up;
        if (_facing)
        {
            Vector3 n = toCamera.LengthSquared() < 1e-6f ? Vector3.Up : toCamera.Normalized();
            Vector3 up = _way - n * _way.Dot(n);
            if (up.LengthSquared() < 1e-4f)
                up = camera != null ? camera.GlobalBasis.Y : Vector3.Forward;
            up = (up - n * up.Dot(n)).Normalized();
            GlobalBasis = new Basis(up.Cross(n) * _length, up * _length, n);
            return;
        }
        Vector3 normal = toCamera - _way * toCamera.Dot(_way);
        if (normal.LengthSquared() < 1e-6f)
            normal = Vector3.Up;
        normal = normal.Normalized();
        Vector3 across = _way.Cross(normal).Normalized();
        GlobalBasis = new Basis(across, _way * _length, normal);
    }

    /// <summary>The appearance source that shows a root appearance without its transform (the segment's turn is the quad's way).</summary>
    public sealed class Unturned : IAppearanceSource
    {
        private readonly IAppearanceSource _tables;
        private readonly TableRef _root;

        public Unturned(IAppearanceSource tables, TableRef root)
        {
            _tables = tables;
            _root = root;
        }

        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry)
        {
            if (!_tables.TryGetAppearance(reference, out entry))
                return false;
            if (reference == _root && entry.Transform != null)
            {
                entry = entry.WithOverlays(_ => true);
                entry.Transform = null;
            }
            return true;
        }

        public string ResolveString(TableRef reference) => _tables.ResolveString(reference);

        public string ResolveResource(TableRef reference) => _tables.ResolveResource(reference);
    }
}
