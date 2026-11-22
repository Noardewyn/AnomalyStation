using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RequestsConsole/ (index.tsx, RequestsConsoleHeader.tsx,
// RequestsConsoleMainScreen.tsx, MessageViewTab.tsx, MessageWriteTab.tsx, AnnouncementTab.tsx, types.ts) (gaps SC1). The window reads
// tg's ui_data and ui_static_data of the requests console (machinery/requests_console.dm:390, :425) and sends its acts; the tab and the
// unsent message and announcement are the window's local state (tg's shared and local state). tgui-core's BlockQuote is a box.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class RequestsConsole
{
    // tg: types.ts:36-58
    private const string Assistance = "Assistance Request";
    private const string Supplies = "Supplies Request";
    private const string Information = "Relay Information";
    private const string OreUpdate = "Ore Update";
    private const int Normal = 1, High = 2, Extreme = 3;
    private const int MessageView = 1, MessageWrite = 2, Announce = 3;

    // tg: index.tsx:9
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: index.tsx:13  <Window title={`${department} Requests Console`} width={500} height={600}>
        return c.Window(500, 600, data.Str("department") + " Requests Console",
            new UiNode(UiKind.Stack) { Key = "content", Vertical = true, Fill = true }.With(
                Header(c, data),
                MainScreen(c, data),
                // tg: index.tsx:33  the footer's speaker checkbox
                new UiNode(UiKind.StackItem) { Key = "footer" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "toggle_silent",
                        Fluid = true,
                        Checkbox = !data.Truthy("silent"),
                        Text = "Speaker",
                        OnClick = () => c.Act("toggle_silent"),
                    })));
    }

    // tg: RequestsConsoleHeader.tsx:6
    private static UiNode Header(TguiContext c, UiValue data)
    {
        int priority = (int)data.Num("new_message_priority");
        string emergency = data.Str("emergency");
        UiNode emergencyBox = !string.IsNullOrEmpty(emergency)
            ? new UiNode(UiKind.NoticeBox) { Key = "emergency", Color = "danger", Text = emergency + " called! RETA may open doors in area to them." }
            : new UiNode(UiKind.Stack) { Key = "emergencies", Fill = true }.With(
                Emergency(c, "Security", "shield", "Call Security"),
                Emergency(c, "Engineering", "screwdriver-wrench", "Call Engineering"),
                Emergency(c, "Medical", "suitcase-medical", "Call Medical"));
        return new UiNode(UiKind.StackItem) { Key = "header" }.With(
            data.Truthy("has_mail_send_error") ? new UiNode(UiKind.NoticeBox) { Key = "error", Color = "danger", Text = "Error occured while sending a message!" } : null,
            priority != 0
                ? new UiNode(UiKind.NoticeBox)
                {
                    Key = "unread",
                    Text = "You have new unread " + (priority == High ? "PRIORITY " : "") + (priority == Extreme ? "EXTREME PRIORITY " : "") + "messages",
                }
                : null,
            emergencyBox);
    }

    private static UiNode Emergency(TguiContext c, string emergency, string icon, string text) =>
        new UiNode(UiKind.StackItem) { Key = emergency, Grow = 1 }.With(new UiNode(UiKind.Button)
        {
            Key = "emergency:" + emergency,
            Fluid = true,
            Color = "red",
            Icon = icon,
            Text = text,
            OnClick = () => c.Act("set_emergency", ("emergency", emergency)),
        });

    // tg: RequestsConsoleMainScreen.tsx:9
    private static UiNode MainScreen(TguiContext c, UiValue data)
    {
        bool canAnnounce = data.Truthy("can_send_announcements");
        int tab = c.UseState("tab", MessageView);
        UiNode body = tab == MessageView ? MessageViewTab(c, data)
            : tab == MessageWrite ? MessageWriteTab(c, data)
            : canAnnounce && tab == Announce ? AnnouncementTab(c, data)
            : null;
        return new UiNode(UiKind.StackItem) { Key = "main", Grow = 1 }.With(
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "tabs" }.With(new UiNode(UiKind.Tabs) { TextAlign = "center", Fluid = true }.With(
                    new UiNode(UiKind.Tab)
                    {
                        Key = "tab:view",
                        Text = "View Messages",
                        Icon = "envelope-open",
                        Selected = tab == MessageView,
                        OnClick = () =>
                        {
                            c.SetState("tab", MessageView);
                            c.Act("clear_message_status");
                            c.Act("clear_authentication");
                        },
                    },
                    new UiNode(UiKind.Tab)
                    {
                        Key = "tab:write",
                        Text = "Write Message",
                        Icon = "pencil",
                        Selected = tab == MessageWrite,
                        OnClick = () =>
                        {
                            if (tab == MessageView)
                                c.Act("clear_message_status");
                            else if (tab != MessageWrite)
                                c.Act("clear_authentication");
                            c.SetState("tab", MessageWrite);
                        },
                    },
                    canAnnounce
                        ? new UiNode(UiKind.Tab)
                        {
                            Key = "tab:announce",
                            Text = "Make Announcement",
                            Icon = "bullhorn",
                            Selected = tab == Announce,
                            OnClick = () =>
                            {
                                if (tab == MessageView)
                                    c.Act("clear_message_status");
                                else if (tab != Announce)
                                    c.Act("clear_authentication");
                                c.SetState("tab", Announce);
                            },
                        }
                        : null)),
                new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(body)));
    }

    // tg: MessageViewTab.tsx:19
    private static UiNode MessageViewTab(TguiContext c, UiValue data)
    {
        IReadOnlyList<UiValue> messages = data.Get("messages")?.Items ?? Array.Empty<UiValue>();
        return new UiNode(UiKind.Section) { Key = "messages", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Stack) { Vertical = true }.With(messages.Select((m, i) => MessageDisplay(c, m, i))));
    }

    // tg: MessageViewTab.tsx:33
    private static UiNode MessageDisplay(TguiContext c, UiValue message, int index)
    {
        int priority = (int)message.Num("priority");
        string requestType = message.Str("request_type");
        UiValue appended = message.Get("appended_list");
        IReadOnlyList<KeyValuePair<string, UiValue>> appendedEntries = appended?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>();
        return new UiNode(UiKind.StackItem) { Key = "message:" + index }.With(
            new UiNode(UiKind.Section) { Title = requestType + " from " + message.Str("sender_department") + ", " + message.Str("received_time") }.With(
                priority == High ? new UiNode(UiKind.NoticeBox) { Key = "high", Text = "High Priority" } : null,
                priority == Extreme ? new UiNode(UiKind.NoticeBox) { Key = "extreme", Color = "danger", Text = "!!!Extreme Priority!!!" } : null,
                new UiNode(UiKind.Box) { Key = "content" }.With(
                    new UiNode(UiKind.Box) { Key = "text", Text = TguiFormat.DecodeHtmlEntities(message.Str("content")) },
                    appendedEntries.Count > 0
                        ? new UiNode(UiKind.LabeledList) { Key = "appended" }.With(appendedEntries.Select(kv => new UiNode(UiKind.LabeledItem)
                        {
                            Key = kv.Key,
                            Label = kv.Key,
                            Text = kv.Value.AsStr(),
                        }))
                        : null),
                new UiNode(UiKind.LabeledList) { Key = "auth" }.With(
                    new UiNode(UiKind.LabeledItem) { Label = "Message Verified By", Text = NonEmpty(message.Str("message_verified_by"), "Not Verified") },
                    new UiNode(UiKind.LabeledItem) { Label = "Message Stamped By", Text = NonEmpty(message.Str("message_stamped_by"), "Not Stamped") }),
                requestType != OreUpdate
                    ? new UiNode(UiKind.Section) { Key = "reply" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "quick_reply",
                        Icon = "reply",
                        Text = "Quick Reply",
                        OnClick = () => c.Act("quick_reply", ("reply_recipient", message.Str("sender_department"))),
                    })
                    : null));
    }

    private static string NonEmpty(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    // tg: MessageWriteTab.tsx:14
    private static UiNode MessageWriteTab(TguiContext c, UiValue data)
    {
        UiValue auth = data.Get("authentication_data");
        bool hacked = data.Truthy("hack_state");
        string messageText = c.UseState("messageText", "");
        string requestType = c.UseState("requestType", Assistance);
        int priority = c.UseState("priority", Normal);
        string recipient = c.UseState("recipient", "");
        void ResetMessage()
        {
            c.SetState("messageText", "");
            c.SetState("recipient", "");
            c.SetState("priority", Normal);
            c.SetState("requestType", Assistance);
        }
        UiNode TypeButton(string type, string icon, string text) => new UiNode(UiKind.StackItem) { Key = type, Grow = 1 }.With(new UiNode(UiKind.Button)
        {
            Key = "type:" + type,
            Fluid = true,
            Icon = icon,
            Selected = requestType == type,
            Text = text,
            OnClick = () =>
            {
                c.SetState("recipient", "");
                c.SetState("requestType", type);
            },
        });
        UiNode PriorityButton(int value, string icon, string text) => new UiNode(UiKind.StackItem) { Key = "priority" + value, Grow = 1 }.With(new UiNode(UiKind.Button)
        {
            Key = "priority:" + value,
            Fluid = true,
            Icon = icon,
            Text = text,
            Selected = priority == value,
            OnClick = () => c.SetState("priority", value),
        });
        string listKey = requestType == Assistance ? "assistance_consoles" : requestType == Supplies ? "supply_consoles" : "information_consoles";
        List<string> recipients = (data.Get(listKey)?.Items ?? Array.Empty<UiValue>()).Select(v => v.AsStr()).OrderBy(s => s, StringComparer.Ordinal).ToList();
        bool canSend = !string.IsNullOrEmpty(messageText) && !string.IsNullOrEmpty(recipient);
        return new UiNode(UiKind.Section) { Key = "write" }.With(
            new UiNode(UiKind.Stack) { Key = "types", Fill = true }.With(
                TypeButton(Assistance, "handshake-angle", "Request Assistance"),
                TypeButton(Supplies, "boxes-stacked", "Request Supplies"),
                TypeButton(Information, "upload", "Relay Information")),
            new UiNode(UiKind.Box) { Key = "recipient" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "recipient:" + requestType,
                Width = "100%",
                Options = recipients,
                TextValue = recipient,
                Tooltip = "Pick a Recipient",
                OnSelected = value => c.SetState("recipient", value),
            }),
            new UiNode(UiKind.Stack) { Key = "priorities", Fill = true }.With(
                PriorityButton(Normal, "envelope", "Normal Priority"),
                PriorityButton(High, "exclamation", "High Priority"),
                hacked ? PriorityButton(Extreme, "burst", "EXTREME PRIORITY") : null),
            new UiNode(UiKind.TextArea)
            {
                Key = "message",
                Fluid = true,
                Wrap = true,
                Height = "20em",
                MaxLength = 1025,
                TextValue = messageText,
                Tooltip = "Type your message...",
                OnInput = value => c.SetState("messageText", value),
            },
            new UiNode(UiKind.Section) { Key = "send" }.With(
                new UiNode(UiKind.Stack) { Fill = true, Justify = "space-between" }.With(
                    new UiNode(UiKind.StackItem) { Key = "send" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "send_message",
                        Icon = "paper-plane",
                        Text = "Send message",
                        Disabled = !canSend,
                        OnClick = () =>
                        {
                            if (!canSend)
                                return;
                            c.Act("send_message", ("message", messageText), ("recipient", recipient), ("request_type", requestType), ("priority", priority));
                            ResetMessage();
                        },
                    }),
                    new UiNode(UiKind.StackItem) { Key = "auth" }.With(
                        new UiNode(UiKind.Button) { Key = "verify_id", Icon = "id-card", Text = NonEmpty(auth?.Str("message_verified_by"), "Not verified"), OnClick = () => c.Act("verify_id") },
                        new UiNode(UiKind.Button) { Key = "stamp", Icon = "stamp", Text = NonEmpty(auth?.Str("message_stamped_by"), "Not stamped"), OnClick = () => c.Act("stamp") })),
                new UiNode(UiKind.Button)
                {
                    Key = "discard",
                    Icon = "trash-can",
                    Text = "Discard message",
                    OnClick = () =>
                    {
                        c.Act("clear_authentication");
                        ResetMessage();
                    },
                }));
    }

    // tg: AnnouncementTab.tsx:7
    private static UiNode AnnouncementTab(TguiContext c, UiValue data)
    {
        UiValue auth = data.Get("authentication_data");
        bool authenticated = (auth?.Truthy("announcement_authenticated") ?? false) || data.Truthy("is_admin_ghost_ai");
        string messageText = c.UseState("announcementText", "");
        return new UiNode(UiKind.Section) { Key = "announce" }.With(
            new UiNode(UiKind.TextArea)
            {
                Key = "announcement",
                Fluid = true,
                Wrap = true,
                Height = "20em",
                MaxLength = 1025,
                TextValue = messageText,
                Tooltip = "Type your announcement...",
                OnInput = value => c.SetState("announcementText", value),
            },
            new UiNode(UiKind.Section) { Key = "send" }.With(
                // tg: AnnouncementTab.tsx:62
                !authenticated
                    ? new UiNode(UiKind.NoticeBox) { Key = "auth", Text = "Swipe your card to authenticate yourself" }
                    : new UiNode(UiKind.NoticeBox) { Key = "auth", Color = "info", Text = "Successfully authenticated" },
                new UiNode(UiKind.Button)
                {
                    Key = "send_announcement",
                    Icon = "bullhorn",
                    Text = "Send announcement",
                    Disabled = !authenticated || string.IsNullOrEmpty(messageText),
                    OnClick = () =>
                    {
                        if (!authenticated || string.IsNullOrEmpty(messageText))
                            return;
                        c.Act("send_announcement", ("message", messageText));
                        c.SetState("announcementText", "");
                    },
                },
                new UiNode(UiKind.Button)
                {
                    Key = "discard",
                    Icon = "trash-can",
                    Text = "Discard announcement",
                    OnClick = () =>
                    {
                        c.Act("clear_authentication");
                        c.SetState("announcementText", "");
                    },
                }));
    }
}
