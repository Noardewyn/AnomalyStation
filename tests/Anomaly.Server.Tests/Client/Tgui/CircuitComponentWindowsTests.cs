using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The circuit components' own windows: the access checker's CircuitAccessChecker (one or all of the accesses, the access regions as
/// the airlock electronics draw them) and the signal handler's admin CircuitSignalHandler (a signal id with its responses and
/// parameters written in the window, sent as one add_new_id). Both are registered under their names.
/// </summary>
public class CircuitComponentWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json, TguiLocalState local = null, List<(string, string)> acts = null)
    {
        acts ??= new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key)
    {
        UiNode found = tree.Walk().First(n => n.Key == key);
        while (found.Walk().Skip(1).FirstOrDefault(n => n.Key == key) is UiNode inner)
            found = inner;
        return found;
    }

    [Fact]
    public void TheAccessCheckerShowsTheRegionsAndSendsTgsActs()
    {
        Assert.NotNull(TguiInterfaces.Find("CircuitAccessChecker"));
        var (context, tree, acts) = Render("CircuitAccessChecker", CircuitAccessChecker.Render,
            "{\"accesses\":[\"12\"],\"oneAccess\":1,"
            + "\"regions\":[{\"name\":\"General\",\"accesses\":[{\"desc\":\"Maintenance\",\"ref\":\"12\"},{\"desc\":\"Bar\",\"ref\":\"25\"}]},"
            + "{\"name\":\"Security\",\"accesses\":[{\"desc\":\"Brig\",\"ref\":\"2\"}]}]}");
        Assert.Equal((420.0, 360.0), (context.WindowWidth, context.WindowHeight));
        UiNode oneAccess = ByKey(tree, "one_access");
        Assert.Equal(("unlock", "One"), (oneAccess.Icon, oneAccess.Text));
        Assert.Equal(new[] { ("Bar", false), ("Maintenance", true) }, tree.Walk().Where(n => n.Checkbox != null).Select(n => (n.Text, n.Checkbox.Value)));
        oneAccess.OnClick();
        ByKey(tree, "Bar").OnClick();
        ByKey(tree, "grant_region").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("one_access", null), ("set", "{\"access\":\"25\"}"), ("grant_region", "{\"region\":\"General\"}"),
        }, acts);

        (_, tree, _) = Render("CircuitAccessChecker", CircuitAccessChecker.Render, "{\"accesses\":[],\"oneAccess\":0,\"regions\":[]}");
        Assert.Equal(("lock", "All"), (ByKey(tree, "one_access").Icon, ByKey(tree, "one_access").Text));
    }

    [Fact]
    public void TheSignalHandlerWindowWritesResponsesAndParametersAndSubmitsThem()
    {
        Assert.NotNull(TguiInterfaces.Find("CircuitSignalHandler"));
        const string data = "{\"global_port_types\":[\"any\",\"number\",\"string\"]}";
        var local = new TguiLocalState();
        var acts = new List<(string, string)>();
        var (context, tree, _) = Render("CircuitSignalHandler", CircuitSignalHandler.Render, data, local, acts);
        Assert.Equal((600.0, 300.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("signal_id", ByKey(tree, "signal_id").TextValue);

        ByKey(tree, "signal_id").OnInput("COMSIG_TEST");
        ByKey(tree, "global").OnClick();
        ByKey(tree, "add_response").OnClick();
        (_, tree, _) = Render("CircuitSignalHandler", CircuitSignalHandler.Render, data, local, acts);
        ByKey(tree, "add_response").OnClick();
        ByKey(tree, "add_parameter").OnClick();
        (_, tree, _) = Render("CircuitSignalHandler", CircuitSignalHandler.Render, data, local, acts);

        // the responses take the flags in order and show their names; a parameter starts as the first port type
        UiNode second = ByKey(tree, "response1");
        Assert.Equal("Flag 2", ByKey(second, "option").Text);
        ByKey(ByKey(tree, "response0"), "name").OnInput("Cancel");
        ByKey(second, "remove").OnClick();
        UiNode parameter = ByKey(tree, "parameter0");
        Assert.Equal("any", ByKey(parameter, "option").TextValue);
        ByKey(parameter, "option").OnSelected("number");
        ByKey(parameter, "name").OnInput("Amount");
        (_, tree, _) = Render("CircuitSignalHandler", CircuitSignalHandler.Render, data, local, acts);
        Assert.Equal(new[] { "response0", "parameter0" }, tree.Walk().Select(n => n.Key).Where(k => k is "response0" or "response1" or "parameter0").Distinct());
        Assert.True(ByKey(tree, "global").Checkbox);

        ByKey(tree, "submit").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("add_new_id", "{\"signal_id\":\"COMSIG_TEST\",\"responses\":[{\"name\":\"Cancel\",\"bitflag\":1}],"
                + "\"parameters\":[{\"name\":\"Amount\",\"datatype\":\"number\"}],\"global\":true}"),
        }, acts);
    }
}
