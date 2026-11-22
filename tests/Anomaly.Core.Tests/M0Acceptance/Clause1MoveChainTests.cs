using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave4.Integration;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M0Acceptance;

/// <summary>
/// ROADMAP M0, "Done when", clause 1: "a headless test world with turfs and atoms moves an atom through the translated
/// <c>Move</c> chain with signals firing in tg order". Each test runs on a booted world (<see cref="BootedToyWorld"/>: Genesis,
/// <c>world/New</c>, the real Master and its ticker subsystems firing) and drives the chain the way a player does, with
/// <c>step</c> and <c>forceMove</c> in the command phase of a tick. The mover is a real <c>/mob</c> or a real <c>/obj</c>, the
/// turfs are tg's <c>/turf/open/indestructible</c> and <c>/turf/closed/indestructible</c>, the areas are
/// <c>/area/misc/testroom</c>, and every proc of the chain is tg's own: <c>step</c>, <c>Move</c>, <c>/turf/Enter</c>,
/// <c>Exit</c>, <c>Exited</c>, <c>Entered</c>, <c>/area/Exited</c>, <c>/area/Entered</c>, <c>Moved</c>, <c>doMove</c>,
/// <c>forceMove</c>, <c>Bump</c>. The only test types are the signal probes of <see cref="Toy.On"/>: a datum that records the
/// signal it hears and its arguments, with the tick, in the order the signals fire.
/// <para>
/// The expected order is written from docs/ai/specs/move-chain.md section 3 (3a for <c>step</c>, 3b and 3c for <c>Enter</c> and
/// <c>Bump</c>, 3e for <c>forceMove</c>, 3f for a diagonal step) and from the DM it cites, not from the C#. A line reads
/// "tick:label source args": the tick the probe heard it in, the probe's label and the signal's arguments after the source.
/// </para>
/// </summary>
public class Clause1MoveChainTests
{
    // ---- the scene ----

    /// <summary>A room split in two areas at x = 4 (room | annex) with a mover at (3, 3) of the given kind, named "mover".</summary>
    private sealed class Scene
    {
        public readonly BootedToyWorld W = new();
        public area Room;
        public area Annex;
        public atom_movable Mover;

        public Scene(string kind, int x = 3, int y = 3)
        {
            W.In(() =>
            {
                Annex = W.Annex(4);
                Room = W.Room.Room;
                Mover = Make(kind, "mover", x, y);
            });
        }

        public static atom_movable Make(string kind, string name, int x, int y) => kind switch
        {
            "mob" => BootedToyWorld.Mob(name, x, y),
            "obj" => BootedToyWorld.Obj(name, x, y),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "a mover is a mob or an obj"),
        };

        public long T => W.T;

        /// <summary>The lines heard, in order.</summary>
        public List<string> Heard => Toy.Trace(W.World);

        public void Forget() => Heard.Clear();
    }

    /// <summary>A probe that writes the tick, then "label source args...".</summary>
    private static void Hear(datum target, string signal, string label, int arity = 3)
        => Toy.On(target, signal, label, arity, stamp_tick: true);

    private static string[] At(long tick, params string[] lines) => lines.Select(l => tick + ":" + l).ToArray();

    // ---- step: a mob and an obj, across an area border ----

    // move-chain.md 3a: the whole successful cardinal step, in the order the engine and tg send it. The probes listen to every
    // signal of it: on the mover, on the source and destination turfs, on the two areas and on a rug (not dense) lying on the
    // destination, which is crossed and does not block.
    private static void ListenToTheWholeStep(Scene s, turf a, turf b, obj rug)
    {
        atom_movable m = s.Mover;
        Hear(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
        Hear(m, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir", 2);
        Hear(m, COMSIG_ATOM_DIR_CHANGE, "dir", 2);
        Hear(m, COMSIG_ATOM_POST_DIR_CHANGE, "post_dir", 2);
        Hear(a, COMSIG_ATOM_EXIT, "exit", 2);
        Hear(b, COMSIG_ATOM_TRIED_PASS, "tried_pass_turf", 2);
        if (rug != null)
        {
            Hear(rug, COMSIG_MOVABLE_CROSS, "cross", 1);
            Hear(m, COMSIG_MOVABLE_CROSS_OVER, "cross_over", 1);
            Hear(rug, COMSIG_ATOM_TRIED_PASS, "tried_pass_rug", 2);
        }
        Hear(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
        Hear(a, COMSIG_ATOM_EXITED, "exited", 2);
        Hear(m, COMSIG_ATOM_EXITING, "exiting", 2);
        Hear(s.Room, COMSIG_AREA_EXITED, "area_exited", 2);
        Hear(m, COMSIG_MOVABLE_EXITED_AREA, "exited_area", 2);
        Hear(b, COMSIG_ATOM_ENTERED, "entered", 3);
        Hear(m, COMSIG_ATOM_ENTERING, "entering", 3);
        Hear(s.Annex, COMSIG_AREA_ENTERED, "area_entered", 2);
        Hear(m, COMSIG_MOVABLE_MOVED, "moved", 5);
        Hear(a, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited", 2);
        Hear(b, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered", 3);
    }

    [Theory]
    [InlineData("obj")]
    [InlineData("mob")]
    public void AnAtomStepsThroughTheMoveChainAcrossAnAreaBorderWithEverySignalInTgOrder(string kind)
    {
        var s = new Scene(kind);
        object result = null;
        s.W.In(() =>
        {
            obj rug = BootedToyWorld.Obj("rug", 4, 3);
            ListenToTheWholeStep(s, locate(3, 3, 1), locate(4, 3, 1), rug);
            s.Forget();
        });
        s.W.At(0, () => result = Toy.Step(s.Mover, EAST));

        s.W.RunTo(0);

        Toy.AssertNumber(TRUE, result);
        Toy.AssertTrace(At(s.T,
            "attempted mover (4,3) 4",           // ATTEMPTED_MOVE first, before anything, with the direction asked for (3a.2)
            "pre_dir mover 2 4",                 // the mover turns BEFORE Exit and Enter (3a.5): old dir SOUTH, new dir EAST
            "dir mover 2 4",
            "post_dir mover 2 4",
            "exit (3,3) mover 4",                // loc.Exit(src, direction) (3a.6)
            "tried_pass_turf (4,3) mover 8",     // newloc.Enter: the turf's CanPass first (3b.1), border_dir = get_dir(turf, mover)
            "cross rug mover",                   // then the Cross of each occupant (3b.2): its own signal,
            "cross_over mover rug",              // the mover's,
            "tried_pass_rug rug mover 8",        // and the occupant's CanPass
            "pre_move mover (4,3)",              // PRE_MOVE after Enter (3a.8)
            "exited (3,3) mover 4",              // the point of no return is passed: oldloc.Exited (3a.10) ...
            "exiting mover (3,3) 4",
            "area_exited area:room mover 4",     // ... then the old area's Exited, because the areas differ (3a.11)
            "exited_area mover area:room 4",
            "entered (4,3) mover (3,3) null",    // newloc.Entered(src, oldloc, old_locs) (3a.12) ...
            "entering mover (4,3) (3,3) null",
            "area_entered area:annex mover area:room",   // ... then the new area's Entered (3a.13)
            "moved mover (3,3) 4 0 null 1",      // Moved(oldloc, direction, FALSE, old_locs, momentum_change = TRUE) (3a.14, 3d.2)
            "abstract_exited (3,3) mover 4",     // 3d.3: ABSTRACT_EXITED, then ABSTRACT_ENTERED
            "abstract_entered (4,3) mover (3,3) null"), s.Heard);
        s.W.In(() =>
        {
            Assert.Same(locate(4, 3, 1), s.Mover.loc);
            Assert.Same(s.Annex, s.Mover.loc.loc);
            Assert.Equal(EAST, s.Mover.dir);
            Toy.AssertNumber(EAST, s.Mover.last_move);
            Assert.Null(s.Mover.active_movement);
            Assert.Equal(0f, s.Mover.moving_diagonally);
        });
        Assert.Empty(s.W.Runtimes);
    }

    [Theory]
    [InlineData("obj")]
    [InlineData("mob")]
    public void AStepInsideOneAreaSendsTheSameSignalsWithoutTheAreaOnes(string kind)
    {
        var s = new Scene(kind, 5, 3);   // in the annex: a step east from (5,3) to (6,3) stays in it
        object result = null;
        s.W.In(() =>
        {
            Assert.Same(s.Annex, locate(6, 3, 1).loc);
            ListenToTheWholeStep(s, locate(5, 3, 1), locate(6, 3, 1), null);
            s.Forget();
        });
        s.W.At(0, () => result = Toy.Step(s.Mover, EAST));

        s.W.RunTo(0);

        Toy.AssertNumber(TRUE, result);
        Toy.AssertTrace(At(s.T,
            "attempted mover (6,3) 4",
            "pre_dir mover 2 4",
            "dir mover 2 4",
            "post_dir mover 2 4",
            "exit (5,3) mover 4",
            "tried_pass_turf (6,3) mover 8",
            "pre_move mover (6,3)",
            "exited (5,3) mover 4",
            "exiting mover (5,3) 4",
            "entered (6,3) mover (5,3) null",    // no area_exited, exited_area or area_entered: the areas are equal (3a.11, 3a.13)
            "entering mover (6,3) (5,3) null",
            "moved mover (5,3) 4 0 null 1",
            "abstract_exited (5,3) mover 4",
            "abstract_entered (6,3) mover (5,3) null"), s.Heard);
        Assert.Empty(s.W.Runtimes);
    }

    // ---- a blocked step: Bump ----

    [Theory]
    [InlineData("obj", "dense obj")]
    [InlineData("mob", "dense obj")]
    [InlineData("obj", "wall")]
    [InlineData("mob", "wall")]
    public void AStepIntoADenseObjOrAWallBumpsAfterEnterAndBeforeAnythingIsCommitted(string kind, string blocker_kind)
    {
        // move-chain.md 3a.7 and 3b: Enter fails, the mover Bumps what blocks it (the occupant, or the turf itself when the turf
        // does not let the mover pass), and Move returns FALSE with no PRE_MOVE, no Exited, no Entered, no Moved (3g).
        bool wall = blocker_kind == "wall";
        var s = new Scene(kind, wall ? 2 : 3, 3);
        object result = null;
        atom blocker = null;
        turf here = null, ahead = null;
        int ahead_x = wall ? 1 : 4;
        s.W.In(() =>
        {
            here = s.Mover.loc as turf;
            ahead = locate(ahead_x, 3, 1);
            blocker = wall ? ahead : BootedToyWorld.Obj("blocker", 4, 3, dense: true);
            Assert.IsType<turf_closed_indestructible>(wall ? ahead : locate(1, 1, 1));
            Hear(s.Mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            Hear(s.Mover, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir", 2);
            Hear(here, COMSIG_ATOM_EXIT, "exit", 2);
            Hear(ahead, COMSIG_ATOM_TRIED_PASS, "tried_pass_turf", 2);
            if (!wall)
            {
                Hear(blocker, COMSIG_MOVABLE_CROSS, "cross", 1);
                Hear(s.Mover, COMSIG_MOVABLE_CROSS_OVER, "cross_over", 1);
                Hear(blocker, COMSIG_ATOM_TRIED_PASS, "tried_pass_blocker", 2);
            }
            Hear(s.Mover, COMSIG_MOVABLE_BUMP, "bump", 1);
            Hear(blocker, COMSIG_ATOM_BUMPED, "bumped", 1);
            Hear(s.Mover, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
            Hear(here, COMSIG_ATOM_EXITED, "exited", 2);
            Hear(ahead, COMSIG_ATOM_ENTERED, "entered", 3);
            Hear(s.Mover, COMSIG_MOVABLE_MOVED, "moved", 5);
            s.Forget();
        });
        float dir = wall ? WEST : EAST;
        s.W.At(0, () => result = Toy.Step(s.Mover, dir));

        s.W.RunTo(0);

        Toy.AssertNumber(FALSE, result);
        string[] expected = wall
            ? At(s.T,
                "attempted mover (1,3) 8",
                "pre_dir mover 2 8",                // a failed move still turns the mover (3a.5)
                "exit (2,3) mover 8",
                "tried_pass_turf (1,3) mover 4",    // border_dir = get_dir(wall, mover) = EAST: the wall's CanPass says no
                "bump mover (1,3)",                 // first_bump = src, the turf itself (3b.3)
                "bumped (1,3) mover")
            : At(s.T,
                "attempted mover (4,3) 4",
                "pre_dir mover 2 4",
                "exit (3,3) mover 4",
                "tried_pass_turf (4,3) mover 8",
                "cross blocker mover",
                "cross_over mover blocker",
                "tried_pass_blocker blocker mover 8",   // the occupant's CanPass says no: density
                "bump mover blocker",                   // Bump(first_bump), still inside Enter (3b.2, 3c)
                "bumped blocker mover");                // bumped.Bumped(src) (3c)
        Toy.AssertTrace(expected, s.Heard);   // and nothing after: no pre_move, exited, entered or moved
        s.W.In(() =>
        {
            Assert.Same(here, s.Mover.loc);
            Assert.Equal(dir, s.Mover.dir);
            Toy.AssertNumber(0, s.Mover.last_move);   // def2's bail at atoms_movable.dm:831 (3g)
            Assert.Null(s.Mover.active_movement);
        });
        Assert.Empty(s.W.Runtimes);
    }

    [Fact]
    public void ABumpThatIsInterceptedStopsTheChainAtTheBumpSignalAndBumpedIsNeverCalled()
    {
        // 3c: COMSIG_MOVABLE_BUMP with COMPONENT_INTERCEPT_BUMPED returns TRUE before Bumped. The step still fails: Enter
        // already failed, and the interception only skips the bumped atom's reaction.
        var s = new Scene("mob");
        object result = null;
        s.W.In(() =>
        {
            obj blocker = BootedToyWorld.Obj("blocker", 4, 3, dense: true);
            Toy.On(s.Mover, COMSIG_MOVABLE_BUMP, "bump", 1, result: COMPONENT_INTERCEPT_BUMPED, stamp_tick: true);
            Hear(blocker, COMSIG_ATOM_BUMPED, "bumped", 1);
            s.Forget();
        });
        s.W.At(0, () => result = Toy.Step(s.Mover, EAST));

        s.W.RunTo(0);

        Toy.AssertNumber(FALSE, result);
        Toy.AssertTrace(At(s.T, "bump mover blocker"), s.Heard);
        Assert.Empty(s.W.Runtimes);
    }

    // ---- forceMove ----

    [Theory]
    [InlineData("obj")]
    [InlineData("mob")]
    public void AForceMoveAcrossAnAreaBorderSendsNoExitEnterCheckOrBumpAndMovedIsForced(string kind)
    {
        // move-chain.md 3e: doMove is a teleport. Exited and Entered on the turfs and the areas, then Moved with forced = TRUE and
        // NONE as the direction; nothing of ATTEMPTED_MOVE, Exit, Enter's CanPass and Cross, PRE_MOVE or Bump, even when the
        // destination is blocked by a dense occupant.
        var s = new Scene(kind);
        object result = null;
        s.W.In(() =>
        {
            BootedToyWorld.Obj("blocker", 5, 4, dense: true);
            turf a = locate(3, 3, 1), b = locate(5, 4, 1);
            Hear(s.Mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            Hear(a, COMSIG_ATOM_EXIT, "exit", 2);
            Hear(b, COMSIG_ATOM_TRIED_PASS, "tried_pass_turf", 2);
            Hear(s.Mover, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
            Hear(s.Mover, COMSIG_MOVABLE_BUMP, "bump", 1);
            Hear(a, COMSIG_ATOM_EXITED, "exited", 2);
            Hear(s.Mover, COMSIG_ATOM_EXITING, "exiting", 2);
            Hear(s.Room, COMSIG_AREA_EXITED, "area_exited", 2);
            Hear(b, COMSIG_ATOM_ENTERED, "entered", 3);
            Hear(s.Mover, COMSIG_ATOM_ENTERING, "entering", 3);
            Hear(s.Annex, COMSIG_AREA_ENTERED, "area_entered", 2);
            Hear(s.Mover, COMSIG_MOVABLE_MOVED, "moved", 5);
            Hear(a, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited", 2);
            Hear(b, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered", 3);
            s.Forget();
        });
        s.W.At(0, () => result = Toy.Force(s.Mover, locate(5, 4, 1)));

        s.W.RunTo(0);

        Toy.AssertNumber(TRUE, result);
        Toy.AssertTrace(At(s.T,
            "exited (3,3) mover 5",              // movement_dir = get_dir(src, dest) = NORTHEAST, read before loc changes (3e.3)
            "exiting mover (3,3) 5",
            "area_exited area:room mover 5",
            "entered (5,4) mover (3,3) null",    // destination.Entered(src, oldloc): two arguments, so old_locs is null (3e.4)
            "entering mover (5,4) (3,3) null",
            "area_entered area:annex mover area:room",
            "moved mover (3,3) 0 1 null 1",      // Moved(oldloc, NONE, TRUE, null): forced = TRUE (3e.5)
            "abstract_exited (3,3) mover 0",
            "abstract_entered (5,4) mover (3,3) null"), s.Heard);
        s.W.In(() =>
        {
            Assert.Same(locate(5, 4, 1), s.Mover.loc);
            Assert.Equal(SOUTH, s.Mover.dir);   // a teleport does not turn the mover
            Assert.Null(s.Mover.active_movement);
        });
        Assert.Empty(s.W.Runtimes);
    }

    // ---- diagonal ----

    [Fact]
    public void ADiagonalStepIsTwoCardinalStepsEachWithItsOwnChainNorthFirst()
    {
        // move-chain.md 3f: NORTHEAST is a step NORTH, then a step EAST, each running the full cardinal chain.
        var s = new Scene("mob");
        object result = null;
        s.W.In(() =>
        {
            Hear(s.Mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            Hear(s.Mover, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
            Hear(s.Mover, COMSIG_MOVABLE_MOVED, "moved", 5);
            s.Forget();
        });
        s.W.At(0, () => result = Toy.Step(s.Mover, NORTHEAST));

        s.W.RunTo(0);

        Toy.AssertNumber(TRUE, result);
        Toy.AssertTrace(At(s.T,
            "attempted mover (3,4) 1",
            "pre_move mover (3,4)",
            "moved mover (3,3) 1 0 null 1",    // RESOLVE_ACTIVE_MOVEMENT calls Moved with four arguments: momentum_change keeps its default TRUE
            "attempted mover (4,4) 4",
            "pre_move mover (4,4)",
            "moved mover (3,4) 4 0 null 1"), s.Heard);
        s.W.In(() =>
        {
            Assert.Same(locate(4, 4, 1), s.Mover.loc);
            Assert.Equal(0f, s.Mover.moving_diagonally);
        });
        Assert.Empty(s.W.Runtimes);
    }

    // ---- over time: the Master runs between the steps ----

    [Fact]
    public void AMobWalksTheRoomOneStepPerTickWhileTheMasterFiresBetweenTheStepsAndEveryChainIsWholeInItsOwnTick()
    {
        var s = new Scene("mob", 2, 3);
        var steps = new List<(long Offset, float Dir)>
        {
            (0, EAST), (1, EAST), (2, EAST),    // (2,3) -> (3,3) -> (4,3) across the border -> (5,3)
            (3, NORTH),                          // (5,3) -> (5,4)
            (4, NORTH),                          // the dense crate at (5,5): bump
            (6, WEST),                           // (5,4) -> (4,4), two ticks later (a tick with no command in between)
        };
        var results = new List<float>();
        float fired_before = 0;
        s.W.In(() =>
        {
            BootedToyWorld.Obj("crate", 5, 5, dense: true);
            Hear(s.Mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
            Hear(s.Mover, COMSIG_MOVABLE_MOVED, "moved", 5);
            Hear(s.Mover, COMSIG_MOVABLE_BUMP, "bump", 1);
            Hear(s.Annex, COMSIG_AREA_ENTERED, "area_entered", 2);
            s.Forget();
            fired_before = SStimed_actions.times_fired;
        });
        foreach ((long offset, float dir) in steps)
            s.W.At(offset, () => results.Add(Dm.num(Toy.Step(s.Mover, dir))));

        s.W.RunTo(7);

        Assert.Equal(new float[] { TRUE, TRUE, TRUE, TRUE, FALSE, TRUE }, results);
        Toy.AssertTrace(
            At(s.T, "attempted mover (3,3) 4", "moved mover (2,3) 4 0 null 1")
                .Concat(At(s.T + 1, "attempted mover (4,3) 4", "area_entered area:annex mover area:room", "moved mover (3,3) 4 0 null 1"))
                .Concat(At(s.T + 2, "attempted mover (5,3) 4", "moved mover (4,3) 4 0 null 1"))
                .Concat(At(s.T + 3, "attempted mover (5,4) 1", "moved mover (5,3) 1 0 null 1"))
                .Concat(At(s.T + 4, "attempted mover (5,5) 1", "bump mover crate"))
                .Concat(At(s.T + 6, "attempted mover (4,4) 8", "moved mover (5,4) 8 0 null 1")), s.Heard);
        s.W.In(() =>
        {
            Assert.Same(locate(4, 4, 1), s.Mover.loc);
            Assert.Equal(WEST, s.Mover.dir);
            // the Master fired in each of the eight ticks the walk took (T to T + 7), and the ticker subsystems with it
            Assert.InRange(SStimed_actions.times_fired - fired_before, 7, 8);
        });
        Assert.True(s.W.MasterIsRunning());
        Assert.Empty(s.W.Runtimes);
    }

    [Fact]
    public void AMobAndAnObjStepInOneTickAndTheirChainsRunOneAfterTheOtherInCommandOrder()
    {
        var s = new Scene("mob", 2, 2);
        atom_movable thing = null;
        s.W.In(() =>
        {
            thing = Scene.Make("obj", "thing", 2, 4);
            Hear(s.Mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted_mob", 2);
            Hear(s.Mover, COMSIG_MOVABLE_MOVED, "moved_mob", 5);
            Hear(thing, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted_obj", 2);
            Hear(thing, COMSIG_MOVABLE_MOVED, "moved_obj", 5);
            s.Forget();
        });
        s.W.At(0, () =>
        {
            Toy.Step(thing, EAST);        // the obj goes first: command order, not creation order
            Toy.Step(s.Mover, NORTH);
            Toy.Step(thing, EAST);
        });

        s.W.RunTo(0);

        Toy.AssertTrace(At(s.T,
            "attempted_obj thing (3,4) 4",
            "moved_obj thing (2,4) 4 0 null 1",
            "attempted_mob mover (2,3) 1",
            "moved_mob mover (2,2) 1 0 null 1",
            "attempted_obj thing (4,4) 4",
            "moved_obj thing (3,4) 4 0 null 1"), s.Heard);
        Assert.Empty(s.W.Runtimes);
    }
}
