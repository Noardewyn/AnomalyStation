using System.Numerics;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// D152 (block windows) (BlockFaces): a full-tile window, a grille and a girder drawn as a tile-sized block; every side face of a window or girder
/// shows tg's lone pane, the frame round all four edges, so each block of a run is framed.
/// </summary>
public class BlockFacesTests
{
    [Fact]
    public void ARunOfThreeShowsItsFrameOnlyAtTheEnds()
    {
        // west end, middle, east end of an east-west run (tg's junctions: EAST, EAST|WEST, WEST)
        Assert.Equal("window-4", BlockFaces.FaceState("window-4", ByondDirConst.South));
        Assert.Equal("window-12", BlockFaces.FaceState("window-12", ByondDirConst.South));
        Assert.Equal("window-8", BlockFaces.FaceState("window-8", ByondDirConst.South));
        // seen from the north the run is mirrored: the west end is on the viewer's right
        Assert.Equal("window-8", BlockFaces.FaceState("window-4", ByondDirConst.North));
        Assert.Equal("window-12", BlockFaces.FaceState("window-12", ByondDirConst.North));
    }

    [Fact]
    public void ASideFaceTakesItsOwnNeighboursAndTheTopTheWholeJunction()
    {
        // a north-south run's middle seen from the east: south on the left, north on the right
        Assert.Equal("reinforced_window-12", BlockFaces.FaceState("reinforced_window-3", ByondDirConst.East));
        Assert.Equal("reinforced_window-4", BlockFaces.FaceState("reinforced_window-1", ByondDirConst.East));
        Assert.Equal("reinforced_window-8", BlockFaces.FaceState("reinforced_window-1", ByondDirConst.West));
        // the end face of an east-west run joins nothing on its own sides: the whole frame
        Assert.Equal("window-0", BlockFaces.FaceState("window-12", ByondDirConst.East));
        // the top: the junction as tg draws it, diagonals and all
        Assert.Equal("window-23", BlockFaces.FaceState("window-23", 0));
        Assert.Null(BlockFaces.FaceState("grille", ByondDirConst.South));
    }

    [Fact]
    public void AWindowJoinedToAWallKeepsItsFrameThere()
    {
        // a feed that joined the run's west end to a wall (junction 12): only the window east of it joins, so the frame shows on the west
        Assert.Equal("window-4", BlockFaces.FaceState("window-12", ByondDirConst.South, ByondDirConst.East));
        // a diagonal stays only with both of its sides
        Assert.Equal(1 | 4 | 16, BlockFaces.Restrict(1 | 4 | 16, 1 | 4 | 8));
        Assert.Equal(1, BlockFaces.Restrict(1 | 4 | 16, 1));
    }

    [Fact]
    public void AnLOrTCornerIsFramedOutsideAndInside()
    {
        // the owner (2026-11-10): an L's corner block joins west and south (junction 10); its north and east faces are the outside of the turn, framed at the corner
        Assert.Equal("window-4", BlockFaces.FaceState("window-10", ByondDirConst.North));
        Assert.Equal("window-8", BlockFaces.FaceState("window-10", ByondDirConst.East));
        // the inside: the west arm (junction 4, the corner east of it) has the south arm's top diagonally south-east of it: its south face is framed on its right
        Assert.Equal("window-4", BlockFaces.FaceState("window-4", ByondDirConst.South));
        Assert.Equal("window-0", BlockFaces.FaceState("window-4", ByondDirConst.South, diagonals: Smoothing.SouthEast));
        // and the south arm (junction 1, the corner north of it) has the west arm diagonally north-west: its west face is framed on its left (north)
        Assert.Equal("window-0", BlockFaces.FaceState("window-1", ByondDirConst.West, diagonals: Smoothing.NorthWest));
        // a diagonal behind the face is no corner of it
        Assert.Equal("window-4", BlockFaces.FaceState("window-4", ByondDirConst.South, diagonals: Smoothing.NorthEast));
        Assert.Equal("window-12", BlockFaces.FaceState("window-12", ByondDirConst.South));
        // a T's bar (joined west, east and south): its north face joins both sides
        Assert.Equal("window-12", BlockFaces.FaceState("window-14", ByondDirConst.North));
        // a straight run's end keeps its frame, and so does its end face
        Assert.Equal("window-4", BlockFaces.FaceState("window-4", ByondDirConst.South));
        Assert.Equal("window-0", BlockFaces.FaceState("window-4", ByondDirConst.West));
    }

    [Fact]
    public void ARunOfDirectionalPanesShowsOneFrameAndIsFramedAtItsCorners()
    {
        // three panes on the north edges of (4..6, 5) and one on the east edge of (6, 5), meeting the run's east end at the tile corner
        var panes = new HashSet<(int, int, int, byte)> { (4, 5, 1, ByondDirConst.North), (5, 5, 1, ByondDirConst.North), (6, 5, 1, ByondDirConst.North), (6, 5, 1, ByondDirConst.East) };
        bool Pane(int x, int y, int z, byte face) => panes.Contains((x, y, z, face));
        // a north pane's low end is west: the run's west end keeps its frame, the middle joins both sides
        Assert.Equal(Smoothing.East, BlockFaces.BorderWindowJunction(4, 5, 1, ByondDirConst.North, Pane));
        Assert.Equal(Smoothing.EastWest, BlockFaces.BorderWindowJunction(5, 5, 1, ByondDirConst.North, Pane));
        // the owner (2026-10-29): each wall of panes is framed at its own ends, so at the corner both the run's east end and the east pane keep their frames
        Assert.Equal(Smoothing.West, BlockFaces.BorderWindowJunction(6, 5, 1, ByondDirConst.North, Pane));
        Assert.Equal(0, BlockFaces.BorderWindowJunction(6, 5, 1, ByondDirConst.East, Pane));
        // a lone pane: the whole frame
        Assert.Equal(0, BlockFaces.BorderWindowJunction(9, 9, 1, ByondDirConst.South, Pane));
    }

    [Fact]
    public void AFaceIsHiddenOnlyAgainstAWallOrAJoinedBlockOfItsKind()
    {
        static bool Wall(int x, int y, int z) => x == 4 && y == 5;
        static bool Window(int x, int y, int z) => (x == 6 || x == 5) && y == 5 || x == 5 && y == 6;
        // a window joined east (junction 4) with a wall west: the north window is not in its junction (it is unanchored), so the north face stays, and the face
        // against the wall is drawn too (the run's end framed)
        Assert.Equal(ByondDirConst.East, BlockFaces.Covered(5, 5, 1, RenderKind.FullWindow, "window-4", Wall, Window));
        // a grille has no junction: every grille beside it covers its face
        Assert.Equal(ByondDirConst.East | ByondDirConst.West | ByondDirConst.North, BlockFaces.Covered(5, 5, 1, RenderKind.Grille, "grille", Wall, Window));
        // nothing beside it: every face is drawn
        Assert.Equal(0, BlockFaces.Covered(5, 5, 1, RenderKind.Girder, "girder-0", (x, y, z) => false, (x, y, z) => false));
    }

    [Fact]
    public void TheBlockFacesCoverTheTileAndShowTheirPictureOnce()
    {
        const float wall = 2f;
        List<GeoQuad> pieces = WorldGeometry.Pieces(wall);
        foreach (byte face in BlockFaces.Sides.Append((byte)0))
        {
            GeoQuad q = Assert.Single(pieces, q => q.Part == BlockFaces.PartOf(face));
            Assert.Equal(0, q.Arm);
            Assert.Equal(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, q.Uvs);
            Assert.All(q.Corners, c => Assert.True(MathF.Abs(c.X) <= 1f && MathF.Abs(c.Z) <= 1f && c.Y >= 0f && c.Y <= wall));
            // the face lies on the tile's edge it looks out of (the top on the wall's height)
            Vector3 n = q.Normal;
            Assert.All(q.Corners, c => Assert.Equal(1f, n.Y > 0 ? c.Y / wall : Vector3.Dot(c, n), 4));
        }
        Assert.Equal(PieceParts.Count, pieces.Max(q => q.Part) + 1);
    }

    [Fact]
    public void TheSouthFaceRunsWestToEastAndTheNorthFaceEastToWest()
    {
        List<GeoQuad> pieces = WorldGeometry.Pieces(2f);
        GeoQuad south = pieces.Single(q => q.Part == PieceParts.BlockSouth);
        GeoQuad north = pieces.Single(q => q.Part == PieceParts.BlockNorth);
        GeoQuad east = pieces.Single(q => q.Part == PieceParts.BlockEast);
        // u 0 (the picture's west edge) at the viewer's left: the west for the south face, the east for the north face, the south (+z) for the east face
        Assert.Equal(-1f, south.A.X);
        Assert.Equal(1f, north.A.X);
        Assert.Equal(1f, east.A.Z);
    }
}
