using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Movable.Chain;

namespace Anomaly.Core.Tests.Tg.Wave4.Movable;

/// <summary>
/// The container side of the Move chain: important_recursive_contents kept by /atom/movable/Entered and Exited
/// (atoms_movable.dm:993-1030), hearing and area sensitivity (:1032-1089), /atom/movable/Destroy (:215-273) and Initialize
/// (:149-213), movement_detector across container nesting (T12, move-chain.md section 6) and the small procs of the file.
/// Written from the DM; the channels are the lazy associative lists of tg ("LAZYINITLIST", "UNSETEMPTY").
/// </summary>
public class MoveContainerTests
{
    private const string HEARING = RECURSIVE_CONTENTS_HEARING_SENSITIVE;
    private const string AREA = RECURSIVE_CONTENTS_AREA_SENSITIVE;

    private static object[] Channel(atom_movable holder, string channel)
        => holder.important_recursive_contents == null ? Array.Empty<object>() : EngineTestHelpers.Items(holder.important_recursive_contents[channel]);

    // ---- hearing sensitivity ----

    [Fact]
    public void BecomingHearingSensitiveAddsTheMovableToItsOwnAndEveryContainersChannelButNotTheTurfs()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var crate = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var box = Dm.@new<obj_test_movable_thing>(crate);
            var item = Dm.@new<obj_test_movable_thing>(box);
            item.become_hearing_sensitive();

            Assert.True(HAS_TRAIT_FROM(item, TRAIT_HEARING_SENSITIVE, TRAIT_GENERIC));
            foreach (atom_movable holder in new[] { item, box, crate })
                Assert.Equal(new object[] { item }, Channel(holder, HEARING));
            Assert.Null(locate(1, 1, 1)._listen_lookup); // nothing went to the turf: it is not a movable
            // the spatial grid (M4b audio slice 1): each holder is keyed for the hearing channel, and the item, whose grid has not
            // initialized, waits in its pre-init queue (add_grid_membership, spatial_gridmap.dm:333)
            foreach (atom_movable holder in new[] { item, box, crate })
                Assert.Equal(HEARING, holder.spatial_grid_key);
            Assert.True(Dm.@in(item, SSspatial_grid.waiting_to_add_by_type[HEARING]));
            Assert.False(Dm.@in(box, SSspatial_grid.waiting_to_add_by_type[HEARING]));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ASecondSourceOfHearingSensitivityDoesNotListTheMovableTwiceAndOnlyTheLastSourceRemovesIt()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var box = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_movable_thing>(box);

            item.become_hearing_sensitive();
            item.become_hearing_sensitive("radio"); // "we don't wanna be in important_recursive_contents twice" (:1034-1036)
            Assert.Equal(new object[] { item }, Channel(box, HEARING));
            Assert.True(HAS_TRAIT_FROM(item, TRAIT_HEARING_SENSITIVE, "radio"));

            item.lose_hearing_sensitivity("radio"); // the generic source is still there: nothing is removed
            Assert.True(HAS_TRAIT(item, TRAIT_HEARING_SENSITIVE));
            Assert.Equal(new object[] { item }, Channel(box, HEARING));

            item.lose_hearing_sensitivity(); // the last source: the channel goes and the lists become null (UNSETEMPTY)
            Assert.False(HAS_TRAIT(item, TRAIT_HEARING_SENSITIVE));
            Assert.Null(box.important_recursive_contents);
            Assert.Null(item.important_recursive_contents);

            item.lose_hearing_sensitivity(); // not sensitive: a no-op (:1056)
            Assert.Null(item.important_recursive_contents);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void LosingHearingSensitivityKeepsTheOtherHearersOfTheSameContainer()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var box = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var a = Dm.@new<obj_test_movable_thing>(box);
            var b = Dm.@new<obj_test_movable_thing>(box);
            a.become_hearing_sensitive();
            b.become_hearing_sensitive();
            Assert.Equal(new object[] { a, b }, Channel(box, HEARING));

            a.lose_hearing_sensitivity();

            Assert.Equal(new object[] { b }, Channel(box, HEARING));
            Assert.Null(a.important_recursive_contents);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- area sensitivity ----

    [Fact]
    public void AreaSensitivityIsAListedChannelInEveryContainerAndTheTraitComesFromItsSources()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var crate = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_movable_thing>(crate);

            item.become_area_sensitive();
            item.become_area_sensitive("pda"); // the channel is added only for the first source (:1075)
            Assert.Equal(new object[] { item }, Channel(crate, AREA));
            Assert.Equal(new object[] { item }, Channel(item, AREA));
            Assert.True(HAS_TRAIT_FROM(item, TRAIT_AREA_SENSITIVE, "pda"));

            item.lose_area_sensitivity("pda");
            Assert.Equal(new object[] { item }, Channel(crate, AREA));
            item.lose_area_sensitivity();
            Assert.Null(crate.important_recursive_contents);
            Assert.Null(item.important_recursive_contents);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- Entered and Exited keep the channels of the containers ----

    [Fact]
    public void ASensitiveThingPutIntoAContainerIsListedInItAndInEveryContainerAroundIt()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var crate = Dm.@new<obj_test_movable_thing>(here);
            var box = Dm.@new<obj_test_movable_thing>(here);
            var item = Dm.@new<obj_test_movable_thing>(here);
            item.become_hearing_sensitive();
            item.become_area_sensitive();
            Force(box, crate);
            Assert.Null(box.important_recursive_contents);

            Force(item, box); // box.Entered(item): the item's channels go to the box and, nested, the crate (:1014-1030)

            Assert.Equal(new object[] { item }, Channel(box, HEARING));
            Assert.Equal(new object[] { item }, Channel(crate, HEARING));
            Assert.Equal(new object[] { item }, Channel(box, AREA));
            Assert.Equal(new object[] { item }, Channel(crate, AREA));
            Assert.Equal(new object[] { item }, Channel(item, HEARING)); // its own is unchanged
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ALeavingSensitiveThingIsRemovedFromEveryContainerAndTheListsBecomeNullWhenEmpty()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var crate = Dm.@new<obj_test_movable_thing>(here);
            var box = Dm.@new<obj_test_movable_thing>(crate);
            var item = Dm.@new<obj_test_movable_thing>(box);
            var other = Dm.@new<obj_test_movable_thing>(crate);
            item.become_hearing_sensitive();
            other.become_hearing_sensitive();
            Assert.Equal(new object[] { item, other }, Channel(crate, HEARING)); // in the order they became sensitive (`+= list(src)`)

            Force(item, there); // box.Exited(item), whose nested locs are the box and the crate

            Assert.Null(box.important_recursive_contents);
            Assert.Equal(new object[] { other }, Channel(crate, HEARING)); // the other hearer stays
            Assert.Equal(new object[] { item }, Channel(item, HEARING));

            Force(other, there);
            Assert.Null(crate.important_recursive_contents); // empty channel: removed, then the whole lazy list
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AContainerCarriedAsAWholeKeepsItsListsAndAMovedInnerBoxTakesItsHearersAlong()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var outer = Dm.@new<obj_test_movable_thing>(here);
            var inner = Dm.@new<obj_test_movable_thing>(outer);
            var hearer1 = Dm.@new<obj_test_movable_thing>(inner);
            var hearer2 = Dm.@new<obj_test_movable_thing>(inner);
            var hearer3 = Dm.@new<obj_test_movable_thing>(outer);
            foreach (var h in new[] { hearer1, hearer2, hearer3 })
                h.become_hearing_sensitive();
            Assert.Equal(3, Channel(outer, HEARING).Length);

            Step(outer, EAST); // a turf-to-turf move of the outermost box changes no list
            Assert.Equal(3, Channel(outer, HEARING).Length);
            Assert.Equal(2, Channel(inner, HEARING).Length);

            Force(inner, there); // inner leaves outer: its hearers (a list of two) are subtracted from outer's
            Assert.Equal(new object[] { hearer3 }, Channel(outer, HEARING));
            Assert.Equal(new object[] { hearer1, hearer2 }, Channel(inner, HEARING));

            Force(inner, outer); // and comes back: the union adds them in order
            Assert.Equal(new object[] { hearer3, hearer1, hearer2 }, Channel(outer, HEARING));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AnEnteredTwiceForTheSameArrivalListsItOnlyOnceBecauseTheChannelIsAUnion()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var box = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            item.become_hearing_sensitive();

            // `recursive_contents[channel] |= arrived.important_recursive_contents[channel]` (:1029). The stale Entered of
            // move-chain.md 3g is such a second call for a thing the container already lists.
            Waiting.NoSleep(box.Entered(item, locate(2, 1, 1)));
            Waiting.NoSleep(box.Entered(item, locate(2, 1, 1)));

            Assert.Equal(new object[] { item }, Channel(box, HEARING));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void EveryChannelFlowsThroughTheContainersAndOnlyTheSpatialGridChannelsCallTheGrid()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1), there = locate(2, 1, 1);
            var box = Dm.@new<obj_test_movable_thing>(here);
            var item = Dm.@new<obj_test_movable_thing>(here);
            // channels the other milestones fill (clients M2, storage M5): written raw, as their procs would
            item.important_recursive_contents = Dm.list(
                Dm.kv(RECURSIVE_CONTENTS_CLIENT_MOBS, Dm.list(item)),
                Dm.kv(RECURSIVE_CONTENTS_ACTIVE_STORAGE, Dm.list(item)));
            Force(item, box);

            Assert.Equal(new object[] { item }, Channel(box, RECURSIVE_CONTENTS_CLIENT_MOBS));
            Assert.Equal(new object[] { item }, Channel(box, RECURSIVE_CONTENTS_ACTIVE_STORAGE));
            // add_grid_awareness for the client channel only, when the container's list was empty (:1024-1027)
            Assert.Equal(RECURSIVE_CONTENTS_CLIENT_MOBS, box.spatial_grid_key);

            Force(item, there);

            Assert.Null(box.important_recursive_contents);
            Assert.Equal("", box.spatial_grid_key); // remove_grid_awareness (:1007-1010): no channel left
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void AThingWithNoChannelsLeavesAndEntersContainersWithoutTouchingTheirLists()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var box = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var item = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));

            Force(item, box);
            Force(item, locate(2, 1, 1));

            Assert.Null(box.important_recursive_contents); // `if(!LAZYLEN(arrived.important_recursive_contents)) return` (:1015)
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- Destroy ----

    [Fact]
    public void DeletingASensitiveThingInsideABoxCleansTheBoxAndTheThingsOwnList()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var crate = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var box = Dm.@new<obj_test_movable_thing>(crate);
            var item = Dm.@new<obj_test_movable_thing>(box);
            item.become_hearing_sensitive();
            item.become_area_sensitive();

            qdel(item);

            // moveToNullspace's Exited updates the containers BEFORE the thing's own list is nulled (:260-264): no ref of the
            // deleted thing stays hung in a container.
            Assert.Null(box.important_recursive_contents);
            Assert.Null(crate.important_recursive_contents);
            Assert.Null(item.important_recursive_contents);
            Assert.Null(item.loc);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void DeletingABoxDeletesItsContentsInOrderAndThenMovesTheBoxToNullspace()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            turf here = locate(1, 1, 1);
            var box = Dm.@new<obj_test_movable_witness>(here);
            var c1 = Dm.@new<obj_test_movable_witness>(box);
            var c2 = Dm.@new<obj_test_movable_witness>(box);
            var c3 = Dm.@new<obj_test_movable_witness>(box);
            var nested = Dm.@new<obj_test_movable_witness>(c2); // contents of contents go too (their own Destroy)
            map.Trace.Clear();

            qdel(box);

            Assert.True(QDELETED(box) && QDELETED(c1) && QDELETED(c2) && QDELETED(c3) && QDELETED(nested));
            Assert.Null(box.loc);
            Assert.Empty(EngineTestHelpers.Items(box.contents));
            // each content leaves for nullspace in `contents` order (the loop reads a snapshot, :258-259); the nested one
            // first because c2's Destroy deletes its contents before c2's own move; the box's own move comes last (:261)
            Assert.Equal(new[] { c1, nested, c2, c3, box }.Select(o => "witness_moved " + D(o)).ToArray(),
                Lines(map.Trace, "witness_moved").Select(l => string.Join(" ", l.Split(' ').Take(2))).ToArray());
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void DeletingAPullerOrAPulledThingLetsGoOfTheOtherEndAndTheSignalsStillReachTheOneBeingDeleted()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var puller = Dm.@new<mob_test_movable_mob>(locate(1, 1, 1));
            var crate = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            On(crate, COMSIG_ATOM_NO_LONGER_PULLED, "no_longer_pulled");
            On(puller, COMSIG_ATOM_NO_LONGER_PULLING, "no_longer_pulling");
            Waiting.NoSleep(puller.start_pulling(crate, GRAB_PASSIVE));
            map.Trace.Clear();

            // atoms_movable.dm:238-241: `if(pulledby) pulledby.stop_pulling()` runs before `..()` clears the signal tables
            qdel(crate);

            Assert.Null(puller.pulling);
            Assert.Equal(new[] { "no_longer_pulled", "no_longer_pulling" }, Kinds(map.Trace));

            var crate2 = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            Waiting.NoSleep(puller.start_pulling(crate2, GRAB_PASSIVE));
            On(crate2, COMSIG_ATOM_NO_LONGER_PULLED, "no_longer_pulled2");
            map.Trace.Clear();
            qdel(puller); // `if(pulling) stop_pulling()`
            Assert.Null(crate2.pulledby);
            Assert.Equal(1, Count(map.Trace, "no_longer_pulled2"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Theory]
    [InlineData(ATMOS_PASS_DENSITY, true, true, ATMOS_PASS_YES, true)]  // blocks air by its density: restored, the turf is told
    [InlineData(ATMOS_PASS_DENSITY, false, true, ATMOS_PASS_DENSITY, false)] // not dense: it never blocked
    [InlineData(ATMOS_PASS_NO, false, true, ATMOS_PASS_YES, true)]
    [InlineData(ATMOS_PASS_YES, true, true, ATMOS_PASS_YES, false)]
    [InlineData(ATMOS_PASS_DENSITY, true, false, ATMOS_PASS_DENSITY, false)] // not on a turf (`isturf(loc)`)
    public void DestroyRestoresTheAirFlowOfAMovableThatBlockedItOnATurf(int pass, bool dense, bool on_turf, int expected, bool turf_told)
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var holder = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            atom_movable thing = Dm.@new<obj_test_movable_block>(on_turf ? (atom)locate(1, 1, 1) : holder);
            thing.density = dense ? TRUE : FALSE;
            thing.can_atmos_pass = pass;
            // M4b slice 1: air_update_turf is translated (LINDA_system.dm:166); once SSair is initialized the turf's
            // air_update_turf(TRUE) recalculates its adjacency, which sends COMSIG_TURF_CALCULATED_ADJACENT_ATMOS
            if (SSair == null)
                Dm.@new<datum_controller_subsystem_air>();
            SSair.initialized = TRUE;
            On(locate(1, 1, 1), COMSIG_TURF_CALCULATED_ADJACENT_ATMOS, "turf_told");

            qdel(thing);

            Assert.Equal(expected, thing.can_atmos_pass);
            // the turf is told exactly when air_update_turf is reached
            Assert.Equal(turf_told ? 1 : 0, Count(map.Trace, "turf_told"));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void DestroyDeletesTheDriftHandlerAndTheMovePacketAndClearsTheVisContents()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var other = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            var drift = Dm.@new<datum_drift_handler_test_movable>();
            drift.parent = thing; // what tg's drift_handler/New sets (drift_handler.dm:21)
            var packet = Dm.@new<datum_movement_packet>(thing); // movement_packet/New(parent) sets thing.move_packet (move_manager.dm:66)
            thing.drift_handler = drift;
            Dm.append(thing.vis_contents, other);

            qdel(thing);

            Assert.True(QDELETED(drift));
            Assert.Null(thing.drift_handler); // QDEL_NULL (:216-218)
            Assert.True(QDELETED(packet));
            Assert.Null(thing.move_packet);
            Assert.Equal(0, thing.vis_contents.Count); // `vis_contents.Cut()` (:271-272)
            Assert.False(QDELETED(other));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- Initialize ----

    [Fact]
    public void InitializeSharesOneFactionListBetweenMovablesWithTheSameFactionsAndLeavesAnEmptyOneAlone()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var a = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var b = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var c = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var d = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            a.faction = Dm.list("neutral", "station");
            b.faction = Dm.list("neutral", "station");
            c.faction = Dm.list("neutral");
            d.faction = Dm.list();

            AssertNumber(INITIALIZE_HINT_NORMAL, a.Initialize(FALSE));
            b.Initialize(FALSE);
            c.Initialize(FALSE);
            var empty = d.faction;
            d.Initialize(FALSE);

            // `faction = string_list(faction)` (:150-151): cached lists are shared by their contents
            Assert.Same(a.faction, b.faction);
            Assert.NotSame(a.faction, c.faction);
            Assert.Same(empty, d.faction); // LAZYLEN is 0: not replaced
            Assert.Equal(new object[] { "neutral", "station" }, EngineTestHelpers.Items(a.faction));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void InitializeAddsTheLightingElementDefersTheUniqueEmissiveBlockerAndAddsTheOverlayLight()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            // Off, so the component registers no spatial grid cells (this world has no SSspatial_grid).
            Dm.@new<datum_controller_subsystem_processing_dcs>(); // the light_blocking element needs SSdcs, which tg's MC always creates
            var opaque = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            opaque.opacity = TRUE;
            opaque.light_system = OVERLAY_LIGHT_BEAM;
            opaque.light_on = FALSE;
            opaque.Initialize(FALSE);
            // M7 lumcount: the light_blocking element (atoms_movable.dm:206) makes the turf opaque (lighting_turf.dm:125)
            Assert.True(Dm.@in(opaque, locate(1, 1, 1).opacity_sources));
            Assert.Equal(ALL_CARDINALS, locate(1, 1, 1).directional_opacity);
            var beam = Assert.IsType<datum_component_overlay_lighting>(opaque.GetComponent(typeof(datum_component_overlay_lighting)));
            Assert.Equal(TRUE, beam.directional);
            Assert.Equal(TRUE, beam.beam);

            var plain = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            plain.light_system = OVERLAY_LIGHT;
            plain.light_on = FALSE;
            plain.Initialize(FALSE);
            var round = Assert.IsType<datum_component_overlay_lighting>(plain.GetComponent(typeof(datum_component_overlay_lighting)));
            Assert.Equal(FALSE, round.directional);
            var directional = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            directional.light_system = OVERLAY_LIGHT_DIRECTIONAL;
            directional.light_on = FALSE;
            directional.Initialize(FALSE);
            var cone = Assert.IsType<datum_component_overlay_lighting>(directional.GetComponent(typeof(datum_component_overlay_lighting)));
            Assert.Equal(TRUE, cone.directional);
            Assert.Equal(FALSE, cone.beam);

            var unique = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            unique.blocks_emissive = EMISSIVE_BLOCK_UNIQUE; // DEVIATION(DV12): a deferred presentation leaf since M3 P3 (the lead's decision)
            unique.Initialize(FALSE);
            Assert.Contains("code/game/atoms_movable.dm:172", DmWorldState.Current.DeferredSites);
            Assert.Null(unique.em_block);
        }
    }

    [Fact]
    public void SetExplosionBlockMovesTheExplosiveResistanceByTheDifferenceAndSendsTheChange()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var thing = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            thing.explosive_resistance = 5;
            On(thing, COMSIG_MOVABLE_EXPLOSION_BLOCK_CHANGED, "block_changed");
            map.Trace.Clear();

            thing.set_explosion_block(3);
            Assert.Equal(3f, thing.explosion_block);
            Assert.Equal(8f, thing.explosive_resistance);
            thing.set_explosion_block(1);
            Assert.Equal(6f, thing.explosive_resistance);
            Assert.Equal(new[] { "block_changed " + D(thing) + " 0 3 null", "block_changed " + D(thing) + " 3 1 null" }, map.Trace);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void ThePushAndCrushHooksOfAnObjAnswerTrue()
    {
        // /atom/movable's hooks answer FALSE (atoms_movable.dm:1475, :1488); /obj's answer TRUE (obj_defense.dm:89-98, M5 slice 1).
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            var a = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            var b = Dm.@new<obj_test_movable_thing>(locate(2, 1, 1));
            AssertNumber(TRUE, Waiting.NoSleep(a.force_push(b, silent: TRUE))); // silent: visible_message needs SSspatial_grid, which this fixture world has not
            AssertNumber(TRUE, Waiting.NoSleep(b.force_pushed(a)));
            // move_crushed's collision damage plays the attack sound (no sound system in this fixture world): IntegrityTests

            Assert.Null(a.get_cell(b));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    // ---- T12: movement_detector across container nesting, on the real chain ----

    private sealed class Scene
    {
        public turf A, B;
        public obj_test_movable_thing outer, inner, item;
        public datum_movement_detector detector;
    }

    private static Scene Detect(EngineWorld map)
    {
        var s = new Scene { A = locate(1, 1, 1), B = locate(2, 1, 1) };
        s.outer = Dm.@new<obj_test_movable_thing>(s.A);
        s.inner = Dm.@new<obj_test_movable_thing>(s.outer);
        s.item = Dm.@new<obj_test_movable_thing>(s.inner);
        var recorder = Dm.@new<datum_test_movable_recorder>();
        s.detector = Dm.@new<datum_movement_detector>(s.item, Dm.@new<datum_callback>(recorder, nameof(datum_test_movable_recorder.on_tracked_moved)));
        map.Trace.Clear();
        return s;
    }

    // The detector's callback lines of the trace (without the fixture floors' Enter records).
    private static List<string> Tracked(EngineWorld map) => map.Trace.Where(l => l.StartsWith("tracked_moved ")).ToList();

    private static bool Hooked(datum d) => Dm.truthy(d._listen_lookup?[COMSIG_MOVABLE_MOVED]);

    [Fact]
    public void T12_MovingTheOuterBoxWithAStepReportsItWithTheTrackedItemInside()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            Scene s = Detect(map);
            Assert.True(Hooked(s.item) && Hooked(s.inner) && Hooked(s.outer));

            AssertNumber(TRUE, Step(s.outer, EAST));

            Assert.Equal(new[] { "tracked_moved " + D(s.item) + " " + D(s.outer) + " " + D(s.A) + " 4" }, Tracked(map));
            Assert.True(Hooked(s.inner) && Hooked(s.outer)); // a turf oldloc unhooks nothing
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T12_TakingTheInnerBoxOutUnhooksTheOuterBoxAndTheItemLeavingUnhooksTheInnerOne()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            Scene s = Detect(map);

            Force(s.inner, s.A); // the inner box leaves the outer one onto the turf: forceMove's Moved (NONE, forced)
            Assert.Equal(new[] { "tracked_moved " + D(s.item) + " " + D(s.inner) + " " + D(s.outer) + " 0" }, Tracked(map));
            Assert.False(Hooked(s.outer));
            Assert.True(Hooked(s.inner));
            Step(s.outer, EAST);
            Assert.Single(Tracked(map)); // the outer box no longer carries the item

            Force(s.item, s.A);
            Assert.Equal("tracked_moved " + D(s.item) + " " + D(s.item) + " " + D(s.inner) + " 0", Tracked(map)[^1]);
            Assert.False(Hooked(s.inner));
            Step(s.inner, EAST);
            Assert.Equal(2, Tracked(map).Count);
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T12_PuttingTheItemIntoANewContainerHooksTheNewChainAndAStepOfTheNewOuterBoxIsReported()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            Scene s = Detect(map);
            Force(s.item, s.A);
            var crate = Dm.@new<obj_test_movable_thing>(s.B);
            var shell = Dm.@new<obj_test_movable_thing>(s.B);
            Force(crate, shell);
            map.Trace.Clear();

            Force(s.item, crate); // from a turf into a crate in a shell: the new chain (crate, shell) is hooked
            Assert.True(Hooked(crate) && Hooked(shell));
            Step(shell, WEST);

            Assert.Equal(new[]
            {
                "tracked_moved " + D(s.item) + " " + D(s.item) + " " + D(s.A) + " 0",
                "tracked_moved " + D(s.item) + " " + D(shell) + " " + D(s.B) + " 8",
            }, Tracked(map));
            MovableWorld.AssertNoRuntimes(map);
        }
    }

    [Fact]
    public void T12_DestroyingTheDetectorUnhooksTheItemAndEveryContainer()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            Scene s = Detect(map);
            qdel(s.detector);
            Assert.Null(s.detector.tracked);
            Assert.False(Hooked(s.item) || Hooked(s.inner) || Hooked(s.outer));
            Step(s.outer, EAST);
            Assert.Empty(Tracked(map));
            MovableWorld.AssertNoRuntimes(map);
        }
    }
}
