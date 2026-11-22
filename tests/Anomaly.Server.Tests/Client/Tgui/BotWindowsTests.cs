using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package C3: the client's bot windows without the engine: SimpleBot.tsx, RepairBot.tsx, RepairbotResources.tsx, Mule.tsx, NavBeacon.tsx, BotAnnouncement.tsx and
/// ProximitySensor.jsx read tg's ui_data the way the TSX does and send tg's acts.
/// </summary>
public class BotWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts, TguiLocalState Local) Render(TguiInterface view, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        local ??= new TguiLocalState();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, view(context), acts, local);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Settings = "\"settings\":{\"pai_inserted\":0,\"allow_possession\":8,\"possession_enabled\":0,\"airplane_mode\":0,\"maintenance_lock\":1,\"power\":1,\"patrol_station\":2}";

    [Fact]
    public void A_locked_bot_shows_its_notice_and_no_controls()
    {
        var (context, tree, _, _) = Render(SimpleBot.Render, "SimpleBot", "{\"can_hack\":0,\"custom_controls\":{},\"emagged\":0,\"has_access\":0,\"locked\":2,\"settings\":{}}");
        Assert.Equal((450.0, 300.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "Locked!");
        Assert.DoesNotContain(tree.Walk(), n => n.Title == "Controls");
        Assert.True(ByKey(tree, "lock").Disabled);
    }

    [Fact]
    public void An_unlocked_cleanbot_shows_the_settings_and_its_toggles_and_sends_tgs_acts()
    {
        var (_, tree, acts, _) = Render(SimpleBot.Render, "SimpleBot", "{\"can_hack\":0,\"custom_controls\":{\"clean_blood\":1,\"clean_trash\":0},\"emagged\":0,\"has_access\":1,\"locked\":0," + Settings + "}");
        Assert.Equal(new[] { "Power", "Airplane Mode", "Patrol Station", "Maintenance Lock", "Personality", "Clean Blood", "Clean Trash" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledControl).Select(n => n.Label));
        Assert.Equal("good", ByKey(tree, "power").Color);
        Assert.Equal("yellow", ByKey(tree, "maintenance").Color);
        Assert.Equal("toggle-on", ByKey(tree, "clean_blood").Icon);
        Assert.Equal("toggle-off", ByKey(tree, "clean_trash").Icon);
        Assert.Equal("No PAI Inserted", ByKey(tree, "pai").Text);
        ByKey(tree, "power").OnClick();
        ByKey(tree, "clean_trash").OnClick();
        ByKey(tree, "lock").OnClick();
        Assert.Equal(new[] { "power", "clean_trash", "lock" }, acts.Select(a => a.Action));
    }

    [Fact]
    public void A_medbots_threshold_is_a_slider_and_a_silicon_gets_the_hack_button()
    {
        var (_, tree, acts, _) = Render(SimpleBot.Render, "SimpleBot", "{\"can_hack\":1,\"custom_controls\":{\"heal_threshold\":10,\"speaker\":1},\"emagged\":1,\"has_access\":1,\"locked\":2," + Settings + "}");
        UiNode slider = ByKey(tree, "heal_threshold");
        Assert.Equal((10.0, 5.0, 75.0, 5.0, "%"), (slider.Value, slider.MinValue, slider.MaxValue, slider.Step, slider.Unit));
        slider.OnChange(25);
        Assert.Equal(("heal_threshold", "{\"threshold\":25}"), acts[0]);
        Assert.Equal("Malfunctional", ByKey(tree, "hack").Text);
    }

    [Fact]
    public void A_repairbot_lists_its_materials_and_gives_one_back()
    {
        var (context, tree, acts, _) = Render(RepairBot.Render, "RepairBot", "{\"can_hack\":0,\"custom_controls\":{\"fix_breaches\":1},\"emagged\":0,\"has_access\":1,\"locked\":0," + Settings
            + ",\"repairbot_materials\":[{\"material_ref\":\"[0x1]\",\"material_icon\":\"icons/obj/stack_objects.dmi\",\"material_icon_state\":\"sheet-metal\"}]}");
        Assert.Equal((450.0, 415.0), (context.WindowWidth, context.WindowHeight));
        UiNode mat = ByKey(tree, "material:[0x1]");
        Assert.Equal(("icons/obj/stack_objects.dmi", "sheet-metal"), (mat.DmIcon, mat.DmIconState));
        mat.OnClick();
        Assert.Equal(("remove_item", "{\"item_reference\":\"[0x1]\"}"), acts[0]);
        tree = RepairBot.Render(new TguiContext(1, "RepairBot", "RepairBot", 2, UiValue.FromJson("{\"can_hack\":0,\"custom_controls\":{},\"has_access\":1,\"locked\":0," + Settings + ",\"repairbot_materials\":[]}"), (a, p) => { }, new TguiLocalState()));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "No Materials!");
    }

    [Fact]
    public void The_resources_window_shows_the_stacks_and_ejects_one()
    {
        var (context, tree, acts, _) = Render(RepairbotResources.Render, "RepairbotResources", "{\"repairbot_icon\":\"icons/ui/x.dmi\",\"repairbot_icon_state\":\"smile\",\"stacks\":[{\"stack_reference\":\"[0x2]\",\"stack_name\":\"iron\",\"stack_amount\":25,\"stack_maximum_amount\":50,\"stack_icon\":\"a.dmi\",\"stack_icon_state\":\"sheet\"}]}");
        Assert.Equal("ntos", context.WindowTheme);
        UiNode bar = ByKey(tree, "amount:[0x2]");
        Assert.Equal((25.0, 50.0), (bar.Value, bar.MaxValue));
        ByKey(tree, "eject:[0x2]").OnClick();
        Assert.Equal(("eject", "{\"item_reference\":\"[0x2]\"}"), acts[0]);
    }

    [Fact]
    public void A_mule_shows_its_status_and_sends_the_destination_and_tgs_own_odd_acts()
    {
        const string Mule = "{\"powerStatus\":1,\"locked\":0,\"siliconUser\":0,\"mode\":\"Idle\",\"modeStatus\":\"good\",\"load\":1,\"loadName\":\"crate\",\"destination\":\"QM #1\",\"homeDestination\":\"QM #2\",\"destinationsList\":[\"QM #1\",\"Bar\"],\"cellPercent\":75,\"autoReturn\":1,\"autoPickup\":2,\"reportDelivery\":0,\"botId\":\"Bert\",\"allowPossession\":0,\"possessionEnabled\":0,\"paiInserted\":0}";
        var (context, tree, acts, _) = Render(Anomaly.Client.Shared.Tgui.Interfaces.Mule.Render, "Mule", Mule);
        Assert.Equal((350.0, 540.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(0.75, ByKey(tree, "cell").Value);
        Assert.Contains(tree.Walk(), n => n.Label == "Load" && n.Text == "Crate" && n.Color == "good");
        ByKey(tree, "destination").OnSelected("Bar");
        ByKey(tree, "go").OnClick();
        ByKey(tree, "autoret").OnClick();
        ByKey(tree, "unload").OnClick();
        Assert.Equal(new[] { ("destination", "{\"value\":\"Bar\"}"), ("go", null), ("autored", null), ("unload", null) }, acts);
        // a locked mule hides the controls
        var locked = Render(Anomaly.Client.Shared.Tgui.Interfaces.Mule.Render, "Mule", Mule.Replace("\"locked\":0", "\"locked\":2"));
        Assert.DoesNotContain(locked.Tree.Walk(), n => n.Key == "go");
    }

    [Fact]
    public void A_navbeacon_shows_its_codes_and_locks_for_a_crewman()
    {
        const string Beacon = "{\"locked\":0,\"siliconUser\":0,\"controls\":{\"location\":\"Bar\",\"patrol_enabled\":1,\"patrol_next\":\"Kitchen\",\"delivery_enabled\":0,\"delivery_direction\":\"north\",\"cover_locked\":1},\"static_controls\":{\"direction_options\":[\"none\",\"east\",\"north\"],\"has_codes\":1}}";
        var (context, tree, acts, _) = Render(NavBeacon.Render, "NavBeacon", Beacon);
        Assert.Equal((400.0, 350.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Bar", ByKey(tree, "location").Text);
        Assert.True(ByKey(tree, "patrol").Checkbox);
        ByKey(tree, "direction").OnSelected("east");
        ByKey(tree, "reset").OnClick();
        Assert.Equal(new[] { ("set_delivery_direction", "{\"direction\":\"east\"}"), ("reset_codes", null) }, acts);
        Assert.True(ByKey(Render(NavBeacon.Render, "NavBeacon", Beacon.Replace("\"locked\":0", "\"locked\":1")).Tree, "location").Disabled);
    }

    [Fact]
    public void The_announcement_window_plays_a_chosen_line_and_makes_a_shortcut()
    {
        const string Data = "{\"channels\":[\"Medical\"],\"lines\":[\"Hello\",\"Bye\"],\"button_data\":[{\"name\":\"Hello\",\"channel\":\"Medical\"}],\"cooldown_left\":0}";
        var (_, tree, acts, local) = Render(BotAnnouncement.Render, "BotAnnouncement", Data);
        ByKey(tree, "select:1").OnClick();
        (_, tree, acts, _) = Render(BotAnnouncement.Render, "BotAnnouncement", Data, local);
        ByKey(tree, "play").OnClick();
        ByKey(tree, "shortcut").OnClick();
        Assert.Equal("announce", acts[0].Action);
        Assert.Contains("Bye", acts[0].Params);
        Assert.Equal("set_button", acts[1].Action);
        ByKey(tree, "tab:shortcuts").OnClick();
        (_, tree, acts, _) = Render(BotAnnouncement.Render, "BotAnnouncement", Data, local);
        ByKey(tree, "select:0").OnClick();
        (_, tree, acts, _) = Render(BotAnnouncement.Render, "BotAnnouncement", Data, local);
        ByKey(tree, "shortcut").OnClick();
        Assert.Equal(("remove_button", "{\"index\":1}"), acts[0]);
        Assert.True(ByKey(Render(BotAnnouncement.Render, "BotAnnouncement", Data.Replace("\"cooldown_left\":0", "\"cooldown_left\":30")).Tree, "play").Disabled);
    }

    [Fact]
    public void The_proximity_sensor_arms_and_adjusts_its_clock()
    {
        var (_, tree, acts, _) = Render(ProximitySensor.Render, "ProximitySensor", "{\"minutes\":1,\"seconds\":5,\"timing\":0,\"scanning\":0,\"sensitivity\":2}");
        Assert.Equal("01:05", ByKey(tree, "clock").Text);
        Assert.Equal("2", ByKey(tree, "range").Text);
        ByKey(tree, "scanning").OnClick();
        ByKey(tree, "adjust+30").OnClick();
        Assert.Equal(new[] { ("scanning", null), ("input", "{\"adjust\":30}") }, acts);
    }
}
