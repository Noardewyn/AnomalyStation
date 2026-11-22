// Harness for ported tg unit tests. Mirrors tgstation (snapshot 1d99bd7f): code/modules/unit_tests/unit_test.dm
// (/datum/unit_test, /proc/RunUnitTest) and the assertion macros of code/modules/unit_tests/_unit_tests.dm.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// tg compiles its unit tests only with UNIT_TESTS defined, which the production build does not define (rules 1.7), so
// /datum/unit_test is not translated into src/Core/Tg: this harness is its test-side counterpart, with tg's names, so
// that a ported test reads like its .dm file:
//
//     // tg: code/modules/unit_tests/some_test.dm:1 (/datum/unit_test/some_test; class named by rules 2.3)
//     public sealed class datum_unit_test_some_test : TgUnitTest
//     {
//         public override async DmTask Run()
//         {
//             obj_item thing = allocate<obj_item>();
//             TEST_ASSERT_EQUAL(thing.force, 0, "should start harmless");
//         }
//     }
//
// Each concrete subclass is one xunit test (the inherited [Fact] RunUnitTest), run in a fresh World.
using System.Runtime.CompilerServices;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

/// <summary>
/// The base of a ported tg unit test (tg <c>/datum/unit_test</c>, unit_test.dm:35). Override <see cref="Run"/> with
/// the test's <c>/Run()</c>; use <see cref="allocate"/> for the instances the test creates and the <c>TEST_*</c>
/// macros for its checks. The inherited <see cref="RunUnitTest"/> fact does what tg's <c>/proc/RunUnitTest</c>
/// (unit_test.dm:206) does for one test, in a fresh <see cref="World"/>: create the test, run it (stepping the world
/// while <see cref="Run"/> sleeps), collect the fail reasons, tear it down.
/// </summary>
public abstract class TgUnitTest
{
    // tg: code/modules/unit_tests/_unit_tests.dm:58-70 (inactive in production, so not in the generated Defines).
    public const float UNIT_TEST_PASSED = 0;
    public const float UNIT_TEST_FAILED = 1;
    public const float UNIT_TEST_SKIPPED = 2;
    public const float TEST_PRE = 0;
    public const float TEST_DEFAULT = 1;
    /// After most test steps, used for tests that run long so shorter issues can be noticed faster
    public const float TEST_LONGER = 10;
    public const float TEST_CREATE_AND_DESTROY = 9001;
    /**
     * For tests that rely on create and destroy having iterated through every (tangible) atom so they don't have to do something similar.
     * Keep in mind tho that create and destroy will absolutely break the test platform, anything that relies on its shape cannot come after it.
     */
    // tg: code/modules/unit_tests/_unit_tests.dm:68
    public const float TEST_AFTER_CREATE_AND_DESTROY = Defines.INFINITY;

    // Unit test bitflags (tg: code/modules/unit_tests/_unit_tests.dm:72-78)

    /// If any unit test has this bitflag, only unit tests with UNIT_TEST_FOCUS will run.
    public const int UNIT_TEST_FOCUS = 1 << 0;
    /// This unit test only runs on specially designated unit test maps (Should only ever be one).
    public const int UNIT_TEST_DEBUG_MAP_ONLY = 1 << 1;
    public const int UNIT_TEST_BASIC = UNIT_TEST_DEBUG_MAP_ONLY;
    public const int UNIT_TEST_MAP_TEST = Defines.NONE;

    /// <summary>
    /// How long <see cref="RunUnitTest"/> steps the world for a <see cref="Run"/> that sleeps, in deciseconds of game
    /// time (tg has no per-test limit; CI kills the whole run). A test that needs longer overrides it.
    /// </summary>
    protected virtual float MaxRunDeciseconds => 10 * 60 * 10;

    /// <summary>The world seed of this test (each run gets a fresh world with it).</summary>
    protected virtual ulong Seed => 1;

    // ---- tg vars (unit_test.dm:38-63) ----

    /// <summary>
    /// Behavior flags for this unit test. The harness runs every test (there is no focus and no map to choose yet),
    /// so it reads none of them.
    /// </summary>
    public float test_flags = UNIT_TEST_BASIC;
    /// The priority of the test, the larger it is the later it fires
    public float priority = TEST_DEFAULT;
    /// How many times this unit test will run. Use the TEST_REPEAT() macro
    public float times_to_run = 1;
    /// If this test has passed or not
    public float succeeded = TRUE;
    /// <summary>
    /// The bottom left floor turf of the testing zone. Null until the port loads tg's unit test map
    /// (<c>_maps/templates/unit_tests.dmm</c>, with the map loader): allocated atoms are created nowhere until then.
    /// </summary>
    public object run_loc_floor_bottom_left;
    /// The top right floor turf of the testing zone (null until the test map is loaded, see above)
    public object run_loc_floor_top_right;
    /// A list of instances created by this unit test. Use allocate()
    public DmList allocated;
    /// Lazy list of why this unit test failed.
    public DmList fail_reasons;

    /// <summary>The notices of <see cref="TEST_NOTICE"/> (tg logs them to the CI output).</summary>
    public readonly List<string> notices = new();

    /// <summary>The world of the current run.</summary>
    protected World world_under_test { get; private set; }

    // GLOB.current_test (unit_test.dm:14): runtimes fail the current test (error_handler.dm:166-169).
    private bool _current;

    // ---- the runner (/proc/RunUnitTest, unit_test.dm:206-267) ----

    /// <summary>Runs this tg unit test <see cref="times_to_run"/> times, each in a fresh world, and fails on any failure.</summary>
    [Fact]
    public void RunUnitTest()
    {
        var failures = new List<string>();
        float loop_count = times_to_run;
        for (int i = 1; i <= loop_count; i++)
        {
            string message = RunOnce();
            if (message != null)
                failures.Add(loop_count > 1 ? Dm.text("run [] of []:\n[]", i, loop_count, message) : message);
        }
        if (failures.Count > 0)
            Assert.Fail(GetType().Name + " failed:\n" + string.Join("\n", failures));
    }

    /// <summary>One run of the test in a fresh world; null when it passed, else tg's failure log text.</summary>
    internal string RunOnce()
    {
        succeeded = TRUE;
        fail_reasons = null;
        allocated = null;
        notices.Clear();
        var teardownRuntimes = new List<string>();
        var w = new World(Seed);
        w.RuntimeErrorSink = e =>
        {
            if (e is TestAssertionReturn) return; // a TEST_ASSERT inside a detached block: its Fail is recorded already
            if (_current)
                RuntimeFail(e);
            else
                teardownRuntimes.Add(RuntimeLine(e));
        };
        world_under_test = w;
        // The M7 supermatter slice: the crystal starts its hum at creation (supermatter.dm:228), and a looping sound with sound
        // tokens reads its files' lengths (SSsounds.get_sound_length, sounds.dm:251), which tg's rust-g always knows: the host's
        // sound length table (M4b audio slice 5), as TgTestWorld and TgToyRoom install it.
        M4bAudio.TestSoundLengths.Install(w);
        using (w.Enter())
        {
            // World bootstrap stand-in: tg creates GLOB and runs its InitGlobal* procs at world start, and its MC
            // creates SSgarbage, SSdcs and SSobj (SUBSYSTEM_DEF, PROCESSING_SUBSYSTEM_DEF, NEW_SS_GLOBAL) before any test runs.
            // The wave-3 bootstrap (TgWorld.Boot) does all of it once every wave-3 package is merged; until then the
            // harness builds GLOB with its C# constructor (test code only) instead of Dm.@new, because GLOB's New
            // (controllers/globals.dm:11) queues a timer (QDEL_IN) and the harness world has no SStimer, and runs GLOB's
            // Initialize (:48), which calls every translated InitGlobal proc (GLOB.sortInstance, which sortTim uses, ...).
            GLOB__var = new datum_controller_global_vars();
            Dm.@new<datum_controller_subsystem_materials>(); // M5 slice 2: GLOB's make_datum_reference_lists reads SSmaterials' recipes (global_lists.dm:93)
            Dm.@new<datum_controller_subsystem_accessories>(); // M10x slice D2: tg makes it at PreInit, before GLOB (sprite_accessories.dm:47); bodies read its lists
            Dm.@new<datum_controller_subsystem_garbage>(); // M5 slice 8: before GLOB, as the Master makes it (master.cs:186-198): init_crafting_recipes qdels the recipes out of scope
            Waiting.NoSleep(GLOB.Initialize());
            AtomsStandIn.Install(); // SSatoms, which /atom/New reads (wave 3), before Master.Initialize: atoms defer Initialize
            Dm.@new<datum_controller_subsystem_processing_dcs>();
            // Wave 4: /obj/Destroy stops processing on SSobj (objs.dm:74), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_processing_obj>();
            // M2 P2: a human's status effects (the organless blindness of setup_organless_effects, human.dm:57) start and stop
            // processing on SSfastprocess, SSprocessing or SSpriority_effects, which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_processing_fastprocess>();
            Dm.@new<datum_controller_subsystem_processing>();
            Dm.@new<datum_controller_subsystem_processing_priority_effects>();
            // M3 I1b: an atmospherics machine's Initialize starts processing on SSair (atmosmachinery.dm:102), which tg's MC
            // always creates.
            Dm.@new<datum_controller_subsystem_air>();
            // M4b slice 2: a firelock's Destroy has its merge group queue a refresh (addtimer, _merger.dm:80), and tg's MC
            // always creates SStimer (its timers never fire here: the harness runs no MC).
            Dm.@new<datum_controller_subsystem_timer>();
            // M5x slice 5: a singularity starts processing on SSsinguloprocess (singularity.dm:61), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_processing_singuloprocess>();
            // syndicate N1a: a nuke reads the security level in its Initialize (_nuclear_bomb.dm:66), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_security_level>();
            // The M7 delamination slice (G6): a resonance cascade's crystal mass starts processing on SSsupermatter_cascade
            // (cascade_delam_objects.dm:25), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_processing_supermatter_cascade>();
            // The M7 projectiles slice: a fired projectile starts processing on SSprojectiles (projectile.dm:817) and its Destroy
            // stops it (:285), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_processing_projectiles>();
            // M6 slice 1: a blood decal starts drying on SSblood_drying (blood.dm:43), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_processing_blood_drying>();
            // M7 tools slice: a full belt takes its tools from SSwardrobe (belt.dm:51), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_wardrobe>();
            // M7 slice G1: a solar panel, tracker or control computer registers for COMSIG_SUN_MOVED on SSsun (solar.dm:38), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_sun>();
            Dm.@new<datum_controller_subsystem_persistence>(); // an incident display reads the delamination counter (incident_display.dm:102), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_polling>(); // Syndicate X2: a ghost-controllable mob polls the ghosts (ghost_direct_control.dm:81), tg's MC always creates it
            // M7 lighting slice L1: a trash item on a station level (the burnt flare, the melted candle) records its mess on SSblackbox (trash.dm:13), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_blackbox>();
            // M7 lumcount: SSlighting, which tg's MC always creates; not initialized, so the turfs have no lighting mark and count as
            // lit (lighting_turf.dm:22); a lumcount test initializes it itself.
            Dm.@new<datum_controller_subsystem_lighting>();
            // M9 slice D: a radio, headset or signaler registers its frequencies with SSradio (radio.dm:168), which tg's MC always creates.
            Dm.@new<datum_controller_subsystem_radio>();
            Dm.@new<datum_controller_subsystem_cameras>(); // M10 slice D: a security camera registers itself (camera.dm:124), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_verbs>().Initialize()); // silicons SI4: a human and an AI are given their verbs (human.dm:2, ai.dm:79), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_pai>(); // silicons SI6: a pAI card joins SSpai's card list (card.dm:32), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_skills>().Initialize()); // gaps SL1: a mind's skills read the skill singletons (GetSkillRef), tg's MC always creates it
            Waiting.NoSleep(Dm.@new<datum_controller_subsystem_processing_instruments>().Initialize()); // silicons SI6c: an instrument's song takes its instrument from SSinstruments (_song.dm:182), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_persistent_paintings>(); // silicons SI4d: a painting frame registers itself (paintings.dm:585), tg's MC always creates it
            Dm.@new<datum_controller_subsystem_processing_transport>(); // D325: an elevator panel, button or indicator and a lift platform look the lifts up in it, tg's MC always creates it
            // M9 slice E: a head's PDA looks for its fax on SSmachines (role_tablet_presets.dm:34) and a PDA's programs come from
            // SSmodular_computers; the message server and NtOS's log stamp the round time from SSticker (time.dm:8). tg's MC always creates them.
            Dm.@new<datum_controller_subsystem_machines>();
            Dm.@new<datum_controller_subsystem_ticker>();
            Dm.@new<datum_controller_subsystem_modular_computers>();
            // engineering E15, E16: a plumbing component stops processing on SSplumbing when deleted (_plumbing.dm:114) and the crafter
            // starts processing on SSmanufacturing at Initialize (crafter.dm:25); tg's MC always creates both.
            Dm.@new<datum_controller_subsystem_processing_plumbing>();
            Dm.@new<datum_controller_subsystem_processing_manufacturing>();

            // var/datum/unit_test/test = new test_path
            New();
            // GLOB.current_test = test
            _current = true;
            try
            {
                DmTask run = Run();
                // A runtime in Run ends Run and fails the test (error_handler.dm:166-169). Raised before Run first
                // slept, it is in the returned task; raised later, the Kernel reports it to the sink above when Run
                // finishes, because nothing awaits Run.
                if (run.IsFaulted && run.Exception is not TestAssertionReturn)
                    RuntimeFail(run.Exception);
                long limit = w.Tick + Scheduler.TicksCovering(MaxRunDeciseconds);
                while (!run.IsCompleted && w.Tick < limit)
                    w.Step();
                if (!run.IsCompleted)
                    Fail(Dm.text("Run() did not finish within [] deciseconds of game time", MaxRunDeciseconds), "TgUnitTest", 0);
            }
            finally
            {
                // if(test.priority < TEST_CREATE_AND_DESTROY) test.restore_atmos(): no atmos in the port yet.
                // GLOB.current_test = null
                _current = false;
            }

            // qdel(test)
            Destroy();
        }
        world_under_test = null;

        var log_entry = new List<string>();
        for (int reasonID = 1; reasonID <= length(fail_reasons); reasonID++)
        {
            var reason = (DmList)fail_reasons[reasonID];
            log_entry.Add(Dm.text("\tFAILURE #[]: [] at []:[]", reasonID, reason[1], reason[2], reason[3]));
        }
        // tg logs a runtime of the teardown (after GLOB.current_test = null) instead of failing the test; the port
        // fails on it too, so that a teardown runtime cannot pass unnoticed.
        foreach (string line in teardownRuntimes)
            log_entry.Add("\tRUNTIME IN TEARDOWN: " + line);
        if (notices.Count > 0 && log_entry.Count > 0)
            log_entry.AddRange(notices.Select(n => "\tNOTICE: " + n));
        return log_entry.Count == 0 && Dm.truthy(succeeded) ? null : string.Join("\n", log_entry);
    }

    // ---- /datum/unit_test procs ----

    /// <summary>
    /// tg <c>/datum/unit_test/New()</c> (unit_test.dm:68): <c>allocated = list()</c>. The test map (the
    /// <c>reservation</c> z-level, the landmark turfs and their checks) is not loaded yet, see
    /// <see cref="run_loc_floor_bottom_left"/>. A test that needs setup overrides it and calls the base first.
    /// </summary>
    public virtual void New()
    {
        allocated = Dm.list();
    }

    /// <summary>
    /// tg <c>/datum/unit_test/Destroy()</c> (unit_test.dm:92): <c>QDEL_LIST(allocated)</c>. Clearing the test
    /// z-level's turfs waits for the test map. A test that needs teardown overrides it and calls the base last.
    /// </summary>
    public virtual void Destroy()
    {
        QDEL_LIST(allocated);
    }

    /// <summary>tg <c>/datum/unit_test/proc/Run()</c> (unit_test.dm:105): the test. It may sleep; the runner steps the world.</summary>
    public virtual async DmTask Run()
    {
        TEST_FAIL(Dm.text("[]/Run() called parent or not implemented", GetType().Name));
    }

    /// <summary>tg <c>/datum/unit_test/proc/Fail(reason = "No reason", file = "OUTDATED_TEST", line = 1)</c> (unit_test.dm:108).</summary>
    public void Fail(object reason = null, object file = null, object line = null)
    {
        reason ??= "No reason";
        file ??= "OUTDATED_TEST";
        line ??= 1;
        succeeded = FALSE;

        if (!istext(reason))
            reason = Dm.text("FORMATTED: []", reason != null ? reason : "NULL");

        LAZYADD(ref fail_reasons, Dm.list(Dm.list(reason, file, line)));
    }

    /// <summary>
    /// tg <c>/datum/unit_test/proc/allocate(type, ...)</c> (unit_test.dm:118): creates an instance of
    /// <paramref name="type"/> with the given <c>New</c> arguments (an atom at <see cref="run_loc_floor_bottom_left"/>
    /// when no location is given) and qdels it when the test is over.
    /// </summary>
    public object allocate(Type type, params object[] arguments)
    {
        arguments ??= new object[] { null };
        if (priority > TEST_CREATE_AND_DESTROY) //I'm not using TEST_ASSERT here since these are just numbers that tell nothing useful about the problem.
        {
            TEST_FAIL("allocate() was called for a unit test after 'create_and_destroy' has finished. The unit test room is no longer a reliable testing ground for atoms.");
            return null; //you deserve runtime errors for it
        }
        DmList args = Dm.list(arguments);
        if (ispath(type, typeof(atom)))
        {
            if (!Dm.truthy(args.len))
                args = Dm.list(run_loc_floor_bottom_left);
            else if (args[1] == null)
                args[1] = run_loc_floor_bottom_left;
        }
        object instance = null;
        // Byond will throw an index out of bounds if arguments is empty in that arglist call. Sigh
        if (Dm.truthy(length(args)))
            instance = Dm.@new(type, Dm.arglist(args));
        else
            instance = Dm.@new(type);
        Dm.append(ref allocated, instance);
        return instance;
    }

    /// <summary><see cref="allocate(Type, object[])"/> with a static type.</summary>
    public T allocate<T>(params object[] arguments) where T : class => allocate(typeof(T), arguments) as T;

    // ---- the assertion macros (_unit_tests.dm:6-56) ----
    // DM's TEST_ASSERT* macros `return Fail(...)` from the proc that uses them. Here they record the failure and then
    // throw TestAssertionReturn, which the runner catches at the end of Run: the same for a check in Run itself, while
    // a check in a helper proc ends Run too (in DM only the helper returns; the test fails either way). The file and
    // line are the use site's (tg's __FILE__ and __LINE__), as the C# compiler gives them.

    /// For advanced cases, fail unconditionally but don't return (so a test can return multiple results)
    public void TEST_FAIL(object reason = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        => Fail(Dm.truthy(reason) ? reason : "No reason", SourcePath(file), line);

    /// Asserts that a condition is true
    /// If the condition is not true, fails the test
    public void TEST_ASSERT(bool assertion, object reason = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
    {
        if (!assertion)
            Return(Dm.text("Assertion failed: []", Dm.truthy(reason) ? reason : "No reason"), file, line);
    }

    /// Asserts that a parameter is not null
    public void TEST_ASSERT_NOTNULL(object a, object reason = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
    {
        if (a == null)
            Return(Dm.text("Expected non-null value: []", Dm.truthy(reason) ? reason : "No reason"), file, line);
    }

    /// Asserts that a parameter is null
    public void TEST_ASSERT_NULL(object a, object reason = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
    {
        if (a != null)
            Return(Dm.text("Expected null value but received []: []", a, Dm.truthy(reason) ? reason : "No reason"), file, line);
    }

    /// Asserts that the two parameters passed are equal, fails otherwise
    /// Optionally allows an additional message in the case of a failure
    public void TEST_ASSERT_EQUAL(object a, object b, object message = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
    {
        object lhs = a;
        object rhs = b;
        if (!Dm.eq(lhs, rhs))
            Return(Dm.text("Expected [] to be equal to [].[]", lhs == null ? "null" : lhs, rhs == null ? "null" : rhs, Dm.truthy(message) ? Dm.text(" []", message) : ""), file, line);
    }

    /// Asserts that the two parameters passed are not equal, fails otherwise
    /// Optionally allows an additional message in the case of a failure
    public void TEST_ASSERT_NOTEQUAL(object a, object b, object message = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
    {
        object lhs = a;
        object rhs = b;
        if (Dm.eq(lhs, rhs))
            Return(Dm.text("Expected [] to not be equal to [].[]", lhs == null ? "null" : lhs, rhs == null ? "null" : rhs, Dm.truthy(message) ? Dm.text(" []", message) : ""), file, line);
    }

    /// Logs a noticable message on GitHub, but will not mark as an error.
    /// Use this when something shouldn't happen and is of note, but shouldn't block CI.
    /// Does not mark the test as failed.
    public void TEST_NOTICE(TgUnitTest source, object message, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        => source.log_for_test(message, "notice", SourcePath(file), line);

    /// <summary>
    /// tg <c>/datum/unit_test/proc/log_for_test(text, priority, file, line)</c> (unit_test.dm:185): a line in GitHub's
    /// annotation syntax. tg writes it to world.log; the harness keeps it in <see cref="notices"/>.
    /// </summary>
    public void log_for_test(object text, object priority, object file, object line)
    {
        // var/map_name = SSmapping.current_map.map_name: no map is loaded yet (see run_loc_floor_bottom_left).
        string map_name = null;

        // Need to escape the text to properly support newlines.
        string annotation_text = replacetext(Dm.str(text), "%", "%25");
        annotation_text = replacetext(annotation_text, "\n", "%0A");

        notices.Add(Dm.text("::[] file=[],line=[],title=[]: []::[]", priority, file, line, map_name, GetType().Name, annotation_text));
    }

    // `return Fail(...)` of the TEST_ASSERT* macros.
    private void Return(string reason, string file, int line)
    {
        Fail(reason, SourcePath(file), line);
        throw new TestAssertionReturn(reason);
    }

    // error_handler.dm:161-169: GLOB.current_test.Fail("[main_line]\n[desclines]", file = E.file, line = E.line).
    private void RuntimeFail(Exception e)
    {
        var dm = e as DmRuntimeException;
        Fail(RuntimeLine(e), (object)dm?.DmFile ?? "runtime", dm?.DmLine ?? 0);
    }

    private static string RuntimeLine(Exception e)
    {
        var dm = e as DmRuntimeException;
        // A C# exception of the port (a null reference, a bad cast) or a shim runtime without a DM position: its first frames
        // say where it was.
        string where = dm?.DmFile == null && e.StackTrace != null
            ? " [" + string.Join(" < ", e.StackTrace.Split('\n').Take(3).Select(f => f.Trim())) + "]"
            : string.Empty;
        return "Runtime in " + (dm?.DmFile ?? "?") + "," + (dm?.DmLine ?? 0) + ": " + e.GetType().Name + ": " + e.Message + where;
    }

    // The use site relative to the repository (tests/...), not the machine's absolute path.
    private static string SourcePath(string file)
    {
        if (string.IsNullOrEmpty(file)) return "?";
        string normalized = file.Replace('\\', '/');
        int at = normalized.LastIndexOf("/tests/", StringComparison.Ordinal);
        return at >= 0 ? normalized.Substring(at + 1) : normalized;
    }

    /// <summary>Ends <see cref="Run"/> after a failed TEST_ASSERT* (the macros' <c>return</c>); never a DM runtime.</summary>
    public sealed class TestAssertionReturn : Exception
    {
        public TestAssertionReturn(string reason) : base(reason)
        {
        }
    }
}
