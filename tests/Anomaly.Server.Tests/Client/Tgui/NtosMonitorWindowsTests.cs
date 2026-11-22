using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps S4b (D327): the client's NtOS monitor windows without the engine: NtosPowerMonitor, NtosSignaler, NtosRoboControl,
/// NtosCyborgRemoteMonitor, NtosNetMonitor and NtosNetChat. The payloads follow the servers' ui_data (powermonitor.dm:80,
/// signalcommander.dm:32, robocontrol.dm:24, borg_monitor.dm:71, wirecarp.dm:39, ntnrc_client.dm:243).
/// </summary>
public class NtosMonitorWindowsTests
{
    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(string ui, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, ui, ui, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, acts);
    }

    private const string Header = "\"PC_device_theme\":\"ntos\",\"PC_batteryicon\":null,\"PC_batterypercent\":null,\"PC_ntneticon\":\"sig_high.gif\","
        + "\"PC_stationtime\":\"00:12:30\",\"PC_stationdate\":\"Fri, October 23, 2566\",\"PC_programheaders\":[],\"PC_lowpower_mode\":0,\"PC_showexitprogram\":1";

    [Fact]
    public void AmpcheckAndSignalcommanderWrapTheirMachinesContent()
    {
        var (power, _) = Context("NtosPowerMonitor", "{" + Header + ",\"stored\":60,\"interval\":5,\"attached\":1,\"supply\":\"120 kW\",\"demand\":\"80 kW\","
            + "\"history\":{\"supply\":[1,2],\"demand\":[1,1]},\"areas\":[{\"name\":\"Bridge\",\"charge\":90,\"load\":\"5 kW\",\"charging\":1,\"eqp\":3,\"lgt\":3,\"env\":3}]}");
        UiNode tree = NtosPowerMonitor.Render(power);
        Assert.Equal((550.0, 700.0), (power.WindowWidth, power.WindowHeight));
        Assert.Contains("Bridge", tree.AllText());

        var (signal, acts) = Context("NtosSignaler", "{" + Header + ",\"frequency\":1457,\"cooldown\":10,\"code\":30,\"minFrequency\":1201,\"maxFrequency\":1599}");
        UiNode signalTree = NtosSignaler.Render(signal);
        Assert.Equal((400.0, 300.0), (signal.WindowWidth, signal.WindowHeight));
        signalTree.Walk().First(n => n.Kind == UiKind.Button && n.Text == "Send Signal").OnClick();
        Assert.Equal(("signal", (string)null), acts.Single());
    }

    [Fact]
    public void BotkeeperListsABotAndAMulesControls()
    {
        var (c, acts) = Context("NtosRoboControl", "{" + Header + ",\"id_owner\":\"Alice Doe\",\"botcount\":2,\"droneaccess\":0,\"dronepingtypes\":[\"Low\",\"High\"],\"drones\":[],"
            + "\"bots\":[{\"name\":\"Cleanbot\",\"mode\":\"Idle\",\"model\":\"Cleanbot\",\"locat\":\"Bar\",\"bot_ref\":\"[0x1]\",\"mule_check\":0},"
            + "{\"name\":\"MULE #1\",\"mode\":\"Idle\",\"model\":\"MULEbot\",\"locat\":\"Cargo\",\"bot_ref\":\"[0x2]\",\"mule_check\":1}],"
            + "\"mules\":[{\"name\":\"MULE #1\",\"id\":\"1\",\"dest\":null,\"power\":50,\"home\":\"QM #1\",\"autoReturn\":1,\"autoPickup\":0,\"reportDelivery\":1,\"mule_ref\":\"[0x2]\",\"load\":null}]}");
        UiNode tree = NtosRoboControl.Render(c);
        UiNode cleanbot = tree.Walk().Single(n => n.Key == "bot:[0x1]");
        cleanbot.Walk().Single(n => n.Key == "patroloff").OnClick();
        cleanbot.Walk().Single(n => n.Key == "summon").OnClick();
        UiNode mule = tree.Walk().Single(n => n.Key == "bot:[0x2]");
        Assert.Contains(mule.Walk(), n => n.Key == "dest" && n.Text == "N/A");
        Assert.True(mule.Walk().Single(n => n.Key == "autoret").Checkbox);
        Assert.False(mule.Walk().Single(n => n.Key == "autopick").Checkbox);
        mule.Walk().Single(n => n.Key == "go").OnClick();
        Assert.Equal(new[]
        {
            ("patroloff", "{\"robot\":\"[0x1]\"}"), ("summon", "{\"robot\":\"[0x1]\"}"), ("go", "{\"robot\":\"[0x2]\"}"),
        }, acts);

        tree.Walk().Single(n => n.Key == "tab:drones").OnClick();
        UiNode drones = NtosRoboControl.Render(c);
        Assert.Equal("Revoke Drone Access", drones.Walk().Single(n => n.Key == "droneaccess").Text);
        drones.Walk().Single(n => n.Key == "ping:High").OnClick();
        Assert.Equal(("ping_drones", "{\"ping_type\":\"High\"}"), acts.Last());
    }

    [Fact]
    public void SiliconnectShowsACyborgsStateAndItsDownloadedLog()
    {
        var (c, acts) = Context("NtosCyborgRemoteMonitor", "{" + Header + ",\"card\":1,\"DL_progress\":50,\"borglog\":[\"System log of unit Robot-123\",\"Activated\"],"
            + "\"cyborgs\":[{\"name\":\"Robot-123\",\"integ\":60,\"locked_down\":0,\"status\":0,\"shell_discon\":0,\"charge\":25,\"module\":\"Engineering Model\",\"upgrades\":[\"[Ion thruster] \"],\"ref\":\"[0x9]\"}]}");
        UiNode tree = NtosCyborgRemoteMonitor.Render(c);
        UiNode borg = tree.Walk().Single(n => n.Key == "cyborg:[0x9]");
        Assert.Contains(borg.Walk(), n => n.Text == "Nominal" && n.Color == "good");
        Assert.Contains(borg.Walk(), n => n.Text == "Functionality Impaired" && n.Color == "average");
        Assert.Contains(borg.Walk(), n => n.Text == "25%" && n.Color == "bad");
        Assert.Contains(borg.Walk(), n => n.Key == "upgrades" && n.Text == "[Ion thruster] ");
        borg.Walk().Single(n => n.Key == "message").OnClick();
        Assert.Equal(("messagebot", "{\"ref\":\"[0x9]\"}"), acts.Single());

        tree.Walk().Single(n => n.Key == "tab:log").OnClick();
        UiNode log = NtosCyborgRemoteMonitor.Render(c);
        Assert.Equal("Downloading", log.Walk().Single(n => n.Key == "progress").Text);
        Assert.Contains(log.Walk(), n => n.Text == "Activated" && n.Color == "green");

        var (empty, _) = Context("NtosCyborgRemoteMonitor", "{" + Header + ",\"card\":0,\"cyborgs\":[],\"borglog\":[]}");
        Assert.Contains(NtosCyborgRemoteMonitor.Render(empty).Walk(), n => n.Text == "No cyborg units detected.");
    }

    [Fact]
    public void WirecarpTogglesARelayTheIdsAndAPdasMassMessages()
    {
        var (c, acts) = Context("NtosNetMonitor", "{" + Header + ",\"ntnetrelays\":[{\"is_operational\":1,\"name\":\"NTNet Quantum Relay\",\"ref\":\"[0x5]\"}],"
            + "\"idsstatus\":1,\"idsalarm\":1,\"ntnetlogs\":[{\"entry\":\"00:10 -- a log line\"}],\"tablets\":[{\"enabled_spam\":0,\"name\":\"Bob Roe\",\"ref\":\"[0x7]\"}]}");
        UiNode tree = NtosNetMonitor.Render(c);
        Assert.Contains(tree.Walk(), n => n.Text == "NETWORK INCURSION DETECTED");
        Assert.Contains(tree.Walk(), n => n.Text == "00:10 -- a log line");
        tree.Walk().Single(n => n.Key == "toggleIDS").OnClick();
        tree.Walk().Single(n => n.Key == "resetIDS").OnClick();
        UiNode relay = tree.Walk().Single(n => n.Key == "relay:[0x5]").Buttons;
        relay.OnClick(); // Button.Confirm: asks first
        NtosNetMonitor.Render(c).Walk().Single(n => n.Key == "relay:[0x5]").Buttons.OnClick();
        Assert.Equal(new[] { ("toggleIDS", (string)null), ("resetIDS", (string)null), ("toggle_relay", "{\"ref\":\"[0x5]\"}") }, acts);

        tree.Walk().Single(n => n.Key == "tab:tablets").OnClick();
        UiNode tablets = NtosNetMonitor.Render(c);
        Assert.Equal("Allow Mass PDA", tablets.Walk().Single(n => n.Key == "tablet:[0x7]").Buttons.Text);
    }

    [Fact]
    public void TheChatClientJoinsSpeaksAndItsOperatorMutes()
    {
        var local = new TguiLocalState();
        var (c, acts) = Context("NtosNetChat", "{" + Header + ",\"selfref\":\"[0xa]\",\"all_channels\":[{\"chan\":\"ops\",\"id\":0},{\"chan\":\"cargo\",\"id\":1}],\"active_channel\":0,"
            + "\"title\":\"ops\",\"strong\":0,\"is_operator\":1,\"username\":\"Alice_D\",\"adminmode\":0,\"can_admin\":0,\"authed\":1,"
            + "\"clients\":[{\"name\":\"Alice_D\",\"online\":1,\"away\":0,\"muted\":0,\"operator\":1,\"status\":3,\"ref\":\"[0xa]\"},"
            + "{\"name\":\"Bob\",\"online\":0,\"away\":1,\"muted\":0,\"operator\":0,\"status\":2,\"ref\":\"[0xb]\"}],"
            + "\"messages\":[{\"key\":\"1\",\"msg\":\"12:00 Bob: hello\"},{\"key\":\"0\",\"msg\":\"12:00 -!- Bob has joined the channel.\"}]}", local);
        UiNode tree = NtosNetChat.Render(c);
        Assert.Equal((1000.0, 675.0), (c.WindowWidth, c.WindowHeight));
        Assert.True(tree.Walk().Single(n => n.Key == "chan:ops").Selected);
        Assert.Contains(tree.Walk(), n => n.Text == "12:00 Bob: hello");
        UiNode alice = tree.Walk().Single(n => n.Key == "client:Alice_D");
        Assert.DoesNotContain(alice.Walk(), n => n.Key == "ping"); // not yourself
        UiNode bob = tree.Walk().Single(n => n.Key == "client:Bob");
        bob.Walk().Single(n => n.Key == "ping").OnClick();
        bob.Walk().Single(n => n.Key == "mute").OnClick();
        tree.Walk().Single(n => n.Key == "say").OnInput("hi Bob");
        NtosNetChat.Render(c).Walk().Single(n => n.Key == "say").OnEnter();
        tree.Walk().Single(n => n.Key == "chan:cargo").OnClick();
        Assert.Equal(new[]
        {
            ("PRG_ping_user", "{\"ref\":\"[0xb]\"}"), ("PRG_mute_user", "{\"ref\":\"[0xb]\"}"), ("PRG_speak", "{\"message\":\"hi Bob\"}"), ("PRG_joinchannel", "{\"id\":1}"),
        }, acts);
        Assert.Equal("", NtosNetChat.Render(c).Walk().Single(n => n.Key == "say").TextValue); // selfClear

        var (none, _) = Context("NtosNetChat", "{" + Header + ",\"selfref\":\"[0xa]\",\"all_channels\":[],\"active_channel\":null,\"username\":\"DefaultUser123\","
            + "\"adminmode\":0,\"can_admin\":1,\"authed\":0,\"clients\":[],\"messages\":[]}");
        UiNode noneTree = NtosNetChat.Render(none);
        Assert.Contains(noneTree.Walk(), n => n.Text == "Click a channel to start chatting!");
        Assert.Equal("ADMIN MODE: OFF", noneTree.Walk().Single(n => n.Key == "adminmode").Text);
    }
}
