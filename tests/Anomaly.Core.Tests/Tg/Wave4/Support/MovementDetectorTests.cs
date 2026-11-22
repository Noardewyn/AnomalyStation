using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Support;

/// <summary>
/// /datum/movement_detector (code/datums/movement_detector.dm) across container nesting: move-chain.md section 11, T12.
/// It listens to COMSIG_MOVABLE_MOVED on the tracked movable and every movable it is inside, and keeps that chain current
/// as things move in and out of containers. Movement uses <see cref="SupportMoves"/>: package A's real Move and forceMove.
/// </summary>
public class MovementDetectorTests
{
    private sealed class Scene
    {
        public turf A, B;
        public obj_test_support_box outer, inner;
        public obj_test_support_listener item;
        public datum_movement_detector detector;
    }

    // An item in an inner box in an outer box on turf A, tracked by a detector whose callback records its arguments.
    private static Scene Build()
    {
        var s = new Scene { A = locate(1, 1, 1), B = locate(2, 1, 1) };
        s.outer = Dm.@new<obj_test_support_box>(s.A);
        s.inner = Dm.@new<obj_test_support_box>(s.outer);
        s.item = Dm.@new<obj_test_support_listener>(s.inner);
        var recorder = Dm.@new<datum_test_support_recorder>();
        var callback = Dm.@new<datum_callback>(recorder, nameof(datum_test_support_recorder.on_tracked_moved));
        s.detector = Dm.@new<datum_movement_detector>(s.item, callback);
        return s;
    }

    private static string Line(object tracked, object mover, object oldloc, object dir)
        => "moved tracked=" + SupportTrace.D(tracked) + " mover=" + SupportTrace.D(mover) + " oldloc=" + SupportTrace.D(oldloc) + " dir=" + SupportTrace.D(dir);

    private static bool Hooked(datum target) => Dm.truthy(target._listen_lookup?[COMSIG_MOVABLE_MOVED]);

    [Fact]
    public void TrackingHooksTheItemAndEveryMovableAroundItButNotTheTurf()
    {
        using var w = new SupportWorld();
        Scene s = Build();
        Assert.Same(s.item, s.detector.tracked);
        Assert.True(Hooked(s.item) && Hooked(s.inner) && Hooked(s.outer));
        Assert.False(Hooked(s.A));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void MovingTheOuterContainerReportsItWithTheTrackedItem()
    {
        using var w = new SupportWorld();
        Scene s = Build();
        SupportMoves.Move(s.outer, s.B, EAST);
        Assert.Equal(new[] { Line(s.item, s.outer, s.A, EAST) }, w.Trace);
        Assert.True(Hooked(s.inner) && Hooked(s.outer)); // a turf oldloc unhooks nothing
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void LeavingAContainerUnhooksTheOldChain()
    {
        using var w = new SupportWorld();
        Scene s = Build();

        // The inner box leaves the outer one onto the turf: the old chain (the outer box) is unhooked.
        SupportMoves.ForceMove(s.inner, s.A);
        Assert.Equal(new[] { Line(s.item, s.inner, s.outer, NONE) }, w.Trace);
        Assert.False(Hooked(s.outer));
        Assert.True(Hooked(s.inner));
        SupportMoves.Move(s.outer, s.B, EAST);
        Assert.Single(w.Trace); // the outer box no longer carries the item

        // The item leaves the inner box: tracked.loc is its turf, so nothing is hooked again.
        SupportMoves.ForceMove(s.item, s.A);
        Assert.Equal(Line(s.item, s.item, s.inner, NONE), w.Trace[^1]);
        Assert.False(Hooked(s.inner));
        Assert.True(Hooked(s.item));
        SupportMoves.Move(s.inner, s.B, EAST);
        Assert.Equal(2, w.Trace.Count);
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void EnteringAContainerHooksTheNewChain()
    {
        using var w = new SupportWorld();
        Scene s = Build();
        SupportMoves.ForceMove(s.item, s.A);
        var crate = Dm.@new<obj_test_support_box>(s.B);
        var shell = Dm.@new<obj_test_support_box>(s.B);
        SupportMoves.ForceMove(crate, shell);
        w.Trace.Clear();

        // From a turf into a crate in a shell: the turf oldloc unhooks nothing, the new chain (crate, shell) is hooked.
        SupportMoves.ForceMove(s.item, crate);
        Assert.True(Hooked(crate) && Hooked(shell));
        SupportMoves.Move(shell, s.A, WEST);
        Assert.Equal(new[] { Line(s.item, s.item, s.A, NONE), Line(s.item, shell, s.B, WEST) }, w.Trace);
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void DestroyingTheDetectorUnhooksEverything()
    {
        using var w = new SupportWorld();
        Scene s = Build();
        qdel(s.detector);
        Assert.Null(s.detector.tracked);
        Assert.Null(s.detector.listener);
        Assert.False(Hooked(s.item) || Hooked(s.inner) || Hooked(s.outer));
        SupportMoves.Move(s.outer, s.B, EAST);
        Assert.Empty(w.Trace);
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void ADetectorMadeWithoutATargetTracksNothing()
    {
        using var w = new SupportWorld();
        var detector = Dm.@new<datum_movement_detector>();
        Assert.Null(detector.tracked);
        detector.untrack(); // no tracked movable: returns at once
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void TrackingAgainMovesTheHooksToTheNewTarget()
    {
        using var w = new SupportWorld();
        Scene s = Build();
        var other = Dm.@new<obj_test_support_listener>(s.B);
        s.detector.track(other, s.detector.listener);
        Assert.False(Hooked(s.item) || Hooked(s.inner) || Hooked(s.outer));
        Assert.True(Hooked(other));
        SupportMoves.Move(other, s.A, WEST);
        Assert.Equal(new[] { Line(other, other, s.B, WEST) }, w.Trace);
        Assert.Empty(w.Runtimes);
    }
}
