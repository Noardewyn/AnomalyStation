using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.M4aHands.P2;

/// <summary>
/// M4a P2 (D126, D131), the HealthDollTests pattern: the real server's private block carries tg's human HUD as screen entries
/// with C's HUD group byte: the two hand slots and the six always-shown slots in <see cref="HudGroup.Static"/>, the nine
/// toggleable slots in <see cref="HudGroup.ToggleableInventory"/> once the inventory is shown (tg's show_hud), each at tg's
/// <c>screen_loc</c>; and a worn item drawn in its slot (the slot's vis_contents, flattened by the replicator).
/// </summary>
public class HudEntryTests
{
    private static List<datum_inventory_slot> HumanSlots(BotServerRig rig)
    {
        using (rig.World.Enter())
        {
            DmList all = GlobalProcs.GLOB.inventory_slot_datums;
            return Enumerable.Range(1, (int)all.len).Select(i => all[all[i]]).OfType<datum_inventory_slot>()
                .Where(d => d.GetType().IsSubclassOf(typeof(datum_inventory_slot_human))).ToList();
        }
    }

    private static ScreenEntry? At(Bot bot, string screenLoc) =>
        bot.Replica.Screen.Values.Where(e => e.ScreenLoc == screenLoc).Cast<ScreenEntry?>().FirstOrDefault();

    [Fact]
    public void ThePrivateBlockCarriesTheHandsAndTheSlotsInTheirGroupsAndAWornItemInItsSlot()
    {
        using var rig = new BotServerRig(strict: true);
        Bot bot = rig.AddBot("Wearer", BotBehaviour.Idle);
        rig.UntilJoined(bot);
        rig.Tick(10);

        // the hands: tg's ui_hand_position (__HELPERS/hud.dm:1)
        foreach (string loc in new[] { "CENTER+0:16,SOUTH+0:5", "CENTER+-1:16,SOUTH+0:5" })
        {
            ScreenEntry? hand = At(bot, loc);
            Assert.True(hand != null, "hand slot at " + loc);
            Assert.Equal(HudGroup.Static, hand.Value.Group);
        }

        List<datum_inventory_slot> slots = HumanSlots(rig);
        Assert.Equal(15, slots.Count);
        foreach (datum_inventory_slot slot in slots.Where(s => s.screen_group == Defines.HUD_GROUP_STATIC))
        {
            ScreenEntry? entry = At(bot, Dm.str(slot.screen_loc));
            Assert.True(entry != null, slot.name);
            Assert.Equal(HudGroup.Static, entry.Value.Group);
        }
        foreach (datum_inventory_slot slot in slots.Where(s => s.screen_group == Defines.HUD_GROUP_TOGGLEABLE_INVENTORY))
            Assert.True(At(bot, Dm.str(slot.screen_loc)) == null, slot.name + " is hidden until the inventory is shown");

        var human = (mob_living_carbon_human)rig.MobOf("Wearer");
        string beltLoc = Dm.str(slots.Single(s => s.slot_id == Defines.ITEM_SLOT_BELT).screen_loc);
        // M4a P5: the assistant's outfit wears the PDA on the belt; the test wants an empty belt slot first.
        using (rig.World.Enter())
        {
            if (human.belt != null)
                Anomaly.Core.Tg.GlobalProcs.qdel(human.belt);
        }
        rig.Tick(2);
        TableRef emptyBelt = At(bot, beltLoc)!.Value.Appearance;
        using (rig.World.Enter())
        {
            obj_item suit = Dm.@new<obj_item>(human.loc);
            suit.slot_flags = Defines.ITEM_SLOT_ICLOTHING;
            obj_item belt = Dm.@new<obj_item>(human.loc);
            belt.slot_flags = Defines.ITEM_SLOT_BELT;
            belt.icon = Dm.resource("icons/obj/clothing/belts.dmi");
            belt.icon_state = "utility";
            if (human.w_uniform == null) // the spawn's outfit puts on a jumpsuit (the belt needs one)
                Assert.True(Dm.truthy(Waiting.NoSleep(human.equip_to_slot_if_possible(suit, Defines.ITEM_SLOT_ICLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(human.equip_to_slot_if_possible(belt, Defines.ITEM_SLOT_BELT))));
            human.hud_used.inventory_shown = 1;
            Waiting.NoSleep(human.hud_used.show_hud(human.hud_used.hud_version));
        }
        rig.Tick(4);

        Assert.NotEqual(emptyBelt, At(bot, beltLoc)!.Value.Appearance); // the belt is drawn in its slot
        foreach (datum_inventory_slot slot in slots.Where(s => s.screen_group == Defines.HUD_GROUP_TOGGLEABLE_INVENTORY))
        {
            ScreenEntry? entry = At(bot, Dm.str(slot.screen_loc));
            Assert.True(entry != null, slot.name + " shown");
            Assert.Equal(HudGroup.ToggleableInventory, entry.Value.Group);
        }
        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }
}
