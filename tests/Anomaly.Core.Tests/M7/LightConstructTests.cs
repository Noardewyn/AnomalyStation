using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 lighting slice L1 (D168): light fixture frames (light_construct.dm, light_wallframes.dm, light.dm:433-470). A frame from
/// a hand is mounted on the wall; a cable wires it (:83), a screwdriver closes it into an empty fixture that keeps its cell
/// (:143), a tube lights it; the same screwdriver on an empty fixture opens it back into a wired frame (light.dm:433-452),
/// a wirecutter unwires a frame (:167), a wrench takes an empty frame apart into sheets (:123), and a smashed fixture leaves a
/// damaged frame with a loose cable (light.dm:454-461).
/// </summary>
public class LightConstructTests
{
    private const int Second = 20;
    private static ChatCapture Chat;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";

    private static (AudioRoom R, mob_living_carbon_human Bob) Build(ulong seed, Action<World> extras = null)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            Dm.@new<turf_open_floor_plating>(locate(5, 2, 1)); // a frame is mounted from a floor tile
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).static_lighting = TRUE; // the test room is unlit, a station room is lit (light_wallframes.dm:29)
            extras?.Invoke(w);
        });
        mob_living_carbon_human bob = r.Join(5, 2, "Bob");
        PresentationSinks.Of(r.World).Messages = Chat = new ChatCapture();
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        T item = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
        return item;
    });

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, Left));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    private static T Find<T>(AudioRoom r, atom where) where T : class
        => r.In(() => Dm.each(get_turf(where).contents).OfType<T>().SingleOrDefault());

    [Fact]
    public void AFrameFromTheHandIsMountedOnTheWallWiredClosedAndLitThenOpenedBackAndTakenApart()
    {
        var (r, bob) = Build(9101);
        turf wall = r.In(() => locate(5, 1, 1) as turf);
        r.In(() => Waiting.NoSleep(bob.setDir(SOUTH)));

        // the wall frame (light_wallframes.dm, wall_mounted.dm): clicked on the wall the user faces
        var frameItem = Hold<obj_item_wallframe_light_fixture>(r, bob);
        Click(r, bob, wall);
        obj_structure_light_construct frame = Find<obj_structure_light_construct>(r, bob);
        r.In(() =>
        {
            Assert.True(frame != null, string.Join("|", Chat.Html));
            Assert.True(QDELETED(frameItem));
            Assert.Equal(SOUTH, Dm.num(frame.dir));
            Assert.Equal(LIGHT_CONSTRUCT_EMPTY, frame.stage);
            Assert.Equal("tube-construct-stage1", frame.icon_state);
            Assert.Equal(200f, Dm.num(frame.max_integrity));
            Assert.Contains("an empty frame with no wires", string.Join("|", Dm.each(Waiting.NoSleep(frame.examine(bob))).Select(Dm.str)), StringComparison.Ordinal);
        });

        // a tube is refused until the frame is finished (:97)
        Hold<obj_item_light_tube>(r, bob);
        Click(r, bob, frame);
        r.In(() => Assert.Equal(LIGHT_CONSTRUCT_EMPTY, frame.stage));

        // one length of cable wires it (:105)
        var coil = Hold<obj_item_stack_cable_coil>(r, bob);
        float coilAmount = r.In(() => Dm.num(coil.amount));
        Click(r, bob, frame);
        r.In(() =>
        {
            Assert.Equal(LIGHT_CONSTRUCT_WIRED, frame.stage);
            Assert.Equal("tube-construct-stage2", frame.icon_state);
            Assert.Equal(coilAmount - 1, Dm.num(coil.amount));
        });

        // a power cell hooked up inside (:86), then a screwdriver closes the casing into an empty fixture (:143)
        var cell = Hold<obj_item_stock_parts_power_store_cell_emergency_light>(r, bob);
        Click(r, bob, frame);
        r.In(() => Assert.Same(cell, frame.cell));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, frame);
        obj_machinery_light lamp = Find<obj_machinery_light>(r, bob);
        r.In(() =>
        {
            Assert.True(QDELETED(frame));
            Assert.IsType<obj_machinery_light_empty>(lamp);
            Assert.Equal(LIGHT_EMPTY, Dm.num(lamp.status));
            Assert.Equal(SOUTH, Dm.num(lamp.dir));
            Assert.Same(cell, lamp.cell);                 // the cell moves into the fixture
            Assert.Same(lamp, cell.loc);
            Assert.False(Dm.truthy(lamp.on));
        });

        // a tube lights it
        Hold<obj_item_light_tube>(r, bob);
        Click(r, bob, lamp);
        r.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(lamp.status));
            Assert.True(Dm.truthy(lamp.on));
            Assert.Equal(8f, Dm.num(lamp.light_range));
        });

        // light.dm:433: the screwdriver does nothing while the tube is in; with it out, it opens the casing into a wired frame
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, lamp);
        r.In(() => Assert.False(QDELETED(lamp)));
        r.In(() => Waiting.NoSleep(lamp.drop_light_tube()));
        r.In(() => foreach_drop(bob));
        Click(r, bob, lamp);
        var reopened = Find<obj_structure_light_construct>(r, bob);
        r.In(() =>
        {
            Assert.True(QDELETED(lamp));
            Assert.NotNull(reopened);
            Assert.Equal(LIGHT_CONSTRUCT_WIRED, reopened.stage);
            Assert.Equal("tube-construct-stage2", reopened.icon_state);
            Assert.Equal(SOUTH, Dm.num(reopened.dir));
            Assert.Same(cell, reopened.cell);
        });

        // a wirecutter unwires it: a red cable falls out (:167)
        Hold<obj_item_wirecutters>(r, bob);
        Click(r, bob, reopened);
        r.In(() =>
        {
            Assert.Equal(LIGHT_CONSTRUCT_EMPTY, reopened.stage);
            Assert.Equal("tube-construct-stage1", reopened.icon_state);
            Assert.Single(Dm.each(get_turf(reopened).contents).OfType<obj_item_stack_cable_coil>(), c => Dm.num(c.amount) == 1);
        });

        // a wrench: the cell first (:129), taken out by hand (:66), then the frame comes apart into 2 sheets of iron (:130-142)
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, reopened, 4);
        r.In(() => Assert.False(QDELETED(reopened)));
        r.In(() => Waiting.NoSleep(bob.drop_all_held_items()));
        Click(r, bob, reopened); // the empty hand takes the cell out
        r.In(() =>
        {
            Assert.Null(reopened.cell);
            Assert.Same(cell, bob.get_active_held_item());
        });
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, reopened, 5); // use_tool(30): 3 seconds
        r.In(() =>
        {
            Assert.True(QDELETED(reopened));
            obj_item_stack_sheet_iron iron = Assert.Single(Dm.each(get_turf(bob).contents).OfType<obj_item_stack_sheet_iron>());
            Assert.Equal(2f, Dm.num(iron.amount)); // sheets_refunded, :16
        });
        r.AssertNoRuntimes();
    }

    private static void foreach_drop(mob_living_carbon_human who)
    {
        foreach (obj_item_light l in Dm.each(get_turf(who).contents).OfType<obj_item_light>().ToList())
            qdel(l);
    }

    [Fact]
    public void ABulbFrameAndAFloorFrameMakeTheirOwnFixturesAndRefundOneSheet()
    {
        var (r, bob) = Build(9102);
        foreach ((Type frameType, Type lampType, string stage2, float sheets) in new[]
        {
            (typeof(obj_structure_light_construct_small), typeof(obj_machinery_light_small_empty), "bulb-construct-stage2", 1f),
            (typeof(obj_structure_light_construct_floor), typeof(obj_machinery_light_floor_empty), "floor-construct-stage2", 1f),
        })
        {
            var frame = r.In(() => (obj_structure_light_construct)Dm.@new(frameType, get_turf(bob)));
            r.In(() => Assert.Equal(sheets, Dm.num(frame.sheets_refunded)));
            Hold<obj_item_stack_cable_coil>(r, bob);
            Click(r, bob, frame);
            r.In(() => Assert.Equal(stage2, frame.icon_state));
            Hold<obj_item_screwdriver>(r, bob);
            Click(r, bob, frame);
            r.In(() =>
            {
                Assert.True(QDELETED(frame));
                obj_machinery_light lamp = Assert.Single(Dm.each(get_turf(bob).contents).OfType<obj_machinery_light>());
                Assert.IsType(lampType, lamp);
                Assert.Equal(LIGHT_EMPTY, Dm.num(lamp.status));
                qdel(lamp);
            });
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASmashedFixtureLeavesADamagedFrameABrokenTubeAndALooseCable()
    {
        obj_machinery_light lamp = null;
        var (r, bob) = Build(9103, w =>
        {
            lamp = Dm.@new<obj_machinery_light_directional_south>(locate(7, 2, 1));
            lamp.allow_break_on_init = FALSE;
        });
        r.In(() =>
        {
            if (!Dm.eq(lamp.status, LIGHT_OK))
                Waiting.NoSleep(lamp.fix());
            Waiting.NoSleep(lamp.deconstruct(FALSE)); // light.dm:443: disassembled = FALSE
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(lamp));
            turf t = locate(7, 2, 1) as turf;
            obj_structure_light_construct frame = Assert.Single(Dm.each(t.contents).OfType<obj_structure_light_construct>());
            Assert.Equal(LIGHT_CONSTRUCT_WIRED, frame.stage);
            Assert.Equal(100f, Dm.num(frame.atom_integrity)); // take_damage(max_integrity * 0.5)
            Assert.Single(Dm.each(t.contents).OfType<obj_item_light_tube>(), l => Dm.eq(l.status, LIGHT_BROKEN)); // break_light_tube + drop_light_tube
            Assert.Single(Dm.each(t.contents).OfType<obj_item_stack_cable_coil>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASmallFrameInTheHandIsAttachedToTheFloorAndRefusedWhereAnObjectIsInTheWay()
    {
        var (r, bob) = Build(9104);
        var small = Hold<obj_item_wallframe_light_fixture_small>(r, bob);
        turf floor = r.In(() => get_turf(bob) as turf);
        // a dense thing on the tile refuses it (light_wallframes.dm:48)
        var crate = r.In(() => Dm.@new<obj_structure_closet_crate>(floor));
        r.In(() => Waiting.NoSleep(small.attack_self(bob)));
        r.In(() =>
        {
            Assert.False(QDELETED(small));
            Assert.Empty(Dm.each(floor.contents).OfType<obj_structure_light_construct_floor>());
        });
        r.In(() => qdel(crate));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(small.attack_self(bob)));
        r.In(() =>
        {
            Assert.True(QDELETED(small));
            var frame = Assert.Single(Dm.each(floor.contents).OfType<obj_structure_light_construct_floor>());
            Assert.Equal("floor-construct-stage1", frame.icon_state);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("click", StringComparison.Ordinal)); // :57
        r.AssertNoRuntimes();
    }
}
