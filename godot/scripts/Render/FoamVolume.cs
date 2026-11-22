using System;
using Godot;
using Anomaly.Client.Shared;

namespace Anomaly.Client.Render;

// tg draws its foam (/obj/effect/particle_effect/fluid/foam, effects_foam.dm) as a 32-pixel picture filling each tile it covers, over what lies
// there, and the foamed metal it leaves (/obj/structure/foamedmetal) as a dense, opaque tile of the same kind. A standing billboard reads as a
// sheet of paper in first person, so the client draws both as a volume on the tile, each face showing the entity's own composed picture: tg's
// icon state (foam, mfoam; metalfoam, ironfoam, atmos_resin), its dissolve flick ([state]-disolve, played by the entity layer's flick as on any
// picture), its reagent colour and its alpha (the resin's 120 is blended). The foam is a low mound a person stands in; the foamed metal a block as
// tall as the storey. DEVIATION(DV247): see docs/ai/DEVIATIONS.md (proposed by gaps F1).

/// <summary>A tile of tg's foam or foamed metal: a box of five faces (no bottom) showing the entity's picture.</summary>
public partial class FoamVolume : MeshInstance3D
{
    /// <summary>tg's foam fluids, every kind.</summary>
    public const string FoamType = "/obj/effect/particle_effect/fluid/foam";

    /// <summary>tg's foamed metal walls, the iron and the resin ones too.</summary>
    public const string FoamedMetalType = "/obj/structure/foamedmetal";

    /// <summary>tg's wooden barricades (deployable.dm), the crude plank ones too: as flat in first person as the foamed metal (DV247).</summary>
    public const string WoodenBarricadeType = "/obj/structure/barricade/wooden";

    /// <summary>tg's security barrier (deployable.dm, the grenade's and the locker's): a block like the wooden barricade.</summary>
    public const string SecurityBarricadeType = "/obj/structure/barricade/security";

    /// <summary>The foam mound's height (metres): about a third of a tile, a person's knees.</summary>
    public const float MoundHeight = 0.7f;

    private static ArrayMesh _unit;
    private static Shader _opaque;
    private static Shader _translucent;

    private ShaderMaterial _material;
    private Texture2D _texture;
    private bool _translucentNow;

    /// <summary>Whether this type is drawn as a foam volume.</summary>
    public static bool Applies(string typePath) => IsFoam(typePath) || IsBlock(typePath);

    /// <summary>tg's foam fluid (a mound), as opposed to the foamed metal (a block).</summary>
    public static bool IsFoam(string typePath) => Is(typePath, FoamType);

    /// <summary>A storey-tall block: tg's foamed metal, iron foam or atmos resin wall, or a wooden barricade.</summary>
    public static bool IsBlock(string typePath) => Is(typePath, FoamedMetalType) || Is(typePath, WoodenBarricadeType) || Is(typePath, SecurityBarricadeType);

    private static bool Is(string path, string prefix)
        => path != null && path.StartsWith(prefix, StringComparison.Ordinal) && (path.Length == prefix.Length || path[prefix.Length] == '/');

    /// <summary>Makes the volume of one tile under <paramref name="parent"/> (the entity's node, at the tile's floor centre).</summary>
    public static FoamVolume Create(Node3D parent, bool block)
    {
        float height = block ? TileSpace.LevelHeight : MoundHeight;
        var volume = new FoamVolume
        {
            Name = "Foam",
            Mesh = _unit ??= UnitBox(),
            Scale = new Vector3(TileSpace.TileSize, height, TileSpace.TileSize),
            CastShadow = block ? ShadowCastingSetting.On : ShadowCastingSetting.Off,
        };
        parent.AddChild(volume);
        return volume;
    }

    /// <summary>Shows <paramref name="texture"/> (the entity's composed picture now) on every face; blended when the picture is see-through.</summary>
    public void Set(Texture2D texture, bool translucent)
    {
        if (_material == null || translucent != _translucentNow)
        {
            _translucentNow = translucent;
            _material = new ShaderMaterial
            {
                Shader = translucent
                    ? _translucent ??= GD.Load<Shader>("res://shaders/foam_volume_translucent.gdshader")
                    : _opaque ??= GD.Load<Shader>("res://shaders/foam_volume.gdshader"),
            };
            MaterialOverride = _material;
            _texture = null;
        }
        if (!ReferenceEquals(texture, _texture))
        {
            _texture = texture;
            _material.SetShaderParameter("picture", texture);
        }
    }

    // A unit box standing on its base's centre (x and z from -0.5 to 0.5, y from 0 to 1): the top and the four sides, each the whole picture with its
    // top-left at the face's top-left as the viewer outside sees it (the top: north up, as tg draws it from above).
    private static ArrayMesh UnitBox()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Face(Vector3 topLeft, Vector3 topRight, Vector3 bottomRight, Vector3 bottomLeft, Vector3 normal)
        {
            (Vector3 at, Vector2 uv)[] corners =
            {
                (topLeft, new Vector2(0, 0)), (topRight, new Vector2(1, 0)), (bottomRight, new Vector2(1, 1)),
                (topLeft, new Vector2(0, 0)), (bottomRight, new Vector2(1, 1)), (bottomLeft, new Vector2(0, 1)),
            };
            foreach ((Vector3 at, Vector2 uv) in corners)
            {
                surface.SetNormal(normal);
                surface.SetUV(uv);
                surface.AddVertex(at);
            }
        }
        const float h = 0.5f;
        // north is -z (TileSpace.WorldZ), east is +x
        Face(new Vector3(-h, 1, -h), new Vector3(h, 1, -h), new Vector3(h, 1, h), new Vector3(-h, 1, h), Vector3.Up);
        Face(new Vector3(-h, 1, h), new Vector3(h, 1, h), new Vector3(h, 0, h), new Vector3(-h, 0, h), Vector3.Back); // south face
        Face(new Vector3(h, 1, -h), new Vector3(-h, 1, -h), new Vector3(-h, 0, -h), new Vector3(h, 0, -h), Vector3.Forward); // north face
        Face(new Vector3(h, 1, h), new Vector3(h, 1, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h), Vector3.Right); // east face
        Face(new Vector3(-h, 1, -h), new Vector3(-h, 1, h), new Vector3(-h, 0, h), new Vector3(-h, 0, -h), Vector3.Left); // west face
        return surface.Commit();
    }
}
