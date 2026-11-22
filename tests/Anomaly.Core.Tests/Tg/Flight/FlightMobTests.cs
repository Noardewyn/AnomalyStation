using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Flight;

/// <summary>
/// DV416 stage 2 (D328): a thrown player flies the same arc as a thing: the capsule stands where the arc is each tick, takes no wish in the
/// air (its client is told it cannot move), and stands at the landing point when it lands, walking on from there.
/// </summary>
public class FlightMobTests
{
    [Fact]
    public void AThrownPlayersCapsuleFollowsTheArcTakesNoWishAndStandsWhereItLands()
    {
        var r = new T2Room(4410);
        mob_living_carbon_human bob = r.Join(3, 8, "Bob", name: "bob");
        r.Step(2);
        r.Command(() => Waiting.Detach(bob.throw_at(locate(8, 8, 1), 7, 2)));
        bool flew = false, steered = false;
        double highest = 0;
        for (int i = 0; i < 40; i++)
        {
            r.Step();
            r.In(() =>
            {
                FlightArc arc = bob.dm_flight;
                if (!arc.IsMoving)
                    return;
                flew = true;
                (double x, double y, double lift) = arc.At(r.World.Tick);
                highest = Math.Max(highest, lift);
                Assert.True(LocomotionSeam.TryGetState(r.World, bob.dm_id, out MotionState s, out MotionParams p));
                Assert.Equal(x, s.X / 256.0, 1); // the capsule is where the arc is (kept inside its loc's tile)
                Assert.Equal(y, s.Y / 256.0, 2);
                steered |= p.CanMove;
            });
        }
        Assert.True(flew, "the player flew an arc");
        Assert.False(steered, "no wish moves a body in the air");
        Assert.True(highest >= FlightSeam.FloorThrowPeakPixels - 0.5, "a body thrown from the floor hops: " + highest);
        r.In(() =>
        {
            Assert.Null(bob.throwing);
            Assert.True(bob.dm_flight.IsNone); // a body has no rest point: its capsule stands there
            Assert.Equal(8f, Dm.num(bob.x));
            Assert.True(LocomotionSeam.TryGetState(r.World, bob.dm_id, out MotionState s, out MotionParams p));
            Assert.Equal(8, s.X / 256.0, 1);
            Assert.True(p.CanMove);
        });
        r.AssertNoRuntimes();
    }
}
