using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Syndicate stage G1b (D316): the agent card's ChameleonCard window without the engine: ChameleonCard.jsx keeps the stolen card's
/// accesses outside the agent card's trim, and AccessList.jsx shows those its selected wildcard can hold and sends tg's act.
/// </summary>
public class ChameleonCardWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, UiValue Params)> Acts) Render(string json)
    {
        var acts = new List<(string, UiValue)>();
        var context = new TguiContext(1, "ChameleonCard", "agent card", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p)), new TguiLocalState());
        return (context, ChameleonCard.Render(context), acts);
    }

    private const string Data = "{\"showBasic\":0,\"accesses\":[{\"name\":\"Security\",\"accesses\":[{\"desc\":\"Brig\",\"ref\":\"brig\"},"
        + "{\"desc\":\"Armory\",\"ref\":\"armory\"},{\"desc\":\"Maintenance\",\"ref\":\"maint_tunnels\"}]},{\"name\":\"General\",\"accesses\":"
        + "[{\"desc\":\"Bar\",\"ref\":\"bar\"}]}],\"ourAccess\":[\"maint_tunnels\"],\"ourTrimAccess\":[\"maint_tunnels\"],\"theftAccess\":[\"brig\",\"armory\"],"
        + "\"wildcardSlots\":{\"common\":{\"limit\":3,\"usage\":[]}},\"selectedList\":[\"maint_tunnels\"],\"trimAccess\":[],"
        + "\"accessFlags\":{\"brig\":1,\"armory\":2,\"bar\":1,\"maint_tunnels\":1},\"wildcardFlags\":{\"common\":1},\"accessFlagNames\":{\"1\":\"Common\",\"2\":\"Command\"}}";

    [Fact]
    public void The_stolen_cards_accesses_outside_the_trim_show_under_the_wildcard_that_can_hold_them()
    {
        var (context, tree, acts) = Render(Data);
        Assert.Equal((500.0, 620.0), (context.WindowWidth, context.WindowHeight));
        // the region tabs: only Security has a stolen access; the agent card's own trim access is snipped
        List<UiNode> regionTabs = tree.Walk().Where(n => n.Kind == UiKind.Tab && n.Icon != null).ToList();
        Assert.Equal(new[] { "Security" }, regionTabs.Select(t => t.Text));
        Assert.Equal("times", regionTabs[0].Icon);
        // the wildcard tab counts its usage
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Tab && n.Text == "common:\n0/3");
        // the common wildcard holds the brig (flag 1) but not the armory (flag 2)
        List<UiNode> boxes = tree.Walk().Where(n => n.Kind == UiKind.Button && n.Checkbox != null).ToList();
        Assert.Equal(new[] { "Brig (Common)" }, boxes.Select(b => b.Text));
        Assert.False(boxes[0].Checkbox);
        boxes[0].OnClick();
        Assert.Single(acts);
        Assert.Equal("mod_access", acts[0].Action);
        Assert.Equal("brig", acts[0].Params.Str("access_target"));
        Assert.Equal("common", acts[0].Params.Str("access_wildcard"));
    }
}
