using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>Gaps package J5: BloodFilter.tsx, Electropack.tsx and CrewConsole.tsx read tg's ui_data the way the TSX does and send tg's acts.</summary>
public class JobGearWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts, TguiLocalState Local) Render(Func<TguiContext, UiNode> view, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        local ??= new TguiLocalState();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, view(context), acts, local);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void The_blood_filter_lists_its_whitelist_and_sends_add_and_remove()
    {
        var (context, tree, acts, _) = Render(BloodFilter.Render, "BloodFilter", "{\"whitelist\":[\"Toxin\"]}");
        Assert.Equal((500.0, 300.0), (context.WindowWidth, context.WindowHeight));
        ByKey(tree, "add-whitelist").OnClick();
        ByKey(tree, "whitelist-Toxin").OnClick();
        Assert.Equal("add", acts[0].Action);
        Assert.Equal("remove", acts[1].Action);
        Assert.Contains("Toxin", acts[1].Params);
    }

    [Fact]
    public void The_electropack_shows_its_power_frequency_and_code()
    {
        var (context, tree, acts, _) = Render(Electropack.Render, "Electropack", "{\"power\":1,\"frequency\":1449,\"code\":2,\"minFrequency\":1200,\"maxFrequency\":1600}");
        Assert.Equal((260.0, 137.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("On", ByKey(tree, "power").Text);
        Assert.Equal(144.9, ByKey(tree, "freq").Value, 3);
        Assert.Equal(2, ByKey(tree, "code").Value);
        ByKey(tree, "power").OnClick();
        ByKey(tree, "resetcode").OnClick();
        Assert.Equal(("power", (string)null), (acts[0].Action, acts[0].Params));
        Assert.Contains("code", acts[1].Params);
    }

    private const string Sensors = "{\"link_allowed\":0,\"sensors\":["
        + "{\"ref\":\"a\",\"name\":\"Zed\",\"ijob\":40,\"assignment\":\"Chief Engineer\",\"life_status\":0,\"oxydam\":0,\"toxdam\":0,\"burndam\":0,\"brutedam\":30,\"health\":70,\"area\":\"Engineering\"},"
        + "{\"ref\":\"b\",\"name\":\"Amy\",\"ijob\":998,\"life_status\":4}]}";

    [Fact]
    public void The_crew_monitor_sorts_by_job_shows_vitals_and_hides_what_the_sensor_mode_hides()
    {
        var (context, tree, _, local) = Render(CrewConsole.Render, "CrewConsole", Sensors);
        Assert.Equal((600.0, 600.0), (context.WindowWidth, context.WindowHeight));
        List<UiNode> rows = tree.Walk().Where(n => n.Key != null && n.Key.StartsWith("row:")).ToList();
        Assert.Equal(new[] { "row:a", "row:b" }, rows.Select(n => n.Key));
        Assert.Contains("Zed (Chief Engineer)", rows[0].AllText());
        Assert.Contains("30", rows[0].AllText());
        Assert.Contains("Engineering", rows[0].AllText());
        // a binary sensor: dead or alive and no position
        Assert.Contains("Dead", rows[1].AllText());
        Assert.Contains(rows[1].Walk(), n => n.Kind == UiKind.Icon && n.Icon == "skull");
        Assert.Contains(rows[1].Walk(), n => n.Kind == UiKind.Icon && n.Icon == "question");
        // the Name sort is the next option; the AI's Track column is hidden
        ByKey(tree, "sortby").OnClick();
        var (_, again, _, _) = Render(CrewConsole.Render, "CrewConsole", Sensors, local);
        Assert.Equal(new[] { "row:b", "row:a" }, again.Walk().Where(n => n.Key != null && n.Key.StartsWith("row:")).Select(n => n.Key));
        Assert.DoesNotContain(again.Walk(), n => n.Key == "track:a");
    }
}
