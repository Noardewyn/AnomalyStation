using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The fishing windows (tg's FishingRod.tsx and FishingCalculator.tsx) without the engine: the rod's three slots with the slotted
/// part's picture and name, each sending tg's slot act; the admins' calculator's pickers, bait input and results table with each
/// catch's share of the total weight.
/// </summary>
public class FishingWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "rod", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void TheRodWindowShowsEachSlotsPartAndSendsTheSlotAct()
    {
        var (context, tree, acts) = Render(FishingRod.Render, "FishingRod",
            "{\"bait_name\":\"worm\",\"bait_icon\":{\"icon\":\"icons/obj/fishing.dmi\",\"icon_state\":\"worm\"},\"line_name\":null,\"line_icon\":null,"
            + "\"hook_name\":\"simple fishing hook\",\"hook_icon\":{\"icon\":\"icons/obj/fishing.dmi\",\"icon_state\":\"hook\"},\"busy\":0,\"description\":\"A plain rod.\"}");
        Assert.Equal((400.0, 450.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Bait", "Line", "Hook" }, new[] { "slot-bait", "slot-line", "slot-hook" }.Select(k => ByKey(tree, k).Title));
        UiNode bait = ByKey(tree, "slot-bait");
        Assert.Equal(("icons/obj/fishing.dmi", "worm", "64px"), (ByKey(bait, "icon").DmIcon, ByKey(bait, "icon").DmIconState, (string)ByKey(bait, "icon").Width));
        Assert.Equal("worm", ByKey(bait, "label").Text);
        UiNode line = ByKey(tree, "slot-line");
        Assert.DoesNotContain(line.Walk(), n => n.Key == "icon");
        Assert.Equal("None", ByKey(line, "label").Text);
        Assert.Equal("simple fishing hook", ByKey(ByKey(tree, "slot-hook"), "label").Text);
        Assert.Equal("A plain rod.", ByKey(tree, "description").Text);

        ByKey(bait, "action").OnClick();
        ByKey(line, "action").OnClick();
        Assert.Equal(new (string, string)[] { ("slot_action", "{\"slot\":\"bait\"}"), ("slot_action", "{\"slot\":\"line\"}") }, acts);
    }

    [Fact]
    public void TheCalculatorWindowPicksTheGearAndListsEachCatchsChance()
    {
        const string types = "\"spot_types\":[\"/datum/fish_source/toilet\",\"/datum/fish_source/lavaland\"],\"rod_types\":[\"/obj/item/fishing_rod\"],"
            + "\"hook_types\":[\"/obj/item/fishing_hook\",\"/obj/item/fishing_hook/magnet\"],\"line_types\":[\"/obj/item/fishing_line\"]";
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(FishingCalculator.Render, "FishingCalculator", "{" + types + ",\"info\":[]}", local);
        Assert.Equal((500.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "/datum/fish_source/toilet", "/datum/fish_source/lavaland" }, ByKey(tree, "spot").Options);
        Assert.Equal("/datum/fish_source/toilet", ByKey(tree, "spot").TextValue);
        Assert.Equal("/obj/item/food/bait/worm", ByKey(tree, "bait").TextValue);
        Assert.Equal(new[] { "Outcome", "Weight", "Probabilty", "Difficulty", "Count" },
            ByKey(tree, "header").Children.Select(n => n.Text));
        Assert.True(ByKey(tree, "header").Children.All(n => n.Bold));

        // pick another spot and hook, type a bait, then calculate
        ByKey(tree, "spot").OnSelected("/datum/fish_source/lavaland");
        ByKey(tree, "hook").OnSelected("/obj/item/fishing_hook/magnet");
        ByKey(tree, "bait").OnInput("/obj/item/food/bait/doughball");
        var (_, picked, pickedActs) = Render(FishingCalculator.Render, "FishingCalculator", "{" + types + ",\"info\":[]}", local);
        Assert.Equal("/datum/fish_source/lavaland", ByKey(picked, "spot").TextValue);
        ByKey(picked, "calculate").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("recalc", "{\"rod\":\"/obj/item/fishing_rod\",\"bait\":\"/obj/item/food/bait/doughball\",\"hook\":\"/obj/item/fishing_hook/magnet\",\"line\":\"/obj/item/fishing_line\",\"spot\":\"/datum/fish_source/lavaland\"}"),
        }, pickedActs);
        Assert.Empty(acts);

        // the results: each weight over the total, rounded to two places
        var (_, results, _) = Render(FishingCalculator.Render, "FishingCalculator", "{" + types
            + ",\"info\":[{\"result\":\"/obj/item/fish/lavaloop\",\"weight\":2,\"difficulty\":40,\"count\":\"Infinite\"},"
            + "{\"result\":\"/obj/structure/closet/crate/necropolis/tendril\",\"weight\":1,\"difficulty\":45,\"count\":1}]}", local);
        List<UiNode> rows = results.Walk().Where(n => n.Kind == UiKind.TableRow && n.Key.StartsWith("result-")).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "/obj/item/fish/lavaloop", "2", "66.67%", "40", "Infinite" }, rows[0].Children.Select(n => n.Text));
        Assert.Equal("33.33%", ByKey(rows[1], "chance").Text);
    }

    [Fact]
    public void TheTwoWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)FishingRod.Render, TguiInterfaces.Find("FishingRod"));
        Assert.Equal((TguiInterface)FishingCalculator.Render, TguiInterfaces.Find("FishingCalculator"));
    }
}
