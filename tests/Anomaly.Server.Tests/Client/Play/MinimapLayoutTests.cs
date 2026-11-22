using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>D315: the minimap's layout of a level: the picture's rows, the area under a tile, the department labels and the zoom steps.</summary>
public class MinimapLayoutTests
{
    // 4 x 3 from (10, 20): an L of medbay (area 1, colour 1) in the bottom row and the left column, a hallway with no department (area 2,
    // colour 2) in the rest but the top right cell, which is undrawn
    private static MinimapMessage Map()
    {
        var m = new MinimapMessage { Z = 1, MinX = 10, MinY = 20, Width = 4, Height = 3 };
        m.Colours.Add(0x5B97BCFF);
        m.Colours.Add(0xEBE5E5EE);
        m.AreaNames.Add("Medbay");
        m.AreaCodes.Add("MED");
        m.AreaNames.Add("Hallway");
        m.AreaCodes.Add("");
        int[] areas = { 1, 1, 1, 1, 1, 2, 2, 2, 1, 2, 2, 0 };
        foreach (int a in areas)
        {
            m.PixelRuns.Add(new TurfRun(1, a));
            m.AreaRuns.Add(new TurfRun(1, a));
        }
        return m;
    }

    [Fact]
    public void TheAreaUnderATileAndThePictureRowsNorthFirst()
    {
        var layout = new MinimapLayout(Map());
        Assert.Equal("Medbay", layout.AreaNameAt(10, 20));
        Assert.Equal("Hallway", layout.AreaNameAt(11, 21));
        Assert.Null(layout.AreaNameAt(13, 22)); // undrawn
        Assert.Null(layout.AreaNameAt(14, 20)); // off the picture

        byte[] rgba = layout.Rgba();
        Assert.Equal(new byte[] { 0x5B, 0x97, 0xBC, 0xFF }, rgba[..4]); // the top left is the top row's (y 22) first cell, medbay
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, rgba[12..16]); // the top right, undrawn
        Assert.Equal(new byte[] { 0x5B, 0x97, 0xBC, 0xFF }, rgba[^4..]); // the bottom right, medbay's row y 20
    }

    [Fact]
    public void ADepartmentsLabelSitsOnItsAreaAndShowsWhenItFits()
    {
        var layout = new MinimapLayout(Map());
        MinimapLayout.Label med = Assert.Single(layout.Labels); // the hallway has no department
        Assert.Equal("MED", med.Code);
        Assert.Equal(6, med.Cells);
        Assert.Equal((4, 3), (med.Width, med.Height));
        Assert.Equal("Medbay", layout.AreaNameAt(med.X, med.Y)); // on a medbay cell, not the L's empty middle

        Assert.Single(layout.Visible(10f, _ => 30f, 10f)); // 3 x 1 tiles: fits the 4 x 3 bounds
        Assert.Empty(layout.Visible(5f, _ => 30f, 10f)); // 6 x 2 tiles: wider than the area
    }

    [Theory]
    [InlineData(200, 1, 300)]
    [InlineData(200, -1, 150)]
    [InlineData(800, 1, 800)]
    [InlineData(100, -1, 100)]
    public void TheWheelStepsThroughTheZoomsAndStopsAtTheEnds(int from, int direction, int to)
        => Assert.Equal(to, MinimapLayout.Zoom(from, direction));

    [Fact]
    public void TheHoverNamesTheNearestMobWithinATileAsTgsBlipHover()
    {
        var mobs = new MinimapMobsMessage { Z = 1 };
        mobs.Mobs.Add(new MinimapMob(10, 10, true, "Alice"));
        mobs.Mobs.Add(new MinimapMob(11, 11, false, "the mouse"));
        mobs.Mobs.Add(new MinimapMob(12, 10, false, ""));
        Assert.Equal("Alice", MinimapLayout.MobAt(mobs, 10, 10));
        Assert.Equal("the mouse", MinimapLayout.MobAt(mobs, 11, 12)); // Alice is two tiles off on y
        Assert.Equal("Alice", MinimapLayout.MobAt(mobs, 11, 10)); // a tie of 1 and 1: the first
        Assert.Null(MinimapLayout.MobAt(mobs, 13, 10)); // only the nameless one within a tile
        Assert.Null(MinimapLayout.MobAt(null, 10, 10));
    }

    [Fact]
    public void MaintenanceHasNoLabel()
    {
        MinimapMessage map = Map();
        map.AreaCodes[0] = "MNT";
        Assert.Empty(new MinimapLayout(map).Labels);
        Assert.Equal("Medbay", new MinimapLayout(map).AreaNameAt(10, 20)); // its name still shows under the cursor
    }

    [Fact]
    public void ADragMovesTheViewWithThePointerUntilTheMobMoves()
    {
        MinimapMessage map = Map(); // 4 x 3 from (10, 20): tiles 10 to 13, 20 to 22
        var view = new MinimapView();
        Assert.Equal((11f, 21f), view.Centre(11f, 21f, map)); // on the mob

        view.Drag(10f, -10f, 10f, 11f, 21f); // the pointer right and up a tile at 10 pixels a tile: the view a tile west and south
        Assert.True(view.Panned);
        (float x, float y) = view.Centre(11.1f, 21f, map); // a tenth of a tile is no move: the view keeps its offset from the mob
        Assert.Equal((10.1f, 20f), (MathF.Round(x, 3), MathF.Round(y, 3)));

        view.Drag(100f, -100f, 10f, 11.1f, 21f); // far past the picture: kept over its edge, and coming back needs no extra drag
        Assert.Equal((9.5f, 19.5f), view.Centre(11.1f, 21f, map));
        view.Drag(-10f, 10f, 10f, 11.1f, 21f);
        Assert.Equal((10.5f, 20.5f), view.Centre(11.1f, 21f, map));

        Assert.Equal((11.3f, 21f), view.Centre(11.3f, 21f, map)); // the mob walked a quarter tile and more: back on it
        Assert.False(view.Panned);
    }
}
