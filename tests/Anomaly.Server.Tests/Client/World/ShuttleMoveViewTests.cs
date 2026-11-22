using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>What the client does with a moved shuttle's cargo and the reservations' borders.</summary>
public class ShuttleMoveViewTests
{
    [Fact]
    public void Tg_invisible_cordon_draws_nothing() => Assert.Equal(TurfKind.Void, TurfStyle.Classify("/turf/cordon"));

    [Fact]
    public void Transit_tiles_show_the_sky_and_name_the_way_hyperspace_streams()
    {
        Assert.Equal(TurfKind.Void, TurfStyle.Classify("/turf/open/space/transit/north"));
        Assert.Equal(TurfKind.Void, TurfStyle.Classify("/turf/open/space/basic"));
        string At(int x, int y) => x == 15 ? "/turf/open/space/transit/north" : "/turf/open/floor/plating";
        Assert.Equal(System.Numerics.Vector3.UnitZ, HyperspaceSky.Flow(10, 10, At));
        Assert.Null(HyperspaceSky.Flow(10, 10, (_, _) => "/turf/open/space/basic"));
        // a ship parked in plain space, with another ship's transit further along the reserved level
        Assert.Null(HyperspaceSky.Flow(10, 10, (x, _) => x <= 11 ? "/turf/open/floor/plating" : x == 15 ? "/turf/open/space/transit/north" : "/turf/open/space/basic"));

        // the own ship's box: its hull, not the other ship across the transit space (x 30 to 35)
        string Level(int x, int y) => (x is >= 8 and <= 12 && y is >= 9 and <= 14) || (x is >= 30 and <= 35 && y == 10) ? "/turf/open/floor/plating" : "/turf/open/space/transit/north";
        Assert.Equal((8, 9, 12, 14), HyperspaceSky.ShipBounds(10, 10, Level));
    }

    [Theory]
    [InlineData(true, 11, 10, 1, true)] // a window pushed a tile: a piece has no node form
    [InlineData(false, 11, 10, 1, false)] // a crate pushed a tile glides as a node
    [InlineData(false, 40, 10, 1, true)] // a jump on the level
    [InlineData(false, 10, 10, 2, true)] // a jump to another level
    public void A_piece_or_a_jumped_thing_is_planned_where_it_now_stands(bool piece, int x, int y, int z, bool replans)
        => Assert.Equal(replans, StaticPropRule.Replans(piece, (10, 10, 1), x, y, z));
}
