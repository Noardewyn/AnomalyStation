using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 10, slice 3A (DV693): the belt hotbar through a real server and a headless client. Alice wears a toolbelt of seven tools; her
/// client's number keys send presses (PlaySession.HotbarPress), the server moves the items with tg's procs and sends her the ten cells and the held
/// names (protocol 100's Hotbar), and her client keeps them (HotbarModel) with the last refused press. Presses from a ghost or a cyborg do nothing, and a
/// flood of presses is held by the action rate limit with no item duplicated or lost.
/// </summary>
public class HotbarGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private sealed record Rig(RealHeadlessWorld W, RealHeadlessWorld.Player Alice, mob_living_carbon_human Body, obj_item Belt) : IDisposable
    {
        public HotbarModel Bar => Alice.Session.Hotbar;

        public void Dispose() => W.Dispose();
    }

    // Alice in a jumpsuit with nothing else worn, a full toolbelt on unless told otherwise.
    private static Rig Make(ulong seed, bool belt = true)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var body = (mob_living_carbon_human)w.MobOf(alice);
        obj_item worn = null;
        In(w, () =>
        {
            foreach (obj_item item in new[] { body.belt, body.l_store, body.r_store, body.wear_id, body.get_active_held_item() as obj_item, body.get_inactive_held_item() as obj_item })
                if (item != null)
                    qdel(item);
            if (body.w_uniform == null)
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(body), ITEM_SLOT_ICLOTHING))));
            if (belt)
            {
                worn = Dm.@new<obj_item_storage_belt_utility_full>(get_turf(body));
                Assert.True(Dm.truthy(Waiting.NoSleep(body.equip_to_slot_if_possible(worn, ITEM_SLOT_BELT))));
            }
        });
        w.Run(Second);
        return new Rig(w, alice, body, worn);
    }

    private static void Press(Rig g, int cell, int ticks = 10)
    {
        Assert.True(g.Alice.Session.HotbarPress(cell));
        g.W.Run(ticks);
    }

    private static List<obj_item> Tools(Rig g) => In(g.W, () => Dm.each<obj_item>(g.Belt.atom_storage.real_location.contents).ToList());

    private static obj_item Active(Rig g) => In(g.W, () => g.Body.get_active_held_item() as obj_item);

    private static string NameOf(Rig g, obj_item item) => In(g.W, () => Dm.jointext(Dm.list(item), ""));

    private static string ActiveHandName(Rig g) => In(g.W, () => Dm.eq(g.Body.active_hand_index, 1)) ? g.Bar.LeftHand : g.Bar.RightHand;

    [Fact]
    public void Press_3_takes_the_third_tool_and_press_3_again_puts_it_back_in_the_same_cell()
    {
        using Rig g = Make(7101);
        RealHeadlessWorld w = g.W;
        int runtimes = w.World.RuntimeErrorCount;
        Assert.True(g.Bar.Bar && g.Bar.BeltWorn, "the client has the bar and the belt");
        for (int c = 1; c <= 7; c++)
        {
            Assert.Equal(HotbarCellState.Holding, g.Bar.StateOf(c));
            Assert.False(g.Bar.Cells[c - 1].Appearance.IsNone, "cell " + c + " has its tool's picture");
            Assert.True(g.Alice.Session.Replica.TryGetAppearance(g.Bar.Cells[c - 1].Appearance, out _), "cell " + c + "'s picture is in the client's tables");
        }
        for (int c = 8; c <= 10; c++)
            Assert.Equal(HotbarCellState.Empty, g.Bar.StateOf(c));
        Assert.Null(g.Bar.LeftHand);
        Assert.Null(g.Bar.RightHand);
        obj_item third = Tools(g)[2];
        TableRef picture = g.Bar.Cells[2].Appearance;

        Press(g, 3);
        Assert.Same(third, Active(g));
        Assert.Equal(HotbarCellState.Out, g.Bar.StateOf(3));
        // the picture stays (drawn faded in slice 3B): the item as it is drawn now, in a hand (tg's equipped item has the HUD's layer and plane)
        Assert.False(g.Bar.Cells[2].Appearance.IsNone);
        Assert.True(g.Alice.Session.Replica.TryGetAppearance(g.Bar.Cells[2].Appearance, out AppearanceEntry held));
        Assert.True(g.Alice.Session.Replica.TryGetAppearance(picture, out AppearanceEntry stored));
        Assert.Equal(stored.IconState, held.IconState); // the same picture
        Assert.Equal(NameOf(g, third), ActiveHandName(g));
        Assert.False(string.IsNullOrEmpty(ActiveHandName(g)));

        Press(g, 3);
        Assert.Null(Active(g));
        Assert.Equal(HotbarCellState.Holding, g.Bar.StateOf(3));
        Assert.Same(third, In(w, () => BeltHotbar.Peek(g.Body).ItemOf(3)));
        Assert.True(In(w, () => third.loc == g.Belt.atom_storage.real_location));
        Assert.Null(ActiveHandName(g));
        Assert.Equal(0, g.Bar.RefusedCell);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void Press_5_with_the_tool_of_3_in_hand_swaps_and_a_foreign_item_goes_into_an_empty_cell()
    {
        using Rig g = Make(7102);
        RealHeadlessWorld w = g.W;
        List<obj_item> tools = Tools(g);
        Press(g, 3);
        Press(g, 5);
        Assert.Same(tools[4], Active(g));
        Assert.Equal(HotbarCellState.Holding, g.Bar.StateOf(3));
        Assert.Equal(HotbarCellState.Out, g.Bar.StateOf(5));
        Assert.True(In(w, () => tools[2].loc == g.Belt.atom_storage.real_location));

        // the fifth tool dropped: its cell is free; a foreign screwdriver in hand goes into it
        In(w, () => Waiting.NoSleep(g.Body.dropItemToGround(tools[4])));
        w.Run(4);
        Assert.Equal(HotbarCellState.Empty, g.Bar.StateOf(5));
        obj_item foreign = In(w, () =>
        {
            var screwdriver = Dm.@new<obj_item_screwdriver>(get_turf(g.Body));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Body.put_in_active_hand(screwdriver))));
            return (obj_item)screwdriver;
        });
        w.Run(2);
        Press(g, 5);
        Assert.Null(Active(g));
        Assert.Equal(HotbarCellState.Holding, g.Bar.StateOf(5));
        Assert.Same(foreign, In(w, () => BeltHotbar.Peek(g.Body).ItemOf(5)));
        Assert.Equal(0, g.Bar.RefusedCell);
    }

    [Fact]
    public void A_refused_press_changes_nothing_and_the_client_learns_which_cell_and_when()
    {
        using Rig g = Make(7103);
        RealHeadlessWorld w = g.W;
        Assert.Equal(0, g.Bar.RefusedCell);
        Assert.Equal(double.NegativeInfinity, g.Bar.RefusedAt);
        List<HotbarCellState> before = Enumerable.Range(1, 10).Select(g.Bar.StateOf).ToList();
        double sent = w.Now;
        Press(g, 8); // an empty hand on an empty pocket cell
        Assert.Equal(8, g.Bar.RefusedCell);
        Assert.InRange(g.Bar.RefusedAt, sent, w.Now);
        Assert.Equal(before, Enumerable.Range(1, 10).Select(g.Bar.StateOf).ToList());
        Assert.Null(Active(g));
        // a foreign item on a cell that holds a tool: refused again, the same state, a later refusal
        double first = g.Bar.RefusedAt;
        obj_item foreign = In(w, () =>
        {
            var analyzer = Dm.@new<obj_item_analyzer>(get_turf(g.Body));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Body.put_in_active_hand(analyzer))));
            return (obj_item)analyzer;
        });
        w.Run(2);
        Press(g, 2);
        Assert.Equal(2, g.Bar.RefusedCell);
        Assert.True(g.Bar.RefusedAt > first);
        Assert.Same(foreign, Active(g));
        Assert.Equal(HotbarCellState.Holding, g.Bar.StateOf(2));
        // the refusal is an event: the next state message does not repeat it
        Press(g, 2, ticks: 2);
        double third = g.Bar.RefusedAt;
        w.Run(Second);
        Assert.Equal(third, g.Bar.RefusedAt);
    }

    [Fact]
    public void An_old_refusal_is_not_replayed_on_reentering_the_body_and_a_refusal_in_new_cells_first_phase_arrives()
    {
        using Rig g = Make(7109);
        RealHeadlessWorld w = g.W;
        int runtimes = w.World.RuntimeErrorCount;
        Press(g, 8); // refused: an empty hand on an empty pocket
        Assert.Equal(8, g.Bar.RefusedCell);
        int refusals = g.Alice.Client.HotbarRefusals;
        double refusedAt = g.Bar.RefusedAt;

        // she ghosts and re-enters her body (tg's Re-enter Corpse button): the body's cells, with their old refusal, are hers again
        In(w, () => g.Body.ghostize(Dm.TRUE));
        w.Run(2 * Second);
        Assert.False(g.Bar.Bar);
        long button = g.Alice.Session.Replica.Screen.Values.Single(e => In(w, () => w.World.Objects.TryGet(e.Id, out object o) && o is atom_movable_screen_ghost_reenter_corpse)).Id;
        Assert.True(g.Alice.Session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(g.Body, In(w, () => Dm.each<client>(GLOB.clients).First(c => c.key as string == "Alice").mob));
        Assert.True(g.Bar.Bar);
        Assert.Equal(refusals, g.Alice.Client.HotbarRefusals); // no refusal for a press nobody made
        Assert.Equal(refusedAt, g.Bar.RefusedAt);

        // a fresh human whose cells are made in the very command phase that refuses her press: that refusal arrives
        mob_living_carbon_human fresh = In(w, () => Dm.@new<mob_living_carbon_human>(get_turf(g.Body)));
        Assert.Null(In(w, () => fresh.dm_hotbar));
        In(w, () => ClientSeam.KeyMob(fresh, "Alice"));
        Assert.True(g.Alice.Session.HotbarPress(9));
        w.Run(Second);
        Assert.Same(fresh, In(w, () => Dm.each<client>(GLOB.clients).First(c => c.key as string == "Alice").mob));
        Assert.Equal(1, In(w, () => fresh.dm_hotbar.Refusals));
        Assert.Equal(refusals + 1, g.Alice.Client.HotbarRefusals);
        Assert.Equal(9, g.Bar.RefusedCell);
        Assert.True(g.Bar.RefusedAt > refusedAt);
        Assert.Equal(runtimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void Dropping_an_out_item_frees_its_cell_and_taking_the_belt_off_empties_the_seven()
    {
        using Rig g = Make(7104);
        RealHeadlessWorld w = g.W;
        obj_item second = Tools(g)[1];
        Press(g, 2);
        Assert.Equal(HotbarCellState.Out, g.Bar.StateOf(2));
        In(w, () => Waiting.NoSleep(g.Body.dropItemToGround(second)));
        w.Run(4);
        Assert.Equal(HotbarCellState.Empty, g.Bar.StateOf(2));
        Assert.True(g.Bar.Cells[1].Appearance.IsNone);
        Assert.Null(g.Bar.LeftHand ?? g.Bar.RightHand);

        In(w, () => Waiting.NoSleep(g.Body.dropItemToGround(g.Belt)));
        w.Run(4);
        Assert.False(g.Bar.BeltWorn);
        Assert.True(g.Bar.Bar);
        for (int c = 1; c <= 7; c++)
            Assert.Equal(HotbarCellState.Empty, g.Bar.StateOf(c));
        Press(g, 1); // no belt: refused
        Assert.Equal(1, g.Bar.RefusedCell);
    }

    [Fact]
    public void The_pockets_and_the_id_slot_take_out_put_back_and_store()
    {
        using Rig g = Make(7105, belt: false);
        RealHeadlessWorld w = g.W;
        Assert.True(g.Bar.Bar);
        Assert.False(g.Bar.BeltWorn);
        obj_item pen = In(w, () =>
        {
            var p = Dm.@new<obj_item_pen>(get_turf(g.Body));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Body.equip_to_slot_if_possible(p, ITEM_SLOT_RPOCKET))));
            return (obj_item)p;
        });
        w.Run(4);
        Assert.Equal(HotbarCellState.Holding, g.Bar.StateOf(9));
        Press(g, 9);
        Assert.Same(pen, Active(g));
        Assert.Equal(HotbarCellState.Out, g.Bar.StateOf(9));
        Press(g, 9);
        Assert.Same(pen, In(w, () => g.Body.r_store));
        Assert.Equal(HotbarCellState.Holding, g.Bar.StateOf(9));

        obj_item card = In(w, () =>
        {
            var c = Dm.@new<obj_item_card_id>(get_turf(g.Body));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Body.put_in_active_hand(c))));
            return (obj_item)c;
        });
        w.Run(2);
        Press(g, 10);
        Assert.Same(card, In(w, () => g.Body.wear_id));
        Assert.Equal(HotbarCellState.Holding, g.Bar.StateOf(10));
        Press(g, 10);
        Assert.Same(card, Active(g));
        Assert.Equal(HotbarCellState.Out, g.Bar.StateOf(10));
        Assert.Equal(0, g.Bar.RefusedCell);
    }

    [Fact]
    public void Presses_from_a_ghost_or_a_cyborg_do_nothing()
    {
        foreach (string who in new[] { "ghost", "cyborg" })
        {
            using Rig g = Make(who == "ghost" ? 7106UL : 7107UL);
            RealHeadlessWorld w = g.W;
            int runtimes = w.World.RuntimeErrorCount;
            List<obj_item> tools = Tools(g);
            if (who == "ghost")
                In(w, () => g.Body.ghostize(Dm.TRUE));
            else
                In(w, () => Waiting.NoSleep(g.Body.Robotize()));
            w.Run(2 * Second);
            mob now = In(w, () => Dm.each<client>(GLOB.clients).First(c => c.key as string == "Alice").mob);
            Assert.True(who == "ghost" ? now is mob_dead_observer : now is mob_living_silicon_robot, who + ": " + now?.GetType().Name);
            Assert.False(g.Bar.Bar, who + " has no bar");
            Assert.All(Enumerable.Range(1, 10), c => Assert.Equal(HotbarCellState.Empty, g.Bar.StateOf(c)));
            string Where() => In(w, () => string.Join(",", tools.Select(t => (t.loc as datum)?.dm_id ?? 0)));
            string before = Where();
            for (int c = 1; c <= 10; c++)
                Press(g, c, ticks: 2);
            w.Run(Second);
            Assert.True(before == Where(), who + ": the tools did not move");
            Assert.True(g.Bar.RefusedCell == 0, who + ": no refusal either, the press is dropped");
            Assert.True(runtimes == w.World.RuntimeErrorCount, who + ": no runtime");
        }
    }

    [Fact]
    public void Two_hundred_presses_in_a_tick_are_held_by_the_rate_limit_and_no_item_is_duplicated_or_lost()
    {
        using Rig g = Make(7108);
        RealHeadlessWorld w = g.W;
        List<obj_item> tools = Tools(g);
        Session session = w.Host.LiveSession("Alice");
        long applied = session.ActionsApplied, limited = session.RateLimited;
        int Items() => In(w, () => Dm.each<obj_item>(g.Belt.atom_storage.real_location.contents).Count()
            + Dm.each<obj_item>(g.Body.held_items).Count() + Dm.each<obj_item>(get_turf(g.Body).contents).Count());
        int itemsBefore = Items();
        var rng = new Random(5);
        for (int i = 0; i < 200; i++)
            Assert.True(g.Alice.Session.HotbarPress(rng.Next(1, 11)));
        w.Run(2 * Second);
        Assert.Equal(FeedPhase.Playing, g.Alice.Client.Phase); // held, not kicked
        long pressesApplied = session.ActionsApplied - applied;
        Assert.InRange(pressesApplied, 1, 5 + 2 * 10); // the keybinding bucket's burst, and its refill over the run
        Assert.True(session.RateLimited - limited >= 150, "dropped: " + (session.RateLimited - limited));
        Assert.Equal(itemsBefore, Items());
        // every tool is exactly once in the belt or a hand, and nothing new appeared
        In(w, () =>
        {
            foreach (obj_item tool in tools)
            {
                Assert.False(QDELETED(tool));
                Assert.True(tool.loc == g.Belt.atom_storage.real_location || Dm.truthy(g.Body.is_holding(tool)), Dm.str(tool.name));
            }
            List<obj_item> held = Dm.each<obj_item>(g.Body.held_items).ToList();
            List<obj_item> stored = Dm.each<obj_item>(g.Belt.atom_storage.real_location.contents).ToList();
            Assert.Equal(7, held.Count + stored.Count);
            Assert.Empty(held.Intersect(stored));
        });
    }
}
