using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// /turf/Initialize (code/game/turfs/turf.dm:139-197), /turf/open/space/Initialize (space_EXPENSIVE.dm:13-45) and
/// assemble_baseturfs (turf.dm:469-516), from the DM: what each base does, in what order, and what only the turf bases do
/// (they do not call /atom/Initialize and set INITIALIZED_1 themselves, scope R14).
/// </summary>
public class TurfInitializeTests
{
    private static List<string> Messages(TurfAreaWorld t) => t.Runtimes.Select(e => e.Message).ToList();

    [Fact]
    public void InitializedFlagIsSetExactlyOncePerTurfBaseWhenSSatomsInitializesTheMap()
    {
        var t = new TurfAreaWorld(4, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_test_turfarea_wall>(locate(2, 1, 1));
            var space = Dm.@new<turf_test_turfarea_space>(locate(3, 1, 1));
            var plain_space = Dm.@new<turf_open_space>(locate(4, 1, 1));
            var floor = (turf_test_turfarea_floor)locate(1, 1, 1);
            foreach (turf T in new turf[] { floor, wall, space, plain_space })
                Assert.False(Dm.truthy(Dm.bitand(T.flags_1, INITIALIZED_1)));

            TurfAreaWorld.InitializeSSatoms();

            foreach (turf T in new turf[] { floor, wall, space, plain_space })
                Assert.True(Dm.truthy(Dm.bitand(T.flags_1, INITIALIZED_1)), T.GetType().Name);
            // one Initialize call each, and no "initialized multiple times" stack_trace from any of the four bases
            Assert.Equal(1, floor.init_calls);
            Assert.Equal(1, wall.init_calls);
            Assert.Equal(1, space.init_calls);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ATurfCreatedAfterSSatomsInitializedIsInitializedOnceInsideNew()
    {
        var t = new TurfAreaWorld(3, 1, 1);
        using (t.World.Enter())
        {
            TurfAreaWorld.InitializeSSatoms();
            var old = locate(2, 1, 1);
            var made = Dm.@new<turf_test_turfarea_floor>(locate(2, 1, 1));
            var wall = Dm.@new<turf_test_turfarea_wall>(locate(3, 1, 1));
            var space = Dm.@new<turf_open_space>(locate(1, 1, 1));

            Assert.Equal(1, made.init_calls);
            Assert.True(Dm.truthy(Dm.bitand(made.flags_1, INITIALIZED_1)));
            Assert.Same(made, locate(2, 1, 1));
            Assert.Equal(1, wall.init_calls);
            Assert.True(Dm.truthy(Dm.bitand(space.flags_1, INITIALIZED_1)));
            // the replaced turf is not initialized again
            Assert.NotSame(made, old);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void DV1ARuntimeInOneTurfsInitializeEndsOnlyThatInitializeAndTheMapGoesOn()
    {
        var t = new TurfAreaWorld(3, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            Dm.@new<turf_test_turfarea_floor_crashing>(locate(2, 1, 1));
            TurfAreaWorld.InitializeSSatoms();

            // the runtime unwinds to InitAtom's boundary (DV1); every other turf is initialized and the flag the crashing
            // turf set before it failed stays set
            foreach (turf T in Dm.each<turf>(block(1, 1, 1, 3, 1, 1)))
                Assert.True(Dm.truthy(Dm.bitand(T.flags_1, INITIALIZED_1)), TurfAreaTrace.D(T));
            var crashed = Assert.Single(t.Runtimes);
            Assert.Contains("runtime in Initialize", crashed.Message);
        }
    }

    [Fact]
    public void ATurfCreatedWithTheOldTurfsSignalTablesIsInitializedWithTheMaploadFlagOnly()
    {
        // /turf/New passes only the loc on (turf.dm:122 `..(loc)`): Initialize gets no extra arguments (the inherited tables)
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            TurfAreaWorld.InitializeSSatoms();
            turf old = locate(2, 1, 1);
            var made = Dm.@new<turf_test_turfarea_floor>(old, null, null);
            Assert.Single(TurfAreaTrace.Of(t.World).Lines, l => l == "Initialize turf(2,1,1) mapload=0 extras=0");
            Assert.Equal(1, made.init_calls);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirkSpaceBasicNewNeverInitializesTheTurfAfterSSatomsIsUp()
    {
        // /turf/open/space/basic/New (space.dm:79, "Do not convert to Initialize") does not call ..(): no /atom/New, so no
        // InitAtom. tg's default world.turf is initialized only by SSatoms' sweep over the world.
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            TurfAreaWorld.InitializeSSatoms();
            var basic = Dm.@new<turf_test_turfarea_space>(locate(1, 1, 1));
            Assert.Equal(0, basic.init_calls);
            Assert.False(Dm.truthy(Dm.bitand(basic.flags_1, INITIALIZED_1)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InitializingATurfTwiceWarnsWithAStackTraceButCarriesOn()
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            Assert.True(Dm.eq(floor.Initialize(TRUE), INITIALIZE_HINT_NORMAL));
            Assert.Empty(t.Runtimes);
            // turf.dm:141-142: the warning is a stack_trace, so Initialize goes on and returns its hint
            Assert.True(Dm.eq(floor.Initialize(TRUE), INITIALIZE_HINT_NORMAL));
            Assert.Single(t.Runtimes);
            Assert.Contains("initialized multiple times", Messages(t)[0]);
            Assert.True(Dm.truthy(Dm.bitand(floor.flags_1, INITIALIZED_1)));
        }
    }

    [Fact]
    public void TgQuirkSpaceInitializeDoesNotWarnWhenCalledTwice()
    {
        // space_EXPENSIVE.dm:18-20: the double-initialization check is under PERFORM_ALL_TESTS, which production leaves off
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_open_space));
        using (t.World.Enter())
        {
            turf space = locate(1, 1, 1);
            Assert.True(Dm.eq(space.Initialize(TRUE), INITIALIZE_HINT_NORMAL));
            Assert.True(Dm.eq(space.Initialize(TRUE), INITIALIZE_HINT_NORMAL));
            Assert.Empty(t.Runtimes);
            Assert.True(Dm.truthy(Dm.bitand(space.flags_1, INITIALIZED_1)));
        }
    }

    [Fact]
    public void TurfInitializeDoesNotCallAtomInitialize()
    {
        // /atom/Initialize (atoms_initializing_EXPENSIVE.dm:131-132) turns post_init_icon_state into icon_state; /turf/Initialize
        // (SHOULD_CALL_PARENT(FALSE), turf.dm:140) copies only part of atom's work, and not that
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_test_turfarea_floor_integral));
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            string before = floor.icon_state;
            floor.Initialize(TRUE);
            Assert.Equal(before, floor.icon_state);
            Assert.NotEqual("set_by_atom_initialize", floor.icon_state);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InitializeCopiesIntegrityAndDirectionalOpacityFromAtomInitialize()
    {
        var t = new TurfAreaWorld(2, 1, 1, turfType: typeof(turf_test_turfarea_floor_integral));
        using (t.World.Enter())
        {
            turf lit = locate(1, 1, 1);
            Assert.Equal(NONE, lit.directional_opacity);
            lit.Initialize(TRUE);
            Assert.Equal(ALL_CARDINALS, lit.directional_opacity);   // turf.dm:187-188
            Assert.True(Dm.eq(lit.atom_integrity, 77));                   // turf.dm:190-191: uses_integrity
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AWallInitializesWithFullDirectionalOpacityAndAFloorWithNone()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(2, 1, 1));
            var floor = locate(1, 1, 1);
            TurfAreaWorld.InitializeSSatoms();
            Assert.Equal(ALL_CARDINALS, wall.directional_opacity);  // /turf/closed has opacity = TRUE (_closed.dm:6)
            Assert.Equal(NONE, floor.directional_opacity);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InitializeCutsVisContentsThatWereInheritedFromTheReplacedTurf()
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var thing = Dm.@new<obj_test_turfarea_sitter>();
            Dm.append(floor.vis_contents, thing);
            Assert.Equal(1, floor.vis_contents.len);
            floor.Initialize(TRUE);
            Assert.Equal(0, floor.vis_contents.len);   // turf.dm:164-165
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirkSpaceInitializeLeavesVisContentsAlone()
    {
        // space_EXPENSIVE.dm leaves out what space "does not use": the vis_contents cut, baseturfs, levelupdate, occupants
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_open_space));
        using (t.World.Enter())
        {
            turf space = locate(1, 1, 1);
            var thing = Dm.@new<obj_test_turfarea_sitter>();
            Dm.append(space.vis_contents, thing);
            space.Initialize(TRUE);
            Assert.Equal(1, space.vis_contents.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TurfInitializeAssemblesBaseturfsAndSpaceInitializeDoesNot()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var space = Dm.@new<turf_open_space>(locate(2, 1, 1));
            turf floor = locate(1, 1, 1);
            floor.Initialize(TRUE);
            space.Initialize(TRUE);
            Assert.Same(typeof(turf_baseturf_bottom), floor.baseturfs);
            // space's baseturfs var keeps tg's class default, a single type that was never walked
            Assert.Same(typeof(turf_baseturf_bottom), space.baseturfs);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ASmoothingTurfParsesItsGroupsAndIsQueuedForSmoothing()
    {
        // SETUP_SMOOTHING and `if (smoothing_flags & USES_SMOOTHING) QUEUE_SMOOTH(src)` (turf.dm:167-171), translated in M3 (P4)
        var t = new TurfAreaWorld(2, 1, 1, turfType: typeof(turf_test_turfarea_floor_smooth));
        using (t.World.Enter())
        {
            turf smooth = locate(1, 1, 1);
            Dm.mask(ref smooth.smoothing_flags, Dm.bitnot(SMOOTH_QUEUED));
            smooth.smoothing_groups = SMOOTH_GROUP_TURF_OPEN;
            smooth.canSmoothWith = SMOOTH_GROUP_TURF_OPEN;
            smooth.Initialize(TRUE);
            Assert.IsType<DmList>(smooth.smoothing_groups); // PARSE_SMOOTHING_GROUPS
            Assert.IsType<DmList>(smooth.canSmoothWith);    // PARSE_CAN_SMOOTH_WITH
            Assert.True(Dm.truthy(Dm.bitand(smooth.smoothing_flags, SMOOTH_QUEUED))); // SSicon_smooth.add_to_queue
            Assert.True(Dm.@in(smooth, SSicon_smooth.smooth_queue));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void APlainTurfNeitherParsesGroupsNorQueuesForSmoothing()
    {
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            locate(1, 1, 1).Initialize(TRUE);
            IReadOnlyList<string> sites = DmWorldState.Current.DeferredSites;
            Assert.DoesNotContain(sites, s => s.Contains("icon_smoothing.dm"));
            Assert.DoesNotContain(sites, s => s.Contains("icon_smooth.dm:74"));
        }
    }

    // ---- levelupdate (turf.dm:517-526) ----

    [Fact]
    public void InitializeSendsCOMSIG_OBJ_HIDEWithTheUnderfloorAccessibilityToEveryInitializedObj()
    {
        var t = new TurfAreaWorld(2, 1, 1, turfType: typeof(turf_test_turfarea_floor_interactable));
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var initialized = Dm.@new<obj_test_turfarea_sitter>(floor);
            initialized.name = "initialized";
            Dm.combine(ref initialized.flags_1, INITIALIZED_1);
            var raw = Dm.@new<obj_test_turfarea_sitter>(floor);
            raw.name = "raw";
            var listener = Dm.@new<datum_test_turfarea_listener>();
            listener.label = "hide";
            listener.RegisterSignal(initialized, COMSIG_OBJ_HIDE, nameof(datum_test_turfarea_listener.on_any));
            listener.RegisterSignal(raw, COMSIG_OBJ_HIDE, nameof(datum_test_turfarea_listener.on_any));

            floor.Initialize(TRUE);

            // only the initialized obj hears it, and with the turf's underfloor_accessibility
            Assert.Equal(new[] { "hide initialized(" + Dm.str(UNDERFLOOR_INTERACTABLE) + ",null,null,null)" }, TurfAreaTrace.Of(t.World).Lines.Where(l => l.StartsWith("hide ")));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirkSpaceNeverHidesAnythingUnderFloor()
    {
        // turf.dm:523-524: /turf/open/space/levelupdate is a bare return
        var t = new TurfAreaWorld(1, 1, 1, turfType: typeof(turf_open_space));
        using (t.World.Enter())
        {
            turf space = locate(1, 1, 1);
            var obj = Dm.@new<obj_test_turfarea_sitter>(space);
            Dm.combine(ref obj.flags_1, INITIALIZED_1);
            var listener = Dm.@new<datum_test_turfarea_listener>();
            listener.label = "hide";
            listener.RegisterSignal(obj, COMSIG_OBJ_HIDE, nameof(datum_test_turfarea_listener.on_any));
            space.levelupdate();
            space.Initialize(TRUE);
            Assert.DoesNotContain(TurfAreaTrace.Of(t.World).Lines, l => l.StartsWith("hide"));
        }
    }

    // ---- planes and multiz (turf.dm:146-157) ----

    [Fact]
    public void WithPlaneOffsetsAHigherZTurfMovesItsPlaneDownAndABlacklistedPlaneStays()
    {
        var t = new TurfAreaWorld(1, 1, 3, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            SSmapping.max_plane_offset = 2;
            SSmapping.z_level_to_plane_offset = Dm.list(0, 1, 2);
            SSmapping.plane_offset_blacklist = Dm.list();
            turf z1 = locate(1, 1, 1), z2 = locate(1, 1, 2), z3 = locate(1, 1, 3);
            float plane = z1.plane;
            z1.Initialize(TRUE);
            z2.Initialize(TRUE);
            z3.Initialize(TRUE);
            Assert.Equal(plane, z1.plane);
            Assert.Equal(plane - PLANE_RANGE, z2.plane);
            Assert.Equal(plane - 2 * PLANE_RANGE, z3.plane);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ABlacklistedPlaneIsNotOffsetByTurfInitialize()
    {
        var t = new TurfAreaWorld(1, 1, 2, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            SSmapping.max_plane_offset = 1;
            SSmapping.z_level_to_plane_offset = Dm.list(0, 1);
            turf z2 = locate(1, 1, 2);
            SSmapping.plane_offset_blacklist = Dm.list(Dm.kv(Dm.text("[]", z2.plane), 1));
            float plane = z2.plane;
            z2.Initialize(TRUE);
            Assert.Equal(plane, z2.plane);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void SpaceInitializeOffsetsPLANE_SPACEWithoutLookingAtTheBlacklist()
    {
        var t = new TurfAreaWorld(1, 1, 2, turfType: typeof(turf_open_space));
        using (t.World.Enter())
        {
            SSmapping.max_plane_offset = 1;
            SSmapping.z_level_to_plane_offset = Dm.list(0, 1);
            SSmapping.plane_offset_blacklist = Dm.list(Dm.kv(Dm.text("[]", PLANE_SPACE), 1));
            turf z1 = locate(1, 1, 1), z2 = locate(1, 1, 2);
            z1.Initialize(TRUE);
            z2.Initialize(TRUE);
            // space_EXPENSIVE.dm:23-25: "We make the assumption that the space plane will never be blacklisted"
            Assert.Equal(PLANE_SPACE, z1.plane);
            Assert.Equal(PLANE_SPACE - PLANE_RANGE, z2.plane);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TurfInitializeTellsTheTurfsAboveAndBelowAboutTheNewTurf()
    {
        var t = new TurfAreaWorld(1, 1, 3, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            SSmapping.max_plane_offset = 2;
            SSmapping.z_level_to_plane_offset = Dm.list(0, 1, 2);
            SSmapping.plane_offset_blacklist = Dm.list();
            // SSmapping's manage_z_level (mapping.dm:237-239): one list per z of LARGEST_Z_LEVEL_INDEX flags, indexed by
            // Z_LEVEL_UP (1) and Z_LEVEL_DOWN (2)
            SSmapping.multiz_levels = Dm.list(Dm.list(TRUE, FALSE), Dm.list(TRUE, TRUE), Dm.list(FALSE, TRUE));
            turf z1 = locate(1, 1, 1), z2 = locate(1, 1, 2), z3 = locate(1, 1, 3);
            var listener = Dm.@new<datum_test_turfarea_listener>();
            listener.label = "multiz_new";
            listener.RegisterSignal(z1, COMSIG_TURF_MULTIZ_NEW, nameof(datum_test_turfarea_listener.on_any));
            listener.RegisterSignal(z3, COMSIG_TURF_MULTIZ_NEW, nameof(datum_test_turfarea_listener.on_any));

            z2.Initialize(TRUE);

            // multiz_turf_new(src, DOWN) on the turf above (z3), multiz_turf_new(src, UP) on the turf below (z1):
            // the signal's source is the turf that is told, its arguments the new turf and the direction (turf.dm:277)
            Assert.Equal(new[]
            {
                "multiz_new turf(1,1,3)(turf(1,1,2)," + Dm.str(DOWN) + ",null,null)",
                "multiz_new turf(1,1,1)(turf(1,1,2)," + Dm.str(UP) + ",null,null)",
            }, TurfAreaTrace.Of(t.World).Lines.Where(l => l.StartsWith("multiz_new")));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void WithoutPlaneOffsetsTheTurfsAboveAndBelowAreNotTold()
    {
        var t = new TurfAreaWorld(1, 1, 2, turfType: typeof(turf_test_turfarea_floor));
        using (t.World.Enter())
        {
            SSmapping.multiz_levels = Dm.list(Dm.list(TRUE, FALSE), Dm.list(FALSE, TRUE));
            turf z1 = locate(1, 1, 1), z2 = locate(1, 1, 2);
            var listener = Dm.@new<datum_test_turfarea_listener>();
            listener.label = "multiz_new";
            listener.RegisterSignal(z2, COMSIG_TURF_MULTIZ_NEW, nameof(datum_test_turfarea_listener.on_any));
            z1.Initialize(TRUE);   // max_plane_offset is 0: the whole block is skipped (turf.dm:146)
            Assert.DoesNotContain(TurfAreaTrace.Of(t.World).Lines, l => l.StartsWith("multiz_new"));
        }
    }

    // ---- assemble_baseturfs (turf.dm:469-516) ----

    [Fact]
    public void ABaseturfChainIsWalkedFromTheTurfsBaseturfDownToTheSelfReferentialOne()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var a = Dm.@new<turf_test_turfarea_chain_a>(locate(1, 1, 1));
            object result = Waiting.NoSleep(a.assemble_baseturfs());
            // list(B), then C and D are inserted at the front: bottom first, the nearest baseturf last (turf.dm:505-511)
            var list = Assert.IsType<DmList>(a.baseturfs);
            Assert.Equal(3, list.len);
            Assert.Same(typeof(turf_test_turfarea_chain_d), list[1f]);
            Assert.Same(typeof(turf_test_turfarea_chain_c), list[2f]);
            Assert.Same(typeof(turf_test_turfarea_chain_b), list[3f]);
            // the return value is the list that was built, which the string list wrapper only interned
            Assert.IsType<DmList>(result);
            Assert.Equal(3, ((DmList)result).len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ASingleSelfReferentialBaseturfStaysATypeAndIsCached()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            var d1 = Dm.@new<turf_test_turfarea_chain_d>(locate(1, 1, 1));
            var d2 = Dm.@new<turf_test_turfarea_chain_d>(locate(2, 1, 1));
            object first = Waiting.NoSleep(d1.assemble_baseturfs());
            Assert.Same(typeof(turf_test_turfarea_chain_d), d1.baseturfs);
            Assert.Same(typeof(turf_test_turfarea_chain_d), first);
            // second turf: the cache answers (turf.dm:493-499 stores the type itself under its own key)
            Waiting.NoSleep(d2.assemble_baseturfs());
            Assert.Same(typeof(turf_test_turfarea_chain_d), d2.baseturfs);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TurfsWithTheSameBaseturfShareOneInternedList()
    {
        var t = new TurfAreaWorld(3, 1, 1);
        using (t.World.Enter())
        {
            var a1 = Dm.@new<turf_test_turfarea_chain_a>(locate(1, 1, 1));
            var a2 = Dm.@new<turf_test_turfarea_chain_a>(locate(2, 1, 1));
            var other_type = Dm.@new<turf_test_turfarea_chain_a2>(locate(3, 1, 1));
            Waiting.NoSleep(a1.assemble_baseturfs());
            Waiting.NoSleep(a2.assemble_baseturfs());   // the cache hit copies the premade list (turf.dm:488-490), the string list interns it
            Waiting.NoSleep(other_type.assemble_baseturfs());
            Assert.Same(a1.baseturfs, a2.baseturfs);
            // the cache is keyed by the top of the chain (turf.dm:514), so another type with the same baseturf hits it too
            Assert.Same(a1.baseturfs, other_type.baseturfs);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnAssembledBaseturfsListIsNotAssembledAgain()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var a = Dm.@new<turf_test_turfarea_chain_a>(locate(1, 1, 1));
            Waiting.NoSleep(a.assemble_baseturfs());
            object assembled = a.baseturfs;
            // turf.dm:480-481: "the current baseturfs value is already a list/assembled"
            Assert.Null(Waiting.NoSleep(a.assemble_baseturfs()));
            Assert.Same(assembled, a.baseturfs);
        }
    }

    [Fact]
    public void AFakeBaseturfListIsAppliedAsItIs()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            Waiting.NoSleep(floor.assemble_baseturfs(Dm.list(typeof(turf_baseturf_bottom), typeof(turf_open_indestructible_plating))));
            // turf.dm:473-476: a list is applied and returned without walking
            var list = Assert.IsType<DmList>(floor.baseturfs);
            Assert.Equal(2, list.len);
            Assert.Same(typeof(turf_open_indestructible_plating), list[2f]);
        }
    }

    [Fact]
    public void AFakeBaseturfTypeRebuildsTheChainFromThatType()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            Waiting.NoSleep(floor.assemble_baseturfs(typeof(turf_test_turfarea_chain_b)));
            // current_target = the given type; its own baseturf is walked, B is the top of the list
            var list = Assert.IsType<DmList>(floor.baseturfs);
            Assert.Equal(3, list.len);
            Assert.Same(typeof(turf_test_turfarea_chain_d), list[1f]);
            Assert.Same(typeof(turf_test_turfarea_chain_c), list[2f]);
            Assert.Same(typeof(turf_test_turfarea_chain_b), list[3f]);
        }
    }

    [Fact]
    public void ANullBaseturfsGetsTheTypeDefaultAndAStackTrace()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var wall = Dm.@new<turf_closed_indestructible>(locate(1, 1, 1));
            wall.baseturfs = null;
            Waiting.NoSleep(wall.assemble_baseturfs());
            // turf.dm:482-485: initial(baseturfs) of the type, here the plating over the bottom
            var list = Assert.IsType<DmList>(wall.baseturfs);
            Assert.Same(typeof(turf_baseturf_bottom), list[1f]);
            Assert.Same(typeof(turf_open_indestructible_plating), list[2f]);
            Assert.Single(t.Runtimes);
            Assert.Contains("baseturfs var was null for", Messages(t)[0]);
        }
    }
}
