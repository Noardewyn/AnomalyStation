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
/// Gaps package H2 (D333): the smart fridges and the dehydrator (food_and_drinks/machinery/smartfridge.dm) in a booted toy room. A food
/// fridge takes food from the hand and refuses what it does not keep, its SmartVend window lists the food by type and name, Release puts
/// it in the hand; its contents show on it (tg's contents overlay); the dehydrator takes only what dries, and Dry turns a slab into jerky.
/// </summary>
public class SmartFridgeH2Tests
{
    private const int X = 5, Y = 4;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(X, Y, 1)) as area).requires_power = FALSE); // the toy room has no APC
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        Click(r, who, target);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void AFoodFridgeKeepsFoodListsItInSmartVendAndReleasesItToTheHand()
    {
        var (r, feed, bob) = Room(7301);
        var fridge = Place<obj_machinery_smartfridge_food>(r, X, Y);
        r.In(() => Assert.Equal("smartfridge", fridge.icon_state));

        // two slabs go in one by one; a crowbar is refused
        for (int i = 0; i < 2; i++)
        {
            var slab = Hold<obj_item_food_meat_slab>(r, bob);
            Use(r, bob, fridge);
            r.In(() => Assert.Same(fridge, slab.loc));
        }
        Assert.True(feed.Any("You add"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        var crowbar = Hold<obj_item_crowbar>(r, bob);
        r.In(() => bob.combat_mode = FALSE);
        r.In(() => Waiting.NoSleep(fridge.item_interaction(bob, crowbar, Dm.list())));
        Assert.True(feed.Any("smartly refuses"));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(crowbar)));

        // tg's contents overlay of a food fridge, and its glass
        r.Step(2);
        Assert.Contains("smartfridge-food-1", Overlays(r, fridge));
        Assert.Contains("smartfridge-glass", Overlays(r, fridge));

        // the window: the slabs as one entry of two, keyed by type and name
        Take(bob);
        Use(r, bob, fridge);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("SmartVend", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(0, data.GetProperty("isdryer").GetInt32());
        var entries = data.GetProperty("contents").EnumerateObject().ToList();
        Assert.True(entries.Count == 1, "contents: " + string.Join(", ", entries.Select(e => e.Name)));
        JsonElement entry = entries.Single().Value;
        string path = entry.GetProperty("path").GetString();
        Assert.StartsWith("/obj/item/food/meat/slab-", path);
        Assert.Equal(2, entry.GetProperty("amount").GetInt32());

        // Release one: a slab in Bob's hand, one left
        Act(r, bob, window, "Release", ("path", path), ("amount", 1));
        r.In(() => Assert.IsType<obj_item_food_meat_slab>(bob.get_active_held_item()));
        r.In(() => Assert.Single(Dm.each<obj_item_food_meat_slab>(fridge.contents)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDehydratorTakesOnlyWhatDriesAndDryTurnsASlabIntoJerky()
    {
        var (r, feed, bob) = Room(7302);
        var dryer = Place<obj_machinery_smartfridge_drying>(r, X, Y);

        var crowbar = Hold<obj_item_crowbar>(r, bob);
        r.In(() => Waiting.NoSleep(dryer.item_interaction(bob, crowbar, Dm.list())));
        Assert.True(feed.Any("smartly refuses"));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(crowbar)));

        var slab = Hold<obj_item_food_meat_slab>(r, bob);
        Use(r, bob, dryer);
        r.In(() => Assert.Same(dryer, slab.loc));
        Assert.Contains("dehydrator-off", Overlays(r, dryer));

        Take(bob);
        Use(r, bob, dryer);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal(1, payload.GetProperty("data").GetProperty("isdryer").GetInt32());
        Act(r, bob, window, "Dry");
        r.In(() => Assert.Equal(1f, dryer.drying));
        Assert.Contains("dehydrator-on", Overlays(r, dryer));

        // the next machine tick dries it: the slab becomes healthy jerky, still inside
        r.Step(5 * Second);
        r.In(() => Assert.True(QDELETED(slab)));
        r.In(() => Assert.Single(Dm.each<obj_item_food_sosjerky_healthy>(dryer.contents)));
        r.AssertNoRuntimes();
    }
}
