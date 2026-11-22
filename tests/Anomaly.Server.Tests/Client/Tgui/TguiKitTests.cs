using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// M5 slice 3 (D148): the client's tgui kit without the engine. The translated interfaces read tg's data the way the TSX does and their
/// buttons send tg's acts (Canister, NumberInputModal, AlertModal, StackCrafting, AirlockElectronics); the window manager places
/// windows in the middle (cascaded) or where the player last dragged that kind, keeps them on the screen, scales them with the HUD,
/// remembers dragged positions in the options file, and turns Esc, Enter and the close button into tg's messages; tgui-core's
/// formatting helpers and units.
/// </summary>
public class TguiKitTests
{
    [Fact]
    public void WireControlsSendTheColorAndDisablePulsingACutWire()
    {
        var (_, tree, acts) = Render(Wires.Render, Json("{\"wires\":[{\"color\":\"red\",\"cut\":false},{\"color\":\"blue\",\"cut\":true}],\"status\":[\"The test light is on.\"]}"));
        ByKey(tree, "red-pulse").OnClick();
        ByKey(tree, "blue-cut").OnClick();
        Assert.Equal(new[] { ("pulse", "{\"wire\":\"red\"}"), ("cut", "{\"wire\":\"blue\"}") }, acts);
        Assert.True(ByKey(tree, "blue-pulse").Disabled);
        Assert.Equal("Mend", ByKey(tree, "blue-cut").Text);
    }

    private sealed class Sent
    {
        public readonly List<(int Window, string Action, string Params)> Acts = new();
        public readonly List<int> Closes = new();
    }

    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface view, UiValue data, TguiLocalState local = null, int status = 2)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "X", "Title", status, data, (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        UiNode tree = view(context);
        return (context, tree, acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private static UiValue Json(string json) => UiValue.FromJson(json);

    private const string CanisterData = "{\"defaultReleasePressure\":101,\"minReleasePressure\":10,\"maxReleasePressure\":2533,\"pressureLimit\":531484,"
        + "\"holdingTankLeakPressure\":3039,\"holdingTankFragPressure\":4053,\"portConnected\":0,\"tankPressure\":4559,\"releasePressure\":101,"
        + "\"valveOpen\":0,\"hasHoldingTank\":0,\"hasHypernobCrystal\":0,\"reactionSuppressionEnabled\":0,\"holdingTank\":null,\"shielding\":0,\"cellCharge\":0}";

    [Fact]
    public void TheCanisterWindowShowsTgsDataAndSendsItsActs()
    {
        var (context, tree, acts) = Render(Canister.Render, Json(CanisterData));
        Assert.Equal((350.0, 335.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Title", context.WindowTitle);
        UiNode valve = ByKey(tree, "valve");
        Assert.Equal("Closed", valve.Text);
        Assert.Null(valve.Color);
        Assert.Contains("No Holding Tank", tree.AllText());
        Assert.Contains("Missing Cell", tree.AllText());
        Assert.Contains("Shielding-OFF", tree.AllText());
        Assert.DoesNotContain("Reaction Suppression", tree.AllText());
        UiNode gauge = ByKey(tree, "tankPressure");
        Assert.Equal(4559, gauge.Value);
        Assert.Equal(531484 * 0.7, gauge.AlertAfter.Value, 3);
        Assert.Equal("4559 kPa", gauge.Format(4559));
        Assert.Equal("12.35 MPa", gauge.Format(12345));
        Assert.Equal("1", ByKey(tree, "releasePressure").Step == 5 ? "1" : "0");

        valve.OnClick();
        ByKey(tree, "releasePressure").OnChange(1000);
        ByKey(tree, "max").OnClick();
        ByKey(tree, "default").OnClick();
        Assert.Equal(new[] { ("valve", (string)null), ("pressure", "{\"pressure\":1000}"), ("pressure", "{\"pressure\":2533}"), ("pressure", "{\"pressure\":101}") }, acts);

        // An open valve with a holding tank: "Open" in caution, the tank's gauge and an Eject button in danger.
        var open = Render(Canister.Render, Json(CanisterData).MergedWith(Json("{\"valveOpen\":1,\"hasHoldingTank\":1,\"holdingTank\":{\"name\":\"oxygen tank\",\"tankPressure\":2000},\"shielding\":1,\"cellCharge\":75}")));
        Assert.Equal(("Open", "caution"), (ByKey(open.Tree, "valve").Text, ByKey(open.Tree, "valve").Color));
        Assert.Equal(2000, ByKey(open.Tree, "holdingTankPressure").Value);
        Assert.Contains("oxygen tank", open.Tree.AllText());
        Assert.Contains("75%", open.Tree.AllText());
        Assert.Contains("Shielding-ON", open.Tree.AllText());
        UiNode eject = open.Tree.Walk().Single(n => n.Text == "Eject");
        Assert.Equal("danger", eject.Color);
        eject.OnClick();
        Assert.Equal(("eject", null), open.Acts.Single());
        Assert.Equal("danger", ByKey(Render(Canister.Render, Json(CanisterData).MergedWith(Json("{\"valveOpen\":1}"))).Tree, "valve").Color);
    }

    [Fact]
    public void TheNumberInputKeepsItsValueLocallyAndSubmitsIt()
    {
        var local = new TguiLocalState();
        UiValue data = Json("{\"init_value\":5,\"large_buttons\":0,\"max_value\":50,\"message\":\"How many sheets do you wish to take out of this stack?\",\"min_value\":1,\"swapped_buttons\":1,\"title\":\"Amount\",\"round_value\":1}");
        var (context, tree, acts) = Render(NumberInputModal.Render, data, local);
        Assert.Equal(270, context.WindowWidth);
        Assert.Equal(140 + Math.Ceiling(53 / 3.0), context.WindowHeight);
        Assert.Equal("Amount", context.WindowTitle);
        Assert.False(ByKey(tree, "entry").AllowFloats);
        Assert.True(ByKey(tree, "reset").Disabled); // the value is the initial one
        ByKey(tree, "max").OnClick();
        Assert.Equal(50.0, local.Get("value", 0.0));
        (context, tree, acts) = Render(NumberInputModal.Render, data, local);
        Assert.True(ByKey(tree, "max").Disabled);
        Assert.True(ByKey(tree, "up").Disabled);
        ByKey(tree, "down").OnClick();
        ByKey(tree, "entry").OnChange(12);
        ByKey(tree, "submit").OnClick();
        Assert.Equal(("submit", "{\"entry\":12}"), acts.Last());
        ByKey(tree, "entry").OnValidation(false);
        (context, tree, acts) = Render(NumberInputModal.Render, data, local);
        Assert.True(ByKey(tree, "submit").Disabled);
        context.OnEnter();
        Assert.Empty(acts); // Enter does nothing while the entry is invalid
    }

    [Fact]
    public void ATypedNumberIsTheValueTheButtonsAndEnterUse()
    {
        // The owner's report (2026-10-18, C-4's timer): a typed time could not be submitted, and the down button stayed off after one was typed.
        // tg's onChange={setValue} is a state change: the window is drawn again for the typed value.
        var local = new TguiLocalState();
        UiValue data = Json("{\"init_value\":10,\"large_buttons\":0,\"max_value\":60000,\"message\":\"Please set the timer\",\"min_value\":10,\"title\":\"C4 Timer\",\"round_value\":1}");
        var (context, tree, acts) = Render(NumberInputModal.Render, data, local);
        Assert.True(ByKey(tree, "entry").AutoFocus); // tg's autoFocus and autoSelect
        Assert.True(ByKey(tree, "down").Disabled); // at the minimum
        int serial = local.Serial;
        ByKey(tree, "entry").OnChange(120);
        Assert.NotEqual(serial, local.Serial); // a redraw
        (context, tree, acts) = Render(NumberInputModal.Render, data, local);
        Assert.False(ByKey(tree, "down").Disabled);
        Assert.False(ByKey(tree, "reset").Disabled);
        ByKey(tree, "down").OnClick();
        Assert.Equal(119.0, local.Get("value", 0.0));
        (context, tree, acts) = Render(NumberInputModal.Render, data, local);
        ByKey(tree, "entry").OnEnter(); // Enter in the field
        Assert.Equal(("submit", "{\"entry\":119}"), acts.Last());
    }

    [Fact]
    public void TheAlertShowsTgsButtonsAndChoosesOne()
    {
        UiValue data = Json("{\"autofocus\":1,\"buttons\":[\"Yes\",\"No\"],\"message\":\"Respawn?\",\"large_buttons\":0,\"swapped_buttons\":1,\"title\":\"Respawn\"}");
        var (context, tree, acts) = Render(AlertModal.Render, data);
        Assert.Equal((345.0, 120.0), (context.WindowWidth, context.WindowHeight));
        UiNode[] buttons = tree.Walk().Where(n => n.Kind == UiKind.Button).ToArray();
        Assert.Equal(new[] { "Yes", "No" }, buttons.Select(b => b.Text));
        Assert.True(buttons[0].Selected);
        buttons[1].OnClick();
        context.OnEnter();
        Assert.Equal(new[] { ("choose", "{\"choice\":\"No\"}"), ("choose", "{\"choice\":\"Yes\"}") }, acts);
        Assert.True(tree.Walk().Single(n => n.Kind == UiKind.Stack && n.Justify == "space-around").Reverse == false); // swapped: in order
    }

    private const string Recipes = "{\"amount\":12,\"recipes\":{"
        + "\"stool\":{\"ref\":\"r1\",\"req_amount\":1,\"res_amount\":1,\"max_res_amount\":1,\"icon\":\"icons/obj/chairs.dmi\",\"icon_state\":\"stool\"},"
        + "\"floor tile\":{\"ref\":\"r2\",\"req_amount\":1,\"res_amount\":4,\"max_res_amount\":20,\"icon\":\"icons/obj/tiles.dmi\",\"icon_state\":\"tile\"},"
        + "\"airlock assembly\":{\"ref\":\"r3\",\"req_amount\":4,\"res_amount\":1,\"max_res_amount\":1,\"icon\":\"icons/obj/doors/airlocks/station/public.dmi\",\"icon_state\":\"construction\"},"
        + "\"office chairs\":{\"dark office chair\":{\"ref\":\"r4\",\"req_amount\":5,\"res_amount\":1,\"max_res_amount\":1,\"icon\":\"i\",\"icon_state\":\"s\"}},"
        + "\"computer frame\":{\"ref\":\"r5\",\"req_amount\":25,\"res_amount\":1,\"max_res_amount\":1,\"icon\":\"i\",\"icon_state\":\"s\"}}}";

    [Fact]
    public void TheStackWindowListsTgsRecipesWithCategoriesFirstAndTheMultipliers()
    {
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(StackCrafting.Render, Json(Recipes), local);
        Assert.Equal(400, context.WindowWidth);
        Assert.Equal(TguiFormat.Clamp(96 + 5 * 37, 250, 500), context.WindowHeight);
        Assert.Equal("Amount: 12", tree.Walk().First(n => n.Kind == UiKind.Section).Title);
        // Categories first, then the recipes in alphabetical order.
        var top = tree.Walk().First(n => n.Kind == UiKind.Section).Children.Single().Children;
        Assert.Equal(new[] { "Office Chairs", "airlock assembly", "computer frame", "floor tile", "stool" },
            top.Select(n => n.Kind == UiKind.Collapsible ? n.Title : n.Key));
        UiNode tile = ByKey(tree, "floor tile");
        Assert.Contains("4x Floor Tile", tile.AllText());
        Assert.Contains("1 sheet", tile.AllText());
        Assert.Equal(("icons/obj/tiles.dmi", "tile"), (tile.DmIcon, tile.DmIconState));
        // 12 sheets: max multiplier 12, recipes max 20/4 = 5: the x5 button (20 tiles), and the max (5x) is in the list already.
        Assert.Equal(new[] { "20x" }, tile.Buttons.Children.Select(b => b.Text));
        Assert.True(ByKey(tree, "computer frame").Disabled); // 25 sheets
        Assert.Null(ByKey(tree, "stool").Buttons);
        tile.OnClick();
        tile.Buttons.Children[0].OnClick();
        Assert.Equal(new[] { ("make", "{\"ref\":\"r2\",\"multiplier\":1}"), ("make", "{\"ref\":\"r2\",\"multiplier\":5}") }, acts);
        Assert.Contains("4 sheets", ByKey(tree, "airlock assembly").AllText());

        // The search bar: a category's name keeps it whole, a recipe's name keeps it alone, nothing found shows tg's notice.
        ByKey(tree, "search").OnInput("chair");
        (_, tree, _) = Render(StackCrafting.Render, Json(Recipes), local);
        Assert.Equal(new[] { "Office Chairs" }, tree.Walk().Where(n => n.Kind == UiKind.Collapsible).Select(n => n.Title));
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "stool");
        local.Set("searchText", "zzz");
        (_, tree, _) = Render(StackCrafting.Render, Json(Recipes), local);
        Assert.Contains("No recipes found.", tree.AllText());
    }

    [Fact]
    public void TheAirlockElectronicsWindowShowsTheRegionsAndSendsTgsActs()
    {
        var local = new TguiLocalState();
        UiValue data = Json("{\"accesses\":[\"12\"],\"oneAccess\":0,\"passedName\":\"Airlock\",\"passedCycleId\":\"\",\"shell\":0,\"unres_direction\":5,"
            + "\"regions\":[{\"name\":\"General\",\"accesses\":[{\"desc\":\"Maintenance\",\"ref\":\"12\"},{\"desc\":\"Bar\",\"ref\":\"25\"}]},"
            + "{\"name\":\"Security\",\"accesses\":[{\"desc\":\"Brig\",\"ref\":\"2\"}]}]}");
        var (context, tree, acts) = Render(AirlockElectronics.Render, data, local);
        Assert.Equal((420.0, 485.0), (context.WindowWidth, context.WindowHeight));
        UiNode[] tabs = tree.Walk().Where(n => n.Kind == UiKind.Tab).ToArray();
        Assert.Equal(new[] { ("General", "stop-circle", (string)null, true), ("Security", "times-circle", "bad", false) },
            tabs.Select(t => (t.Text, t.Icon, t.Color, t.Selected)));
        // The selected region's accesses, sorted, as checkboxes.
        Assert.Equal(new[] { ("Bar", false), ("Maintenance", true) }, tree.Walk().Where(n => n.Checkbox != null && n.Key != "shell").Select(n => (n.Text, n.Checkbox.Value)));
        Assert.True(ByKey(tree, "North").Selected);
        Assert.False(ByKey(tree, "South").Selected);
        Assert.True(ByKey(tree, "East").Selected);
        ByKey(tree, "Bar").OnClick();
        ByKey(tree, "West").OnClick();
        ByKey(tree, "one_access").OnClick();
        ByKey(tree, "shell").OnClick();
        ByKey(tree, "passedName").OnCommit("Bridge");
        ByKey(tree, "grant_region").OnClick();
        tabs[1].OnClick();
        Assert.Equal(new[]
        {
            ("set", "{\"access\":\"25\"}"), ("direc_set", "{\"unres_direction\":\"8\"}"), ("one_access", (string)null), ("set_shell", "{\"on\":true}"),
            ("passedName", "{\"passedName\":\"Bridge\"}"), ("grant_region", "{\"region\":\"General\"}"),
        }, acts);
        (_, tree, _) = Render(AirlockElectronics.Render, data, local);
        Assert.Equal(new[] { "Brig" }, tree.Walk().Where(n => n.Checkbox != null && n.Key != "shell").Select(n => n.Text));
    }

    private static UiWindowState State(int window, string @interface, string data, int status = 2)
    {
        var replica = new ReplicaWorld();
        var grid = new MapGrid { Tick = 5, Width = 4, Height = 4, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor");
        grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        replica.Apply(grid);
        replica.Apply(new Snapshot { Tick = 6 });
        replica.Apply(new UiWindowMessage { Kind = UiWindowKind.Update, Window = window, Interface = @interface, Title = "T", Status = status, Data = UiValue.FromJson(data) });
        return replica.UiWindows[window];
    }

    [Fact]
    public void TheEscapeMenusWindowIsNoTguiWindow()
    {
        // G4 (protocol 52): tg's escape menu window is the Esc menu's data; it never opens as a window nor frees the pointer.
        var windows = new TguiWindows();
        var states = new Dictionary<int, UiWindowState> { [Limits.EscapeMenuUiWindow] = State(Limits.EscapeMenuUiWindow, Limits.EscapeMenuUiInterface, "{\"stationName\":\"S\"}") };
        windows.Sync(states, 1280, 720, 2);
        Assert.False(windows.AnyOpen);
        Assert.Null(windows.Get(Limits.EscapeMenuUiWindow));
    }

    [Fact]
    public void AWindowIsResizedByItsCornerAndItsKindOpensAtThatSizeNextTime()
    {
        // The owner (2026-10-20): the game's windows can be resized; the size is remembered per interface (window_size.NAME=w,h).
        var windows = new TguiWindows();
        var states = new Dictionary<int, UiWindowState> { [1] = State(1, "Canister", CanisterData) };
        windows.Sync(states, 1280, 720, 2);
        TguiWindow canister = windows.Get(1);
        int saved = 0;
        windows.PositionsChanged += () => saved++;
        windows.Resize(canister, 100, 40, 1280, 720, 2); // screen pixels at HUD scale 2: 50 x 20 at 100 %
        Assert.Equal((400.0, 355.0), canister.Size(1));
        windows.Resize(canister, -5000, -5000, 1280, 720, 2);
        Assert.Equal((TguiWindows.MinWidth, TguiWindows.MinHeight), canister.Size(1));
        windows.Resize(canister, 10000, 10000, 1280, 720, 2);
        Assert.Equal((640.0, 360.0), canister.Size(1)); // no larger than the screen
        windows.Resize(canister, -200, -100, 1280, 720, 2);
        windows.ResizeEnded(canister);
        Assert.Equal(1, saved);
        Assert.Equal((540, 310), windows.Sizes["Canister"]);

        states.Remove(1);
        windows.Sync(states, 1280, 720, 2);
        states[2] = State(2, "Canister", CanisterData);
        windows.Sync(states, 1280, 720, 2);
        Assert.Equal((540.0, 310.0), windows.Get(2).Size(1));

        var options = new PlayOptions();
        Assert.True(options.TrySet("window_size.Canister", "540,310", out _));
        Assert.Equal((540, 310), options.WindowSizes["Canister"]);
        Assert.False(options.TrySet("window_size.Canister", "50,310", out _));
        Assert.Contains("window_size.Canister=540,310", options.ToText());

        // D282: a window drawn as cells (the storage, the inventory) keeps a scale instead
        Assert.True(options.TrySet("window_scale.storage", "140", out _));
        Assert.False(options.TrySet("window_scale.storage", "400", out _));
        Assert.Contains("window_scale.storage=140", options.ToText());
    }

    [Fact]
    public void AResizedGameWindowMovesTheWindowsByTheirShareOfTheScreen()
    {
        // The owner (2026-10-26): after the game's window shrank, a window near its right edge stayed out of the screen.
        var windows = new TguiWindows();
        var states = new Dictionary<int, UiWindowState> { [1] = State(1, "Canister", CanisterData) };
        windows.Sync(states, 1920, 1080, 1);
        TguiWindow canister = windows.Get(1);
        (double w, double h) = canister.Size(1);
        windows.Drag(canister, 1920 - w - canister.X, -canister.Y, 1920, 1080, 1);
        windows.Rescreen(1920, 1080, 960, 540, 1);
        Assert.Equal((1920 - w) / 2, canister.X, 3); // the top left keeps its share of the screen
        Assert.InRange(canister.Y, 0, 540);
        // The owner (2026-10-28): a screen too small for the place clamps the window, and going back to the size brings it back.
        windows.Rescreen(960, 540, 200, 300, 1);
        Assert.Equal(200 - 48.0, canister.X, 3);
        windows.Rescreen(200, 300, 1920, 1080, 1);
        Assert.Equal(1920 - w, canister.X, 3);
        Assert.Equal(0, canister.Y, 3);
    }

    [Fact]
    public void WindowsAlwaysOpenCentredAndStayOnTheScreen()
    {
        var sent = new Sent();
        var windows = new TguiWindows { SendAct = (w, a, p) => sent.Acts.Add((w, a, p?.ToJson())), SendClose = w => sent.Closes.Add(w) };
        var states = new Dictionary<int, UiWindowState> { [1] = State(1, "Canister", CanisterData) };
        Assert.True(windows.Sync(states, 1280, 720, 1));
        TguiWindow canister = windows.Get(1);
        Assert.Equal((Math.Round((1280 - 350) / 2.0), Math.Round((720 - 335) / 2.0)), (canister.X, canister.Y)); // centred
        Assert.Equal((700.0, 670.0), canister.Size(2)); // the HUD scale doubles it

        // A second window cascades; the top is the newest.
        states[2] = State(2, "AlertModal", "{\"buttons\":[\"Ok\"],\"message\":\"Hi\"}");
        windows.Sync(states, 1280, 720, 1);
        TguiWindow alert = windows.Get(2);
        Assert.Equal(Math.Round((1280 - 345) / 2.0) + TguiWindows.Cascade, alert.X);
        Assert.Same(alert, windows.Top);

        // Dragged: kept on the screen (the title bar stays reachable).
        windows.Drag(canister, -5000, -5000, 1280, 720, 1);
        Assert.Equal((48 - 350.0, 0.0), (canister.X, canister.Y));
        windows.Drag(canister, 5000 + 300, 5000, 1280, 720, 1);
        Assert.Equal((1280 - 48.0, 720 - TguiWindows.TitleBarHeight), (canister.X, canister.Y));
        windows.Drag(canister, -900, -300, 1280, 720, 1);
        Assert.Same(canister, windows.Top); // a drag raises it

        // Closed by the server, then a new Canister window opens in the middle again (the owner, 2026-10-29), cascaded past the alert.
        states.Remove(1);
        windows.Sync(states, 1280, 720, 1);
        Assert.Null(windows.Get(1));
        states[3] = State(3, "Canister", CanisterData);
        windows.Sync(states, 1280, 720, 1);
        Assert.Equal(Math.Round((1280 - 350) / 2.0) + TguiWindows.Cascade, windows.Get(3).X);

        // A window as tall as the screen or taller opens at its top (its bar reachable).
        states[5] = State(5, "Canister", CanisterData);
        windows.Sync(states, 1280, 300, 1);
        Assert.Equal(0, windows.Get(5).Y);

        // The close button sends tg's close.
        windows.Close(windows.Get(3));
        Assert.Equal(3, sent.Closes.Last());
        windows.Close(windows.Get(2));
        Assert.Equal(2, sent.Closes.Last());
        Assert.Same(windows.Get(3), windows.At(windows.Get(3).X + 10, windows.Get(3).Y + 10, 1));
        Assert.Null(windows.At(-1000, -1000, 1));

        // D292: Esc closes the top window, the close-windows key every one (they go when the server says so).
        sent.Closes.Clear();
        Assert.True(windows.CloseTop());
        Assert.Equal(new[] { 5 }, sent.Closes);
        sent.Closes.Clear();
        Assert.True(windows.CloseAll());
        Assert.Equal(new[] { 2, 3, 5 }, sent.Closes);
        Assert.False(new TguiWindows().CloseTop());
    }

    [Fact]
    public void AWindowThatOnlyUpdatesOrIsDisabledSendsNoActsAndAnUnknownInterfaceSaysSo()
    {
        var sent = new Sent();
        var windows = new TguiWindows { SendAct = (w, a, p) => sent.Acts.Add((w, a, p?.ToJson())) };
        windows.Sync(new Dictionary<int, UiWindowState> { [1] = State(1, "Canister", CanisterData, status: 1) }, 800, 600, 1);
        ByKey(windows.Get(1).Content, "valve").OnClick();
        Assert.Empty(sent.Acts);
        windows.Sync(new Dictionary<int, UiWindowState> { [4] = State(4, "SomethingNew", "{}") }, 800, 600, 1);
        Assert.Contains("SomethingNew", windows.Get(4).Content.AllText());
        Assert.Contains("not ported yet", windows.Get(4).Content.AllText());
    }

    [Fact]
    public void WindowPositionsAreKeptInTheOptionsFile()
    {
        var options = new PlayOptions();
        options.WindowPositions["Canister"] = (412, 180);
        options.WindowPositions["AlertModal"] = (-20, 5);
        string text = options.ToText();
        Assert.Contains("window.AlertModal=-20,5\n", text);
        Assert.Contains("window.Canister=412,180\n", text);
        var errors = new List<string>();
        PlayOptions back = PlayOptions.Parse(text + "window.Bad Name=1,2\nwindow.Canister2=1\nwindow.Far=20000,1\n", errors);
        Assert.Equal((412, 180), back.WindowPositions["Canister"]);
        Assert.Equal((-20, 5), back.WindowPositions["AlertModal"]);
        Assert.Equal(3, errors.Count);
        Assert.Equal(2, back.WindowPositions.Count);
    }

    [Fact]
    public void TguiCoresHelpersAndUnitsBehaveAsTheirs()
    {
        Assert.Equal("4559", TguiFormat.ToFixed(4558.6));
        Assert.Equal("1.50", TguiFormat.ToFixed(1.495, 2));
        Assert.Equal("4.56 MPa", TguiFormat.FormatSiUnit(4559000, 1, "Pa"));
        Assert.Equal("12.00 kPa", TguiFormat.FormatSiUnit(12000, 1, "Pa"));
        Assert.Equal("Floor Tile", TguiFormat.ToTitleCase("floor tile"));
        Assert.Equal("Box of Donuts", TguiFormat.ToTitleCase("box of donuts"));
        Assert.Equal("ID Card", TguiFormat.ToTitleCase("id card"));
        Func<string, bool> search = TguiFormat.CreateSearch<string>(" Chair", s => s);
        Assert.True(search("Office CHAIR"));
        Assert.False(search("stool"));
        Assert.True(TguiFormat.CreateSearch<string>("", s => s)("anything"));
        Assert.Equal("1.5", TguiFormat.JsNumber(1.5));
        Assert.Equal("20", TguiFormat.JsNumber(20));
        Assert.Equal(new UiSize(24, 0), UiSize.Of(2));
        Assert.Equal(new UiSize(50, 0), UiSize.Of("50px"));
        Assert.Equal(new UiSize(180, 0), UiSize.Of("15em"));
        Assert.Equal(new UiSize(0, 100), UiSize.Of("100%"));
        Assert.Null(UiSize.Of(false));
        Assert.Null(UiSize.Of("wide"));
    }

    // the air alarm window (2026-10-16): tg's AirAlarm interface with a silicon's lock row, a sensor and an editor modal
    private const string AirAlarmData = "{\"locked\":0,\"siliconUser\":1,\"emagged\":1,\"dangerLevel\":0,\"atmosAlarm\":0,\"fireAlarm\":0,"
        + "\"faultStatus\":1,\"faultLocation\":\"Bridge\",\"sensor\":1,\"allowLinkChange\":1,\"envData\":[],"
        + "\"tlvSettings\":[{\"name\":\"Pressure\",\"unit\":\"kPa\",\"id\":\"pressure\",\"warning_min\":80,\"hazard_min\":-1,\"warning_max\":120,\"hazard_max\":140}],"
        + "\"thresholdTypeMap\":{\"warning_min\":1,\"hazard_min\":2,\"warning_max\":4,\"hazard_max\":8,\"all\":15},"
        + "\"vents\":[],\"scrubbers\":[{\"refID\":\"[0x2]\",\"long_name\":\"Bridge &#39;air&#39; scrubber\",\"power\":1,\"scrubbing\":0,\"widenet\":0,"
        + "\"filter_types\":[{\"gas_id\":\"co2\",\"gas_name\":\"Carbon Dioxide\",\"enabled\":1}]}],"
        + "\"selectedModePath\":\"/datum/air_alarm_mode/filtering\",\"modes\":[],\"panicSiphonPath\":\"/datum/air_alarm_mode/panic_siphon\",\"filteringPath\":\"/datum/air_alarm_mode/filtering\"}";

    [Fact]
    public void TheAirAlarmWindowShowsTgsStatusAndItsLockSensorAndThresholdEditor()
    {
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(AirAlarm.Render, Json(AirAlarmData), local);
        Assert.Equal((475.0, 650.0), (context.WindowWidth, context.WindowHeight));
        // a silicon's lock row, the empty sample's warning, the emag warning, the fault
        Assert.Equal("Unlocked", ByKey(tree, "lock").Text);
        Assert.Contains("Cannot obtain air sample for analysis.", tree.AllText());
        Assert.Contains("Safety measures offline.", tree.AllText());
        Assert.DoesNotContain("Local Status", tree.AllText());
        ByKey(tree, "lock").OnClick();
        Assert.Equal(("lock", (string)null), acts[^1]);

        // Disconnect Sensor is a Button.Confirm: the first click asks, the second sends tg's act
        UiNode disconnect = ByKey(tree, "disconnect_sensor");
        Assert.Equal("Disconnect Sensor", disconnect.Text);
        disconnect.OnClick();
        Assert.Single(acts); // the lock only
        tree = AirAlarm.Render(context);
        Assert.Equal(("Confirm?", "bad"), (ByKey(tree, "disconnect_sensor").Text, ByKey(tree, "disconnect_sensor").Color));
        ByKey(tree, "disconnect_sensor").OnClick();
        Assert.Equal(("disconnect_sensor", (string)null), acts[^1]);

        // a disabled threshold's editor offers Enable (tg's value 0), an enabled one a number field and Disable (-1)
        local.Set("screen", "thresholds");
        tree = AirAlarm.Render(context);
        Assert.Equal("Disabled", ByKey(ByKey(tree, "tlv:Pressure"), "hazard_min").Children.Single().Text);
        Assert.Equal("140 kPa", ByKey(ByKey(tree, "tlv:Pressure"), "hazard_max").Children.Single().Text);
        ByKey(ByKey(tree, "tlv:Pressure"), "hazard_min").Children.Single().OnClick();
        tree = AirAlarm.Render(context);
        Assert.Contains("Editing the minimum hazard value for pressure...", tree.AllText());
        ByKey(tree, "enable").OnClick();
        Assert.Equal(("set_threshold", "{\"threshold\":\"pressure\",\"threshold_type\":2,\"value\":0}"), acts[^1]);
        ByKey(tree, "close").OnClick();
        tree = AirAlarm.Render(context);
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Modal);
        ByKey(ByKey(tree, "reset"), "reset").OnClick();
        Assert.Equal(("reset_threshold", "{\"threshold\":\"pressure\",\"threshold_type\":15}"), acts[^1]);

        // a siphoning scrubber shows no filters (N/A); its name's entities are decoded; the modes list is empty
        local.Set("screen", "scrubbers");
        tree = AirAlarm.Render(context);
        UiNode scrubber = ByKey(tree, "[0x2]");
        Assert.Equal("Bridge 'air' scrubber", scrubber.Title);
        Assert.Contains("N/A", scrubber.AllText());
        Assert.DoesNotContain(scrubber.Walk(), n => n.Key == "co2");
        ByKey(scrubber, "scrubbing").OnClick();
        Assert.Equal(("scrubbing", "{\"ref\":\"[0x2]\",\"val\":1}"), acts[^1]);
        local.Set("screen", "modes");
        Assert.Contains("Nothing to show", AirAlarm.Render(context).AllText());
        Assert.Equal("CO₂", TguiFormat.GetGasLabel("CO2", "Carbon Dioxide"));
        Assert.Equal("Carbon Dioxide", TguiFormat.GetGasLabel("unknown", "Carbon Dioxide"));
        Assert.Equal("a & b\nc", TguiFormat.DecodeHtmlEntities("a &amp; <b>b</b><br>c"));
    }

    [Fact]
    public void TheSmesWindowShowsTheChargeAndSendsTgsInputAndOutputActs()
    {
        const string data = "{\"capacity\":50000000,\"inputLevelMax\":200000,\"outputLevelMax\":200000,\"charge\":25000000,\"inputAttempt\":1,\"inputting\":0,"
            + "\"inputLevel\":50000,\"inputAvailable\":0,\"outputAttempt\":1,\"outputting\":1,\"outputLevel\":90000,\"outputUsed\":90000}";
        var (context, tree, acts) = Render(Smes.Render, Json(data));
        Assert.Equal((340.0, 350.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("Stored Energy", tree.AllText());
        Assert.Contains("Not Charging", tree.AllText());
        Assert.Contains("Sending", tree.AllText());
        // the bar: round(100 * 0.5, 0.1) / 100 in tgui-core's rounding, green from half
        UiNode bar = tree.Walk().Single(n => n.Kind == UiKind.ProgressBar);
        Assert.InRange(bar.Value, 0.5, 0.51);
        Assert.Equal("good", bar.Ranges.First(r => bar.Value >= r.From && bar.Value < r.To).Color);
        Assert.Equal("bad", ByKey(tree, "chargeMode").Color); // not fully charged, not charging
        Assert.Equal("good", ByKey(tree, "outputMode").Color);
        Assert.Equal("Auto", ByKey(tree, "tryinput").Text);
        Assert.True(ByKey(tree, "tryinput").Selected);
        Assert.Equal("On", ByKey(tree, "tryoutput").Text);
        Assert.Equal("90.00 kW", ByKey(tree, "outputting").Text);
        Assert.Equal("0.00 W", ByKey(tree, "available").Text);

        ByKey(tree, "tryinput").OnClick();
        ByKey(tree, "tryoutput").OnClick();
        UiNode output = ByKey(tree, "outputLevel");
        ByKey(output, "min").OnClick();
        ByKey(output, "backward").OnClick();
        ByKey(output, "forward").OnClick();
        ByKey(output, "max").OnClick();
        UiNode slider = ByKey(output, "slider");
        Assert.Equal((90.0, 0.0, 200.0, 5.0, 4.0), (slider.Value, slider.MinValue, slider.MaxValue, slider.Step, slider.StepPixelSize));
        Assert.Null(slider.FillValue);
        Assert.Equal("90.00 kW", slider.Format(90));
        slider.OnChange(120);
        UiNode input = ByKey(tree, "inputLevel");
        Assert.Equal(0.0, ByKey(input, "slider").FillValue); // the available input is the lighter bar
        ByKey(input, "slider").OnChange(30);
        Assert.Equal(new (string, string)[]
        {
            ("tryinput", null), ("tryoutput", null),
            ("output", "{\"target\":\"min\"}"), ("output", "{\"adjust\":-10000}"), ("output", "{\"adjust\":10000}"), ("output", "{\"target\":\"max\"}"),
            ("output", "{\"target\":120000}"), ("input", "{\"target\":30000}"),
        }, acts.Select(a => (a.Action, a.Params)));
        // the end buttons are off at the limits
        Assert.True(ByKey(ByKey(Render(Smes.Render, Json(data.Replace("\"outputLevel\":90000", "\"outputLevel\":0"))).Tree, "outputLevel"), "min").Disabled);
        Assert.True(ByKey(ByKey(Render(Smes.Render, Json(data.Replace("\"outputLevel\":90000", "\"outputLevel\":200000"))).Tree, "outputLevel"), "max").Disabled);
        // full: "Fully Charged" in green
        var (_, full, _) = Render(Smes.Render, Json(data.Replace("25000000", "50000000")));
        Assert.Equal(("Fully Charged", "good"), (ByKey(full, "chargeMode").Text, ByKey(full, "chargeMode").Color));
    }

    [Fact]
    public void ThePowerMonitorWindowDrawsTheHistoryAndTheApcRowsAndSortsThem()
    {
        const string data = "{\"stored\":60,\"interval\":5,\"attached\":1,\"supply\":\"270 kW\",\"demand\":\"200 kW\","
            + "\"history\":{\"supply\":[100000,300000,270000],\"demand\":[50000,600000,200000]},\"areas\":["
            + "{\"name\":\"Medbay\",\"charge\":80,\"load\":\"2.50 kW\",\"charging\":0,\"eqp\":3,\"lgt\":2,\"env\":1},"
            + "{\"name\":\"Atmos\",\"charge\":20,\"load\":\"900.00 W\",\"charging\":1,\"eqp\":0,\"lgt\":3,\"env\":3},"
            + "{\"name\":\"Bridge\",\"charge\":100,\"load\":\"1.20 MW\",\"charging\":2,\"eqp\":3,\"lgt\":3,\"env\":3}]}";
        var local = new TguiLocalState();
        var (context, tree, _) = Render(PowerMonitor.Render, Json(data), local);
        Assert.Equal((550.0, 700.0), (context.WindowWidth, context.WindowHeight));
        // the bars: the last point of each line against the biggest of the history and 500 kW (PEAK_DRAW)
        UiNode supply = ByKey(tree, "supply");
        Assert.Equal((270000.0, 0.0, 600000.0, "teal", "270 kW"), (supply.Value, supply.MinValue, supply.MaxValue, supply.Color, supply.Text));
        UiNode draw = ByKey(tree, "draw");
        Assert.Equal((200000.0, "pink", "200 kW"), (draw.Value, draw.Color, draw.Text));
        // the two lines over the history, x as the index, y from 0 to the maximum
        List<UiNode> lines = tree.Walk().Where(n => n.Kind == UiKind.ChartLine).ToList();
        Assert.Equal(new[] { "supplyLine", "demandLine" }, lines.Select(l => l.Key));
        Assert.Equal(new (double, double)[] { (0, 100000), (1, 300000), (2, 270000) }, lines[0].Data);
        Assert.Equal(((0.0, 2.0), (0.0, 600000.0)), (lines[1].RangeX, lines[1].RangeY));
        Assert.Equal("rgba(0, 181, 173, 1)", lines[0].StrokeColor);
        Assert.Equal("rgba(224, 57, 151, 0.25)", lines[1].FillColor);
        Assert.True(ByKey(tree, "chart").Plain);

        // the rows in the order of the list, with the charge icon and colour, the load and the three channel boxes
        List<UiNode> rows() => Render(PowerMonitor.Render, Json(data), local).Tree.Walk().Where(n => n.Kind == UiKind.TableRow && n.Key.StartsWith("area:")).ToList();
        Assert.Equal(new[] { "Medbay", "Atmos", "Bridge" }, rows().Select(r => ByKey(r, "name").Text));
        UiNode medbay = rows()[0];
        UiNode charge = ByKey(medbay, "charge");
        Assert.Equal(("battery-half", "yellow"), (ByKey(charge, "icon").Icon, ByKey(charge, "icon").Color)); // not charging, above 50 %
        Assert.Equal("80%", ByKey(charge, "percent").Text);
        Assert.Equal("2.50 kW", ByKey(medbay, "load").Text);
        UiNode eqp = ByKey(ByKey(medbay, "eqp"), "box"); // 3: on and auto
        Assert.Equal(("good", null, "On [auto]"), (eqp.Color, eqp.Text, eqp.Tooltip));
        UiNode lgt = ByKey(ByKey(medbay, "lgt"), "box"); // 2: on and manual
        Assert.Equal(("good", "M", "On [manual]"), (lgt.Color, lgt.Text, lgt.Tooltip));
        UiNode env = ByKey(ByKey(medbay, "env"), "box"); // 1: off and auto
        Assert.Equal(("bad", null, "Off [auto]"), (env.Color, env.Text, env.Tooltip));
        UiNode atmos = rows()[1];
        Assert.Equal(("bolt", "yellow"), (ByKey(ByKey(atmos, "charge"), "icon").Icon, ByKey(ByKey(atmos, "charge"), "icon").Color));
        Assert.Equal(("bad", "M", "Off [manual]"), (ByKey(ByKey(atmos, "eqp"), "box").Color, ByKey(ByKey(atmos, "eqp"), "box").Text, ByKey(ByKey(atmos, "eqp"), "box").Tooltip));
        Assert.Equal(("battery-full", "green"), (ByKey(ByKey(rows()[2], "charge"), "icon").Icon, ByKey(ByKey(rows()[2], "charge"), "icon").Color));

        // the sort buttons are checkboxes that toggle (useState), one at a time
        UiNode ByButton(string key) => ByKey(Render(PowerMonitor.Render, Json(data), local).Tree, key);
        ByButton("sort:name").OnClick();
        Assert.Equal(new[] { "Atmos", "Bridge", "Medbay" }, rows().Select(r => ByKey(r, "name").Text));
        Assert.True(ByButton("sort:name").Checkbox);
        ByButton("sort:charge").OnClick();
        Assert.Equal(new[] { "Atmos", "Medbay", "Bridge" }, rows().Select(r => ByKey(r, "name").Text));
        Assert.False(ByButton("sort:name").Checkbox);
        ByButton("sort:draw").OnClick(); // the highest draw first: powerSort(b, a) orders W, kW, MW by unit before value
        Assert.Equal(new[] { "Bridge", "Medbay", "Atmos" }, rows().Select(r => ByKey(r, "name").Text));
        ByButton("sort:draw").OnClick(); // clicked again: unsorted
        Assert.Equal(new[] { "Medbay", "Atmos", "Bridge" }, rows().Select(r => ByKey(r, "name").Text));
        Assert.Equal(2, PowerMonitor.PowerRank("1 MW"));
        Assert.Equal(12.5, PowerMonitor.ParseFloat("12.50 kW"));

        // no history yet: tg's loading text; an empty history: the loading screen
        Assert.Contains("Loading...", Render(PowerMonitor.Render, Json("{}")).Tree.AllText());
        Assert.Contains("Please wait...", Render(PowerMonitor.Render, Json("{\"history\":{\"supply\":[],\"demand\":[]},\"areas\":[]}")).Tree.AllText());
    }

    [Fact]
    public void ThePortableGeneratorWindowShowsTheFuelHeatAndOutputAndSendsTgsActs()
    {
        const string data = "{\"active\":0,\"sheet_name\":\"Solid plasma\",\"sheets\":15,\"stack_percent\":0,\"anchored\":0,\"connected\":0,\"ready_to_boot\":0,"
            + "\"power_generated\":\"10 kW\",\"power_output\":\"10 kW\",\"power_available\":0,\"current_heat\":0}";
        var (context, tree, acts) = Render(PortableGenerator.Render, Json(data));
        Assert.Equal((450.0, 340.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("Generator not anchored.", tree.AllText());
        Assert.Contains("Solid plasma sheets", tree.AllText());
        Assert.True(ByKey(tree, "power").Disabled); // not ready to boot
        Assert.Equal("Off", ByKey(tree, "power").Text);
        Assert.Equal("bad", ByKey(tree, "sheets").Color); // no sheet opened yet: stack_percent 0
        Assert.Equal(("Nominal", "good"), (ByKey(tree, "heat").Text, ByKey(tree, "heat").Color));
        Assert.Equal(("Unconnected", "bad"), (ByKey(tree, "available").Text, ByKey(tree, "available").Color));
        Assert.Equal("10 kW", ByKey(tree, "lower").Text);
        ByKey(tree, "eject").OnClick();
        ByKey(tree, "lower").OnClick();
        ByKey(tree, "higher").OnClick();
        Assert.Equal(new (string, string)[] { ("eject", null), ("lower_power", null), ("higher_power", null) }, acts.Select(a => (a.Action, a.Params)));

        const string running = "{\"active\":1,\"sheet_name\":\"Solid plasma\",\"sheets\":14,\"stack_percent\":60,\"anchored\":1,\"connected\":1,\"ready_to_boot\":1,"
            + "\"power_generated\":\"10 kW\",\"power_output\":\"20 kW\",\"power_available\":\"540 kW\",\"current_heat\":150}";
        var (_, on, onActs) = Render(PortableGenerator.Render, Json(running));
        Assert.DoesNotContain("Generator not anchored.", on.AllText());
        Assert.Equal("On", ByKey(on, "power").Text);
        Assert.False(ByKey(on, "power").Disabled);
        Assert.True(ByKey(on, "eject").Disabled); // it runs
        Assert.Equal("good", ByKey(on, "sheets").Color);
        Assert.Equal(("Caution", "average"), (ByKey(on, "heat").Text, ByKey(on, "heat").Color));
        Assert.Equal("540 kW", ByKey(on, "available").Text);
        ByKey(on, "power").OnClick();
        Assert.Equal(new (string, string)[] { ("toggle_power", null) }, onActs.Select(a => (a.Action, a.Params)));
        Assert.Equal(("DANGER", "bad"), (ByKey(Render(PortableGenerator.Render, Json(running.Replace("150", "250"))).Tree, "heat").Text, ByKey(Render(PortableGenerator.Render, Json(running.Replace("150", "250"))).Tree, "heat").Color));
    }

    [Fact]
    public void ALabeledListItemKeepsItsLabelBesideItsButtons()
    {
        // The M7 visual check: the APC window lost "Main Breaker", the channel names and the Misc labels, every LabeledList.Item with a
        // buttons prop, because the renderer drew the buttons in the label's cell. tgui-core's row is label | content | buttons.
        string channel(string title) => "{\"title\":\"" + title + "\",\"powerLoad\":\"500 W\",\"status\":2,\"topicParams\":{\"auto\":{\"eqp\":3},\"on\":{\"eqp\":2},\"off\":{\"eqp\":1}}}";
        UiValue data = Json("{\"locked\":0,\"isOperating\":1,\"externalPower\":2,\"powerCellStatus\":100,\"chargeMode\":1,\"chargingStatus\":2,"
            + "\"totalLoad\":\"2.03 kW\",\"coverLocked\":1,\"siliconUser\":0,\"malfStatus\":0,\"emergencyLights\":0,\"nightshiftLights\":0,\"failTime\":0,"
            + "\"powerChannels\":[" + channel("Equipment") + "," + channel("Lighting") + "," + channel("Environment") + "]}");
        UiNode tree = Render(Apc.Render, data).Tree;
        var rows = tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.LabeledRow()).ToArray();
        Assert.All(rows, r => Assert.Null(r.LabelNode));
        Assert.Equal(new[] { "Main Breaker:", "Power Cell:", "Charge Mode:", "Equipment:", "Lighting:", "Environment:", "Total Load:",
                "Cover Lock:", "Emergency Lighting:", "Night Shift Lighting:" },
            rows.Select(r => r.LabelText));
        Assert.Equal(new[] { true, false, true, true, true, true, false, true, true, true }, rows.Select(r => r.Buttons != null));
        Assert.Equal("breaker", rows[0].Buttons.Key);
        Assert.Equal(new[] { "state", "auto", "on", "off" }, rows[3].Buttons.Children.Select(b => b.Key));

        // An element label (the gas analyzer's gas buttons, tgui's label={<Button>}) is the label cell, with no buttons cell.
        UiNode gases = GasmixParser.Render(Json("{\"total_moles\":2,\"temperature\":293,\"volume\":70,\"pressure\":101,\"reactions\":[],"
            + "\"gases\":[[\"o2\",\"Oxygen\",1],[\"n2\",\"Nitrogen\",1]]}"), _ => { }, _ => { });
        var gas = gases.Walk().First(n => n.Kind == UiKind.LabeledItem).LabeledRow();
        Assert.Equal(("gas:o2", "", null), (gas.LabelNode?.Key, gas.LabelText, gas.Buttons));
    }
}
