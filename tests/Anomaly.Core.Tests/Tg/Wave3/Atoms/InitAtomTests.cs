using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave3.Atoms.AtomsTrace;

namespace Anomaly.Core.Tests.Tg.Wave3.Atoms;

/// <summary>
/// The InitAtom table of docs/ai/specs/m0-reading/atom.md section 12, from the DM of
/// game/atom/atoms_initializing_EXPENSIVE.dm (InitAtom :2-54, /atom/New :65-75, /atom/Initialize :115-159) and
/// controllers/subsystem/atoms.dm (Initialize :37, InitializeAtoms :47, CreateAtoms :89): when an atom initializes, with
/// which mapload, what each hint does, what is recorded as a bad init, and the signals after a successful init.
/// </summary>
public class InitAtomTests
{
    private static bool Initialized(atom a) => Dm.truthy(Dm.bitand(a.flags_1, INITIALIZED_1));

    private static object Bad(Type t) => SSatoms.BadInitializeCalls[t];

    // ---- before and after SSatoms initializes ----

    [Fact]
    public void AtomsCreatedBeforeSSatomsInitializesWaitThenInitializeInTheWorldsOrderWithMapload()
    {
        var t = new AtomsTestWorld(maxx: 2, maxy: 1, maxz: 1);
        using (t.World.Enter())
        {
            turf t1 = locate(1, 1, 1);
            turf t2 = locate(2, 1, 1);
            var room = (area)t1.loc;
            var a = Dm.@new<obj_test_atoms_probe>(t1);
            var b = Dm.@new<obj_test_atoms_probe>(t2);
            // atom/New with SSatoms.initialized == INITIALIZATION_INSSATOMS does nothing (atoms_initializing:70-71).
            Assert.Empty(t.Trace);
            Assert.False(Initialized(a));
            Assert.False(Initialized(t1));
            Assert.False(Initialized(room));

            object result = AtomsTestWorld.InitializeSSatoms();

            Assert.True(Dm.eq(result, SS_INIT_SUCCESS));
            // CreateAtoms without a list walks the world (atoms.dm:121) in the world's order: the movables, then the
            // area, then the turfs, each in id order (object-model.md 5, OpenDream AtomManager.EnumerateAtoms), and passes
            // list(TRUE): mapload is TRUE (atoms.dm:97, :124).
            Assert.Equal(new[]
            {
                Id(a) + " Initialize mapload=1",
                Id(b) + " Initialize mapload=1",
                Id(room) + " Initialize mapload=1",
                Id(t1) + " Initialize mapload=1",
                Id(t2) + " Initialize mapload=1",
            }, t.Trace);
            Assert.True(Initialized(a) && Initialized(b) && Initialized(t1) && Initialized(t2) && Initialized(room));
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
        }
    }

    [Fact]
    public void DuringSSatomsInitTheAtomSeesTheMaploadStateItsSourceAndAnActiveInitialization()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            AtomsTestWorld.InitializeSSatoms();
            Assert.Equal((float)INITIALIZATION_INNEW_MAPLOAD, a.seen_initialized_state);
            Assert.True(Dm.eq(a.seen_initializing_something, TRUE));
            // TgQuirk: uid = WRAP_UID(uid + 1) is ((uid + 1) % (SHORT_REAL_LIMIT - 1)) + 1 (maths.dm:51-54), so the first
            // source is "subsystem init 2"; SSatomsTests pins the step of two.
            Assert.True(Dm.eq(a.seen_source, "subsystem init 2"));
            Assert.True(Dm.eq(SSatoms.initializing_something(), FALSE));
            Assert.Null(SSatoms.get_initialized_source());
        }
    }

    [Fact]
    public void MapInitializationPassesNoArgumentsBeyondMapload()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            // "Any parameters from new are passed through (excluding loc), naturally if you're loading from a map there
            // are no other arguments" (atoms_initializing:106-107): CreateAtoms calls InitAtom with list(TRUE).
            var a = Dm.@new<obj_test_atoms_probe>(null, "extra", 5);
            AtomsTestWorld.InitializeSSatoms();
            Assert.True(Dm.eq(a.seen_mapload, TRUE));
            Assert.Empty(a.seen_extras);
        }
    }

    [Fact]
    public void AtomsCreatedAfterSSatomsInitializedInitializeInsideNewWithMaploadFalseAndTheirExtraArguments()
    {
        var t = new AtomsTestWorld(maxx: 1, maxy: 1, maxz: 1);
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            t.Trace.Clear();
            turf floor = locate(1, 1, 1);
            var a = Dm.@new<obj_test_atoms_probe>(floor, "extra", 5);
            // atom/New: args[1] = (initialized == INITIALIZATION_INNEW_MAPLOAD), then InitAtom(src, FALSE, args), so
            // Initialize gets FALSE where New got loc, and the rest of new's arguments (atoms_initializing:70-73).
            Assert.Equal(new[] { Id(a) + " Initialize mapload=0" }, t.Trace);
            Assert.True(Dm.eq(a.seen_mapload, FALSE));
            Assert.Equal(2, a.seen_extras.Length);
            Assert.True(Dm.eq(a.seen_extras[0], "extra"));
            Assert.True(Dm.eq(a.seen_extras[1], 5));
            Assert.Same(floor, a.loc); // loc is in place before New (the silent placement)
            Assert.Null(a.seen_source);
            Assert.True(Dm.eq(a.seen_initializing_something, FALSE));
            Assert.True(Initialized(a));
        }
    }

    [Fact]
    public void AnAtomCreatedDuringAnotherAtomsMapInitializationInitializesInNewWithMaploadAndOnlyOnce()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var spawner = Dm.@new<obj_test_atoms_probe_spawner>();
            var after = Dm.@new<obj_test_atoms_probe>();
            AtomsTestWorld.InitializeSSatoms();
            obj_test_atoms_probe spawned = spawner.spawned;
            Assert.NotNull(spawned);
            // SSatoms.initialized is INITIALIZATION_INNEW_MAPLOAD while CreateAtoms runs, so the new atom initializes in
            // New with mapload TRUE; the world loop then reaches it (the loop is live) and skips it: INITIALIZED_1.
            Assert.Equal(new[]
            {
                Id(spawner) + " Initialize mapload=1",
                Id(spawned) + " Initialize mapload=1",
                Id(after) + " Initialize mapload=1",
            }, t.Trace);
            Assert.True(Dm.eq(spawned.seen_source, "subsystem init 2"));
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
            Assert.Empty(t.Messages); // no "initialized multiple times!" stack trace (atoms_initializing:120)
        }
    }

    // ---- the hints ----

    [Fact]
    public void LateloadAtRuntimeRunsLateInitializeInsideInitAtomBeforeThePostInitSignals()
    {
        var t = new AtomsTestWorld(maxx: 1, maxy: 1, maxz: 1);
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            turf floor = locate(1, 1, 1);
            var listener = Dm.@new<datum_test_atoms_listener>();
            listener.RegisterSignal(SSdcs, COMSIG_GLOB_ATOM_AFTER_POST_INIT, nameof(datum_test_atoms_listener.on_glob_post_init));
            listener.RegisterSignal(floor, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(datum_test_atoms_listener.on_initialized_on));
            t.Trace.Clear();

            var a = Dm.@new<obj_test_atoms_probe_lateload>(floor);

            Assert.Equal(new[]
            {
                Id(a) + " Initialize mapload=0",
                Id(a) + " LateInitialize initialized=1",
                Id(a) + " GLOB ATOM_AFTER_POST_INIT from SSdcs",
                Id(floor) + " AFTER_SUCCESSFUL_INITIALIZED_ON " + Id(a) + " mapload=0",
            }, t.Trace);
            Assert.Equal(0f, SSatoms.late_loaders.len);
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
        }
    }

    [Fact]
    public void LateloadAtMaploadWaitsUntilEveryAtomInitializedThenRunsInInitializeOrder()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var l1 = Dm.@new<obj_test_atoms_probe_lateload>();
            var n = Dm.@new<obj_test_atoms_probe>();
            var l2 = Dm.@new<obj_test_atoms_probe_lateload>();
            AtomsTestWorld.InitializeSSatoms();
            // InitAtom: arguments[1] (mapload) is TRUE, so late_loaders += A (atoms_initializing:30-31); InitializeAtoms
            // runs them after CreateAtoms and clear_tracked_initalize (atoms.dm:64-72). SSatoms/Initialize set
            // initialized = INITIALIZATION_INNEW_MAPLOAD itself (atoms.dm:41), which the stack restores before the late
            // loaders run: they see 2.
            Assert.Equal(new[]
            {
                Id(l1) + " Initialize mapload=1",
                Id(n) + " Initialize mapload=1",
                Id(l2) + " Initialize mapload=1",
                Id(l1) + " LateInitialize initialized=2",
                Id(l2) + " LateInitialize initialized=2",
            }, t.Trace);
            Assert.Equal(0f, SSatoms.late_loaders.len);
        }
    }

    [Fact]
    public void ARuntimeInOneLateInitializeEndsOnlyItAndTheOtherLateLoadersStillRun()
    {
        // DM: the runtime ends only that LateInitialize; InitializeAtoms' loop goes on and cuts late_loaders
        // (atoms.dm:64-72; rules 9.2 item 9).
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var l1 = Dm.@new<obj_test_atoms_probe_lateload_crash>();
            var l2 = Dm.@new<obj_test_atoms_probe_lateload>();
            AtomsTestWorld.InitializeSSatoms();
            Assert.Contains(Id(l1) + " LateInitialize initialized=2", t.Trace);
            Assert.Contains(Id(l2) + " LateInitialize initialized=2", t.Trace);
            Assert.Equal(0f, SSatoms.late_loaders.len);
        }
        Assert.Equal(1, t.World.RuntimeErrorCount);
    }

    [Fact]
    public void LateloadFromAMapTemplateAfterSSatomsInitializedSeesTheRegularStateInLateInitialize()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            SSatoms.map_loader_begin("template");
            var l = Dm.@new<obj_test_atoms_probe_lateload>();
            SSatoms.map_loader_stop("template");
            Assert.Empty(t.Trace);
            SSatoms.InitializeAtoms(Dm.list(l));
            Assert.Equal(new[]
            {
                Id(l) + " Initialize mapload=1",
                Id(l) + " LateInitialize initialized=1",
            }, t.Trace);
        }
    }

    [Fact]
    public void ALateInitializeThatSleepsDoesNotHoldUpTheOtherLateLoaders()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var sleeper = Dm.@new<obj_test_atoms_probe_late_sleeper>();
            var l = Dm.@new<obj_test_atoms_probe_lateload>();
            AtomsTestWorld.InitializeSSatoms();
            // LateInitialize has set waitfor = FALSE (atoms_initializing:173, inherited by every override): the caller
            // goes on when it sleeps.
            Assert.Equal(new[]
            {
                Id(sleeper) + " Initialize mapload=1",
                Id(l) + " Initialize mapload=1",
                Id(sleeper) + " LateInitialize start",
                Id(l) + " LateInitialize initialized=2",
            }, t.Trace);
        }
        t.World.StepDeciseconds(3);
        Assert.EndsWith(" LateInitialize end", t.Trace[^1]);
        Assert.Empty(t.Messages);
    }

    [Fact]
    public void TgQuirkALateloadAtomCreatedInALateInitializeAtMaploadNeverLateInitializes()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var spawner = Dm.@new<obj_test_atoms_probe_late_spawner>();
            AtomsTestWorld.InitializeSSatoms();
            obj_test_atoms_probe spawned = spawner.spawned;
            Assert.NotNull(spawned);
            // The late loader runs while initialized is still INITIALIZATION_INNEW_MAPLOAD, so the new atom initializes
            // with mapload TRUE and its LATELOAD hint appends it to late_loaders; but the loop's bound was read once
            // (atoms.dm:65) and late_loaders.Cut() (:72) then drops it: its LateInitialize never runs.
            Assert.Equal(new[]
            {
                Id(spawner) + " Initialize mapload=1",
                Id(spawner) + " LateInitialize initialized=2",
                Id(spawned) + " Initialize mapload=1",
            }, t.Trace);
            Assert.Equal(0f, SSatoms.late_loaders.len);
            Assert.True(Initialized(spawned));
        }
    }

    [Fact]
    public void DV2ALateLoaderQdeldAtTime0BeforeTheLateLoadersRunIsSkipped()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var doomed = Dm.@new<obj_test_atoms_probe_lateload>();
            var other = Dm.@new<obj_test_atoms_probe_lateload>();
            var listener = Dm.@new<datum_test_atoms_listener>();
            listener.RegisterSignal(doomed, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZE, nameof(datum_test_atoms_listener.qdel_source));
            AtomsTestWorld.InitializeSSatoms();
            // DEVIATION(DV2): QDELETED is gc_destroyed != null, so the late loader deleted at world time 0 is skipped
            // (atoms.dm:68-69); tg reads gc_destroyed = 0 as false at time 0 and would LateInitialize the deleted atom.
            Assert.Equal(new[]
            {
                Id(doomed) + " Initialize mapload=1",
                Id(doomed) + " qdel by listener",
                Id(doomed) + " Destroy",
                Id(other) + " Initialize mapload=1",
                Id(other) + " LateInitialize initialized=2",
            }, t.Trace);
            Assert.Equal(0f, SSatoms.late_loaders.len);
        }
    }

    [Fact]
    public void TheQdelHintDeletesTheAtomAndNewSeesItDeleted()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var listener = Dm.@new<datum_test_atoms_listener>();
            listener.RegisterSignal(SSdcs, COMSIG_GLOB_ATOM_AFTER_POST_INIT, nameof(datum_test_atoms_listener.on_glob_post_init));
            t.Trace.Clear();

            var a = Dm.@new<obj_test_atoms_probe_qdel>();

            Assert.True(QDELETED(a));
            // TgQuirk: qdel leaves a soft-deleted A, so InitAtom's "if(!A)" is false and INITIALIZED_1 is set: the global
            // post-init signal still goes out for the deleted atom (atoms_initializing:34-46).
            Assert.Equal(new[]
            {
                Id(a) + " Initialize mapload=0",
                Id(a) + " Destroy",
                Id(a) + " GLOB ATOM_AFTER_POST_INIT from SSdcs",
            }, t.Trace);
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
        }
    }

    [Fact]
    public void InitAtomReturnsWhetherTheAtomWasDeleted()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var keep = Dm.@new<obj_test_atoms_probe>();
            var gone = Dm.@new<obj_test_atoms_probe_qdel>();
            AtomsTestWorld.InitializeSSatoms(); // initializes both (the world loop); InitAtom below is a second call
            var fresh_keep = Dm.@new<obj_test_atoms_probe>();
            fresh_keep.flags_1 = NONE;
            Assert.True(Dm.eq(SSatoms.InitAtom(fresh_keep, FALSE, Dm.list(FALSE)), FALSE));
            Assert.True(QDELETED(gone));
            var fresh_gone = Dm.@new<obj_test_atoms_probe>();
            fresh_gone.flags_1 = NONE;
            fresh_gone.hint = INITIALIZE_HINT_QDEL;
            Assert.True(Dm.eq(SSatoms.InitAtom(fresh_gone, FALSE, Dm.list(FALSE)), TRUE));
            Assert.False(QDELETED(keep));
        }
    }

    [Fact]
    public void ANullOrUnknownHintIsRecordedAsBadInitNoHintAndTheSignalsStillGoOut()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var listener = Dm.@new<datum_test_atoms_listener>();
            listener.RegisterSignal(SSdcs, COMSIG_GLOB_ATOM_AFTER_POST_INIT, nameof(datum_test_atoms_listener.on_glob_post_init));
            t.Trace.Clear();

            // H2: the result is an object, so null stays distinct from INITIALIZE_HINT_NORMAL (0).
            var a = Dm.@new<obj_test_atoms_probe_no_hint>();
            Assert.True(Dm.eq(Bad(typeof(obj_test_atoms_probe_no_hint)), BAD_INIT_NO_HINT));
            Assert.False(QDELETED(a));
            Assert.Equal(new[]
            {
                Id(a) + " Initialize mapload=0",
                Id(a) + " GLOB ATOM_AFTER_POST_INIT from SSdcs",
            }, t.Trace);

            // Any value but 0, 1 and 2 falls to the switch's else (atoms_initializing:37-38); "0" is text, not 0.
            var b = Dm.@new<obj_test_atoms_probe>();
            b.flags_1 = NONE;
            b.hint = 3;
            SSatoms.InitAtom(b, FALSE, Dm.list(FALSE));
            var c = Dm.@new<obj_test_atoms_probe>();
            c.flags_1 = NONE;
            c.hint = "0";
            SSatoms.InitAtom(c, FALSE, Dm.list(FALSE));
            Assert.True(Dm.eq(Bad(typeof(obj_test_atoms_probe)), BAD_INIT_NO_HINT));
            Assert.Equal("Path : /obj/test_atoms_probe/no_hint \n- Didn't return an Initialize hint\n"
                + "Path : /obj/test_atoms_probe \n- Didn't return an Initialize hint\n", (string)SSatoms.InitLog());
        }
    }

    [Fact]
    public void AnInitializeThatDoesNotCallParentIsRecordedAsDidntInitAndSendsNoSignals()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var listener = Dm.@new<datum_test_atoms_listener>();
            listener.RegisterSignal(SSdcs, COMSIG_GLOB_ATOM_AFTER_POST_INIT, nameof(datum_test_atoms_listener.on_glob_post_init));
            t.Trace.Clear();

            var a = Dm.@new<obj_test_atoms_probe_no_parent>();

            Assert.False(Initialized(a));
            Assert.False(QDELETED(a));
            Assert.True(Dm.eq(Bad(typeof(obj_test_atoms_probe_no_parent)), BAD_INIT_DIDNT_INIT));
            Assert.Equal(new[] { Id(a) + " Initialize mapload=0" }, t.Trace);
            Assert.Equal("Path : /obj/test_atoms_probe/no_parent \n- Didn't call atom/Initialize(mapload)\n", (string)SSatoms.InitLog());
        }
    }

    [Fact]
    public void ARuntimeInInitializeEndsItWithNoHintAtTheInitAtomBoundary()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var listener = Dm.@new<datum_test_atoms_listener>();
            listener.RegisterSignal(SSdcs, COMSIG_GLOB_ATOM_AFTER_POST_INIT, nameof(datum_test_atoms_listener.on_glob_post_init));
            t.Trace.Clear();

            // DM ends Initialize with '.' (null); the port unwinds to InitAtom's isolation boundary (DV1, rules 9.2 item 6),
            // which reports it and goes on with a null result: the same outcome.
            var early = Dm.@new<obj_test_atoms_probe_crash_before>();
            var late = Dm.@new<obj_test_atoms_probe_crash_after>();

            Assert.True(Dm.eq(Bad(typeof(obj_test_atoms_probe_crash_before)), BAD_INIT_NO_HINT | BAD_INIT_DIDNT_INIT));
            Assert.True(Dm.eq(Bad(typeof(obj_test_atoms_probe_crash_after)), BAD_INIT_NO_HINT));
            Assert.False(Initialized(early));
            Assert.True(Initialized(late));
            Assert.Equal(new[]
            {
                Id(early) + " Initialize mapload=0",
                Id(late) + " Initialize mapload=0",
                Id(late) + " GLOB ATOM_AFTER_POST_INIT from SSdcs",
            }, t.Trace);
            Assert.Equal(new[] { "test_atoms_probe: runtime before ..()", "test_atoms_probe: runtime after ..()" }, t.Messages);
        }
    }

    [Fact]
    public void ASleepInInitializeIsAReportedRuntimeAndNotABadInitInProduction()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            t.Trace.Clear();
            var a = Dm.@new<obj_test_atoms_probe_sleeper>();
            // BAD_INIT_SLEPT is recorded only under UNIT_TESTS (atoms_initializing:13-22), which production does not
            // define (rules 1.7). The port's Initialize is synchronous: the sleeping call is a runtime, Initialize goes on
            // and returns its hint, the callee finishes detached (DV5, waiting-model.md 5.3).
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
            Assert.True(Initialized(a));
            Assert.Equal(new[] { Id(a) + " Initialize mapload=0", Id(a) + " nap start" }, t.Trace);
            Assert.Single(t.Runtimes);
            Assert.Contains("slept", t.Messages[0]);
        }
        t.World.StepDeciseconds(5);
        Assert.EndsWith(" nap end", t.Trace[^1]);
    }

    // ---- qdel before Initialize (scope R18: DV2 at world time 0) ----

    [Fact]
    public void DV2AnAtomQdeldAtTime0BeforeItsMapInitializationIsSkippedAndNotRecorded()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            qdel(a);
            Assert.True(Dm.eq(a.gc_destroyed, 0)); // queued at world.time 0 (garbage.dm:270)
            AtomsTestWorld.InitializeSSatoms();
            Assert.True(Dm.eq(SSatoms.init_start_time, 0));
            t.Trace.Clear();
            SSatoms.InitializeAtoms(Dm.list(a));
            // DEVIATION(DV2): QDELING(A) is gc_destroyed != null, so InitAtom returns TRUE without Initialize; tg reads
            // gc_destroyed = 0 as false at time 0 and would Initialize the deleted atom. gc_destroyed (0) is not above
            // init_start_time (0), so nothing is recorded (atoms_initializing:6-10).
            Assert.Empty(t.Trace);
            Assert.False(Initialized(a));
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
        }
    }

    [Fact]
    public void AnAtomQdeldAfterSSatomsStartedButBeforeItsInitializeIsRecordedAsQdelBefore()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
            AtomsTestWorld.InitializeSSatoms(); // init_start_time = 0
        t.World.StepDeciseconds(10);
        using (t.World.Enter())
        {
            SSatoms.map_loader_begin("template");
            var a = Dm.@new<obj_test_atoms_probe>();
            qdel(a);
            SSatoms.map_loader_stop("template");
            t.Trace.Clear();
            SSatoms.InitializeAtoms(Dm.list(a));
            Assert.Empty(t.Trace);
            Assert.True(Dm.eq(Bad(typeof(obj_test_atoms_probe)), BAD_INIT_QDEL_BEFORE));
            Assert.Equal("Path : /obj/test_atoms_probe \n- Qdel'd before Initialize proc ran\n", (string)SSatoms.InitLog());
        }
    }

    [Fact]
    public void AnAtomQdeldBeforeSSatomsStartedIsNotRecorded()
    {
        var t = new AtomsTestWorld();
        obj_test_atoms_probe a;
        t.World.StepDeciseconds(5);
        using (t.World.Enter())
        {
            a = Dm.@new<obj_test_atoms_probe>();
            qdel(a); // gc_destroyed = 5
        }
        t.World.StepDeciseconds(5);
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms(); // init_start_time = 10
            SSatoms.InitializeAtoms(Dm.list(a));
            // "Check init_start_time to not worry about atoms created before the atoms SS that are cleaned up before this"
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
            Assert.False(Initialized(a));
        }
    }

    // ---- the signals after a successful Initialize (atoms_initializing:44-50) ----

    [Fact]
    public void AfterASuccessfulInitializeTheAtomThenTheGlobalThenItsLocGetTheirSignals()
    {
        var t = new AtomsTestWorld(maxx: 1, maxy: 1, maxz: 1);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var a = Dm.@new<obj_test_atoms_probe>(floor);
            var listener = Dm.@new<datum_test_atoms_listener>();
            listener.RegisterSignal(floor, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(datum_test_atoms_listener.on_initialized_on));
            listener.RegisterSignal(SSdcs, COMSIG_GLOB_ATOM_AFTER_POST_INIT, nameof(datum_test_atoms_listener.on_glob_post_init));
            listener.RegisterSignal(a, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZE, nameof(datum_test_atoms_listener.on_init_success));
            AtomsTestWorld.InitializeSSatoms();
            List<string> forA = t.Trace.Where(l => l.Contains(Id(a))).ToList();
            Assert.Equal(new[]
            {
                Id(a) + " Initialize mapload=1",
                Id(a) + " AFTER_SUCCESSFUL_INITIALIZE",
                Id(a) + " GLOB ATOM_AFTER_POST_INIT from SSdcs",
                Id(floor) + " AFTER_SUCCESSFUL_INITIALIZED_ON " + Id(a) + " mapload=1",
            }, forA);
            // The turf itself was initialized earlier in the same loop (it has no loc signal listener: its loc is the area).
            Assert.Contains(Id(floor) + " GLOB ATOM_AFTER_POST_INIT from SSdcs", t.Trace);
        }
    }

    [Fact]
    public void AnAtomInNullspaceGetsNoLocSignal()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var listener = Dm.@new<datum_test_atoms_listener>();
            listener.RegisterSignal(SSdcs, COMSIG_GLOB_ATOM_AFTER_POST_INIT, nameof(datum_test_atoms_listener.on_glob_post_init));
            t.Trace.Clear();
            var a = Dm.@new<obj_test_atoms_probe>();
            Assert.Equal(new[] { Id(a) + " Initialize mapload=0", Id(a) + " GLOB ATOM_AFTER_POST_INIT from SSdcs" }, t.Trace);
        }
    }

    [Fact]
    public void ATemplateInitializationReturnsNoCreatedTurfs()
    {
        var t = new AtomsTestWorld(maxx: 1, maxy: 1, maxz: 1);
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            SSatoms.map_loader_begin("template");
            turf floor = locate(1, 1, 1);
            var fresh = Dm.@new<turf_test_atoms_floor>(floor); // replaces the turf (BYOND new at a turf)
            SSatoms.map_loader_stop("template");
            DmList returned = Dm.list();
            SSatoms.InitializeAtoms(Dm.list(fresh), returned);
            // created_atoms collects only movables' contents (atoms_initializing:51-52), and is reset to null after it
            // was appended to atoms_to_return (atoms.dm:74-76).
            Assert.True(Initialized(fresh));
            Assert.Equal(0f, returned.len);
            Assert.Null(SSatoms.created_atoms);
        }
    }

    // ---- INITIALIZE_IMMEDIATE (__DEFINES/subsystems.dm:96-105) ----

    [Fact]
    public void InitializeImmediateInitializesInNewWithMaploadBeforeSSatomsIsUpAndRestoresTheState()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_immediate_probe>(null, "extra");
            Assert.True(Initialized(a));
            Assert.Equal(new[] { Id(a) + " Initialize mapload=1" }, t.Trace);
            Assert.Equal((float)INITIALIZATION_INNEW_MAPLOAD, a.seen_initialized_state);
            Assert.Null(a.seen_source); // the macro sets initialized directly, not through the tracked stack
            Assert.True(Dm.eq(a.seen_extras[0], "extra"));
            Assert.Equal((float)INITIALIZATION_INSSATOMS, a.seen_initialized_after_new);
            Assert.Equal((float)INITIALIZATION_INSSATOMS, SSatoms.initialized);
            Assert.Equal(0f, SSatoms.initialized_state.len);

            AtomsTestWorld.InitializeSSatoms();
            Assert.Single(t.Trace); // the world loop skips it: INITIALIZED_1
        }
    }

    [Fact]
    public void InitializeImmediateAfterSSatomsIsUpInitializesOnceInAtomNew()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var a = Dm.@new<obj_test_atoms_immediate_probe>();
            Assert.Equal(new[] { Id(a) + " Initialize mapload=0" }, t.Trace);
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
            Assert.Empty(t.Messages);
        }
    }

    [Fact]
    public void InitializeImmediateDuringAMapLoadInitializesAndPutsBackTheLoadersState()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            SSatoms.map_loader_begin("template");
            var a = Dm.@new<obj_test_atoms_immediate_probe>();
            var b = Dm.@new<obj_test_atoms_probe>();
            Assert.True(Initialized(a));
            Assert.False(Initialized(b));
            Assert.Equal((float)INITIALIZATION_INSSATOMS, SSatoms.initialized);
            Assert.True(Dm.eq(SSatoms.get_initialized_source(), "template"));
            SSatoms.map_loader_stop("template");
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
        }
    }

    [Fact]
    public void TgQuirkAnImmediateLateloadSubtypeBeforeSSatomsIsUpWaitsForSSatomsToLateInitialize()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            // The subtype inherits the macro's New. It initializes with args[1] = TRUE, so InitAtom treats its LATELOAD
            // as a mapload one: late_loaders += A (atoms_initializing:30-31), run by the next InitializeAtoms.
            var a = Dm.@new<obj_test_atoms_immediate_probe_lateload>();
            Assert.Equal(new[] { Id(a) + " Initialize mapload=1" }, t.Trace);
            Assert.Equal(1f, SSatoms.late_loaders.len);
            Assert.Same(a, SSatoms.late_loaders[1]);

            AtomsTestWorld.InitializeSSatoms();
            Assert.Equal(new[] { Id(a) + " Initialize mapload=1", Id(a) + " LateInitialize initialized=2" }, t.Trace);
            Assert.Equal(0f, SSatoms.late_loaders.len);
        }
    }

    [Fact]
    public void ASecondInitializeOfAnInitializedAtomIsAStackTraceAndStillInitializes()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var a = Dm.@new<obj_test_atoms_probe>();
            t.Trace.Clear();
            SSatoms.InitAtom(a, FALSE, Dm.list(FALSE));
            // "Warning: [src]([type]) initialized multiple times!" (atoms_initializing:119-120) is a stack_trace: the
            // proc goes on. An improper name embedded in text gets "the" (dm-conformance.md).
            Assert.Equal(new[] { Id(a) + " Initialize mapload=0" }, t.Trace);
            Assert.Single(t.Messages);
            Assert.Equal("Warning: the test atoms probe(/obj/test_atoms_probe) initialized multiple times!", t.Messages[0]);
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
        }
    }
}
