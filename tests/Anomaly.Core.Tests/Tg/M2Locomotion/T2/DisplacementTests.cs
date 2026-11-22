using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// A loc change tg makes (a shove's Move, forceMove, a teleport) moves the capsule (D105, scope 2a "moved by others"): a
/// neighbouring tile slides the capsule to the new tile's centre over the mob's glide time (a shove slides its full tile,
/// 2 m, tg's glide of 8 pixels a tick is ceil(32 / 8) = 4 ticks), anything else snaps. While it slides the wish is ignored and
/// the bridge calls no Move (tg already moved the mob).
/// </summary>
public class DisplacementTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);
    private const int C = T2Room.C;

    private static void TgMove(T2Room r, mob_living who, int x, int y, float dir) => r.Command(() => Waiting.NoSleep(who.Move(locate(x, y, 1), dir)));

    [Fact]
    public void AOneTileMoveByTgSlidesTheCentreInFourEqualSharesAndTheLastLandsOnTheTilesCentre()
    {
        var r = new T2Room(701);
        mob_living_carbon_human a = r.Join(10, 10, "Alice", name: "mover");
        Assert.Equal(8f, r.In(() => a.glide_size)); // tg's default glide_size
        TgMove(r, a, 11, 10, EAST);
        var xs = new List<int>();
        for (int tick = 0; tick < 5; tick++)
        {
            r.Go(a, 0, 0, East);
            xs.Add(r.State(a).X);
            r.AssertOnLoc(a, "tick " + tick);
            Assert.Equal(10 * C, r.State(a).Y);
        }
        // the slide runs in the very tick tg moved the mob (the locomotion phase follows the command phase): 4 shares of 64
        Assert.Equal(new[] { 10 * C + 64, 10 * C + 128, 10 * C + 192, 11 * C, 11 * C }, xs);
        Assert.False(r.State(a).Slide.Active);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(32f, 1)]
    [InlineData(16f, 2)]
    [InlineData(8f, 4)]
    [InlineData(5f, 7)]   // ceil(32 / 5)
    [InlineData(4f, 8)]
    [InlineData(1f, 32)]
    public void TheSlideTakesTgsGlideTimeOfOneTile(float glideSize, int ticks)
    {
        var r = new T2Room(702);
        mob_living_carbon_human a = r.Join(10, 10, "Alice", name: "mover");
        r.In(() => a.glide_size = glideSize);
        TgMove(r, a, 10, 11, NORTH);
        int slid = 0;
        while (slid < 40)
        {
            r.Go(a, 0, 0, East);
            slid++;
            if (!r.State(a).Slide.Active)
                break;
        }
        Assert.Equal(ticks, slid);
        Assert.Equal((10 * C, 11 * C), (r.State(a).X, r.State(a).Y));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADiagonalMoveByTgSlidesToTheDiagonalNeighbour()
    {
        var r = new T2Room(703);
        mob_living_carbon_human a = r.Join(10, 10, "Alice", name: "mover");
        TgMove(r, a, 11, 11, NORTHEAST);
        r.Go(a, 0, 0, East);
        Assert.Equal((11, 11, 1), r.LocOf(a));
        MotionState s = r.State(a);
        Assert.True(s.Slide.Active);
        Assert.Equal((10 * C + 64, 10 * C + 64), (s.X, s.Y));
        r.Go(a, 0, 0, East, 3);
        Assert.Equal((11 * C, 11 * C), (r.State(a).X, r.State(a).Y));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AForceMoveAndATeleportFarAwaySnapAtOnceAndAnotherLevelToo()
    {
        var r = new T2Room(704);
        mob_living_carbon_human a = r.Join(5, 5, "Alice", name: "mover");
        r.Go(a, 127, 0, East, 1); // 85 into the tile
        r.Command(() => Waiting.NoSleep(a.forceMove(locate(12, 3, 1))));
        r.Go(a, 0, 0, East);
        Assert.Equal((12 * C, 3 * C), (r.State(a).X, r.State(a).Y));
        Assert.False(r.State(a).Slide.Active);
        r.AssertOnLoc(a);
        // a two-tile hop is not a neighbour either
        r.Command(() => Waiting.NoSleep(a.forceMove(locate(12, 5, 1))));
        r.Go(a, 0, 0, East);
        Assert.Equal((12 * C, 5 * C), (r.State(a).X, r.State(a).Y));
        Assert.False(r.State(a).Slide.Active);
        // a glide size of 0 is no glide: the next neighbour snaps
        r.In(() => a.glide_size = 0);
        TgMove(r, a, 13, 5, EAST);
        r.Go(a, 0, 0, East);
        Assert.Equal((13 * C, 5 * C), (r.State(a).X, r.State(a).Y));
        r.AssertOnLoc(a);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWishIsIgnoredWhileSlidingAndNoMoveOrCrossingEventIsMade()
    {
        var r = new T2Room(705);
        mob_living_carbon_human a = r.Join(10, 10, "Alice", name: "mover");
        TgMove(r, a, 11, 10, EAST);
        r.Go(a, 0, 0, East); // the slide begins
        r.In(() =>
        {
            On(a, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            On(a, COMSIG_MOB_CLIENT_MOVED, "client_moved", 3);
            On(a, COMSIG_MOVABLE_MOVED, "moved", 5);
        });
        // a wish to the west and then north while the 3 remaining shares play
        r.Go(a, -127, 0, East, 3);
        Assert.Equal((11 * C, 10 * C), (r.State(a).X, r.State(a).Y));
        Assert.Empty(r.Trace);
        // the slide is over: the wish counts again
        r.Go(a, 0, 127, East, 1);
        Assert.Equal(10 * C + 85, r.State(a).Y);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASlideIntoAnotherCapsuleEndsWithTheMoverPushedOutOfIt()
    {
        // a shove onto a tile where another body stands: the capsule slides to the tile's centre, then the push-out
        // separates them (the bodies do not overlap after the slide), and the invariant holds throughout
        var r = new T2Room(706);
        mob_living_carbon_human b = r.Dummy(11, 10, name: "other");
        mob_living_carbon_human a = r.Join(10, 10, "Alice", name: "mover");
        r.Command(() => Waiting.NoSleep(a.forceMove(locate(11, 10, 1)))); // forceMove: no Enter, so no bump and no swap (tg's Move would swap the two)
        for (int tick = 0; tick < 12; tick++)
        {
            r.Go(a, 0, 0, East);
            r.AssertOnLoc(a, "tick " + tick);
        }
        MotionState sa = r.State(a), sb = r.State(b);
        long dx = sa.X - sb.X, dy = sa.Y - sb.Y;
        Assert.True(dx * dx + dy * dy >= 90L * 90L - 2 * 90, $"the bodies overlap: ({dx},{dy})");
        Assert.Equal(11 * C, sb.X); // the other one was not moved
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMobInsideAnObjectStandsOnTheOuterTurfAndTakesNoPartInCollisions()
    {
        var r = new T2Room(707);
        mob_living_carbon_human a = r.Join(5, 5, "Alice", name: "mover");
        obj locker = r.Crate(8, 8);
        r.In(() => locker.density = FALSE);
        r.Command(() => Waiting.NoSleep(a.forceMove(locker)));
        r.Go(a, 0, 0, East, 2);
        Assert.Equal((8 * C, 8 * C), (r.State(a).X, r.State(a).Y));
        Assert.Same(locker, r.In(() => a.loc));
        // another capsule walks through the tile the locker stands on: the mob inside is not an obstacle
        mob_living_carbon_human b = r.Join(5, 8, "Bob", name: "other");
        r.Go(b, 127, 0, East, 12);
        Assert.True(r.State(b).X > 8 * C + 60);
        // M3 P3: with SSmachines in the Master the subsystems' random first fires moved (master.dm:653), so SSmobs now fires
        // within this test: the mob inside the locker breathes through the locker, obj.handle_internal_lifeform (objs.dm:141,
        // atmospherics, M4). That leaf is the only runtime allowed.
        Assert.True(r.Room.Runtimes.All(e => e.Message.Contains("handle_internal_lifeform", StringComparison.Ordinal)), string.Join("\n", r.Room.RuntimeText));
    }
}
