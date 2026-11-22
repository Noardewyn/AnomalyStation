using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Syndicate N1b (D316): the client's NtosRadar windows (tg's NtosRadar.tsx, NtosRadarSyndicate.tsx) without the engine; the payloads
/// follow the radar's ui_data (radar.dm:57). The signals listed and picked, the pointer on the grid near, the turned arrow far, and
/// "Signal Lost" when the picked one is gone.
/// </summary>
public class NtosRadarTests
{
    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(string ui, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, ui, ui, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, acts);
    }

    private const string Header = "\"PC_device_theme\":\"syndicate\",\"PC_batteryicon\":\"batt_80.gif\",\"PC_batterypercent\":\"79%\",\"PC_ntneticon\":\"sig_high.gif\","
        + "\"PC_stationtime\":\"00:12:30\",\"PC_stationdate\":\"Fri, October 23, 2566\",\"PC_programheaders\":[],\"PC_lowpower_mode\":0,\"PC_showexitprogram\":1";

    private const string Objects = "\"object\":[{\"ref\":\"[0x2000011]\",\"name\":\"syndicate nuclear device\"},{\"ref\":\"[0x2000012]\",\"name\":\"Nuke Auth. Disk\"}]";

    [Fact]
    public void TheSignalsAreListedAndPickedAndTheNearPointerStandsOnTheGrid()
    {
        var (c, acts) = Context("NtosRadarSyndicate", "{" + Header + ",\"selected\":\"[0x2000012]\",\"scanning\":0," + Objects
            + ",\"target\":{\"locx\":26,\"locy\":22,\"userot\":0,\"rot\":0,\"arrowstyle\":\"ntosradarpointerS.png\",\"color\":\"red\",\"pointer\":\"crosshairs\"}}");
        UiNode tree = NtosRadar.RenderSyndicate(c);
        Assert.Equal((800.0, 600.0, "syndicate"), (c.WindowWidth, c.WindowHeight, c.WindowTheme));
        Assert.Equal(NtosRadar.Background, tree.Walk().Single(n => n.Key == "background").DmIcon);
        UiNode disk = tree.Walk().Single(n => n.Key == "target:1");
        Assert.True(disk.Selected);
        Assert.False(tree.Walk().Single(n => n.Key == "target:0").Selected);
        UiNode pointer = tree.Walk().Single(n => n.Key == "pointer");
        Assert.Equal(("crosshairs", "red", 276.0, 239.0), (pointer.Icon, pointer.Color, pointer.PosX, pointer.PosY));
        tree.Walk().Single(n => n.Key == "target:0").OnClick();
        tree.Walk().Single(n => n.Key == "scan").OnClick();
        Assert.Equal(("selecttarget", "{\"ref\":\"[0x2000011]\"}"), acts[0]);
        Assert.Equal(("scan", (string)null), acts[1]);
    }

    [Fact]
    public void AFarTargetTurnsTheArrowAndALostOneSaysSo()
    {
        var (far, _) = Context("NtosRadar", "{" + Header + ",\"selected\":\"[0x2000012]\",\"scanning\":0," + Objects
            + ",\"target\":{\"locx\":80,\"locy\":2,\"userot\":1,\"rot\":63,\"arrowstyle\":\"ntosradarpointer.png\",\"color\":\"green\",\"pointer\":\"crosshairs\"}}");
        UiNode arrow = NtosRadar.Render(far).Walk().Single(n => n.Key == "arrow");
        Assert.Equal(("icons/ui/tgui/ntosradar_pointer.png", 63.0, 243.0, 20.0), (arrow.DmIcon, arrow.Rotation, arrow.PosX, arrow.PosY));

        var (lost, _) = Context("NtosRadar", "{" + Header + ",\"selected\":\"[0x2000012]\",\"scanning\":1," + Objects + ",\"target\":[]}");
        UiNode tree = NtosRadar.Render(lost);
        Assert.Equal("Signal Lost", tree.Walk().Single(n => n.Key == "lost").Text);
        Assert.Equal("Scanning...", tree.Walk().Single(n => n.Key == "scan").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "target:0"); // the list hides while scanning
    }
}
