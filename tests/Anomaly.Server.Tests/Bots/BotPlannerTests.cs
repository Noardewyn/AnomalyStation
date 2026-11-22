using Anomaly.Core.Motion;
using Anomaly.Protocol;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

/// <summary>A solidity grid built from rows of text for the planners' tests (no replica, no codec): <c>#</c> is solid. Row 0 is the northmost; BYOND's x and y start at 1, y grows north.</summary>
internal sealed class GridSolidity : ISolidity
{
    private readonly string[] _rows;
    private readonly int _height;

    public GridSolidity(params string[] rows)
    {
        _rows = rows;
        _height = rows.Length;
    }

    public int Width => _rows[0].Length;
    public int Height => _height;

    public bool IsSolid(int x, int y, int z)
    {
        if (z != 1 || x < 1 || y < 1 || x > Width || y > _height) return true;
        return _rows[_height - y][x - 1] == '#';
    }

    /// <summary>The tile of a centre in 1/256 tile.</summary>
    public static int Tile(int subunits) => MotionMath.TileOf(subunits);

    /// <summary>Whether a capsule of the controller's radius centred at (x, y) subunits overlaps a solid tile.</summary>
    public bool Touches(int xs, int ys)
    {
        double r = MotionMath.DefaultRadius;
        double px = xs / 256.0, py = ys / 256.0;
        for (int tx = (int)Math.Floor(px - r + 0.5); tx <= (int)Math.Floor(px + r + 0.5); tx++)
        {
            for (int ty = (int)Math.Floor(py - r + 0.5); ty <= (int)Math.Floor(py + r + 0.5); ty++)
            {
                if (!IsSolid(tx, ty, 1)) continue;
                double nx = Math.Clamp(px, tx - 0.5, tx + 0.5), ny = Math.Clamp(py, ty - 0.5, ty + 0.5);
                double dx = px - nx, dy = py - ny;
                if (dx * dx + dy * dy < (r - 0.011) * (r - 0.011)) return true;
            }
        }
        return false;
    }
}

/// <summary>
/// The wander planner (continuous space, scope 2l): headings picked in the open, held for a while, steered away from walls
/// by looking ahead with the capsule's radius, re-picked when the position stops changing, and able to leave a dead end and
/// to run along a 1-tile corridor. The tests walk it on a tiny model of the controller (a body that moves along the wish and
/// stops at solid tiles), so no server and no codec are needed.
/// </summary>
public class WanderPlannerTests
{
    private static readonly string[] Room =
    {
        "##########",
        "#........#",
        "#........#",
        "#...##...#",
        "#...##...#",
        "#........#",
        "#........#",
        "##########",
    };

    // A body that obeys the wish at 1/3 tile per tick, sweeping axis by axis against the grid like the controller does.
    private sealed class Body
    {
        private readonly GridSolidity _grid;
        public int X, Y;
        public int Wall;

        public Body(GridSolidity grid, int tileX, int tileY)
        {
            _grid = grid;
            X = tileX * 256;
            Y = tileY * 256;
        }

        public void Move(BotWish wish)
        {
            int dx = (int)Math.Round(wish.MoveX / 127.0 * 85);
            int dy = (int)Math.Round(wish.MoveY / 127.0 * 85);
            if (!_grid.Touches(X, Y + dy)) Y += dy;
            else Wall++;
            if (!_grid.Touches(X + dx, Y)) X += dx;
            else Wall++;
        }
    }

    [Fact]
    public void ItNeverWalksIntoAWallAndKeepsMoving()
    {
        var grid = new GridSolidity(Room);
        var random = new BotRandom(5);
        var planner = new WanderPlanner(random);
        var body = new Body(grid, 2, 2);
        var visited = new HashSet<(int, int)>();
        for (int tick = 0; tick < 4000; tick++)
        {
            BotWish wish = planner.Update(true, body.X, body.Y, 1, grid);
            body.Move(wish);
            Assert.False(grid.Touches(body.X, body.Y), $"inside a wall at tick {tick}");
            visited.Add((GridSolidity.Tile(body.X), GridSolidity.Tile(body.Y)));
        }
        Assert.True(visited.Count >= 25, $"it saw only {visited.Count} tiles");
        // It looks ahead, so it hardly ever pushes into a wall.
        Assert.True(body.Wall < 400, $"{body.Wall} blocked sweeps in 4000 ticks");
        Assert.True(planner.Picks >= 20);
    }

    [Fact]
    public void ItIsDeterministicPerSeedAndDiffersBetweenSeeds()
    {
        var grid = new GridSolidity(Room);
        List<(sbyte, sbyte, ushort)> Run(ulong seed)
        {
            var planner = new WanderPlanner(new BotRandom(seed));
            var body = new Body(grid, 3, 6);
            var path = new List<(sbyte, sbyte, ushort)>();
            for (int i = 0; i < 500; i++)
            {
                BotWish w = planner.Update(true, body.X, body.Y, 1, grid);
                body.Move(w);
                path.Add((w.MoveX, w.MoveY, w.Yaw));
            }
            return path;
        }
        Assert.Equal(Run(1), Run(1));
        Assert.NotEqual(Run(1), Run(2));
    }

    [Fact]
    public void WithoutAPositionItAsksForNothing()
    {
        var planner = new WanderPlanner(new BotRandom(1));
        Assert.True(planner.Update(false, 0, 0, 1, null).IsStill);
        Assert.Equal(0, planner.Picks);
    }

    [Fact]
    public void ItPicksAgainWhenThePositionStopsChanging()
    {
        var planner = new WanderPlanner(new BotRandom(9), new WanderSettings { BlockedTicks = 5, MinHoldTicks = 1000, MaxHoldTicks = 2000 });
        var open = new GridSolidity("....", "....", "....", "....");
        // The body does not move at all (something holds it): after BlockedTicks the heading changes.
        planner.Update(true, 2 * 256, 2 * 256, 1, open);
        double first = planner.Heading;
        for (int i = 0; i < 20; i++) planner.Update(true, 2 * 256, 2 * 256, 1, open);
        Assert.True(planner.BlockedRepicks >= 1);
        Assert.True(planner.Picks >= 2);
        Assert.NotEqual(first, planner.Heading);
    }

    [Fact]
    public void ADeadEndOffersOnlyTheWayBackAndItTakesIt()
    {
        // A 1-wide dead-end corridor going east from a hall: the bot starts at the end.
        var grid = new GridSolidity(
            "############",
            "#..........#",
            "#.##########",
            "#.##########",
            "#..........#",
            "############");
        var planner = new WanderPlanner(new BotRandom(3));
        var body = new Body(grid, 11, 2); // the east end of the lower corridor
        for (int tick = 0; tick < 600; tick++)
        {
            body.Move(planner.Update(true, body.X, body.Y, 1, grid));
            Assert.False(grid.Touches(body.X, body.Y));
        }
        Assert.True(GridSolidity.Tile(body.X) <= 10, "it got out of the east end at least a tile");
    }

    [Fact]
    public void ItRunsAlongAOneTileCorridorWithoutGettingStuck()
    {
        var grid = new GridSolidity(
            "##############",
            "#....#########",
            "#....#########",
            "#.............",
            "#....#########",
            "#....#########",
            "##############");
        // The east end is open to the map's edge (solid outside), so the corridor is a 1-wide dead end of length 8.
        var planner = new WanderPlanner(new BotRandom(11));
        var body = new Body(grid, 3, 3);
        int maxX = 0, minX = 99;
        for (int tick = 0; tick < 3000; tick++)
        {
            body.Move(planner.Update(true, body.X, body.Y, 1, grid));
            Assert.False(grid.Touches(body.X, body.Y));
            maxX = Math.Max(maxX, GridSolidity.Tile(body.X));
            minX = Math.Min(minX, GridSolidity.Tile(body.X));
        }
        Assert.True(maxX >= 9, $"it never entered the corridor (max x {maxX})");
        Assert.True(minX <= 3, "it never came back to the room");
    }

    [Fact]
    public void FreeDistanceSweepsTheBodyAlongTheHeading()
    {
        var planner = new WanderPlanner(new BotRandom(1));
        var grid = new GridSolidity("#####", "#...#", "#...#", "#####");
        // From the centre of (2, 2) heading east the wall tile x = 5 starts at x = 4.5: 2.5 tiles away, less the radius.
        double free = planner.FreeDistance(2, 2, 1, 0.25, 10, grid);
        Assert.InRange(free, 1.9, 2.4);
        Assert.Equal(10, planner.FreeDistance(2, 2, 1, 0.25, 10, null)); // nothing known: free
    }

    [Theory]
    [InlineData(0.0, 0, 127, 0)]
    [InlineData(0.25, 127, 0, 16384)]
    [InlineData(0.5, 0, -127, 32768)]
    [InlineData(0.75, -127, 0, 49152)]
    public void TheWishPointsAlongTheCompassBearing(double turns, int moveX, int moveY, int yaw)
    {
        BotWish wish = BotWish.Toward(turns, 127);
        Assert.Equal((moveX, moveY, yaw), (wish.MoveX, wish.MoveY, (int)wish.Yaw));
        Assert.InRange(BotWish.BearingOf(moveX, moveY) - turns, -1e-9, 1e-9);
    }
}

public class ShovePlannerTests
{
    private static OtherBody Other(long id, double x, double y, bool lying = false) => new(id, (int)(x * 256), (int)(y * 256), lying);

    [Fact]
    public void ItWalksToTheNearestStandingBodyAndFacesIt()
    {
        var planner = new ShovePlanner();
        var others = new[] { Other(10, 9, 5), Other(11, 5, 8), Other(12, 5, 5, lying: true) };
        ShoveStep step = planner.Update(100, 5 * 256, 5 * 256, 1, others);
        Assert.True(step.HasTarget);
        Assert.Equal(11, step.TargetId);          // 3 tiles north, not 4 east, and the lying one at 0 is ignored
        Assert.False(step.Click);
        Assert.Equal(0, step.Wish.MoveX);
        Assert.Equal(127, step.Wish.MoveY);
        Assert.Equal(0, step.Wish.Yaw);           // north
        Assert.InRange(step.DistanceTiles, 2.99, 3.01);
    }

    [Fact]
    public void WithinReachItClicksOnceAndWaitsTheCooldown()
    {
        var planner = new ShovePlanner();
        var near = new[] { Other(7, 5, 6) };
        ShoveStep first = planner.Update(100, 5 * 256, 5 * 256, 1, near);
        Assert.True(first.Click);
        Assert.Equal(7, first.TargetId);
        for (int t = 101; t < 100 + ShovePlanner.CooldownTicks; t++)
            Assert.False(planner.Update(t, 5 * 256, 5 * 256, 1, near).Click, $"a click at tick {t}");
        Assert.True(planner.Update(100 + ShovePlanner.CooldownTicks, 5 * 256, 5 * 256, 1, near).Click);
        Assert.Equal(2, planner.Clicks);
    }

    [Fact]
    public void NobodyInSightMeansNoTargetAndTheOwnBodyAndTheLyingAreSkipped()
    {
        var planner = new ShovePlanner();
        Assert.False(planner.Update(1, 0, 0, 1, ReadOnlySpan<OtherBody>.Empty).HasTarget);
        Assert.False(planner.Update(1, 0, 0, 1, new[] { Other(1, 3, 3), Other(2, 4, 4, lying: true) }).HasTarget);
    }

    [Fact]
    public void ATieGoesToTheLowerIdAndAnOverlapKeepsStill()
    {
        var planner = new ShovePlanner();
        ShoveStep tie = planner.Update(1, 0, 0, 99, new[] { Other(20, 2, 0), Other(5, 0, 2) });
        Assert.Equal(5, tie.TargetId);
        ShoveStep onTop = planner.Update(50, 256, 256, 99, new[] { Other(3, 1, 1) });
        Assert.True(onTop.HasTarget);
        Assert.True(onTop.Wish.IsStill);
        Assert.True(onTop.Click);
    }

    [Fact]
    public void ResetForgetsTheCooldown()
    {
        var planner = new ShovePlanner();
        var near = new[] { Other(7, 5, 6) };
        Assert.True(planner.Update(100, 5 * 256, 5 * 256, 1, near).Click);
        planner.Reset();
        Assert.True(planner.Update(101, 5 * 256, 5 * 256, 1, near).Click);
    }
}

public class RestScheduleTests
{
    [Fact]
    public void ItLiesDownAfterFiveToFifteenSecondsStaysAndGetsUp()
    {
        var rest = new RestSchedule(new BotRandom(2));
        long lieAt = -1, upAt = -1;
        bool lying = false;
        for (long t = 0; t < 2000 && upAt < 0; t++)
        {
            RestStep step = rest.Update(t, lying);
            if (step.SendRest && lieAt < 0)
            {
                lieAt = t;
                lying = true;           // the server made it lie
                Assert.False(step.MayMove);
            }
            else if (step.SendRest)
            {
                upAt = t;
                Assert.True(step.MayMove);
                lying = false;
            }
            else if (lieAt >= 0 && upAt < 0)
            {
                Assert.False(step.MayMove, "it asks to move while lying");
            }
        }
        Assert.InRange(lieAt, 5 * 20, 15 * 20);
        Assert.InRange(upAt - lieAt, 3 * 20, 6 * 20);
        Assert.Equal((1, 1), (rest.LieDowns, rest.GetUps));
    }

    [Fact]
    public void ARestThatNeverMadeTheBodyLieIsGivenUpAfterTheConfirmWindow()
    {
        var rest = new RestSchedule(new BotRandom(3), new RestSettings { MinUpSeconds = 1, MaxUpSeconds = 1, ConfirmTicks = 30 });
        bool sent = false;
        long t = 0;
        for (; t < 200; t++)
        {
            if (rest.Update(t, false).SendRest) { sent = true; break; }
        }
        Assert.True(sent);
        long from = t;
        for (t++; t < from + 100; t++)
        {
            RestStep step = rest.Update(t, false); // the posture never says lying
            Assert.False(step.SendRest, "a second Rest would lie the body down");
            if (step.MayMove) break;
        }
        Assert.Equal(1, rest.Refused);
        Assert.False(rest.Down);
    }

    [Fact]
    public void ABodyThatIsAlreadyDownIsNotAskedToRest()
    {
        var rest = new RestSchedule(new BotRandom(4), new RestSettings { MinUpSeconds = 1, MaxUpSeconds = 1 });
        for (long t = 0; t < 60; t++)
            Assert.False(rest.Update(t, true).SendRest); // knocked down (a shove): left alone
        Assert.Equal(0, rest.LieDowns);
    }

    [Fact]
    public void ToggleSchedulesPressAndPressBack()
    {
        var toggle = new ToggleSchedule(new BotRandom(6), 2, 2, 1, 1);
        var presses = new List<long>();
        for (long t = 0; t < 400; t++)
            if (toggle.Update(t)) presses.Add(t);
        Assert.True(presses.Count >= 6);
        Assert.Equal(presses.Count % 2 == 1, toggle.On);
        // On for 1 s, off for 2 s: press gaps alternate 20 and 40 ticks.
        Assert.Equal(40, presses[0]);
        Assert.Equal(20, presses[1] - presses[0]);
        Assert.Equal(40, presses[2] - presses[1]);
        toggle.Reset();
        Assert.False(toggle.On);
    }
}

public class BotDriverTests
{
    private static OwnBody Own(int tx, int ty, bool? lying = false) => new(true, 1, tx * 256, ty * 256, 1, lying);

    [Fact]
    public void WithoutAnOwnBodyItSendsNothing()
    {
        var driver = new BotDriver(BotBehaviour.Wander, false, new BotRandom(1));
        var actions = new List<PlannedAction>();
        BotWish wish = driver.Decide(1, default, ReadOnlySpan<OtherBody>.Empty, null, actions);
        Assert.True(wish.IsStill);
        Assert.Empty(actions);
    }

    [Fact]
    public void AMixedBotIsRefusedItMustBeResolvedFirst()
    {
        Assert.Throws<ArgumentException>(() => new BotDriver(BotBehaviour.Mixed, false, new BotRandom(1)));
        Assert.Equal(new[] { BotBehaviour.Wander, BotBehaviour.Rest, BotBehaviour.Shove, BotBehaviour.Wander },
            Enumerable.Range(0, 4).Select(i => BotBehaviourNames.Resolve(BotBehaviour.Mixed, i)));
        Assert.Equal(BotBehaviour.Churn, BotBehaviourNames.Resolve(BotBehaviour.Churn, 2));
    }

    [Fact]
    public void ARestingBotAsksForRestAndDoesNotMoveWhileItLies()
    {
        var driver = new BotDriver(BotBehaviour.Rest, false, new BotRandom(2), rest: new RestSettings { MinUpSeconds = 1, MaxUpSeconds = 1, MinDownSeconds = 2, MaxDownSeconds = 2 });
        var grid = new GridSolidity("########", "#......#", "#......#", "#......#", "########");
        var actions = new List<PlannedAction>();
        bool lying = false;
        int stillTicks = 0;
        for (long t = 0; t < 200; t++)
        {
            actions.Clear();
            BotWish wish = driver.Decide(t, Own(3, 2, lying), ReadOnlySpan<OtherBody>.Empty, grid, actions);
            foreach (PlannedAction a in actions)
            {
                Assert.Equal(ActionKind.Rest, a.Kind);
                lying = !lying; // the server toggles
            }
            if (lying) { Assert.True(wish.IsStill); stillTicks++; }
        }
        Assert.True(driver.Rest.LieDowns >= 2);
        Assert.True(stillTicks >= 40);
    }

    [Fact]
    public void AShovingBotWalksToAnotherAndClicksItRightWithItsId()
    {
        var driver = new BotDriver(BotBehaviour.Shove, false, new BotRandom(3));
        var grid = new GridSolidity("##########", "#........#", "#........#", "#........#", "##########");
        var actions = new List<PlannedAction>();
        var target = new[] { new OtherBody(77, 3 * 256, 3 * 256, false) };
        BotWish far = driver.Decide(1, Own(8, 3), target, grid, actions);
        Assert.Empty(actions);
        Assert.True(far.MoveX < 0 && far.MoveY == 0); // west toward it
        actions.Clear();
        driver.Decide(2, Own(3, 3), target, grid, actions);
        var click = Assert.Single(actions);
        Assert.Equal((ActionKind.Click, 77L, ClickButton.Right), (click.Kind, click.Target, click.Button));
    }

    [Fact]
    public void AShovingBotDoesNotChaseABodyBehindAWallOrFarAway()
    {
        var grid = new GridSolidity(
            "####################",
            "#........#.........#",
            "#........#.........#",
            "#........#.........#",
            "####################");
        var driver = new BotDriver(BotBehaviour.Shove, false, new BotRandom(5));
        var actions = new List<PlannedAction>();
        var behindTheWall = new[] { new OtherBody(9, 15 * 256, 3 * 256, false) };
        for (long t = 0; t < 300; t++)
        {
            BotWish wish = driver.Decide(t, Own(3, 3), behindTheWall, grid, actions);
            Assert.Empty(actions);
            Assert.False(wish.MoveX > 100 && wish.MoveY == 0 && t > 0 && driver.Shove.Clicks > 0);
        }
        Assert.Equal(0, driver.Shove.Clicks); // it never reached it, so it never clicked
        // In the open but farther than the chase distance: it wanders.
        var wide = new GridSolidity(Enumerable.Repeat(new string('.', 40), 6).ToArray());
        var far = new[] { new OtherBody(9, 30 * 256, 3 * 256, false) };
        var d2 = new BotDriver(BotBehaviour.Shove, false, new BotRandom(6));
        d2.Decide(1, Own(3, 3), far, wide, actions);
        Assert.True(d2.Wander.Picks >= 1, "it wandered instead of chasing");
        // Near and clear: it chases.
        var near = new[] { new OtherBody(9, 8 * 256, 3 * 256, false) };
        var d3 = new BotDriver(BotBehaviour.Shove, false, new BotRandom(7));
        BotWish chase = d3.Decide(1, Own(3, 3), near, wide, actions);
        Assert.Equal(0, d3.Wander.Picks);
        Assert.True(chase.MoveX > 0);
    }

    [Fact]
    public void WithTogglesOnItNowAndThenPressesWalkRunAndCombatMode()
    {
        var driver = new BotDriver(BotBehaviour.Wander, true, new BotRandom(4));
        var grid = new GridSolidity(new string('.', 20), new string('.', 20), new string('.', 20), new string('.', 20));
        var kinds = new HashSet<ActionKind>();
        var actions = new List<PlannedAction>();
        for (long t = 0; t < 4000; t++)
        {
            actions.Clear();
            driver.Decide(t, Own(5, 2), ReadOnlySpan<OtherBody>.Empty, grid, actions);
            foreach (PlannedAction a in actions) kinds.Add(a.Kind);
        }
        Assert.Contains(ActionKind.ToggleMoveIntent, kinds);
        Assert.Contains(ActionKind.ToggleCombatMode, kinds);
        Assert.DoesNotContain(ActionKind.Click, kinds);
        Assert.DoesNotContain(ActionKind.Rest, kinds);
    }
}
