using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BotAnnouncement.tsx (gaps package C3): a player bot's prerecorded lines and shortcuts
// (bot_announcement.dm:51). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Not drawn: a shortcut's channel in the radio channel's colour (RADIO_CHANNELS of tgui/constants): the plain text.

public static class BotAnnouncement
{
    private const int TabAnnouncements = 0;
    private const int TabShortcuts = 1;
    private const string NoChannel = "No radio channel";

    // tg: BotAnnouncement.tsx:44
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> lines = data.Field("lines")?.Items ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> button_data = data.Field("button_data")?.Items ?? Array.Empty<UiValue>();
        IReadOnlyList<UiValue> channels = data.Field("channels")?.Items ?? Array.Empty<UiValue>();
        double cooldown_left = data.Num("cooldown_left");

        int tab = c.UseState("tab", TabAnnouncements);
        string selectedChannel = c.UseState<string>("selectedChannel", null);
        int selectedLine = c.UseState("selectedLine", -1);
        int selectedButton = c.UseState("selectedButton", -1);
        string search = c.UseState("search", "");

        var list = new UiNode(UiKind.Stack) { Vertical = true };
        if (tab == TabAnnouncements)
        {
            for (int index = 0; index < lines.Count; index++)
            {
                string line = lines[index].AsStr();
                if (search.Length > 0 && !line.Contains(search, StringComparison.OrdinalIgnoreCase))
                    continue;
                int i = index;
                list.With(new UiNode(UiKind.StackItem) { Key = "line:" + line }.With(new UiNode(UiKind.Button)
                {
                    Key = "select:" + i,
                    Fluid = true,
                    Color = selectedLine == i ? "green" : "transparent",
                    Icon = "volume-high",
                    Text = line,
                    OnClick = () => c.SetState("selectedLine", i),
                }));
            }
        }
        else
        {
            for (int index = 0; index < button_data.Count; index++)
            {
                UiValue button = button_data[index];
                if (search.Length > 0 && !button.Str("name").Contains(search, StringComparison.OrdinalIgnoreCase))
                    continue;
                int i = index;
                string channel = button.Str("channel");
                list.With(new UiNode(UiKind.StackItem) { Key = "shortcut:" + i }.With(new UiNode(UiKind.Button)
                {
                    Key = "select:" + i,
                    Fluid = true,
                    Color = selectedButton == i ? "green" : "transparent",
                    Icon = "volume-high",
                    Text = button.Str("name") + " (" + (string.IsNullOrEmpty(channel) ? NoChannel : channel) + ")",
                    OnClick = () => c.SetState("selectedButton", i),
                }));
            }
        }

        var channelOptions = new List<string> { NoChannel };
        channelOptions.AddRange(channels.Select(ch => ch.AsStr()));

        // tg: BotAnnouncement.tsx:81  <Window width={650} height={500}>
        return c.Window(650, 500, null,
            new UiNode(UiKind.Section).With(new UiNode(UiKind.Tabs).With(
                new UiNode(UiKind.Tab)
                {
                    Key = "tab:announcements",
                    Text = "Announcements",
                    Selected = tab == TabAnnouncements,
                    OnClick = () =>
                    {
                        c.SetState("search", "");
                        c.SetState("tab", TabAnnouncements);
                    },
                },
                new UiNode(UiKind.Tab)
                {
                    Key = "tab:shortcuts",
                    Text = "Shortcuts",
                    Selected = tab == TabShortcuts,
                    OnClick = () =>
                    {
                        c.SetState("search", "");
                        c.SetState("tab", TabShortcuts);
                    },
                })),
            new UiNode(UiKind.Section) { Scrollable = true, Height = "350px" }.With(list),
            new UiNode(UiKind.Section).With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
                {
                    Key = "search",
                    Fluid = true,
                    AutoFocus = true,
                    Tooltip = "Search...",
                    TextValue = search,
                    OnInput = value => c.SetState("search", value),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack) { Align = "center" }.With(
                    tab == TabAnnouncements
                        ? new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Dropdown)
                        {
                            Key = "channel",
                            Width = "100%",
                            Options = channelOptions,
                            Text = selectedChannel ?? NoChannel,
                            OnSelected = value => c.SetState<string>("selectedChannel", value == NoChannel ? null : value),
                        })
                        : new UiNode(UiKind.StackItem) { Grow = 1 },
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "shortcut",
                        TextAlign = "center",
                        MinWidth = "96px",
                        Color = tab == TabAnnouncements ? "default" : "red",
                        Text = tab == TabAnnouncements ? "Make Shortcut" : "Delete Shortcut",
                        OnClick = () =>
                        {
                            if (tab == TabAnnouncements)
                            {
                                if (selectedLine < 0)
                                    return;
                                c.Act("set_button", ("picked", lines[selectedLine].AsStr()), ("channel", selectedChannel));
                            }
                            else
                            {
                                if (selectedButton < 0)
                                    return;
                                c.Act("remove_button", ("index", selectedButton + 1));
                            }
                        },
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "play",
                        TextAlign = "center",
                        MinWidth = "96px",
                        Disabled = cooldown_left > 0,
                        Text = "Play " + (cooldown_left > 0 ? "(" + Math.Round(cooldown_left / 10) + "s)" : ""),
                        OnClick = () =>
                        {
                            if (tab == TabAnnouncements)
                            {
                                if (selectedLine < 0)
                                    return;
                                c.Act("announce", ("picked", lines[selectedLine].AsStr()), ("channel", selectedChannel));
                            }
                            else
                            {
                                if (selectedButton < 0)
                                    return;
                                UiValue button = button_data[selectedButton];
                                c.Act("announce", ("picked", button.Str("name")), ("channel", button.Str("channel")));
                            }
                        },
                    }))))));
    }
}
