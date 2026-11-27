using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The client's exploration drone windows without the engine: the ExoscannerConsole's array power, wide scan, sites, a site's scans,
/// the scan in progress and the failure; the ExodroneConsole's drone listing, master controls, equipment and tools, travel
/// destinations with their bands, the travel and busy screens, a site's exploration and event, an adventure's screen with its
/// picture, and the lost signal; the AdventureBrowser's listing and playtest. All three are registered under their names.
/// </summary>
public class ExplorationWindowsTests
{
    private static (UiNode Tree, List<(string Action, UiValue Params)> Acts, TguiContext Context) Render(TguiInterface window, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, UiValue)>();
        var c = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p)), local ?? new TguiLocalState());
        return (window(c), acts, c);
    }

    private const string Bands = "\"all_bands\":{\"Plasma absorption band\":0,\"Hydrocarbons/Molecular oxygen\":0,\"Narrow-band radio waves\":0,\"Exotic Radiation\":0,\"Increased Density\":0}";

    private const string Tools = "\"all_tools\":{\"welder\":{\"description\":\"A heavy duty welder.\",\"icon\":\"burn\"},\"laser\":{\"description\":\"Multipurpose tool.\",\"icon\":\"bolt\"}}";

    [Fact]
    public void TheWindowsAreRegistered()
    {
        foreach (string name in new[] { "ExoscannerConsole", "ExodroneConsole", "AdventureBrowser" })
            Assert.NotNull(TguiInterfaces.Find(name));
    }

    [Fact]
    public void TheScannerConsoleShowsThePowerTheWideScanAndTheSites()
    {
        var (tree, acts, c) = Render(ExoscannerConsole.Render, "ExoscannerConsole",
            "{\"failed\":0,\"selected_site\":null,\"scan_power\":2,\"wide_scan_eta\":1200,\"scan_conditions\":null,\"scan_in_progress\":0,"
            + "\"possible_sites\":[{\"ref\":\"[0x5]\",\"name\":\"Anomaly\"}]," + Bands + "}");
        Assert.Equal((550.0, 600.0), (c.WindowWidth, c.WindowHeight));
        Assert.Contains("Cost estimate: 2m", tree.AllText());
        Assert.Equal("2", tree.Walk().Single(n => n.Key == "power" && n.Kind == UiKind.Box).Text);
        tree.Walk().Single(n => n.Key == "scan" && n.Kind == UiKind.Button).OnClick();
        tree.Walk().Single(n => n.Key == "[0x5]" && n.Kind == UiKind.Button).OnClick();
        tree.Walk().Single(n => n.Key == "experiments" && n.OnClick != null).OnClick();
        Assert.Equal(new[] { "start_wide_scan", "select_site", "open_experiments" }, acts.Select(a => a.Action));
        Assert.Equal("[0x5]", acts[1].Params.Str("site_ref"));

        (tree, _, _) = Render(ExoscannerConsole.Render, "ExoscannerConsole", "{\"scan_power\":0,\"possible_sites\":[],\"scan_in_progress\":0}");
        Assert.Contains("No properly configured scanner arrays detected.", tree.AllText());
        Assert.Contains("∞ minutes", tree.AllText());
        Assert.True(tree.Walk().Single(n => n.Key == "scan" && n.Kind == UiKind.Button).Disabled);
    }

    [Fact]
    public void ASelectedSiteOffersItsScansAndAScanInProgressItsStop()
    {
        var (tree, acts, _) = Render(ExoscannerConsole.Render, "ExoscannerConsole",
            "{\"failed\":0,\"selected_site\":\"[0x5]\",\"scan_power\":1,\"point_scan_eta\":1200,\"deep_scan_eta\":3600,"
            + "\"site_data\":{\"ref\":\"[0x5]\",\"name\":\"asteroid belt\",\"description\":\"A dense asteroid belt.\",\"distance\":2,\"revealed\":1,\"band_info\":{\"Increased Density\":3}},"
            + "\"scan_conditions\":[\"Site is within an unusually dense nebula.\"],\"scan_in_progress\":0," + Bands + "}");
        Assert.Contains("A dense asteroid belt.", tree.AllText());
        Assert.Contains("Site is within an unusually dense nebula.", tree.AllText());
        Assert.Equal("3", tree.Walk().Single(n => n.Key == "band:Increased Density").Text);
        Assert.Contains("Estimated Time: 2m.", tree.AllText());
        Assert.Contains("Estimated Time: 6m.", tree.AllText());
        foreach (UiNode scan in tree.Walk().Where(n => n.Key == "scan" && n.Kind == UiKind.Button).ToList())
            scan.OnClick();
        tree.Walk().Single(n => n.Key == "back" && n.OnClick != null).OnClick();
        Assert.Equal(new[] { "start_point_scan", "start_deep_scan", "select_site" }, acts.Select(a => a.Action));

        var local = new TguiLocalState();
        (tree, acts, _) = Render(ExoscannerConsole.Render, "ExoscannerConsole",
            "{\"scan_power\":1,\"possible_sites\":[],\"scan_in_progress\":1,\"scan_time\":600,\"current_scan_power\":1,\"scan_description\":\"Wide: Scanning sphere starting 1 AU from the station.\"}", local);
        UiNode modal = tree.Walk().Single(n => n.Kind == UiKind.Modal);
        Assert.Contains("Scan in Progress!", modal.AllText());
        Assert.Contains("00:01:00", modal.AllText());
        modal.Walk().Single(n => n.Key == "stop" && n.Kind == UiKind.Button).OnClick(); // the confirm
        (tree, acts, _) = Render(ExoscannerConsole.Render, "ExoscannerConsole",
            "{\"scan_power\":1,\"possible_sites\":[],\"scan_in_progress\":1,\"scan_time\":600}", local);
        tree.Walk().Single(n => n.Key == "stop" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal("stop_scan", Assert.Single(acts).Action);

        (tree, acts, _) = Render(ExoscannerConsole.Render, "ExoscannerConsole", "{\"scan_power\":1,\"possible_sites\":[],\"scan_in_progress\":0,\"failed\":1}");
        Assert.Contains("SCAN FAILURE!", tree.AllText());
        tree.Walk().Single(n => n.Key == "confirm" && n.OnClick != null).OnClick();
        Assert.Equal("confirm_fail", Assert.Single(acts).Action);
    }

    [Fact]
    public void TheDroneConsoleListsTheDronesAndTakesControl()
    {
        var (tree, acts, c) = Render(ExodroneConsole.Render, "ExodroneConsole",
            "{\"signal_lost\":0,\"drone\":null,\"all_drones\":[{\"name\":\"Voyager\",\"controlled\":0,\"description\":\"Idle.\",\"ref\":\"[0x9]\"},"
            + "{\"name\":\"Juno\",\"controlled\":1,\"description\":\"Exploring asteroid belt.\",\"ref\":\"[0xa]\"}]," + Tools + "," + Bands + "}");
        Assert.Equal((750.0, 600.0), (c.WindowWidth, c.WindowHeight));
        Assert.Contains("Exploration Drone Listing", tree.AllText());
        Assert.Contains("Controlled by another console.", tree.AllText());
        tree.Walk().Single(n => n.Key == "assume" && n.OnClick != null).OnClick();
        Assert.Equal(("select_drone", "[0x9]"), (acts[0].Action, acts[0].Params.Str("drone_ref")));
    }

    private static string Drone(string status, string extra) =>
        "{\"signal_lost\":0,\"drone\":\"Voyager\",\"drone_status\":\"" + status + "\",\"drone_name\":\"Voyager\",\"drone_integrity\":100,\"drone_max_integrity\":200,"
        + "\"drone_log\":[\"Launched from launcher.\"],\"configurable\":" + (status == "idle" ? 1 : 0) + ",\"cargo\":[{\"type\":\"tool\",\"name\":\"laser\"},"
        + "{\"type\":\"cargo\",\"name\":\"antique\",\"ref\":\"[0x20]\"},{\"type\":\"empty\",\"name\":\"Free space\"}],\"drone_travel_coefficent\":250,"
        + "\"can_travel\":1,\"travel_error\":\"\"," + Tools + "," + Bands + (extra.Length > 0 ? "," + extra : "") + "}";

    [Fact]
    public void AnIdleDroneShowsItsControlsEquipmentAndDestinations()
    {
        var local = new TguiLocalState();
        var (tree, acts, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("idle",
            "\"sites\":[{\"ref\":\"[0x5]\",\"name\":\"space junk field\",\"coordinates\":\"ℓ:10°,𝑏:20°\",\"description\":\"A giant cluster of space junk.\",\"distance\":2,"
            + "\"band_info\":{\"Increased Density\":3,\"Exotic Radiation\":0}}],\"site\":null"), local);
        Assert.Equal("Voyager", tree.Walk().Single(n => n.Key == "id").Text);
        Assert.Equal("Idle", tree.Walk().Single(n => n.Key == "activity").Text);
        Assert.Equal(100, tree.Walk().Single(n => n.Key == "integrity" && n.Kind == UiKind.ProgressBar).Value);
        Assert.Contains("Launched from launcher.", tree.AllText());
        Assert.Contains("Empty slot", tree.AllText());
        Assert.Contains("ETA: 50s", tree.AllText()); // two distance units at 250 ds each
        Assert.Equal("3", tree.Walk().Single(n => n.Key == "band:Increased Density").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "band:Exotic Radiation");
        tree.Walk().Single(n => n.Key == "launch" && n.OnClick != null).OnClick();
        tree.Walk().Single(n => n.Key == "eject" && n.OnClick != null).OnClick();
        tree.Walk().Single(n => n.Key == "jettison" && n.OnClick != null).OnClick();
        Assert.Equal(new[] { "start_travel", "remove_tool", "jettison" }, acts.Select(a => a.Action));
        Assert.Equal("[0x5]", acts[0].Params.Str("target_site"));
        Assert.Equal("laser", acts[1].Params.Str("tool_type"));

        // the wrench opens the tool selection, whose tool adds it
        tree.Walk().Single(n => n.Key == "tools" && n.Kind == UiKind.Button).OnClick();
        (tree, acts, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("idle", "\"sites\":[],\"site\":null"), local);
        UiNode modal = tree.Walk().Single(n => n.Kind == UiKind.Modal);
        Assert.Contains("Welder", modal.AllText());
        Assert.DoesNotContain("No Destinations.", tree.AllText()); // hidden while the tools are chosen
        modal.Walk().First(n => n.Key == "add").OnClick();
        Assert.Equal(("add_tool", "welder"), (acts[0].Action, acts[0].Params.Str("tool_type")));
        (tree, _, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("idle", "\"sites\":[],\"site\":null"), local);
        Assert.Contains("No Destinations.", tree.AllText());
    }

    [Fact]
    public void ATravellingOrBusyDroneShowsItsTime()
    {
        var (tree, _, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("travel", "\"travel_time\":500,\"travel_time_left\":300"));
        Assert.Contains("Travel Time: 00:00:30", tree.AllText());
        (tree, _, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("busy", "\"wait_time_left\":250,\"wait_message\":\"Welding open the cache...\""));
        Assert.Contains("Welding open the cache... (00:00:25)", tree.AllText());
    }

    [Fact]
    public void AnExploringDroneShowsTheSiteItsEventsAndAnEventsScreen()
    {
        var local = new TguiLocalState();
        string site = "\"site\":{\"ref\":\"[0x5]\",\"name\":\"mining facility\",\"coordinates\":\"ℓ:1°,𝑏:2°\",\"description\":\"An abandoned mining facility.\",\"distance\":1,"
            + "\"band_info\":[],\"events\":[{\"name\":\"locked maintenance room\",\"ref\":\"[0x30]\"}]},\"sites\":[]";
        var (tree, acts, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("exploration", site), local);
        Assert.Contains("An abandoned mining facility.", tree.AllText());
        tree.Walk().Single(n => n.Key == "explore" && n.Kind == UiKind.Button).OnClick();
        tree.Walk().Single(n => n.Key == "event" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal("Locked maintenance room", tree.Walk().Single(n => n.Key == "event" && n.Kind == UiKind.Button).Text);
        Assert.Equal(new[] { "explore", "explore_event" }, acts.Select(a => a.Action));
        Assert.Equal("[0x30]", acts[1].Params.Str("target_event"));
        tree.Walk().Single(n => n.Key == "travel" && n.Kind == UiKind.Button).OnClick();
        (tree, _, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("exploration", site), local);
        Assert.Contains("Travel Destinations", tree.AllText());
        Assert.Contains("Home", tree.AllText());
        tree.Walk().Single(n => n.Key == "cancel" && n.OnClick != null).OnClick();

        (tree, acts, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("exploration", site
            + ",\"event\":{\"image\":\"default\",\"description\":\"You spot a cleverly hidden metal container.\",\"action_enabled\":1,\"action_text\":\"Weld open\","
            + "\"skippable\":1,\"ignore_text\":\"Ignore\",\"ref\":\"[0x31]\"}"));
        Assert.Equal(ExodroneConsole.AdventurePicture("default"), tree.Walk().Single(n => n.Key == "image" && n.Kind == UiKind.Box).DmIcon);
        Assert.Equal("icons/ui/adventure/default.png", ExodroneConsole.AdventurePicture("default"));
        tree.Walk().Single(n => n.Key == "act" && n.OnClick != null).OnClick();
        tree.Walk().Single(n => n.Key == "ignore" && n.OnClick != null).OnClick();
        Assert.Equal(new[] { "start_event", "skip_event" }, acts.Select(a => a.Action));
    }

    // a 1x1 PNG, as an adventure node's raw_image
    private const string OnePixel = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==";

    [Fact]
    public void AnAdventureShowsItsTextPictureAndChoices()
    {
        var (tree, acts, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("adventure",
            "\"adventure_data\":{\"description\":\"Cameras Online.\",\"image\":null,\"raw_image\":\"" + OnePixel + "\",\"choices\":[{\"key\":\"choice 0\",\"text\":\"Hail other Drone\"}]}"));
        Assert.Contains("Cameras Online.", tree.AllText());
        UiNode picture = tree.Walk().Single(n => n.Key == "image");
        Assert.NotNull(picture.Pixels);
        Assert.Equal((1, 1), (picture.Pixels.Width, picture.Pixels.Height));
        tree.Walk().Single(n => n.Key == "choice" && n.OnClick != null).OnClick();
        Assert.Equal(("adventure_choice", "choice 0"), (acts[0].Action, acts[0].Params.Str("choice")));

        (tree, _, _) = Render(ExodroneConsole.Render, "ExodroneConsole", Drone("adventure",
            "\"adventure_data\":{\"description\":\"A grue.\",\"image\":\"grue\",\"raw_image\":null,\"choices\":[]}"));
        Assert.Equal("icons/ui/adventure/grue.png", tree.Walk().Single(n => n.Key == "image" && n.Kind == UiKind.Box).DmIcon);
    }

    [Fact]
    public void TheLostSignalIsConfirmed()
    {
        var (tree, acts, _) = Render(ExodroneConsole.Render, "ExodroneConsole", "{\"signal_lost\":1,\"drone\":null,\"all_drones\":[]," + Tools + "," + Bands + "}");
        UiNode modal = tree.Walk().Single(n => n.Kind == UiKind.Modal);
        Assert.Contains("CONNECTION LOST", modal.AllText());
        modal.Walk().Single(n => n.Key == "confirm" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal("confirm_signal_lost", Assert.Single(acts).Action);
    }

    [Fact]
    public void TheAdventureBrowserListsAndPlaysTheAdventures()
    {
        var (tree, acts, c) = Render(AdventureBrowser.Render, "AdventureBrowser",
            "{\"adventures\":[{\"ref\":\"[0x40]\",\"filename\":\"space_yacht.json\",\"name\":\"Space Yacht\",\"version\":1,\"uploader\":\"someone\",\"approved\":0,\"json_status\":\"Valid JSON\"}],"
            + "\"feedback_message\":\"\",\"play_mode\":0,\"adventure_data\":null,\"delay_time\":0,\"delay_message\":\"\"}");
        Assert.Equal((600.0, 400.0, "Adventure Overview"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        Assert.Contains("space_yacht.json", tree.AllText());
        tree.Walk().Single(n => n.Key == "play" && n.OnClick != null).OnClick();
        Assert.Equal(("play", "[0x40]"), (acts[0].Action, acts[0].Params.Str("ref")));

        (tree, acts, _) = Render(AdventureBrowser.Render, "AdventureBrowser",
            "{\"adventures\":[],\"feedback_message\":\"Adventure started\",\"play_mode\":1,\"adventure_data\":{\"description\":\"A yacht.\",\"image\":\"default\",\"raw_image\":null,"
            + "\"choices\":[{\"key\":\"a\",\"text\":\"Board\"}]},\"delay_time\":0,\"delay_message\":\"\"}");
        Assert.Contains("Adventure started", tree.AllText());
        Assert.Contains("A yacht.", tree.AllText());
        tree.Walk().Single(n => n.Key == "choice" && n.OnClick != null).OnClick();
        tree.Walk().Single(n => n.Key == "end" && n.OnClick != null).OnClick();
        Assert.Equal(new[] { "adventure_choice", "end_play" }, acts.Select(a => a.Action));

        (tree, _, _) = Render(AdventureBrowser.Render, "AdventureBrowser",
            "{\"adventures\":[],\"feedback_message\":\"\",\"play_mode\":1,\"adventure_data\":null,\"delay_time\":50,\"delay_message\":\"Boarding...\"}");
        Assert.Contains("DELAY 00:00:05 / Boarding...", tree.AllText());
    }
}
