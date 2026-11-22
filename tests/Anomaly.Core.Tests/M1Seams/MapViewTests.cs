using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.M1Seams;

/// <summary>
/// The read-only map view for hosts that read a world from outside it (the replicator, after <c>World.Step</c>): the size
/// and the cells, with no ambient world, and no pending DM contents edit applied by the read.
/// </summary>
public class MapViewTests
{
    [Fact]
    public void TheViewHasTheMapSizeAndTheTurfOfEveryCellWithoutAnAmbientWorld()
    {
        var room = TgToyRoom.Create(7, 5);
        Assert.Null(World.CurrentOrNull);
        Assert.Equal(7, MapView.Width(room.World));
        Assert.Equal(5, MapView.Height(room.World));
        Assert.Equal(1, MapView.Levels(room.World));
        for (int y = 1; y <= 5; y++)
        {
            for (int x = 1; x <= 7; x++)
            {
                int cx = x, cy = y;
                Assert.Same(room.In(() => locate(cx, cy, 1)), MapView.TurfAt(room.World, x, y, 1));
            }
        }
        Assert.Null(MapView.TurfAt(room.World, 0, 1, 1));
        Assert.Null(MapView.TurfAt(room.World, 8, 1, 1));
        Assert.Null(MapView.TurfAt(room.World, 1, 6, 1));
        Assert.Null(MapView.TurfAt(room.World, 1, 1, 2));
    }

    [Fact]
    public void AnEmptyWorldHasAnEmptyMap()
    {
        var world = new World();
        Assert.Equal(0, MapView.Width(world));
        Assert.Null(MapView.TurfAt(world, 1, 1, 1));
    }

    [Fact]
    public void ReadingFromOutsideTheWorldAppliesNoPendingContentsEdit()
    {
        var room = TgToyRoom.Create();
        obj thing = room.In(() => Dm.@new<obj>(TgToyRoom.T(2, 2)));
        turf start = room.In(() => TgToyRoom.T(2, 2));
        turf target = room.In(() => TgToyRoom.T(4, 3));
        room.In(() => Dm.append(target.contents, thing)); // pending until the next engine access inside the world

        Assert.Same(target, MapView.TurfAt(room.World, 4, 3, 1));
        Assert.Same(start, thing.loc); // outside the world the getter reads raw, and the view applied nothing
        Assert.Equal(2f, thing.x);

        Assert.Same(target, room.In(() => thing.loc)); // inside, the engine applies the edit first
    }
}
