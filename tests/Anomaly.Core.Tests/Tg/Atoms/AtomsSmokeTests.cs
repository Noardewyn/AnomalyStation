using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Atoms;

// Smoke tests of the atoms package (wave 3, E): the translation of controllers/subsystem/atoms.dm,
// game/atom/atoms_initializing_EXPENSIVE.dm, game/atom/_atom.dm, modules/mapping/preloader.dm and the plane macros
// compiles and runs. The behaviour tests (the InitAtom table, create_and_destroy) are the independent test-writer's.
//
// Until the engine package (F) and the mc package (A) land, the paths cross their skeletons (atom.loc and overlays,
// WRAP_UID, TICK_CHECK), so these worlds run with NotTranslatedThrows = false and assert only on the atoms package's
// state (wave3-ownership.md).

/// <summary>An atom fixture whose Initialize returns the hint the test sets (rules 15: a fixture path no tg path starts with).</summary>
[DmPath("/obj/test_atoms_hinted")]
public class test_atoms_hinted : obj
{
    public object hint = INITIALIZE_HINT_NORMAL;
    [NotHashed] public List<string> trace = new();

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        trace.Add("Initialize mapload=" + Dm.str(mapload));
        return hint;
    }

    public override void LateInitialize(params object[] __extra)
    {
        trace.Add("LateInitialize");
    }
}

/// <summary>
/// INITIALIZE_IMMEDIATE(/obj/test_atoms_immediate) (__DEFINES/subsystems.dm:96-105), expanded inline as the macro's
/// New override (rules 11.1 (b) and (d): it pastes the type path and reads src and args).
/// </summary>
[DmPath("/obj/test_atoms_immediate")]
public class test_atoms_immediate : obj
{
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        DmList args = Dm.args(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }
}

public class AtomsSmokeTests
{
    private static World NewWorld()
    {
        var w = new World(3);
        DmWorldState.Of(w).NotTranslatedThrows = false;
        Tools.GeneticsStrings.Mount(w); // gaps P3: setupGenetics' speech mutations read them
        using (w.Enter())
        {
            GLOB__var = new datum_controller_global_vars();
            GLOB.gvars_datum_init_order = Dm.list();
            // Gaps P3: strings() sanitizes the speech mutations' file paths with it (__HELPERS/files.dm:40)
            GLOB.filename_forbidden_chars = Dm.regex(@"""|[\\\n\t/?%*:|<>]|\.\.", "g");
            GLOB.InitGlobaluse_preloader();
            GLOB.InitGlobal_preloader_attributes();
            GLOB.InitGlobal_preloader_path();
            GLOB.InitGlobalall_mutations();
            GLOB.InitGlobalfull_sequences();
            GLOB.InitGlobalbad_mutations();
            GLOB.InitGlobalgood_mutations();
            GLOB.InitGlobalnot_good_mutations();
            GLOB.InitGlobalalias_mutations();
            Dm.@new<datum_controller_subsystem_garbage>();
            Dm.@new<datum_controller_subsystem_processing_dcs>();
            Dm.@new<datum_controller_subsystem_mapping>();
            Dm.@new<datum_controller_subsystem_icon_smooth>();
            Dm.@new<datum_controller_subsystem_atoms>();
        }
        return w;
    }

    [Fact]
    public void AtomsCreatedBeforeSSatomsInitializesWaitForItAndLaterOnesInitializeInNew()
    {
        World w = NewWorld();
        using (w.Enter())
        {
            Assert.Equal((float)INITIALIZATION_INSSATOMS, SSatoms.initialized);
            var early = Dm.@new<test_atoms_hinted>();
            early.hint = INITIALIZE_HINT_LATELOAD;
            Assert.False(Dm.truthy(Dm.bitand(early.flags_1, INITIALIZED_1)));
            Assert.Empty(early.trace);

            object result = Waiting.NoSleep(SSatoms.Initialize());
            Assert.True(Dm.eq(result, SS_INIT_SUCCESS));
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
            // Initialize() walks world (CreateAtoms, atoms.dm:121), which finds atoms once the engine package makes them
            // IDmAtom; the list form (a map template's call) reaches the atom either way, and skips it when Initialize did.
            SSatoms.InitializeAtoms(Dm.list(early));
            Assert.True(Dm.truthy(Dm.bitand(early.flags_1, INITIALIZED_1)));
            // CreateAtoms passes list(TRUE) as the arguments; LATELOAD at mapload runs after every atom initialized.
            Assert.Equal(new[] { "Initialize mapload=1", "LateInitialize" }, early.trace);
            Assert.Equal(0f, SSatoms.initialized_state.len);
            Assert.Equal(0f, SSatoms.late_loaders.len);

            var late = Dm.@new<test_atoms_hinted>();
            Assert.Equal(new[] { "Initialize mapload=0" }, late.trace);
            Assert.True(Dm.truthy(Dm.bitand(late.flags_1, INITIALIZED_1)));
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
            Assert.True(Dm.eq(SSatoms.InitLog(), ""));
        }
    }

    [Fact]
    public void InitAtomRecordsAMissingHintAndQdelsOnTheQdelHint()
    {
        World w = NewWorld();
        using (w.Enter())
        {
            Waiting.NoSleep(SSatoms.Initialize());
            var a = Dm.@new<test_atoms_hinted>();
            a.hint = null;
            a.flags_1 = NONE;
            SSatoms.InitAtom(a, FALSE, Dm.list(FALSE));
            Assert.True(Dm.eq(SSatoms.BadInitializeCalls[typeof(test_atoms_hinted)], BAD_INIT_NO_HINT));
            Assert.Contains("- Didn't return an Initialize hint\n", (string)SSatoms.InitLog());

            var b = Dm.@new<test_atoms_hinted>();
            b.hint = INITIALIZE_HINT_QDEL;
            b.flags_1 = NONE;
            object deleted = SSatoms.InitAtom(b, FALSE, Dm.list(FALSE));
            Assert.True(Dm.truthy(deleted));
            Assert.True(QDELETED(b));
        }
    }

    [Fact]
    public void TheTrackedInitializedStateIsAStackOfSources()
    {
        World w = NewWorld();
        using (w.Enter())
        {
            SSatoms.initialized = INITIALIZATION_INNEW_REGULAR;
            SSatoms.map_loader_begin("loader");
            Assert.Equal((float)INITIALIZATION_INSSATOMS, SSatoms.initialized);
            SSatoms.set_tracked_initalized(INITIALIZATION_INNEW_MAPLOAD, "init");
            Assert.True(Dm.eq(SSatoms.get_initialized_source(), "init"));
            SSatoms.map_loader_stop("loader");
            Assert.Equal((float)INITIALIZATION_INNEW_MAPLOAD, SSatoms.initialized);
            SSatoms.clear_tracked_initalize("init");
            Assert.Equal((float)INITIALIZATION_INNEW_REGULAR, SSatoms.initialized);
            Assert.Null(SSatoms.get_initialized_source());
        }
    }

    [Fact]
    public void InitializeImmediateInitializesBeforeSSatomsIsUp()
    {
        World w = NewWorld();
        using (w.Enter())
        {
            var immediate = Dm.@new<test_atoms_immediate>();
            Assert.True(Dm.truthy(Dm.bitand(immediate.flags_1, INITIALIZED_1)));
            Assert.Equal((float)INITIALIZATION_INSSATOMS, SSatoms.initialized);
        }
    }

    [Fact]
    public void InitializeSetsThePlaneAndTheTypeDefaultsOfAtom()
    {
        World w = NewWorld();
        using (w.Enter())
        {
            var a = Dm.@new<test_atoms_hinted>();
            Assert.Equal((float)GAME_PLANE, a.plane);
            // BYOND's builtin /obj sets layer = OBJ_LAYER below tg's /atom override, and tg does not override it on /obj.
            Assert.Equal(OBJ_LAYER, a.layer);
            // tg's /obj and /atom/movable overrides come after /atom's (rules 10.2, "Builtin types", W4.0).
            Assert.Equal(typeof(obj), a.abstract_type);
            Assert.True(Dm.eq(Dm.bitor(Dm.bitor(TILE_BOUND, PIXEL_SCALE), LONG_GLIDE), a.appearance_flags));
            a.plane = 5;
            SET_PLANE_IMPLICIT(a, GAME_PLANE); // SSmapping.max_plane_offset is 0: the base case
            Assert.Equal((float)GAME_PLANE, a.plane);
        }
    }

    [Fact]
    public void PrepareDeletionQueuesUntilSSatomsIsDone()
    {
        World w = NewWorld();
        using (w.Enter())
        {
            var a = Dm.@new<test_atoms_hinted>();
            SSatoms.prepare_deletion(a);
            Assert.False(QDELETED(a));
            Assert.Equal(1f, SSatoms.queued_deletions.len);
            Waiting.NoSleep(SSatoms.Initialize());
            Assert.True(QDELETED(a));
            Assert.Equal(0f, SSatoms.queued_deletions.len);
        }
    }

    [Fact]
    public void TheAtomSignalWrappersSendTheirSignals()
    {
        World w = NewWorld();
        using (w.Enter())
        {
            var a = Dm.@new<test_atoms_hinted>();
            Assert.True(Dm.eq(a.set_density(TRUE), 0f));
            Assert.Equal(1f, a.density);
            Assert.Null(a.set_density(TRUE));
            Assert.True(Dm.eq(Waiting.NoSleep(a.Exit(null, NORTH)), TRUE));
            Waiting.NoSleep(a.setDir(EAST));
            Assert.Equal((float)EAST, a.dir);
            Assert.True(Dm.eq(a.set_base_pixel_x(4), 0f));
            Assert.Equal(4f, a.pixel_x);
            Assert.True(Dm.eq(a.AllowDrop(), FALSE));
            Assert.True(Dm.eq(a.in_contents_of(typeof(turf)), FALSE));
        }
    }
}
