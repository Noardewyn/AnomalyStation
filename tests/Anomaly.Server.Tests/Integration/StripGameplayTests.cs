using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8z slice B (D234-D238, interactions.md) on MetaStation through the real server and the clients' own stacks: Bob is stunned and asleep
/// (so blind) in the medbay lobby; Alice drags him onto her doll and the StripMenu window opens; the radial offers "Strip" on him. She
/// takes his shoes (tg's strip_delay; Carol, watching, reads "Alice tries to remove Bob's ..."), his ID and a pocket item (the pocket's
/// own delay; Bob reads "You feel someone fumble with your belongings."), picks the shoes up and puts them back on him (the shoes'
/// equip_delay_other), sets his suit sensors from the list window, and knots his laces lying on the floor.
/// </summary>
public class StripGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().FirstOrDefault(n => n.Key == key);

    // Open floor tiles near (x, y): no dense thing on them.
    private static List<turf> OpenTiles(RealHeadlessWorld w, int x, int y, int z, int count) => In(w, () =>
    {
        var found = new List<turf>();
        for (int radius = 0; radius < 6 && found.Count < count; radius++)
        {
            for (int dx = -radius; dx <= radius && found.Count < count; dx++)
            {
                for (int dy = -radius; dy <= radius && found.Count < count; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;
                    if (locate(x + dx, y + dy, z) is turf_open floor && !Dm.truthy(floor.density) && !Dm.each_cast<atom>(floor.contents).Any(a => Dm.truthy(a.density)))
                        found.Add(floor);
                }
            }
        }
        return found;
    });

    [MetaServerFact]
    public void Alice_strips_a_sleeping_friend_of_his_shoes_id_and_pocket_dresses_him_and_sets_his_sensors()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 101, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        ReplicaWorld seenByAlice = alice.Session.Replica;
        var aliceTexts = new List<ServerText>();
        var bobTexts = new List<ServerText>();
        var carolTexts = new List<ServerText>();
        alice.Client.ServerTextReceived += aliceTexts.Add;
        bob.Client.ServerTextReceived += bobTexts.Add;
        carol.Client.ServerTextReceived += carolTexts.Add;
        var hints = new List<ContextHintsMessage>();
        alice.Client.ContextHintsReceived += hints.Add;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        int z = In(w, () => (int)aliceMob.z);
        string aliceName = In(w, () => aliceMob.name), bobName = In(w, () => bobMob.name); // the round names the mobs, not the players

        // the three stand in the medbay lobby (the chairs' room): Alice and Bob side by side, Carol next to them
        List<turf> tiles = OpenTiles(w, 94, 104, z, 4);
        Assert.True(tiles.Count >= 3);
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(tiles[0]));
            Waiting.NoSleep(bobMob.forceMove(tiles[1]));
            Waiting.NoSleep(carolMob.forceMove(tiles[2]));
        });
        w.Run(Second);

        // Bob: a pen in his left pocket, stunned and asleep (asleep he is blind: tg's "You feel someone fumble with your belongings.")
        var pen = In(w, () => Dm.@new<obj_item_pen>(bobMob.loc as atom));
        w.Run(2);
        Do(w, () => Waiting.NoSleep(bobMob.equip_to_slot(pen, ITEM_SLOT_LPOCKET)));
        alice.Session.DevCommand("stun Bob 60");
        Do(w, () => Waiting.NoSleep(bobMob.Unconscious(60 * SECONDS)));
        w.Run(Second);
        var shoes = In(w, () => bobMob.shoes as obj_item_clothing_shoes);
        var id = In(w, () => bobMob.wear_id);
        var jumpsuit = In(w, () => bobMob.w_uniform as obj_item_clothing_under);
        Assert.NotNull(shoes);
        Assert.NotNull(id);
        Assert.NotNull(jumpsuit);
        Assert.True(In(w, () => Dm.truthy(bobMob.is_blind())), "asleep, Bob is blind");
        Assert.Same(pen, In(w, () => bobMob.l_store));

        // 1. the radial on Bob offers Strip (a drop of him onto Alice)
        hints.Clear();
        w.Run(4);
        alice.Session.Rehover();
        alice.Session.Hover(On(bobMob), w.Now);
        w.Run(4);
        ContextHintsMessage answer = hints.LastOrDefault(h => h.Target == bobMob.dm_id);
        Assert.NotNull(answer);
        Assert.Contains(answer.Hints, h => h.Label == "Strip" && h.Action == ContextHintAction.DropOntoSelf);
        List<RadialEntry> entries = RadialModel.Build(answer, 0, true);
        Assert.Contains(entries, e => e.Kind == RadialKind.DropOntoSelf && e.Label == "Strip");

        // 2. Alice drags Bob onto her doll (the Drop of the radial's Strip): the StripMenu window opens, named for him
        Assert.True(alice.Session.Drop(new DragRelease(On(bobMob), On(aliceMob), ClickButton.Left, ClickModifiers.None, 255)));
        w.Run(Second / 2);
        UiWindowState strip = Assert.Single(seenByAlice.UiWindows.Values, v => v.Interface == "StripMenu");
        UiNode content = Shown(windows, seenByAlice, strip.Window);
        output.WriteLine(content.AllText());
        Assert.Equal("Stripping " + bobName, windows.Get(strip.Window).Title);
        Assert.Equal(2, strip.Status);
        Assert.NotNull(ByKey(content, "slot:shoes"));
        Assert.Equal(shoes.name, ByKey(content, "slot:shoes").Tooltip);
        Assert.Equal(shoes.icon_state, ByKey(content, "slot:shoes").Children.First(n => n.Key == "item").DmIconState);
        Assert.Equal("obscured left pocket", ByKey(content, "slot:left_pocket").Tooltip); // a pocket's item is hidden (STRIPPABLE_OBSCURING_HIDDEN)
        Assert.Null(ByKey(content, "slot:left_hand").Tooltip); // an empty slot: tg's payload is {obscured: 0}, not null, so it has no tooltip
        Assert.NotNull(ByKey(content, "alt:jumpsuit:adjust_sensor"));
        Assert.NotNull(ByKey(content, "alt:shoes:knot") ?? ByKey(content, "alt:shoes:untie")); // tied laces: "untie" is not offered to a stranger? tg offers by state

        // 3. the shoes: tg's strip_delay; Carol reads the visible message, Bob is told while asleep
        float shoeDelay = In(w, () => shoes.strip_delay);
        float before = In(w, () => (float)Dm.world.time);
        ByKey(content, "slot:shoes").OnClick();
        for (int i = 0; i < Second / 2 && !aliceTexts.Any(t => t.Html.Contains("You try to remove", StringComparison.Ordinal)); i++)
            w.Run(1); // a busy tick (SSair's share) queues the act to SSverb_manager's next fire (DEFAULT_QUEUE_OR_CALL_VERB, tgui.dm)
        Assert.Same(shoes, In(w, () => bobMob.shoes)); // not at once
        Assert.Contains(carolTexts, t => t.Html.Contains(aliceName + " tries to remove " + bobName + "'s", StringComparison.Ordinal));
        Assert.Contains(bobTexts, t => t.Html.Contains("You feel someone fumble with your belongings.", StringComparison.Ordinal));
        Assert.Contains(aliceTexts, t => t.Html.Contains("You try to remove " + bobName + "'s", StringComparison.Ordinal));
        for (int i = 0; i < 20 && ByKey(Shown(windows, seenByAlice, strip.Window), "slot:shoes").Background == null; i++)
            w.Run(2); // the window's next update (SStgui's fire)
        Assert.Equal("#845d15", ByKey(Shown(windows, seenByAlice, strip.Window), "slot:shoes").Background); // the slot being used is highlighted
        output.WriteLine($"strip_delay {shoeDelay}");
        w.Run((int)(shoeDelay * 2) + Second);
        Assert.Null(In(w, () => bobMob.shoes));
        float took = In(w, () => (float)Dm.world.time) - before;
        Assert.True(took >= shoeDelay, $"the shoes came off after {took} ds, strip_delay {shoeDelay}");
        Assert.Same(In(w, () => bobMob.loc), In(w, () => shoes.loc));

        // 4. his ID
        content = Shown(windows, seenByAlice, strip.Window);
        float idDelay = In(w, () => id.strip_delay);
        ByKey(content, "slot:id").OnClick();
        w.Run((int)(idDelay * 2) + Second);
        Assert.Null(In(w, () => bobMob.wear_id));

        // 5. the pen in his pocket: the pocket's delay (POCKET_STRIP_DELAY), hidden from the window until it is out
        content = Shown(windows, seenByAlice, strip.Window);
        ByKey(content, "slot:left_pocket").OnClick();
        w.Run(Second);
        Assert.Same(pen, In(w, () => bobMob.l_store)); // 4 s
        Assert.Contains(aliceTexts, t => t.Html.Contains("You try to empty " + bobName + "'s left pocket.", StringComparison.Ordinal));
        w.Run(POCKET_STRIP_DELAY * 2 + Second);
        Assert.Null(In(w, () => bobMob.l_store));

        // 6. the sensors, from the list window (the jumpsuit's alternate action), then the same delay as adjusting; tg's jumpsuit starts
        // on a random mode (_under.dm:52), so it is set off first to see the change
        In(w, () => jumpsuit.set_sensor_mode(SENSOR_OFF));
        content = Shown(windows, seenByAlice, strip.Window);
        ByKey(content, "alt:jumpsuit:adjust_sensor").OnClick();
        w.Run(Second / 2);
        UiWindowState list = Assert.Single(seenByAlice.UiWindows.Values, v => v.Interface == "ListInputWindow");
        Assert.True(alice.Session.UiAct(list.Window, "submit", UiValue.ObjectOf(new[] { new KeyValuePair<string, UiValue>("entry", UiValue.Of("Tracking")) })));
        w.Run(Second / 2);
        Assert.NotEqual(SENSOR_COORDS, In(w, () => jumpsuit.sensor_mode));
        w.Run((int)(jumpsuit.strip_delay * 0.5f) * 2 + Second);
        Assert.Equal(SENSOR_COORDS, In(w, () => jumpsuit.sensor_mode));
        Assert.Contains(aliceTexts, t => t.Html.Contains("changed sensors", StringComparison.Ordinal));
        Assert.Contains(bobTexts, t => t.Html.Contains("successfully adjusted your", StringComparison.Ordinal));

        // 7. the shoes back on: Alice picks them up from the floor and uses the empty shoes slot
        Assert.True(alice.Session.Click(On(shoes), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(aliceMob, In(w, () => shoes.loc));
        content = Shown(windows, seenByAlice, strip.Window);
        float equipDelay = In(w, () => shoes.equip_delay_other);
        Assert.Null(ByKey(content, "slot:shoes").Tooltip); // empty again
        ByKey(content, "slot:shoes").OnClick();
        w.Run(2);
        Assert.Contains(carolTexts, t => t.Html.Contains(aliceName + " tries to put", StringComparison.Ordinal));
        Assert.Null(In(w, () => bobMob.shoes));
        w.Run((int)(equipDelay * 2) + Second);
        Assert.Same(shoes, In(w, () => bobMob.shoes));

        // 8. the laces: tied shoes offer "untie" to a stranger too (tg moves her towards knots); standing she is refused, lying on the
        // floor beside him she unties them and then knots them
        content = Shown(windows, seenByAlice, strip.Window);
        Assert.Null(ByKey(content, "alt:shoes:knot"));
        ByKey(content, "alt:shoes:untie").OnClick();
        w.Run(Second);
        Assert.Contains(aliceTexts, t => t.Html.Contains("You must be on the floor to interact with", StringComparison.Ordinal));
        Do(w, () => Waiting.NoSleep(aliceMob.set_body_position(LYING_DOWN)));
        w.Run(Second);
        float laceTime = In(w, () => shoes.lace_time);
        ByKey(Shown(windows, seenByAlice, strip.Window), "alt:shoes:untie").OnClick();
        w.Run((int)(laceTime * 2) + Second);
        Assert.Equal(SHOES_UNTIED, In(w, () => shoes.tied));
        Assert.Contains(aliceTexts, t => t.Html.Contains("You quietly set to work", StringComparison.Ordinal));
        for (int i = 0; i < 20 && ByKey(Shown(windows, seenByAlice, strip.Window), "alt:shoes:knot") == null; i++)
            w.Run(2);
        ByKey(Shown(windows, seenByAlice, strip.Window), "alt:shoes:knot").OnClick();
        w.Run((int)(laceTime * 2) + Second);
        Assert.Equal(SHOES_KNOTTED, In(w, () => shoes.tied));

        alice.Session.UiClose(strip.Window);
        w.Run(Second);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
