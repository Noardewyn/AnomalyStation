using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosNetChat.jsx (gaps S4b, D327): the NTNRC chat client:
// the channel list with New Channel, the screen name and admin mode, the messages (or the password notice, or the no-channel hint),
// the message line, the channel's clients with ping and mute, and the channel's settings. Button.Input is an input that commits its
// value (as NtosFileManager); the Dimmer is a centred box (the kit has no dimmer); the line clears itself after sending (selfClear).
// Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosNetChat
{
    // tg: NtosNetChat.jsx:19
    private static UiNode NoChannelDimmer() =>
        new UiNode(UiKind.Box) { Key = "nochannel", TextAlign = "center" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "icons" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Icon = "grin-beam", Color = "green", FontSize = "100px" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Icon = "comment-dots", FontSize = "100px" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Icon = "smile", Color = "green", FontSize = "100px" }))),
            new UiNode(UiKind.StackItem) { Key = "hint", FontSize = "18px", Text = "Click a channel to start chatting!" },
            new UiNode(UiKind.StackItem) { Key = "hint2", FontSize = "15px", Text = "(If you're new, you may want to set your name in the bottom left!)" }));

    // tg: NtosNetChat.jsx:48
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string title = data.Str("title");
        bool canAdmin = data.Truthy("can_admin");
        bool adminmode = data.Truthy("adminmode");
        bool authed = data.Truthy("authed");
        string username = data.Str("username");
        UiValue activeChannel = data.Get("active_channel");
        bool isOperator = data.Truthy("is_operator");
        bool strong = data.Truthy("strong");
        string selfref = data.Str("selfref");
        bool inChannel = activeChannel != null && !activeChannel.IsNull;
        bool authorized = authed || adminmode;
        // This list has clients ordered by operator>status>alphabetical
        List<UiValue> displayedClients = data.Arr("clients")
            .OrderByDescending(cl => cl.Truthy("operator") ? 1 : 0)
            .ThenByDescending(cl => cl.Num("status"))
            .ThenBy(cl => cl.Str("name"), StringComparer.Ordinal)
            .ToList();
        static string ClientColor(UiValue client) => client.Truthy("operator") ? "green" : client.Truthy("online") ? "white" : client.Truthy("away") ? "yellow" : "label";
        // client from this computer!
        UiValue thisClient = data.Arr("clients").FirstOrDefault(cl => cl.Str("ref") == selfref);
        bool muted = thisClient != null && thisClient.Truthy("muted");

        var channels = new UiNode(UiKind.StackItem) { Key = "channels", Grow = 1 }.With(new UiNode(UiKind.Input)
        {
            Key = "newchannel",
            Fluid = true,
            Tooltip = "New Channel...",
            TextValue = "",
            OnCommit = value => c.Act("PRG_newchannel", ("new_channel_name", value)),
        });
        foreach (UiValue channel in data.Arr("all_channels"))
        {
            UiValue id = channel.Get("id");
            channels.With(new UiNode(UiKind.Button)
            {
                Key = "chan:" + channel.Str("chan"),
                Fluid = true,
                Text = channel.Str("chan"),
                Selected = inChannel && id != null && id.Number == activeChannel.Number,
                Color = "transparent",
                OnClick = () => c.Act("PRG_joinchannel", ("id", id.Number)),
            });
        }
        var user = new UiNode(UiKind.StackItem) { Key = "user" }.With(
            new UiNode(UiKind.Box) { Text = "Username:" },
            new UiNode(UiKind.Input)
            {
                Key = "username",
                Fluid = true,
                Tooltip = username + "...",
                TextValue = username,
                OnCommit = value => c.Act("PRG_changename", ("new_name", value)),
            });
        if (canAdmin)
            user.With(new UiNode(UiKind.Button)
            {
                Key = "adminmode",
                Fluid = true,
                Bold = true,
                Text = "ADMIN MODE: " + (adminmode ? "ON" : "OFF"),
                Color = adminmode ? "bad" : "good",
                OnClick = () => c.Act("PRG_toggleadmin"),
            });

        var messages = new UiNode(UiKind.Section) { Key = "messages", Scrollable = true, Fill = true };
        if (inChannel)
        {
            if (authorized)
            {
                foreach (UiValue message in data.Arr("messages"))
                    messages.With(new UiNode(UiKind.Box) { Key = "msg:" + message.Str("key"), Text = message.Str("msg") });
            }
            else
            {
                messages.With(new UiNode(UiKind.Box) { Key = "locked", TextAlign = "center" }.With(
                    new UiNode(UiKind.Icon) { Icon = "exclamation-triangle", FontSize = "40px" },
                    new UiNode(UiKind.Box) { Bold = true, FontSize = "18px", Text = "THIS CHANNEL IS PASSWORD PROTECTED" },
                    new UiNode(UiKind.Box) { Text = "INPUT PASSWORD TO ACCESS" }));
            }
        }
        else
            messages.With(NoChannelDimmer());
        var chat = new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(new UiNode(UiKind.StackItem) { Key = "log", Grow = 1 }.With(messages));
        if (inChannel)
        {
            string line = c.UseState("message", "");
            void Send()
            {
                c.Act("PRG_speak", ("message", c.UseState("message", "")));
                c.SetState("message", "");
            }
            chat.With(new UiNode(UiKind.Input)
            {
                Key = "say",
                Background = muted ? "red" : null,
                Height = "22px",
                Tooltip = muted ? "You are muted!" : "Message " + title,
                Fluid = true,
                Disabled = muted,
                TextValue = line,
                OnInput = v => c.SetState("message", v),
                OnEnter = Send,
            });
        }

        var row = new UiNode(UiKind.Stack) { Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "left" }.With(new UiNode(UiKind.Section) { Fill = true }.With(new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(channels, user))),
            new UiNode(UiKind.Divider) { Key = "div1" },
            new UiNode(UiKind.StackItem) { Key = "chat", Grow = 4 }.With(chat));
        if (inChannel)
        {
            var clientList = new UiNode(UiKind.Stack) { Vertical = true };
            foreach (UiValue client in displayedClients)
            {
                string reference = client.Str("ref");
                var entry = new UiNode(UiKind.Stack) { Key = "client:" + client.Str("name"), Height = "18px", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1, Color = ClientColor(client), Text = client.Str("name") });
                if (client != thisClient)
                {
                    entry.With(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "ping",
                        Disabled = muted,
                        Icon = "bullhorn",
                        Tooltip = !muted ? "Ping" : "You are muted!",
                        OnClick = () => c.Act("PRG_ping_user", ("ref", reference)),
                    }));
                    if (isOperator)
                    {
                        bool clientMuted = client.Truthy("muted");
                        entry.With(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                        {
                            Key = "mute",
                            Icon = !clientMuted ? "volume-up" : "volume-mute",
                            Color = !clientMuted ? "green" : "red",
                            Tooltip = !clientMuted ? "Mute this User" : "Unmute this User",
                            OnClick = () => c.Act("PRG_mute_user", ("ref", reference)),
                        }));
                    }
                }
                clientList.With(entry);
            }
            var settings = new UiNode(UiKind.Stack) { Vertical = true }.With(new UiNode(UiKind.StackItem) { Key = "settings-title", Text = "Settings for " + title + ":" });
            if (inChannel && authorized)
                settings.With(
                    new UiNode(UiKind.Input) { Key = "savelog", Fluid = true, Tooltip = "Save log as...", TextValue = "", OnCommit = value => c.Act("PRG_savelog", ("log_name", value)) },
                    c.ButtonConfirm("leave", new UiNode(UiKind.Button) { Fluid = true, Text = "Leave Channel", OnClick = () => c.Act("PRG_leavechannel") }));
            if (isOperator && authed)
                settings.With(
                    c.ButtonConfirm("delete", new UiNode(UiKind.Button) { Fluid = true, Disabled = strong, Text = "Delete Channel", OnClick = () => c.Act("PRG_deletechannel") }),
                    new UiNode(UiKind.Input) { Key = "renamechannel", Fluid = true, Disabled = strong, Tooltip = "Rename Channel...", TextValue = "", OnCommit = value => c.Act("PRG_renamechannel", ("new_name", value)) },
                    new UiNode(UiKind.Input) { Key = "setpassword", Fluid = true, Tooltip = "Set Password...", TextValue = "", OnCommit = value => c.Act("PRG_setpassword", ("new_password", value)) });
            row.With(
                new UiNode(UiKind.Divider) { Key = "div2" },
                new UiNode(UiKind.StackItem) { Key = "clients", Grow = 2 }.With(new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(new UiNode(UiKind.Section) { Scrollable = true, Fill = true }.With(clientList)),
                    new UiNode(UiKind.Section) { Key = "settings" }.With(settings))));
        }

        return NtosWindow.Render(c, 1000, 675, null, row);
    }
}
