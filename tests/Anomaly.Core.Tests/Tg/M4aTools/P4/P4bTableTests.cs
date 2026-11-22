using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.Tg.M4aTools.P4;

/// <summary>
/// M4a P4b: an item in hand clicked on a table goes onto it (tables_racks.dm:343 base_item_interaction, :387
/// table_place_act) at tg's pixel offsets from the click's icon-x and icon-y (icon-x - 16, clamped to half an icon), and
/// centred without them.
/// </summary>
public class P4bTableTests
{
    private const int Second = 20;

    private static (mob_living_carbon_human, obj_structure_table, obj_item) Rig(CapsuleRoom room)
    {
        mob_living_carbon_human alice = room.Join(5, 5, "Alice");
        var (table, item) = room.In(() =>
        {
            var t = Dm.@new<obj_structure_table>(locate(6, 5, 1));
            var i = Dm.@new<obj_item_crowbar>(locate(5, 5, 1));
            Assert.True(Dm.truthy(Waiting.NoSleep(alice.put_in_hands(i))));
            return (t, i);
        });
        room.Step(Second);
        return (alice, table, item);
    }

    private static void Click(CapsuleRoom room, mob who, atom target, int iconX, int iconY)
    {
        string text = ClientSeam.ClickParams("left", false, false, false, iconX, iconY);
        room.Command(() => ClientSeam.Click(who.client, target, text));
        room.Step(Second);
    }

    [Theory]
    [InlineData(20, 8, 4, -8)]   // inside the clamp: icon-x - 16, icon-y - 16
    [InlineData(1, 32, -15, 16)] // the icon's edges
    [InlineData(16, 16, 0, 0)]   // the centre
    public void AnItemClickedOnATableLandsAtTgsPixelOffsets(int iconX, int iconY, float pixelX, float pixelY)
    {
        var room = new CapsuleRoom(4410);
        var (alice, table, item) = Rig(room);

        Click(room, alice, table, iconX, iconY);
        room.In(() =>
        {
            Assert.Same(table.loc, item.loc);
            Assert.False(Dm.@in(item, alice.held_items));
            Assert.Equal(item.base_pixel_x + pixelX, item.pixel_x);
            Assert.Equal(item.base_pixel_y + pixelY, item.pixel_y);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ThePixelClampKeepsTheItemOnTheTablesTurf()
    {
        var room = new CapsuleRoom(4411);
        var (alice, table, item) = Rig(room);
        // the same modifiers list the click builds, with an icon-x beyond the icon (a table larger than one tile)
        room.In(() =>
        {
            DmList modifiers = Dm.list(Dm.kv(LEFT_CLICK, "1"), Dm.kv(ICON_X, "60"), Dm.kv(ICON_Y, "-40"));
            object result = Waiting.NoSleep(table.table_place_act(alice, item, modifiers));
            Assert.Equal((float)ITEM_INTERACT_SUCCESS, Dm.num(result));
            Assert.Same(table.loc, item.loc);
            Assert.Equal(item.base_pixel_x + ICON_SIZE_X * 0.5f, item.pixel_x);
            Assert.Equal(item.base_pixel_y - ICON_SIZE_Y * 0.5f, item.pixel_y);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutIconCoordinatesTheItemIsCentred()
    {
        var room = new CapsuleRoom(4412);
        var (alice, table, item) = Rig(room);
        room.Command(() => ClientSeam.Click(alice.client, table, "left=1;button=left"));
        room.Step(Second);
        room.In(() =>
        {
            Assert.Same(table.loc, item.loc);
            Assert.Equal(item.base_pixel_x, item.pixel_x);
            Assert.Equal(item.base_pixel_y, item.pixel_y);
        });
        room.AssertNoRuntimes();
    }
}
