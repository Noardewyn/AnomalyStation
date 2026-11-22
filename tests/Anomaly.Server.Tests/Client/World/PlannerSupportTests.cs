using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>M3 P6: the atlas, the door state machine, the occluder merge, the presets and the bench track (engine-free).</summary>
public sealed class PlannerSupportTests
{
    private static Raster Picture(int width, int height, int x0, int y0, int x1, int y1, uint rgba = 0xFF0000FF)
    {
        var raster = new Raster(width, height);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
                raster.Set(x, y, rgba);
        }
        return raster;
    }

    [Fact]
    public void TheAtlasTrimsAPictureAndPlacesItsQuadOnTheFloor()
    {
        var atlas = new AtlasBuilder();
        // A 32 x 32 sprite with a 10 x 20 object at x 11..20, rows 8..27 from the top: the object stands on the icon square's row 4 from the bottom.
        int id = atlas.Add(Picture(32, 32, 11, 8, 20, 27), 0, 0);
        AtlasEntry e = atlas.Entries[id];
        Assert.Equal((10, 20), (e.Width, e.Height));
        Assert.Equal(10 / 16f, e.QuadWidth, 4);
        Assert.Equal(20 / 16f, e.QuadHeight, 4);
        // The picture's centre column is 15.5 px; the object's centre is 15.5 px too: no horizontal shift. Its bottom is 4 px above the floor.
        Assert.Equal(0f, e.CenterX, 3);
        Assert.Equal(4 / 16f, e.BottomY, 4);
        // The stored pixels are the object's, with the border one pixel out.
        Assert.Equal(0xFF0000FFu, atlas.Get(e.X, e.Y));
        Assert.Equal(0xFF0000FFu, atlas.Get(e.X + 9, e.Y + 19));
    }

    [Fact]
    public void AnOverlayThatReachesOutOfTheSquareKeepsItsOffset()
    {
        var atlas = new AtlasBuilder();
        // A 32 x 48 picture whose icon square starts at y 0 from the bottom: a tall thing, drawn rows 0..47.
        int id = atlas.Add(Picture(32, 48, 0, 0, 31, 47), 0, 0);
        AtlasEntry e = atlas.Entries[id];
        Assert.Equal(48 / 16f, e.QuadHeight, 4);
        Assert.Equal(0f, e.BottomY, 4);
    }

    [Fact]
    public void AnEmptyPictureDrawsNothing()
    {
        var atlas = new AtlasBuilder();
        int id = atlas.Add(new Raster(32, 32), 0, 0);
        Assert.Equal(0, atlas.Entries[id].Width);
        Assert.Equal(0f, atlas.Entries[id].QuadWidth);
    }

    [Fact]
    public void PicturesNeverOverlapAndTheAtlasGrowsWithoutMovingThem()
    {
        var atlas = new AtlasBuilder();
        var rects = new List<AtlasEntry>();
        var rng = new Random(5);
        for (int i = 0; i < 4000; i++)
        {
            int w = rng.Next(4, 60), h = rng.Next(4, 60);
            int id = atlas.Add(Picture(64, 64, 0, 0, w - 1, h - 1, (uint)(0x01000000 * ((i % 250) + 1)) | 0xFF), 0, 0);
            rects.Add(atlas.Entries[id]);
        }
        Assert.True(atlas.Pages.Count > 1, "it grew a page");
        for (int i = 0; i < rects.Count; i++)
        {
            AtlasEntry a = rects[i];
            Assert.Equal(a, atlas.Entries[i]); // growing never moved an entry
            Assert.True(a.X >= 1 && a.Y >= 1 && a.X + a.Width + 1 <= AtlasBuilder.AtlasWidth && a.Y + a.Height + 1 <= atlas.Height);
            // a picture and its border lie in one page (the renderer's texture array layer)
            Assert.Equal((a.Y - 1) / AtlasBuilder.PageHeight, (a.Y + a.Height) / AtlasBuilder.PageHeight);
            for (int j = i + 1; j < rects.Count; j++)
            {
                AtlasEntry b = rects[j];
                bool apart = a.X + a.Width + 1 <= b.X - 1 || b.X + b.Width + 1 <= a.X - 1 || a.Y + a.Height + 1 <= b.Y - 1 || b.Y + b.Height + 1 <= a.Y - 1;
                Assert.True(apart, $"entries {i} and {j} overlap");
            }
        }
    }

    [Fact]
    public void TheMetricsTableHasTwoTexelsPerEntry()
    {
        var atlas = new AtlasBuilder();
        atlas.Add(Picture(32, 32, 0, 0, 31, 31), 0, 0, trim: false);
        atlas.AddSwatch(0x00FF00FF);
        float[] metrics = atlas.Metrics();
        Assert.Equal(16, metrics.Length);
        Assert.Equal(32f, metrics[2]);
        Assert.Equal(2f, metrics[4]); // the 32 px picture is 2 m wide
    }

    [Theory]
    [InlineData("opening", DoorPhase.Opening)]
    [InlineData("closing", DoorPhase.Closing)]
    [InlineData("deny", DoorPhase.Deny)]
    [InlineData("closed", DoorPhase.Steady)]
    [InlineData("open", DoorPhase.Steady)]
    [InlineData(null, DoorPhase.Steady)]
    public void TheIconStateNamesThePhase(string state, DoorPhase phase) => Assert.Equal(phase, DoorAnimation.PhaseOf(state));

    [Fact]
    public void AnOpeningDoorSlidesAndFinishesWhenTheCellTurnsPassable()
    {
        DoorAnimState state = DoorAnimation.Initial(solid: true);
        Assert.Equal(0f, state.Open);
        for (int i = 0; i < 10; i++)
            DoorAnimation.Step(ref state, "opening", solid: true, 0.025f);
        Assert.InRange(state.Open, 0.45f, 0.55f); // 0.25 s of 0.5
        // The cell turns passable: the slide ends at once, even though the icon state still says opening.
        for (int i = 0; i < 4; i++)
            DoorAnimation.Step(ref state, "opening", solid: false, 0.025f);
        Assert.Equal(1f, state.Open);
        DoorAnimation.Step(ref state, "open", solid: false, 0.05f);
        Assert.Equal(1f, state.Open);
        Assert.False(state.Moving);
    }

    [Fact]
    public void AClosingDoorShutsWhenTheCellTurnsSolid()
    {
        DoorAnimState state = DoorAnimation.Initial(solid: false);
        for (int i = 0; i < 4; i++)
            DoorAnimation.Step(ref state, "closing", solid: false, 0.025f);
        Assert.InRange(state.Open, 0.6f, 0.7f);
        for (int i = 0; i < 4; i++)
            DoorAnimation.Step(ref state, "closing", solid: true, 0.025f);
        Assert.Equal(0f, state.Open);
    }

    [Fact]
    public void ADenyKeepsTheDoorShutAndFlashes()
    {
        DoorAnimState state = DoorAnimation.Initial(solid: true);
        DoorAnimation.Step(ref state, "deny", solid: true, 0.1f);
        Assert.True(state.Denied);
        Assert.Equal(0f, state.Open);
        DoorAnimation.Step(ref state, "deny", solid: true, 0.3f);
        Assert.False(state.Denied);
        DoorAnimation.Step(ref state, "closed", solid: true, 0.05f);
        Assert.False(state.Moving);
    }

    [Fact]
    public void ADoorNeverMovesWithoutTheServerSayingSo()
    {
        // No prediction (D38): a door whose state is "closed" with a solid cell stays shut however much time passes, and an open one stays open.
        DoorAnimState shut = DoorAnimation.Initial(solid: true);
        DoorAnimState open = DoorAnimation.Initial(solid: false);
        for (int i = 0; i < 100; i++)
        {
            DoorAnimation.Step(ref shut, "closed", true, 0.05f);
            DoorAnimation.Step(ref open, "open", false, 0.05f);
        }
        Assert.Equal(0f, shut.Open);
        Assert.Equal(1f, open.Open);
    }

    [Fact]
    public void WallRunsMergeIntoBoxesThatCoverEachWallOnce()
    {
        // ### / #.# / ### : the ring is a top row, a bottom row and the two sides.
        var cells = new List<(int, int)>();
        for (int x = 1; x <= 3; x++)
        {
            cells.Add((x, 1));
            cells.Add((x, 3));
        }
        cells.Add((1, 2));
        cells.Add((3, 2));
        List<WallBox> boxes = OccluderPlanner.Merge(cells, 1);
        Assert.Equal(cells.Count, boxes.Sum(b => b.CellCount));
        Assert.Equal(4, boxes.Count);
        // A solid block is one box.
        var block = new List<(int, int)>();
        for (int y = 5; y <= 8; y++)
        {
            for (int x = 2; x <= 7; x++)
                block.Add((x, y));
        }
        WallBox single = Assert.Single(OccluderPlanner.Merge(block, 1));
        Assert.Equal(new WallBox(2, 5, 7, 8, 1), single);
    }

    [Fact]
    public void AWallRowWithAGapIsTwoBoxes()
    {
        List<WallBox> boxes = OccluderPlanner.Merge(new[] { (1, 1), (2, 1), (4, 1) }, 1);
        Assert.Equal(2, boxes.Count);
    }

    [Fact]
    public void ThePresetsFollowTheScope()
    {
        Assert.Equal(new[] { 4, 8, 16, 32 }, RenderPreset.All.Select(p => p.ShadowedLights));
        Assert.True(RenderPreset.Low.Fsr);
        Assert.Equal(0.75f, RenderPreset.Low.RenderScale);
        Assert.Equal(new[] { 60f, 100f, 200f, 300f }, RenderPreset.All.Select(p => p.ViewDistance));
        Assert.True(RenderPreset.TryParse("MEDIUM", out RenderPreset medium));
        Assert.Same(RenderPreset.Medium, medium);
        Assert.False(RenderPreset.TryParse("ultra", out _));
    }

    [Fact]
    public void TheBenchTrackFliesAtAConstantPaceAndFacesAhead()
    {
        // 20 m east along z = 0 in 10 s.
        var points = new List<(float, float)>();
        for (int i = 0; i <= 20; i++)
            points.Add((i, 0f));
        var track = new BenchTrack(points, 10);
        Assert.InRange(track.Speed, 1.5f, 2f); // the smoothing shortens the ends a little
        BenchPose mid = track.At(5);
        Assert.Equal(10f, mid.X, 1);
        // Facing east: Godot's yaw -90 degrees.
        Assert.Equal(-MathF.PI / 2, mid.Yaw, 2);
        Assert.Equal(track.At(10).X, track.At(99).X); // clamped at the end
    }

    [Fact]
    public void TheRouteGoesAroundWalls()
    {
        // A wall at x 3 from y 1 to 4 on a 6 x 6 grid: the route from (1,1) to (5,1) must pass y 5.
        bool Passable(int x, int y) => !(x == 3 && y <= 4);
        List<(int X, int Y)> route = BenchPath.Route(6, 6, Passable, new[] { (1, 1), (5, 1) });
        Assert.Equal((1, 1), route[0]);
        Assert.Equal((5, 1), route[^1]);
        Assert.Contains(route, c => c.Y == 5);
        Assert.All(route, c => Assert.True(Passable(c.X, c.Y)));
    }

    [Fact]
    public void TheStatsAreTheFramesPercentiles()
    {
        // 99 frames of 10 ms and one of 50 ms.
        var times = Enumerable.Repeat(0.01, 99).Append(0.05).ToList();
        BenchStats stats = BenchStats.FromFrameTimes(times);
        Assert.Equal(100, stats.Frames);
        Assert.Equal(100 / 1.04, stats.AvgFps, 1);
        Assert.Equal(100.0, stats.P50Fps, 1);
        Assert.Equal(20.0, stats.MinFps, 1);
        Assert.True(stats.P99Fps <= 100.0);
        Assert.Contains("preset=medium", stats.Line("medium", 120, 5000000, 1.5, 300));
    }

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public PlannerSupportTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    [Fact]
    public void MetasBenchWaypointsAreOpenAndConnected()
    {
        string path = FakeMapTests.MetaPath();
        if (path == null)
            return;
        Anomaly.Client.Shared.FakeMap map = Anomaly.Client.Shared.FakeMap.Load(path);
        var doors = new HashSet<(int, int)>(map.Entities.Where(e => e.TypePath.StartsWith("/obj/machinery/door/")).Select(e => (e.X, e.Y)));
        bool Passable(int x, int y) => !map.MapGrid.IsSolid(x, y, 1) || doors.Contains((x, y));
        foreach ((int x, int y) in BenchPath.MetaWaypoints)
            Assert.True(Passable(x, y), $"waypoint {x},{y} is not an open cell");
        List<(int X, int Y)> route = BenchPath.Route(map.Width, map.Height, Passable, BenchPath.MetaWaypoints);
        Assert.True(route.Count > 600, "the route is " + route.Count + " cells");
        // Every leg found a route (a skipped leg would leave a jump).
        for (int i = 1; i < route.Count; i++)
            Assert.Equal(1, Math.Abs(route[i].X - route[i - 1].X) + Math.Abs(route[i].Y - route[i - 1].Y));
        foreach ((int x, int y) in BenchPath.MetaWaypoints)
            Assert.Contains((x, y), route);
    }
}
