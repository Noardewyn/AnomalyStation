using System.IO;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Render;

public class IconSheetTests
{
    // Written the way tools/Anomaly.Tools.Icons/DmiJson.cs writes it: a 3 x 2 sheet of 2 x 2 frames.
    private const string Json = @"{""version"":""4.0"",""width"":2,""height"":2,""sheetWidth"":6,""sheetHeight"":4,""states"":[
        {""name"":"""",""dirs"":1,""frames"":1,""delays"":null,""loop"":0,""rewind"":false,""movement"":false,""hotspot"":null,""first"":0,""rects"":[[0,0,2,2]]},
        {""name"":""walk"",""dirs"":4,""frames"":1,""delays"":null,""loop"":0,""rewind"":false,""movement"":false,""hotspot"":null,""first"":1,""rects"":[[2,0,2,2],[4,0,2,2],[0,2,2,2],[2,2,2,2]]},
        {""name"":""walk"",""dirs"":1,""frames"":1,""delays"":null,""loop"":0,""rewind"":false,""movement"":true,""hotspot"":null,""first"":5,""rects"":[[4,2,2,2]]}]}";

    private static Raster Sheet()
    {
        // Each 2 x 2 frame is one colour: its index in the sheet.
        var sheet = new Raster(6, 4);
        for (int index = 0; index < 6; index++)
        {
            int fx = index % 3 * 2, fy = index / 3 * 2;
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                    sheet.Set(fx + x, fy + y, (uint)(index + 1) << 24 | 0xFF);
            }
        }
        return sheet;
    }

    [Fact]
    public void FramesAreCutByTheirRectanglesAndAskedForByDirection()
    {
        IconSheet icon = IconSheet.Parse(Json, Sheet());
        Assert.True(icon.TryGetState("walk", out IIconState walk));
        Assert.Equal(4, walk.Dirs);
        Assert.Equal(2, walk.Width);
        // south, north, east, west are sheet frames 1, 2, 3, 4 (colours 2..5).
        Assert.Equal(2u << 24 | 0xFF, walk.Get(ByondDir.Index(ByondDir.South, 4), 0).Get(0, 0));
        Assert.Equal(3u << 24 | 0xFF, walk.Get(ByondDir.Index(ByondDir.North, 4), 0).Get(1, 1));
        Assert.Equal(4u << 24 | 0xFF, walk.Get(ByondDir.Index(ByondDir.East, 4), 0).Get(0, 1));
        Assert.Equal(5u << 24 | 0xFF, walk.Get(ByondDir.Index(ByondDir.West, 4), 0).Get(1, 0));
        Assert.True(icon.TryGetState(null, out IIconState blank));
        Assert.Equal(1u << 24 | 0xFF, blank.Get(0, 0).Get(0, 0));
    }

    [Fact]
    public void ALookupTakesTheFirstStateOfAName()
    {
        IconSheet icon = IconSheet.Parse(Json, Sheet());
        icon.TryGetState("walk", out IIconState state);
        Assert.Equal(4, state.Dirs); // not the movement state listed after it
        Assert.False(icon.TryGetState("run", out _));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData(@"{""width"":2,""height"":2,""states"":[{""name"":""a"",""dirs"":4,""frames"":1,""rects"":[[0,0,2,2]]}]}")]
    public void AMalformedDescriptionIsRejected(string json)
        => Assert.Throws<InvalidDataException>(() => IconSheet.Parse(json, Sheet()));

    [Theory]
    [InlineData(ByondDir.South, 4, 0)]
    [InlineData(ByondDir.North, 4, 1)]
    [InlineData(ByondDir.East, 4, 2)]
    [InlineData(ByondDir.West, 4, 3)]
    [InlineData(ByondDir.SouthEast, 4, 2)] // a diagonal on a four-direction icon shows the horizontal side
    [InlineData(ByondDir.NorthWest, 4, 3)]
    [InlineData(ByondDir.SouthEast, 8, 4)]
    [InlineData(ByondDir.SouthWest, 8, 5)]
    [InlineData(ByondDir.NorthEast, 8, 6)]
    [InlineData(ByondDir.NorthWest, 8, 7)]
    [InlineData(ByondDir.East, 1, 0)]
    [InlineData((byte)0, 4, 0)] // no direction: south
    public void DirectionsMapToTheFileOrder(byte dir, int dirs, int expected)
        => Assert.Equal(expected, ByondDir.Index(dir, dirs));

    [Fact]
    public void FromIndexInvertsIndexForEightDirections()
    {
        for (int i = 0; i < 8; i++)
            Assert.Equal(i, ByondDir.Index(ByondDir.FromIndex(i), 8));
    }

    [Fact]
    public void AStillStateShowsFrameZeroForever()
        => Assert.Equal(0, new IconState { Frames = 1 }.FrameAt(123.4));

    [Theory]
    [InlineData(0.00, 0)]
    [InlineData(0.09, 0)]
    [InlineData(0.10, 1)] // one decisecond in: the second frame (delay 1), which lasts 2
    [InlineData(0.29, 1)]
    [InlineData(0.30, 0)] // the loop of 3 ds wraps
    [InlineData(0.55, 1)]
    public void AnAnimationFollowsItsDelaysInDeciseconds(double seconds, int expected)
        => Assert.Equal(expected, new IconState { Frames = 2, Delays = new double[] { 1, 2 } }.FrameAt(seconds));

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.1, 1)]
    [InlineData(0.2, 2)]
    [InlineData(0.3, 1)] // rewind: back through the middle frame
    [InlineData(0.4, 0)]
    [InlineData(0.5, 1)]
    public void ARewindingAnimationPlaysBackAndForth(double seconds, int expected)
        => Assert.Equal(expected, new IconState { Frames = 3, Delays = new double[] { 1, 1, 1 }, Rewind = true }.FrameAt(seconds));

    [Fact]
    public void ALoopCountStopsTheAnimationOnItsLastFrame()
    {
        var state = new IconState { Frames = 3, Delays = new double[] { 1, 1, 1 }, Loop = 2 };
        Assert.Equal(1, state.FrameAt(0.4));
        Assert.Equal(2, state.FrameAt(0.6)); // after two loops (6 ds)
        Assert.Equal(2, state.FrameAt(99));
    }

    [RealIconsFact]
    public void ARealSheetLoadsThroughTheLibrary()
    {
        IconLibrary library = RealIcons.NewLibrary();
        Assert.True(library.TryGetIcon("icons/turf/floors.dmi", out IIcon floors));
        Assert.True(floors.TryGetState("floor", out IIconState floor));
        Assert.Equal(32, floor.Width);
        Assert.False(floor.Get(0, 0).IsEmpty());
        Assert.True(library.TryGetIcon("icons/mob/human/bodyparts_greyscale.dmi", out IIcon bodyparts));
        Assert.True(bodyparts.TryGetState("human_chest_m", out IIconState chest));
        Assert.Equal(4, chest.Dirs);
        Assert.False(library.TryGetIcon("icons/does/not/exist.dmi", out _));
        Assert.Single(library.Problems);
    }
}
