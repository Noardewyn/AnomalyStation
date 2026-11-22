using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package P1: the client's OperatingComputer window without the engine: the patient's state with the doll of BodyZoneSelector.tsx,
/// the next operations pinned and filtered by tool, the catalog searched with its requirements, read from tg's ui_data and ui_static_data
/// the way the TSX does, and the zone change sent as tg's act.
/// </summary>
public class OperatingComputerWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json, TguiLocalState local)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "OperatingComputer", "operating computer", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, OperatingComputer.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Surgeries = "\"surgeries\":["
        + "{\"name\":\"make skin incision\",\"desc\":\"Make an incision.\",\"tool_rec\":\"scalpel\",\"requirements\":[[\"the patient must be lying down\"],[],[],[\"the skin must be intact\"]],\"show_as_next\":1,\"show_in_list\":1,\"priority\":0,\"mechanic\":0},"
        + "{\"name\":\"Disarticulation (Amputation)\",\"desc\":\"Sever a limb.\",\"tool_rec\":\"saw\",\"requirements\":[[\"the skin must be open\",\"the bone must be sawed\"],[],[],[]],\"show_as_next\":0,\"show_in_list\":1,\"priority\":0,\"mechanic\":0},"
        + "{\"name\":\"unscrew shell\",\"desc\":\"Unscrew a shell.\",\"tool_rec\":\"screwdriver\",\"requirements\":[[],[],[\"a surgeon may perform this on themselves\"],[]],\"show_as_next\":0,\"show_in_list\":1,\"priority\":0,\"mechanic\":1024},"
        + "{\"name\":\"Prepare for surgery\",\"desc\":\"Begin surgery.\",\"tool_rec\":\"surgical drapes\",\"show_as_next\":1,\"show_in_list\":0}]";

    private const string Patient = "\"patient\":{\"stat\":\"Stable\",\"statstate\":\"good\",\"health\":100,\"blood_type\":\"O+\",\"maxHealth\":100,\"minHealth\":-100,\"bruteLoss\":20,\"fireLoss\":0,\"toxLoss\":0,\"oxyLoss\":0,\"blood_level\":560,\"standard_blood_level\":560,\"surgery_state\":[\"Ready for surgery\",\"Skin is cut\"]}";

    [Fact]
    public void No_table_and_no_patient_show_tgs_notices()
    {
        var (context, tree, _) = Render("{\"has_table\":0,\"target_zone\":\"chest\"," + Surgeries + ",\"techwebs\":[],\"experiments\":[]}", new TguiLocalState());
        Assert.Equal((350.0, 610.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("operating_computer", context.WindowTheme);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "No table detected");
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Tab && n.Text == "Experiments");
        tree = Render("{\"has_table\":1,\"target_zone\":\"chest\"," + Surgeries + ",\"techwebs\":[],\"experiments\":[]}", new TguiLocalState()).Tree;
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "No patient detected");
    }

    [Fact]
    public void The_patient_state_shows_the_vitals_the_doll_and_the_next_steps_and_a_zone_click_is_sent()
    {
        var local = new TguiLocalState();
        var (_, tree, acts) = Render("{\"has_table\":1,\"target_zone\":\"chest\"," + Patient + "," + Surgeries + ",\"techwebs\":[],\"experiments\":[]}", local);
        Assert.Equal("Stable", ByKey(tree, "row:state").Text);
        Assert.Equal("good", ByKey(tree, "row:state").Color);
        UiNode health = ByKey(tree, "health");
        Assert.Equal((100.0, -100.0, 100.0, "100%"), (health.Value, health.MinValue, health.MaxValue, health.Text));
        Assert.Equal("20", tree.Walk().Single(n => n.Kind == UiKind.ProgressBar && n.Key == "bruteLoss").Text);
        Assert.Contains(tree.Walk(), n => n.Text == "- Skin is cut");
        // the doll: the slimecore theme's zone_sel, the chest drawn over it
        UiNode doll = ByKey(tree, "base");
        Assert.Equal(("icons/hud/screen_slimecore.dmi", "zone_sel"), (doll.DmIcon, doll.DmIconState));
        Assert.Contains(tree.Walk(), n => n.DmIcon == "icons/hud/screen_gen.dmi" && n.DmIconState == "chest");
        doll.OnClickAt(13 * 3, 28 * 3); // a pixel of the head (get_zone_at), scaled by 3
        Assert.Equal(("change_zone", "{\"new_zone\":\"head\"}"), acts.Single());

        // the next steps: the incision and tg's "Prepare for surgery" hint, not the amputation; a click pins one
        Assert.Contains(tree.Walk(), n => n.Key == "op:make skin incision");
        Assert.Contains(tree.Walk(), n => n.Key == "op:Prepare for surgery");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "op:Disarticulation (Amputation)");
        Assert.Equal("All tools", ByKey(tree, "filter").Text);
        ByKey(tree, "op:make skin incision").OnClickWith(new UiClick(false, false, false, false));
        tree = Render("{\"has_table\":1,\"target_zone\":\"chest\"," + Patient + "," + Surgeries + ",\"techwebs\":[],\"experiments\":[]}", local).Tree;
        Assert.Equal("danger", ByKey(tree, "op:make skin incision").Color);
        ByKey(tree, "filter").OnClickWith(new UiClick(false, false, false, false));
        tree = Render("{\"has_table\":1,\"target_zone\":\"chest\"," + Patient + "," + Surgeries + ",\"techwebs\":[],\"experiments\":[]}", local).Tree;
        Assert.Equal("Scalpel", ByKey(tree, "filter").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "op:Prepare for surgery");
    }

    [Fact]
    public void The_catalog_lists_searches_and_hides_the_mechanic_operations_with_their_requirements()
    {
        var local = new TguiLocalState();
        local.Set("tab", 2);
        var (context, tree, _) = Render("{\"has_table\":1,\"target_zone\":\"chest\"," + Patient + "," + Surgeries + ",\"techwebs\":[],\"experiments\":[]}", local);
        Assert.Equal(430.0, context.WindowWidth);
        // tg's extractSurgeryName: "Disarticulation (Amputation)" is the operation, its rnd name beside it
        Assert.Contains(tree.Walk(), n => n.Key == "name" && n.Text == "Disarticulation");
        Assert.Contains(tree.Walk(), n => n.Key == "true_name" && n.Text == "(Amputation)");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "op:Prepare for surgery"); // show_in_list 0
        Assert.Contains(tree.Walk(), n => n.Text == "All of the following are required:");
        Assert.Contains(tree.Walk(), n => n.Text == "- The bone must be sawed");
        Assert.Contains(tree.Walk(), n => n.Text == "The following is optional:"); // the shell's only line: no hard requirement before it
        ByKey(tree, "robotic").OnClick();
        tree = Render("{\"has_table\":1,\"target_zone\":\"chest\"," + Patient + "," + Surgeries + ",\"techwebs\":[],\"experiments\":[]}", local).Tree;
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "op:unscrew shell");
        ByKey(tree, "search").OnInput("saw");
        tree = Render("{\"has_table\":1,\"target_zone\":\"chest\"," + Patient + "," + Surgeries + ",\"techwebs\":[],\"experiments\":[]}", local).Tree;
        Assert.Equal(new[] { "op:Disarticulation (Amputation)" }, tree.Walk().Where(n => n.Key != null && n.Key.StartsWith("op:", StringComparison.Ordinal)).Select(n => n.Key));
    }

    [Fact]
    public void The_doll_maps_tgs_pixels_to_zones()
    {
        Assert.Equal(BodyZoneSelector.RightLeg, BodyZoneSelector.BodyZonePixelToZone(12, 5, true));
        Assert.Equal(BodyZoneSelector.Groin, BodyZoneSelector.BodyZonePixelToZone(16, 11, true));
        Assert.Equal(BodyZoneSelector.Chest, BodyZoneSelector.BodyZonePixelToZone(16, 11, false));
        Assert.Equal(BodyZoneSelector.Eyes, BodyZoneSelector.BodyZonePixelToZone(16, 26, true));
        Assert.Null(BodyZoneSelector.BodyZonePixelToZone(2, 2, true));
    }
}
