using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aHands.P2;

/// <summary>
/// M4a P2, session 2: tg's human HUD (hud.dm, hud/human.dm, inventory_slot.dm, screen_objects.dm: the hand slots, the 15
/// inventory slots in their screen groups, the toggle, a slot click), the keybindings through the client seam
/// (<c>ClientSeam.Keybinding</c>, codes 1-8), examine through <c>examinate</c>, the assistant outfit's real items on the equip
/// path, the armour reads of <c>run_armor_check</c> and the inert guard at P2's sites, in a T2 room where a reached
/// <c>Dm.NotTranslated</c> throws.
/// </summary>
public class HudBindingsAndOutfitTests
{
    private static List<object> Items(DmList L) => L == null ? new List<object>() : Enumerable.Range(1, (int)L.len).Select(i => L[i]).ToList();

    private static atom_movable_screen Screen(T2Room r, mob who, string key) => r.In(() => who.hud_used.screen_objects[key] as atom_movable_screen);

    private static obj_item Item(T2Room r, mob who, float slot_flags, string name) => r.In(() =>
    {
        obj_item it = Dm.@new<obj_item>(who.loc);
        it.name = name;
        it.slot_flags = slot_flags;
        it.w_class = WEIGHT_CLASS_SMALL;
        return it;
    });

    [Fact]
    public void TheHumansHudHasTgsHandSlotsAndFifteenInventorySlotsInTheirScreenGroups()
    {
        var r = new T2Room(4401);
        mob_living_carbon_human bob = r.Join(8, 8, "Bob", name: "bob");
        r.In(() =>
        {
            datum_hud hud = bob.hud_used;
            Assert.IsType<datum_hud_human>(hud);
            for (int i = 1; i <= 2; i++)
            {
                var hand = hud.screen_objects["hand_slot:" + i] as atom_movable_screen_inventory_hand;
                Assert.NotNull(hand);
                Assert.Equal(i, hand.held_index);
                Assert.Equal(HUD_GROUP_STATIC, Dm.num(hand.hud_group_key));
                Assert.Contains(hand, Items(bob.client.screen));
            }
            var slots = new List<atom_movable_screen_inventory>();
            foreach (object key in Dm.each(hud.screen_objects))
                if (hud.screen_objects[key] is atom_movable_screen_inventory inv && inv is not atom_movable_screen_inventory_hand)
                    slots.Add(inv);
            Assert.Equal(15, slots.Count); // D126: all 15 slots of tg's human HUD
            foreach (atom_movable_screen_inventory inv in slots)
            {
                var datum = Items(GLOB.inventory_slot_datums).Select(t => GLOB.inventory_slot_datums[t]).OfType<datum_inventory_slot>().Single(d => d.slot_id == Dm.num(inv.slot_id) && d.GetType().IsSubclassOf(typeof(datum_inventory_slot_human)));
                Assert.Equal(datum.screen_group, Dm.num(inv.hud_group_key));
                Assert.Equal(datum.screen_loc, inv.screen_loc);
                // the toggleable slots reach the screen only when the inventory is shown (tg's show_hud)
                Assert.Equal(datum.screen_group != HUD_GROUP_TOGGLEABLE_INVENTORY, Dm.@in(inv, bob.client.screen));
            }
            foreach (string key in new[] { HUD_MOB_DROP, HUD_MOB_SWAPHAND_2, HUD_HUMAN_TOGGLE_INVENTORY, HUD_MOB_ZONE_SELECTOR, HUD_MOB_RESIST, HUD_MOB_THROW, HUD_MOB_INTENTS, HUD_MOB_SCREENTIP })
                Assert.True(hud.screen_objects[key] is atom_movable_screen, key);
            Assert.Same(hud.screen_objects[HUD_MOB_SCREENTIP], hud.screentip_text);
            Assert.Equal("act_swap", (hud.screen_objects[HUD_MOB_SWAPHAND_2] as atom).icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHeldAndAWornItemShowInTheirSlotsVisContents()
    {
        var r = new T2Room(4402);
        mob_living_carbon_human bob = r.Join(8, 8, "Bob", name: "bob");
        obj_item suit = Item(r, bob, ITEM_SLOT_ICLOTHING, "jumpsuit");
        obj_item belt = Item(r, bob, ITEM_SLOT_BELT, "belt");
        obj_item held = Item(r, bob, 0, "rock");
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(belt, ITEM_SLOT_BELT))));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(held))));
        });
        var beltSlot = Screen(r, bob, "item_slot:" + (int)ITEM_SLOT_BELT) as atom_movable_screen_inventory;
        var hand = Screen(r, bob, "hand_slot:" + r.In(() => (int)Dm.num(bob.active_hand_index))) as atom_movable_screen_inventory;
        r.In(() =>
        {
            Assert.Contains(belt, Items(beltSlot.vis_contents));
            Assert.Equal("template_small", beltSlot.icon_state); // icon_full while the slot holds an item
            Assert.Contains(held, Items(hand.vis_contents));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.dropItemToGround(belt))));
            Assert.Empty(Items(beltSlot.vis_contents));
            Assert.Equal("belt", beltSlot.icon_state); // icon_empty
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheToggleShowsTheToggleableSlotsAndAClickOnASlotEquipsTheHeldItem()
    {
        var r = new T2Room(4403);
        mob_living_carbon_human bob = r.Join(8, 8, "Bob", name: "bob");
        obj_item shoes = Item(r, bob, ITEM_SLOT_FEET, "shoes");
        atom_movable_screen toggle = Screen(r, bob, HUD_HUMAN_TOGGLE_INVENTORY);
        atom_movable_screen feet = Screen(r, bob, "item_slot:" + (int)ITEM_SLOT_FEET);
        r.In(() => Assert.False(Dm.@in(feet, bob.client.screen)));
        r.Command(() => ClientSeam.Click(bob.client, toggle, ""));
        r.Step();
        r.In(() =>
        {
            Assert.True(Dm.truthy(bob.hud_used.inventory_shown));
            Assert.True(Dm.@in(feet, bob.client.screen));
            Assert.Equal("toggle_active", toggle.icon_state);
            Waiting.NoSleep(bob.put_in_hands(shoes));
        });
        r.Step(2); // the click cooldown
        r.Command(() => ClientSeam.Click(bob.client, feet, ""));
        r.Step();
        r.In(() =>
        {
            Assert.Same(shoes, bob.shoes);
            Assert.Null(bob.get_active_held_item());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheKeybindingsSwapSelectDropToggleAndHoldThrowModeAndQuickEquip()
    {
        var r = new T2Room(4404);
        mob_living_carbon_human bob = r.Join(8, 8, "Bob", name: "bob");
        obj_item rock = Item(r, bob, 0, "rock");
        obj_item belt = Item(r, bob, ITEM_SLOT_BELT, "belt");
        obj_item suit = Item(r, bob, ITEM_SLOT_ICLOTHING, "jumpsuit");
        void Key(byte code, bool down = true)
        {
            r.Command(() => ClientSeam.Keybinding(bob.client, code, down));
            r.Step();
        }
        float Hand() => r.In(() => Dm.num(bob.active_hand_index));

        float first = Hand();
        Key(1); // swap_hands
        Assert.NotEqual(first, Hand());
        Key(2); // select_hand right
        Assert.True(r.In(() => IS_RIGHT_INDEX(Dm.num(bob.active_hand_index))));
        Key(3); // select_hand left
        Assert.True(r.In(() => IS_LEFT_INDEX(Dm.num(bob.active_hand_index))));

        var selfUse = new List<object>();
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(rock))));
            // attack_self's own chain is P1's: its /atom/interact reaches ui_interact (tgui, M9, Dm.NotTranslated), so the
            // signal cancels the chain here once the binding has reached the held item (COMPONENT_CANCEL_ATTACK_CHAIN).
            Anomaly.Core.Tests.Tg.Wave4.Integration.Toy.On(rock, COMSIG_ITEM_ATTACK_SELF, "self", 2, result: COMPONENT_CANCEL_ATTACK_CHAIN, act: a => selfUse.AddRange(a));
        });
        Key(4); // activate_inhand: mode() -> execute_mode -> the held item's attack_self
        Assert.Contains(bob, selfUse);
        r.In(() => Assert.Same(rock, bob.get_active_held_item()));
        Key(5); // drop_item
        r.In(() =>
        {
            Assert.Null(bob.get_active_held_item());
            Assert.Same(bob.loc, rock.loc);
        });

        Key(7); // toggle_throw_mode
        r.In(() => Assert.Equal(THROW_MODE_TOGGLE, bob.throw_mode));
        Key(7);
        r.In(() => Assert.Equal(THROW_MODE_DISABLED, bob.throw_mode));
        Key(8); // hold_throw_mode down
        r.In(() => Assert.Equal(THROW_MODE_HOLD, bob.throw_mode));
        Key(8, down: false);
        r.In(() => Assert.Equal(THROW_MODE_DISABLED, bob.throw_mode));

        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(suit, ITEM_SLOT_ICLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(belt))));
        });
        Key(6); // quick_equip: the held belt goes to the belt slot (equip_to_appropriate_slot)
        r.Step(2);
        r.In(() => Assert.Same(belt, bob.belt));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ExaminateSendsTgsExamineBoxWithTheTitleAndTheWeightLine()
    {
        var r = new T2Room(4405);
        mob_living_carbon_human bob = r.Join(8, 8, "Bob", name: "bob");
        var chat = new ChatHtml();
        PresentationSinks.Of(r.World).Messages = chat;
        obj_item rock = Item(r, bob, 0, "rock");
        r.In(() => rock.desc = "A test rock.");
        // the player's shift-click: ClientSeam.Click with tg's params, ShiftClick (P1) calls user.examinate(src)
        r.Command(() => ClientSeam.Click(bob.client, rock, "left=1;shift=1;icon-x=16;icon-y=16"));
        r.Step(2);
        string line = chat.Html.SingleOrDefault(l => l.Contains("fieldset", StringComparison.Ordinal));
        Assert.True(line != null, string.Join(" | ", chat.Html));
        Assert.Contains("A test rock.", line);
        Assert.Contains("rock", line);
        Assert.Contains("item.", line); // /obj/item's examine_descriptor and its weight tag (examine_tags)
        r.In(() => Assert.True(Dm.truthy(bob.client.recent_examines[REF(rock)]), "the examine is remembered for examine_more"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAssistantOutfitsRealItemsEquipIntoTheirSlotsOnTheOutfitPath()
    {
        var r = new T2Room(4406);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        (Type Type, float Slot, Func<obj_item> Worn)[] outfit =
        {
            // /datum/outfit/job (modules/jobs/job_types/_job.dm:339-345); EQUIP_OUTFIT_ITEM's order (outfit.dm:183-205)
            (typeof(obj_item_clothing_under_color_grey), ITEM_SLOT_ICLOTHING, () => bob.w_uniform),
            (typeof(obj_item_modular_computer_pda), ITEM_SLOT_BELT, () => bob.belt),
            (typeof(obj_item_clothing_shoes_sneakers_black), ITEM_SLOT_FEET, () => bob.shoes),
            (typeof(obj_item_radio_headset), ITEM_SLOT_EARS, () => bob.ears),
            (typeof(obj_item_storage_backpack), ITEM_SLOT_BACK, () => bob.back),
            (typeof(obj_item_card_id_advanced), ITEM_SLOT_ID, () => bob.wear_id),
        };
        foreach (var (type, slot, worn) in outfit)
        {
            obj_item it = r.In(() => Dm.@new(type, bob.loc) as obj_item);
            r.In(() =>
            {
                // user.equip_to_slot_or_del(item, slot, TRUE, indirect_action = TRUE): the outfit's initial equipment
                Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_or_del(it, slot, TRUE, indirect_action: TRUE))), type.Name);
                Assert.Same(it, worn());
                Assert.Same(bob, it.loc);
            });
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RunArmorCheckReadsAWornHelmetsMeleeArmourOnTheHeadOnly()
    {
        var r = new T2Room(4407);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        r.In(() =>
        {
            var helmet = Dm.@new<obj_item_clothing_head_helmet>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(helmet, ITEM_SLOT_HEAD, initial: TRUE))));
            Assert.Equal(35f, Dm.num(bob.run_armor_check(BODY_ZONE_HEAD, MELEE, silent: TRUE)), 3); // /datum/armor/head_helmet
            Assert.Equal(0f, Dm.num(bob.run_armor_check(BODY_ZONE_CHEST, MELEE, silent: TRUE)), 3);
            Assert.Equal(50f, Dm.num(bob.getarmor(BODY_ZONE_HEAD, FIRE)), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHurtingThrowHitsADenseMobThroughCanAllowThroughAndDamagesIt()
    {
        var r = new T2Room(4408);
        mob_living_carbon_human bob = r.Join(3, 8, "Bob", name: "bob");
        mob_living_carbon_human dummy = r.Dummy(5, 8, name: "dummy");
        obj_item brick = Item(r, bob, 0, "brick");
        r.In(() =>
        {
            brick.throwforce = 10;
            Waiting.NoSleep(bob.put_in_hands(brick));
            Assert.True(Dm.truthy(dummy.density));
        });
        r.Command(() => Waiting.Detach(bob.throw_item(dummy)));
        r.Step(30);
        r.In(() =>
        {
            Assert.Null(brick.throwing);
            Assert.True(Dm.num(dummy.get_brute_loss()) > 0, "the dense mob took the throw's damage (run_armor_check, apply_damage)");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemWhoseOwnPickupIsNotTranslatedIsRefusedAndStaysWhereItLies()
    {
        var r = new T2Room(4409);
        mob_living_carbon_human bob = r.Join(8, 8, "Bob", name: "bob");
        RecordingSinks sinks = RecordingSinks.Install(r.World);
        obj_item blade = r.In(() => Dm.@new<obj_item_melee_cultblade>(locate(9, 8, 1))); // gaps S3 made the paper live, gaps DV2 the RWD (the examples until then)
        r.In(() =>
        {
            Assert.True(InertSeam.HasUndeclaredOverride(blade, "pickup")); // cult_items.dm (the cult blade's pickup: the cult, an antagonist, D333)
            Assert.False(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(blade))));
            Waiting.NoSleep(blade.attack_hand(bob));
            Assert.Null(bob.get_active_held_item());
            Assert.Same(locate(9, 8, 1), blade.loc);
        });
        Assert.Contains(sinks.Lines, l => l.Contains("balloon") && l.EndsWith("not ported yet", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    /// <summary>The message sink's chat html (tg's to_chat payload), for the examine box.</summary>
    private sealed class ChatHtml : IMessageSink
    {
        public List<string> Html { get; } = new();

        public void Chat(object target, DmList message) => Html.Add(Dm.str(message?["html"]));

        public void Output(object target, string text) { }

        public void Balloon(object viewer, object atom, string text) { }
    }
}
