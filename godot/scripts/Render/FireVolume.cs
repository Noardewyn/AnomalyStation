using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Render;

// M4b slice 3 (fire, D18; D334): tg's hotspot (/obj/effect/hotspot) is a tile's air on fire, drawn as icons/effects/fire.dmi added
// over the tile (BLEND_ADD): its icon state the fire's stage ("light", "medium", "heavy": the hotspot's volume and whether it fills
// its tile), its colour the temperature's (heat2colour, update_color) and its alpha fainter for a cool fire. The client draws the
// same state as glowing air that fills the tile from the floor to the ceiling (shaders/fire_volume.gdshader), so a fire over open
// space burns in the air rather than standing on a floor that is not there: the stage sets how much of the air burns, the
// hotspot's light colour (set_light_color in update_color) tints it, tg's alpha dims it. The light itself is the hotspot's
// replicated light source (D24, DV242).

/// <summary>A burning tile's air: a glowing box that follows tg's fire stage, colour and alpha.</summary>
public partial class FireVolume : MeshInstance3D
{
    /// <summary>tg's three fire stages (the hotspot's icon states).</summary>
    public enum Stage { Light, Medium, Heavy }

    /// <summary>tg's <c>LIGHT_COLOR_FIRE</c> (#FAA019): the colour until the hotspot's light says otherwise.</summary>
    public static readonly Color DefaultTint = new(250 / 255f, 160 / 255f, 25 / 255f);

    private const float Overlap = 0.35f; // metres the box reaches past its tile (fire_volume's overlap)

    private static BoxMesh _mesh;
    private static ShaderMaterial _material;

    private Stage _stage = (Stage)(-1);
    private Color _tint = new(-1, -1, -1);
    private float _alpha = -1f;

    private const int Sparkles = 16, Dashes = 16, Bolts = 4, BoltSegments = 10; // fire_extras' BOLT_SEGMENTS
    private static ArrayMesh _extrasMesh;
    private static ShaderMaterial _extrasMaterial;
    private MeshInstance3D _extras;
    private (float Sparkle, bool Lightning, float Fusion) _extrasNow;

    /// <summary>Makes the fire of one tile under <paramref name="parent"/> (the hotspot's node, at the tile's floor).</summary>
    public static FireVolume Create(Node3D parent)
    {
        var fire = new FireVolume
        {
            Name = "Fire",
            MaterialOverride = Material(),
            Mesh = _mesh,
            CastShadow = ShadowCastingSetting.Off,
            Position = new Vector3(0, TileSpace.LevelHeight / 2f, 0),
        };
        parent.AddChild(fire);
        fire.Set(Stage.Light, DefaultTint, 1f);
        return fire;
    }

    /// <summary>tg's icon state of a hotspot to a stage ("light" when unknown).</summary>
    public static Stage StageOf(string iconState) => iconState switch
    {
        "heavy" => Stage.Heavy,
        "medium" => Stage.Medium,
        _ => Stage.Light,
    };

    /// <summary>Follows the hotspot's stage, colour and alpha (0 to 1); cheap when none changed.</summary>
    public void Set(Stage stage, Color tint, float alpha)
    {
        if (stage != _stage)
        {
            _stage = stage;
            // the heat the air needs to burn: tg's heavy sheet fills its tile, light is a few thin tongues
            SetInstanceShaderParameter("cut", stage switch { Stage.Heavy => 0.3f, Stage.Medium => 0.42f, _ => 0.52f });
        }
        if (!tint.IsEqualApprox(_tint))
        {
            _tint = tint;
            SetInstanceShaderParameter("tint", new Vector3(tint.R, tint.G, tint.B));
            _extras?.SetInstanceShaderParameter("tint", new Vector3(tint.R, tint.G, tint.B));
        }
        if (alpha != _alpha)
        {
            _alpha = alpha;
            SetInstanceShaderParameter("alpha", alpha);
        }
    }

    /// <summary>
    /// Follows tg's overlays of a very hot fire (update_color): the alpha of "shieldsparkles" (0 to 1), whether "overcharged" is
    /// on, the alpha of "fusion_gas". Their pixels are made the first time one shows; cheap when none changed.
    /// </summary>
    public void SetExtras(float sparkle, bool lightning, float fusion)
    {
        if (_extrasNow == (sparkle, lightning, fusion))
            return;
        _extrasNow = (sparkle, lightning, fusion);
        if (_extras == null)
        {
            if (sparkle <= 0 && !lightning && fusion <= 0)
                return;
            var air = new Vector3(TileSpace.TileSize, TileSpace.LevelHeight, TileSpace.TileSize);
            _extras = new MeshInstance3D
            {
                Name = "Extras",
                Mesh = _extrasMesh ??= ExtrasMesh(),
                MaterialOverride = _extrasMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/fire_extras.gdshader") },
                CastShadow = ShadowCastingSetting.Off,
                CustomAabb = new Aabb(-air / 2f, air), // the shader places the pixels
            };
            _extrasMaterial.SetShaderParameter("half_air", air / 2f);
            AddChild(_extras);
            _extras.SetInstanceShaderParameter("tint", new Vector3(_tint.R, _tint.G, _tint.B));
        }
        _extras.SetInstanceShaderParameter("sparkle", sparkle);
        _extras.SetInstanceShaderParameter("lightning", lightning ? 1f : 0f);
        _extras.SetInstanceShaderParameter("fusion", fusion);
    }

    // One quad per sparkle, dash and bolt segment, all on the node (the shader places them): the vertex colour's red numbers it,
    // green is its kind (0 sparkle, 1 dash, 2 bolt segment), blue its bolt; UV runs along (x) and across (y).
    private static ArrayMesh ExtrasMesh()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Quad(int number, int kind, int bolt)
        {
            foreach (Vector2 corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1) })
            {
                surface.SetColor(Color.Color8((byte)number, (byte)kind, (byte)bolt));
                surface.SetUV(corner);
                surface.AddVertex(Vector3.Zero);
            }
        }
        for (int i = 0; i < Sparkles; i++)
            Quad(i, 0, 0);
        for (int i = 0; i < Dashes; i++)
            Quad(i, 1, 0);
        for (int bolt = 0; bolt < Bolts; bolt++)
            for (int segment = 0; segment < BoltSegments; segment++)
                Quad(segment, 2, bolt);
        return surface.Commit();
    }

    // The box and the shader follow the wall height, an option set when a game scene starts.
    private static ShaderMaterial Material()
    {
        var air = new Vector3(TileSpace.TileSize, TileSpace.LevelHeight, TileSpace.TileSize);
        if (_material != null && _mesh.Size == air + Vector3.One * (2 * Overlap))
            return _material;
        _mesh = new BoxMesh { Size = air + Vector3.One * (2 * Overlap) };
        if (_material != null)
        {
            _material.SetShaderParameter("core_half", air / 2f);
            return _material;
        }
        var noise = new NoiseTexture3D
        {
            Width = 48,
            Height = 48,
            Depth = 48,
            Seamless = true,
            Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.1f, FractalOctaves = 2 },
        };
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/fire_volume.gdshader") };
        _material.SetShaderParameter("noise_tex", noise);
        _material.SetShaderParameter("core_half", air / 2f);
        _material.SetShaderParameter("overlap", Overlap);
        return _material;
    }
}
