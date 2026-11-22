using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5x;

/// <summary>
/// M5x slice 2 (S1's open issue 3): a floor changed to space and the space replaced again in the same tick (ChangeTurf then
/// place_on_top, as a chained blast or a builder can) leaves LINDA's lists and the neighbours' adjacency naming the new turf only.
/// </summary>
public class TurfReplacedTwiceTests
{
    [Fact]
    public void AFloorTurnedToSpaceAndPlatedAgainInOneTickBreaksNoAtmosProcessing()
    {
        var r = new AudioRoom(14, 10, seed: 6501);
        r.In(() =>
        {
            for (int x = 3; x <= 12; x++)
                for (int y = 3; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
        });
        r.Step(20);
        r.In(() =>
        {
            // in the room's middle, and at its edge beside space (the venting neighbours of S1's report)
            foreach ((int x, int y) in new[] { (7, 5), (3, 5), (12, 8) })
            {
                turf space = (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_space)));
                Waiting.NoSleep(space.place_on_top(typeof(turf_open_floor_plating)));
            }
            // and a cell breached to space, left a tick, then replaced twice while the room vents into it
            Waiting.NoSleep(locate(9, 3, 1).ChangeTurf(typeof(turf_open_space)));
        });
        r.Step(2);
        r.In(() =>
        {
            turf again = (turf)Waiting.NoSleep(locate(9, 3, 1).place_on_top(typeof(turf_open_floor_plating)));
            Waiting.NoSleep(again.ChangeTurf(typeof(turf_open_space)));
        });
        r.Step(60);
        r.AssertNoRuntimes();
        Assert.True(r.In(() => locate(7, 5, 1) is turf_open_floor_plating));
        Assert.True(r.In(() => locate(9, 3, 1) is turf_open_space));
    }
}
