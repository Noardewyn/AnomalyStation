namespace Anomaly.Client.Shared.World;

/// <summary>
/// The parts of the shaped-piece mesh (D113): one mesh holds every shape, an instance names the one it draws. The numbers are
/// what the prop shader (<c>shaders/prop_atlas.gdshader</c>) moves doors by, so they are fixed here.
/// </summary>
public static class PieceParts
{
    public const int FullWindow = 0;
    public const int DirectionalWindow = 1;
    public const int Grille = 2;
    public const int Table = 3;
    public const int Railing = 4;
    public const int Flap = 5;
    public const int Light = 6;
    public const int AirlockLeft = 7;
    public const int AirlockRight = 8;
    public const int PodDoor = 9;
    public const int Windoor = 10;

    /// <summary>A firelock's two slabs, one on each side of the doorway (M3 R2), and a border-only firelock's edge slab.</summary>
    public const int FirelockSouth = 11, FirelockNorth = 12, FirelockEdge = 13;

    /// <summary>
    /// The corner posts of the border pieces (M3 R3): the mesh part of a wall-high post (a directional window's, a border firelock's) and of a rail-high one (a railing's). A piece that
    /// needs a post gets one more instance of this part, with its own picture and the posts it needs as the joints mask (<see cref="BorderCorners.LowEnd"/>, <see cref="BorderCorners.HighEnd"/>).
    /// </summary>
    public const int BorderPost = 14, RailPost = 15;

    /// <summary>D147: the base under a full-tile window, a low steel block over the whole tile that covers the plating's pipes and cables; one more instance of the window.</summary>
    public const int WindowBase = 16;

    /// <summary>
    /// D152 (block windows) (<see cref="BlockFaces"/>): the faces of a tile-sized block (a full-tile window, a grille or a girder), each an instance of its own
    /// with the smoothing picture of that face; a face against what covers it is not drawn at all.
    /// </summary>
    public const int BlockSouth = 17, BlockNorth = 18, BlockEast = 19, BlockWest = 20, BlockTop = 21;

    /// <summary>The block's bottom, lying flat and seen from above (the owner: a grille block is only its bottom, so the bars lie under the glass and its sides stay clear).</summary>
    public const int BlockBottom = 22;
    public const int Count = 23;

    /// <summary>The instance modes of the prop shader (colour r = mode / 8): M3 R3 added the item (a billboard at the item scale resting on its support), the floor decal (a hatch lying on the floor) and the resting billboard (the tile scale, resting on its support).</summary>
    public const int ModeBillboard = 0, ModeDecal = 1, ModePiece = 2, ModeItem = 3, ModeFloorDecal = 4, ModeResting = 5;

    /// <summary>
    /// The instance mode of a 3D model part (docs/ai/specs/models.md): its own mesh, drawn whole; its UV2.x is how far it slides along its x when the instance's open fraction
    /// (colour a) is 1 (an airlock's leaf), its UV2.y -1 marks a glass face.
    /// </summary>
    public const int ModeModel = 6;

    /// <summary>A floor decal's height above the floor (metres) travels in the instance colour's green as metres times this (the shader divides it back out).</summary>
    public const float LiftScale = 16f;

    /// <summary>The parts an entity of this shape draws (an airlock draws two panels).</summary>
    public static int[] PartsOf(RenderKind shape) => shape switch
    {
        RenderKind.FullWindow or RenderKind.Girder or RenderKind.FalseWall => new[] { FullWindow },
        RenderKind.DirectionalWindow => new[] { DirectionalWindow },
        RenderKind.Grille => new[] { Grille },
        RenderKind.Table => new[] { Table },
        RenderKind.Railing => new[] { Railing },
        RenderKind.Flap => new[] { Flap },
        RenderKind.LightFixture => new[] { Light },
        RenderKind.Airlock or RenderKind.DoorAssembly => new[] { AirlockLeft, AirlockRight },
        RenderKind.PodDoor or RenderKind.Curtain => new[] { PodDoor },
        RenderKind.Windoor => new[] { Windoor },
        RenderKind.Firelock => new[] { FirelockSouth, FirelockNorth },
        RenderKind.FirelockBorder => new[] { FirelockEdge },
        _ => Array.Empty<int>(),
    };

    /// <summary>The part of the extra instance that draws the corner posts of a border piece of this shape; -1 for a shape that has none.</summary>
    public static int PostPartOf(RenderKind shape) => shape switch
    {
        RenderKind.DirectionalWindow or RenderKind.FirelockBorder => BorderPost,
        RenderKind.Railing => RailPost,
        _ => -1,
    };

    /// <summary>True for the shapes that animate (the door shapes).</summary>
    public static bool IsDoor(RenderKind shape) => shape is RenderKind.Airlock or RenderKind.PodDoor or RenderKind.Windoor;

    /// <summary>
    /// D288: a door that shut is opaque in tg (door.dm's opacity), so it casts every lamp's shadow as a wall does: an airlock or a blast door, not a
    /// glass one (glass = TRUE: tg's glass airlocks and window shutters), a firelock or a windoor, which tg's light passes.
    /// </summary>
    public static bool BlocksLight(RenderKind shape, string typePath)
        => shape is RenderKind.Airlock or RenderKind.PodDoor && typePath != null
            && !typePath.Contains("glass", StringComparison.Ordinal) && !typePath.Contains("/shutters/window", StringComparison.Ordinal);

    /// <summary>True when the shape's picture is a flat colour swatch rather than the entity's picture (bars, a light's glow).</summary>
    public static bool UsesSwatch(RenderKind shape) => shape is RenderKind.Grille or RenderKind.LightFixture;
}
