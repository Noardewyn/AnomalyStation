using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>Lavaland L2a (D304): the client's OreBox, OreRedemptionMachine and OreSilo windows (tg's TSX) without the engine.</summary>
public class MiningWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string name, TguiInterface render, string json, TguiLocalState state = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), state ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key && n.Kind != UiKind.StackItem);

    [Fact]
    public void TheOreBoxListsItsOresAndEmptiesThem()
    {
        var (context, tree, acts) = Render("OreBox", OreBox.Render, "{\"materials\":[{\"name\":\"iron ore\",\"amount\":5},{\"name\":\"Boulders\",\"amount\":2}]}");
        Assert.Equal((335.0, 415.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Text == "Iron Ore");
        ByKey(tree, "empty").OnClick();
        Assert.Equal(new (string, string)[] { ("removeall", null) }, acts);
        var (_, empty, _) = Render("OreBox", OreBox.Render, "{\"materials\":[]}");
        Assert.True(ByKey(empty, "empty").Disabled);
    }

    [Fact]
    public void TheRedemptionMachineReleasesSheetsAndClaimsPoints()
    {
        string json = "{\"unclaimedPoints\":40,\"disconnected\":null,\"user\":{\"name\":\"Ann\",\"cash\":120},\"materials\":["
            + "{\"name\":\"iron\",\"id\":\"[0x21]\",\"amount\":12,\"category\":\"material\",\"value\":1,\"icon\":\"icons/obj/stack_objects.dmi\",\"icon_state\":\"sheet-metal\"},"
            + "{\"name\":\"plasma\",\"id\":\"[0x22]\",\"amount\":0,\"category\":\"material\",\"value\":15,\"icon\":\"icons/obj/stack_objects.dmi\",\"icon_state\":\"sheet-plasma\"}]}";
        var (_, tree, acts) = Render("OreRedemptionMachine", OreRedemptionMachine.Render, json);
        Assert.Contains(tree.Walk(), n => n.Text == "12.00 sheets"); // tgui-core's formatSiUnit keeps two decimals
        Assert.Equal("120", ByKey(tree, "balance").Text);
        tree.Walk().First(n => n.Key == "x5").OnClick();
        ByKey(tree, "claim").OnClick();
        Assert.Equal(("Release", "{\"material_ref\":\"[0x21]\",\"sheets\":5}"), acts[0]);
        Assert.Equal(("Claim", null), acts[1]);

        // a disconnected machine cannot claim
        var (_, off, _) = Render("OreRedemptionMachine", OreRedemptionMachine.Render, json.Replace("\"disconnected\":null", "\"disconnected\":\"Mineral withdrawal is on hold\""));
        Assert.True(ByKey(off, "claim").Disabled);
    }

    [Fact]
    public void TheSiloShowsItsLogsAndMachinesAndEjectsFromItsBar()
    {
        string json = "{\"SHEET_MATERIAL_AMOUNT\":100,\"ID_required\":1,\"banned_users\":[],"
            + "\"materials\":[{\"name\":\"iron\",\"ref\":\"[0x31]\",\"amount\":1200,\"color\":\"#878687\"}],"
            + "\"machines\":[{\"icon\":\"icons/obj/machines/mining_machines.dmi\",\"icon_state\":\"ore_redemption\",\"name\":\"ore redemption machine\",\"on_hold\":0,\"location\":\"Cargo Bay\"}],"
            + "\"logs\":[{\"raw_materials\":\"+10 iron\",\"machine_name\":\"ore redemption machine\",\"area_name\":\"Cargo Bay\",\"action\":\"DEPOSITED\",\"amount\":1000,\"time\":\"00:05:00\",\"noun\":\"iron ore\","
            + "\"user_data\":{\"name\":\"Ann\",\"assignment\":\"Shaft Miner\",\"account_id\":123456}}]}";
        var state = new TguiLocalState();
        var (context, tree, acts) = Render("OreSilo", OreSilo.Render, json, state);
        Assert.Equal((620.0, 600.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("DEPOSITED", ByKey(tree, "action").Text);
        Assert.Equal("green", ByKey(tree, "action").Color);
        Assert.Equal("Disable ID Requirement", ByKey(tree, "restrict").Text);
        tree.Walk().First(n => n.Key == "eject" && n.Text == "×10").OnClick();
        Assert.Equal(("eject", "{\"ref\":\"[0x31]\",\"amount\":10}"), acts[0]);

        // the entry opens on a click and offers to ban its user
        ByKey(tree, "expand").OnClick();
        var (_, open, openActs) = Render("OreSilo", OreSilo.Render, json, state);
        ByKey(open, "ban").OnClick();
        Assert.Equal("toggle_ban", openActs[0].Action);
        Assert.Contains("123456", openActs[0].Params);

        // the connections tab lists the machine; its buttons hold and remove it by tg's 1-based index
        ByKey(open, "tab0").OnClick();
        var (_, machines, machineActs) = Render("OreSilo", OreSilo.Render, json, state);
        Assert.Contains(machines.Walk(), n => n.Text == "ore redemption machine (Cargo Bay)");
        ByKey(machines, "pause").OnClick();
        ByKey(machines, "remove").OnClick();
        Assert.Equal(new[] { ("hold", "{\"id\":1}"), ("remove", "{\"id\":1}") }, machineActs);
    }

    [Fact]
    public void TheStackingConsoleListsItsSheetsAndRotates()
    {
        var (context, none, _) = Render("StackingConsole", StackingConsole.Render, "{\"machine\":0,\"stacking_amount\":null,\"contents\":[]}");
        Assert.Equal((320.0, 340.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("No connected stacking machine", ByKey(none, "none").Text);

        var (_, tree, acts) = Render("StackingConsole", StackingConsole.Render, "{\"machine\":1,\"stacking_amount\":50,\"input_direction\":\"east\",\"output_direction\":\"west\","
            + "\"contents\":[{\"type\":\"/obj/item/stack/sheet/iron\",\"name\":\"Iron\",\"amount\":10}]}");
        Assert.Equal("50", ByKey(tree, "amount").Text);
        Assert.Equal("East", ByKey(tree, "input").Text);
        Assert.Equal("10", ByKey(tree, "sheet:/obj/item/stack/sheet/iron").Text);
        ByKey(tree, "release").OnClick();
        ByKey(tree, "rotateOutput").OnClick();
        Assert.Equal(new (string, string)[] { ("release", "{\"type\":\"/obj/item/stack/sheet/iron\"}"), ("rotate", "{\"input\":0}") }, acts);
    }

    [Fact]
    public void TheProcessingConsolePicksTheMaterialAndTurnsTheFurnaceOn()
    {
        string json = "{\"SHEET_MATERIAL_AMOUNT\":100,\"state\":0,\"selectedMaterial\":\"iron\",\"selectedAlloy\":null,\"alloys\":[],\"alloyIcons\":[],"
            + "\"materials\":[{\"name\":\"iron\",\"ref\":\"[0x21]\",\"amount\":1250,\"color\":\"#878687\"},{\"name\":\"glass\",\"ref\":\"[0x22]\",\"amount\":100,\"color\":\"#88cdf1\"}],"
            + "\"materialIcons\":[{\"id\":\"[0x21]\",\"icon\":\"icons/obj/stack_objects.dmi\",\"icon_state\":\"sheet-metal\"}]}";
        var (context, tree, acts) = Render("ProcessingConsole", ProcessingConsole.Render, json);
        Assert.Equal((580.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Text == "12.00 sheets"); // tgui-core's formatSiUnit keeps two decimals
        Assert.Contains(tree.Walk(), n => n.Text == "1.00 sheet");
        Assert.Equal("sheet-metal", ByKey(tree, "mat:iron").Walk().First(n => n.Key == "pic").DmIconState);
        Assert.True(ByKey(tree, "mat:iron").Walk().First(n => n.Key == "check").Checkbox);
        Assert.False(ByKey(tree, "mat:glass").Walk().First(n => n.Key == "check").Checkbox);
        Assert.Contains(tree.Walk(), n => n.Text == "No alloy recipes found!");
        ByKey(tree, "mat:glass").Walk().First(n => n.Key == "check").OnClick();
        ByKey(tree, "toggle").OnClick();
        Assert.Equal(new (string, string)[] { ("setMaterial", "{\"value\":\"[0x22]\"}"), ("toggle", null) }, acts);
        Assert.Equal("Activate", ByKey(tree, "toggle").Text);
    }

    [Fact]
    public void TheLaborClaimConsoleClaimsPointsAndSendsTheShuttleWhenTheGoalIsMet()
    {
        var (context, tree, acts) = Render("LaborClaimConsole", LaborClaimConsole.Render,
            "{\"status_info\":\"You are 80 points away.\",\"id_points\":20,\"unclaimed_points\":15,\"can_go_home\":0,\"shuttle_exists\":1}");
        Assert.Equal((315.0, 440.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("You are 80 points away.", ByKey(tree, "info").Text);
        Assert.True(ByKey(tree, "move").Disabled);
        ByKey(tree, "claim").OnClick();
        Assert.Equal(new (string, string)[] { ("claim_points", null) }, acts);

        var (_, home, homeActs) = Render("LaborClaimConsole", LaborClaimConsole.Render,
            "{\"status_info\":\"Goal met!\",\"id_points\":100,\"unclaimed_points\":0,\"can_go_home\":1,\"shuttle_exists\":1}");
        Assert.True(ByKey(home, "claim").Disabled);
        ByKey(home, "move").OnClick();
        Assert.Equal(new (string, string)[] { ("move_shuttle", null) }, homeActs);

        // no camp shuttle: no shuttle controls
        var (_, noShuttle, _) = Render("LaborClaimConsole", LaborClaimConsole.Render, "{\"status_info\":\"No Prisoner ID detected.\",\"id_points\":0,\"can_go_home\":0,\"shuttle_exists\":0}");
        Assert.DoesNotContain(noShuttle.Walk(), n => n.Key == "move");
    }

    [Fact]
    public void TheWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)OreBox.Render, TguiInterfaces.Find("OreBox"));
        Assert.Equal((TguiInterface)OreRedemptionMachine.Render, TguiInterfaces.Find("OreRedemptionMachine"));
        Assert.Equal((TguiInterface)OreSilo.Render, TguiInterfaces.Find("OreSilo"));
        Assert.Equal((TguiInterface)StackingConsole.Render, TguiInterfaces.Find("StackingConsole"));
        Assert.Equal((TguiInterface)ProcessingConsole.Render, TguiInterfaces.Find("ProcessingConsole"));
        Assert.Equal((TguiInterface)LaborClaimConsole.Render, TguiInterfaces.Find("LaborClaimConsole"));
    }
}
