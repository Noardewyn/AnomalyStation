using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The client's ordnance windows without the engine: the TankCompressor's notice, gauge, switch and records; the DopplerArray's record
/// tabs with the theoretical radii and the causes; the MassDriverControl with and without a driver; the NtosScipaper's tabs, its form's
/// dropdowns and the published papers.
/// </summary>
public class OrdnanceWindowsTests
{
    private static (UiNode Tree, List<(string Action, UiValue Params)> Acts, TguiContext Context) Render(TguiInterface window, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, UiValue)>();
        var c = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p)), local ?? new TguiLocalState());
        UiNode tree = window(c);
        return (tree, acts, c);
    }

    private const string Compressor = "{\"tankPresent\":1,\"tankPressure\":3500,\"leaking\":0,\"active\":1,\"transferRate\":20,\"lastPressure\":0,"
        + "\"disk\":\"data disk\",\"storage\":\"4 / 16 GQ\",\"maxTransfer\":20,\"leakPressure\":3040,\"fragmentPressure\":4053,\"ejectPressure\":2634.45,"
        + "\"records\":[{\"ref\":\"[0x1]\",\"name\":\"Log Recording #1\",\"source\":\"plasma tank\",\"timestamp\":\"12:00 (PT: 00:10)\",\"gases\":{\"Plasma\":250.5}}]}";

    [Fact]
    public void TheTankCompressorShowsItsTankAndItsRecords()
    {
        var (tree, acts, c) = Render(TankCompressor.Render, "TankCompressor", Compressor);
        Assert.Equal((440.0, 440.0, "Tank Compressor"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        UiNode notice = tree.Walk().Single(n => n.Key == "notice");
        Assert.Equal(("Leak Hazard", "average"), (notice.Text, notice.Color));
        Assert.Equal(3500, tree.Walk().Single(n => n.Key == "gauge").Value);
        UiNode toggle = tree.Walk().Single(n => n.Key == "toggle_injection");
        Assert.Equal("On", toggle.Text);
        Assert.True(tree.Walk().Single(n => n.Key == "eject_tank").Disabled, "a tank above the eject pressure stays in");
        Assert.Equal("data disk (4 / 16 GQ)", tree.Walk().Single(n => n.Key == "disk").Title);
        Assert.Equal("250.50 moles", tree.Walk().Single(n => n.Key == "gas-Plasma").Text);
        toggle.OnClick();
        tree.Walk().Single(n => n.Key == "rate-max").OnClick();
        tree.Walk().Single(n => n.Key == "save_record").OnClick();
        Assert.Equal(new[] { "toggle_injection", "change_rate", "save_record" }, acts.Select(a => a.Action));
        Assert.Equal(20, acts[1].Params.Num("target"));
        Assert.Equal("[0x1]", acts[2].Params.Str("ref"));

        (tree, _, _) = Render(TankCompressor.Render, "TankCompressor", "{\"tankPresent\":0,\"lastPressure\":5000,\"records\":[],\"leakPressure\":3040,\"fragmentPressure\":4053}");
        Assert.Equal("Tank destroyed. Displaying last recorded data.", tree.Walk().Single(n => n.Key == "notice").Text);
        Assert.Equal("No Records", tree.Walk().Single(n => n.Key == "no-records").Text);
        Assert.Equal("No Disk Inserted", tree.Walk().Single(n => n.Key == "disk").Title);
    }

    private const string Doppler = "{\"disk\":null,\"storage\":\" /  GQ\",\"records\":[{\"name\":\"Log Recording #1\",\"timestamp\":\"12:00\",\"coordinates\":\"40, 4\","
        + "\"displacement\":0.5,\"factual_epicenter_radius\":3,\"factual_outer_radius\":7,\"factual_shockwave_radius\":14,\"theory_epicenter_radius\":10,"
        + "\"theory_outer_radius\":20,\"theory_shockwave_radius\":40,\"reaction_results\":[\"Plasma Combustion\"],\"ref\":\"[0x2]\"}]}";

    [Fact]
    public void TheDopplerArrayShowsTheSelectedRecord()
    {
        var (tree, acts, c) = Render(DopplerArray.Render, "DopplerArray", Doppler);
        Assert.Equal((650.0, 320.0), (c.WindowWidth, c.WindowHeight));
        Assert.True(tree.Walk().Single(n => n.Key == "tab-Log Recording #1").Selected);
        Assert.Equal("40, 4", tree.Walk().Single(n => n.Key == "coordinates").Text);
        Assert.Equal("0.5 seconds", tree.Walk().Single(n => n.Key == "displacement").Text);
        Assert.Equal("14 (Theoretical: 40)", tree.Walk().Single(n => n.Key == "shockwave").Text);
        Assert.Equal("Plasma Combustion", tree.Walk().Single(n => n.Key == "cause-Plasma Combustion").Text);
        Assert.True(tree.Walk().Single(n => n.Key == "save_record").Disabled, "no disk: nothing to save to");
        Assert.Equal("No Disk Inserted", tree.Walk().Single(n => n.Key == "footer").Title);

        // the delete button asks first
        var local = new TguiLocalState();
        (tree, acts, _) = Render(DopplerArray.Render, "DopplerArray", Doppler, local);
        tree.Walk().Single(n => n.Key == "delete_record").OnClick();
        (tree, acts, _) = Render(DopplerArray.Render, "DopplerArray", Doppler, local);
        UiNode confirm = tree.Walk().Single(n => n.Key == "delete_record");
        Assert.Equal("Confirm?", confirm.Text);
        confirm.OnClick();
        Assert.Equal("delete_record", acts.Single().Action);
        Assert.Equal("[0x2]", acts.Single().Params.Str("ref"));

        (tree, _, _) = Render(DopplerArray.Render, "DopplerArray", "{\"records\":[]}");
        Assert.Equal("No Records", tree.Walk().Single(n => n.Key == "no-records").Text);
    }

    [Fact]
    public void TheMassDriverControlTimesAndLaunches()
    {
        var (tree, acts, c) = Render(MassDriverControl.Render, "MassDriverControl", "{\"connected\":1,\"seconds\":5,\"minutes\":0,\"timing\":0,\"power\":1,\"poddoor\":1}");
        Assert.Equal((300.0, 215.0), (c.WindowWidth, c.WindowHeight));
        Assert.Equal(" 00:05 ", tree.Walk().Single(n => n.Key == "clock").Text);
        tree.Walk().Single(n => n.Key == "adjust30").OnClick();
        tree.Walk().Single(n => n.Key == "launch").OnClick();
        tree.Walk().Single(n => n.Key == "door").OnClick();
        tree.Walk().Single(n => n.Key == "power-input").OnChange(4);
        Assert.Equal(new[] { "input", "launch", "door", "set_power" }, acts.Select(a => a.Action));
        Assert.Equal(30, acts[0].Params.Num("adjust"));
        Assert.Equal(4, acts[3].Params.Num("power"));

        (tree, _, c) = Render(MassDriverControl.Render, "MassDriverControl", "{\"connected\":0,\"timing\":0,\"poddoor\":0}");
        Assert.Equal(107.0, c.WindowHeight);
        Assert.Equal("No connected mass driver", tree.Walk().Single(n => n.Key == "none").Text);
        Assert.True(tree.Walk().Single(n => n.Key == "door").Disabled);
    }

    [Fact]
    public void NtFrontierWritesAndListsPapers()
    {
        string form = "{\"currentTab\":1,\"has_techweb\":1,\"fileList\":{\"Gas Compressor Log Recording #1\":7},\"expList\":{\"Plasma Gas Shells\":\"/datum/experiment/ordnance/gaseous/plasma\"},"
            + "\"allowedTiers\":[1],\"allowedPartners\":{\"Mining Corps\":\"/datum/scientific_partner/mining\"},\"gains\":{\"cooperation\":\"Little\",\"funding\":\"Little\"},"
            + "\"title\":null,\"author\":null,\"etAlia\":0,\"abstract\":null,\"tier\":1,\"selectedFile\":\"Gas Compressor Log Recording #1\",\"selectedExperiment\":\"Plasma Gas Shells\","
            + "\"selectedPartner\":null,\"coopIndex\":\"cooperation\",\"fundingIndex\":\"funding\",\"PC_device_theme\":\"ntos\"}";
        var (tree, acts, _) = Render(NtosScipaper.Render, "NtosScipaper", form);
        Assert.True(tree.Walk().Single(n => n.Key == "tab1").Selected);
        Assert.Null(tree.Walk().FirstOrDefault(n => n.Key == "no-techweb"));
        Assert.Equal("Little", tree.Walk().Single(n => n.Key == "coop-value").Text);
        tree.Walk().Single(n => n.Key == "select_file").OnSelected("Gas Compressor Log Recording #1");
        tree.Walk().Single(n => n.Key == "select_experiment").OnSelected("Plasma Gas Shells");
        tree.Walk().Single(n => n.Key == "select_tier").OnSelected("1");
        tree.Walk().Single(n => n.Key == "select_partner").OnSelected("Mining Corps");
        tree.Walk().Single(n => n.Key == "title-input").OnCommit("On plasma");
        tree.Walk().Single(n => n.Key == "publish").OnClick();
        tree.Walk().Single(n => n.Key == "tab2").OnClick();
        Assert.Equal(new[] { "select_file", "select_experiment", "select_tier", "select_partner", "rewrite", "publish", "change_tab" }, acts.Select(a => a.Action));
        Assert.Equal(7, acts[0].Params.Num("selected_uid"));
        Assert.Equal("/datum/experiment/ordnance/gaseous/plasma", acts[1].Params.Str("selected_expath"));
        Assert.Equal(1, acts[2].Params.Num("selected_tier"));
        Assert.Equal("/datum/scientific_partner/mining", acts[3].Params.Str("selected_partner"));
        Assert.Equal("On plasma", acts[4].Params.Str("title"));
        Assert.Equal(2, acts[6].Params.Num("new_tab"));

        string papers = "{\"currentTab\":2,\"has_techweb\":0,\"coopIndex\":\"cooperation\",\"fundingIndex\":\"funding\",\"publishedPapers\":[{\"title\":\"On plasma\","
            + "\"author\":\"Bob\",\"etAlia\":1,\"abstract\":\"Burns.\",\"experimentName\":\"Plasma Gas Shells\",\"tier\":1,\"partner\":\"Mining Corps\",\"gains\":{\"cooperation\":\"Little\",\"funding\":\"Little\"}}]}";
        (tree, _, _) = Render(NtosScipaper.Render, "NtosScipaper", papers);
        Assert.NotNull(tree.Walk().Single(n => n.Key == "no-techweb"));
        Assert.Equal("On plasma", tree.Walk().Single(n => n.Key == "paper-Plasma Gas Shells1").Title);
        Assert.Equal("Bob et al.", tree.Walk().Single(n => n.Key == "author").Text);
        Assert.Equal("Plasma Gas Shells - 1", tree.Walk().Single(n => n.Key == "topic").Text);
    }
}
