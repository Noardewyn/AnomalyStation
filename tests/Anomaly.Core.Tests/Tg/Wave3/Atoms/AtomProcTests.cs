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
/// The procs of game/atom/_atom.dm that the atoms package translated (docs/ai/specs/m0-reading/atom.md section 11), from
/// their DM: Destroy, the translated parts of CanPass and has_gravity (the rest waits for the /atom/movable, /turf and
/// /area vars of wave 4: the GAP lines), the movement hooks and the one-line signal wrappers.
/// </summary>
public class AtomProcTests
{
    // A world with SSatoms up and a 2x1x1 map: atoms created here initialize in New.
    private static AtomsTestWorld Up(bool notTranslatedThrows = true)
    {
        var t = new AtomsTestWorld(maxx: 2, maxy: 1, maxz: 1);
        DmWorldState.Of(t.World).NotTranslatedThrows = notTranslatedThrows;
        using (t.World.Enter())
            AtomsTestWorld.InitializeSSatoms();
        t.Trace.Clear();
        return t;
    }

    // The trace without the Initialize lines of the atoms the test creates.
    private static List<string> Events(AtomsTestWorld t) => t.Trace.Where(l => !l.Contains(" Initialize mapload=")).ToList();

    private static datum_test_atoms_listener Listen(datum target, string signal, string handler, string label = "", object returns = null)
    {
        var l = Dm.@new<datum_test_atoms_listener>();
        l.label = label;
        l.returns = returns ?? NONE;
        l.RegisterSignal(target, signal, handler);
        return l;
    }

    // ---- Destroy (_atom.dm:163-207) ----

    [Fact]
    public void DestroyDeletesTheHoldersInTgsOrderClearsTheListsAndQueues()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 1));
            var reagents = Dm.@new<datum_test_atoms_part>();
            // M6 slice 1: tg's /datum/forensics/New qdels a forensics datum created without a parent atom (_forensics.dm:44), so it gets one.
            var forensics = Dm.@new<datum_forensics_test_atoms_part>(a);
            // M4a P3: tg's /datum/storage/New qdels a storage created without a parent (storage.dm:144), so it gets one.
            var storage = Dm.@new<datum_storage_test_atoms_part>(a);
            // a holder of its own: wires qdel themselves when their holder is deleted (_wires.dm:89, on_holder_qdel)
            var wires = Dm.@new<datum_wires_test_atoms_part>(Dm.@new<obj_test_atoms_probe>(locate(1, 1, 1)));
            var ai = Dm.@new<datum_test_atoms_part>();
            var light = Dm.@new<datum_light_source_test_atoms_part>();
            DmList sources = Dm.list(Dm.@new<datum_test_atoms_part>());
            a.reagents = reagents;
            a.forensics = forensics;
            a.atom_storage = storage;
            a.wires = wires;
            a.ai_controller = ai;
            a.light = light;
            a.light_sources = sources;
            a.managed_overlays = Dm.list("managed");
            a.overlays.Add("an overlay");
            a.alternate_appearances = Dm.list();
            a.smoothing_flags = SMOOTH_QUEUED;
            t.Trace.Clear();

            qdel(a);

            Assert.Equal(new[]
            {
                Id(a) + " Destroy",
                Id(reagents) + " part Destroy",
                Id(forensics) + " forensics Destroy",
                Id(storage) + " storage Destroy",
                Id(wires) + " wires Destroy",
                Id(ai) + " part Destroy",
                Id(light) + " light Destroy",
            }, Events(t));
            Assert.Null(a.reagents);
            Assert.Null(a.forensics);
            Assert.Null(a.atom_storage);
            Assert.Null(a.wires);
            Assert.Null(a.ai_controller);
            Assert.Null(a.light);
            Assert.Null(a.orbiters);
            Assert.Null(a.managed_overlays);
            Assert.Equal(0f, a.overlays.len);
            Assert.Same(sources, a.light_sources); // Cut, not replaced (_atom.dm:194-195)
            Assert.Equal(0f, sources.len);
            Assert.True(QDELETED(reagents) && QDELETED(light));
            // SMOOTH_QUEUED: SSicon_smooth.remove_from_queues(src) (_atom.dm:197-198; M3 P4: icon_smooth.dm:88 clears the flag)
            Assert.False(Dm.truthy(Dm.bitand(a.smoothing_flags, SMOOTH_QUEUED)));
            // ..() is /datum/Destroy: QDEL_HINT_QUEUE, so qdel queued it (DV7: it leaves the registry).
            Assert.True(QDELETED(a));
            Assert.Empty(t.Messages);
        }
    }

    [Fact]
    public void DestroyOfABareAtomTouchesNothingElse()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            Assert.True(Dm.eq(a.Destroy(), QDEL_HINT_QUEUE));
            Assert.Null(a.light_sources);
            Assert.DoesNotContain("code/controllers/subsystem/icon_smooth.dm:88", DmWorldState.Of(t.World).DeferredSites);
            Assert.Empty(t.Messages);
        }
    }

    // ---- CanPass (_atom.dm:225), CanAStarPass (:1000) ----

    [Fact]
    public void CanPassIsTrueWhenATriedPassHandlerPermitsPassage()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var wall = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 1));
            var mover = Dm.@new<obj_test_atoms_probe>(locate(2, 1, 1));
            wall.density = TRUE;
            Listen(wall, COMSIG_ATOM_TRIED_PASS, nameof(datum_test_atoms_listener.on_any), "TRIED_PASS", COMSIG_COMPONENT_PERMIT_PASSAGE);
            Assert.True(Dm.eq(Waiting.NoSleep(wall.CanPass(mover, WEST)), TRUE));
            Assert.Equal(new[] { Id(wall) + " TRIED_PASS #" + Id(mover) + " 8 " }, Events(t));
        }
    }

    [Fact]
    public void CanAStarPassLetsMatchingPassFlagsThroughElseItIsNotDensity()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            var info = Dm.@new<datum_can_pass_info>();
            a.density = TRUE;
            Assert.True(Dm.eq(a.CanAStarPass(NORTH, info), FALSE));
            a.pass_flags_self = PASSTABLE;
            info.pass_flags = Dm.bitor(PASSTABLE, PASSGLASS);
            Assert.True(Dm.eq(a.CanAStarPass(NORTH, info), TRUE));
            info.pass_flags = PASSGLASS;
            a.density = FALSE;
            Assert.True(Dm.eq(a.CanAStarPass(NORTH, info), TRUE));
        }
    }

    // ---- has_gravity (_atom.dm:778): nullspace and forced gravity ----

    [Fact]
    public void HasGravityIsFalseInNullspaceWithoutAskingAnyone()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            Listen(a, COMSIG_ATOM_HAS_GRAVITY, nameof(datum_test_atoms_listener.on_has_gravity), returns: 1);
            Assert.True(Dm.eq(a.has_gravity(), FALSE));
            Assert.Empty(Events(t));
        }
    }

    [Fact]
    public void ForcedGravityAsksTheAtomThenTheTurfAndAddsTheLargestToTheNegativeMinimum()
    {
        var t = Up();
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var a = Dm.@new<obj_test_atoms_probe>(floor);
            var mine = Listen(a, COMSIG_ATOM_HAS_GRAVITY, nameof(datum_test_atoms_listener.on_has_gravity), returns: 1);
            var turfs = Listen(floor, COMSIG_TURF_HAS_GRAVITY, nameof(datum_test_atoms_listener.on_has_gravity), returns: -1);
            Assert.True(Dm.eq(a.has_gravity(), 0)); // max(1, -1) + min(min(1, -1), 0) = 1 - 1
            Assert.Equal(new[] { Id(a) + " has_gravity #" + Id(floor), Id(floor) + " has_gravity #" + Id(a) }, Events(t));

            mine.returns = 2;
            turfs.returns = 1;
            Assert.True(Dm.eq(a.has_gravity(), 2)); // two effects of 1 do not add up: max + min(min, 0)

            // TgQuirk: with only negative entries the maximum is negative too, and both are added: -1 + -2.
            mine.returns = -1;
            turfs.returns = -2;
            Assert.True(Dm.eq(a.has_gravity(), -3));
        }
    }

    [Fact]
    public void HasGravityOfAGivenTurfAsksThatTurf()
    {
        var t = Up();
        using (t.World.Enter())
        {
            turf here = locate(1, 1, 1);
            turf there = locate(2, 1, 1);
            var a = Dm.@new<obj_test_atoms_probe>(here);
            Listen(there, COMSIG_TURF_HAS_GRAVITY, nameof(datum_test_atoms_listener.on_has_gravity), returns: 1);
            Listen(here, COMSIG_TURF_HAS_GRAVITY, nameof(datum_test_atoms_listener.on_has_gravity), returns: 5);
            Assert.True(Dm.eq(a.has_gravity(there), 1));
            Assert.Equal(new[] { Id(there) + " has_gravity #" + Id(a) }, Events(t));
        }
    }

    // ---- setDir (_atom.dm:579) ----

    [Fact]
    public void SetDirSendsPreThenDirThenPostAroundTheAssignment()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            Listen(a, COMSIG_ATOM_PRE_DIR_CHANGE, nameof(datum_test_atoms_listener.on_any), "PRE");
            Listen(a, COMSIG_ATOM_DIR_CHANGE, nameof(datum_test_atoms_listener.on_any), "DIR");
            Listen(a, COMSIG_ATOM_POST_DIR_CHANGE, nameof(datum_test_atoms_listener.on_any), "POST");
            Waiting.NoSleep(a.setDir(EAST));
            Assert.Equal((float)EAST, a.dir);
            Assert.Equal(new[] { Id(a) + " PRE 2 4 ", Id(a) + " DIR 2 4 ", Id(a) + " POST 2 4 " }, Events(t));
        }
    }

    [Fact]
    public void ABlockedDirChangeKeepsTheDirAndSendsNothingElse()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            Listen(a, COMSIG_ATOM_PRE_DIR_CHANGE, nameof(datum_test_atoms_listener.on_any), "PRE", COMPONENT_ATOM_BLOCK_DIR_CHANGE);
            Listen(a, COMSIG_ATOM_DIR_CHANGE, nameof(datum_test_atoms_listener.on_any), "DIR");
            Waiting.NoSleep(a.setDir(EAST));
            Assert.Equal((float)SOUTH, a.dir);
            Assert.Equal(new[] { Id(a) + " PRE 2 4 " }, Events(t));
        }
    }

    [Fact]
    public void ABorderObjectQueuesItsNeighboursForSmoothingOnADirChange()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 1));
            var neighbour = Dm.@new<obj_test_atoms_probe>(locate(2, 1, 1));
            neighbour.smoothing_flags = SMOOTH_BITMASK;
            Waiting.NoSleep(a.setDir(EAST));
            Assert.False(Dm.truthy(Dm.bitand(neighbour.smoothing_flags, SMOOTH_QUEUED)));
            a.smoothing_flags = SMOOTH_BORDER_OBJECT;
            Waiting.NoSleep(a.setDir(WEST));
            // QUEUE_SMOOTH_NEIGHBORS (__DEFINES/icon_smoothing.dm:63, M3 P4): QUEUE_SMOOTH of each atom in orange(1, src)
            Assert.True(Dm.truthy(Dm.bitand(neighbour.smoothing_flags, SMOOTH_QUEUED)));
            Assert.True(Dm.@in(neighbour, SSicon_smooth.smooth_queue));
        }
    }

    // ---- Entered, Exit, Exited (_atom.dm:620-645) ----

    [Fact]
    public void EnteredTellsTheContainerThenTheArrival()
    {
        var t = Up();
        using (t.World.Enter())
        {
            turf floor = locate(1, 1, 1);
            var box = Dm.@new<obj_test_atoms_probe>(floor);
            var item = Dm.@new<obj_test_atoms_probe>(box);
            Listen(box, COMSIG_ATOM_ENTERED, nameof(datum_test_atoms_listener.on_any), "ENTERED");
            Listen(item, COMSIG_ATOM_ENTERING, nameof(datum_test_atoms_listener.on_any), "ENTERING");
            DmList old_locs = Dm.list(floor);
            Assert.Null(Waiting.NoSleep(box.Entered(item, floor, old_locs)));
            Assert.Equal(new[]
            {
                Id(box) + " ENTERED #" + Id(item) + " #" + Id(floor) + " list(1)",
                Id(item) + " ENTERING #" + Id(box) + " #" + Id(floor) + " list(1)",
            }, Events(t));
        }
    }

    [Fact]
    public void ExitAllowsUnlessAHandlerBlocksAndExitedTellsTheContainerThenTheLeaver()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var box = Dm.@new<obj_test_atoms_probe>();
            var item = Dm.@new<obj_test_atoms_probe>(box);
            Assert.True(Dm.eq(Waiting.NoSleep(box.Exit(item, NORTH)), TRUE));
            var exit = Listen(box, COMSIG_ATOM_EXIT, nameof(datum_test_atoms_listener.on_any), "EXIT", COMPONENT_ATOM_BLOCK_EXIT);
            Assert.True(Dm.eq(Waiting.NoSleep(box.Exit(item, NORTH)), FALSE));
            exit.returns = NONE;
            Assert.True(Dm.eq(Waiting.NoSleep(box.Exit(item, NORTH)), TRUE));

            t.Trace.Clear();
            Listen(box, COMSIG_ATOM_EXITED, nameof(datum_test_atoms_listener.on_any), "EXITED");
            Listen(item, COMSIG_ATOM_EXITING, nameof(datum_test_atoms_listener.on_any), "EXITING");
            Assert.Null(Waiting.NoSleep(box.Exited(item, NORTH)));
            Assert.Equal(new[] { Id(box) + " EXITED #" + Id(item) + " 1 ", Id(item) + " EXITING #" + Id(box) + " 1 " }, Events(t));
        }
    }

    // ---- the setters (_atom.dm:467, :726-751) ----

    [Fact]
    public void SetDensityReturnsTheOldValueAndSignalsOnlyOnAChange()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            Listen(a, COMSIG_ATOM_DENSITY_CHANGED, nameof(datum_test_atoms_listener.on_any), "DENSITY");
            Assert.Null(a.set_density(FALSE)); // a bare return: '.' is still null
            Assert.Empty(Events(t));
            Assert.True(Dm.eq(a.set_density(TRUE), FALSE));
            Assert.Equal(1f, a.density);
            Assert.Equal(new[] { Id(a) + " DENSITY   " }, Events(t));
        }
    }

    [Fact]
    public void SetBasePixelMovesThePixelOffsetByTheDifference()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            a.pixel_x = 5;
            a.pixel_y = -3;
            Assert.True(Dm.eq(a.set_base_pixel_x(4), 0));
            Assert.Equal(9f, a.pixel_x);
            Assert.True(Dm.eq(a.set_base_pixel_x(1), 4));
            Assert.Equal(6f, a.pixel_x); // pixel_x + base_pixel_x - .
            Assert.Null(a.set_base_pixel_x(1));
            Assert.True(Dm.eq(a.set_base_pixel_y(-2), 0));
            Assert.Equal(-5f, a.pixel_y);
            Assert.Equal(-2f, a.base_pixel_y);
        }
    }

    [Fact]
    public void SetWiresStoresTheWires()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            var w = Dm.@new<datum_wires_test_atoms_part>(a);
            a.set_wires(w);
            Assert.Same(w, a.wires);
        }
    }

    // ---- where things are (_atom.dm:471, :543, :609, :400) ----

    [Fact]
    public void DropLocationClimbsToTheFirstLocThatAllowsDrops()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var container = Dm.@new<obj_test_atoms_probe_container>();
            var bag = Dm.@new<obj_test_atoms_probe>(container);
            var item = Dm.@new<obj_test_atoms_probe>(bag);
            Assert.Same(container, item.drop_location());
            Assert.Same(container, bag.drop_location());
            Assert.Null(container.drop_location()); // nullspace
        }
    }

    [Fact]
    public void InContentsOfTestsTheLocsTypeForAPathAndMembershipOtherwise()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var container = Dm.@new<obj_test_atoms_probe_container>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_atoms_probe>(container);
            var elsewhere = Dm.@new<obj_test_atoms_probe>();
            Assert.True(Dm.eq(item.in_contents_of(typeof(obj_test_atoms_probe_container)), TRUE));
            Assert.True(Dm.eq(item.in_contents_of(typeof(obj)), TRUE));
            Assert.True(Dm.eq(item.in_contents_of(typeof(turf)), FALSE));
            Assert.True(Dm.eq(item.in_contents_of(container), TRUE));
            Assert.True(Dm.eq(item.in_contents_of(elsewhere), FALSE));
            Assert.True(Dm.eq(item.in_contents_of(Dm.list(item)), TRUE));
            Assert.True(Dm.eq(container.in_contents_of(locate(1, 1, 1)), TRUE));
        }
    }

    [Fact]
    public void IsInSpaceAsksTheTurfUnderIt()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var box = Dm.@new<obj_test_atoms_probe>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_atoms_probe>(box);
            Assert.True(Dm.eq(item.isinspace(), FALSE));
            Dm.@new<turf_open_space>(locate(2, 1, 1)); // replaces the floor (BYOND new at a turf)
            var drifter = Dm.@new<obj_test_atoms_probe>(locate(2, 1, 1));
            Assert.True(Dm.eq(drifter.isinspace(), TRUE));
            Assert.True(Dm.eq(Dm.@new<obj_test_atoms_probe>().isinspace(), FALSE)); // nullspace has no turf
        }
    }

    [Fact]
    public void ReturnAirAsksTheLocAndIsNullInNullspace()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var box = Dm.@new<obj_test_atoms_probe>();
            var item = Dm.@new<obj_test_atoms_probe>(box);
            Assert.Null(Waiting.NoSleep(item.return_air()));
            Assert.Null(Waiting.NoSleep(box.return_air()));
        }
    }

    // ---- the signal wrappers ----

    [Fact]
    public void BumpedSendsTheBumpedSignal()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            var b = Dm.@new<obj_test_atoms_probe>();
            Listen(a, COMSIG_ATOM_BUMPED, nameof(datum_test_atoms_listener.on_any), "BUMPED");
            Assert.Null(a.Bumped(b));
            Assert.Equal(new[] { Id(a) + " BUMPED #" + Id(b) + "  " }, Events(t));
        }
    }

    [Fact]
    public void RelaymoveWarnsOnceAPerCooldownUnlessAHandlerBlocks()
    {
        // balloon_alert is a skeleton leaf (modules/balloon_alert, a later milestone): this world logs it instead of throwing.
        var t = Up(notTranslatedThrows: false);
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            var blocker = Listen(a, COMSIG_ATOM_RELAYMOVE, nameof(datum_test_atoms_listener.on_any), "RELAYMOVE", COMSIG_BLOCK_RELAYMOVE);
            Assert.Null(Waiting.NoSleep(a.relaymove(null, NORTH)));
            Assert.Equal(0f, a.buckle_message_cooldown);
            blocker.returns = NONE;
            Waiting.NoSleep(a.relaymove(null, NORTH));
            Assert.Equal(25f, a.buckle_message_cooldown); // world.time + 25 (_atom.dm:507)
        }
        t.World.StepDeciseconds(10);
        using (t.World.Enter())
        {
            var a = (obj_test_atoms_probe)Dm.locate<obj_test_atoms_probe>(world);
            Waiting.NoSleep(a.relaymove(null, NORTH));
            Assert.Equal(25f, a.buckle_message_cooldown);
        }
        t.World.StepDeciseconds(15);
        using (t.World.Enter())
        {
            var a = (obj_test_atoms_probe)Dm.locate<obj_test_atoms_probe>(world);
            Waiting.NoSleep(a.relaymove(null, NORTH));
            Assert.Equal(50f, a.buckle_message_cooldown);
        }
    }

    [Fact]
    public void RelaydriveIsTrueUnlessAHandlerBlocksTheDriver()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            Assert.True(Dm.eq(a.relaydrive(null, NORTH), TRUE));
            Listen(a, COMSIG_RIDDEN_DRIVER_MOVE, nameof(datum_test_atoms_listener.on_any), "DRIVE", COMPONENT_DRIVER_BLOCK_MOVE);
            Assert.True(Dm.eq(a.relaydrive(null, NORTH), FALSE));
        }
    }

    [Fact]
    public void InterceptZImpactReturnsWhatTheHandlersReturnWithOneLevelByDefault()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            DmList falling = Dm.list(a);
            Assert.True(Dm.eq(Waiting.NoSleep(a.intercept_zImpact(falling)), NONE));
            Listen(a, COMSIG_ATOM_INTERCEPT_Z_FALL, nameof(datum_test_atoms_listener.on_any), "ZFALL", 6);
            Assert.True(Dm.eq(Waiting.NoSleep(a.intercept_zImpact(falling)), 6));
            Assert.Equal(new[] { Id(a) + " ZFALL list(1) 1 " }, Events(t));
        }
    }

    [Fact]
    public void AttemptChargeAsksTheSenderWithNoExtraFeesByDefault()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            var sender = Dm.@new<obj_test_atoms_probe>();
            var target = Dm.@new<obj_test_atoms_probe>();
            Listen(sender, COMSIG_OBJ_ATTEMPT_CHARGE, nameof(datum_test_atoms_listener.on_any), "CHARGE", 3);
            Assert.True(Dm.eq(a.attempt_charge(sender, target), 3));
            Assert.Equal(new[] { Id(sender) + " CHARGE #" + Id(target) + " 0 " }, Events(t));
        }
    }

    [Fact]
    public void TamedAddsTheInnateTamedTrait()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            Waiting.NoSleep(a.tamed(null, null));
            Assert.True(HAS_TRAIT_FROM(a, TRAIT_TAMED, INNATE_TRAIT));
        }
    }

    [Fact]
    public void TheEmptyHooksReturnNothingAndUsedForProcessingDeletes()
    {
        var t = Up();
        using (t.World.Enter())
        {
            var a = Dm.@new<obj_test_atoms_probe>();
            Assert.True(Dm.eq(a.AllowDrop(), FALSE));
            Assert.Null(a.get_dumping_location());
            Assert.Null(a.remove_air(5));
            Assert.Null(a.return_analyzable_air());
            Assert.Null(Waiting.NoSleep(a.assume_air(null)));
            Assert.Null(Waiting.NoSleep(a.HasProximity(null)));
            Assert.Null(Waiting.NoSleep(a.return_temperature()));
            Assert.Null(Waiting.NoSleep(a.connect_to_shuttle(TRUE, null, null)));
            Assert.Null(a.update_remote_sight(null, null));
            Waiting.NoSleep(a.UsedforProcessing(null, null, null, null));
            Assert.True(QDELETED(a));
        }
    }
}
