using Anomaly.Core.Motion;
using static Anomaly.Core.Tests.Motion.MotionTest;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// M4c slice 7 (D146), DV69 (D160): the weightless capsule. Its driver moves by the wish tg's gate allows and otherwise by tg's drift, a
/// drift into a wall presses its face, a chain of tg's slides never jumps, a move of two tiles in one tick slides when the bridge allows
/// two, and the predictor predicts a drift as any motion.
/// </summary>
public class ZeroGMotionTests
{
    private const int C5 = 5 * 256;

    private static MotionState Weightless(int x, int y)
    {
        MotionState s = MotionState.AtTile(x, y, 1);
        s.Driver = MotionDriver.ZeroG;
        return s;
    }

    [Fact]
    public void A_weightless_capsule_moves_by_the_wish_the_gate_allows_else_by_its_drift()
    {
        // DV69: the wish as on a floor while tg's gate lets it (CanMove), else the drift; a slide still plays first
        var view = new GridView();
        MotionState s = Weightless(5, 5);
        MotionParams pushOff = Run;
        Step(ref s, Frame(127, 0), pushOff, view);
        Assert.Equal(C5 + MotionMath.Quantize(Run.TilesPerTick), s.X); // a push-off or a jetpack: the run's speed
        MotionParams drifting = Run with { CanMove = false, DriftX = -26, DriftY = 13 };
        int x = s.X, y = s.Y;
        var events = new MotionEvents();
        MotionStep.Step(ref s, Frame(127, 0), drifting, view, events, 0);
        Assert.Equal((x - 26, y + 13), (s.X, s.Y)); // the gate refuses the key: the drift moves it
        Assert.True(events.Drifting);
        // no key and the gate allowing: still the drift (a key moves it, not the gate)
        x = s.X;
        MotionStep.Step(ref s, Frame(0, 0), drifting with { CanMove = true }, view, events, 0);
        Assert.Equal(x - 26, s.X);
        // no drift, no key: it floats still
        x = s.X;
        Step(ref s, Frame(0, 0), Run, view);
        Assert.Equal(x, s.X);
        // a slide (a shove tg made: two tiles over 3 ticks each) plays before anything
        MotionStep.Displace(ref s, 7, 5, 1, 3, 2);
        for (int i = 0; i < 6; i++)
            Step(ref s, Frame(0, 127), drifting, view);
        Assert.Equal((7 * 256, C5), (s.X, s.Y));
        Assert.Equal(MotionDriver.ZeroG, s.Driver);
    }

    [Fact]
    public void A_drift_into_a_wall_stops_against_it_and_presses_the_face()
    {
        // DV69: the bridge takes the press for the drift loop's failed move (a wall ends tg's drift)
        var view = new GridView().Solid(4, 5);
        MotionState s = Weightless(5, 5);
        MotionParams drifting = Run with { CanMove = false, DriftX = -40 };
        bool pressed = false;
        for (int i = 0; i < 12; i++)
        {
            var events = new MotionEvents();
            MotionStep.Step(ref s, Frame(0, 0), drifting, view, events, 0);
            for (int e = 0; e < events.Count; e++)
                pressed |= events[e].Kind == MotionEventKind.Pressing && events[e].Dir == MotionDir.West;
        }
        Assert.True(pressed, "the drift pressed the wall's face");
        Assert.Equal(5, s.TileX);
    }

    [Fact]
    public void A_chain_of_steps_is_a_chain_of_slides_that_never_jumps()
    {
        // tg's CO2 jetpack with the stabilizer: a step every 2.4 ticks (3, 2, 3, 2 ...), each slid over ceil(32 / 13.33) = 3 ticks.
        var view = new GridView();
        MotionState s = Weightless(5, 5);
        int[] interval = { 3, 2 };
        int tile = 5, last = s.X, ticks = 0;
        for (int step = 0; step < 8; step++)
        {
            MotionStep.Displace(ref s, ++tile, 5, 1, 3, 2);
            Assert.Equal(tile * 256, s.Slide.TargetX); // the invariant: the slide's target is loc
            for (int t = 0; t < interval[step % 2]; t++, ticks++)
            {
                Step(ref s, Frame(0, 0), Run, view);
                int moved = s.X - last;
                Assert.InRange(moved, 0, 128); // never more than half a tile in a tick: no snap, no jump back
                last = s.X;
            }
        }
        for (int t = 0; t < 3; t++)
            Step(ref s, Frame(0, 0), Run, view);
        Assert.Equal((tile * 256, C5), (s.X, s.Y));
        Assert.Equal(13 * 256, s.X);
    }

    [Fact]
    public void Two_tiles_in_one_tick_slide_when_the_bridge_allows_two_and_snap_otherwise()
    {
        MotionState two = Weightless(5, 5);
        MotionStep.Displace(ref two, 7, 5, 1, 3, 2);
        Assert.Equal(new Slide(7 * 256, C5, 6), two.Slide); // the glide time of each tile
        Assert.Equal(C5, two.X);

        MotionState diagonal = Weightless(5, 5);
        MotionStep.Displace(ref diagonal, 6, 5, 1, 3, 2);
        MotionStep.Displace(ref diagonal, 7, 7, 1, 3, 2); // counted from the tile it slides to: two tiles
        Assert.Equal(new Slide(7 * 256, 7 * 256, 6), diagonal.Slide);

        MotionState grounded = MotionState.AtTile(5, 5, 1);
        MotionStep.Displace(ref grounded, 7, 5, 1, 3); // a walking capsule keeps the one-tile rule: a snap
        Assert.Equal((7 * 256, C5), (grounded.X, grounded.Y));
        Assert.False(grounded.Slide.Active);

        MotionState three = Weightless(5, 5);
        MotionStep.Displace(ref three, 8, 5, 1, 3, 2);
        Assert.Equal(8 * 256, three.X); // three tiles are a teleport
        Assert.False(three.Slide.Active);
    }

    [Fact]
    public void The_predictor_predicts_a_drifting_capsule_and_a_matching_server_state_is_no_correction()
    {
        // DV69: a weightless capsule is predicted as any other, its drift included
        var view = new GridView();
        MotionParams drifting = Run with { CanMove = false, DriftX = 26 };
        MotionState start = Weightless(5, 5);
        var predictor = new MotionPredictor(start);
        for (int i = 0; i < 3; i++)
            predictor.Push(0, 0, 0, MotionInputFlags.None, drifting, view);
        Assert.Equal(C5 + 3 * 26, predictor.Predicted.X);
        MotionState auth = start;
        MotionStep.Step(ref auth, Frame(0, 0), drifting, view, null);
        Reconciliation r = predictor.Reconcile(auth, 1, drifting, view);
        Assert.Equal(CorrectionKind.None, r.Kind);
        Assert.Equal(C5 + 3 * 26, predictor.Predicted.X);
        Assert.Equal(0L, predictor.Counters.ServerDriven);
    }

    [Fact]
    public void Blend_shows_the_prediction_from_where_it_was_drawn_and_decays_like_a_correction()
    {
        var predictor = new MotionPredictor(MotionState.AtTile(5, 5, 1));
        predictor.Blend(-0.4f, 0f);
        Assert.Equal((-0.4f, 0f), predictor.VisualOffset);
        predictor.Decay(0.05f);
        Assert.Equal(-0.2f, predictor.VisualOffset.X, 4);
        predictor.Decay(0.06f);
        Assert.Equal((0f, 0f), predictor.VisualOffset);
        predictor.Blend(1.5f, 0f); // more than a tile: the view snaps
        Assert.Equal((0f, 0f), predictor.VisualOffset);
        Assert.Equal(0, predictor.Counters.Corrections);
    }
}
