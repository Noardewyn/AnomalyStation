using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Package E6: the client's StationAlertConsole, NtosStationAlertConsole, AtmosAlertConsole and ApcControl windows (tg's
/// StationAlertConsole.jsx, NtosStationAlertConsole.tsx, AtmosAlertConsole.tsx and ApcControl.tsx) without the engine: tg's sizes, the
/// sorted alarm categories with their nominal lines, the clearable alerts, the APC console's log-in screen, tabs, sort, table, toggles
/// and log, each sending tg's acts; all four are registered under their interface names.
/// </summary>
public class AlertConsolesWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "console", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string StationData = "{\"cameraView\":0,\"alarms\":["
        + "{\"name\":\"Power\",\"alerts\":[{\"name\":\"Bar\",\"cameras\":null,\"sources\":null,\"ref\":null}]},"
        + "{\"name\":\"Atmosphere\",\"alerts\":[]},"
        + "{\"name\":\"Fire\",\"alerts\":[{\"name\":\"Engineering\",\"cameras\":null,\"sources\":null,\"ref\":null},{\"name\":\"Bridge\",\"cameras\":null,\"sources\":null,\"ref\":null}]}]}";

    [Fact]
    public void TheStationAlertWindowSortsTheCategoriesAndShowsTheirAlertsOrSystemsNominal()
    {
        var (context, tree, _) = Render(StationAlertConsole.Render, "StationAlertConsole", StationData);
        Assert.Equal((345.0, 587.0), (context.WindowWidth, context.WindowHeight));
        List<UiNode> sections = tree.Walk().Where(n => n.Kind == UiKind.Section).ToList();
        // tg's sortingKey: Fire, Atmosphere, Power
        Assert.Equal(new[] { "Fire Alarms", "Atmosphere Alarms", "Power Alarms" }, sections.Select(s => s.Title));
        Assert.Equal(new[] { "• Engineering", "• Bridge" }, sections[0].Walk().Where(n => n.Key == "name").Select(n => n.Text));
        Assert.All(sections[0].Walk().Where(n => n.Key == "name"), n => Assert.Equal("average", n.Color));
        UiNode nominal = Assert.Single(sections[1].Walk(), n => n.Key == "nominal");
        Assert.Equal(("• Systems nominal", "good"), (nominal.Text, nominal.Color));
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Button); // no camera buttons without the camera view

        // the AI's camera view: wider, the sources counted, the camera buttons
        var (wide, cameraTree, acts) = Render(StationAlertConsole.Render, "StationAlertConsole",
            "{\"cameraView\":1,\"alarms\":[{\"name\":\"Fire\",\"alerts\":[{\"name\":\"Bar\",\"cameras\":2,\"sources\":3,\"ref\":\"[0x1]\"},"
            + "{\"name\":\"Dorms\",\"cameras\":0,\"sources\":1,\"ref\":\"[0x2]\"}]}]}");
        Assert.Equal(390.0, wide.WindowWidth);
        Assert.Equal(new[] { "• Bar (3 sources)", "• Dorms" }, cameraTree.Walk().Where(n => n.Key == "name").Select(n => n.Text));
        List<UiNode> cameras = cameraTree.Walk().Where(n => n.Key == "camera").ToList();
        Assert.Equal(new[] { ("2 Cameras", "video", false), ("No Camera", "", true) }, cameras.Select(b => (b.Text, b.Icon, b.Disabled)));
        cameras[0].OnClick();
        Assert.Equal(("select_camera", "{\"alert\":\"[0x1]\"}"), Assert.Single(acts));
    }

    [Fact]
    public void TheNtosStationAlertWindowIsTheSameListInAnNtosWindow()
    {
        var (context, tree, _) = Render(NtosStationAlertConsole.Render, "NtosStationAlertConsole", StationData);
        Assert.Equal((335.0, 587.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Fire Alarms", "Atmosphere Alarms", "Power Alarms" }, tree.Walk().Where(n => n.Kind == UiKind.Section).Select(s => s.Title));
    }

    [Fact]
    public void TheAtmosAlertWindowListsThePriorityAndMinorAlertsEachClearedByItsButton()
    {
        var (context, tree, acts) = Render(AtmosAlertConsole.Render, "AtmosAlertConsole", "{\"priority\":[\"Bar\"],\"minor\":[\"Dorms\",\"Library\"]}");
        Assert.Equal((350.0, 300.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Alarms", ByKey(tree, "alarms").Title);
        List<UiNode> buttons = tree.Walk().Where(n => n.Kind == UiKind.Button).ToList();
        Assert.Equal(new[] { ("Bar", "bad"), ("Dorms", "average"), ("Library", "average") }, buttons.Select(b => (b.Text, b.Color)));
        Assert.All(buttons, b => Assert.Equal("times", b.Icon));
        buttons[1].OnClick();
        Assert.Equal(("clear", "{\"zone\":\"Dorms\"}"), Assert.Single(acts));

        var (_, empty, _) = Render(AtmosAlertConsole.Render, "AtmosAlertConsole", "{\"priority\":[],\"minor\":[]}");
        Assert.Equal(new[] { "• No priority alerts", "• No minor alerts" }, empty.Walk().Where(n => n.Key is "no-priority" or "no-minor").Select(n => n.Text));
    }

    private const string ApcData = "{\"auth_id\":\"Jo (Chief Engineer):\",\"authenticated\":1,\"emagged\":0,\"logging\":1,\"restoring\":0,"
        + "\"logs\":[{\"entry\":\"(12:00:01): Jo (Chief Engineer): Jo (Chief Engineer): logged in to the terminal\"},{\"entry\":\"(12:00:05): Jo (Chief Engineer): Checked Logs\"}],"
        + "\"apcs\":[{\"name\":\"Bar\",\"operating\":1,\"charge\":40,\"load\":\"2 kW\",\"charging\":1,\"chargeMode\":1,\"eqp\":3,\"lgt\":2,\"env\":0,\"responds\":0,\"ref\":\"[0xa]\"},"
        + "{\"name\":\"Atrium\",\"operating\":0,\"charge\":\"NOCELL\",\"load\":\"500 W\",\"charging\":0,\"chargeMode\":1,\"eqp\":1,\"lgt\":3,\"env\":3,\"responds\":0,\"ref\":\"[0xb]\"},"
        + "{\"name\":\"Cargo\",\"operating\":1,\"charge\":90,\"load\":\"1 MW\",\"charging\":2,\"chargeMode\":1,\"eqp\":3,\"lgt\":3,\"env\":3,\"responds\":0,\"ref\":\"[0xc]\"}]}";

    [Fact]
    public void TheApcControlWindowLogsInAndOut()
    {
        var (context, tree, acts) = Render(ApcControl.Render, "ApcControl", "{\"authenticated\":0,\"emagged\":0,\"apcs\":[],\"logs\":[]}");
        Assert.Equal((550.0, 500.0, "APC Controller"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        Assert.Equal("Zeus™ Controller Version 0.19", ByKey(tree, "version").Text);
        Assert.Contains("Authorized personnel only.", tree.AllText());
        UiNode login = ByKey(tree, "login");
        Assert.Equal(("Log In", "sign-in-alt", "good"), (login.Text, login.Icon, login.Color));
        login.OnClick();
        Assert.Equal(("log-in", (string)null), Assert.Single(acts));
        // emagged: anyone opens it
        var (_, emagged, _) = Render(ApcControl.Render, "ApcControl", "{\"authenticated\":0,\"emagged\":1,\"apcs\":[],\"logs\":[]}");
        Assert.Equal(("Open", ""), (ByKey(emagged, "login").Text, ByKey(emagged, "login").Color));
    }

    [Fact]
    public void TheApcControlWindowListsTheApcsSortedWithTheirBreakersChannelsAndChargeAndShowsTheLog()
    {
        var local = new TguiLocalState();
        var (_, tree, acts) = Render(ApcControl.Render, "ApcControl", ApcData, local);
        Assert.True(ByKey(tree, "tab:apcs").Selected);
        Assert.Null(tree.Walk().FirstOrDefault(n => n.Key == "toggle-logs")); // not emagged
        // sorted by name (the default)
        List<UiNode> rows = tree.Walk().Where(n => n.Kind == UiKind.TableRow && n.Key.StartsWith("apc:")).ToList();
        Assert.Equal(new[] { "Atrium", "Bar", "Cargo" }, rows.Select(r => ByKey(r, "access").Text));
        UiNode atrium = rows[0], bar = rows[1];
        Assert.Equal(("times", "bad"), (ByKey(atrium, "breaker").Icon, ByKey(atrium, "breaker").Color));
        Assert.Equal(("power-off", "good"), (ByKey(bar, "breaker").Icon, ByKey(bar, "breaker").Color));
        // the charge: tg's AreaCharge, NaN% for a missing cell
        Assert.Equal(("bolt", "40%"), (ByKey(bar, "icon").Icon, ByKey(bar, "percent").Text));
        Assert.Equal(("battery-quarter", "red", "NaN%"), (ByKey(atrium, "icon").Icon, ByKey(atrium, "icon").Color, ByKey(atrium, "percent").Text));
        Assert.Equal("2 kW", ByKey(bar, "load").Text);
        // the channels: 3 auto on, 2 on, 0 off; 1 auto off
        Assert.Equal(new[] { ("sync", "good"), ("power-off", "good"), ("power-off", "bad") },
            new[] { "equipment", "lighting", "environ" }.Select(k => (ByKey(bar, k).Icon, ByKey(bar, k).Color)));
        Assert.Equal(("sync", "bad"), (ByKey(atrium, "equipment").Icon, ByKey(atrium, "equipment").Color));

        ByKey(bar, "breaker").OnClick();
        ByKey(bar, "access").OnClick();
        ByKey(bar, "equipment").OnClick(); // 3 -> 0
        ByKey(bar, "lighting").OnClick(); // 2 -> 3
        ByKey(bar, "environ").OnClick(); // 0 -> 2
        Assert.Equal(new (string, string)[]
        {
            ("breaker", "{\"ref\":\"[0xa]\"}"), ("access-apc", "{\"ref\":\"[0xa]\"}"),
            ("toggle-minor", "{\"type\":\"equipment\",\"value\":0,\"ref\":\"[0xa]\"}"),
            ("toggle-minor", "{\"type\":\"lighting\",\"value\":3,\"ref\":\"[0xa]\"}"),
            ("toggle-minor", "{\"type\":\"environ\",\"value\":2,\"ref\":\"[0xa]\"}"),
        }, acts);

        // by charge and by draw (MW, kW, W); tg's sort leaves a missing cell's NaN wherever its comparisons put it, so the cell here
        string charged = ApcData.Replace("\"charge\":\"NOCELL\"", "\"charge\":10");
        ByKey(tree, "sort:charge").OnClick();
        var (_, byCharge, _) = Render(ApcControl.Render, "ApcControl", charged, local);
        Assert.Equal(new[] { "Cargo", "Bar", "Atrium" }, byCharge.Walk().Where(n => n.Key == "access").Select(n => n.Text));
        Assert.True(ByKey(byCharge, "sort:charge").Checkbox);
        ByKey(byCharge, "sort:draw").OnClick();
        var (_, byDraw, _) = Render(ApcControl.Render, "ApcControl", charged, local);
        Assert.Equal(new[] { "Cargo", "Bar", "Atrium" }, byDraw.Walk().Where(n => n.Key == "access").Select(n => n.Text));

        // the log tab: newest first, split at the last colon
        var (_, again, logActs) = Render(ApcControl.Render, "ApcControl", ApcData, local);
        ByKey(again, "tab:logs").OnClick();
        Assert.Equal(("check-logs", (string)null), Assert.Single(logActs));
        var (_, logs, _) = Render(ApcControl.Render, "ApcControl", ApcData, local);
        List<UiNode> entries = ByKey(logs, "logpanel").Children;
        Assert.Equal(new[] { "(12:00:05): Jo (Chief Engineer):", "(12:00:01): Jo (Chief Engineer): Jo (Chief Engineer):" }, entries.Select(e => ByKey(e, "timestamp").Text));
        Assert.Equal(new[] { " Checked Logs", " logged in to the terminal" }, entries.Select(e => ByKey(e, "message").Text));
        Assert.DoesNotContain(logs.Walk(), n => n.Key == "access");
    }

    [Fact]
    public void TheEmaggedApcControlWindowTogglesTheLogsResetsAndDimsWhileRestoring()
    {
        string data = ApcData.Replace("\"emagged\":0", "\"emagged\":1").Replace("\"restoring\":0", "\"restoring\":1");
        var (_, tree, acts) = Render(ApcControl.Render, "ApcControl", data);
        UiNode logging = ByKey(tree, "toggle-logs");
        Assert.Equal(("Stop Logging", "bad"), (logging.Text, logging.Color));
        logging.OnClick();
        ByKey(tree, "restore").OnClick();
        ByKey(tree, "logout").OnClick();
        Assert.Equal(new[] { "toggle-logs", "restore-console", "log-out" }, acts.Select(a => a.Action));
        Assert.Equal(" Resetting...", ByKey(tree, "resetting").Text);
        Assert.Equal(UiKind.Modal, ByKey(tree, "restoring").Kind);
    }

    [Fact]
    public void TheFourWindowsAreRegistered()
    {
        Assert.Equal((TguiInterface)StationAlertConsole.Render, TguiInterfaces.Find("StationAlertConsole"));
        Assert.Equal((TguiInterface)AtmosAlertConsole.Render, TguiInterfaces.Find("AtmosAlertConsole"));
        Assert.Equal((TguiInterface)ApcControl.Render, TguiInterfaces.Find("ApcControl"));
        Assert.Equal((TguiInterface)NtosStationAlertConsole.Render, TguiInterfaces.Find("NtosStationAlertConsole"));
    }
}
