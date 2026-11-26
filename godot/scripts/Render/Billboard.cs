using Godot;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Render;

/// <summary>
/// One mob or item drawn as tg draws it (D104): a composed picture on a quad. Standing, the quad is a Y-axis billboard (the material turns
/// it to the camera about the vertical axis) with the icon square's centre on the node and its bottom edge on the floor: 32 pixels are
/// 2 m, so tg's human (rows 3 to 31) is 1.81 m tall. Lying, the picture is tg's own turned sprite (the compositor applies the mob's lying
/// transform) and it is the same billboard (the owner's play test, 2026-10-13: it was a flat quad seen from above), its lowest drawn pixel on the
/// floor: the viewer sees the body from the side and the frame follows the mob's facing like a standing one's. Nearest filtering, alpha scissor;
/// lit by the light grid like every other world surface (shaders/picture_billboard.gdshader); no shadow.
/// </summary>
public partial class Billboard : Node3D
{
    private MeshInstance3D _instance;
    private QuadMesh _quad;
    private ComposedRaster _raster;
    private bool _lying;
    private float _itemScale = 1f;
    private int? _hostTrim;
    private SpriteRect _unshifted;
    private float _across;

    private static Shader _onTopShader;
    private ShaderMaterial _onTop;

    private static Shader _seeMobsShader;
    private MeshInstance3D _throughWalls;
    private bool _wantThroughWalls;
    private Variant _pictureTexture;

    /// <summary>DV374: how the copy seen through the walls is drawn: the picture's own colours, a little see-through (it is behind something).</summary>
    public static readonly Color ThroughWallsTint = new(1f, 1f, 1f, 0.8f);

    /// <summary>
    /// DV374: the picture is drawn again where the walls hide it (the own mob's SEE_MOBS, tg's thermal vision: shaders/see_mobs.gdshader),
    /// over the world and under the things in the own hands.
    /// </summary>
    public bool ThroughWalls
    {
        get => _wantThroughWalls;
        set
        {
            if (value == _wantThroughWalls)
                return;
            _wantThroughWalls = value;
            SyncThroughWalls();
        }
    }

    private void SyncThroughWalls()
    {
        if (!_wantThroughWalls || _raster == null)
        {
            if (_throughWalls != null)
                _throughWalls.Visible = false;
            return;
        }
        if (_throughWalls == null)
        {
            _seeMobsShader ??= GD.Load<Shader>("res://shaders/see_mobs.gdshader");
            var material = new ShaderMaterial { Shader = _seeMobsShader, RenderPriority = MesonLayer.Priority };
            material.SetShaderParameter("tint", ThroughWallsTint);
            // IgnoreOcclusionCulling: the walls' occluders must not cull what is drawn through them
            _throughWalls = new MeshInstance3D { Name = "ThroughWalls", Mesh = _quad, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, IgnoreOcclusionCulling = true };
            AddChild(_throughWalls); // after the picture's own instance: EntityLayer reads the quad as the first child
        }
        ((ShaderMaterial)_throughWalls.MaterialOverride).SetShaderParameter("picture", _pictureTexture);
        _throughWalls.SetInstanceShaderParameter("depth_rank", _depthRank);
        _throughWalls.SetInstanceShaderParameter("depth_snap", _depthSnap);
        _throughWalls.Visible = true;
    }

    /// <summary>DV232: drawn over everything, unlit (tg's POINT_PLANE: the point arrow); its picture's texture on <c>picture_billboard_ontop</c>.</summary>
    public bool OnTop { get; set; }

    /// <summary>The texture of the picture now shown (its material's <c>picture</c>); Nil when none.</summary>
    public Variant PictureTexture => _raster == null ? default : _pictureTexture;

    /// <summary>The picture now shown; null when none.</summary>
    public ComposedRaster Raster => _raster;

    /// <summary>The material that draws the picture at rest (the lunge and the throw swap theirs in and back).</summary>
    public Material PictureMaterial => _instance?.MaterialOverride;

    /// <summary>True while the mob lies (the picture is tg's turned sprite).</summary>
    public bool Lying => _lying;

    /// <summary>The clear rows under the drawn pixels the picture was dropped by (resting them on the floor); 0 at the tile scale standing.</summary>
    public int BottomTrim { get; private set; }

    /// <summary>The rectangle of the picture around the feet.</summary>
    public SpriteRect Rect { get; private set; }

    private float _fullbright;

    /// <summary>
    /// DV67: the picture glows in its own colours whatever the light, this many times as bright, as tg draws a thing on ABOVE_LIGHTING_PLANE
    /// (the singularity); 0: lit as any picture. Its materials' <c>fullbright</c> instance uniform.
    /// </summary>
    public float Fullbright
    {
        get => _fullbright;
        set
        {
            if (_instance == null)
                _Ready();
            if (value == _fullbright)
                return;
            _fullbright = value;
            _instance.SetInstanceShaderParameter("fullbright", value);
        }
    }

    /// <summary>D288: whether the picture casts the lamps' and the sun's shadows (a mob's, with the preset's character shadows).</summary>
    public bool CastsShadow
    {
        get => _castsShadow;
        set
        {
            _castsShadow = value;
            if (_instance != null)
                Apply();
        }
    }

    private bool _castsShadow;

    /// <summary>D288 (the owner, 2026-10-29): the picture is not drawn, only its shadow is (the own body in first person); nothing without <see cref="CastsShadow"/>.</summary>
    public bool ShadowOnly
    {
        get => _shadowOnly;
        set
        {
            if (_shadowOnly == value)
                return;
            _shadowOnly = value;
            if (_instance != null)
                Apply();
        }
    }

    private bool _shadowOnly;

    /// <summary>Whether this picture needs its own reflection in a mirror (set once, at creation: EntityLayer, a mob or an item but not a
    /// point arrow, a mining ping or a visual). Its own quad, drawn as itself, additionally keeps off a mirror's camera
    /// (LightLayer.MirrorHiddenLayer) so only its reflection twin (SetReflection) is seen there, never both.</summary>
    public bool Reflects
    {
        get => _reflects;
        set
        {
            if (_reflects == value)
                return;
            _reflects = value;
            if (_instance != null)
                Apply();
        }
    }

    private bool _reflects;

    // a casting picture is on the characters' layer, which only the nearest lamps' shadows hold (D288); the own body's in first person on its own
    private void Apply()
    {
        _instance.CastShadow = !_castsShadow ? GeometryInstance3D.ShadowCastingSetting.Off
            : _shadowOnly ? GeometryInstance3D.ShadowCastingSetting.ShadowsOnly : GeometryInstance3D.ShadowCastingSetting.On;
        uint shadowLayer = !_castsShadow ? 1u : _shadowOnly ? Anomaly.Client.Light.LightLayer.OwnBodyLayer : Anomaly.Client.Light.LightLayer.CharacterLayer;
        uint layers = _reflects && !_shadowOnly ? shadowLayer | Anomaly.Client.Light.LightLayer.MirrorHiddenLayer : shadowLayer;
        if (_instance.Layers != layers)
        {
            // Godot 4.7 (Forward+) keeps a geometry's light pairs across a layer change and then cannot unpair it from a lamp whose cull mask
            // leaves out the new layer: the freed lamp dangles in the geometry's list and a later cull crashes. Hidden, it has no pairs.
            _instance.Visible = false;
            _instance.Layers = layers;
        }
        _instance.Visible = !_shadowOnly || _castsShadow;
    }

    // A mob (or the own hidden body) often faces a different side in a mirror than the main camera sees: this quad, for the mirrors'
    // cameras alone (Render/MirrorPass.cs: LightLayer.MirrorOnlyLayer), carries its own texture, set by EntityLayer's SetReflection; the
    // main instance above is unaffected and keeps whatever the main camera needs (lit as any picture; no shadow: it is not a real surface).
    private MeshInstance3D _reflection;

    /// <summary>
    /// The reflection twin's texture (EntityLayer, with the mirror's own eye, MirrorViewer); null hides it (no live mirror, out of its
    /// reach, or an item or a visual, which keep the one side for simplicity).
    /// </summary>
    public void SetReflection(GodotPicture picture)
    {
        if (picture == null)
        {
            if (_reflection != null)
                _reflection.Visible = false;
            return;
        }
        if (_instance == null)
            _Ready();
        if (_reflection == null)
        {
            _reflection = new MeshInstance3D { Name = "Reflection", Mesh = _quad, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Layers = Anomaly.Client.Light.LightLayer.MirrorOnlyLayer };
            AddChild(_reflection); // after the picture's own instance: EntityLayer reads the quad as the first child
        }
        _reflection.Visible = true;
        _reflection.MaterialOverride = picture.Standing;
        _reflection.SetInstanceShaderParameter("depth_rank", _depthRank);
        _reflection.SetInstanceShaderParameter("depth_snap", _depthSnap);
        _reflection.SetInstanceShaderParameter("face_eye", _facesEye);
    }
    private float _depthRank;
    private bool _depthSnap;
    private float _sway;

    /// <summary>DV434: the walking tilt about the feet in the picture's plane (radians; Shared/Play/WalkSway.cs), on the picture and its copies.</summary>
    public float Sway
    {
        get => _sway;
        set
        {
            if (value == _sway)
                return;
            if (_instance == null)
                _Ready();
            _sway = value;
            _instance.SetInstanceShaderParameter("sway", value);
            _throughWalls?.SetInstanceShaderParameter("sway", value);
            _reflection?.SetInstanceShaderParameter("sway", value);
        }
    }

    private bool _facesEye;

    /// <summary>
    /// An item on the floor: the picture faces the eye fully, its up the camera's up tilted back about its feet (the shaders' <c>face_eye</c>;
    /// the owner, 2026-11-19), not turned about the vertical axis only as a mob's.
    /// </summary>
    public bool FacesEye
    {
        get => _facesEye;
        set
        {
            if (value == _facesEye)
                return;
            if (_instance == null)
                _Ready();
            _facesEye = value;
            _instance.SetInstanceShaderParameter("face_eye", value);
            _reflection?.SetInstanceShaderParameter("face_eye", value);
        }
    }

    private bool _facesBack;

    /// <summary>
    /// T5a: the own body in first person (its picture and its shadow): its eye is ahead of it, so the card across the view shows the face a viewer
    /// ahead of the body sees, its picture's right on the view's left (the shaders' <c>face_back</c>), where any other picture's right is the view's.
    /// </summary>
    public bool FacesBack
    {
        get => _facesBack;
        set
        {
            if (value == _facesBack)
                return;
            if (_instance == null)
                _Ready();
            _facesBack = value;
            _instance.SetInstanceShaderParameter("face_back", value);
        }
    }

    private bool _flat;

    /// <summary>
    /// The picture lies flat on the floor, its up pointing north, about the icon square's centre (the shaders' <c>lie_flat</c>): a type tuned so
    /// (WorldLooks) or a thing of the floor's layers that moved (a gib that slid), as the batch draws a floor decal.
    /// </summary>
    public bool LiesFlat
    {
        get => _flat;
        set
        {
            if (value == _flat)
                return;
            if (_instance == null)
                _Ready();
            _flat = value;
            _instance.SetInstanceShaderParameter("lie_flat", value);
            if (_raster != null)
            {
                _unshifted = Unshifted(out int trim);
                BottomTrim = trim;
                Place();
            }
        }
    }

    // The picture's rectangle before DV49's move across the view: flat about the square's centre, or standing on the floor.
    private SpriteRect Unshifted(out int trim)
    {
        trim = 0;
        return _flat ? SpriteRect.OfFlat(_raster.Image.Width, _raster.Image.Height, _raster.OriginX, _raster.OriginY).Times(_itemScale)
            : AtlasBuilder.PictureRect(_raster, _lying, _itemScale, _hostTrim, out trim);
    }

    /// <summary>Its place in its tile's draw order (<see cref="DepthRank"/>), at its tile's centre (<paramref name="snap"/>: an item) or where it stands (a mob).</summary>
    public void SetDepthRank(float rank, bool snap)
    {
        if (_instance == null)
            _Ready();
        if (rank == _depthRank && snap == _depthSnap)
            return;
        _depthRank = rank;
        _depthSnap = snap;
        _instance.SetInstanceShaderParameter("depth_rank", rank);
        _instance.SetInstanceShaderParameter("depth_snap", snap);
        if (_throughWalls != null && _wantThroughWalls)
            SyncThroughWalls();
        if (_reflection != null)
        {
            _reflection.SetInstanceShaderParameter("depth_rank", rank);
            _reflection.SetInstanceShaderParameter("depth_snap", snap);
        }
    }

    public override void _Ready()
    {
        _quad = new QuadMesh();
        _instance = new MeshInstance3D { Mesh = _quad };
        Apply();
        AddChild(_instance);
    }

    /// <summary>
    /// Sets <paramref name="picture"/> (the composed <paramref name="raster"/>); lying or standing. A lying mob and a standing ITEM (D121) are drawn with
    /// their drawn pixels' bottom on the floor; <paramref name="itemScale"/> below 1 draws the item at that share of the tile scale. <paramref name="hostTrim"/>:
    /// an image hanging on a mob drops its host's clear rows instead of its own (<see cref="AtlasBuilder.PictureRect"/>).
    /// </summary>
    public void SetPicture(ComposedRaster raster, GodotPicture picture, bool lying, float itemScale = 1f, int? hostTrim = null)
    {
        if (_instance == null)
            _Ready();
        if (ReferenceEquals(raster, _raster) && lying == _lying && itemScale == _itemScale && hostTrim == _hostTrim)
            return;
        _raster = raster;
        _lying = lying;
        _itemScale = itemScale;
        _hostTrim = hostTrim;
        _unshifted = Unshifted(out int trim);
        BottomTrim = trim;
        Place();
        _pictureTexture = picture.Standing.GetShaderParameter("picture");
        SyncThroughWalls();
        if (OnTop)
        {
            _onTopShader ??= GD.Load<Shader>("res://shaders/picture_billboard_ontop.gdshader");
            _onTop ??= new ShaderMaterial { Shader = _onTopShader };
            _onTop.SetShaderParameter("picture", picture.Standing.GetShaderParameter("picture"));
            _instance.MaterialOverride = _onTop;
            _instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            return;
        }
        _instance.MaterialOverride = picture.Standing;
    }

    /// <summary>
    /// DV49: moves the picture <paramref name="metres"/> along the camera's right, as the billboard turns (a big icon tg centres on its tile
    /// by its own pixel_x: the shift belongs to the picture, which faces the viewer, not to the ground under it).
    /// </summary>
    public void ShiftAcross(float metres)
    {
        if (metres == _across)
            return;
        _across = metres;
        if (_raster != null)
            Place();
    }

    private void Place()
    {
        SpriteRect rect = _unshifted with { Left = _unshifted.Left + _across, Right = _unshifted.Right + _across };
        Rect = rect;
        _quad.Size = new Vector2((float)rect.Width, (float)rect.Height);
        _quad.CenterOffset = new Vector3((float)(rect.Left + rect.Right) / 2f, (float)(rect.Bottom + rect.Top) / 2f, 0);
    }

    /// <summary>Shows nothing.</summary>
    public void Clear()
    {
        _raster = null;
        if (_instance != null)
            _instance.MaterialOverride = null;
        SyncThroughWalls();
    }

    /// <summary>
    /// Whether the ray hits this picture's opaque pixels. <paramref name="cameraRight"/> is the camera's right vector (the picture
    /// is turned to the camera about the vertical axis: it spans the camera's right and up).
    /// </summary>
    public bool TryPick(Vector3 origin, Vector3 direction, float maxDistance, Basis camera, out float distance, out Vector3 point)
    {
        Vector3 cameraRight = camera.X;
        distance = 0;
        point = default;
        if (_raster == null || !Visible)
            return false;
        Vector3 here = GlobalPosition;
        var from = new Vec3(origin.X, origin.Y, origin.Z);
        var dir = new Vec3(direction.X, direction.Y, direction.Z);
        BillboardHit hit;
        var right = new Vec3(cameraRight.X, 0, cameraRight.Z);
        double length = right.Length;
        if (length < 1e-6)
            return false;
        right *= 1 / length;
        var feet = new Vec3(here.X, here.Y, here.Z);
        bool found;
        if (_flat)
            found = BillboardMath.RayPlane(from, dir, maxDistance, feet, new Vec3(1, 0, 0), new Vec3(0, 0, -1), Rect, _raster.Image, out hit); // (east, north)
        else if (_facesEye)
        {
            // the shader's up: the camera's back x the ground right, flat on the floor past straight down
            Vector3 up = camera.Z.Cross(new Vector3((float)right.X, 0, (float)right.Z));
            up = (up with { Y = Mathf.Max(up.Y, 0f) }).Normalized();
            found = BillboardMath.RayPlane(from, dir, maxDistance, feet, right, new Vec3(up.X, up.Y, up.Z), Rect, _raster.Image, out hit);
        }
        else
            found = BillboardMath.RayStanding(from, dir, maxDistance, feet, right, Rect, _raster.Image, out hit);
        if (!found)
            return false;
        distance = (float)hit.Distance;
        point = new Vector3((float)hit.Point.X, (float)hit.Point.Y, (float)hit.Point.Z);
        return true;
    }
}
