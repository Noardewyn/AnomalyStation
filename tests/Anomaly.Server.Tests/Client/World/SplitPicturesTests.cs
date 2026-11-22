using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// A wall picture tg splits over neighbouring tiles (the station maps' halves): on a face that looks north the run is mirrored, so its parts meet in tg's order.
/// </summary>
public class SplitPicturesTests
{
    // the pictures by column: the left half reaches its east edge, the right half its west edge, a lone sign neither
    private static (bool West, bool East)? Edges(int x) => x switch
    {
        10 => (false, true),
        11 => (true, false),
        20 => (false, true),
        21 => (true, true),
        22 => (true, false),
        30 => (false, false),
        _ => null,
    };

    [Fact]
    public void TheHalvesOfAMapSwapPlaces()
    {
        Assert.Equal(1, SplitPictures.MirrorShift(10, Edges));
        Assert.Equal(-1, SplitPictures.MirrorShift(11, Edges));
    }

    [Fact]
    public void ARunOfThreeIsMirroredAboutItsMiddle()
    {
        Assert.Equal(2, SplitPictures.MirrorShift(20, Edges));
        Assert.Equal(0, SplitPictures.MirrorShift(21, Edges));
        Assert.Equal(-2, SplitPictures.MirrorShift(22, Edges));
    }

    [Fact]
    public void ALoneSignStays()
    {
        Assert.Equal(0, SplitPictures.MirrorShift(30, Edges));
        // a part whose neighbour does not reach back stays too
        Assert.Equal(0, SplitPictures.MirrorShift(11, x => x == 11 ? (true, false) : x == 10 ? (false, false) : null));
    }
}
