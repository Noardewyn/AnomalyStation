using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps S4d (D327): the client's Mafia windows without the engine: MafiaPanel's lobby and game (the role with its pictures, the
/// chat, the players' actions, the judgement, the role list and the notes), NtosMafiaPanel, and the ghost's MinigamesMenu.
/// </summary>
public class MafiaPanelWindowTests
{
    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(string ui, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, ui, ui, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, acts);
    }

    private const string Lobby = "{\"phase\":\"No Game\",\"is_observer\":1,\"all_roles\":null,"
        + "\"lobbydata\":[{\"name\":\"alice\",\"status\":\"Ready\"},{\"name\":\"bob\",\"status\":\"Disconnected\"}]}";

    private const string Game = "{\"phase\":\"Judgment\",\"turn\":\" - Day 2\",\"timeleft\":250,\"is_observer\":0,\"person_voted_up_ref\":\"[0x2]\","
        + "\"player_voted_up\":0,\"user_notes\":\"bob is sus\",\"all_roles\":[\"Changeling x1\",\"Detective x1\"],"
        + "\"roleinfo\":{\"role\":\"Detective\",\"desc\":\"You can investigate a single person each night to learn their team.\",\"hud_icon\":\"huddetective\",\"revealed_icon\":\"detective\",\"role_dead\":0},"
        + "\"messages\":[{\"msg\":\"Bob: hello &amp; welcome\"},{\"msg\":\"Day 2 started!\"}],"
        + "\"players\":[{\"name\":\"Alice\",\"ref\":\"[0x1]\",\"alive\":1,\"role_revealed\":0,\"is_you\":1,\"possible_actions\":[]},"
        + "{\"name\":\"Bob\",\"ref\":\"[0x2]\",\"alive\":1,\"role_revealed\":0,\"is_you\":0,\"possible_actions\":[{\"name\":\"Vote\",\"ref\":\"[0x9]\"}]},"
        + "{\"name\":\"Carol\",\"ref\":\"[0x3]\",\"alive\":0,\"role_revealed\":\"Chaplain\",\"is_you\":0,\"possible_actions\":[]}]}";

    private const string Header = "\"PC_device_theme\":\"ntos\",\"PC_batteryicon\":null,\"PC_batterypercent\":null,\"PC_ntneticon\":\"sig_high.gif\","
        + "\"PC_stationtime\":\"00:12:30\",\"PC_stationdate\":\"Fri, October 23, 2566\",\"PC_programheaders\":[],\"PC_lowpower_mode\":0,\"PC_showexitprogram\":1";

    [Fact]
    public void TheLobbySignsUpVotesToStartAndCountsTheReadyPlayers()
    {
        var (c, acts) = Context("MafiaPanel", Lobby);
        UiNode tree = MafiaPanel.Render(c);
        Assert.Equal("The lobby currently has 1/12 valid players signed up.", tree.Walk().Single(n => n.Key == "count").Text);
        Assert.Contains(tree.Walk(), n => n.Key == "observer");
        Assert.Equal("red", tree.Walk().Single(n => n.Key == "lobbyist:1").Walk().Single(n => n.Key == "status").Color);
        Assert.Equal("alice", tree.Walk().Single(n => n.Key == "lobbyist:0").Walk().Single(n => n.Key == "name").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "admin-controls"); // not an admin
        tree.Walk().Single(n => n.Key == "signup").OnClick();
        tree.Walk().Single(n => n.Key == "vote-start").OnClick();
        Assert.Equal(new[] { ("mf_signup", (string)null), ("vote_to_start", (string)null) }, acts);
    }

    [Fact]
    public void TheGameShowsTheRoleTheChatThePlayersAndTheJudgement()
    {
        var (c, acts) = Context("MafiaPanel", Game);
        UiNode tree = MafiaPanel.Render(c);
        UiNode role = tree.Walk().Single(n => n.Key == "role" && n.Kind == UiKind.Section);
        Assert.Equal("Judgment - Day 2", role.Title);
        Assert.Equal("00:00:25", tree.Walk().Single(n => n.Key == "timeleft").Text);
        Assert.Equal(("icons/obj/mafia.dmi", "detective"), (tree.Walk().Single(n => n.Key == "revealed-icon").DmIcon, tree.Walk().Single(n => n.Key == "revealed-icon").DmIconState));
        Assert.Equal("huddetective", tree.Walk().Single(n => n.Key == "hud-icon").DmIconState);
        Assert.Equal("Bob: hello & welcome", tree.Walk().Single(n => n.Key == "msg:0").Text);
        Assert.Equal("Alice (YOU)", tree.Walk().Single(n => n.Key == "player:[0x1]").Walk().Single(n => n.Key == "name").Text);
        UiNode bob = tree.Walk().Single(n => n.Key == "player:[0x2]").Walk().Single(n => n.Key == "name");
        Assert.Equal("yellow", bob.Background); // on trial
        UiNode carol = tree.Walk().Single(n => n.Key == "player:[0x3]").Walk().Single(n => n.Key == "name");
        Assert.Equal(("Carol - Chaplain", "red"), (carol.Text, carol.Color));
        tree.Walk().Single(n => n.Key == "action:Vote").OnClick();
        Assert.Equal(("perform_action", "{\"action_ref\":\"[0x9]\",\"target\":\"[0x2]\"}"), acts.Last());
        tree.Walk().Single(n => n.Key == "guilty").OnClick();
        Assert.Equal(("vote_guilty", (string)null), acts.Last());

        tree.Walk().Single(n => n.Key == "chat-box").OnInput("I am the detective");
        MafiaPanel.Render(c).Walk().Single(n => n.Key == "send-chat").OnClick();
        Assert.Equal(("send_message_to_chat", "{\"message\":\"I am the detective\"}"), acts.Last());
        Assert.Equal("", MafiaPanel.Render(c).Walk().Single(n => n.Key == "chat-box").TextValue); // the box clears

        MafiaPanel.Render(c).Walk().Single(n => n.Key == "role:0").Walk().Single(n => n.Key == "help").OnClick();
        Assert.Equal(("mf_lookup", "{\"role_name\":\"Changeling\"}"), acts.Last());
    }

    [Fact]
    public void TheNotesSaveAndTheJudgementHidesForTheAccused()
    {
        var (c, acts) = Context("MafiaPanel", Game);
        MafiaPanel.Render(c).Walk().Single(n => n.Key == "tab:Notes").OnClick();
        UiNode tree = MafiaPanel.Render(c);
        Assert.Equal("bob is sus", tree.Walk().Single(n => n.Key == "notes-box").TextValue);
        tree.Walk().Single(n => n.Key == "notes-box").OnInput("carol is sus");
        MafiaPanel.Render(c).Walk().Single(n => n.Key == "save").OnClick();
        Assert.Equal(("change_notes", "{\"new_notes\":\"carol is sus\"}"), acts.Last());

        var (accused, _) = Context("MafiaPanel", Game.Replace("\"player_voted_up\":0", "\"player_voted_up\":1"));
        Assert.DoesNotContain(MafiaPanel.Render(accused).Walk(), n => n.Key == "judgement");
    }

    [Fact]
    public void ThePdaShowsThePanelAndTheGhostPicksMafia()
    {
        var (pda, _) = Context("NtosMafiaPanel", Lobby.Replace("{\"phase\"", "{" + Header + ",\"phase\""));
        Assert.Contains(NtosMafiaPanel.Render(pda).Walk(), n => n.Key == "signup");

        var (menu, acts) = Context("MinigamesMenu", "{}");
        UiNode tree = MinigamesMenu.Render(menu);
        Assert.Equal(4, tree.Walk().Count(n => n.Key != null && n.Key.StartsWith("play:", StringComparison.Ordinal)));
        tree.Walk().Single(n => n.Key == "play:mafia").OnClick();
        Assert.Equal(("mafia", (string)null), acts.Single());
    }
}
