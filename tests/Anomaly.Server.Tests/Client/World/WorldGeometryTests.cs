using System.Numerics;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 P6: the texture mapping of every face of the world renderer's meshes (the owner's rule, 2026-10-08): every face shows exactly ONE whole
/// tg sprite, uv 0..1 over the whole face, with no wrap or repeat; a share of a bigger picture (an airlock panel, a flap strip) shows exactly its
/// own share, and the shares together show the picture once.
/// </summary>
public sealed class WorldGeometryTests
{
    private static bool IsUnitSquare(IEnumerable<Vector2> uvs)
    {
        var set = new HashSet<(float, float)>(uvs.Select(u => (MathF.Round(u.X, 4), MathF.Round(u.Y, 4))));
        return set.SetEquals(new[] { (0f, 0f), (0f, 1f), (1f, 0f), (1f, 1f) });
    }

    private static bool IsFront(GeoQuad q) => q.Normal == Vector3.UnitZ;

    private static bool IsBack(GeoQuad q) => q.Normal == -Vector3.UnitZ;

    private static (float Min, float Max) URange(GeoQuad q) => (q.Uvs.Min(u => u.X), q.Uvs.Max(u => u.X));

    private static (float Min, float Max) VRange(GeoQuad q) => (q.Uvs.Min(u => u.Y), q.Uvs.Max(u => u.Y));

    [Fact]
    public void EveryUvOfEveryMeshIsInsideTheUnitSquare()
    {
        foreach (GeoQuad q in WorldGeometry.TileKit(Wall).Concat(WorldGeometry.Pieces(Wall)).Concat(WorldGeometry.PictureQuad()))
        {
            foreach (Vector2 uv in q.Uvs)
            {
                Assert.InRange(uv.X, -0.0001f, 1.0001f);
                Assert.InRange(uv.Y, -0.0001f, 1.0001f);
            }
        }
    }

    [Fact]
    public void FloorsCeilingsAndPicturesShowOneSpritePerTile()
    {
        List<GeoQuad> kit = WorldGeometry.TileKit(Wall);
        Assert.True(IsUnitSquare(kit.Single(q => q.Part == TileKitParts.Floor).Uvs));
        Assert.True(IsUnitSquare(kit.Single(q => q.Part == TileKitParts.Ceiling).Uvs));
        Assert.True(IsUnitSquare(WorldGeometry.PictureQuad().Single().Uvs));
        // The floor is one 2 x 2 m tile.
        GeoQuad floor = kit.Single(q => q.Part == TileKitParts.Floor);
        Assert.Equal(2f, floor.Corners.Max(c => c.X) - floor.Corners.Min(c => c.X));
        Assert.Equal(2f, floor.Corners.Max(c => c.Z) - floor.Corners.Min(c => c.Z));
    }

    private const float Wall = 2f; // D119: the default wall height; the tests give the geometry its height instead of reading the setting

    [Fact]
    public void AWallFaceMapsOneSpriteOver2By2MetresFootAtVOne()
    {
        List<GeoQuad> walls = WorldGeometry.TileKit(Wall).Where(q => q.Part == TileKitParts.Wall).ToList();
        Assert.Equal(5, walls.Count); // four sides and the top
        foreach (GeoQuad q in walls)
            Assert.True(IsUnitSquare(q.Uvs), "a wall face does not map the sprite exactly once: " + string.Join(" ", q.Uvs));
        foreach (GeoQuad side in walls.Where(q => MathF.Abs(q.Normal.Y) < 0.5f))
        {
            // 2 m tall, 2 m wide, and the foot is the picture's bottom (v 1), the top its top (v 0): one sprite, square.
            Assert.Equal(Wall, side.Corners.Max(c => c.Y) - side.Corners.Min(c => c.Y));
            for (int i = 0; i < 4; i++)
            {
                float expectedV = 1f - side.Corners[i].Y / Wall;
                Assert.Equal(expectedV, side.Uvs[i].Y, 4);
            }
        }
    }

    [Fact]
    public void AWallsTopLiesUnderTheFloorOfTheLevelAbove()
    {
        // D325: the level above's floor is at the wall's height; a top at that height fought with it
        foreach (GeoQuad top in WorldGeometry.TileKit(Wall).Where(q => q.Part is TileKitParts.Wall or TileKitParts.DiagonalWall && q.Normal.Y > 0.5f))
            Assert.All(top.Corners, c => Assert.Equal(Wall - WorldGeometry.TopSink, c.Y, 4));
        Assert.True(WorldGeometry.TopSink > 0.005f);
    }

    [Fact]
    public void AWallReadsTheRightWayRoundFromOutsideOnEverySide()
    {
        // u grows to the viewer's right: +X seen from the south, -Z from the east, -X from the north, +Z from the west.
        foreach (GeoQuad q in WorldGeometry.TileKit(Wall).Where(q => q.Part == TileKitParts.Wall && MathF.Abs(q.Normal.Y) < 0.5f))
        {
            Vector3 right = Vector3.Cross(Vector3.UnitY, q.Normal); // looking at the face from outside, the viewer's right
            float uAtMostRight = q.Corners.Select((c, i) => (Along: Vector3.Dot(c, right), U: q.Uvs[i].X)).OrderBy(t => t.Along).Last().U;
            Assert.Equal(1f, uAtMostRight, 4);
        }
    }

    // The faces of a part that are always drawn (M3 R3: a joined window or grille's arms and a border piece's corner posts have an Arm bit and are drawn by the instance's joints mask).
    private static List<GeoQuad> Part(int part) => WorldGeometry.Pieces(Wall).Where(q => q.Part == part && q.Arm == 0).ToList();

    // Every face of a part: the post and the four arms of a joined piece.
    private static List<GeoQuad> AllOf(int part) => WorldGeometry.Pieces(Wall).Where(q => q.Part == part).ToList();

    [Theory]
    [InlineData(PieceParts.DirectionalWindow, 2f)]
    [InlineData(PieceParts.PodDoor, 2f)]
    [InlineData(PieceParts.Windoor, 2f)]
    public void ASlabShowsOneWholeSpriteOnEachFace(int part, float height)
    {
        foreach (GeoQuad q in Part(part).Where(q => IsFront(q) || IsBack(q)))
        {
            Assert.True(IsUnitSquare(q.Uvs), $"part {part}: the face does not show the sprite exactly once");
            // v is the height on the sprite: the foot 0, the top 1 over the slab's whole height (no second sprite above).
            for (int i = 0; i < 4; i++)
                Assert.Equal(q.Corners[i].Y / height, q.Uvs[i].Y, 4);
        }
    }

    [Fact]
    public void TheAirlocksPanelsEachShowTheirHalfAndTogetherTheDoorOnce()
    {
        GeoQuad leftFront = Part(PieceParts.AirlockLeft).Single(IsFront);
        GeoQuad rightFront = Part(PieceParts.AirlockRight).Single(IsFront);
        Assert.Equal((0f, 0.5f), URange(leftFront));
        Assert.Equal((0.5f, 1f), URange(rightFront));
        Assert.Equal((0f, 1f), VRange(leftFront));
        Assert.Equal((0f, 1f), VRange(rightFront));
        // The shares meet exactly: no gap and no overlap, and they run left to right with x.
        Assert.Equal(URange(leftFront).Max, URange(rightFront).Min);
        // The back shows the mirror image, as a viewer from the north sees the door.
        GeoQuad leftBack = Part(PieceParts.AirlockLeft).Single(IsBack);
        GeoQuad rightBack = Part(PieceParts.AirlockRight).Single(IsBack);
        Assert.Equal((0.5f, 1f), URange(leftBack));
        Assert.Equal((0f, 0.5f), URange(rightBack));
        // Each panel is 1 m wide, 2 m tall: a half of the sprite over it, the whole height once.
        foreach (GeoQuad q in Part(PieceParts.AirlockLeft).Concat(Part(PieceParts.AirlockRight)).Where(q => IsFront(q) || IsBack(q)))
        {
            for (int i = 0; i < 4; i++)
                Assert.Equal(q.Corners[i].Y / Wall, q.Uvs[i].Y, 4);
        }
    }

    [Fact]
    public void TheFrontOfAPieceReadsLeftToRightAndItsBackIsMirrored()
    {
        foreach (int part in new[] { PieceParts.PodDoor, PieceParts.Windoor })
        {
            GeoQuad front = Part(part).Single(IsFront), back = Part(part).Single(IsBack);
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal((front.Corners[i].X + 1f) / 2f, front.Uvs[i].X, 4); // seen from the south +X is the viewer's right
                Assert.Equal((1f - back.Corners[i].X) / 2f, back.Uvs[i].X, 4);  // seen from the north -X is
            }
        }
    }

    [Fact]
    public void ADirectionalWindowsBackLaysItsPictureAsTheFront()
    {
        // the owner (2026-10-29): a run's side frame stays at the run's end on both faces of the pane, not mirrored onto the joint
        GeoQuad front = Part(PieceParts.DirectionalWindow).Single(IsFront), back = Part(PieceParts.DirectionalWindow).Single(IsBack);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal((front.Corners[i].X + 1f) / 2f, front.Uvs[i].X, 4);
            Assert.Equal((back.Corners[i].X + 1f) / 2f, back.Uvs[i].X, 4);
        }
    }

    [Fact]
    public void ATableShowsTheWholeSpriteOnEveryFaceOfTheTopAndTheLegs()
    {
        List<GeoQuad> table = Part(PieceParts.Table);
        Assert.Equal(5 + 4 * 4, table.Count); // a top of four sides and a top face, and four legs of four sides
        foreach (GeoQuad q in table)
            Assert.True(IsUnitSquare(q.Uvs), "a table face does not show the sprite once: " + string.Join(" ", q.Uvs));
        // The top face is 1.94 x 1.94 m and its sprite has north up.
        GeoQuad top = table.Single(q => q.Normal == Vector3.UnitY && q.Corners[0].Y > WorldGeometry.TableTop - 0.01f);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal((top.Corners[i].X - -0.97f) / 1.94f, top.Uvs[i].X, 3);
            Assert.Equal((0.97f - top.Corners[i].Z) / 1.94f, top.Uvs[i].Y, 3);
        }
    }

    [Fact]
    public void TheFlapStripsShowTheirSharesOfOneSprite()
    {
        List<GeoQuad> fronts = Part(PieceParts.Flap).Where(IsFront).ToList();
        Assert.Equal(4, fronts.Count);
        foreach (GeoQuad q in fronts)
        {
            // A strip shows the part of the picture that is under it: u is (x + 1) / 2 over the doorway, v the height over the wall's 2 m.
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal((q.Corners[i].X + 1f) / 2f, q.Uvs[i].X, 4);
                Assert.Equal(q.Corners[i].Y / Wall, q.Uvs[i].Y, 4);
            }
        }
        Assert.Equal(0f, fronts.Min(q => URange(q).Min), 1);
        Assert.Equal(1f, fronts.Max(q => URange(q).Max), 1);
    }

    [Fact]
    public void TheRailingsPartsShowSharesOfOnePictureNotRepeats()
    {
        foreach (GeoQuad q in Part(PieceParts.Railing).Where(q => IsFront(q) || IsBack(q)))
        {
            Assert.InRange(URange(q).Min, 0f, 1f);
            Assert.InRange(URange(q).Max, 0f, 1f);
            Assert.InRange(VRange(q).Max, 0f, 1f);
        }
        // The rail and the posts are one picture 2 m wide and 1.02 m high.
        GeoQuad rail = Part(PieceParts.Railing).Where(IsFront).OrderByDescending(q => q.Corners.Max(c => c.Y)).First();
        Assert.Equal((0f, 1f), URange(rail));
    }

    [Fact]
    public void EveryPartIsMeshedAndFacesOutward()
    {
        foreach (int part in Enumerable.Range(0, PieceParts.Count))
            Assert.NotEmpty(AllOf(part));
        foreach (GeoQuad q in WorldGeometry.Pieces(Wall).Concat(WorldGeometry.TileKit(Wall)))
        {
            // The corners go counter-clockwise seen from outside: the cross product of two edges points along the normal.
            Vector3 n = Vector3.Cross(q.B - q.A, q.C - q.A);
            Assert.True(Vector3.Dot(Vector3.Normalize(n), q.Normal) > 0.99f, "a quad winds the wrong way");
        }
    }

    // ---- D119: the wall is as high as a tile is wide ----

    // Metres of the face per unit of the sprite's u and v over the union of the given quads' corners (the sprite's whole width and height).
    private static (float PerU, float PerV) MetresPerSprite(IEnumerable<GeoQuad> faces, Func<Vector3, float> along)
    {
        var quads = faces.ToList();
        float x0 = quads.Min(q => q.Corners.Min(along)), x1 = quads.Max(q => q.Corners.Max(along));
        float y0 = quads.Min(q => q.Corners.Min(c => c.Y)), y1 = quads.Max(q => q.Corners.Max(c => c.Y));
        float u0 = quads.Min(q => q.Uvs.Min(u => u.X)), u1 = quads.Max(q => q.Uvs.Max(u => u.X));
        float v0 = quads.Min(q => q.Uvs.Min(u => u.Y)), v1 = quads.Max(q => q.Uvs.Max(u => u.Y));
        return ((x1 - x0) / (u1 - u0), (y1 - y0) / (v1 - v0));
    }

    [Fact]
    public void EveryFullHeightPiecesFaceIsASquareAtTheDefaultHeight()
    {
        Assert.Equal(2f, WorldScale.WallHeight); // the default (D119); nothing in the tests changes it
        // The pieces whose sprite is a whole tile square: each front face, and the airlock's two panels and the flap strips together, show
        // 2 m of the picture's width over 2 m of its height, so tg's 32 x 32 sprite is neither cut nor stretched.
        List<GeoQuad> pieces = WorldGeometry.Pieces();
        foreach (int[] group in new[]
        {
            new[] { PieceParts.FullWindow }, new[] { PieceParts.DirectionalWindow }, new[] { PieceParts.PodDoor }, new[] { PieceParts.Windoor },
            new[] { PieceParts.AirlockLeft, PieceParts.AirlockRight }, new[] { PieceParts.Flap },
        })
        {
            (float perU, float perV) = MetresPerSprite(pieces.Where(q => group.Contains(q.Part) && IsFront(q)), c => c.X);
            Assert.True(MathF.Abs(perU - perV) < 1e-3f && MathF.Abs(perU - 2f) < 1e-3f, $"parts {string.Join(",", group)}: {perU} m by {perV} m per sprite");
        }
        // The back faces are the same size, mirrored.
        (float backU, float backV) = MetresPerSprite(pieces.Where(q => q.Part == PieceParts.FullWindow && IsBack(q)), c => c.X);
        Assert.Equal(backU, backV, 3);
        // The wall box: each side is 2 m by 2 m, a square sprite.
        foreach (GeoQuad side in WorldGeometry.TileKit().Where(q => q.Part == TileKitParts.Wall && MathF.Abs(q.Normal.Y) < 0.5f))
        {
            Vector3 right = Vector3.Cross(Vector3.UnitY, side.Normal);
            (float perU, float perV) = MetresPerSprite(new[] { side }, c => Vector3.Dot(c, right));
            Assert.Equal(2f, perU, 3);
            Assert.Equal(2f, perV, 3);
        }
        // The grille is bars (a flat swatch, no sprite): its frame is as high as the wall less 10 cm, and a straight run of it (the post and the east and west arms) is the
        // tile wide, the rails reaching the tile's edges so that a row of grilles has continuous rails.
        List<GeoQuad> grille = pieces.Where(q => q.Part == PieceParts.Grille && q.Arm is 0 or Joints.East or Joints.West).ToList();
        float width = grille.Max(q => q.Corners.Max(c => c.X)) - grille.Min(q => q.Corners.Min(c => c.X));
        float height = grille.Max(q => q.Corners.Max(c => c.Y)) - grille.Min(q => q.Corners.Min(c => c.Y));
        Assert.Equal(2f, width, 3);
        Assert.Equal(1.9f, height, 3);
    }

    [Fact]
    public void TheCeilingAndEveryFullHeightPieceReachTheWallHeightAndNothingPastIt()
    {
        foreach (float wall in new[] { 2f, 2.4f, 3f })
        {
            List<GeoQuad> kit = WorldGeometry.TileKit(wall), pieces = WorldGeometry.Pieces(wall);
            Assert.Equal(wall, kit.Single(q => q.Part == TileKitParts.Ceiling).Corners.Max(c => c.Y));
            Assert.Equal(wall, kit.Where(q => q.Part == TileKitParts.Wall).Max(q => q.Corners.Max(c => c.Y)));
            foreach (int part in new[] { PieceParts.FullWindow, PieceParts.DirectionalWindow, PieceParts.PodDoor, PieceParts.Windoor, PieceParts.AirlockLeft, PieceParts.AirlockRight, PieceParts.Flap })
                Assert.Equal(wall, pieces.Where(q => q.Part == part).Max(q => q.Corners.Max(c => c.Y)));
            // The grille's frame stops a hair short of the ceiling; no part but the light box (placed at its mount height) is above the ceiling.
            Assert.InRange(pieces.Where(q => q.Part == PieceParts.Grille).Max(q => q.Corners.Max(c => c.Y)), wall - 0.06f, wall);
            Assert.All(kit.Concat(pieces.Where(q => q.Part != PieceParts.Light)), q => Assert.All(q.Corners, c => Assert.InRange(c.Y, -0.0001f, wall + 0.0001f)));
            // Whatever the height, a slab still shows its sprite once over its whole height (the one-sprite-per-face rule holds): a window's arms and post share it.
            foreach (GeoQuad q in pieces.Where(q => q.Part == PieceParts.FullWindow && (IsFront(q) || IsBack(q))))
            {
                for (int i = 0; i < 4; i++)
                    Assert.Equal(q.Corners[i].Y / wall, q.Uvs[i].Y, 4);
            }
            foreach (GeoQuad q in pieces.Where(q => q.Part == PieceParts.DirectionalWindow && q.Arm == 0 && (IsFront(q) || IsBack(q))))
            {
                Assert.True(IsUnitSquare(q.Uvs));
                for (int i = 0; i < 4; i++)
                    Assert.Equal(q.Corners[i].Y / wall, q.Uvs[i].Y, 4);
            }
        }
    }

    [Fact]
    public void TheLowPiecesKeepTheirOwnHeightsUnderTheCeiling()
    {
        List<GeoQuad> pieces = WorldGeometry.Pieces();
        Assert.Equal(WorldScale.TableTop, pieces.Where(q => q.Part == PieceParts.Table).Max(q => q.Corners.Max(c => c.Y)));
        Assert.Equal(0.75f, WorldScale.TableTop, 3); // half the 1.5 m eye (M3 R5, D137)
        Assert.Equal(1.02f, pieces.Where(q => q.Part == PieceParts.Railing).Max(q => q.Corners.Max(c => c.Y)));
        Assert.True(pieces.Where(q => q.Part == PieceParts.Table || q.Part == PieceParts.Railing).All(q => q.Corners.All(c => c.Y < WorldScale.WallHeight)));
    }

    // ---- M3 R3 (D121): windows and grilles joined by a post and arms, border pieces by corner posts ----

    private static bool IsEastFacing(GeoQuad q) => q.Normal == Vector3.UnitX;

    [Fact]
    public void AStraightRunOfAWindowIsThePostAndTwoArmsAndShowsTheWholePictureOnce()
    {
        List<GeoQuad> window = AllOf(PieceParts.FullWindow);
        // One always-drawn post and an arm in each of the four directions, nothing else.
        Assert.Equal(new[] { 0, Joints.North, Joints.South, Joints.East, Joints.West }, window.Select(q => q.Arm).Distinct().OrderBy(a => a).ToArray());
        // The east-west run (post, east arm, west arm): the front faces (+Z) share u 0..1 with no gap and no overlap, the picture's height once.
        List<GeoQuad> fronts = window.Where(q => IsFront(q) && q.Arm is 0 or Joints.East or Joints.West).OrderBy(q => q.Corners.Min(c => c.X)).ToList();
        Assert.Equal(3, fronts.Count);
        Assert.Equal(0f, URange(fronts[0]).Min, 4);
        Assert.Equal(1f, URange(fronts[2]).Max, 4);
        for (int i = 0; i < 2; i++)
            Assert.Equal(URange(fronts[i]).Max, URange(fronts[i + 1]).Min, 4);
        Assert.All(fronts, q => Assert.Equal((0f, 1f), VRange(q)));
        // The same for the back faces (mirrored) of the run.
        List<GeoQuad> backs = window.Where(q => IsBack(q) && q.Arm is 0 or Joints.East or Joints.West).OrderByDescending(q => q.Corners.Min(c => c.X)).ToList();
        Assert.Equal(0f, URange(backs[0]).Min, 4);
        Assert.Equal(1f, URange(backs[2]).Max, 4);
        // And the whole run is 2 m by 2 m of picture: the old slab's size.
        (float perU, float perV) = MetresPerSprite(fronts, c => c.X);
        Assert.Equal((2f, 2f), (MathF.Round(perU, 3), MathF.Round(perV, 3)));
    }

    [Fact]
    public void TheNorthAndSouthArmsAreTheEastAndWestArmsTurnedAQuarterSoTheLinesRunAlongTheSlab()
    {
        List<GeoQuad> window = AllOf(PieceParts.FullWindow);
        foreach ((int arm, Func<Vector3, bool> side) in new (int, Func<Vector3, bool>)[] { (Joints.North, c => c.Z < 0.0001f), (Joints.South, c => c.Z > -0.0001f) })
        {
            List<GeoQuad> quads = window.Where(q => q.Arm == arm).ToList();
            Assert.NotEmpty(quads);
            // The north arm lies on the north half of the tile (z -1..0), the south arm on the south half; each is 12 cm thick in x.
            Assert.All(quads, q => Assert.All(q.Corners, c => Assert.True(side(c))));
            Assert.All(quads, q => Assert.InRange(q.Corners.Max(c => MathF.Abs(c.X)), 0f, GeoSlab + 0.0001f));
            // Its big faces face east and west, and the picture's u runs along z (north), mapped over the arm's own share of the run.
            List<GeoQuad> big = quads.Where(q => IsEastFacing(q) || q.Normal == -Vector3.UnitX).ToList();
            Assert.Equal(2, big.Count);
            foreach (GeoQuad q in big)
            {
                float spanZ = q.Corners.Max(c => c.Z) - q.Corners.Min(c => c.Z);
                Assert.InRange(spanZ, 0.93f, 0.95f);
                float spanU = URange(q).Max - URange(q).Min;
                Assert.Equal(spanZ / 2f, spanU, 3); // 2 m of run is the whole picture
            }
        }
    }

    private const float GeoSlab = WorldGeometry.SlabHalf;

    [Fact]
    public void EveryArmReachesTheTileEdgeAndTheHalvesMeetAtThePost()
    {
        foreach (int part in new[] { PieceParts.FullWindow, PieceParts.Grille })
        {
            List<GeoQuad> all = AllOf(part);
            Assert.Equal(1f, all.Where(q => q.Arm == Joints.East).Max(q => q.Corners.Max(c => c.X)), 4);
            Assert.Equal(-1f, all.Where(q => q.Arm == Joints.West).Min(q => q.Corners.Min(c => c.X)), 4);
            Assert.Equal(-1f, all.Where(q => q.Arm == Joints.North).Min(q => q.Corners.Min(c => c.Z)), 4);
            Assert.Equal(1f, all.Where(q => q.Arm == Joints.South).Max(q => q.Corners.Max(c => c.Z)), 4);
        }
        // The window's arms meet the post's faces (the 12 cm square at the centre) exactly.
        List<GeoQuad> window = AllOf(PieceParts.FullWindow);
        Assert.Equal(GeoSlab, window.Where(q => q.Arm == Joints.East).Min(q => q.Corners.Min(c => c.X)), 4);
        Assert.Equal(-GeoSlab, window.Where(q => q.Arm == Joints.West).Max(q => q.Corners.Max(c => c.X)), 4);
        Assert.Equal(-GeoSlab, window.Where(q => q.Arm == Joints.North).Max(q => q.Corners.Max(c => c.Z)), 4);
        Assert.Equal(GeoSlab, window.Where(q => q.Arm == Joints.South).Min(q => q.Corners.Min(c => c.Z)), 4);
    }

    [Fact]
    public void ABorderPiecesCornerPostsSitAtItsTwoEndsAndAreDrawnOnlyByTheirBit()
    {
        foreach (int part in new[] { PieceParts.BorderPost, PieceParts.RailPost })
        {
            List<GeoQuad> posts = AllOf(part);
            Assert.Equal(new[] { (int)BorderCorners.LowEnd, BorderCorners.HighEnd }, posts.Select(q => q.Arm).Distinct().OrderBy(a => a).ToArray());
            Assert.All(posts.Where(q => q.Arm == BorderCorners.LowEnd), q => Assert.All(q.Corners, c => Assert.InRange(c.X, -1.0001f, -1f + WorldGeometry.PostWidth + 0.0001f)));
            Assert.All(posts.Where(q => q.Arm == BorderCorners.HighEnd), q => Assert.All(q.Corners, c => Assert.InRange(c.X, 1f - WorldGeometry.PostWidth - 0.0001f, 1.0001f)));
            // A post is a little thicker than the piece and never leaves the tile.
            Assert.All(posts, q => Assert.All(q.Corners, c => Assert.InRange(c.Z, -1.0001f, 0f)));
            Assert.Equal(8, posts.Count); // two posts of four faces: the face toward the piece is left out
        }
        // A window's or border firelock's post is as high as the wall, a railing's as high as its rail.
        Assert.Equal(Wall, AllOf(PieceParts.BorderPost).Max(q => q.Corners.Max(c => c.Y)));
        Assert.Equal(1.02f, AllOf(PieceParts.RailPost).Max(q => q.Corners.Max(c => c.Y)));
        // The posts are parts of their own: the pieces themselves are what they were (no post mark on any of their faces), so a piece without a post pays nothing for them;
        // a directional window's two end caps carry D152 (block windows)'s cap marks.
        Assert.All(new[] { PieceParts.Railing, PieceParts.FirelockEdge }, part => Assert.All(AllOf(part), q => Assert.Equal(0, q.Arm)));
        Assert.Equal(3, Part(PieceParts.DirectionalWindow).Count);
        Assert.Equal(new[] { WorldGeometry.PaneCapLow, WorldGeometry.PaneCapHigh }, AllOf(PieceParts.DirectionalWindow).Where(q => q.Arm != 0).Select(q => q.Arm).OrderBy(a => a).ToArray());
        Assert.Equal(PieceParts.BorderPost, PieceParts.PostPartOf(RenderKind.DirectionalWindow));
        Assert.Equal(PieceParts.BorderPost, PieceParts.PostPartOf(RenderKind.FirelockBorder));
        Assert.Equal(PieceParts.RailPost, PieceParts.PostPartOf(RenderKind.Railing));
        Assert.Equal(-1, PieceParts.PostPartOf(RenderKind.Windoor));
    }

    [Fact]
    public void TheGrilleIsCheapAndTheWholePieceMeshStaysNearItsOldSize()
    {
        // Every instance of the piece MultiMesh pays for every vertex of the mesh, so the joined pieces' geometry is kept lean: bars of a flat swatch are crossed quads, not boxes.
        List<GeoQuad> pieces = WorldGeometry.Pieces(Wall);
        Assert.InRange(AllOf(PieceParts.Grille).Count, 40, 60);
        Assert.InRange(pieces.Count, 100, 200); // it was 149 quads before the joints (M3 R3)
        Assert.All(AllOf(PieceParts.Grille).Where(q => q.Arm is 0 or Joints.East or Joints.West), q => Assert.True(q.Corners.All(c => MathF.Abs(c.Z) <= 0.0401f))); // thin, as before
    }

    [Fact]
    public void NoArmOrPostMarkHidesAFaceOfAnotherPart()
    {
        // Only the window, the grille and the two post parts have marked faces; every other part is always drawn.
        foreach (int part in Enumerable.Range(0, PieceParts.Count).Except(new[] { PieceParts.FullWindow, PieceParts.Grille, PieceParts.BorderPost, PieceParts.RailPost, PieceParts.DirectionalWindow, PieceParts.Windoor }))
            Assert.All(AllOf(part), q => Assert.Equal(0, q.Arm));
        // Every mark is a bit of the shader's mask (0..15).
        Assert.All(WorldGeometry.Pieces(Wall), q => Assert.InRange(q.Arm, 0, 15));
    }
}
