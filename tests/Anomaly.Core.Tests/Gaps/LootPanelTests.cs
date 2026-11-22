using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package G1: the loot panel (code/modules/lootpanel). An alt-click on a thing of a tile opens tg's "LootPanel" window with the
/// tile and everything on it (the turf first), each with its icon file and state; a mob and a thing with more than two overlays wait for
/// SSlooting to make their icon; the panel follows the tile (a thing that leaves or is deleted drops out of the list, one that arrives is
/// added) until the window closes; "grab" clicks the thing as the player would, with the keys the window sends; what the player wears or
/// holds is no loot; a ghost's alt-click opens it too.
/// </summary>
public class LootPanelTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        return (r, bob);
    }

    private static JsonElement Contents(JsonElement data) => data.GetProperty("contents");

    private static string[] Names(JsonElement data) => Contents(data).EnumerateArray().Select(i => i.GetProperty("name").GetString()).ToArray();

    private static string RefOf(JsonElement data, string name) => Contents(data).EnumerateArray().First(i => i.GetProperty("name").GetString() == name).GetProperty("ref").GetString();

    [Fact]
    public void AnAltClickOpensTheWindowWithTheTileAndWhatLiesOnItTheTurfFirst()
    {
        var (r, bob) = Room(9701);
        var wrench = Place<obj_item_wrench>(r, 5, 4);
        var crowbar = Place<obj_item_crowbar>(r, 5, 4);
        Take(bob);

        Click(r, bob, crowbar, "alt=1");
        JsonElement payload = LastUpdate(Take(bob), out _);
        Assert.Equal("LootPanel", InterfaceOf(payload));
        Assert.Equal(UI_INTERACTIVE, payload.GetProperty("config").GetProperty("status").GetInt32());
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(new[] { r.In(() => (string)((turf)crowbar.loc).name), "wrench", "pocket crowbar" }, Names(data));
        Assert.Equal(0, data.GetProperty("searching").GetInt32());

        JsonElement[] items = Contents(data).EnumerateArray().ToArray();
        // the turf has no path (tg: only an object has one); an object's is its typepath; the icon is the item's own file and state
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("path").ValueKind);
        Assert.Equal("/obj/item/wrench", items[1].GetProperty("path").GetString());
        Assert.Equal(("icons/obj/tools.dmi", "wrench"), (items[1].GetProperty("icon").GetString(), items[1].GetProperty("icon_state").GetString()));
        Assert.Equal(("icons/obj/tools.dmi", "crowbar"), (items[2].GetProperty("icon").GetString(), items[2].GetProperty("icon_state").GetString()));
        r.In(() =>
        {
            datum_lootpanel panel = bob.client.loot_panel;
            Assert.Equal(3f, panel.contents.len);
            Assert.Same(crowbar.loc, panel.source_turf);
            Assert.Equal(0f, panel.to_image.len);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAltClickOutOfReachChecksTheTelekinesisMutationWithoutARuntime()
    {
        // living.dm's alt-click range check asks dna.check_mutation(/datum/mutation/telekinesis) (dna.dm:172, get_mutation :526): a
        // human with no mutations has none, so nothing reaches past the range and nothing throws
        var (r, bob) = Room(9708);
        var crowbar = Place<obj_item_crowbar>(r, 9, 4);
        Take(bob);
        Click(r, bob, crowbar, "alt=1");
        r.In(() => Assert.Null(bob.dna.check_mutation(typeof(datum_mutation_telekinesis))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAltClickOnTheFloorItselfListsTheTileToo()
    {
        var (r, bob) = Room(9707);
        var crowbar = Place<obj_item_crowbar>(r, 5, 5);
        turf floor = r.In(() => (turf)crowbar.loc);
        Take(bob);
        Click(r, bob, floor, "alt=1");
        JsonElement payload = LastUpdate(Take(bob), out _);
        Assert.Equal("LootPanel", InterfaceOf(payload));
        Assert.Equal(new[] { r.In(() => (string)floor.name), "pocket crowbar" }, Names(payload.GetProperty("data")));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMobAndAThingWithManyOverlaysWaitForSSLootingToMakeTheirIcon()
    {
        var (r, bob) = Room(9702);
        var wrench = Place<obj_item_wrench>(r, 5, 4);
        var dummy = r.Dummy(5, 4);
        Take(bob);

        Click(r, bob, wrench, "alt=1");
        // the panel opened with the mob's icon still to make (to_image) and the window says it is searching, as tg's does
        r.In(() =>
        {
            datum_lootpanel panel = bob.client.loot_panel;
            Assert.Contains(Dm.each_cast<datum_search_object>(panel.contents), c => c.item == dummy);
        });
        JsonElement data = LastUpdate(Take(bob), out _).GetProperty("data");
        Assert.Equal(1, data.GetProperty("searching").GetInt32());
        JsonElement mob = Contents(data).EnumerateArray().Single(i => i.GetProperty("ref").GetString() == RefOf(data, r.In(() => (string)dummy.name)));
        Assert.True(mob.GetProperty("icon").ValueKind == JsonValueKind.Null);

        // SSlooting (every half second) makes it and tells the window
        r.Step(Second);
        data = LastUpdate(Take(bob), out _).GetProperty("data");
        Assert.Equal(0, data.GetProperty("searching").GetInt32());
        mob = Contents(data).EnumerateArray().Single(i => i.GetProperty("name").GetString() == r.In(() => (string)dummy.name));
        Assert.Equal(JsonValueKind.String, mob.GetProperty("icon").ValueKind);
        r.In(() => Assert.Equal(0f, bob.client.loot_panel.to_image.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void GrabClicksTheThingAsThePlayerWouldAndTheListFollowsTheTile()
    {
        var (r, bob) = Room(9703);
        Feed feed = Listen(r);
        var wrench = Place<obj_item_wrench>(r, 5, 4);
        var crowbar = Place<obj_item_crowbar>(r, 5, 4);
        Take(bob);
        Click(r, bob, crowbar, "alt=1");
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("LootPanel", InterfaceOf(payload));
        string crowbarRef = RefOf(payload.GetProperty("data"), "pocket crowbar");
        r.Step(Second); // a click is a click: the player's next one comes a tenth of a second after (next_click, click.dm:152)

        // a plain grab is a click with an empty hand: the crowbar comes up into it, and drops out of the list as it leaves the tile
        Act(r, bob, window, "grab", ("ref", crowbarRef), ("alt", false), ("ctrl", false), ("shift", false));
        r.In(() => Assert.Same(crowbar, bob.get_active_held_item()));
        JsonElement data = Data(r, bob, out _);
        Assert.DoesNotContain("pocket crowbar", Names(data));
        Assert.Contains("wrench", Names(data));

        // shift is tg's examine
        string wrenchRef = RefOf(data, "wrench");
        r.Step(Second);
        Act(r, bob, window, "grab", ("ref", wrenchRef), ("shift", true));
        Assert.True(feed.Any("wrench"), string.Join(" | ", feed.Chat.Select(c => c.Html)));

        // a thing that arrives on the tile joins the list (the timer of the Entered signal)
        var driver = Place<obj_item_screwdriver>(r, 5, 4);
        r.Step(Second);
        data = LastUpdate(Take(bob), out _).GetProperty("data");
        Assert.Contains(r.In(() => (string)driver.name), Names(data));

        // a thing that is deleted drops out
        r.In(() => qdel(driver));
        r.Step(Second);
        data = LastUpdate(Take(bob), out _).GetProperty("data");
        Assert.DoesNotContain(r.In(() => (string)driver.name), Names(data));

        // a ref that is not on the list, or no ref, does nothing
        Act(r, bob, window, "grab", ("ref", "[0xdeadbeef]"));
        Act(r, bob, window, "grab");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ClosingTheWindowResetsThePanelAndWhatIsWornIsNoLoot()
    {
        var (r, bob) = Room(9704);
        Feed feed = Listen(r);
        var wrench = Place<obj_item_wrench>(r, 5, 4);
        Take(bob);
        Click(r, bob, wrench, "alt=1");
        LastUpdate(Take(bob), out string window);
        r.In(() => Assert.Equal(2f, bob.client.loot_panel.contents.len));

        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "close", null));
        r.Step(4);
        r.In(() =>
        {
            datum_lootpanel panel = bob.client.loot_panel;
            Assert.Equal(0f, panel.contents.len);
            Assert.Null(panel.source_turf);
        });

        // a crowbar in the player's hand: "You can't search for this item, it's already in your inventory!"
        var held = r.In(() => Dm.@new<obj_item_crowbar>(bob));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(held)));
        Take(bob);
        Click(r, bob, held, "alt=1");
        Assert.DoesNotContain(Take(bob), o => o.Kind == TguiOutputKind.Initialize && o.WindowId != "lobby_menu");
        Assert.True(feed.Any("already in your inventory"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATurfThatIsReplacedTakesItsPanelWithIt()
    {
        var (r, bob) = Room(9706);
        var wrench = Place<obj_item_wrench>(r, 5, 4);
        Take(bob);
        Click(r, bob, wrench, "alt=1");
        LastUpdate(Take(bob), out _);
        turf floor = r.In(() => (turf)wrench.loc);
        r.In(() => Assert.Equal(2f, bob.client.loot_panel.contents.len));

        // the search object of the turf deletes itself after the change (COMSIG_TURF_CHANGE's post change callback): the old tile is gone,
        // so tg's tgui closes the window, which resets the panel
        r.In(() => Waiting.NoSleep(floor.ChangeTurf(typeof(turf_open_floor_plating))));
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Equal(0f, bob.client.loot_panel.contents.len);
            Assert.Null(bob.client.loot_panel.source_turf);
            Assert.Equal(0f, SStgui.all_uis.len);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGhostsAltClickProcOpensItToo()
    {
        var r = new AudioRoom(13, 9, seed: 9705);
        mob_living_carbon_human alice = r.Join(8, 8, "Alice");
        client player = alice.client;
        var wrench = Place<obj_item_wrench>(r, 6, 4);
        r.In(() => Waiting.Detach(alice.gib()));
        r.Step();
        var ghost = Assert.IsType<mob_dead_observer>(player.mob);
        r.In(() => Waiting.NoSleep(ghost.forceMove(locate(5, 4, 1))));
        r.Step(Second);
        Take(ghost);

        // tg's observer ClickOn sends an alt-click to base_click_alt, which a ghost does not override: only its AltClickOn (observer.dm:926)
        // opens the panel, and nothing in tg's click code calls that for a ghost. The proc itself is translated and works.
        Click(r, ghost, wrench, "alt=1");
        Assert.DoesNotContain(Take(ghost), o => o.Kind == TguiOutputKind.Initialize && o.WindowId != "lobby_menu");
        r.Command(() => Waiting.NoSleep(ghost.AltClickOn(wrench)));
        r.Step(4);
        JsonElement payload = LastUpdate(Take(ghost), out _);
        Assert.Equal("LootPanel", InterfaceOf(payload));
        Assert.Contains("wrench", Names(payload.GetProperty("data")));
        Assert.Equal(UI_UPDATE, payload.GetProperty("config").GetProperty("status").GetInt32()); // a ghost looks, it does not take
        r.AssertNoRuntimes();
    }
}
