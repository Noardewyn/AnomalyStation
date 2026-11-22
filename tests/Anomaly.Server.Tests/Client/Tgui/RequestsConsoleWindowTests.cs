using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps SC1 (the requests console): the client's RequestsConsole window (tg's RequestsConsole/*.tsx) shows the department, the
/// unread notice, the three emergency calls or the called emergency, the messages with their priority and authentication, writes
/// a message to a listed console and sends it, shows the announcement tab only to an announcing console, and is registered under
/// its name.
/// </summary>
public class RequestsConsoleWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json, TguiLocalState local)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "RequestsConsole", "rc", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, RequestsConsole.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Medbay = "{\"department\":\"Medbay\",\"can_send_announcements\":0,\"has_mail_send_error\":0,\"new_message_priority\":2,"
        + "\"silent\":0,\"emergency\":null,\"hack_state\":0,\"is_admin_ghost_ai\":0,"
        + "\"authentication_data\":{\"message_verified_by\":\"\",\"message_stamped_by\":\"\",\"announcement_authenticated\":0},"
        + "\"messages\":[{\"sender_department\":\"Bar\",\"received_time\":\"12:05\",\"content\":\"A patron &amp; his drink\",\"message_verified_by\":\"Bob (Bartender)\","
        + "\"message_stamped_by\":\"\",\"priority\":2,\"request_type\":\"Assistance Request\",\"appended_list\":{}}],"
        + "\"assistance_consoles\":[\"Security\",\"Bar\"],\"supply_consoles\":[\"Cargo Bay\"],\"information_consoles\":[\"Atmospherics\"],"
        + "\"reta_enabled\":1,\"reta_cooldowns\":{\"Security\":0,\"Engineering\":0,\"Medical\":0},\"reta_user_dept\":\"Medical\"}";

    [Fact]
    public void TheRequestsConsoleWindowShowsItsMessagesCallsEmergenciesAndWritesAMessage()
    {
        Assert.NotNull(TguiInterfaces.Find("RequestsConsole"));
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(Medbay, local);
        Assert.Equal((500.0, 600.0), (context.WindowWidth, context.WindowHeight));
        string text = tree.AllText();
        Assert.Contains("You have new unread PRIORITY messages", text);
        Assert.Contains("Call Security", text);
        Assert.Contains("Assistance Request from Bar, 12:05", text);
        Assert.Contains("A patron & his drink", text);
        Assert.Contains("High Priority", text);
        Assert.Contains("Bob (Bartender)", text);
        Assert.Contains("Not Stamped", text);
        Assert.DoesNotContain("Make Announcement", text); // not an announcing console

        ByKey(tree, "emergency:Security").OnClick();
        ByKey(tree, "quick_reply").OnClick();
        ByKey(tree, "toggle_silent").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("set_emergency", "{\"emergency\":\"Security\"}"),
            ("quick_reply", "{\"reply_recipient\":\"Bar\"}"),
            ("toggle_silent", null),
        }, acts);

        // the write tab: pick a recipient of the assistance list, type, send
        acts.Clear();
        ByKey(tree, "tab:write").OnClick();
        Assert.Equal(("clear_message_status", null), acts[0]);
        var (_, write, writeActs) = Render(Medbay, local);
        var dropdown = ByKey(write, "recipient:Assistance Request");
        Assert.Equal(new[] { "Bar", "Security" }, dropdown.Options);
        Assert.True(ByKey(write, "send_message").Disabled);
        dropdown.OnSelected("Security");
        ByKey(write, "message").OnInput("Need a hand");
        ByKey(write, "priority:2").OnClick();
        var (_, ready, readyActs) = Render(Medbay, local);
        Assert.False(ByKey(ready, "send_message").Disabled);
        Assert.DoesNotContain(ready.Walk(), n => n.Key == "priority:3"); // EXTREME only when hacked
        ByKey(ready, "send_message").OnClick();
        Assert.Equal(("send_message", "{\"message\":\"Need a hand\",\"recipient\":\"Security\",\"request_type\":\"Assistance Request\",\"priority\":2}"), readyActs.Single());

        // a called emergency replaces the buttons; an announcing console has its tab
        var (_, called, _) = Render(Medbay.Replace("\"emergency\":null", "\"emergency\":\"Security\"").Replace("\"can_send_announcements\":0", "\"can_send_announcements\":1"), new TguiLocalState());
        string calledText = called.AllText();
        Assert.Contains("Security called! RETA may open doors in area to them.", calledText);
        Assert.DoesNotContain("Call Engineering", calledText);
        Assert.Contains("Make Announcement", calledText);
    }
}
