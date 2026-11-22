using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosMessenger/index.tsx and ChatScreen.tsx (M9 slice E: the
// PDA messenger). Engine-free (compiled into the tests too). The chat's bubbles are boxes on the message's side (tg's
// NtosChatMessage styles); the unread messages are marked read when the chat is shown (tg after a second's timer); a chat's draft
// is saved when it is left by its Back button (tg when the screen unmounts). The photos are not ported (the camera): no photo
// button picks one. The Input's placeholder is its tooltip (as in AtmosHandbook).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosMessenger
{
    // tg: NtosMessenger/index.tsx:43
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue savedChats = data.Get("saved_chats");
        UiValue messengers = data.Get("messengers");
        UiValue openChatRef = data.Get("open_chat");
        string openChat = openChatRef is { Kind: UiValueKind.String } ? openChatRef.Text : null;

        UiNode content;
        if (data.Truthy("remote_silicon"))
            content = AccessDenied();
        else if (openChat != null)
        {
            UiValue chat = savedChats?.Get(openChat);
            UiValue temporaryRecipient = messengers?.Get(openChat);
            if (chat == null && temporaryRecipient == null)
                content = Contacts(c);
            else
                content = Chat(c, chat, chat != null ? chat.Get("recipient") : temporaryRecipient);
        }
        else
            content = Contacts(c);

        return NtosWindow.Render(c, 600, 850, null, content);
    }

    // tg: NtosMessenger/index.tsx:93
    private static UiNode AccessDenied() => new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section).With(new UiNode(UiKind.Box) { Key = "title", Bold = true, TextAlign = "center", Text = "SpaceMessenger V6.5.3" })),
        new UiNode(UiKind.NoticeBox) { Key = "refused", Color = "white", Text = "ERROR: CONNECTION REFUSED" },
        new UiNode(UiKind.Section).With(
            new UiNode(UiKind.Box) { Text = "Message from host:" },
            new UiNode(UiKind.Box) { Text = "- Remote access of this application has been restricted." },
            new UiNode(UiKind.Box) { Text = "- Contact your Administrator for further assistance." }));

    private static string Name(UiValue messenger) => messenger.Str("name") + " (" + messenger.Str("job") + ")";

    // tg: NtosMessenger/index.tsx:131  ChatButton
    private static UiNode ChatButton(TguiContext c, string name, string chatRef, int unreads)
    {
        bool hasUnreads = unreads > 0;
        string prefix = hasUnreads ? "[" + (unreads <= 9 ? unreads.ToString(System.Globalization.CultureInfo.InvariantCulture) : "9+") + " unread message" + (unreads != 1 ? "s" : "") + "] " : " ";
        return new UiNode(UiKind.StackItem) { Key = "chat:" + chatRef }.With(new UiNode(UiKind.Button)
        {
            Key = "chat",
            Icon = hasUnreads ? "envelope" : null,
            Fluid = true,
            Text = prefix + name,
            OnClick = () => c.Act("PDA_viewMessages", ("ref", chatRef)),
        });
    }

    // tg: NtosMessenger/index.tsx:125  ContactsScreen
    private static UiNode Contacts(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue owner = data.Get("owner");
        bool alertSilenced = data.Truthy("alert_silenced");
        bool alertAble = data.Truthy("alert_able");
        bool sendingAndReceiving = data.Truthy("sending_and_receiving");
        bool sortByJob = data.Truthy("sort_by_job");
        bool canSpam = data.Truthy("can_spam");
        bool isSilicon = data.Truthy("is_silicon");
        bool virusAttach = data.Truthy("virus_attach");
        bool sendingVirus = data.Truthy("sending_virus");
        string searchUser = c.UseState("searchUser", "");

        var chats = (data.Get("saved_chats")?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>()).Select(e => e.Value).ToList();
        var messengers = data.Get("messengers")?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>();

        Func<UiValue, bool> searchChat = TguiFormat.CreateSearch<UiValue>(searchUser, chat => chat.Get("recipient").Str("name") + chat.Get("recipient").Str("job"));
        Func<UiValue, bool> searchMessenger = TguiFormat.CreateSearch<UiValue>(searchUser, m => m.Str("name") + m.Str("job"));

        // sortBy(chats, unread_messages): ascending, stable
        var openChats = chats.OrderBy(chat => chat.Num("unread_messages")).Where(searchChat).ToList();
        var filteredChatButtons = openChats.Where(chat => chat.Truthy("visible"))
            .Select(chat => ChatButton(c, Name(chat.Get("recipient")), chat.Str("ref"), (int)chat.Num("unread_messages"))).ToList();
        var messengerButtons = messengers
            .Where(entry => openChats.All(chat => chat.Get("recipient").Str("ref") != entry.Key) && searchMessenger(entry.Value))
            .Select(entry => ChatButton(c, Name(entry.Value), entry.Value.Str("ref"), 0))
            .Concat(openChats.Where(chat => !chat.Truthy("visible")).Select(chat => ChatButton(c, Name(chat.Get("recipient")), chat.Str("ref"), (int)chat.Num("unread_messages"))))
            .ToList();
        bool noId = (owner == null || owner.IsNull) && !isSilicon;

        var toolbar = new List<UiNode>
        {
            new UiNode(UiKind.Button) { Key = "ringer", Icon = "bell", Disabled = !alertAble, Text = alertAble && !alertSilenced ? "Ringer: On" : "Ringer: Off", OnClick = () => c.Act("PDA_toggleAlerts") },
            new UiNode(UiKind.Button) { Key = "sendreceive", Icon = "address-card", Text = sendingAndReceiving ? "Send / Receive: On" : "Send / Receive: Off", OnClick = () => c.Act("PDA_toggleSendingAndReceiving") },
            new UiNode(UiKind.Button) { Key = "ringtone", Icon = "bell", Text = "Set Ringtone", OnClick = () => c.Act("PDA_ringSet") },
            new UiNode(UiKind.Button) { Key = "sort", Icon = "sort", Text = "Sort by: " + (sortByJob ? "Job" : "Name"), OnClick = () => c.Act("PDA_changeSortStyle") },
        };
        if (virusAttach)
            toolbar.Add(new UiNode(UiKind.Button) { Key = "virus", Icon = "bug", Color = "bad", Text = "Attach Virus: " + (sendingVirus ? "Yes" : "No"), OnClick = () => c.Act("PDA_toggleVirus") });

        var items = new List<UiNode>
        {
            new UiNode(UiKind.StackItem) { Key = "head" }.With(new UiNode(UiKind.Section).With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "title", Bold = true, TextAlign = "center", Icon = "address-card", Text = "SpaceMessenger V6.5.4" },
                    new UiNode(UiKind.StackItem) { Key = "motto", Opacity = 0.3, TextAlign = "center", Text = "Bringing you spy-proof communications since 2467." },
                    new UiNode(UiKind.StackItem) { Key = "toolbar" }.With(new UiNode(UiKind.Box) { Wrap = true }.With(toolbar))),
                new UiNode(UiKind.Stack) { Justify = "space-between", Align = "center" }.With(
                    new UiNode(UiKind.StackItem) { Key = "searchlabel", Icon = "magnifying-glass", Text = "Search For User" },
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
                    {
                        Key = "searchUser",
                        Width = "220px",
                        Tooltip = "Search by name or job...",
                        TextValue = searchUser,
                        OnInput = v => c.SetState("searchUser", v),
                    })))),
        };
        if (filteredChatButtons.Count > 0)
        {
            items.Add(new UiNode(UiKind.StackItem) { Key = "previous", Grow = 1 }.With(new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.Section).With(new UiNode(UiKind.Box) { Key = "previoustitle", Icon = "comments", Text = "Previous Messages" }),
                new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(filteredChatButtons)))));
        }
        items.Add(new UiNode(UiKind.StackItem) { Key = "detected", Grow = 2 }.With(new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.Section).With(new UiNode(UiKind.Box) { Key = "detectedtitle", Icon = "address-card", Text = "Detected Messengers" }),
            new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                messengerButtons.Count == 0
                    ? new[] { new UiNode(UiKind.StackItem) { Key = "nousers", Color = "gray", Icon = "user-slash", Text = "No users found." } }
                    : messengerButtons.ToArray())))));
        if (canSpam)
            items.Add(new UiNode(UiKind.StackItem) { Key = "everyone" }.With(SendToAll(c)));

        var screen = new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(items);
        if (!noId)
            return screen;
        // tg: NtosMessenger/index.tsx:376  NoIDDimmer
        return new UiNode(UiKind.Fragment).With(screen, new UiNode(UiKind.Modal) { Key = "noid" }.With(
            new UiNode(UiKind.Box) { Key = "noidicon", Color = "red", Icon = "address-card", FontSize = "48px" },
            new UiNode(UiKind.Box) { Key = "noidtext", FontSize = "18px", Text = "Please imprint an ID to continue." }));
    }

    // tg: NtosMessenger/index.tsx:330  SendToAllSection
    private static UiNode SendToAll(TguiContext c)
    {
        bool onCooldown = c.Data.Truthy("on_spam_cooldown");
        string message = c.UseState("everyoneMessage", "");
        void Send()
        {
            c.Act("PDA_sendEveryone", ("message", c.UseState("everyoneMessage", "")));
            c.SetState("everyoneMessage", "");
        }
        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Section).With(new UiNode(UiKind.Stack) { Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Key = "everyonetitle", Icon = "satellite-dish", Text = "Send To All" },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "sendeveryone",
                    Icon = "arrow-right",
                    Text = "Send",
                    Disabled = onCooldown || message == "",
                    Tooltip = onCooldown ? "Wait before sending more messages!" : null,
                    OnClick = Send,
                }))),
            new UiNode(UiKind.Section).With(new UiNode(UiKind.TextArea)
            {
                Key = "everyoneMessage",
                Height = 6,
                TextValue = message,
                Tooltip = "Send message to everyone...",
                OnInput = v => c.SetState("everyoneMessage", v),
                OnEnter = Send,
            }));
    }

    // tg: NtosMessenger/ChatScreen.tsx:179  ChatScreen.render
    private static UiNode Chat(TguiContext c, UiValue chat, UiValue recipient)
    {
        bool canReply = chat != null ? chat.Truthy("can_reply") : recipient != null;
        IReadOnlyList<UiValue> messages = chat?.Get("messages")?.Items ?? Array.Empty<UiValue>();
        string chatRef = chat?.Str("ref");
        int unreads = chat != null ? (int)chat.Num("unread_messages") : 0;
        bool sendingVirus = c.Data.Truthy("sending_virus");
        string messageKey = "message:" + (chatRef ?? recipient.Str("ref"));
        string message = c.UseState(messageKey, chat?.Str("message_draft") ?? "");

        // tg's componentDidMount timer (READ_UNREADS_TIME_MS): the chat's unreads are read once it is shown
        string readKey = "read:" + chatRef + ":" + messages.Count;
        if (chatRef != null && unreads > 0 && !c.UseState(readKey, false))
        {
            c.Local.SetQuiet(readKey, true);
            c.Act("PDA_clearUnreads", ("ref", chatRef));
        }

        void SendMessage()
        {
            string text = c.UseState(messageKey, "");
            if (text == "")
                return;
            c.Act("PDA_sendMessage", ("ref", chatRef ?? recipient.Str("ref")), ("message", text));
            c.SetState(messageKey, "");
        }

        var log = new List<UiNode>();
        if (messages.Count > 0 && canReply)
        {
            log.Add(new UiNode(UiKind.StackItem) { Key = "beginning", TextAlign = "center", Text = "This is the beginning of your chat with " + recipient.Str("name") + "." });
            log.Add(new UiNode(UiKind.Divider) { Key = "beginningrule" });
        }
        for (int index = 0; index < messages.Count; index++)
        {
            UiValue entry = messages[index];
            bool outgoing = entry.Truthy("outgoing");
            if (index == messages.Count - unreads)
                log.Add(new UiNode(UiKind.Divider) { Key = "unread", Rule = true, Text = "Unread Messages" }); // tg: ChatDivider
            var bubble = new List<UiNode>
            {
                new UiNode(UiKind.Box) { Key = "text", Wrap = true, Text = TguiFormat.DecodeHtmlEntities(entry.Str("message")), Tooltip = entry.Str("timestamp") },
            };
            if (entry.Truthy("everyone"))
                bubble.Add(new UiNode(UiKind.Box) { Key = "everyone", Opacity = 0.5, Text = "Sent to everyone" });
            log.Add(new UiNode(UiKind.StackItem) { Key = "msg:" + index }.With(new UiNode(UiKind.Stack) { Justify = outgoing ? "flex-end" : "flex-start" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = outgoing ? "outgoing" : "incoming", Background = outgoing ? "#2c5591" : "#3a3a3a" }.With(bubble)))));
        }

        UiNode sendingBar;
        if (!canReply)
            sendingBar = new UiNode(UiKind.Section) { Fill = true }.With(new UiNode(UiKind.Box) { Key = "noreply", Color = "gray", Text = "You cannot reply to this user." });
        else
        {
            UiNode attachment = sendingVirus
                ? new UiNode(UiKind.Button) { Key = "virusattached", Tooltip = "ERROR: File signature is unverified. Please contact an NT support intern.", Icon = "triangle-exclamation", Color = "red" }
                : new UiNode(UiKind.Button) { Key = "attach", Tooltip = "Add attachment", Icon = "image", Disabled = true }; // the photos (the camera)
            sendingBar = new UiNode(UiKind.Section) { Fill = true }.With(new UiNode(UiKind.Stack) { Fill = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Input)
                {
                    Key = "chatInput",
                    Tooltip = "Send message to " + recipient.Str("name") + "...",
                    Fluid = true,
                    AutoFocus = true,
                    TextValue = message,
                    MaxLength = 1024,
                    OnInput = v => c.SetState(messageKey, v),
                    OnEnter = SendMessage,
                }),
                new UiNode(UiKind.StackItem).With(attachment),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "send", Tooltip = "Send", Icon = "arrow-right", OnClick = SendMessage })));
        }

        var top = new List<UiNode>
        {
            new UiNode(UiKind.Button)
            {
                Key = "back",
                Icon = "arrow-left",
                Text = "Back",
                OnClick = () =>
                {
                    if (chatRef != null)
                        c.Act("PDA_saveMessageDraft", ("ref", chatRef), ("message", c.UseState(messageKey, "")));
                    c.Act("PDA_viewMessages", ("ref", null));
                },
            },
        };
        if (chatRef != null)
        {
            top.Add(new UiNode(UiKind.Button) { Key = "closechat", Icon = "box-archive", Text = "Close chat", OnClick = () => c.Act("PDA_closeMessages", ("ref", chatRef)) });
            top.Add(c.ButtonConfirm("deletechat", new UiNode(UiKind.Button) { Icon = "trash-can", Text = "Delete chat", OnClick = () => c.Act("PDA_clearMessages", ("ref", chatRef)) }));
        }

        return new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.Section).With(top),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section) { Scrollable = true, Fill = true, Title = Name(recipient) }.With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(log))),
            new UiNode(UiKind.StackItem).With(sendingBar));
    }
}
