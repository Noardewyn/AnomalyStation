using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 7: tg's furniture and the other structures taken apart by the player's tools in a booted toy room
/// (tables_racks.dm, table_frames.dm, beds_chairs/chair.dm, closets.dm, railings.dm, plasticflaps.dm, extinguisher.dm,
/// signs/_signs.dm, wall_mounted.dm): each deconstruction leaves tg's materials, table-driven from the dm (the amounts read
/// from the generated var blocks, which carry tg's values), and the building paths (a table from a frame and a sheet, a rack
/// from its parts, a chair rotated and picked up and planted).
/// </summary>
public class FurnitureTests
{
    private const int Second = 20;
    internal const string Left = "left=1;button=left;icon-x=16;icon-y=16";
    internal const string Right = "right=1;button=right;icon-x=16;icon-y=16";
    private const int X = 5, Y = 4;

    internal static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    internal static T Place<T>(AudioRoom r, int x = X, int y = Y) where T : atom => r.In(() => Dm.@new<T>(locate(x, y, 1)));

    internal static void Click(AudioRoom r, mob who, atom target, string modifiers, float seconds)
    {
        r.Command(() => ClientSeam.Click(who.client, target, modifiers));
        r.Step((int)(seconds * Second));
    }

    internal static List<T> On<T>(AudioRoom r, int x = X, int y = Y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    // the amount of the stacks of type T on the tile
    internal static float Amount<T>(AudioRoom r, int x = X, int y = Y) where T : obj_item_stack
        => r.In(() => On<T>(r, x, y).Where(s => s.GetType() == typeof(T)).Sum(s => Dm.num(s.amount)));

    public static IEnumerable<object[]> Deconstructions() => new[]
    {
        // structure, tool, right click, seconds, combat mode, expected (stack type, amount from the var named) ...
        new object[] { typeof(obj_structure_table), typeof(obj_item_screwdriver), true, 3f, "tables_racks.dm:324: buildstack + frame" },
        new object[] { typeof(obj_structure_table), typeof(obj_item_wrench), true, 5f, "tables_racks.dm:332: buildstack + framestack (frame = null)" },
        new object[] { typeof(obj_structure_table_glass), typeof(obj_item_screwdriver), true, 3f, "tables_racks.dm:589: glass + frame" },
        new object[] { typeof(obj_structure_table_frame), typeof(obj_item_wrench), false, 4f, "table_frames.dm:42: framestack" },
        new object[] { typeof(obj_structure_rack), typeof(obj_item_wrench), true, 1f, "tables_racks.dm:1377: rack parts" },
        new object[] { typeof(obj_structure_chair), typeof(obj_item_wrench), true, 1f, "chair.dm:80: buildstacktype x buildstackamount" },
        new object[] { typeof(obj_structure_chair_stool), typeof(obj_item_wrench), true, 1f, "chair.dm:80" },
        new object[] { typeof(obj_structure_chair_office), typeof(obj_item_wrench), true, 1f, "chair.dm:80: 5 iron" },
        new object[] { typeof(obj_structure_chair_comfy), typeof(obj_item_wrench), true, 1f, "chair.dm:80: 2 iron" },
        new object[] { typeof(obj_structure_railing), typeof(obj_item_wirecutters), false, 1f, "railings.dm:120: 2 rods" },
    };

    [Theory]
    [MemberData(nameof(Deconstructions))]
    public void EachDeconstructionLeavesTgsMaterials(Type structure, Type tool, bool right, float seconds, string where)
    {
        var (r, bob) = Room(5701);
        var thing = r.In(() => (atom)Dm.@new(structure, locate(X, Y, 1)));
        r.Step(Second);
        // what tg's atom_deconstruct makes, read from the type's own (generated, tg-valued) vars
        var expected = r.In(() => Expected(thing, tool));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(Dm.@new(tool, get_turf(bob)) as obj_item)));
        Click(r, bob, thing, right ? Right : Left, seconds);
        Assert.True(r.In(() => QDELETED(thing)), where + ": the structure is gone");
        foreach ((Type type, float amount) in expected)
        {
            float found = r.In(() => On<atom>(r).Where(a => a.GetType() == type).Sum(a => a is obj_item_stack s ? Dm.num(s.amount) : 1f));
            Assert.True(amount == found, where + ": " + type.Name + " expected " + amount + ", found " + found);
        }
        r.AssertNoRuntimes();
    }

    private static List<(Type, float)> Expected(atom thing, Type tool)
    {
        switch (thing)
        {
            case obj_structure_table table:
                // the screwdriver keeps the frame (tables_racks.dm:324), the wrench drops it as its rods (:332, frame = null)
                return tool == typeof(obj_item_screwdriver)
                    ? new() { (table.buildstack, table.buildstackamount), (table.frame, 1) }
                    : new() { (table.buildstack, table.buildstackamount), (table.framestack, table.framestackamount) };
            case obj_structure_table_frame frame:
                return new() { (frame.framestack, frame.framestackamount) };
            case obj_structure_rack:
                return new() { (typeof(obj_item_rack_parts), 1) };
            case obj_structure_chair chair:
                return new() { (chair.buildstacktype, chair.buildstackamount) };
            case obj_structure_railing railing:
                return new() { (railing.item_deconstruct, 2) };
        }
        return new();
    }

    // the activate_inhand keybinding (Z): mob/mode, the held item's attack_self
    internal static void UseInHand(AudioRoom r, mob_living_carbon_human who)
    {
        r.Command(() => ClientSeam.Keybinding(who.client, 4, true));
        r.Command(() => ClientSeam.Keybinding(who.client, 4, false));
        r.Step(2);
    }

    internal static T Hold<T>(AudioRoom r, mob_living_carbon_human who, float? amount = null) where T : obj_item => WallTests.Hold<T>(r, who, amount);

    [Fact]
    public void AFrameTakesOneSheetAndBecomesItsTableAndTheRackPartsAssembleInHand()
    {
        var (r, bob) = Room(5702);
        var frame = Place<obj_structure_table_frame>(r);
        var iron = Hold<obj_item_stack_sheet_iron>(r, bob, 5);
        // table_frames.dm:54: 2 s do_after, one sheet of the stack's table_type (stack.dm:259, iron: /obj/structure/table)
        Click(r, bob, frame, Left, 1);
        Assert.Empty(On<obj_structure_table>(r));
        r.Step(2 * Second);
        var table = Assert.Single(On<obj_structure_table>(r));
        Assert.Equal(typeof(obj_structure_table), table.GetType());
        Assert.True(r.In(() => QDELETED(frame)));
        Assert.Equal(4f, r.In(() => Dm.num(iron.amount)));
        // tables_racks.dm:97 apply_frame_properties: the table remembers its frame
        Assert.Equal(typeof(obj_structure_table_frame), r.In(() => table.frame));

        // rack parts: attack_self, 5 s, a rack on the user's turf (tables_racks.dm:1467)
        var parts = Hold<obj_item_rack_parts>(r, bob);
        UseInHand(r, bob);
        r.Step(6 * Second);
        Assert.Single(On<obj_structure_rack>(r, 4, 4));
        Assert.True(r.In(() => QDELETED(parts)));
        // and new rack parts wrenched into one iron sheet (tables_racks.dm:1459, :1464)
        var loose = Place<obj_item_rack_parts>(r, 5, 5);
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, loose, Left, 1);
        Assert.Equal(1f, Amount<obj_item_stack_sheet_iron>(r, 5, 5));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStoolRotatesIsPickedUpAndPlantedAgain()
    {
        var (r, bob) = Room(5703);
        var stool = Place<obj_structure_chair_stool>(r, 4, 5);
        r.Step(Second);
        float dir = r.In(() => Dm.num(stool.dir));
        // the simple_rotation element (chair.dm:73, ROTATION_IGNORE_ANCHORED): alt-click turns it
        Click(r, bob, stool, "alt=1;left=1;button=left;icon-x=16;icon-y=16", 1);
        Assert.NotEqual(dir, r.In(() => Dm.num(stool.dir)));
        // dragged onto the user (chair.dm:347): the stool item in hand with the structure's integrity
        r.In(() => Waiting.NoSleep(stool.mouse_drop_dragged(bob, bob)));
        r.Step(1);
        var item = r.In(() => bob.get_active_held_item()) as obj_item_chair_stool;
        Assert.NotNull(item);
        Assert.True(r.In(() => QDELETED(stool)));
        // planted (chair.dm:434): a stool facing the user's dir on the user's turf
        UseInHand(r, bob);
        r.Step(Second);
        var planted = Assert.Single(On<obj_structure_chair_stool>(r, 4, 4));
        Assert.Equal(r.In(() => Dm.num(bob.dir)), r.In(() => Dm.num(planted.dir)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALockerIsCutApartWithAWelderAndASecureOneLeavesItsElectronics()
    {
        var (r, bob) = Room(5704);
        var locker = Place<obj_structure_closet>(r);
        var secure = Place<obj_structure_closet_secure_closet>(r, 6, 4);
        r.Step(Second);
        float amount = r.In(() => locker.material_drop_amount);
        r.In(() => Waiting.NoSleep(locker.open(bob)));
        Hold<obj_item_weldingtool>(r, bob);
        // closets.dm:924: an open closet, the welder (1 fuel), 4 s, deconstruct(TRUE) -> handle_deconstruct :642
        Click(r, bob, locker, Left, 3);
        Assert.False(r.In(() => QDELETED(locker)));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(locker)));
        Assert.Equal(amount, Amount<obj_item_stack_sheet_iron>(r));

        // a secure closet drops its airlock electronics with its accesses (closets.dm:648)
        DmList access = r.In(() => secure.req_access);
        r.In(() => { secure.locked = FALSE; Waiting.NoSleep(secure.open(bob)); });
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(5, 4, 1))));
        Click(r, bob, secure, Left, 6);
        Assert.True(r.In(() => QDELETED(secure)));
        var electronics = Assert.Single(On<obj_item_electronics_airlock>(r, 6, 4));
        Assert.Equal(access, r.In(() => electronics.accesses));
        Assert.True(Amount<obj_item_stack_sheet_iron>(r, 6, 4) > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALockerIsAnchoredAndTakesElectronicsThatAScrewdriverRemoves()
    {
        var (r, bob) = Room(5705);
        var locker = Place<obj_structure_closet>(r);
        r.Step(Second);
        Hold<obj_item_wrench>(r, bob);
        // closets.dm:971: the wrench's secondary act toggles the anchor
        Click(r, bob, locker, Right, 1);
        Assert.True(r.In(() => Dm.truthy(locker.anchored)));
        Click(r, bob, locker, Right, 1);
        Assert.False(r.In(() => Dm.truthy(locker.anchored)));

        // closets.dm:714: airlock electronics in a closed locker, 4 s: it becomes secure with their access
        var electronics = Hold<obj_item_electronics_airlock>(r, bob);
        r.In(() => electronics.accesses = Dm.list(ACCESS_ENGINEERING));
        Click(r, bob, locker, Left, 5);
        Assert.True(r.In(() => Dm.truthy(locker.secure)));
        Assert.Equal(new object[] { ACCESS_ENGINEERING }, r.In(() => Dm.each(locker.req_access).ToArray()));
        // closets.dm:875: the screwdriver takes them out again, 4 s
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, locker, Left, 5);
        Assert.False(r.In(() => Dm.truthy(locker.secure)));
        var back = Assert.Single(On<obj_item_electronics_airlock>(r));
        Assert.Equal(new object[] { ACCESS_ENGINEERING }, r.In(() => Dm.each(back.accesses).ToArray()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWallStructuresComeOffTheWallAndTheFlapsAreCutApart()
    {
        var (r, bob) = Room(5706);
        // extinguisher cabinet (extinguisher.dm:68): empty, the wrench, 6 s, its frame; the frame wrenched (wall_mounted.dm:116)
        var cabinet = r.In(() => { var c = Dm.@new<obj_structure_extinguisher_cabinet>(locate(X, Y, 1)); return c; });
        r.Step(Second);
        r.In(() => { if (cabinet.stored_extinguisher != null) { qdel(cabinet.stored_extinguisher); cabinet.stored_extinguisher = null; } });
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, cabinet, Left, 7);
        var frame = Assert.Single(On<obj_item_wallframe_extinguisher_cabinet>(r));
        Click(r, bob, frame, Left, 1);
        Assert.Equal(2f, Amount<obj_item_stack_sheet_iron>(r)); // wall_mounted.dm:3 iron SHEET_MATERIAL_AMOUNT * 2
        // a sign (_signs.dm:50): 4 s, its sign item
        var sign = Place<obj_structure_sign_warning>(r, 5, 5);
        Click(r, bob, sign, Left, 5);
        Assert.Single(On<obj_item_sign>(r, 5, 5));
        // plastic flaps (plasticflaps.dm:150, :169): unscrewed in 10 s, cut in 5 s into five plastic sheets
        var flaps = Place<obj_structure_plasticflaps>(r, 6, 5);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(5, 4, 1))));
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, flaps, Left, 11);
        Assert.False(r.In(() => Dm.truthy(flaps.anchored)));
        Hold<obj_item_wirecutters>(r, bob);
        Click(r, bob, flaps, Left, 6);
        Assert.Equal(5f, Amount<obj_item_stack_sheet_plastic_five>(r, 6, 5));
        r.AssertNoRuntimes();
    }
}
