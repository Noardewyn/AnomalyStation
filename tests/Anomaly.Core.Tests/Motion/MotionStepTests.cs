using Anomaly.Core.Motion;
using static Anomaly.Core.Tests.Motion.MotionTest;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// The capsule controller (M2 P4, <c>MotionStep</c>; docs/ai/specs/locomotion.md section 2): speed, the tile sweep and the
/// slide along walls, the crossing events and their order, presses at tg's move delay, capsule contacts and the push-out,
/// the slide of a tg-made displacement, the clamp after a refused Move.
/// </summary>
public class MotionStepTests
{
    // Tile (5, 5) is centred on (1280, 1280); its east face is x = 1408; the capsule's half-side is 45 subunits.
    private const int C5 = 5 * 256;

    [Fact]
    public void RunningEastMovesAThirdOfATilePerTick()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        Step(ref s, Frame(127, 0), Run, view);
        Assert.Equal((C5 + 85, C5), (s.X, s.Y)); // 1.5 ds per tile: 1/3 tile per 0.5 ds tick = 85.33 subunits, quantized
        Step(ref s, Frame(127, 0), Run, view);
        Assert.Equal(C5 + 170, s.X);
    }

    [Fact]
    public void TheSpeedIsTheSameInEveryDirection()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        Step(ref s, Frame(127, 127), Run, view);
        Assert.Equal((C5 + 60, C5 + 60), (s.X, s.Y)); // the wish's length is clamped to 127: 85 * 0.707 per axis
        MotionState w = MotionState.AtTile(5, 5, 1);
        Step(ref w, Frame(-128, 0), Run, view); // -128 is longer than 127: clamped too
        Assert.Equal(C5 - 85, w.X);
    }

    [Fact]
    public void AHalfWishMovesHalfAsFast()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        Step(ref s, Frame(0, 64), Run, view);
        Assert.Equal(C5 + 43, s.Y);
    }

    [Fact]
    public void AStateThatCannotMoveKeepsItsPlaceButTakesTheYaw()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        MotionParams frozen = Run with { CanMove = false };
        List<MotionEvent> events = Step(ref s, Frame(127, 0, yaw: 16384), frozen, view);
        Assert.Equal((C5, C5), (s.X, s.Y));
        Assert.Equal(16384, s.Yaw);
        Assert.Empty(events);
    }

    [Fact]
    public void AWallStopsTheCapsuleAtItsFaceAndThePressRepeatsAtTgsMoveDelay()
    {
        var view = new GridView().Solid(6, 5);
        MotionState s = MotionState.AtTile(5, 5, 1);
        var presses = new List<int>();
        for (int tick = 1; tick <= 7; tick++)
        {
            List<MotionEvent> events = Step(ref s, Frame(127, 0), Run, view);
            Assert.Equal(1408 - 45, s.X); // the box touches the face, never passes it
            foreach (MotionEvent e in events)
            {
                Assert.Equal(MotionEventKind.Pressing, e.Kind);
                Assert.Equal((5, 5, 6, 5, MotionDir.East), (e.FromX, e.FromY, e.ToX, e.ToY, e.Dir));
                presses.Add(tick);
            }
        }
        Assert.Equal(new[] { 1, 4, 7 }, presses); // RUN_DELAY 1.5 ds is 3 ticks: tg's client/Move tries the wall every move delay
    }

    [Fact]
    public void ThePressTimerRunsDownWhileNotPressing()
    {
        var view = new GridView().Solid(6, 5);
        MotionState s = MotionState.AtTile(5, 5, 1);
        Assert.Single(Step(ref s, Frame(127, 0), Run, view)); // a press
        Assert.Equal(3, s.PressTimer);
        Step(ref s, Frame(0, 0), Run, view);
        Step(ref s, Frame(0, 0), Run, view);
        Step(ref s, Frame(0, 0), Run, view);
        Assert.Equal(0, s.PressTimer);
        Assert.Single(Step(ref s, Frame(127, 0), Run, view)); // at once again
    }

    [Fact]
    public void ACapsuleSlidesAlongAWallAndPressesOnlyWithin45DegreesOfIt()
    {
        var view = new GridView();
        for (int x = 1; x <= 20; x++) view.Solid(x, 6); // a wall along the north side of row 5
        MotionState s = MotionState.AtTile(5, 5, 1);
        int pressesShallow = 0;
        for (int i = 0; i < 6; i++)
            pressesShallow += Step(ref s, Frame(127, 40), Run, view).Count(e => e.Kind == MotionEventKind.Pressing);
        Assert.Equal(1408 - 45, s.Y); // against the wall
        Assert.True(s.X > C5 + 400, "it kept sliding east"); // the north component is lost, the east one kept
        Assert.Equal(0, pressesShallow); // mostly east: not a press into the north face

        MotionState n = MotionState.AtTile(5, 5, 1);
        int pressesSteep = 0;
        for (int i = 0; i < 6; i++)
            pressesSteep += Step(ref n, Frame(40, 127), Run, view).Count(e => e.Kind == MotionEventKind.Pressing && e.Dir == MotionDir.North);
        Assert.True(pressesSteep >= 1);
    }

    [Fact]
    public void ACornerOfADiagonalNeighbourBlocksWithoutAPress()
    {
        // The box near the top of row 5 overlaps row 6; the tile north-east is solid, the one east is open.
        var view = new GridView().Solid(6, 6);
        MotionState s = MotionState.AtTile(5, 5, 1);
        s.Y = C5 + 100; // box top 1425 overlaps row 6
        List<MotionEvent> events = Step(ref s, Frame(127, 0), Run, view);
        Assert.Equal(1408 - 45, s.X);
        Assert.Empty(events); // tile (6, 5) is open: no Move attempt into it
    }

    [Fact]
    public void ACrossingIsOneCardinalEvent()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        s.X = 1340;
        List<MotionEvent> events = Step(ref s, Frame(127, 0), Run, view);
        Assert.Equal(1425, s.X);
        MotionEvent e = Assert.Single(events);
        Assert.Equal((MotionEventKind.Crossed, 5, 5, 6, 5, MotionDir.East), (e.Kind, e.FromX, e.FromY, e.ToX, e.ToY, e.Dir));
        Assert.Equal(6, s.TileX);
    }

    [Fact]
    public void AnExactCornerGivesNorthSouthThenEastWest()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        s.X = 1360;
        s.Y = 1360;
        List<MotionEvent> events = Step(ref s, Frame(127, 127), Run, view);
        Assert.Equal((1420, 1420), (s.X, s.Y));
        Assert.Equal(2, events.Count);
        Assert.Equal((5, 5, 5, 6, MotionDir.North), (events[0].FromX, events[0].FromY, events[0].ToX, events[0].ToY, events[0].Dir));
        Assert.Equal((5, 6, 6, 6, MotionDir.East), (events[1].FromX, events[1].FromY, events[1].ToX, events[1].ToY, events[1].Dir));
    }

    [Fact]
    public void TwoCrossingsComeInTheOrderThePathMeetsTheBorders()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        s.X = 1400;
        s.Y = 1380;
        List<MotionEvent> events = Step(ref s, Frame(127, 64), Run, view);
        Assert.Equal((1476, 1418), (s.X, s.Y));
        Assert.Equal(new[] { MotionDir.East, MotionDir.North }, events.Select(e => e.Dir).ToArray());
        Assert.Equal((6, 5), (events[0].ToX, events[0].ToY));
        Assert.Equal((6, 6), (events[1].ToX, events[1].ToY));

        MotionState t = MotionState.AtTile(5, 5, 1);
        t.X = 1380;
        t.Y = 1400;
        events = Step(ref t, Frame(64, 127), Run, view);
        Assert.Equal(new[] { MotionDir.North, MotionDir.East }, events.Select(e => e.Dir).ToArray());
    }

    [Fact]
    public void AStraightPathThroughAWallsCornerIsCrossedTheWayTheSweepWent()
    {
        // Fast (1 tile per tick) north-east from just west of a solid tile (6, 5): the straight path would cross into (6, 5)
        // first; the sweep went north first, so the crossings are north, then east (never into the solid tile).
        var view = new GridView().Solid(6, 5);
        MotionState s = MotionState.AtTile(5, 5, 1);
        s.X = 1363;
        s.Y = 1300;
        MotionParams fast = Run with { TilesPerTick = 1f };
        List<MotionEvent> events = Step(ref s, Frame(127, 127), fast, view);
        Assert.Equal((1544, 1481), (s.X, s.Y));
        Assert.Equal(new[] { MotionDir.North, MotionDir.East }, events.Select(e => e.Dir).ToArray());
        Assert.Equal((5, 6), (events[0].ToX, events[0].ToY));
    }

    [Fact]
    public void ABlockedFaceStopsLikeAWallAndIsPressed()
    {
        var view = new GridView().Edge(5, 5, TileEdges.East); // a window on the east border of (5, 5) (M3)
        MotionState s = MotionState.AtTile(5, 5, 1);
        List<MotionEvent> events = Step(ref s, Frame(127, 0), Run, view);
        Assert.Equal(1408 - 45, s.X);
        Assert.Equal(MotionEventKind.Pressing, Assert.Single(events).Kind);

        var other = new GridView().Edge(6, 5, TileEdges.West); // the same face seen from the other side
        MotionState t = MotionState.AtTile(5, 5, 1);
        Step(ref t, Frame(127, 0), Run, other);
        Assert.Equal(1408 - 45, t.X);
    }

    [Fact]
    public void TheMapEdgeIsSolid()
    {
        var view = new GridView(10, 10);
        MotionState s = MotionState.AtTile(1, 1, 1);
        for (int i = 0; i < 5; i++) Step(ref s, Frame(-127, -127), Run, view);
        Assert.Equal((256 - 128 + 45, 256 - 128 + 45), (s.X, s.Y));
    }

    [Fact]
    public void WalkingIntoAStandingCapsuleTouchesItAndStopsAtItsSurface()
    {
        var view = new GridView();
        view.Capsules.Add(new CapsuleObstacle(7, C5 + 110, C5, 0.175f));
        MotionState s = MotionState.AtTile(5, 5, 1);
        List<MotionEvent> events = Step(ref s, Frame(127, 0), Run, view);
        Assert.Equal((C5 + 20, C5), (s.X, s.Y)); // pushed back to the surface: 90 subunits from the other centre
        MotionEvent contact = Assert.Single(events);
        Assert.Equal((MotionEventKind.Contact, 7L), (contact.Kind, contact.OtherId));
        // Pushing on keeps touching it (the bridge rate-limits the signals).
        events = Step(ref s, Frame(127, 0), Run, view);
        Assert.Equal(C5 + 20, s.X);
        Assert.Equal(MotionEventKind.Contact, Assert.Single(events).Kind);
    }

    [Fact]
    public void AnOffCentreContactSlidesAroundTheOtherCapsule()
    {
        var view = new GridView();
        view.Capsules.Add(new CapsuleObstacle(7, C5 + 100, C5 + 30, 0.175f));
        MotionState s = MotionState.AtTile(5, 5, 1);
        for (int i = 0; i < 6; i++)
        {
            Step(ref s, Frame(127, 0), Run, view);
            Assert.False(view.InCapsule(s.X, s.Y, 45));
        }
        Assert.True(s.X > C5 + 100, "the mover went around the body");
        Assert.True(s.Y < C5, "pushed off to the south side");
    }

    [Fact]
    public void MovingAwayFromATouchingCapsuleIsNoContact()
    {
        var view = new GridView();
        view.Capsules.Add(new CapsuleObstacle(7, C5 + 90, C5, 0.175f));
        MotionState s = MotionState.AtTile(5, 5, 1);
        Assert.Empty(Step(ref s, Frame(-127, 0), Run, view));
    }

    [Fact]
    public void AProneCapsuleCrawlsThroughStandingOnes()
    {
        var view = new GridView();
        view.Capsules.Add(new CapsuleObstacle(7, C5 + 110, C5, 0.175f));
        MotionState s = MotionState.AtTile(5, 5, 1);
        s.Posture = Posture.Lying;
        s.Driver = MotionDriver.Prone;
        Assert.Empty(Step(ref s, Frame(127, 0), Run, view));
        Assert.Equal(C5 + 85, s.X);
    }

    [Fact]
    public void TheMoverIsLeftOutOfItsOwnObstaclesByItsId()
    {
        var view = new GridView();
        view.Capsules.Add(new CapsuleObstacle(3, C5, C5, 0.175f)); // the mover itself, as the server's view lists it
        MotionState s = MotionState.AtTile(5, 5, 1);
        var events = new MotionEvents();
        MotionInput frame = Frame(127, 0);
        MotionParams run = Run;
        MotionStep.Step(ref s, in frame, in run, view, events, selfId: 3);
        Assert.Equal(C5 + 85, s.X);
        Assert.Equal(0, events.Count);
    }

    [Fact]
    public void ASlideMovesAnEqualShareEachTickRaisesNoEventAndIgnoresTheWish()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        MotionStep.Displace(ref s, 6, 5, 1, glideTicks: 4);
        Assert.True(s.Slide.Active);
        Assert.Equal((C5, C5), (s.X, s.Y)); // nothing moves until the next step
        var xs = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            Assert.Empty(Step(ref s, Frame(0, 127, yaw: 1000), Run, view));
            xs.Add(s.X);
        }
        Assert.Equal(new[] { C5 + 64, C5 + 128, C5 + 192, C5 + 256 }, xs);
        Assert.Equal(C5, s.Y);
        Assert.False(s.Slide.Active);
        Assert.Equal(1000, s.Yaw); // the yaw follows the input even while sliding
        Step(ref s, Frame(0, 127), Run, view);
        Assert.Equal(C5 + 85, s.Y); // the wish moves it again
    }

    [Fact]
    public void ADiagonalNeighbourSlidesAFartherTileSnapsAndALevelAwayKeepsItsPlace()
    {
        MotionState d = MotionState.AtTile(5, 5, 1);
        MotionStep.Displace(ref d, 6, 6, 1, 3);
        Assert.Equal(new Slide(6 * 256, 6 * 256, 3), d.Slide);

        MotionState far = MotionState.AtTile(5, 5, 1);
        far.X += 20;
        MotionStep.Displace(ref far, 9, 2, 1, 3);
        Assert.Equal((9 * 256, 2 * 256, 1), (far.X, far.Y, far.Z));
        Assert.False(far.Slide.Active);

        // DV370: a level up onto the next tile (tg's stairs) keeps the capsule where it is, brought onto that tile's near edge
        MotionState up = MotionState.AtTile(5, 5, 1);
        MotionStep.Displace(ref up, 5, 6, 2, 3);
        Assert.Equal((5 * 256, 6 * 256 - 128, 2), (up.X, up.Y, up.Z));
        Assert.False(up.Slide.Active);

        // a fall to the tile below, one level or more, keeps its place on the tile; a farther tile on another level snaps to the centre
        MotionState fall = MotionState.AtTile(5, 5, 3);
        fall.X += 40;
        MotionStep.Displace(ref fall, 5, 5, 1, 3);
        Assert.Equal((5 * 256 + 40, 5 * 256, 1), (fall.X, fall.Y, fall.Z));
        MotionState away = MotionState.AtTile(5, 5, 2);
        away.X += 40;
        MotionStep.Displace(ref away, 5, 7, 1, 3);
        Assert.Equal((5 * 256, 7 * 256, 1), (away.X, away.Y, away.Z));

        MotionState zero = MotionState.AtTile(5, 5, 1);
        MotionStep.Displace(ref zero, 6, 5, 1, 0);
        Assert.Equal((6 * 256, C5), (zero.X, zero.Y));

        MotionState same = MotionState.AtTile(5, 5, 1);
        same.X += 30;
        MotionStep.Displace(ref same, 5, 5, 1, 4);
        Assert.Equal(C5 + 30, same.X);
        Assert.False(same.Slide.Active);
    }

    [Fact]
    public void ASecondShoveDuringASlideGoesOnFromWhereTheCapsuleIs()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        MotionStep.Displace(ref s, 6, 5, 1, 4);
        Step(ref s, Frame(0, 0), Run, view);
        Step(ref s, Frame(0, 0), Run, view);
        MotionStep.Displace(ref s, 7, 5, 1, 4); // a neighbour of the slide's target
        Assert.Equal(new Slide(7 * 256, C5, 4), s.Slide);
        for (int i = 0; i < 4; i++) Step(ref s, Frame(0, 0), Run, view);
        Assert.Equal(7 * 256, s.X);
    }

    [Fact]
    public void ClampToTilePutsTheCentreBackInsideAndEndsASlide()
    {
        MotionState s = MotionState.AtTile(6, 5, 1);
        s.X = 6 * 256 - 120; // just inside tile 6 near its west face, as after a crossing
        s.Slide = new Slide(9, 9, 2);
        MotionStep.ClampToTile(ref s, 5, 5, MotionMath.DefaultRadius);
        Assert.Equal(1408 - 1 - 45, s.X); // tile 5's east face is excluded from it
        Assert.Equal(5, s.TileX);
        Assert.False(s.Slide.Active);
        MotionState t = MotionState.AtTile(5, 5, 1);
        MotionStep.ClampToTile(ref t, 5, 5, MotionMath.DefaultRadius);
        Assert.Equal((C5, C5), (t.X, t.Y)); // already inside: unchanged
    }

    [Fact]
    public void ThrownIsReservedAndMovesNothing()
    {
        var view = new GridView();
        MotionState s = MotionState.AtTile(5, 5, 1);
        s.Driver = MotionDriver.Thrown;
        Assert.Empty(Step(ref s, Frame(127, 0), Run, view));
        Assert.Equal(C5, s.X); // (ZeroG moves since DV69: ZeroGMotionTests)
    }

    [Fact]
    public void TheStepIsAPureFunctionOfItsInputs()
    {
        var view = new GridView().Solid(7, 5).Solid(5, 7);
        view.Capsules.Add(new CapsuleObstacle(9, C5 + 300, C5 + 200, 0.175f));
        var rng = new Random(5);
        MotionState a = MotionState.AtTile(5, 5, 1);
        for (int i = 0; i < 200; i++)
        {
            MotionInput frame = Frame((sbyte)rng.Next(-128, 128), (sbyte)rng.Next(-128, 128), (uint)i, (ushort)rng.Next(65536));
            MotionState copy = a;
            List<MotionEvent> e1 = Step(ref a, frame, Run, view);
            List<MotionEvent> e2 = Step(ref copy, frame, Run, view);
            Assert.Equal(a, copy);
            Assert.Equal(e1, e2);
        }
    }
}
