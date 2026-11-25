using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>Gaps package SP2c: SlimeBodySwapper.jsx lists the slimeperson's bodies the way the JSX does and sends tg's swap with the body's ref.</summary>
public class SlimeBodySwapperWindowTests
{
    [Fact]
    public void The_body_swapper_lists_each_body_and_sends_swap_with_its_ref()
    {
        var acts = new List<(string Action, string Params)>();
        const string json = "{\"bodies\":["
            + "{\"htmlcolor\":\"#7fff7f\",\"area\":\"Bridge\",\"status\":\"Stable\",\"exoticblood\":560,\"name\":\"Bob\",\"ref\":\"[0x1]\",\"occupied\":\"owner\"},"
            + "{\"htmlcolor\":\"#ffffff\",\"area\":\"Medbay\",\"status\":\"Dead\",\"exoticblood\":12,\"name\":\"Bob (2)\",\"ref\":\"[0x2]\",\"occupied\":\"available\"}]}";
        var context = new TguiContext(1, "SlimeBodySwapper", "Swap Body", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        UiNode tree = SlimeBodySwapper.Render(context);
        Assert.Equal((400.0, 400.0), (context.WindowWidth, context.WindowHeight));
        UiNode own = tree.Walk().Single(n => n.Key == "swap-Bob");
        Assert.Equal("You Are Here", own.Text);
        Assert.True(own.Selected);
        UiNode other = tree.Walk().Single(n => n.Key == "swap-Bob (2)");
        Assert.Equal("Swap", other.Text);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.LabeledItem && n.Label == "Jelly");
        other.OnClick();
        Assert.Equal("swap", acts[0].Action);
        Assert.Contains("[0x2]", acts[0].Params);
    }

    [Fact]
    public void The_window_is_in_the_interface_registry()
    {
        Assert.NotNull(TguiInterfaces.Find("SlimeBodySwapper"));
    }
}
