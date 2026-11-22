using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps S4c (D327): the client's NtOS device, game, maintenance and antagonist windows without the engine: NtosEmojipedia,
/// NtosPhysScanner, NtosBotanicalEncyclopedia, NtosCursor, NtosNetDos, NtosRevelation, NtosArcade and NtosSpectreMeter.
/// </summary>
public class NtosDeviceAppsWindowsTests
{
    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(string ui, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, ui, ui, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, acts);
    }

    private const string Header = "\"PC_device_theme\":\"ntos\",\"PC_batteryicon\":null,\"PC_batterypercent\":null,\"PC_ntneticon\":\"sig_high.gif\","
        + "\"PC_stationtime\":\"00:12:30\",\"PC_stationdate\":\"Fri, October 23, 2566\",\"PC_programheaders\":[],\"PC_lowpower_mode\":0,\"PC_showexitprogram\":1";

    [Fact]
    public void EmojipediaFiltersAndCopiesATag()
    {
        var (c, _) = Context("NtosEmojipedia", "{" + Header + ",\"emoji_list\":[{\"name\":\"smile\"},{\"name\":\"sad\"},{\"name\":\"smirk\"}]}");
        string copied = null;
        TguiContext.CopyText = text => copied = text;
        try
        {
            UiNode tree = NtosEmojipedia.Render(c);
            Assert.Equal(3, tree.Walk().Count(n => n.Key != null && n.Key.StartsWith("emoji:", StringComparison.Ordinal)));
            Assert.Equal("icons/ui/chat/emoji.dmi", tree.Walk().Single(n => n.Key == "emoji:sad").Walk().Last().DmIcon);
            tree.Walk().Single(n => n.Key == "filter").OnInput("sm");
            UiNode filtered = NtosEmojipedia.Render(c);
            Assert.Equal(2, filtered.Walk().Count(n => n.Key != null && n.Key.StartsWith("emoji:", StringComparison.Ordinal)));
            filtered.Walk().Single(n => n.Key == "emoji:smirk").OnClick();
            Assert.Equal(":smirk:", copied);
        }
        finally
        {
            TguiContext.CopyText = null;
        }
    }

    [Fact]
    public void TheSmallAppsDrawTheirState()
    {
        var (scan, _) = Context("NtosPhysScanner", "{" + Header + ",\"last_record\":\"<b>Analyzing results for Bob:</b>\"}");
        Assert.Contains(NtosPhysScanner.Render(scan).Walk(), n => n.Kind == UiKind.Html && n.TextValue.Contains("Analyzing results", StringComparison.Ordinal));

        var (cursor, _) = Context("NtosCursor", "{" + Header + ",\"dmi\":{\"icon\":\"icons/effects/mouse_pointers/cool_sword.dmi\",\"icon_state\":\"\"}}");
        UiNode cursorTree = NtosCursor.Render(cursor);
        Assert.Equal("There's only one option... It's the sword.", cursorTree.Walk().Single(n => n.Kind == UiKind.NoticeBox).Text);
        for (int i = 0; i < 3; i++)
            NtosCursor.Render(cursor).Walk().Single(n => n.Key == "sword" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal("You clicked the sword. It's still the sword.", NtosCursor.Render(cursor).Walk().Single(n => n.Kind == UiKind.NoticeBox).Text);

        var (rev, revActs) = Context("NtosRevelation", "{" + Header + ",\"armed\":1}");
        UiNode revTree = NtosRevelation.Render(rev);
        Assert.Equal("ARMED", revTree.Walk().Single(n => n.Key == "arm").Text);
        revTree.Walk().Single(n => n.Key == "activate").OnClick();
        Assert.Equal(("PRG_activate", (string)null), revActs.Single());

        var (meter, meterActs) = Context("NtosSpectreMeter", "{" + Header + ",\"spook_value\":42,\"auto_mode\":0,\"on_cooldown\":1}");
        UiNode meterTree = NtosSpectreMeter.Render(meter);
        Assert.True(meterTree.Walk().Single(n => n.Key == "scan").Disabled); // cooling down
        Assert.Equal(" Spookiness: 42% ", meterTree.Walk().Single(n => n.Key == "spook").Text);
        meterTree.Walk().Single(n => n.Key == "toggle_mode").OnClick();
        Assert.Equal(("toggle_mode", (string)null), meterActs.Single());
    }

    [Fact]
    public void TheDosPicksARelayThenShowsTheFlood()
    {
        var (pick, acts) = Context("NtosNetDos", "{" + Header + ",\"error\":\"\",\"target\":0,\"relays\":[{\"id\":\"R1\"},{\"id\":\"R2\"}],\"focus\":\"R2\"}");
        UiNode tree = NtosNetDos.Render(pick);
        Assert.True(tree.Walk().Single(n => n.Key == "relay:R2").Selected);
        tree.Walk().Single(n => n.Key == "relay:R1").OnClick();
        tree.Walk().Single(n => n.Key == "execute").OnClick();
        Assert.Equal(new[] { ("PRG_target_relay", "{\"targid\":\"R1\"}"), ("PRG_execute", (string)null) }, acts);

        var (flood, _) = Context("NtosNetDos", "{" + Header + ",\"error\":\"\",\"target\":1,\"speed\":40,\"overload\":250,\"capacity\":500}");
        UiNode floodTree = NtosNetDos.Render(flood);
        Assert.Contains(floodTree.Walk(), n => n.Text == "CURRENT SPEED: 40 GQ/s");
        Assert.Equal(5, floodTree.Walk().Count(n => n.Key != null && n.Key.StartsWith("line:", StringComparison.Ordinal) && n.Text.Length == 45));
    }

    [Fact]
    public void TheArcadeShowsTheBossAndTheTurnButtons()
    {
        var (c, acts) = Context("NtosArcade", "{" + Header + ",\"Hitpoints\":40,\"PlayerHitpoints\":30,\"PlayerMP\":10,\"TicketCount\":1,\"GameActive\":1,"
            + "\"PauseState\":0,\"Status\":\"You attack for 5 damage.\",\"BossID\":\"boss3.gif\"}");
        UiNode tree = NtosArcade.Render(c);
        Assert.Equal("icons/ui/arcade/boss3.gif", tree.Walk().Single(n => n.Key == "bossimage").DmIcon);
        Assert.Equal("danger", tree.Walk().Single(n => n.Key == "status").Color);
        Assert.True(tree.Walk().Single(n => n.Key == "start").Disabled);
        Assert.Equal("good", tree.Walk().Single(n => n.Key == "earned").Color);
        tree.Walk().Single(n => n.Key == "attack").OnClick();
        tree.Walk().Single(n => n.Key == "heal").OnClick();
        Assert.Equal(new[] { ("Attack", (string)null), ("Heal", (string)null) }, acts);
    }
}
