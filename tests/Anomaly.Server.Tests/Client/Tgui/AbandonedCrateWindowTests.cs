using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps ST3: the client's AbandonedCrate window (tg's AbandonedCrate.tsx) warns how many failed attempts are left before the
/// anti-tamper bomb, lists the previous attempts with their bulls and cows under the header row, and grows 19 pixels a row; it is
/// registered under its name.
/// </summary>
public class AbandonedCrateWindowTests
{
    private static (TguiContext Context, UiNode Tree) Render(string json)
    {
        var context = new TguiContext(1, "AbandonedCrate", "w", 2, UiValue.FromJson(json), (_, _) => { }, new TguiLocalState());
        return (context, AbandonedCrate.Render(context));
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Has(UiNode tree, string key) => tree.Walk().Any(n => n.Key == key);

    [Fact]
    public void TheAbandonedCrateWindowWarnsOfTheBombAndListsTheAttempts()
    {
        Assert.NotNull(TguiInterfaces.Find("AbandonedCrate"));

        // no attempt yet: the warning, no header row
        var (context, fresh) = Render("{\"previous_attempts\":[],\"attempts_left\":10}");
        Assert.Equal((335.0, 180.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Deca-Code Lock", ByKey(fresh, "lock").Title);
        Assert.Equal("Anti-Tamper Bomb will activate after 10 failed access attempts.", ByKey(fresh, "warning").Text);
        Assert.False(Has(fresh, "header"));
        Assert.Contains("non-repeating digits", ByKey(fresh, "info").Tooltip);

        // two attempts, one left: the header row and a row each with its bulls and cows
        var (grown, tried) = Render("{\"previous_attempts\":[{\"attempt\":\"1234\",\"bulls\":1,\"cows\":2},{\"attempt\":\"5678\",\"bulls\":0,\"cows\":1}],\"attempts_left\":1}");
        Assert.Equal(180.0 + 2 * 19, grown.WindowHeight);
        Assert.Equal("Anti-Tamper Bomb will activate on next failed access attempt.", ByKey(tried, "warning").Text);
        Assert.True(Has(tried, "header"));
        Assert.Equal("Correct digits at correct positions", ByKey(tried, "bulls-info").Tooltip);
        UiNode first = ByKey(tried, "attempt:0:1234");
        Assert.Equal(new[] { "1234", "1", "2" }, first.Walk().Where(n => n.Kind == UiKind.TableCell).Select(n => n.Text).ToArray());
        UiNode second = ByKey(tried, "attempt:1:5678");
        Assert.Equal(new[] { "5678", "0", "1" }, second.Walk().Where(n => n.Kind == UiKind.TableCell).Select(n => n.Text).ToArray());
    }
}
