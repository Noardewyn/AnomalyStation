using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps DV2: the client's ImplantPad window (tg's ImplantPad.tsx) shows the case's state, the deathrattle groups with their three
/// buttons and the implant's information and lore; the ImplantChair window (ImplantChair.jsx) shows the occupant, the door, the
/// implanting with its recharge cog and the implants left. Both are registered under their names.
/// </summary>
public class ImplantWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheImplantPadShowsTheCaseTheGroupsAndTheImplant()
    {
        Assert.NotNull(TguiInterfaces.Find("ImplantPad"));
        var (context, empty, _) = Render("ImplantPad", ImplantPad.Render, "{\"saved_deathrattle_group\":null,\"current_deathrattle_group\":null,\"has_case\":0}");
        Assert.Equal((300.0, 350.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("Implant Mini-Computer", empty.AllText());
        Assert.Contains("No implant case detected.", ByKey(empty, "information_text").Text);
        Assert.True(ByKey(empty, "eject").Disabled);
        Assert.True(ByKey(empty, "set").Disabled);
        Assert.Equal("Saved Deathrattle Group: None", ByKey(empty, "saved").Text);

        var (_, nocase, _) = Render("ImplantPad", ImplantPad.Render, "{\"has_case\":1,\"has_implant\":0}");
        Assert.Contains("does not have an implant", ByKey(nocase, "information_text").Text);
        Assert.False(ByKey(nocase, "set").Disabled);
        Assert.True(ByKey(nocase, "save").Disabled);

        var (_, tree, acts) = Render("ImplantPad", ImplantPad.Render,
            "{\"has_case\":1,\"has_implant\":1,\"case_information\":\"Name: Nanotrasen Employee Management Implant\",\"case_lore\":\"Some lore.\","
            + "\"saved_deathrattle_group\":\"g1\",\"current_deathrattle_group\":\"g2\"}");
        Assert.Equal("Name: Nanotrasen Employee Management Implant", ByKey(tree, "information_text").Text);
        Assert.True(ByKey(tree, "information").Open);
        Assert.False(ByKey(tree, "extended").Open);
        Assert.Equal("Some lore.", ByKey(tree, "extended_text").Text);
        Assert.Equal("Current Deathrattle Group: g2", ByKey(tree, "current").Text);
        Assert.False(ByKey(tree, "save").Disabled);
        Assert.True(ByKey(tree, "init").Disabled);
        ByKey(tree, "eject").OnClick();
        ByKey(tree, "save").OnClick();
        ByKey(tree, "set").OnClick();
        ByKey(tree, "init").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("eject_implant", null), ("save_deathrattle_group", null), ("set_deathrattle_group", null), ("init_deathrattle_group", null),
        }, acts);
    }

    [Fact]
    public void TheImplantChairShowsItsOccupantDoorAndImplants()
    {
        Assert.NotNull(TguiInterfaces.Find("ImplantChair"));
        var (context, empty, _) = Render("ImplantChair", ImplantChair.Render,
            "{\"occupied\":0,\"open\":1,\"occupant\":[],\"special_name\":null,\"ready_implants\":5,\"ready\":1,\"replenishing\":0}");
        Assert.Equal((375.0, 280.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("No Occupant", ByKey(empty, "name").Text);
        Assert.DoesNotContain(empty.Walk(), n => n.Key == "status");
        Assert.Equal("Open", ByKey(empty, "door").Text);
        Assert.Equal("unlock", ByKey(empty, "door").Icon);
        Assert.Equal("Implant", ByKey(empty, "implant").Text);
        Assert.Equal("5", ByKey(empty, "remaining_row").Text);
        Assert.DoesNotContain(empty.Walk(), n => n.Key == "cog" || n.Key == "sync");

        var (_, tree, acts) = Render("ImplantChair", ImplantChair.Render,
            "{\"occupied\":1,\"open\":0,\"occupant\":{\"name\":\"Bob\",\"stat\":1},\"special_name\":\"Mindshield\",\"ready_implants\":4,\"ready\":0,\"replenishing\":1}");
        Assert.Equal("Bob", ByKey(tree, "name").Text);
        Assert.Equal("Unconscious", ByKey(tree, "status").Text);
        Assert.Equal("average", ByKey(tree, "status").Color);
        Assert.Equal("Closed", ByKey(tree, "door").Text);
        Assert.Equal("red", ByKey(tree, "door").Color);
        Assert.Equal("Recharging", ByKey(tree, "implant").Text);
        Assert.True(ByKey(tree, "cog").Spin);
        Assert.True(ByKey(tree, "sync").Spin);
        ByKey(tree, "door").OnClick();
        ByKey(tree, "implant").OnClick();
        Assert.Equal(new (string, string)[] { ("door", null), ("implant", null) }, acts);

        var (_, ready, _) = Render("ImplantChair", ImplantChair.Render,
            "{\"occupied\":1,\"open\":0,\"occupant\":{\"name\":\"Bob\",\"stat\":2},\"special_name\":\"Mindshield\",\"ready_implants\":4,\"ready\":1,\"replenishing\":0}");
        Assert.Equal("Mindshield", ByKey(ready, "implant").Text);
        Assert.Equal("Dead", ByKey(ready, "status").Text);
    }
}
