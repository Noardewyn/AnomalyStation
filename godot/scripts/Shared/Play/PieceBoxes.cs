using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// docs/ai/specs/interaction.md 4.1: the pick boxes of the D113 pieces (WorldGeometry.Pieces), a little thicker than what is drawn so that a thin
// slab can be aimed at. Local frame of a piece: metres east and SOUTH of the tile centre, as the mesh is built for a piece on the north side;
// PieceBoxes turns them by the piece's face into the pick's frame (east, north).

/// <summary>A box in the pick's frame, relative to the tile centre (east, north, up).</summary>
public readonly record struct PieceBox(double MinEast, double MinNorth, double MinUp, double MaxEast, double MaxNorth, double MaxUp);

/// <summary>The pick boxes of the shaped pieces.</summary>
public static class PieceBoxes
{
    /// <summary>The half thickness a thin slab is picked with (metres): never less, so that a 12 cm window is not a hair to aim at.</summary>
    public const double MinHalfThickness = 0.1;

    /// <summary>The depth of a firelock's frame at either edge of its tile (metres: the model's 0.26, models.json's firelocks).</summary>
    public const double FirelockDepth = 0.26;

    private static readonly byte[] Faces = { ByondDir.North, ByondDir.East, ByondDir.South, ByondDir.West };

    /// <summary>
    /// The boxes of <paramref name="placed"/> in the pick's frame around its tile centre. <paramref name="open"/> says a door stands open
    /// (its panels slid into the frame: only the ends can be aimed at; a pod door that dropped has none). Nothing for a shape that is not
    /// picked as a piece (a light fixture: <see cref="Lamp"/>, whose size comes from its picture).
    /// </summary>
    public static List<PieceBox> For(in Placed placed, double wallHeight, bool open, bool standalone = false)
    {
        var boxes = new List<PieceBox>(4);
        double top = wallHeight;
        double edge = WorldGeometry.Edge;
        double t = MinHalfThickness;
        switch (placed.Shape)
        {
            case RenderKind.Airlock:
                if (open)
                {
                    Local(boxes, -1.0, -0.15, -0.7, 0.15, 0, top, placed.Face);
                    Local(boxes, 0.7, -0.15, 1.0, 0.15, 0, top, placed.Face);
                }
                else
                    Local(boxes, -1.0, -Math.Max(t, 0.1), 1.0, Math.Max(t, 0.1), 0, top, placed.Face);
                break;
            case RenderKind.DoorAssembly:
                Local(boxes, -1.0, -Math.Max(t, 0.1), 1.0, Math.Max(t, 0.1), 0, top, placed.Face);
                break;
            case RenderKind.PodDoor:
                if (!open)
                    Local(boxes, -1.0, -0.12, 1.0, 0.12, 0, top, placed.Face);
                else // the lip an open pod door leaves under the ceiling (prop_atlas.gdshader)
                    Local(boxes, -1.0, -0.12, 1.0, 0.12, top - WorldGeometry.PodDoorLip, top, placed.Face);
                break;
            case RenderKind.Firelock:
                // its model and the model's copy stand at both edges of the tile, flush with the walls' faces (models.json: offset 0.87, frame 0.13; MapRenderer's MirrorFirelock)
                foreach (double z in new[] { -1.0 + FirelockDepth / 2, 1.0 - FirelockDepth / 2 })
                {
                    if (open)
                    {
                        Local(boxes, -1.0, z - FirelockDepth / 2, -0.8, z + FirelockDepth / 2, 0, top, placed.Face);
                        Local(boxes, 0.8, z - FirelockDepth / 2, 1.0, z + FirelockDepth / 2, 0, top, placed.Face);
                    }
                    else
                        Local(boxes, -1.0, z - FirelockDepth / 2, 1.0, z + FirelockDepth / 2, 0, top, placed.Face);
                }
                break;
            case RenderKind.Windoor:
                // An open windoor's glass has slid aside (prop_atlas.gdshader): only its far end is left to aim at, so a click goes through the doorway to the
                // table or the rack behind it (the owner's report, 2026-10-19: the click landed on the open door).
                if (open)
                    Local(boxes, 0.85, edge - t, 1.0, edge + t, 0, top, placed.Face);
                else
                    Local(boxes, -1.0, edge - t, 1.0, edge + t, 0, top, placed.Face);
                break;
            case RenderKind.DirectionalWindow:
            case RenderKind.FirelockBorder:
                Local(boxes, -1.0, edge - t, 1.0, edge + t, 0, top, placed.Face);
                break;
            case RenderKind.Railing:
                Local(boxes, -1.0, edge - t, 1.0, edge + t, 0, 1.02, placed.Face);
                break;
            case RenderKind.FullWindow:
            case RenderKind.Grille:
            case RenderKind.Girder:
            case RenderKind.FalseWall:
                if (WorldScale.BlockPieces)
                {
                    // D152 (block windows): the whole block; a grille under a window is only its bottom (a slab low over the floor, under the glass), so a ray at the
                    // window finds the window; a grille with no window (standalone) is a whole block of bars.
                    boxes.Add(placed.Shape == RenderKind.Grille && !standalone ? new PieceBox(-1.0, -1.0, 0, 1.0, 1.0, WorldGeometry.WindowBaseHeight + 0.05) : new PieceBox(-1.0, -1.0, 0, 1.0, 1.0, top));
                    break;
                }
                JoinedSlab(boxes, placed.Joints, top - (placed.Shape == RenderKind.Grille ? 0.05 : 0.0));
                break;
            case RenderKind.Table:
                // The whole body down to the floor, not the top alone: a ray aimed at the side of a table is a click on the table (tg's table fills its tile), never on the floor under it.
                boxes.Add(new PieceBox(-0.97, -0.97, 0, 0.97, 0.97, WorldGeometry.TableTop));
                break;
            case RenderKind.Curtain:
                Local(boxes, -1.0, -t, 1.0, t, 0, top, placed.Face);
                break;
            case RenderKind.Flap:
                Local(boxes, -0.77, -0.06, 0.77, 0.06, 0, top, 0);
                break;
        }
        return boxes;
    }

    /// <summary>
    /// A light fixture's pick box (D142: a lamp is aimed at and clicked, its tube taken out): the drawn box on the wall face, its half width
    /// and height as the renderer scaled them to the picture (never thinner than <see cref="MinHalfThickness"/>), a little deeper than drawn,
    /// centred at the fixture's height and moved along the wall by the root's pixel_x as it is drawn.
    /// </summary>
    public static PieceBox Lamp(in Placed placed, double halfWidth, double halfHeight)
    {
        var boxes = new List<PieceBox>(1);
        double hw = Math.Max(halfWidth, MinHalfThickness), hh = Math.Max(halfHeight, MinHalfThickness);
        Local(boxes, -hw, -1.0, hw, -1.0 + WorldGeometry.LampDepth + 0.06, placed.Height - hh, placed.Height + hh, placed.Face);
        PieceBox b = boxes[0];
        bool alongEast = placed.Face is ByondDir.North or ByondDir.South;
        double de = alongEast ? placed.OffsetX : 0, dn = alongEast ? 0 : placed.OffsetX;
        return new PieceBox(b.MinEast + de, b.MinNorth + dn, b.MinUp, b.MaxEast + de, b.MaxNorth + dn, b.MaxUp);
    }

    /// <summary>
    /// D152 (block windows): the boxes of a door, shutters or a firelock that shares its cell with a window block and is drawn on both faces of the glass
    /// (MapRenderer.SplitAcrossGlass): a sheet just outside each face, so the crosshair finds the shutters before the window behind them. An open pod door keeps its
    /// lip under the ceiling; any other open door has nothing to aim at.
    /// </summary>
    public static List<PieceBox> AcrossGlass(in Placed placed, double wallHeight, bool open)
    {
        var boxes = new List<PieceBox>(2);
        if (open && placed.Shape != RenderKind.PodDoor)
            return boxes;
        double y0 = open ? wallHeight - WorldGeometry.PodDoorLip : 0;
        const double reach = 0.16;
        Local(boxes, -1.0, 1.0, 1.0, 1.0 + reach, y0, wallHeight, placed.Face);
        Local(boxes, -1.0, -1.0 - reach, 1.0, -1.0, y0, wallHeight, placed.Face);
        return boxes;
    }

    // A joined window or grille is built in world directions: the centre post and an arm to each neighbour it joins (the joints mask).
    private static void JoinedSlab(List<PieceBox> boxes, byte joints, double top)
    {
        double t = MinHalfThickness;
        boxes.Add(new PieceBox(-t, -t, 0, t, t, top));
        if ((joints & Joints.East) != 0)
            boxes.Add(new PieceBox(t, -t, 0, 1.0, t, top));
        if ((joints & Joints.West) != 0)
            boxes.Add(new PieceBox(-1.0, -t, 0, -t, t, top));
        if ((joints & Joints.North) != 0)
            boxes.Add(new PieceBox(-t, t, 0, t, 1.0, top));
        if ((joints & Joints.South) != 0)
            boxes.Add(new PieceBox(-t, -1.0, 0, t, -t, top));
    }

    // A box of the north-side frame (x east, z south) turned by the face (0: not turned) into east and north.
    private static void Local(List<PieceBox> boxes, double x0, double z0, double x1, double z1, double y0, double y1, byte face)
    {
        // In (east, north): the north side has north = -z.
        double e0 = x0, e1 = x1, n0 = -z1, n1 = -z0;
        int quarter = face switch { ByondDir.East => 1, ByondDir.South => 2, ByondDir.West => 3, _ => 0 };
        for (int i = 0; i < quarter; i++)
        {
            // A quarter turn clockwise seen from above: (east, north) becomes (north, -east).
            (double ne0, double ne1, double nn0, double nn1) = (n0, n1, -e1, -e0);
            e0 = ne0;
            e1 = ne1;
            n0 = nn0;
            n1 = nn1;
        }
        boxes.Add(new PieceBox(Math.Min(e0, e1), Math.Min(n0, n1), y0, Math.Max(e0, e1), Math.Max(n0, n1), y1));
    }

    /// <summary>The face names, for the tests.</summary>
    internal static IReadOnlyList<byte> AllFaces => Faces;
}
