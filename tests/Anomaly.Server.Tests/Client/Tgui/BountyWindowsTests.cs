using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps P4c (D298): the civilian bounty windows without the engine: CivCargoHoldTerminal's personal tab (the pad buttons, the three
/// choices, the bounty info) and station tab (the list, a bounty's progress and Send &amp; Claim), and the NtosCivCargoHoldTerminal app.
/// </summary>
public class BountyWindowsTests
{
    private static (UiNode Tree, TguiContext Context, List<(string Action, string Params)> Acts) Render(Func<TguiContext, UiNode> render, string name, string json, TguiLocalState local = null, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var c = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (render(c), c, acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Picking = "{\"points\":0,\"pad\":1,\"sending\":0,\"status_report\":\"Ready for delivery.\",\"id_inserted\":\"[0x2]\",\"claimed_bounties\":2,"
        + "\"picking\":1,\"id_bounty_names\":[\"Soap\",\"Spears\",\"Toolbox\"],\"id_bounty_infos\":[\"<b>Soap</b> gone\",\"Spears!\",\"Robust\"],\"id_bounty_values\":[300,300,240],"
        + "\"listBounty\":[{\"name\":\"Gondola Hide\",\"description\":\"A <i>hide</i>.\",\"reward\":2000,\"claimed\":0,\"shipped\":1,\"maximum\":3,\"priority\":1,\"unique\":0}]}";

    [Fact]
    public void ThePersonalTabPicksABountyAndRunsThePad()
    {
        var (tree, context, acts) = Render(CivCargoHoldTerminal.Render, "CivCargoHoldTerminal", Picking);
        Assert.Equal((580.0, 375.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Welcome valued employee.", ByKey(tree, "notice").Text);
        Assert.Equal(("Online", "good"), (ByKey(tree, "status").Text, ByKey(tree, "status").Color));
        Assert.Equal("Payout: 300 cr", ByKey(ByKey(tree, "choice:2"), "payout").Text);
        Assert.Equal("<b>Soap</b> gone", ByKey(ByKey(tree, "choice:1"), "info").TextValue);
        ByKey(ByKey(tree, "choice:3"), "pick").OnClick();
        ByKey(tree, "recalc").OnClick();
        ByKey(tree, "send").OnClick();
        ByKey(tree, "eject").OnClick();
        Assert.Equal(new (string, string)[] { ("pick", "{\"value\":3}"), ("recalc", null), ("send", null), ("eject", null) }, acts);

        string chosen = Picking.Replace("\"picking\":1", "\"picking\":0,\"id_bounty_info\":\"Ship soap.\",\"id_bounty_num\":\"1/3\",\"id_bounty_value\":300");
        var (info, _, _) = Render(CivCargoHoldTerminal.Render, "CivCargoHoldTerminal", chosen);
        Assert.Equal("Ship soap.", ByKey(info, "description").Text);
        Assert.Equal("1/3", ByKey(info, "quantity").Text);
        Assert.Equal(("recycle", "Replace Bounty"), (ByKey(info, "bounty").Icon, ByKey(info, "bounty").Tooltip));

        var (empty, _, _) = Render(CivCargoHoldTerminal.Render, "CivCargoHoldTerminal", "{\"pad\":0,\"sending\":0,\"status_report\":\"\",\"id_inserted\":null,\"picking\":0,\"listBounty\":[],\"claimed_bounties\":0}");
        Assert.Equal("To begin, insert your ID into the console.", ByKey(empty, "notice").Text);
        Assert.True(ByKey(empty, "bounty").Disabled);
        Assert.Equal("N/A, please add a new bounty.", ByKey(empty, "description").Text);
    }

    [Fact]
    public void TheStationTabListsTheBountiesAndClaimsOne()
    {
        var local = new TguiLocalState();
        var (tree, _, _) = Render(CivCargoHoldTerminal.Render, "CivCargoHoldTerminal", Picking, local);
        ByKey(tree, "tab:station").OnClick();
        var (station, _, acts) = Render(CivCargoHoldTerminal.Render, "CivCargoHoldTerminal", Picking, local);
        Assert.Equal("2 bounties served!", ByKey(station, "served").Text);
        UiNode tab = ByKey(station, "bounty:Gondola Hide");
        Assert.Equal(("star", "#cec328a8"), (tab.Icon, tab.Background));
        Assert.Equal("Please select a bounty from the list.", ByKey(station, "select").Text);
        tab.OnClick();
        var (detail, _, _) = Render(CivCargoHoldTerminal.Render, "CivCargoHoldTerminal", Picking, local, acts);
        Assert.Equal("1 / 3 shipped.", ByKey(detail, "progress").Text);
        Assert.Equal("Reward: 2000 Credits", ByKey(detail, "reward").Text);
        Assert.Equal("You'll receive a cut of 600 Credits.", ByKey(detail, "reward").Tooltip);
        ByKey(detail, "claim").OnClick();
        ByKey(detail, "print").OnClick();
        Assert.Equal(new (string, string)[] { ("send", "{\"global\":true}"), ("print", null) }, acts);

        var (none, _, noneActs) = Render(CivCargoHoldTerminal.Render, "CivCargoHoldTerminal", Picking.Replace("\"listBounty\":[{", "\"listBounty\":[],\"x\":[{"), local);
        ByKey(none, "update").OnClick();
        Assert.Equal(("update_list", (string)null), Assert.Single(noneActs));
    }

    [Fact]
    public void TheAppHandsOutABounty()
    {
        string json = Picking.Replace("{\"points\"", "{\"PC_device_theme\":\"ntos\",\"points\"");
        var (tree, context, acts) = Render(NtosCivCargoHoldTerminal.Render, "NtosCivCargoHoldTerminal", json);
        Assert.Equal((580.0, 375.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Robust", ByKey(ByKey(tree, "choice:3"), "infoBox").Text); // the app draws the description as text
        ByKey(tree, "bounty").OnClick();
        ByKey(ByKey(tree, "choice:1"), "pick").OnClick();
        Assert.Equal(new (string, string)[] { ("bounty", null), ("pick", "{\"value\":1}") }, acts);
    }
}
