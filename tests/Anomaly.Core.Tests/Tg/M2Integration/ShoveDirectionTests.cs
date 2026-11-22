using Anomaly.Core.DmShim;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.Tg.M2Integration;

/// <summary>
/// DV17's shove direction between two capsule mobs (lead decision, M2 integration): the bridge's 8-sector rule at 22.5
/// degrees (LocomotionSeam.SectorDir, the rule of the wish direction and the facing), so a mostly-east offset shoves east;
/// tg's get_dir of the centres would shove diagonally at any off-axis offset.
/// </summary>
public class ShoveDirectionTests
{
    private static void RightClick(T2Room r, mob_living_carbon_human who, atom target)
    {
        r.Command(() => ClientSeam.Click(who.client, target, ClientSeam.ClickParams(RIGHT_CLICK)));
        r.Step();
    }

    [Theory]
    [InlineData(1, 0)]  // 85 subunits south of the tile centre: the target is 18 degrees north of east, a shove east
    [InlineData(2, 1)]  // 170 south (on the tile below): 34 degrees north of east, a shove north-east
    public void AnOffAxisOffsetShovesByTheEightSectorRule(int ticksSouth, int expectedNorth)
    {
        var r = new T2Room(1501);
        mob_living_carbon_human a = r.Join(8, 8, "Alice", name: "shover");
        mob_living_carbon_human b = r.Dummy(9, 8, name: "target");
        r.Go(a, 0, (sbyte)-MotionInput.MaxMove, T2Room.Yaw(SOUTH), ticksSouth);
        r.Go(a, 0, 0, T2Room.Yaw(EAST), 1); // stand still, facing the target
        MotionState sa = r.State(a), sb = r.State(b);
        long dx = sb.X - sa.X, dy = sb.Y - sa.Y;
        Assert.True(dx > 0 && dy > 0, $"the offset is not north of east: ({dx},{dy})");
        Assert.Equal(expectedNorth == 1, dy * 1000 > dx * 414); // the sector boundary at 22.5 degrees
        RightClick(r, a, b);
        Assert.Equal((10, 8 + expectedNorth, 1), r.LocOf(b));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSectorRuleIsTheWishRule()
    {
        Assert.Equal(MotionDir.East, LocomotionSeam.SectorDir(256, 85));
        Assert.Equal((byte)(MotionDir.North | MotionDir.East), LocomotionSeam.SectorDir(256, 170));
        Assert.Equal(MotionDir.South, LocomotionSeam.SectorDir(-10, -256));
        Assert.Equal(MotionDir.None, LocomotionSeam.SectorDir(0, 0));
        for (sbyte x = -127; x < 127; x += 7)
            for (sbyte y = -127; y < 127; y += 11)
                Assert.Equal(LocomotionSeam.WishDir(x, y), LocomotionSeam.SectorDir(x, y));
    }
}
