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
/// The SSatoms subsystem from the DM of controllers/subsystem/atoms.dm: Initialize (:37), InitializeAtoms (:47) and
/// CreateAtoms (:89), the tracked initialized state (:137-171), prepare_deletion (:219), InitLog (:204) and Shutdown
/// (:226).
/// </summary>
public class SSatomsTests
{
    private static bool Initialized(atom a) => Dm.truthy(Dm.bitand(a.flags_1, INITIALIZED_1));

    private static ref float Uid => ref TgState.Current.datum_controller_subsystem_atoms__InitializeAtoms__uid;

    // ---- Initialize and InitializeAtoms ----

    [Fact]
    public void InitializeRecordsItsStartTimeAndLeavesTheRegularState()
    {
        var t = new AtomsTestWorld();
        t.World.StepDeciseconds(10);
        using (t.World.Enter())
        {
            Assert.Equal((float)INITIALIZATION_INSSATOMS, SSatoms.initialized); // atoms.dm:35
            Assert.Null(SSatoms.init_start_time);
            object result = AtomsTestWorld.InitializeSSatoms();
            Assert.True(Dm.eq(result, SS_INIT_SUCCESS));
            Assert.True(Dm.eq(SSatoms.init_start_time, 10));
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
            // The push at atoms.dm:55 saved base_initialized = 2 (set at :41); the pop at :61 put it back and reset the
            // base to INITIALIZATION_INNEW_REGULAR (:168).
            Assert.True(Dm.eq(SSatoms.base_initialized, INITIALIZATION_INNEW_REGULAR));
            Assert.Equal(0f, SSatoms.active_initializations);
            Assert.Equal(0f, SSatoms.initialized_state.len);
            Assert.Equal(0f, SSatoms.late_loaders.len);
            Assert.Equal(0f, SSatoms.queued_deletions.len);
            Assert.Null(SSatoms.created_atoms);
            // setupGenetics (atoms.dm:184) walks subtypesof(/datum/mutation) and makes one of each (gaps P3: every mutation type is
            // translated); the unlocked ones go to the quality pools.
            DmList every = subtypesof(typeof(datum_mutation));
            Assert.Equal(every.len, GLOB.all_mutations.len);
            Assert.Equal(every.len, GLOB.alias_mutations.len);
            float good = 0;
            foreach (object path in Dm.each(GLOB.all_mutations))
            {
                datum_mutation made = GLOB.all_mutations[path] as datum_mutation;
                if (!Dm.truthy(made.locked) && Dm.eq(made.quality, POSITIVE))
                    good++;
            }
            Assert.True(good > 0);
            Assert.Equal(good, GLOB.good_mutations.len);
            Assert.Equal(16, Dm.str(GLOB.full_sequences[typeof(datum_mutation_telekinesis)]).Length / 2); // 4 blocks of 4 pairs (dna.dm:228)
            Assert.Empty(t.Messages);
        }
    }

    [Fact]
    public void InitializeAtomsDoesNothingWhileSSatomsHasNotStarted()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            SSatoms.InitializeAtoms(Dm.list(a)); // atoms.dm:48-49
            Assert.False(Initialized(a));
            Assert.Empty(t.Trace);
            Assert.Equal(0f, Uid);
            Assert.Equal(0f, SSatoms.initialized_state.len);
        }
    }

    [Fact]
    public void TgQuirkTheInitializeAtomsUidStepsByTwoAndWrapsToOne()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            Assert.Equal(2f, Uid);
            // uid = WRAP_UID(uid + 1), WRAP_UID(val) = (val % (SHORT_REAL_LIMIT - 1)) + 1 (maths.dm:51-54, atoms.dm:53):
            // the + 1 is applied twice.
            var sources = new List<object>();
            for (int i = 0; i < 2; i++)
            {
                SSatoms.map_loader_begin("template");
                var a = Dm.@new<obj_test_atoms_probe>();
                SSatoms.map_loader_stop("template");
                SSatoms.InitializeAtoms(Dm.list(a));
                sources.Add(a.seen_source);
            }
            Assert.True(Dm.eq(sources[0], "subsystem init 4"));
            Assert.True(Dm.eq(sources[1], "subsystem init 6"));

            Uid = SHORT_REAL_LIMIT - 2; // 16777214: uid + 1 is the modulus, so the next uid is 0 + 1
            SSatoms.InitializeAtoms(Dm.list());
            Assert.Equal(1f, Uid);
        }
    }

    [Fact]
    public void ANestedInitializeAtomsUsesItsOwnSourceAndTgQuirkRunsTheOuterLateLoadersEarly()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var l1 = Dm.@new<obj_test_atoms_probe_lateload>();
            var nester = Dm.@new<obj_test_atoms_probe_nester>();
            var after = Dm.@new<obj_test_atoms_probe>();
            var inner = Dm.@new<obj_test_atoms_probe>();
            nester.nested = Dm.list(inner);
            AtomsTestWorld.InitializeSSatoms();
            // The inner InitializeAtoms (a template loaded during an Initialize) pushes its own source, initializes its
            // list, pops, and then runs every pending late loader, also the outer run's l1, while the outer run's source
            // is still on the stack (atoms.dm:55-72; the reading report's H11: port as written).
            Assert.Equal(new[]
            {
                Id(l1) + " Initialize mapload=1",
                Id(nester) + " Initialize mapload=1",
                Id(inner) + " Initialize mapload=1",
                Id(l1) + " LateInitialize initialized=2",
                Id(after) + " Initialize mapload=1",
            }, t.Trace);
            Assert.True(Dm.eq(nester.seen_source, "subsystem init 2"));
            Assert.True(Dm.eq(inner.seen_source, "subsystem init 4"));
            Assert.True(Dm.eq(nester.source_after_nested, "subsystem init 2"));
            Assert.True(Dm.eq(after.seen_source, "subsystem init 2"));
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
            Assert.Equal(0f, SSatoms.initialized_state.len);
        }
    }

    [Fact]
    public void ARuntimeInCreateAtomsEndsOnlyCreateAtomsAndInitializeAtomsPutsTheStateBack()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            SSatoms.map_loader_begin("template");
            var first = Dm.@new<obj_test_atoms_probe_lateload>();
            var second = Dm.@new<obj_test_atoms_probe>();
            var doomed = Dm.@new<obj_test_atoms_probe>();
            SSatoms.prepare_deletion(doomed);
            SSatoms.map_loader_stop("template");

            // A null entry: "A.flags_1" is a runtime that ends CreateAtoms. CreateAtoms "Exists solely so a runtime in
            // the creation logic doesn't cause initialized to totally break" (atoms.dm:57, :88): InitializeAtoms carries
            // on, puts the state back and runs the late loaders and the queued deletions (rules 9.3).
            SSatoms.InitializeAtoms(Dm.list(first, null, second));

            Assert.Single(t.Runtimes);
            Assert.True(Initialized(first));
            Assert.False(Initialized(second));
            Assert.Equal(new[] { Id(first) + " Initialize mapload=1", Id(first) + " LateInitialize initialized=1", Id(doomed) + " Destroy" }, t.Trace);
            Assert.Equal(0f, SSatoms.active_initializations);
            Assert.Equal(0f, SSatoms.initialized_state.len);
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
            Assert.Equal(0f, SSatoms.late_loaders.len);
            Assert.True(QDELETED(doomed));

            var later = Dm.@new<obj_test_atoms_probe_lateload>();
            Assert.True(Dm.eq(later.seen_mapload, FALSE));
        }
    }

    // ---- prepare_deletion and the queued deletions ----

    [Fact]
    public void PrepareDeletionQdelsAtOnceInTheRegularState()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            var a = Dm.@new<obj_test_atoms_probe>();
            SSatoms.prepare_deletion(a);
            Assert.True(QDELETED(a));
            Assert.Equal(0f, SSatoms.queued_deletions.len);
        }
    }

    [Fact]
    public void PrepareDeletionDuringAMapLoadQueuesAWeakrefUntilTheNextInitializeAtoms()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            SSatoms.map_loader_begin("template");
            var a = Dm.@new<obj_test_atoms_probe>();
            SSatoms.prepare_deletion(a);
            SSatoms.map_loader_stop("template");
            Assert.False(QDELETED(a));
            Assert.Equal(1f, SSatoms.queued_deletions.len);
            Assert.IsType<datum_weakref>(SSatoms.queued_deletions[1]);
            Assert.Same(a, ((datum_weakref)SSatoms.queued_deletions[1]).resolve());

            // qdel of the weakref qdels its target (datums/weakrefs.dm, Destroy), atoms.dm:78-79.
            SSatoms.InitializeAtoms(Dm.list(a));
            Assert.True(QDELETED(a));
            Assert.Equal(0f, SSatoms.queued_deletions.len);
            Assert.Equal(new[] { Id(a) + " Initialize mapload=1", Id(a) + " Destroy" }, t.Trace);
        }
    }

    [Fact]
    public void AtomsAskingForDeletionInTheirMapInitializeAreDeletedAfterTheLateLoaders()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            var d = Dm.@new<obj_test_atoms_probe_self_deleting>();
            var l = Dm.@new<obj_test_atoms_probe_lateload>();
            AtomsTestWorld.InitializeSSatoms();
            Assert.Equal(new[]
            {
                Id(d) + " Initialize mapload=1",
                Id(l) + " Initialize mapload=1",
                Id(l) + " LateInitialize initialized=2",
                Id(d) + " Destroy",
            }, t.Trace);
            Assert.True(QDELETED(d));
        }
    }

    // ---- InitLog and Shutdown ----

    [Fact]
    public void InitLogListsEachTypeWithItsFailuresInTgsOrder()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            SSatoms.BadInitializeCalls[typeof(obj_test_atoms_probe)] = BAD_INIT_SLEPT | BAD_INIT_QDEL_BEFORE | BAD_INIT_NO_HINT | BAD_INIT_DIDNT_INIT;
            SSatoms.BadInitializeCalls[typeof(obj_test_atoms_probe_qdel)] = BAD_INIT_SLEPT;
            Assert.Equal(
                "Path : /obj/test_atoms_probe \n"
                + "- Didn't call atom/Initialize(mapload)\n"
                + "- Didn't return an Initialize hint\n"
                + "- Qdel'd before Initialize proc ran\n"
                + "- Slept during Initialize()\n"
                + "Path : /obj/test_atoms_probe/qdel \n"
                + "- Slept during Initialize()\n",
                (string)SSatoms.InitLog());
        }
    }

    [Fact]
    public void ShutdownWritesTheInitLogOnlyWhenSomethingFailed()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            GLOB.log_directory = "data/logs/round";
            AtomsTestWorld.InitializeSSatoms();
            Dm.@new<obj_test_atoms_probe>();
            Waiting.NoSleep(SSatoms.Shutdown());
            var files = (DmMemoryFiles)DmWorldState.Current.Files;
            Assert.False(files.Exists("data/logs/round/initialize.log"));

            Dm.@new<obj_test_atoms_probe_no_hint>();
            Waiting.NoSleep(SSatoms.Shutdown());
            Assert.Equal("Path : /obj/test_atoms_probe/no_hint \n- Didn't return an Initialize hint\n",
                files.ReadText("data/logs/round/initialize.log"));
        }
    }
}

/// <summary>
/// The tracked initialized state of SSatoms (controllers/subsystem/atoms.dm:137-171): a stack of list(source, state)
/// whose top decides SSatoms.initialized, over a saved base.
/// </summary>
public class SSatomsInitializedStateTests
{
    private static DmList Entry(float i) => (DmList)SSatoms.initialized_state[i];

    [Fact]
    public void AnEmptyStackHasNoSourceAndClearingItChangesNothing()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            Assert.Null(SSatoms.get_initialized_source());
            SSatoms.initialized = INITIALIZATION_INNEW_REGULAR;
            SSatoms.base_initialized = 7;
            SSatoms.clear_tracked_initalize("nobody"); // atoms.dm:159-160
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
            Assert.True(Dm.eq(SSatoms.base_initialized, 7));
        }
    }

    [Fact]
    public void ThePushSavesTheBaseOnlyOnAnEmptyStackAndTheTopDecides()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            SSatoms.initialized = INITIALIZATION_INNEW_REGULAR;
            SSatoms.set_tracked_initalized(INITIALIZATION_INSSATOMS, "loader");
            Assert.True(Dm.eq(SSatoms.base_initialized, INITIALIZATION_INNEW_REGULAR));
            SSatoms.set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, "init");
            Assert.True(Dm.eq(SSatoms.base_initialized, INITIALIZATION_INNEW_REGULAR)); // not the 0 of "loader"
            Assert.Equal((float)INITIALIZATION_INNEW_MAPLOAD, SSatoms.initialized);
            Assert.True(Dm.eq(SSatoms.get_initialized_source(), "init"));
            Assert.Equal(2f, SSatoms.initialized_state.len);
            Assert.True(Dm.eq(Entry(1)[1], "loader") && Dm.eq(Entry(1)[2], INITIALIZATION_INSSATOMS));
            Assert.True(Dm.eq(Entry(2)[1], "init") && Dm.eq(Entry(2)[2], INITIALIZATION_INNEW_MAPLOAD));
        }
    }

    [Fact]
    public void ClearingASourceBelowTheTopKeepsTheTopsState()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            SSatoms.initialized = INITIALIZATION_INNEW_REGULAR;
            SSatoms.map_loader_begin("loader");
            SSatoms.set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, "init");
            SSatoms.map_loader_stop("loader");
            Assert.Equal((float)INITIALIZATION_INNEW_MAPLOAD, SSatoms.initialized);
            Assert.Equal(1f, SSatoms.initialized_state.len);
            Assert.True(Dm.eq(SSatoms.get_initialized_source(), "init"));
        }
    }

    [Fact]
    public void ClearingAnUnknownSourceRemovesNothingAndReappliesTheTopsState()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            SSatoms.initialized = INITIALIZATION_INNEW_REGULAR;
            SSatoms.map_loader_begin("loader");
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD; // INITIALIZE_IMMEDIATE writes it directly
            SSatoms.clear_tracked_initalize("stranger");
            Assert.Equal(1f, SSatoms.initialized_state.len);
            Assert.Equal((float)INITIALIZATION_INSSATOMS, SSatoms.initialized); // atoms.dm:171
        }
    }

    [Fact]
    public void TgQuirkClearingTheLastSourceRestoresTheBaseAndResetsTheBaseToRegular()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            Assert.Equal((float)INITIALIZATION_INSSATOMS, SSatoms.initialized);
            SSatoms.map_loader_begin("loader");
            SSatoms.map_loader_stop("loader");
            Assert.Equal((float)INITIALIZATION_INSSATOMS, SSatoms.initialized);
            // base_initialized becomes INITIALIZATION_INNEW_REGULAR (atoms.dm:168), not what it was before the push.
            Assert.True(Dm.eq(SSatoms.base_initialized, INITIALIZATION_INNEW_REGULAR));
        }
    }

    [Fact]
    public void ARepeatedSourceIsClearedFromTheTopDown()
    {
        var t = new AtomsTestWorld();
        using (t.World.Enter())
        {
            SSatoms.initialized = INITIALIZATION_INNEW_REGULAR;
            SSatoms.set_tracked_initalized(INITIALIZATION_INSSATOMS, "same");
            SSatoms.set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, "other");
            SSatoms.set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, "same");
            SSatoms.clear_tracked_initalize("same"); // atoms.dm:161-164: from length to 1, the first match only
            Assert.Equal(2f, SSatoms.initialized_state.len);
            Assert.True(Dm.eq(Entry(1)[1], "same") && Dm.eq(Entry(2)[1], "other"));
            Assert.Equal((float)INITIALIZATION_INNEW_MAPLOAD, SSatoms.initialized);
            SSatoms.clear_tracked_initalize("same");
            Assert.Equal((float)INITIALIZATION_INNEW_MAPLOAD, SSatoms.initialized);
            Assert.True(Dm.eq(SSatoms.get_initialized_source(), "other"));
            SSatoms.clear_tracked_initalize("other");
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
        }
    }

    [Fact]
    public void AMapLoadAfterSSatomsInitializedDefersItsAtomsUntilTheTemplateIsInitialized()
    {
        var t = new AtomsTestWorld(maxx: 1, maxy: 1, maxz: 1);
        using (t.World.Enter())
        {
            AtomsTestWorld.InitializeSSatoms();
            t.Trace.Clear();
            SSatoms.map_loader_begin("template"); // reader.dm's load pushes INITIALIZATION_INSSATOMS
            var a = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 1));
            var b = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 1));
            Assert.Empty(t.Trace);
            SSatoms.map_loader_stop("template");
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
            SSatoms.InitializeAtoms(Dm.list(b, a));
            // The list form initializes in list order with mapload TRUE (atoms.dm:104-114).
            Assert.Equal(new[] { Id(b) + " Initialize mapload=1", Id(a) + " Initialize mapload=1" }, t.Trace);
            Assert.True(Dm.eq(a.seen_initialized_state, INITIALIZATION_INNEW_MAPLOAD));
        }
    }
}
