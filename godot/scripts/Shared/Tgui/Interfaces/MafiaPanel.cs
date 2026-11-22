using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MafiaPanel.tsx (gaps S4d, D327): Mafia's panel: the
// lobby with its signup and early start vote, the role with the phase's time left and the role's pictures (tg's mafia spritesheet,
// icons/obj/mafia.dmi), the chat log and its box, the judgement votes, the players with the actions the player can take on them,
// the role list with its help, the notes, and the admin controls. MafiaPanelData is shared with NtosMafiaPanel. Flex is a Stack;
// the role pictures are drawn at twice their size, side by side (the kit has no transforms). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MafiaPanel
{
    // tg: MafiaPanel.tsx:155
    public static UiNode Render(TguiContext c)
    {
        UiValue roleinfo = c.Data.Get("roleinfo");
        c.WindowTheme = roleinfo is { IsNull: false } ? roleinfo.Str("role_theme", null) : null;
        return c.Window(900, 600, "Mafia", Data(c));
    }

    // tg: MafiaPanel.tsx:82  MafiaPanelData
    public static UiNode Data(TguiContext c)
    {
        UiValue data = c.Data;
        string phase = data.Str("phase");
        bool hasRole = data.Get("roleinfo") is { IsNull: false };
        bool admin = data.Truthy("admin_controls");
        string tab = c.UseState("mafia_tab", "Role list");

        if (phase == "No Game")
        {
            var lobby = new UiNode(UiKind.Stack) { Key = "lobby-stack", Fill = true, Vertical = true }.With(Lobby(c));
            if (admin)
                lobby.With(Admin(c));
            return lobby;
        }

        var right = new UiNode(UiKind.Stack) { Key = "right", Fill = true, Vertical = true };
        if (hasRole)
        {
            right.With(new UiNode(UiKind.StackItem) { Key = "role" }.With(Role(c)));
            if (phase == "Judgment" && !data.Truthy("player_voted_up"))
                right.With(new UiNode(UiKind.StackItem) { Key = "judgement" }.With(Judgement(c)));
        }
        right.With(new UiNode(UiKind.StackItem) { Key = "admin" }.With(admin ? Admin(c) : null));
        UiNode Tab(string name, string icon, string tooltip) => new UiNode(UiKind.Tab)
        {
            Key = "tab:" + name,
            TextAlign = "center",
            Selected = tab == name,
            OnClick = () => c.SetState("mafia_tab", name),
        }.With(
            new UiNode(UiKind.Box) { Text = name },
            new UiNode(UiKind.Button) { Key = "tab-help:" + name, Color = "transparent", Icon = icon, Tooltip = tooltip });
        right.With(new UiNode(UiKind.StackItem) { Key = "lower" }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "players", Grow = 1 }.With(Players(c)),
            new UiNode(UiKind.StackItem) { Key = "tabs", Grow = 1 }.With(
                new UiNode(UiKind.Tabs) { Key = "mafia-tabs", Fluid = true }.With(
                    Tab("Role list", "address-book", "This is the list of roles in the game. You can press the question mark to get a quick blurb about the role itself."),
                    Tab("Notes", "pencil", "This is your notes, anything you want to write can be saved for future reference. You can also send it to chat with a button.")),
                tab == "Role list" ? ListOfRoles(c) : tab == "Notes" ? Notes(c) : null))));

        var stack = new UiNode(UiKind.Stack) { Key = "game", Fill = true };
        if (hasRole)
            stack.With(new UiNode(UiKind.StackItem) { Key = "chat", Grow = 1 }.With(Chat(c)));
        return stack.With(new UiNode(UiKind.StackItem) { Key = "board", Grow = 1 }.With(right));
    }

    // tg: MafiaPanel.tsx:168
    private static UiNode Chat(TguiContext c)
    {
        UiValue messages = c.Data.Get("messages");
        string message = c.UseState("message_to_send", "");
        var stack = new UiNode(UiKind.Stack) { Key = "chat-stack", Vertical = true, Fill = true };
        if (messages is { IsNull: false })
        {
            var log = new UiNode(UiKind.Section) { Key = "logs", Fill = true, Scrollable = true, Title = "Chat Logs" };
            int index = 0;
            foreach (UiValue entry in c.Data.Arr("messages"))
                log.With(new UiNode(UiKind.Box) { Key = "msg:" + index++, Wrap = true, Text = TguiFormat.DecodeHtmlEntities(entry.Str("msg")) });
            stack.With(
                log,
                new UiNode(UiKind.TextArea)
                {
                    Key = "chat-box",
                    Fluid = true,
                    Height = "10%",
                    MaxLength = 300,
                    Candystripe = true,
                    Tooltip = "Type to chat",
                    TextValue = message,
                    OnInput = v => c.SetState("message_to_send", v),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "send-chat",
                    Color = "bad",
                    Fluid = true,
                    TextAlign = "center",
                    Tooltip = "Sends your message to chat.",
                    Text = "Send to Chat",
                    OnClick = () =>
                    {
                        c.SetState("message_to_send", "");
                        c.Act("send_message_to_chat", ("message", message));
                    },
                });
        }
        return stack;
    }

    // tg: MafiaPanel.tsx:211
    private static UiNode Lobby(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> lobbydata = data.Arr("lobbydata");
        int ready = lobbydata.Count(player => player.Str("status") == "Ready");
        bool observer = data.Truthy("is_observer");
        var section = new UiNode(UiKind.Section)
        {
            Key = "lobby",
            Fill = true,
            Scrollable = true,
            Title = "Lobby",
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button)
                {
                    Key = "signup",
                    Icon = "clipboard-check",
                    Tooltip = "Signs you up for the next game. If there is an ongoing one, you will be signed up for the next.",
                    Text = "Sign Up",
                    OnClick = () => c.Act("mf_signup"),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "vote-start",
                    Icon = "arrow-right",
                    Tooltip = "Submit a vote to start the game early. Starts when half of the current signup list have voted to start. Requires a bare minimum of six players.",
                    Text = "Start Now!",
                    OnClick = () => c.Act("vote_to_start"),
                }),
        }.With(new UiNode(UiKind.NoticeBox) { Key = "count", Color = "info", TextAlign = "center", Text = "The lobby currently has " + ready + "/12 valid players signed up." });
        if (observer)
            section.With(new UiNode(UiKind.NoticeBox)
            {
                Key = "observer",
                Color = "green",
                TextAlign = "center",
                Text = "Players who sign up for Mafia while dead will be returned to their bodies after the game finishes, allowing you to temporarily exit to play a match.",
            });
        int index = 0;
        foreach (UiValue lobbyist in lobbydata)
        {
            string status = lobbyist.Str("status");
            section.With(new UiNode(UiKind.Stack) { Key = "lobbyist:" + index++, Candystripe = true, Align = "baseline" }.With(
                new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, Text = !observer ? "Unknown Player" : lobbyist.Str("name") },
                new UiNode(UiKind.StackItem) { Key = "label", Text = "Status:" },
                new UiNode(UiKind.StackItem) { Key = "status", Color = status == "Ready" ? "green" : "red", Text = status }));
        }
        return section;
    }

    // tg: MafiaPanel.tsx:280
    private static UiNode Role(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue roleinfo = data.Get("roleinfo");
        string title = data.Str("phase") + data.Str("turn");
        var info = new UiNode(UiKind.StackItem) { Key = "info", Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "you-are", FontSize = "16px", Text = "You are the " + roleinfo.Str("role") });
        if (roleinfo.Truthy("role_dead"))
            info.With(new UiNode(UiKind.Box) { Key = "dead", Bold = true, Wrap = true, Text = "You are currently dead. You may speak with the Chaplain at night, if there is one." });
        else
            info.With(new UiNode(UiKind.Box) { Key = "desc", Wrap = true, Text = roleinfo.Str("desc") });
        return new UiNode(UiKind.Section)
        {
            Key = "role",
            Fill = true,
            Title = title,
            Buttons = new UiNode(UiKind.Box) { Key = "timeleft", FontSize = "14px", Bold = true, Text = TguiFormat.FormatTime(data.Num("timeleft")) },
        }.With(new UiNode(UiKind.Stack) { Key = "role-stack" }.With(
            info,
            new UiNode(UiKind.StackItem) { Key = "icons" }.With(
                new UiNode(UiKind.Box) { Key = "revealed-icon", DmIcon = "icons/obj/mafia.dmi", DmIconState = roleinfo.Str("revealed_icon"), Width = "64px", Height = "64px" },
                new UiNode(UiKind.Box) { Key = "hud-icon", DmIcon = "icons/obj/mafia.dmi", DmIconState = roleinfo.Str("hud_icon"), Width = "64px", Height = "64px" })));
    }

    // tg: MafiaPanel.tsx:330
    private static UiNode ListOfRoles(TguiContext c)
    {
        var column = new UiNode(UiKind.Stack) { Key = "roles", Vertical = true };
        int index = 0;
        foreach (UiValue role in c.Data.Arr("all_roles"))
        {
            string name = role.Text ?? "";
            column.With(new UiNode(UiKind.StackItem) { Key = "role:" + index++, Candystripe = true }.With(new UiNode(UiKind.Stack) { Align = "center", Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Key = "name", Text = name },
                new UiNode(UiKind.StackItem) { Key = "help-item", TextAlign = "right" }.With(new UiNode(UiKind.Button)
                {
                    Key = "help",
                    Color = "transparent",
                    Icon = "question",
                    OnClick = () => c.Act("mf_lookup", ("role_name", name.Length >= 3 ? name[..^3] : "")),
                }))));
        }
        return new UiNode(UiKind.Section) { Key = "role-list", Fill = true }.With(column);
    }

    // tg: MafiaPanel.tsx:362
    private static UiNode Notes(TguiContext c)
    {
        string note = c.UseState("note_message", c.Data.Str("user_notes"));
        return new UiNode(UiKind.Section) { Key = "notes", Fill = true }.With(
            new UiNode(UiKind.TextArea)
            {
                Key = "notes-box",
                Height = "80%",
                MaxLength = 600,
                Candystripe = true,
                Tooltip = "Insert Notes...",
                TextValue = note,
                OnInput = v => c.SetState("note_message", v),
            },
            new UiNode(UiKind.Button)
            {
                Key = "save",
                Color = "good",
                Fluid = true,
                TextAlign = "center",
                Tooltip = "Saves whatever is written as your notepad. This can't be done while dead.",
                Text = "Save",
                OnClick = () => c.Act("change_notes", ("new_notes", note)),
            },
            c.ButtonConfirm("send-notes", new UiNode(UiKind.Button)
            {
                Color = "bad",
                Fluid = true,
                TextAlign = "center",
                Text = "Send to Chat",
                OnClick = () => c.Act("send_notes_to_chat"),
            }));
    }

    // tg: MafiaPanel.tsx:399
    private static UiNode Judgement(TguiContext c)
    {
        return new UiNode(UiKind.Section) { Key = "judgement", Title = "Judgement" }.With(
            new UiNode(UiKind.Stack) { Key = "votes" }.With(
                new UiNode(UiKind.Button) { Key = "innocent", Icon = "smile-beam", Color = "good", Text = "Innocent", OnClick = () => c.Act("vote_innocent") },
                new UiNode(UiKind.Box) { Key = "prompt", Text = "It is now time to vote, vote the accused innocent or guilty!" },
                new UiNode(UiKind.Button) { Key = "guilty", Icon = "angry", Color = "bad", Text = "Guilty", OnClick = () => c.Act("vote_guilty") }),
            new UiNode(UiKind.Stack) { Key = "abstain-row", Justify = "center" }.With(
                new UiNode(UiKind.Button) { Key = "abstain", Icon = "meh", Color = "white", Text = "Abstain", OnClick = () => c.Act("vote_abstain") }));
    }

    // tg: MafiaPanel.tsx:425
    private static UiNode Players(TguiContext c)
    {
        string votedUp = c.Data.Str("person_voted_up_ref");
        var column = new UiNode(UiKind.Stack) { Key = "players-list", Vertical = true, Fill = true, Justify = "space-around" };
        foreach (UiValue player in c.Data.Arr("players"))
        {
            string reference = player.Str("ref");
            bool alive = player.Truthy("alive");
            string suffix = player.Truthy("is_you") ? " (YOU)" : player.Truthy("role_revealed") ? " - " + player.Str("role_revealed") : "";
            var actions = new UiNode(UiKind.StackItem) { Key = "actions", MinWidth = "42px", TextAlign = "center" };
            foreach (UiValue action in player.Arr("possible_actions"))
            {
                string actionRef = action.Str("ref");
                actions.With(new UiNode(UiKind.Button)
                {
                    Key = "action:" + action.Str("name"),
                    Text = action.Str("name"),
                    OnClick = () => c.Act("perform_action", ("action_ref", actionRef), ("target", reference)),
                });
            }
            column.With(new UiNode(UiKind.StackItem) { Key = "player:" + reference, Candystripe = true }.With(new UiNode(UiKind.Stack) { Align = "center" }.With(
                new UiNode(UiKind.StackItem)
                {
                    Key = "name",
                    Grow = 1,
                    Color = alive ? null : "red",
                    Background = reference == votedUp ? "yellow" : null,
                    Text = player.Str("name") + suffix,
                },
                new UiNode(UiKind.StackItem) { Key = "votes" }, // tg shows player.votes, which the server never sends
                actions)));
        }
        return new UiNode(UiKind.Section) { Key = "players", Fill = true, Scrollable = true, Title = "Players" }.With(column);
    }

    // tg: MafiaPanel.tsx:468
    private static UiNode Admin(TguiContext c)
    {
        UiNode Button(string key, string icon, string text, string action) => new UiNode(UiKind.Button) { Key = key, Icon = icon, Text = text, OnClick = () => c.Act(action) };
        return new UiNode(UiKind.Collapsible) { Key = "admin-controls", Title = "ADMIN CONTROLS", Color = "red" }.With(new UiNode(UiKind.Section) { Key = "admin" }.With(
            new UiNode(UiKind.Collapsible) { Key = "warning", Title = "A kind, coder warning", Color = "transparent" }.With(new UiNode(UiKind.Box)
            {
                Wrap = true,
                Text = "Almost all of these are all built to help me debug the game (ow, debugging a 12 player game!) So they are rudamentary and prone to breaking at the drop of a hat. Make sure you know what you're doing when you press one. Also because an admin did it: do not gib/delete/dust anyone! It will runtime the game to death",
            }),
            new UiNode(UiKind.Stack) { Key = "row1" }.With(
                Button("next_phase", "arrow-right", "Next Phase", "next_phase"),
                Button("players_home", "home", "Send All Players Home", "players_home"),
                Button("new_game", "sync-alt", "New Game", "new_game"),
                Button("nuke", "skull", "Nuke", "nuke")),
            new UiNode(UiKind.Stack) { Key = "row2" }.With(
                Button("debug_setup", "paint-brush", "Create Custom Setup", "debug_setup"),
                Button("cancel_setup", "paint-roller", "Reset Custom Setup", "cancel_setup"),
                Button("start_now", "magic", "Start now!", "start_now"))));
    }
}
