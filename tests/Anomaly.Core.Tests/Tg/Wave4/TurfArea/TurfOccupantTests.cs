using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// What a turf does to the movables already on it: /turf/initialize_occupant (turf.dm:198-201), the /turf/open overrides
/// (_open.dm:226-274, 276-279) and the signal tables a replaced turf hands to its successor (turf.dm:119-122, 241-243).
/// </summary>
public class TurfOccupantTests
{
    private static atom_movable Named(string name, atom at)
    {
        var sitter = Dm.@new<obj_test_turfarea_sitter>(at);
        sitter.name = name;
        return sitter;
    }

    private static datum_test_turfarea_listener Listener(string label)
    {
        var listener = Dm.@new<datum_test_turfarea_listener>();
        listener.label = label;
        return listener;
    }

    private static List<string> Trace(TurfAreaWorld t, string prefix) => TurfAreaTrace.Of(t.World).Lines.Where(l => l.StartsWith(prefix)).ToList();

    // ---- initialize_occupant ----

    [Fact]
    public void InitializeTellsEveryOccupantInContentsOrderAndAnnouncesNoMovement()
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var a = Named("a", floor);
            var b = Named("b", floor);
            var c = Named("c", floor);
            var init = Listener("turf_initializing");
            var moved = Listener("moved");
            foreach (var m in new[] { a, b, c })
            {
                init.RegisterSignal(m, COMSIG_MOVABLE_TURF_INITIALIZING, nameof(datum_test_turfarea_listener.on_any));
                moved.RegisterSignal(m, COMSIG_ATOM_ENTERING, nameof(datum_test_turfarea_listener.on_any));
                moved.RegisterSignal(m, COMSIG_MOVABLE_MOVED, nameof(datum_test_turfarea_listener.on_any));
                moved.RegisterSignal(m, COMSIG_ATOM_EXITING, nameof(datum_test_turfarea_listener.on_any));
            }
            moved.RegisterSignal(floor, COMSIG_ATOM_ENTERED, nameof(datum_test_turfarea_listener.on_any));
            moved.RegisterSignal(floor, COMSIG_ATOM_EXITED, nameof(datum_test_turfarea_listener.on_any));

            floor.Initialize(TRUE);

            // COMSIG_MOVABLE_TURF_INITIALIZING (source: the occupant; argument: the turf), once each, in contents order
            Assert.Equal(new[]
            {
                "turf_initializing a(turf(1,1,1),null,null,null)",
                "turf_initializing b(turf(1,1,1),null,null,null)",
                "turf_initializing c(turf(1,1,1),null,null,null)",
            }, Trace(t, "turf_initializing"));
            // "instead of Entered": no Entered, Entering, Exited, Moved
            Assert.Empty(Trace(t, "moved"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ATurfThatReplacesAnotherInitializesTheOccupantsItTookOver()
    {
        // new /turf/T(old) moves old's movables onto the new turf silently (move-chain.md E3), then /atom/New initializes it
        // at once (SSatoms is up) and /turf/Initialize walks the contents it inherited (turf.dm:171-172)
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            TurfAreaWorld.InitializeSSatoms();
            turf old = locate(2, 1, 1);
            var a = Named("a", old);
            var b = Named("b", old);
            var init = Listener("turf_initializing");
            var moved = Listener("moved");
            foreach (var m in new[] { a, b })
            {
                init.RegisterSignal(m, COMSIG_MOVABLE_TURF_INITIALIZING, nameof(datum_test_turfarea_listener.on_any));
                moved.RegisterSignal(m, COMSIG_ATOM_ENTERING, nameof(datum_test_turfarea_listener.on_any));
                moved.RegisterSignal(m, COMSIG_ATOM_EXITING, nameof(datum_test_turfarea_listener.on_any));
            }

            var made = Dm.@new<turf_closed_indestructible>(old);

            Assert.Same(made, a.loc);
            Assert.Same(made, b.loc);
            Assert.Equal(new[]
            {
                "turf_initializing a(turf(2,1,1),null,null,null)",
                "turf_initializing b(turf(2,1,1),null,null,null)",
            }, Trace(t, "turf_initializing"));
            Assert.Empty(Trace(t, "moved"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirkOccupantsMovedAwayByAHandlerAreStillInitializedBecauseContentsIsIteratedFromACopy()
    {
        var t = new TurfAreaWorld(2, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1), elsewhere = locate(2, 1, 1);
            var a = Named("a", floor);
            var b = Named("b", floor);
            var init = Listener("turf_initializing");
            init.RegisterSignal(a, COMSIG_MOVABLE_TURF_INITIALIZING, nameof(datum_test_turfarea_listener.on_any));
            init.RegisterSignal(b, COMSIG_MOVABLE_TURF_INITIALIZING, nameof(datum_test_turfarea_listener.on_any));
            // a's handler takes b off the turf before b's turn (turf.dm:171: `for(var/atom/movable/content as anything in src)`)
            var mover = Listener("mover");
            mover.victim = b;
            mover.destination = elsewhere;
            mover.RegisterSignal(a, COMSIG_MOVABLE_TURF_INITIALIZING, nameof(datum_test_turfarea_listener.on_move_victim));

            floor.Initialize(TRUE);

            Assert.Same(elsewhere, b.loc);
            Assert.Equal(new[]
            {
                "turf_initializing a(turf(1,1,1),null,null,null)",
                "turf_initializing b(turf(1,1,1),null,null,null)",
            }, Trace(t, "turf_initializing"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InitializeOccupantWithNoListenerSendsNothingAndDoesNotFail()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var a = Named("a", floor);
            Waiting.NoSleep(floor.initialize_occupant(a));
            Assert.Same(floor, a.loc);
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- /turf/open: transfer_occupant (_open.dm:241-274) ----

    [Fact]
    public void OpenTurfInitializeOccupantTellsBeforeItTransfers()
    {
        var t = new TurfAreaWorld(4, 4, 2, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            source.destination_x = 2;
            source.destination_y = 2;
            source.destination_z = 2;
            var traveller = Dm.@new<obj_test_turfarea_transit>(source);
            traveller.name = "traveller";
            var init = Listener("turf_initializing");
            init.RegisterSignal(traveller, COMSIG_MOVABLE_TURF_INITIALIZING, nameof(datum_test_turfarea_listener.on_any));

            source.Initialize(TRUE);

            // the base class first (the signal), then transfer_occupant (the z move); no ENTERED in between
            Assert.Equal(new[]
            {
                "turf_initializing traveller(turf(1,1,1),null,null,null)",
                "zMove traveller dir=null -> turf(2,2,2) flags=" + Dm.str(ZMOVE_ALLOW_BUCKLED),
            }, TurfAreaTrace.Of(t.World).Lines.Where(l => !l.StartsWith("Initialize ")).ToList());
            Assert.Same(locate(2, 2, 2), traveller.loc);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TransferOccupantNeedsAllThreeDestinationCoordinates()
    {
        var t = new TurfAreaWorld(4, 4, 2);
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            var traveller = Dm.@new<obj_test_turfarea_transit>(source);
            // _open.dm:249: a missing or zero coordinate is "no connection"
            foreach (var (x, y, z) in new (object, object, object)[] { (null, 2, 2), (2, null, 2), (2, 2, null), (0, 2, 2), (2, 0, 2) })
            {
                source.destination_x = x;
                source.destination_y = y;
                source.destination_z = z;
                Waiting.NoSleep(source.transfer_occupant(traveller));
            }
            Assert.Same(source, traveller.loc);
            Assert.DoesNotContain(TurfAreaTrace.Of(t.World).Lines, l => l.StartsWith("zMove"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TransferOccupantIgnoresNullAnOccupantElsewhereAPulledOneAndOneThatIsZMoving()
    {
        var t = new TurfAreaWorld(4, 4, 2);
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            source.destination_x = 2;
            source.destination_y = 2;
            source.destination_z = 2;
            var elsewhere = Dm.@new<obj_test_turfarea_transit>(locate(3, 3, 1));
            var pulled = Dm.@new<obj_test_turfarea_transit>(source);
            pulled.pulledby = Dm.@new<mob_test_turfarea_mob>(locate(4, 4, 1));
            var falling = Dm.@new<obj_test_turfarea_transit>(source);
            falling.currently_z_moving = CURRENTLY_Z_MOVING_GENERIC;

            Waiting.NoSleep(source.transfer_occupant(null));
            Waiting.NoSleep(source.transfer_occupant(elsewhere));   // _open.dm:243: occupant.loc != src
            Waiting.NoSleep(source.transfer_occupant(pulled));
            Waiting.NoSleep(source.transfer_occupant(falling));

            Assert.DoesNotContain(TurfAreaTrace.Of(t.World).Lines, l => l.StartsWith("zMove"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TransferOccupantWaitsOutTheMapload()
    {
        var t = new TurfAreaWorld(4, 4, 2);
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            source.destination_x = 2;
            source.destination_y = 2;
            source.destination_z = 2;
            var traveller = Dm.@new<obj_test_turfarea_transit>(source);
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            Waiting.NoSleep(source.transfer_occupant(traveller));   // _open.dm:252-253
            Assert.Same(source, traveller.loc);
            SSatoms.initialized = INITIALIZATION_INNEW_REGULAR;
            Waiting.NoSleep(source.transfer_occupant(traveller));
            Assert.Same(locate(2, 2, 2), traveller.loc);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TransferOccupantLooksPastDenseDestinationsTowardsTheCenterOfTheMap()
    {
        var t = new TurfAreaWorld(6, 6, 2);
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            Dm.@new<turf_closed_indestructible>(locate(2, 2, 2));
            Dm.@new<turf_closed_indestructible>(locate(3, 3, 2));
            source.destination_x = 2;
            source.destination_y = 2;
            source.destination_z = 2;
            var traveller = Dm.@new<obj_test_turfarea_transit>(source);

            Waiting.NoSleep(source.transfer_occupant(traveller));

            // _open.dm:258-270: below 128 both coordinates go up by one for every dense turf met
            Assert.Same(locate(4, 4, 2), traveller.loc);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TransferOccupantMovesTheChainOfPulledMovablesToTheTileBehindThePuller()
    {
        var t = new TurfAreaWorld(6, 6, 2);
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            source.destination_x = 4;
            source.destination_y = 3;
            source.destination_z = 2;
            var puller = Dm.@new<mob_test_turfarea_mob>(source);
            puller.name = "puller";
            puller.dir = EAST;
            var first = Dm.@new<obj_test_turfarea_transit>(locate(1, 2, 1));
            first.name = "first";
            var second = Dm.@new<obj_test_turfarea_transit>(locate(1, 3, 1));
            second.name = "second";
            puller.pulling = first;
            first.pulledby = puller;
            // the second link is pulled by a mob too (pulledby is typed /mob): a mob that pulls the first one
            var middle = Dm.@new<mob_test_turfarea_mob>(locate(1, 4, 1));
            middle.name = "middle";
            middle.dir = NORTH;

            Waiting.NoSleep(source.transfer_occupant(puller));

            Assert.Same(locate(4, 3, 2), puller.loc);
            // _open.dm:272-277: the tile behind the puller (opposite of its dir) on its new level, or its own tile
            Assert.Same(locate(3, 3, 2), first.loc);
            Assert.Equal(new[]
            {
                "zMove puller dir=null -> turf(4,3,2) flags=" + Dm.str(ZMOVE_ALLOW_BUCKLED),
                "zMove first dir=null -> turf(3,3,2) flags=" + Dm.str(ZMOVE_ALLOW_BUCKLED),
            }, Trace(t, "zMove"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TransferOccupantFallsBackToThePullersTileWhenTheTileBehindIsOffTheMap()
    {
        var t = new TurfAreaWorld(6, 6, 2);
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            source.destination_x = 1;
            source.destination_y = 3;
            source.destination_z = 2;
            var puller = Dm.@new<mob_test_turfarea_mob>(source);
            puller.name = "puller";
            puller.dir = EAST;   // behind it is x = 0: off the map
            var first = Dm.@new<obj_test_turfarea_transit>(locate(2, 1, 1));
            first.name = "first";
            puller.pulling = first;
            first.pulledby = puller;

            Waiting.NoSleep(source.transfer_occupant(puller));

            // `get_step(...) || current_pull.pulledby.loc` (_open.dm:273)
            Assert.Same(locate(1, 3, 2), first.loc);
            Assert.Empty(t.Runtimes);
        }
    }

    // ---- /turf/open/Entered and Exited ----

    [Fact]
    public void OpenEnteredSendsTheAtomSignalsFirstAndThenTransfersAnArrivalThatIsStillHere()
    {
        var t = new TurfAreaWorld(4, 4, 2);
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            source.destination_x = 2;
            source.destination_y = 2;
            source.destination_z = 2;
            var traveller = Dm.@new<obj_test_turfarea_transit>(source);
            traveller.name = "traveller";
            var watch = Listener("entered");
            watch.RegisterSignal(source, COMSIG_ATOM_ENTERED, nameof(datum_test_turfarea_listener.on_any));
            watch.RegisterSignal(traveller, COMSIG_ATOM_ENTERING, nameof(datum_test_turfarea_listener.on_any));
            var from = locate(3, 1, 1);

            Waiting.NoSleep(source.Entered(traveller, from, null));

            Assert.Equal(new[]
            {
                "entered turf(1,1,1)(traveller,turf(3,1,1),null,null)",
                "entered traveller(turf(1,1,1),turf(3,1,1),null,null)",
            }, Trace(t, "entered"));
            Assert.Single(Trace(t, "zMove"));
            Assert.Same(locate(2, 2, 2), traveller.loc);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void OpenEnteredDoesNotTransferAnArrivalThatLeftBeforeTheCallReturned()
    {
        var t = new TurfAreaWorld(4, 4, 2);
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            source.destination_x = 2;
            source.destination_y = 2;
            source.destination_z = 2;
            var traveller = Dm.@new<obj_test_turfarea_transit>(locate(3, 3, 1));   // src != arrived.loc (_open.dm:228)
            var watch = Listener("entered");
            watch.RegisterSignal(source, COMSIG_ATOM_ENTERED, nameof(datum_test_turfarea_listener.on_any));

            Waiting.NoSleep(source.Entered(traveller, null, null));

            Assert.Single(Trace(t, "entered"));   // the signal is sent from the base class, before the guard
            Assert.Empty(Trace(t, "zMove"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void OpenExitedSendsTheAtomSignalsAndLeavesNoFootprintsForNonHumans()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var source = (turf_open)locate(1, 1, 1);
            var traveller = Named("traveller", source);
            var watch = Listener("exited");
            watch.RegisterSignal(source, COMSIG_ATOM_EXITED, nameof(datum_test_turfarea_listener.on_any));
            watch.RegisterSignal(traveller, COMSIG_ATOM_EXITING, nameof(datum_test_turfarea_listener.on_any));
            Waiting.NoSleep(source.Exited(traveller, EAST));
            Assert.Equal(new[]
            {
                "exited turf(1,1,1)(traveller," + Dm.str(EAST) + ",null,null)",
                "exited traveller(turf(1,1,1)," + Dm.str(EAST) + ",null,null)",
            }, Trace(t, "exited"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void OpenTurfDefaultsHaveNoTransitAndAreBuildable()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var floor = (turf_open)locate(1, 1, 1);
            Assert.True(Dm.eq(floor.CanBuildHere(), TRUE));
            Assert.False(Dm.truthy(floor.is_transition_turf()));
            floor.destination_y = 5;
            Assert.True(Dm.eq(floor.is_transition_turf(), TRUE));   // _open.dm:186: any of the three coordinates
            Assert.True(Dm.eq(floor.CanBuildHere(), TRUE));         // CanBuildHere looks only at destination_z
            floor.destination_z = 2;
            Assert.True(Dm.eq(floor.CanBuildHere(), FALSE));
            Assert.Same(floor, floor.get_dumping_location());
        }
    }

    // ---- signal tables of a replaced turf (turf.dm:119-122, :241-243) ----

    [Fact]
    public void ANewTurfWithoutInheritedTablesStartsWithNone()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var made = Dm.@new<turf_test_turfarea_floor>(locate(1, 1, 1));
            Assert.Null(made._listen_lookup);
            Assert.Null(made._signal_procs);
        }
    }

    [Fact]
    public void ATurfBuiltWithTheOldTurfsSignalTablesTakesThemOver()
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            turf old = locate(1, 1, 1);
            var watch = Listener("entered");
            watch.RegisterSignal(old, COMSIG_ATOM_ENTERED, nameof(datum_test_turfarea_listener.on_any));
            DmList listen = old._listen_lookup, procs = old._signal_procs;
            Assert.NotNull(listen);

            // ChangeTurf's `new path(src, old_listen_lookup, old_signal_procs)`: the extra arguments are the turf's own
            var made = Dm.@new<turf_closed_indestructible>(old, listen, procs);

            // the tables themselves are handed over (turf.dm:120-121), not copies
            Assert.Same(listen, made._listen_lookup);
            Assert.Same(procs, made._signal_procs);
            // R13 (scope.md section 6; ChangeTurf is M3): in BYOND the listener's reference to the old turf now names the new
            // one, so the signal would reach it. Here the listener's _signal_procs stay keyed by the replaced turf object, which
            // `made` is not, so delivery from the new turf is not tested before M3.
        }
    }

    [Fact]
    public void ClearSignalRefsDropsWhatTheTurfRegisteredOnItselfAndKeepsWhatOthersRegisteredOnIt()
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            var floor = (turf_test_turfarea_floor)locate(1, 1, 1);
            floor.RegisterSignal(floor, COMSIG_ATOM_ENTERED, nameof(turf_test_turfarea_floor.on_self_signal));
            var watch = Listener("external");
            watch.RegisterSignal(floor, COMSIG_ATOM_EXITED, nameof(datum_test_turfarea_listener.on_any));

            floor._clear_signal_refs();

            // tg's comment (turf.dm:235-240): signals the turf registered on itself go, the outside world's stay
            TurfAreaSignals.Send(floor, COMSIG_ATOM_ENTERED);
            TurfAreaSignals.Send(floor, COMSIG_ATOM_EXITED);
            Assert.DoesNotContain(TurfAreaTrace.Of(t.World).Lines, l => l.StartsWith("self"));
            Assert.Single(Trace(t, "external"));
            Assert.Empty(t.Runtimes);
        }
    }
}
