using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MessageMonitor.tsx (M9 slice E: the message monitor
// console). Engine-free (compiled into the tests too). The Input's placeholder is its tooltip (as in AtmosHandbook).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MessageMonitor
{
    // tg: MessageMonitor.tsx:16  enum Screen
    private const int ScreenMain = 0;
    private const int ScreenMessageLogs = 1;
    private const int ScreenRequestLogs = 2;
    private const int ScreenHacked = 3;

    private static UiNode Home(TguiContext c) => new UiNode(UiKind.Button) { Key = "home", Text = "Main Menu", Icon = "home", OnClick = () => c.Act("return_home") };

    private static UiNode Cell(string text) => new UiNode(UiKind.TableCell) { Text = text };

    private static UiNode Header(params string[] cells) => new UiNode(UiKind.TableRow) { Key = "head" }.With(cells.Select(t => new UiNode(UiKind.TableCell) { Bold = true, Text = t }));

    // tg: MessageMonitor.tsx:55
    private static UiNode RequestLogs(TguiContext c)
    {
        IReadOnlyList<UiValue> requests = c.Data.Get("requests")?.Items ?? Array.Empty<UiValue>();
        return new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = "Requests", Buttons = Home(c) }.With(new UiNode(UiKind.Table).With(
            new[] { Header("Delete", "Message", "Stamp", "Department", "Authentication") }.Concat(requests.Select(request => new UiNode(UiKind.TableRow) { Key = "request:" + request.Str("ref"), Candystripe = true }.With(
                new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Button) { Key = "delete", Icon = "trash", Color = "red", OnClick = () => c.Act("delete_request", ("ref", request.Str("ref"))) }),
                Cell(request.Str("message")), Cell(request.Str("stamp")), Cell(request.Str("sender_department")), Cell(request.Str("id_auth")))))));
    }

    // tg: MessageMonitor.tsx:101
    private static UiNode MessageLogs(TguiContext c)
    {
        IReadOnlyList<UiValue> messages = c.Data.Get("messages")?.Items ?? Array.Empty<UiValue>();
        return new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = "Stored Messages", Buttons = Home(c) }.With(new UiNode(UiKind.Table).With(
            new[] { Header("Delete", "Sender", "Recipient", "Message") }.Concat(messages.Select(message => new UiNode(UiKind.TableRow) { Key = "message:" + message.Str("ref"), Candystripe = true }.With(
                new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Button) { Key = "delete", Icon = "trash", Color = "red", OnClick = () => c.Act("delete_message", ("ref", message.Str("ref"))) }),
                Cell(message.Str("sender")), Cell(message.Str("recipient")), Cell(TguiFormat.DecodeHtmlEntities(message.Str("message"))))))));
    }

    // tg: MessageMonitor.tsx:147
    private static UiNode Hacked() => new UiNode(UiKind.Stack) { Vertical = true, Fill = true, Align = "center" }.With(
        new UiNode(UiKind.StackItem) { Grow = 1 },
        new UiNode(UiKind.Box) { Color = "red", FontSize = "18px", Bold = true, Text = "E%*#OR: CRIT#%^CAL Ss&@STEM Fai++~|URE..." },
        new UiNode(UiKind.Box) { Color = "red", FontSize = "18px", Bold = true, Text = "EN*bLING u&*E REB-00T ProT/.\\;L..." });

    private static UiNode Option(TguiContext c, UiNode button, string description) => new UiNode(UiKind.TableRow) { Key = "option:" + button.Key }.With(
        new UiNode(UiKind.TableCell).With(button), Cell(description));

    // tg: MessageMonitor.tsx:166  MainScreenAuth and :270 MainScreenNotAuth
    private static UiNode MainScreen(TguiContext c)
    {
        UiValue data = c.Data;
        bool auth = data.Truthy("auth");
        bool status = data.Truthy("status");
        bool isMalf = data.Truthy("is_malf");
        string password = c.UseState("authPassword", data.Str("password") ?? "");
        var bar = new List<UiNode>
        {
            new UiNode(UiKind.Input) { Key = "authPassword", TextValue = password, Tooltip = "Password", OnInput = v => c.Local.SetQuiet("authPassword", v) },
            new UiNode(UiKind.Button) { Key = "auth", Text = auth ? "Logout" : "Auth", OnClick = () => c.Act("auth", ("auth_password", c.UseState("authPassword", ""))) },
            new UiNode(UiKind.Button) { Key = "status", Icon = status ? "power-off" : "times", Text = status ? "ON" : "OFF", Color = status ? "green" : "red", Disabled = !auth, OnClick = () => c.Act("turn_server") },
        };
        if (isMalf)
            bar.Add(new UiNode(UiKind.Button) { Key = "hack", Icon = auth ? "terminal" : null, Text = "Hack", Color = "red", Disabled = auth, OnClick = () => c.Act("hack") });

        UiNode options;
        if (auth)
        {
            var rows = new List<UiNode>
            {
                Header("Option", "Description"),
                Option(c, new UiNode(UiKind.Button) { Key = "viewmessages", Text = "View Message Logs", OnClick = () => c.Act("view_message_logs") }, "Shows all messages that have been sent"),
                Option(c, new UiNode(UiKind.Button) { Key = "viewrequests", Text = "View Request Console Logs", OnClick = () => c.Act("view_request_logs") }, "Shows all orders that were made in the cargo department"),
                Option(c, c.ButtonConfirm("clearmessages", new UiNode(UiKind.Button) { Text = "Clear Message Logs", OnClick = () => c.Act("clear_message_logs") }), "Clears message logs"),
                Option(c, c.ButtonConfirm("clearrequests", new UiNode(UiKind.Button) { Text = "Clear Request Console Logs", OnClick = () => c.Act("clear_request_logs") }), "Clears request console logs"),
            };
            if (data.Truthy("is_on_station"))
                rows.Add(Option(c, new UiNode(UiKind.Button) { Key = "setkey", Text = "Set Custom Key", OnClick = () => c.Act("set_key") }, "Changes decryption key"));
            rows.Add(Option(c, new UiNode(UiKind.Button) { Key = "fakemessage", Text = "Send Admin Message", OnClick = () => c.Act("send_fake_message") }, "Sends a custom message to the user's PDA"));
            options = new UiNode(UiKind.Table).With(rows);
        }
        else
        {
            options = new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = "Choose Option" }.With(new UiNode(UiKind.Table).With(
                Header("Option", "Description"),
                Option(c, new UiNode(UiKind.Button) { Key = "link", Text = "Link Server", OnClick = () => c.Act("link_server") }, "Connects to the server")));
        }
        return new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section).With(new UiNode(UiKind.Stack).With(bar.Select(b => new UiNode(UiKind.StackItem).With(b))))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(options));
    }

    // tg: MessageMonitor.tsx:342
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int screen = (int)data.Num("screen");
        string error = data.Str("error_message");
        string success = data.Str("success_message");
        string notice = data.Str("notice_message");

        var items = new List<UiNode>();
        if (data.Truthy("server_status"))
        {
            if (!string.IsNullOrEmpty(error))
                items.Add(new UiNode(UiKind.NoticeBox) { Key = "error", Color = "red", Text = error });
            if (!string.IsNullOrEmpty(success))
                items.Add(new UiNode(UiKind.NoticeBox) { Key = "success", Color = "green", Text = success });
            items.Add(new UiNode(UiKind.StackItem) { Key = "screen", Grow = 1 }.With(screen switch
            {
                ScreenMain => MainScreen(c),
                ScreenMessageLogs => MessageLogs(c),
                ScreenRequestLogs => RequestLogs(c),
                ScreenHacked => Hacked(),
                _ => null,
            }));
            if (!string.IsNullOrEmpty(notice))
                items.Add(new UiNode(UiKind.NoticeBox) { Key = "notice", Color = "yellow", Text = notice });
            items.Add(new UiNode(UiKind.Box) { Key = "reg514", Text = "Reg. #514 forbids sending messages to a Head of Staff containing Erotic Rendering Properties." });
        }
        else
        {
            items.Add(new UiNode(UiKind.NoticeBox) { Key = "noserver", Color = "red", Text = "Server not found, click button to scan the network" });
            items.Add(new UiNode(UiKind.Button) { Key = "connect", Text = "Connect to server", OnClick = () => c.Act("connect_server") });
        }
        return c.Window(700, 400, null, new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(items));
    }
}
