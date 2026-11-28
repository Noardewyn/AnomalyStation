using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.UiOverhaul;

/// <summary>
/// ui-overhaul.md section 12, slice 4C-1 (DV696): the other container, the storage a player has opened by tg's own means that she does not carry (a
/// box on the floor beside her: tg's active_storage while tg keeps it open, <see cref="CarriedStorages.Other"/>), in a booted toy room with Alice (a
/// backpack and a toolbelt) at (4, 3) and the box at (5, 3). It is listed while tg keeps it open and she can reach it, and it closes by tg's own rules;
/// a move takes an item out of it by its own removal and puts one in by tg's attempt_insert, its reach asked before and after every wait; after each
/// move the item is in exactly one place and tg's refusal is worded as tg words it; nothing of a container she has not opened can be named.
/// </summary>
public class CarriedStoragesOtherTests
{
    private const int Second = 20;

    private sealed class ChatCapture : IMessageSink
    {
        public List<string> Lines { get; } = new();
        public void Chat(object target, DmList message) => Lines.Add(Dm.str(message["html"] ?? message["text"]));
        public void Output(object target, string text) => Lines.Add(text);
        public void Balloon(object viewer, object atom, string text) => Lines.Add("balloon:" + text);
        public bool Saw(string text) => Lines.Any(l => l != null && l.Contains(text, StringComparison.Ordinal));
    }

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Alice, ChatCapture Chat)
    {
        public obj_item Backpack, Belt, PocketBox, HeldBox, Box;
        public mob_living_carbon_human Bob;
        public readonly List<obj_item> Storages = new();
    }

    private static Rig Build(ulong seed, bool pocketBox = false, bool heldBox = false, bool bob = false)
    {
        var r = new AudioRoom(9, 7, seed: seed);
        mob_living_carbon_human alice = r.Join(4, 3, "Alice");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        var g = new Rig(r, alice, chat);
        if (bob)
            g.Bob = r.Join(6, 3, "Bob");
        r.Step(Second);
        r.In(() =>
        {
            foreach (mob_living_carbon_human who in new[] { alice, g.Bob })
            {
                if (who == null)
                    continue;
                foreach (obj_item worn in new[] { who.back, who.belt, who.l_store, who.r_store, who.wear_id, who.s_store, who.wear_suit,
                    who.get_item_for_held_index(1) as obj_item, who.get_item_for_held_index(2) as obj_item })
                    if (worn != null)
                        qdel(worn);
                if (who.w_uniform == null)
                    Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(who), ITEM_SLOT_ICLOTHING))));
            }
            g.Backpack = Wear(alice, Dm.@new<obj_item_storage_backpack>(get_turf(alice)), ITEM_SLOT_BACK);
            g.Belt = Wear(alice, Dm.@new<obj_item_storage_belt_utility>(get_turf(alice)), ITEM_SLOT_BELT);
            if (pocketBox)
            {
                // tg's box does not fit a pocket by hand; it is put there as an outfit's equip_to_slot would
                g.PocketBox = Dm.@new<obj_item_storage_box>(get_turf(alice));
                Waiting.NoSleep(alice.equip_to_slot(g.PocketBox, ITEM_SLOT_RPOCKET));
            }
            if (heldBox)
            {
                g.HeldBox = Dm.@new<obj_item_storage_box>(get_turf(alice));
                Assert.True(Dm.truthy(Waiting.NoSleep(alice.put_in_hand(g.HeldBox, Inactive(alice)))));
            }
            g.Box = Dm.@new<obj_item_storage_box>(locate(5, 3, 1));
        });
        g.Storages.AddRange(new[] { g.Backpack, g.Belt, g.PocketBox, g.HeldBox, g.Box }.Where(s => s != null));
        r.Step(1);
        chat.Lines.Clear();
        return g;
    }

    private static obj_item Wear(mob_living_carbon_human who, obj_item item, int slot)
    {
        Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(item, slot))), "worn in " + slot);
        return item;
    }

    private static int Active(mob_living_carbon_human who) => (int)Dm.num(who.active_hand_index);

    private static int Inactive(mob_living_carbon_human who) => 3 - Active(who);

    private static T Make<T>(Rig g, int x = 4, int y = 3) where T : obj_item => g.R.In(() =>
    {
        T made = Dm.@new<T>(locate(x, y, 1));
        if (made.atom_storage != null)
            g.Storages.Add(made);
        return made;
    });

    private static void Store(Rig g, obj_item storage, obj_item item)
        => g.R.In(() => Assert.True(Dm.truthy(storage.atom_storage.attempt_insert(item, null, messages: FALSE)), "stored in " + storage.name));

    // tg's own opening (open_storage: what a right click or an alt click on it does)
    private static void Open(Rig g, mob_living_carbon_human who, obj_item storage)
    {
        g.R.In(() => Waiting.NoSleep(storage.atom_storage.open_storage(who)));
        g.R.Step(1);
    }

    private static void Move(Rig g, obj_item item, MoveTarget to, obj_item storage = null, int index = 0, int ticks = 4, mob_living_carbon_human who = null)
    {
        mob_living_carbon_human mover = who ?? g.Alice;
        g.R.Command(() => CarriedStorages.Move(mover.client, item.dm_id, to, storage?.dm_id ?? 0, index));
        g.R.Step(ticks);
    }

    private static int Refusals(Rig g, mob_living_carbon_human who = null) => g.R.In(() => (who ?? g.Alice).dm_carried?.Refusals ?? 0);

    private static datum_storage Other(Rig g, mob_living_carbon_human who = null) => g.R.In(() => CarriedStorages.Other(who ?? g.Alice));

    private static datum_storage InReach(Rig g, mob_living_carbon_human who = null) => g.R.In(() => CarriedStorages.OtherInReachForMove(who ?? g.Alice));

    // What the Storages message lists: tg's state only, read from outside the world as the replicator reads it
    private static datum_storage Shown(Rig g, mob_living_carbon_human who = null) => CarriedStorages.Other(who ?? g.Alice);

    // Every place the item is found in: a hand of Alice's or Bob's, a worn slot of Alice's, a storage of the test's, the floor.
    private static List<string> Places(Rig g, obj_item item) => g.R.In(() =>
    {
        var places = new List<string>();
        if (QDELETED(item))
            return places;
        foreach ((string who, mob_living_carbon_human m) in new[] { ("hand ", g.Alice), ("bob hand ", g.Bob) })
            if (m != null)
                for (int i = 1; i <= Dm.length(m.held_items); i++)
                    if (m.get_item_for_held_index(i) == item)
                        places.Add(who + i);
        foreach (int slot in CarriedStorages.WornSlots)
            if (g.Alice.get_item_by_slot(slot) == item)
                places.Add("slot " + slot);
        foreach (obj_item s in g.Storages)
            if (!QDELETED(s) && item.loc == s.atom_storage.real_location)
                places.Add("in " + s.dm_id);
        if (item.loc is turf)
            places.Add("floor");
        return places;
    });

    private static string Place(Rig g, obj_item item)
    {
        List<string> places = Places(g, item);
        Assert.True(places.Count == 1, "in exactly one place: " + string.Join(", ", places));
        return places[0];
    }

    private static string In(obj_item storage) => "in " + storage.dm_id;

    // ---- the list ----

    [Fact]
    public void AFloorBoxOpenedByTgsOwnWayIsTheOtherContainerOfItsOpenersOnly()
    {
        var g = Build(9801, bob: true);
        obj_item a = Make<obj_item_screwdriver>(g), b = Make<obj_item_wrench>(g);
        Store(g, g.Box, a);
        Store(g, g.Box, b);
        Assert.Null(Other(g));
        Assert.Null(Shown(g));
        Open(g, g.Alice, g.Box);
        Assert.Same(g.Box.atom_storage, g.R.In(() => g.Alice.active_storage)); // tg's state: she has it open
        Assert.Same(g.Box.atom_storage, Other(g));
        Assert.Same(g.Box.atom_storage, InReach(g));
        Assert.Same(g.Box.atom_storage, Shown(g));
        Assert.Equal(new[] { a, b }, g.R.In(() => CarriedStorages.ShownItems(g.Box.atom_storage)));
        Assert.DoesNotContain(g.R.In(() => CarriedStorages.List(g.Alice)), c => c.Item == g.Box); // it is no carried storage
        Assert.True(g.R.In(() => CarriedStorages.ShowsItem(g.Alice, a))); // her Shift+click examines it
        // Bob, beside the box, has not opened it: nothing of it is his; then he opens it too, and each has it
        Assert.Null(Other(g, g.Bob));
        Assert.False(g.R.In(() => CarriedStorages.ShowsItem(g.Bob, a)));
        Open(g, g.Bob, g.Box);
        Assert.Same(g.Box.atom_storage, Shown(g, g.Bob));
        Assert.Same(g.Box.atom_storage, Shown(g));
        // she opens her own backpack by tg's way: it is carried, so it is listed once, as carried, and the box is closed for her (tg's one window)
        Open(g, g.Alice, g.Backpack);
        Assert.Same(g.Backpack.atom_storage, g.R.In(() => g.Alice.active_storage));
        Assert.Null(Other(g));
        Assert.Null(Shown(g));
        Assert.False(g.R.In(() => CarriedStorages.ShowsItem(g.Alice, a)));
        Assert.Same(g.Box.atom_storage, Shown(g, g.Bob));
        g.R.AssertNoRuntimes();
    }

    [Theory]
    [InlineData("stepping away")]
    [InlineData("another storage opened")]
    [InlineData("the box deleted")]
    [InlineData("the box moved away")]
    [InlineData("the box picked up by someone else")]
    [InlineData("stunned")]
    [InlineData("cuffed")]
    [InlineData("killed")]
    [InlineData("locked")]
    public void TgClosesTheOtherContainerAndItIsNoLongerListed(string how)
    {
        var g = Build(9802, bob: how == "the box picked up by someone else");
        obj_item tool = Make<obj_item_screwdriver>(g);
        Store(g, g.Box, tool);
        Open(g, g.Alice, g.Box);
        Assert.Same(g.Box.atom_storage, Shown(g));
        obj_item second = how == "another storage opened" ? Make<obj_item_storage_box>(g, 3, 3) : null;
        g.R.In(() =>
        {
            switch (how)
            {
                case "stepping away": // tg's living Move: out of reach and not inside her, so hidden
                    Waiting.Detach(g.Alice.Move(locate(3, 3, 1), WEST));
                    break;
                case "another storage opened":
                    Waiting.NoSleep(second.atom_storage.open_storage(g.Alice));
                    break;
                case "the box deleted":
                    qdel(g.Box);
                    break;
                case "the box moved away": // tg's close_distance on the box's Moved
                    Waiting.NoSleep(g.Box.forceMove(locate(7, 5, 1)));
                    break;
                case "the box picked up by someone else": // Bob, two tiles from her, takes it: it is in his hand, out of her reach
                    Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_hands(g.Box))));
                    break;
                case "stunned": // tg's on_handsblocked_start
                    Waiting.NoSleep(g.Alice.Paralyze(30 * SECONDS));
                    break;
                case "cuffed":
                    Waiting.NoSleep(g.Alice.set_handcuffed(Dm.@new<obj_item_restraints_handcuffs>(g.Alice)));
                    break;
                case "killed": // death blocks the hands (set_stat), which closes it
                    Waiting.Detach(g.Alice.death());
                    break;
                default: // tg's set_locked closes it for everyone
                    g.Box.atom_storage.set_locked(STORAGE_FULLY_LOCKED);
                    break;
            }
        });
        g.R.Step(4);
        Assert.True(Other(g) == null || (how == "another storage opened" && Other(g) == second.atom_storage), how + ": still the box");
        Assert.Null(how == "another storage opened" ? null : Shown(g));
        if (how == "another storage opened")
            Assert.Same(second.atom_storage, Shown(g));
        Assert.Null(g.R.In(() => QDELETED(g.Box) ? null : CarriedStorages.OtherInReachForMove(g.Alice) == g.Box.atom_storage ? g.Box.atom_storage : null));
        if (!QDELETED(g.Box))
            Assert.False(g.R.In(() => Dm.@in(g.Alice, g.Box.atom_storage.is_using)), how + ": tg closed it for her");
        Assert.False(g.R.In(() => !QDELETED(tool) && CarriedStorages.ShowsItem(g.Alice, tool)), how + ": still examinable"); // a locked or closed box's items are not
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ABoxPickedUpByAPlayerBesideHerStaysOpenForHerAsInTg()
    {
        // tg's reach: a container in the hands of a mob beside her is reachable (click.dm:285-286, mob/living IsContainedAtomAccessible), so
        // close_distance keeps it open and she can still take from it
        var g = Build(9811);
        g.Bob = g.R.Join(5, 4, "Bob");
        g.R.Step(2);
        obj_item tool = Make<obj_item_screwdriver>(g);
        Store(g, g.Box, tool);
        Open(g, g.Alice, g.Box);
        g.R.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_hands(g.Box)))));
        g.R.Step(2);
        Assert.Same(g.Box.atom_storage, Shown(g));
        Move(g, tool, MoveTarget.Hand, index: Inactive(g.Alice));
        Assert.Equal("hand " + Inactive(g.Alice), Place(g, tool));
        // Bob steps away with it: the box did not move within him, so tg's own state keeps it open (no close_distance) and it stays listed, as
        // tg's window would show it; a move into it goes in, as tg's window click does (screen_objects.dm:653-667: no reach), and a move out of it is
        // refused, as tg's ClickOn on the item asks the reach (click.dm:171)
        g.R.In(() => Waiting.Detach(g.Bob.Move(locate(6, 4, 1), EAST)));
        g.R.Step(4);
        Assert.Same(g.Box.atom_storage, Shown(g));
        Assert.Null(InReach(g));
        Move(g, tool, MoveTarget.Storage, g.Box);
        Assert.Equal(In(g.Box), Place(g, tool));
        Move(g, tool, MoveTarget.Hand, index: Inactive(g.Alice));
        Assert.Equal(In(g.Box), Place(g, tool));
        Assert.Equal(0, Refusals(g));
        // the next event tg closes it on: her own step (living/Move asks the reach)
        g.R.In(() => Waiting.Detach(g.Alice.Move(locate(4, 2, 1), SOUTH)));
        g.R.Step(4);
        Assert.Null(g.R.In(() => g.Alice.active_storage));
        Assert.Null(Shown(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ATrashBagWhoseOpeningThrewThenDeletedLeavesNoOpenStorageBehindOnTheNextStep()
    {
        // the port's show_contents sets active_storage, then throws in orient_storage for a numbered-stack storage (/datum/numbered_display/New is
        // not translated), so she is not among the bag's is_using and its Destroy does not close it for her
        var g = Build(9812);
        obj_item bag = Make<obj_item_storage_bag_trash>(g, 5, 3), tool = Make<obj_item_screwdriver>(g);
        Store(g, bag, tool);
        Exception thrown = null;
        g.R.In(() =>
        {
            try
            {
                Waiting.NoSleep(bag.atom_storage.open_storage(g.Alice));
            }
            catch (Exception e)
            {
                thrown = e;
            }
        });
        Assert.NotNull(thrown); // the gap as it is today
        Assert.Same(bag.atom_storage, g.R.In(() => g.Alice.active_storage));
        Assert.Null(Other(g)); // not among its is_using: never listed
        datum_storage stale = g.R.In(() => g.Alice.active_storage);
        g.R.In(() => qdel(bag));
        g.R.Step(1);
        Assert.Null(stale.parent);
        g.R.In(() => Waiting.Detach(g.Alice.Move(locate(4, 4, 1), NORTH)));
        g.R.Step(4);
        Assert.Equal(4f, g.R.In(() => Dm.num(g.Alice.y)));
        Assert.Null(g.R.In(() => g.Alice.active_storage));
        g.R.In(() => Waiting.Detach(g.Alice.Move(locate(4, 3, 1), SOUTH)));
        g.R.Step(4);
        g.R.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AnOpenContainerMakesNoProbeWhileSheStandsStillAndTgsMoveClosesIt(int reach)
    {
        // reach 1, the box beside her; reach 2 (tg's elastic arms, reach.dm:46-52), the box two tiles east; an armed proximity sensor between them
        // (on the box's tile for reach 1). Nothing asks tg's reach while she stands still: tg's ranged reach would walk a probe object
        // (RangedReachCheck) that the sensor senses. The listing is read every tick, as the replicator reads it: tg's state only.
        var g = Build(9813);
        obj_item_assembly_prox_sensor sensor = null;
        obj_item box = null, tool = Make<obj_item_screwdriver>(g);
        g.R.In(() =>
        {
            if (reach == 2)
                Waiting.Detach(g.Alice.dna.add_mutation(typeof(datum_mutation_elastic_arms), MUTATION_SOURCE_ACTIVATED));
            qdel(g.Box);
            box = Dm.@new<obj_item_storage_box>(locate(4 + reach, 3, 1));
            sensor = Dm.@new<obj_item_assembly_prox_sensor>(locate(5, 3, 1));
            Assert.True(Dm.truthy(sensor.secured));
            Waiting.NoSleep(sensor.toggle_scan(TRUE));
            Assert.True(Dm.truthy(sensor.scanning));
        });
        g.Storages.Add(box);
        Store(g, box, tool);
        g.R.World.AddPhase("the listing's read", _ => CarriedStorages.Other(g.Alice));
        g.R.Step(4 * Second);
        Assert.Equal((float)reach, g.R.In(() => g.Alice.reach_length));
        int Beeps() => g.Chat.Lines.Count(l => l != null && l.Contains("*beep*", StringComparison.Ordinal));
        (int Beeps, long Ids) Still(int ticks)
        {
            g.Chat.Lines.Clear();
            long before = g.R.World.Objects.HighestId;
            for (int t = 0; t < ticks; t++)
            {
                g.R.Step(1);
                if (box.atom_storage == g.R.In(() => g.Alice.active_storage))
                    Assert.Same(box.atom_storage, CarriedStorages.Other(g.Alice)); // listed every tick while it is open
            }
            return (Beeps(), g.R.World.Objects.HighestId - before);
        }
        (int Beeps, long Ids) closed = Still(200);
        Open(g, g.Alice, box);
        Assert.Same(box.atom_storage, g.R.In(() => g.Alice.active_storage)); // tg's open_storage reaches it (two tiles with elastic arms)
        g.R.Step(4 * Second); // past the sensor's 3 s cooldown
        (int Beeps, long Ids) open = Still(200);
        Assert.Equal(closed.Beeps, open.Beeps);
        Assert.Equal(closed.Ids, open.Ids); // no reach_checker made while she stands still with it open: as many new ids as with it closed
        Console.WriteLine($"reach {reach}: closed: {closed.Beeps} beeps, {closed.Ids} new ids; open: {open.Beeps} beeps, {open.Ids} new ids");
        // a move from it is a request: tg's real rule, probe and all, lets her reach it
        Move(g, tool, MoveTarget.Hand, index: Active(g.Alice));
        Assert.Equal("hand " + Active(g.Alice), Place(g, tool));
        Assert.Same(box.atom_storage, CarriedStorages.Other(g.Alice));
        // she steps out of reach (a third tile for reach 2): tg's living/Move asks the reach and closes it
        g.R.In(() => Waiting.Detach(g.Alice.Move(locate(3, 3, 1), WEST)));
        g.R.Step(4);
        Assert.Equal(3f, g.R.In(() => Dm.num(g.Alice.x)));
        Assert.Null(g.R.In(() => g.Alice.active_storage));
        Assert.Null(CarriedStorages.Other(g.Alice));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheExamineOfAnItemInTheOtherContainerAsksNoReach()
    {
        // tg's Shift+click returns at ShiftClickOn, before any reach (click.dm); a prison gate under the box answers a reach test by speaking and
        // starting its cooldown (its CanAllowThrough, asked by Adjacent's ClickCross), and a reach of two tiles walks a probe object (a new id)
        var g = Build(9814);
        obj_machinery_prisongate gate = null;
        obj_item pen = Make<obj_item_pen>(g), far = null, farPen = Make<obj_item_pen>(g);
        g.R.In(() =>
        {
            gate = Dm.@new<obj_machinery_prisongate>(locate(5, 3, 1));
            gate.gate_active = TRUE;
        });
        Store(g, g.Box, pen);
        Open(g, g.Alice, g.Box);
        g.R.Step(4 * Second); // past the gate's 3 s cooldown from tg's opening
        float cooldown = g.R.In(() => gate.spam_cooldown_time);
        for (int i = 0; i < 20; i++)
            Assert.True(g.R.In(() => CarriedStorages.ShowsItem(g.Alice, pen)));
        Assert.Equal(cooldown, g.R.In(() => gate.spam_cooldown_time));
        Assert.NotNull(g.R.In(() => CarriedStorages.OtherInReachForMove(g.Alice))); // a move's reach test, by contrast, sets the gate off
        Assert.NotEqual(cooldown, g.R.In(() => gate.spam_cooldown_time));
        // reach 2 (elastic arms), a box two tiles away: twenty examines make no probe object
        g.R.In(() =>
        {
            Waiting.Detach(g.Alice.dna.add_mutation(typeof(datum_mutation_elastic_arms), MUTATION_SOURCE_ACTIVATED));
            far = Dm.@new<obj_item_storage_box>(locate(4, 5, 1));
        });
        g.Storages.Add(far);
        Store(g, far, farPen);
        g.R.Step(2);
        Open(g, g.Alice, far);
        Assert.Same(far.atom_storage, Shown(g));
        long ids = g.R.World.Objects.HighestId;
        for (int i = 0; i < 20; i++)
            Assert.True(g.R.In(() => CarriedStorages.ShowsItem(g.Alice, farPen)));
        Assert.Equal(ids, g.R.World.Objects.HighestId);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnArmedMousetrapPutIntoHerOpenBoxComesOutArmedButSnapsFromHerBackpack()
    {
        // tg runs on_found when a storage is shown (storage.dm:1080-1084), not on a click on an item of an open window (attack_hand: a mousetrap
        // snaps only on a clumsy hand, mousetrap.dm:180-183); the carried storages keep slice 4B's search on a take-out
        var g = Build(9815, bob: true);
        Open(g, g.Alice, g.Box);
        obj_item_assembly_mousetrap trap = Make<obj_item_assembly_mousetrap_armed>(g, 6, 3), packed = Make<obj_item_assembly_mousetrap_armed>(g);
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_hands(trap))));
            Assert.True(Dm.truthy(g.Box.atom_storage.attempt_insert(trap, g.Bob, messages: FALSE))); // Bob puts it into the box she has open
            Waiting.NoSleep(packed.forceMove(g.Backpack.atom_storage.real_location));
        });
        g.R.Step(1);
        Assert.True(g.R.In(() => Dm.truthy(trap.armed)));
        Move(g, trap, MoveTarget.Hand, index: Inactive(g.Alice));
        Assert.Equal("hand " + Inactive(g.Alice), Place(g, trap));
        Assert.True(g.R.In(() => Dm.truthy(trap.armed)), "it came out armed");
        Assert.False(g.Chat.Saw("You accidentally trigger"), string.Join(" | ", g.Chat.Lines));
        Move(g, packed, MoveTarget.Hand, index: Active(g.Alice)); // from her backpack: the search snaps it, as before this slice
        Assert.False(g.R.In(() => Dm.truthy(packed.armed)), "the backpack's trap snapped");
        Assert.True(g.Chat.Saw("You accidentally trigger"), string.Join(" | ", g.Chat.Lines));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void InsideAClosedClosetHerOwnBackpackIsInReachAndTheFloorBoxIsNot()
    {
        // tg's ClickOn answers an item inside the mob from DirectAccess (click.dm:150) before the loc's AllowClick (:161), which a closet refuses
        var g = Build(9816);
        obj_item tool = Make<obj_item_screwdriver>(g), pen = Make<obj_item_pen>(g);
        Store(g, g.Backpack, tool);
        Store(g, g.Box, pen);
        Open(g, g.Alice, g.Box);
        obj_structure_closet closet = g.R.In(() => (obj_structure_closet)Dm.@new<obj_structure_closet>(locate(4, 3, 1)));
        g.R.In(() => Waiting.NoSleep(g.Alice.forceMove(closet)));
        g.R.Step(2);
        Assert.Same(g.Box.atom_storage, Shown(g)); // tg keeps it open after a forceMove in
        Assert.False(g.R.In(() => Dm.truthy(g.Alice.loc.AllowClick())));
        Move(g, tool, MoveTarget.Hand, index: Inactive(g.Alice));
        Assert.Equal("hand " + Inactive(g.Alice), Place(g, tool));
        Move(g, pen, MoveTarget.Hand, index: Active(g.Alice));
        Assert.Equal(In(g.Box), Place(g, pen));
        g.R.AssertNoRuntimes();
    }

    // ---- the moves ----

    public static IEnumerable<object[]> Pairs()
    {
        foreach (string source in new[] { "hand", "slot", "back", "belt", "pocketbox", "heldbox" })
            yield return new object[] { source, "other" };
        foreach (string target in new[] { "hand", "inactive hand", "slot", "back", "belt cell 3", "pocketbox", "heldbox", "floor" })
            yield return new object[] { "other", target };
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void AScrewdriverMovesFromEverySourceKindIntoTheOtherContainerAndFromItToEveryTargetKind(string source, string target)
    {
        bool pocket = source == "pocketbox" || target == "pocketbox", held = source == "heldbox" || target == "heldbox";
        var g = Build(9803, pocketBox: pocket, heldBox: held);
        obj_item tool = Make<obj_item_screwdriver>(g);
        obj_item StorageOf(string kind) => kind switch
        {
            "back" => g.Backpack, "belt" or "belt cell 3" => g.Belt, "pocketbox" => g.PocketBox, "heldbox" => g.HeldBox, "other" => g.Box, _ => null,
        };
        g.R.In(() =>
        {
            if (source == "hand")
                Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hand(tool, Active(g.Alice)))));
            else if (source == "slot")
                Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.equip_to_slot_if_possible(tool, ITEM_SLOT_LPOCKET))));
        });
        if (StorageOf(source) is obj_item from)
            Store(g, from, tool);
        Open(g, g.Alice, g.Box);
        Assert.Same(g.Box.atom_storage, Other(g));
        string before = Place(g, tool);
        string expected;
        switch (target)
        {
            case "hand":
                Move(g, tool, MoveTarget.Hand, index: Active(g.Alice));
                expected = "hand " + Active(g.Alice);
                break;
            case "inactive hand":
                Move(g, tool, MoveTarget.Hand, index: Inactive(g.Alice));
                expected = "hand " + Inactive(g.Alice);
                break;
            case "slot":
                Move(g, tool, MoveTarget.Slot, index: ITEM_SLOT_LPOCKET);
                expected = "slot " + ITEM_SLOT_LPOCKET;
                break;
            case "floor":
                Move(g, tool, MoveTarget.Floor);
                expected = "floor";
                break;
            case "belt cell 3":
                Move(g, tool, MoveTarget.Storage, g.Belt, index: 3);
                expected = In(g.Belt);
                Assert.Same(tool, g.R.In(() => BeltHotbar.Peek(g.Alice).ItemOf(3)));
                break;
            default:
                Move(g, tool, MoveTarget.Storage, StorageOf(target));
                expected = In(StorageOf(target));
                break;
        }
        Assert.True(expected == Place(g, tool), source + " to " + target + ": was " + before + ", is " + Place(g, tool));
        Assert.Equal(0, Refusals(g));
        Assert.Same(g.Box.atom_storage, Other(g)); // the moves keep it open, as tg's clicks in its window do
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AWishedCellOrdersTheOtherContainerAsTgsDragInItsWindowDoes()
    {
        var g = Build(9804);
        obj_item a = Make<obj_item_screwdriver>(g), b = Make<obj_item_wrench>(g), c = Make<obj_item_crowbar>(g);
        Store(g, g.Box, a);
        Store(g, g.Box, b);
        g.R.In(() => Waiting.NoSleep(g.Alice.put_in_hand(c, 2)));
        Open(g, g.Alice, g.Box);
        List<obj_item> Order() => g.R.In(() => Dm.each<obj_item>(g.Box.atom_storage.real_location.contents).ToList());
        Move(g, c, MoveTarget.Storage, g.Box, index: 1); // before the first
        Assert.Equal(new[] { c, a, b }, Order());
        Move(g, b, MoveTarget.Storage, g.Box, index: 1); // within it (tg's mousedrop_receive: it is her active_storage)
        Assert.Equal(new[] { b, c, a }, Order());
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AFullOtherContainerAndAnItemTooBigAreRefusedWithTgsMessages()
    {
        var g = Build(9805);
        obj_item tool = Make<obj_item_screwdriver>(g), toolbox = Make<obj_item_storage_toolbox_mechanical>(g);
        g.R.In(() =>
        {
            Waiting.NoSleep(g.Alice.put_in_hand(tool, 1));
            Waiting.NoSleep(g.Alice.put_in_hand(toolbox, 2));
        });
        Open(g, g.Alice, g.Box);
        Move(g, toolbox, MoveTarget.Storage, g.Box); // bulky into a box
        Assert.Equal("hand 2", Place(g, toolbox));
        Assert.True(g.Chat.Saw("balloon:too big!"), string.Join(" | ", g.Chat.Lines));
        g.R.In(() =>
        {
            while (Dm.length(g.Box.atom_storage.real_location.contents) < Dm.num(g.Box.atom_storage.max_slots))
                Assert.True(Dm.truthy(g.Box.atom_storage.attempt_insert(Dm.@new<obj_item_pen>(locate(5, 3, 1)), null, messages: FALSE)));
        });
        g.R.Step(1);
        Move(g, tool, MoveTarget.Storage, g.Box); // a full box
        Assert.Equal("hand 1", Place(g, tool));
        Assert.True(g.Chat.Saw("balloon:no room!"), string.Join(" | ", g.Chat.Lines));
        Assert.Equal(2, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    // ---- nothing else can be named ----

    [Fact]
    public void AContainerNotOpenedOneAnotherHasOpenALockedOneAHiddenOneAndOneOutOfReachGiveWhatAStrangersItemGives()
    {
        var g = Build(9806, bob: true);
        obj_item boxed = Make<obj_item_screwdriver>(g), held = Make<obj_item_wrench>(g);
        Store(g, g.Box, boxed);
        g.R.In(() => Waiting.NoSleep(g.Alice.put_in_hand(held, 1)));
        void Nothing(string why)
        {
            string boxedAt = Place(g, boxed), heldAt = Place(g, held);
            Move(g, boxed, MoveTarget.Hand, index: 2);
            Move(g, boxed, MoveTarget.Storage, g.Backpack);
            Move(g, boxed, MoveTarget.Floor);
            Move(g, held, MoveTarget.Storage, g.Box);
            Move(g, held, MoveTarget.Storage, g.Box, index: 1);
            Assert.True(boxedAt == Place(g, boxed), why + ": the boxed item moved");
            Assert.True(heldAt == Place(g, held), why + ": the held item moved");
            Assert.True(Refusals(g) == 0, why + ": refused aloud");
            Assert.False(g.R.In(() => CarriedStorages.ShowsItem(g.Alice, boxed)), why + ": examined");
            Assert.Null(Shown(g));
        }
        Nothing("beside the box, not opened");
        Open(g, g.Bob, g.Box);
        Nothing("Bob has it open");
        // a locked one cannot be opened (tg's "closed!"), and one whose contents tg hides cannot either (show_contents)
        obj_item lockbox = Make<obj_item_storage_lockbox>(g, 5, 2), pack = Make<obj_item_storage_fancy_cigarettes>(g, 4, 2);
        Assert.True(g.R.In(() => Dm.truthy(lockbox.atom_storage.locked)));
        Assert.False(g.R.In(() => CarriedStorages.ShowsItems(pack.atom_storage)));
        obj_item inLock = g.R.In(() => (obj_item)Dm.@new<obj_item_pen>(lockbox)), inPack = g.R.In(() => Dm.each<obj_item>(pack.atom_storage.real_location.contents).FirstOrDefault());
        Open(g, g.Alice, lockbox);
        Open(g, g.Alice, pack);
        Assert.Null(g.R.In(() => g.Alice.active_storage));
        foreach (obj_item item in new[] { inLock, inPack }.Where(i => i != null))
        {
            string at = Place(g, item);
            Move(g, item, MoveTarget.Hand, index: 2);
            Assert.Equal(at, Place(g, item));
        }
        Move(g, held, MoveTarget.Storage, lockbox);
        Move(g, held, MoveTarget.Storage, pack);
        Assert.Equal("hand 1", Place(g, held));
        Assert.Equal(0, Refusals(g));
        // an item deeper inside the open container: a box inside a backpack on the floor, the backpack opened
        obj_item floorPack = Make<obj_item_storage_backpack>(g, 5, 4), inner = Make<obj_item_storage_box>(g), deep = Make<obj_item_pen>(g);
        Store(g, inner, deep);
        Store(g, floorPack, inner);
        Open(g, g.Alice, floorPack);
        Assert.Same(floorPack.atom_storage, Shown(g));
        Move(g, deep, MoveTarget.Hand, index: 2);
        Assert.Equal(In(inner), Place(g, deep));
        Assert.Equal(0, Refusals(g));
        // the box opened inside that backpack, then the backpack carried off by Bob: tg's own state keeps the box open (the box itself did not move),
        // and it stays listed as tg's window would show it; she can no longer reach into it, but she can put an item in, as tg's window click can
        Open(g, g.Alice, inner);
        Assert.Same(inner.atom_storage, Shown(g));
        g.R.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Bob.put_in_hands(floorPack))));
            Waiting.Detach(g.Bob.Move(locate(7, 3, 1), EAST));
        });
        g.R.Step(4);
        Assert.Same(inner.atom_storage, Shown(g)); // tg's state
        Assert.Null(InReach(g));
        Assert.True(g.R.In(() => CarriedStorages.ShowsItem(g.Alice, deep))); // listed, so examinable: tg's Shift+click on its window asks no reach
        Move(g, deep, MoveTarget.Hand, index: 2); // out of it: tg's reach, refused
        Assert.Equal(In(inner), Place(g, deep));
        Move(g, held, MoveTarget.Storage, inner); // into it: tg's window click, no reach
        Assert.Equal(In(inner), Place(g, held));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    // ---- waits ----

    [Theory]
    [InlineData("moved away")]
    [InlineData("another storage opened")]
    public void TheOtherContainerLostDuringThePickUpsWaitGivesNothingOut(string how)
    {
        // heavy gravity: attempt_pickup's wait (items.dm:556-561) before the box's own removal; the box is moved away or another storage opened meanwhile
        var g = Build(9807);
        obj_item tool = Make<obj_item_screwdriver>(g), second = Make<obj_item_storage_box>(g, 3, 3);
        Store(g, g.Box, tool);
        g.R.In(() => Waiting.NoSleep(g.Alice.AddElement(typeof(datum_element_forced_gravity), 2)));
        Open(g, g.Alice, g.Box);
        Move(g, tool, MoveTarget.Storage, g.Backpack, ticks: 2);
        Assert.True(g.Chat.Saw("You start picking up"), string.Join(" | ", g.Chat.Lines));
        g.R.In(() =>
        {
            if (how == "moved away")
                Waiting.NoSleep(g.Box.forceMove(locate(7, 5, 1)));
            else
                Waiting.NoSleep(second.atom_storage.open_storage(g.Alice));
        });
        g.R.Step(4 * Second);
        Assert.Equal(In(g.Box), Place(g, tool));
        Assert.Equal(1, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheOtherContainerMovedAwayDuringAnEquipDelayPutsNothingOn()
    {
        var g = Build(9808);
        obj_item floorPack = Make<obj_item_storage_backpack>(g, 5, 3), gauntlets = Make<obj_item_clothing_gloves_cargo_gauntlet>(g);
        Store(g, floorPack, gauntlets);
        Open(g, g.Alice, floorPack);
        Move(g, gauntlets, MoveTarget.Slot, index: ITEM_SLOT_GLOVES, ticks: 2); // tg's 3 s equip delay, the gauntlets still in the bag
        Assert.True(g.Chat.Saw("You start putting on"), string.Join(" | ", g.Chat.Lines));
        Assert.Equal(In(floorPack), Place(g, gauntlets));
        g.R.In(() => Waiting.NoSleep(floorPack.forceMove(locate(7, 5, 1))));
        g.R.Step(4 * Second);
        Assert.Equal(In(floorPack), Place(g, gauntlets));
        Assert.Null(g.R.In(() => g.Alice.gloves));
        Assert.Equal(1, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheOtherContainerAsTargetMovedAwayDuringTheSourcesSearchSendsTheItemBack()
    {
        var g = Build(9809);
        obj_item bag = Make<obj_item_storage_bag_trash>(g), tool = Make<obj_item_screwdriver>(g);
        g.R.In(() =>
        {
            Waiting.NoSleep(g.Alice.equip_to_slot(bag, ITEM_SLOT_LPOCKET)); // as an outfit would (it does not fit a pocket by hand)
            Assert.True(Dm.truthy(bag.atom_storage.attempt_insert(tool, null, messages: FALSE)));
        });
        Open(g, g.Alice, g.Box);
        Move(g, tool, MoveTarget.Storage, g.Box, ticks: 2);
        Assert.True(g.Chat.Saw("fishing around inside") || g.Chat.Saw("digging around in"), string.Join(" | ", g.Chat.Lines));
        g.R.In(() => Waiting.NoSleep(g.Box.forceMove(locate(7, 5, 1))));
        g.R.Step(2 * Second);
        Assert.Equal(In(bag), Place(g, tool)); // back in the trash bag by tg's insert
        Assert.Equal(1, Refusals(g));
        g.R.AssertNoRuntimes();
    }

    // ---- two players, one container ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoPlayersMovingOneItemOfTheSameContainerInOneTickLeaveItWithExactlyOne(bool wait)
    {
        var g = Build(9810, bob: true);
        obj_item tool = Make<obj_item_screwdriver>(g);
        Store(g, g.Box, tool);
        Open(g, g.Alice, g.Box);
        Open(g, g.Bob, g.Box);
        Assert.Same(g.Box.atom_storage, Other(g, g.Bob));
        if (wait) // heavy gravity: both pick-ups wait (items.dm:556-561) with the tool still in the box
            g.R.In(() =>
            {
                Waiting.NoSleep(g.Alice.AddElement(typeof(datum_element_forced_gravity), 2));
                Waiting.NoSleep(g.Bob.AddElement(typeof(datum_element_forced_gravity), 2));
            });
        g.R.Command(() =>
        {
            CarriedStorages.Move(g.Alice.client, tool.dm_id, MoveTarget.Hand, 0, Inactive(g.Alice));
            CarriedStorages.Move(g.Bob.client, tool.dm_id, MoveTarget.Hand, 0, Inactive(g.Bob));
        });
        g.R.Step(wait ? 4 * Second : 4);
        string place = Place(g, tool);
        // the first move took it; the second found it gone (a stranger's item now: nothing) or, during the wait, being moved (refused)
        Assert.Equal("hand " + Inactive(g.Alice), place);
        Assert.Equal(wait ? 1 : 0, Refusals(g, g.Bob));
        Assert.Equal(0, Refusals(g));
        g.R.AssertNoRuntimes();
    }
}
