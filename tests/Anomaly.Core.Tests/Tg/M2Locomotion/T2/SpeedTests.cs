using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// Speed (D106, scope 2c): tg's one tile costs cached_multiplicative_slowdown deciseconds, so the capsule covers
/// 1 / slowdown tiles per decisecond, 0.5 / slowdown per 0.5 ds tick, times MOVE_SPEED_SCALE; the same in every direction
/// (DV15); read from tg every tick. A tick's step is quantized to 1/256 tile, so the long-run speed is within half a subunit a
/// tick of the exact one.
/// </summary>
public class SpeedTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);
    private const int C = T2Room.C;

    // the wish of a tick in subunits for a straight wish of full length: floor(256 * 0.5 / slowdown * scale + 0.5)
    private static int Step(float slowdown, float scale = 1f) => (int)Math.Floor(256.0 * 0.5 / Math.Max(slowdown, 0.5) * scale + 0.5);

    [Theory]
    [InlineData(1.5f)]  // run: 85.33
    [InlineData(4f)]    // walk: 32
    [InlineData(2.03f)] // run at 40 damage (scope 2c table)
    [InlineData(2.82f)]
    [InlineData(5.5f)]  // crawl
    [InlineData(7.5f)]  // crawl in soft crit
    [InlineData(2.35f)] // staggered
    public void ACardinalWalkCoversOneOverSlowdownTilesPerDecisecond(float slowdown)
    {
        var r = new T2Room(801);
        mob_living_carbon_human m = r.Join(2, 8, "Alice", slowdown, "mover");
        int x0 = r.State(m).X;
        const int ticks = 30; // 15 ds
        for (int tick = 0; tick < ticks; tick++)
        {
            int before = r.State(m).X;
            r.Go(m, 127, 0, East);
            Assert.Equal(Step(slowdown), r.State(m).X - before); // every tick the same whole number of subunits
            r.AssertOnLoc(m);
        }
        double exact = ticks * 256.0 * 0.5 / slowdown;
        Assert.InRange(r.State(m).X - x0, exact - ticks * 0.5, exact + ticks * 0.5);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(90f)]
    [InlineData(180f)]
    [InlineData(270f)]
    [InlineData(45f)]
    [InlineData(135f)]
    [InlineData(22.5f)]
    [InlineData(67.5f)]
    [InlineData(10f)]
    [InlineData(300f)]
    public void TheSpeedIsTheSameInEveryDirection(float degrees)
    {
        // DV15: tg's diagonal step is 6 % faster per axis pair (2.0 ds for a diagonal tile of 1.41); ours is isotropic
        var r = new T2Room(802);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        double rad = degrees * Math.PI / 180.0;
        sbyte mx = (sbyte)Math.Round(127 * Math.Sin(rad)); // compass bearing: 0 north, 90 east
        sbyte my = (sbyte)Math.Round(127 * Math.Cos(rad));
        int x0 = r.State(m).X, y0 = r.State(m).Y;
        r.Go(m, mx, my, East, 6);
        double dx = r.State(m).X - x0, dy = r.State(m).Y - y0;
        double perTick = Math.Sqrt(dx * dx + dy * dy) / 6;
        Assert.InRange(perTick, 85.33 - 2.5, 85.33 + 2.5);
        r.AssertOnLoc(m);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnOverlongWishVectorIsClampedToFullSpeed()
    {
        var r = new T2Room(803);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        int x0 = r.State(m).X, y0 = r.State(m).Y;
        r.Go(m, 127, 127, East, 1); // length 179.6 clamped to 127: 60 on each axis
        Assert.Equal((x0 + 60, y0 + 60), (r.State(m).X, r.State(m).Y));
        r.Go(m, -128, -128, East, 1);
        Assert.Equal((x0, y0), (r.State(m).X, r.State(m).Y));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHalfWishIsHalfTheSpeed()
    {
        var r = new T2Room(804);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        int x0 = r.State(m).X;
        r.Go(m, 64, 0, East, 4);
        Assert.InRange(r.State(m).X - x0, 4 * 43 - 2, 4 * 43 + 2); // 85.33 * 64 / 127 = 43
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(2f)]
    [InlineData(3f)]
    [InlineData(4f)]
    public void TheSpeedScaleMultipliesTheSpeedOfEveryCapsule(float scale)
    {
        var r = new T2Room(805);
        LocomotionSeam.SetSpeedScale(r.World, scale);
        mob_living_carbon_human m = r.Join(2, 8, "Alice", name: "mover");
        Assert.Equal(0.5f / 1.5f * scale, r.Params(m).TilesPerTick, 5);
        int x0 = r.State(m).X;
        int before = x0;
        for (int tick = 0; tick < 6; tick++)
        {
            r.Go(m, 127, 0, East);
            Assert.Equal(Step(1.5f, scale), r.State(m).X - before);
            before = r.State(m).X;
            r.AssertOnLoc(m, $"scale {scale} tick {tick}");
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMobsOwnSpeedMultipliesOnlyItsCapsuleOverTheWorldsScaleUpToTheCap()
    {
        var r = new T2Room(808);
        LocomotionSeam.SetSpeedScale(r.World, 0.5f);
        mob_living_carbon_human fast = r.Join(2, 8, "Alice", name: "fast");
        mob_living_carbon_human other = r.Join(2, 10, "Bob", name: "other");
        r.In(() => Assert.True(LocomotionSeam.SetOwnSpeed(fast, 4f)));
        Assert.Equal(0.5f / 1.5f * 2f, r.Params(fast).TilesPerTick, 5);
        Assert.Equal(0.5f / 1.5f * 0.5f, r.Params(other).TilesPerTick, 5);
        int before = r.State(fast).X;
        r.Go(fast, 127, 0, East);
        Assert.Equal(Step(1.5f, 2f), r.State(fast).X - before);
        Assert.Equal(0.5f / 1.5f * 2f, r.Params(fast).TilesPerTick, 5); // the phase keeps it
        LocomotionSeam.SetSpeedScale(r.World, 2f);
        Assert.Equal(0.5f / 1.5f * 4f, r.Params(fast).TilesPerTick, 5); // 2 x 4 stops at the cap
        Assert.Equal(0.5f / 1.5f * 2f, r.Params(other).TilesPerTick, 5);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASpeedOverATilePerTickCrossesSeveralTilesInOneTickAndEachCrossingIsARealMove()
    {
        var r = new T2Room(806);
        LocomotionSeam.SetSpeedScale(r.World, 4f);          // 4/3 tile a tick
        mob_living_carbon_human m = r.Join(2, 8, "Alice", name: "mover");
        var seen = new List<string>();
        r.In(() => Wave4.Integration.Toy.On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1, act: a => seen.Add(Wave4.Integration.Toy.D(a[1]))));
        r.Go(m, 127, 0, East, 3);                           // 3 * 341 = 1024 subunits: from 512 to 1536 = 4 tiles
        Assert.Equal(new[] { "(3,8)", "(4,8)", "(5,8)", "(6,8)" }, seen);
        Assert.Equal((6, 8, 1), r.LocOf(m));
        r.AssertOnLoc(m);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TgsSlowdownIsReadEveryTick()
    {
        var r = new T2Room(807);
        mob_living_carbon_human m = r.Join(2, 8, "Alice", name: "mover");
        r.Go(m, 127, 0, East, 2);
        int x = r.State(m).X;
        r.In(() => m.cached_multiplicative_slowdown = 4f); // a modifier changed it (walk intent, damage, a drug)
        r.Go(m, 127, 0, East, 1);
        Assert.Equal(32, r.State(m).X - x);
        Assert.Equal(0.125f, r.Params(m).TilesPerTick, 6);
        r.In(() => m.cached_multiplicative_slowdown = 0.25f); // under a tick: a tile a tick at the most
        r.Go(m, 127, 0, East, 1);
        Assert.Equal(1f, r.Params(m).TilesPerTick, 6);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMobThatCannotMoveDoesNotMoveAtAnySpeed()
    {
        var r = new T2Room(808);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        r.In(() => ADD_TRAIT(m, TRAIT_NO_TRANSFORM, "t2"));
        int x0 = r.State(m).X;
        r.Go(m, 127, 0, East, 6);
        Assert.Equal(x0, r.State(m).X);
        Assert.False(r.Params(m).CanMove);
        r.In(() => REMOVE_TRAIT(m, TRAIT_NO_TRANSFORM, "t2"));
        r.Go(m, 127, 0, East, 1);
        Assert.Equal(x0 + 85, r.State(m).X);
        Assert.True(r.Params(m).CanMove);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMoveDelayInTicksIsTgsDelayRuleOfTheWaitingModel()
    {
        var r = new T2Room(809);
        mob_living_carbon_human m = r.Join(2, 8, "Alice", name: "mover");
        foreach ((float slowdown, short ticks) in new (float, short)[] { (1.5f, 3), (4f, 8), (1f, 2), (0.5f, 1), (0.3f, 1), (2.5f, 5), (5.5f, 11), (7.5f, 15), (1.4f, 2), (0.1f * 15, 3) })
        {
            r.In(() => m.cached_multiplicative_slowdown = slowdown);
            r.Step();
            Assert.Equal(ticks, r.Params(m).MoveDelayTicks);
        }
        r.AssertNoRuntimes();
    }
}
