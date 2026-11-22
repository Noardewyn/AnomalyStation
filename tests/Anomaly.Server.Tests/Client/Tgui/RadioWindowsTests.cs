using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// M9 slice D: the client's Radio, Signaler, Telecomms and AutomatedAnnouncement windows, and (slice D2) the TelecommsMonitor and
/// ServerMonitor consoles' (tg's .tsx/.jsx) without the engine; the payloads follow the servers' ui_data (radio.dm:480,
/// signaler.dm:84, machine_interactions.dm:34, announcement_system.dm:106, telemonitor.dm:41, logbrowser.dm:27).
/// </summary>
public class RadioWindowsTests
{
    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(string ui, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, ui, ui, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, acts);
    }

    [Fact]
    public void TheRadioShowsItsChannelAndSendsItsToggles()
    {
        var (c, acts) = Context("Radio", "{\"broadcasting\":0,\"listening\":1,\"frequency\":1357,\"minFrequency\":1441,\"maxFrequency\":1489,\"freqlock\":0,"
            + "\"channels\":{\"Engineering\":1,\"Command\":0},\"command\":1,\"useCommand\":0,\"subspace\":1,\"subspaceSwitchable\":0,\"headset\":1}");
        UiNode tree = Radio.Render(c);
        Assert.Equal((330.0, 105.0 + 2 * 25 + 8), (c.WindowWidth, c.WindowHeight)); // tg's height for two channels
        Assert.Equal("[Engineering]", tree.Walk().Single(n => n.Key == "tuned").Text);
        Assert.Equal(new[] { "Engineering", "Command" }, tree.Walk().Where(n => n.Key == "channel").Select(n => n.Text));
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "tune"); // a subspace headset does not tune
        tree.Walk().Single(n => n.Key == "listen").OnClick();
        tree.Walk().Where(n => n.Key == "channel").Last().OnClick();
        tree.Walk().Single(n => n.Key == "freq").OnChange(135.9);
        Assert.Equal(("listen", (string)null), acts[0]);
        Assert.Equal(("channel", "{\"channel\":\"Command\"}"), acts[1]);
        Assert.Equal("frequency", acts[2].Action);
        Assert.Contains("\"adjust\":0.2", acts[2].Params, StringComparison.Ordinal);
        Assert.Equal("High volume OFF", tree.Walk().Single(n => n.Key == "command").Text);
    }

    [Fact]
    public void TheSignalerSendsItsCodeAndSignal()
    {
        var (c, acts) = Context("Signaler", "{\"frequency\":1457,\"cooldown\":10,\"code\":30,\"minFrequency\":1201,\"maxFrequency\":1599}");
        UiNode tree = Signaler.Render(c);
        Assert.Equal((280.0, 128.0), (c.WindowWidth, c.WindowHeight));
        Assert.Equal("Cooldown: 1 seconds", tree.Walk().Single(n => n.Key == "signal").Tooltip);
        tree.Walk().Single(n => n.Key == "code").OnChange(42);
        tree.Walk().Single(n => n.Key == "signal").OnClick();
        Assert.Equal(("code", "{\"code\":42}"), acts[0]);
        Assert.Equal(("signal", (string)null), acts[1]);
    }

    [Fact]
    public void TheTelecommsWindowNeedsAMultitool()
    {
        const string Base = "\"channels\":[{\"freq\":1459,\"name\":\"Common\",\"color\":\"#1ecc43\"}],\"minfreq\":1201,\"maxfreq\":1599,\"frequency\":1459,"
            + "\"toggled\":1,\"id\":\"Hub\",\"network\":\"tcommsat\",\"prefab\":1,\"linked\":[{\"index\":1,\"name\":\"subspace receiver\",\"id\":\"Receiver A\"}],\"frequencies\":[1459]";
        var (without, _) = Context("Telecomms", "{" + Base + "}");
        UiNode bare = Telecomms.Render(without);
        Assert.Equal("Hub", without.WindowTitle);
        Assert.Contains(bare.Walk(), n => n.Key == "nomultitool");
        Assert.DoesNotContain(bare.Walk(), n => n.Title == "Linked Network Entities");

        var (with, acts) = Context("Telecomms", "{" + Base + ",\"multitool\":\"multitool\",\"multibuff\":\"subspace receiver\"}");
        UiNode tree = Telecomms.Render(with);
        Assert.Equal("1. Receiver A (subspace receiver)", tree.Walk().Single(n => n.Bold && n.Text != null && n.Text.StartsWith("1.", StringComparison.Ordinal)).Text);
        Assert.Equal("[Common]", tree.Walk().First(n => n.Key == "tag").Text);
        Assert.Equal("Current Buffer: subspace receiver", tree.Walk().Single(n => n.Key == "buffer").Text);
        tree.Walk().Single(n => n.Key == "unlink").OnClick();
        tree.Walk().Single(n => n.Key == "link").OnClick();
        Assert.Equal(("unlink", "{\"value\":1}"), acts[0]);
        Assert.Equal(("link", (string)null), acts[1]);
    }

    [Fact]
    public void TheAnnouncementSystemListsAndEditsItsLines()
    {
        var (c, acts) = Context("AutomatedAnnouncement", "{\"max_announcement_len\":168,\"config_entries\":["
            + "{\"name\":\"Global: Arrival Announcement\",\"entryRef\":\"[0x1]\",\"enabled\":1,\"modifiable\":1,\"announcementLinesMap\":{\"Message\":\"%PERSON has signed up as %RANK\"},"
            + "\"generalTooltip\":null,\"varsAndTooltipsMap\":{\"PERSON\":\"will be replaced with their name.\"}},"
            + "{\"name\":\"Engineering Alert: Arrivals Shuttle Malfunction Announcement\",\"entryRef\":\"[0x2]\",\"enabled\":1,\"modifiable\":0,\"announcementLinesMap\":{\"Message\":\"x\"},"
            + "\"generalTooltip\":\"No vars.\",\"varsAndTooltipsMap\":{}}]}");
        UiNode tree = AutomatedAnnouncement.Render(c);
        Assert.Equal(new[] { "Engineering Alert: Arrivals Shuttle Malfunction Announcement", "Global: Arrival Announcement" }, tree.Walk().Where(n => n.Kind == UiKind.Section && n.Key != null).Select(n => n.Title));
        Assert.Equal("%PERSON will be replaced with their name.", tree.Walk().Where(n => n.Key == "info").Last().Tooltip);
        Assert.True(tree.Walk().First(n => n.Key == "toggle").Disabled); // CentCom's entry
        tree.Walk().Single(n => n.Key == "line:[0x1]:Message").OnCommit("%PERSON joined");
        Assert.Equal(("Text", "{\"entryRef\":\"[0x1]\",\"lineKey\":\"Message\",\"newText\":\"%PERSON joined\"}"), acts[0]);
    }

    [Fact]
    public void TheTelecommsMonitorProbesAndListsItsMachines()
    {
        var (c, acts) = Context("TelecommsMonitor", "{\"screen\":0,\"statusMessage\":\"2 ENTITIES LOCATED & BUFFERED\",\"network\":\"tcommsat\","
            + "\"machines\":[{\"id\":\"Hub\",\"name\":\"telecommunication hub\",\"icon\":\"hub\"},{\"id\":\"Receiver A\",\"name\":\"subspace receiver\",\"icon\":\"broadcast receiver\"}]}");
        UiNode tree = TelecommsMonitor.Render(c);
        Assert.Equal((350.0, 500.0, "T-comms Monitoring Console"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        Assert.Equal("2 ENTITIES LOCATED & BUFFERED", tree.Walk().Single(n => n.Key == "status").Text);
        Assert.Equal(new[] { "hub", "broadcast receiver" }, tree.Walk().Where(n => n.Key == "icon").Select(n => n.DmIconState));
        Assert.All(tree.Walk().Where(n => n.Key == "icon"), n => Assert.Equal(TguiSpritesheets.Tcomms32, n.DmIcon));
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Tcomms32, "hub", out SpritesheetIcon hub));
        Assert.Equal(("icons/obj/machines/telecomms.dmi", "hub", (byte)2), (hub.Files.Single(), hub.State, hub.Dir));
        Assert.Equal("Telecommunication Hub", tree.Walk().First(n => n.Key == "select").Tooltip);
        tree.Walk().Single(n => n.Key == "probe").OnClick();
        tree.Walk().First(n => n.Key == "select").OnClick();
        tree.Walk().Single(n => n.Key == "flush").OnClick();
        Assert.Equal(("probe", "{\"id\":\"tcommsat\"}"), acts[0]);
        Assert.Equal(("view", "{\"id\":\"Hub\"}"), acts[1]);
        Assert.Equal(("flush", (string)null), acts[2]);

        // the search by ID (local state)
        tree.Walk().Single(n => n.Key == "search").OnClick();
        TelecommsMonitor.Render(c).Walk().Single(n => n.Key == "searchInput").OnInput("rec");
        Assert.Single(TelecommsMonitor.Render(c).Walk(), n => n.Key == "select");
    }

    [Fact]
    public void TheServerMonitorShowsAServersPackets()
    {
        var (c, acts) = Context("ServerMonitor", "{\"screen\":1,\"network\":\"tcommsat\",\"error\":\"\",\"server\":{\"name\":\"Common Server\",\"traffic\":2048,"
            + "\"packets\":[{\"name\":\"data packet (ab12)\",\"language\":\"Galactic Common\",\"message\":\"\\\"Hello\\\"\",\"race\":\"Humanoid\",\"source\":\"Alice\",\"job\":\"Assistant\",\"type\":\"Speech File\",\"ref\":\"[0x2]\"}]}}");
        UiNode tree = ServerMonitor.Render(c);
        Assert.Equal((575.0, 400.0), (c.WindowWidth, c.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Label == "Total Recorded Traffic" && n.Text == "2 TB");
        Assert.Contains(tree.Walk(), n => n.Label == "Source" && n.Text == "Alice (Assistant)");
        Assert.Contains(tree.Walk(), n => n.Label == "Contents" && n.Text == "\"Hello\"");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "error");
        tree.Walk().Single(n => n.Key == "delete").OnClick();
        tree.Walk().Single(n => n.Key == "home").OnClick();
        Assert.Equal(("delete_packet", "{\"ref\":\"[0x2]\"}"), acts[0]);
        Assert.Equal(("return_home", (string)null), acts[1]);
    }
}
