using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps SC2 (D333): the client's WarrantConsole window (tg's WarrantConsole.tsx) of the security warrant console: the crew with
/// citations sorted by name, the selected one's citations in their fine's colour, a bounty printed for a fine still due and a payment
/// of 5 credits up to the fine, confirmed; it is registered under its name.
/// </summary>
public class WarrantConsoleWindowTests
{
    private sealed class Window
    {
        public string Json;
        public TguiLocalState Local = new();
        public List<(string Action, string Params)> Acts = new();
        public TguiContext Context;

        public UiNode Render()
        {
            Context = new TguiContext(1, "WarrantConsole", "security warrant console", 2, UiValue.FromJson(Json), (a, p) => Acts.Add((a, p?.ToJson())), Local);
            return WarrantConsole.Render(Context);
        }
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Data = "{\"records\":["
        + "{\"crew_name\":\"Zed\",\"crew_ref\":\"[0x2]\",\"notes\":null,\"rank\":\"Assistant\",\"citations\":["
        + "{\"author\":\"Bob\",\"details\":\"Dropped a peel\",\"fine\":800,\"fine_name\":\"Littering\",\"fine_ref\":\"[0x7]\",\"paid\":0,\"time\":\"12:00\"},"
        + "{\"author\":\"Bob\",\"details\":\"Too loud\",\"fine\":0,\"fine_name\":\"Noise\",\"fine_ref\":\"[0x8]\",\"paid\":50,\"time\":\"12:05\"}]},"
        + "{\"crew_name\":\"Alice\",\"crew_ref\":\"[0x3]\",\"notes\":null,\"rank\":\"Botanist\",\"citations\":["
        + "{\"author\":\"Bob\",\"details\":\"Weeds\",\"fine\":400,\"fine_name\":\"Contraband\",\"fine_ref\":\"[0x9]\",\"paid\":0,\"time\":\"12:10\"}]}]}";

    [Fact]
    public void TheCitationsAreListedByNameAndASelectedOnesFinesArePaidAndPrinted()
    {
        var w = new Window { Json = Data };
        UiNode tree = w.Render();
        Assert.Equal((500.0, 500.0, "security warrant console"), (w.Context.WindowWidth, w.Context.WindowHeight, w.Context.WindowTitle));
        Assert.Equal(new[] { "Alice: 1", "Zed: 2" }, tree.Walk().Where(n => n.Kind == UiKind.Tab).Select(n => n.Text));
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Collapsible);
        ByKey(tree, "refresh").OnClick();

        ByKey(tree, "record:1").OnClick();
        tree = w.Render();
        Assert.True(ByKey(tree, "record:1").Selected);
        Assert.Equal("Zed", ByKey(tree, "record").Title);
        UiNode littering = ByKey(tree, "fine:[0x7]");
        UiNode noise = ByKey(tree, "fine:[0x8]");
        Assert.Equal(("Littering", "bad"), (littering.Title, littering.Color));
        Assert.Equal(("Noise", "grey"), (noise.Title, noise.Color));
        Assert.True(noise.Buttons.Disabled); // nothing to print for a paid fine
        Assert.DoesNotContain(noise.Walk(), n => n.Key == "pay");
        Assert.Contains("Dropped a peel", littering.AllText());

        UiNode amount = ByKey(tree, "paying:[0x7]");
        Assert.Equal((5.0, 800.0, 5.0), (amount.MinValue, amount.MaxValue, amount.Value));
        amount.OnChange(120);
        tree = w.Render();
        ByKey(tree, "pay:[0x7]").OnClick();
        tree = w.Render();
        Assert.Equal("Confirm?", ByKey(tree, "pay:[0x7]").Text);
        ByKey(tree, "pay:[0x7]").OnClick();
        ByKey(tree, "fine:[0x7]").Buttons.OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("refresh", null),
            ("pay", "{\"amount\":120,\"crew_ref\":\"[0x2]\",\"fine_ref\":\"[0x7]\"}"),
            ("print", "{\"crew_ref\":\"[0x2]\",\"fine_ref\":\"[0x7]\"}"),
        }, w.Acts);

        // a fine's colour
        Assert.Equal(new[] { "bad", "average", "", "grey" }, new[] { 701.0, 301.0, 300.0, 0.0 }.Select(WarrantConsole.FineColor));

        // the selection goes away with the record
        w.Json = "{\"records\":[]}";
        tree = w.Render();
        Assert.Equal("No citations issued.", ByKey(tree, "empty").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "record");
    }

    [Fact]
    public void TheWarrantConsoleWindowIsRegisteredUnderItsName()
    {
        Assert.Equal((TguiInterface)WarrantConsole.Render, TguiInterfaces.Find("WarrantConsole"));
    }
}
