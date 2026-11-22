using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Support;

/// <summary>
/// The connect_loc element (code/datums/elements/connect_loc.dm): it registers its connections on the listener's loc and
/// moves them along on COMSIG_MOVABLE_MOVED. Movement uses <see cref="SupportMoves"/>: package A's real Move and forceMove.
/// </summary>
public class ConnectLocTests
{
    private const string MOCK = "test_support_mock";

    private static DmList Connections() => Dm.list(Dm.kv(MOCK, nameof(obj_test_support_listener.on_mock)));

    private static void Fire(atom target)
    {
        if (Dm.truthy(target._listen_lookup?[MOCK]))
            target._SendSignal(MOCK, Dm.list(target));
    }

    private static obj_test_support_listener Listener(atom loc, DmList connections)
    {
        var listener = Dm.@new<obj_test_support_listener>(loc);
        Waiting.NoSleep(listener.AddElement(typeof(datum_element_connect_loc), connections));
        return listener;
    }

    [Fact]
    public void TheConnectionsFollowTheListenerFromTurfToTurf()
    {
        using var w = new SupportWorld();
        turf a = locate(1, 1, 1), b = locate(2, 1, 1);
        var listener = Listener(a, Connections());
        Fire(a);
        Assert.True(Dm.eq(listener.times_called, 1));

        SupportMoves.Move(listener, b, EAST);
        Assert.Null(a._listen_lookup); // unregister_signals(listener, old_loc) left nothing on the old turf
        Fire(a);
        Assert.True(Dm.eq(listener.times_called, 1));
        Fire(b);
        Assert.True(Dm.eq(listener.times_called, 2));
        Assert.Equal("mock " + SupportTrace.D(listener) + " from " + SupportTrace.D(b), w.Trace[^1]);
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void ItHooksTheLocNotTheTurfSoAListenerInABoxListensToTheBox()
    {
        using var w = new SupportWorld();
        turf a = locate(1, 1, 1);
        var box = Dm.@new<obj_test_support_box>(a);
        var listener = Listener(a, Connections());

        SupportMoves.ForceMove(listener, box);
        Fire(a);
        Assert.Null(listener.times_called);
        Fire(box);
        Assert.True(Dm.eq(listener.times_called, 1));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void InNullspaceNothingIsHookedAndBackOnATurfItIsAgain()
    {
        using var w = new SupportWorld();
        turf a = locate(1, 1, 1), c = locate(3, 3, 1);
        var listener = Listener(a, Connections());

        SupportMoves.ForceMove(listener, null);  // update_signals: QDELETED(listener_loc) for a null loc returns
        Assert.Null(a._listen_lookup);
        Assert.Equal(0f, length(listener._signal_procs)); // UnregisterSignal drops the turf and keeps the empty list (signals.dm:104)

        SupportMoves.ForceMove(listener, c);
        Fire(c);
        Assert.True(Dm.eq(listener.times_called, 1));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void ListenersWithTheSameConnectionsShareOneBespokeElementAndBothHear()
    {
        using var w = new SupportWorld();
        turf a = locate(1, 1, 1);
        DmList connections = Connections();
        var first = Listener(a, connections);
        var second = Listener(a, connections);
        Assert.Equal(1, SSdcs.elements_by_type.Count); // the id hashes REF(connections) (argument_hash_start_idx = 2)

        Fire(a);
        Assert.True(Dm.eq(first.times_called, 1));
        Assert.True(Dm.eq(second.times_called, 1));

        Listener(a, Connections()); // another list: another element
        Assert.Equal(2, SSdcs.elements_by_type.Count);
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void RemovingTheElementUnhooksTheLocAndTheMovedSignal()
    {
        using var w = new SupportWorld();
        turf a = locate(1, 1, 1), b = locate(2, 1, 1);
        DmList connections = Connections();
        var listener = Listener(a, connections);
        Assert.NotNull(listener._listen_lookup?[COMSIG_MOVABLE_MOVED]);

        listener.RemoveElement(typeof(datum_element_connect_loc), connections);
        Assert.Null(a._listen_lookup);
        Assert.Null(listener._listen_lookup); // Detach unregistered COMSIG_MOVABLE_MOVED

        SupportMoves.Move(listener, b, EAST);
        Fire(b);
        Assert.Null(listener.times_called);
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void ANonMovableTargetIsIncompatible()
    {
        using var w = new SupportWorld();
        turf a = locate(1, 1, 1);
        var e = Assert.ThrowsAny<Exception>(() => Waiting.NoSleep(a.AddElement(typeof(datum_element_connect_loc), Connections())));
        Assert.Contains("Incompatible element", e.Message); // _AddElement's CRASH (_element.dm:69)
        Assert.Null(a._listen_lookup);
    }
}
