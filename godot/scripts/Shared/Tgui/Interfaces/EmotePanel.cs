using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/EmotePanel.tsx (M9 slice B, D256): tg's emote panel, the
// emote wheel's "More..." (tg's /datum/emote_panel ui_static_data "emotes"; acts play_emote and preview_sound).
// DEVIATION(DV163): a button's tooltip of icons (the visible, audible, sound, hands and params marks, red when off) is the same marks as
// text, since the kit's tooltip is text.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class EmotePanel
{
    // tg: tgui/packages/tgui/interfaces/EmotePanel.tsx:25
    public static UiNode Content(TguiContext c)
    {
        IReadOnlyList<UiValue> emotes = c.Data.Arr("emotes");

        bool filterVisible = c.UseState("filterVisible", false);
        bool filterAudible = c.UseState("filterAudible", false);
        bool filterSound = c.UseState("filterSound", false);
        bool filterHands = c.UseState("filterHands", false);
        bool filterUseParams = c.UseState("filterUseParams", false);
        bool useParams = c.UseState("useParams", false);
        string searchText = c.UseState("searchText", "");
        bool showNames = c.UseState("showNames", true);
        bool showIcons = c.UseState("showIcons", false);

        List<UiValue> filteredEmotes = emotes
            .Where(emote =>
                emote.Str("key").Length > 0
                && (searchText.Length > 0
                    ? emote.Str("key").ToLowerInvariant().Contains(searchText.ToLowerInvariant(), StringComparison.Ordinal)
                        || emote.Str("name").ToLowerInvariant().Contains(searchText.ToLowerInvariant(), StringComparison.Ordinal)
                    : true)
                && (!filterVisible || emote.Truthy("visible"))
                && (!filterAudible || emote.Truthy("audible"))
                && (!filterSound || emote.Truthy("sound"))
                && (!filterHands || emote.Truthy("hands"))
                && (!filterUseParams || emote.Truthy("use_params")))
            .OrderBy(emote => emote.Str("name"), StringComparer.Ordinal)
            .ToList();

        UiNode Filter(string key, string icon, string tooltip, bool on) => new UiNode(UiKind.Button)
        {
            Key = key,
            Icon = icon,
            Width = "100%",
            Height = "100%",
            Align = "center",
            Tooltip = tooltip,
            Selected = on,
            OnClick = () => c.SetState(key, !on),
        };

        var list = new UiNode(UiKind.Stack) { Wrap = true };
        foreach (UiValue emote in filteredEmotes)
        {
            string key = emote.Str("key");
            list.With(EmoteButton(emote, showIcons, showNames,
                () => c.Act("play_emote", ("emote_key", key), ("use_params", useParams)),
                () => c.Act("preview_sound", ("emote_key", key))));
        }

        return new UiNode(UiKind.Section).With(
            new UiNode(UiKind.Section)
            {
                Title = "Filters",
                Buttons = new UiNode(UiKind.Stack).With(
                    Filter("filterVisible", "eye", "Visible", filterVisible),
                    Filter("filterAudible", "comment", "Audible", filterAudible),
                    Filter("filterSound", "volume-up", "Sound", filterSound),
                    Filter("filterHands", "hand-paper", "Hands", filterHands),
                    Filter("filterUseParams", "crosshairs", "Params", filterUseParams)),
            }.With(
                // <SearchBar query={searchText} onSearch={setSearchText} placeholder="Search all emotes..." />
                new UiNode(UiKind.Input)
                {
                    Key = "search",
                    Icon = "search",
                    Fluid = true,
                    TextValue = searchText,
                    Tooltip = "Search all emotes...",
                    OnInput = value => c.SetState("searchText", value),
                }),
            new UiNode(UiKind.Section)
            {
                Title = searchText.Length > 0 ? $"Search results of \"{searchText}\"" : "All Emotes",
                Buttons = new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Button) { Key = "showNames", Text = showNames ? "Show Names" : "Show Keys", OnClick = () => c.SetState("showNames", !showNames) },
                        new UiNode(UiKind.Button) { Key = "showIcons", Text = "Show Icons", Selected = showIcons, OnClick = () => c.SetState("showIcons", !showIcons) }),
                    new UiNode(UiKind.StackItem).With(
                        new UiNode(UiKind.Button) { Key = "useParams", Icon = "crosshairs", Text = "Use Params", Selected = useParams, OnClick = () => c.SetState("useParams", !useParams) })),
            }.With(list));
    }

    // tg: tgui/packages/tgui/interfaces/EmotePanel.tsx:203
    private static UiNode EmoteButton(UiValue emote, bool showIcons, bool showNames, Action onPlay, Action onPreview)
    {
        string label = showNames ? CapitalizeFirst(emote.Str("name").ToLowerInvariant()) : emote.Str("key");
        var button = new UiNode(UiKind.Button)
        {
            Key = "play:" + emote.Str("key"),
            Fluid = true,
            Tooltip = showIcons ? null : IconsText(emote),
            OnClick = onPlay,
        };
        if (showIcons)
            button.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Text = label }),
                new UiNode(UiKind.StackItem).With(EmoteIcons(emote))));
        else
            button.Text = label;

        var row = new UiNode(UiKind.Stack) { Align = "center" }.With(new UiNode(UiKind.StackItem) { Grow = 1 }.With(button));
        if (emote.Truthy("sound"))
        {
            row.With(new UiNode(UiKind.StackItem) { Width = 1.75 }.With(
                new UiNode(UiKind.Button) { Key = "preview:" + emote.Str("key"), Fluid = true, Icon = "volume-up", Tooltip = "Preview Sound", OnClick = onPreview }));
        }
        return new UiNode(UiKind.StackItem) { Key = emote.Str("key"), Width = showIcons ? 16 : 8 }.With(row);
    }

    // tg: tgui/packages/tgui/interfaces/EmotePanel.tsx:263
    private static UiNode EmoteIcons(UiValue emote)
    {
        UiNode Mark(string name, bool on) => new UiNode(UiKind.Icon) { Icon = name, Color = on ? null : "red", Opacity = on ? null : 0.5 };
        return new UiNode(UiKind.Stack).With(
            Mark("eye", emote.Truthy("visible")),
            Mark("comment", emote.Truthy("audible")),
            Mark("volume-up", emote.Truthy("sound")),
            Mark("hand-paper", emote.Truthy("hands")),
            Mark("crosshairs", emote.Truthy("use_params")));
    }

    // DV163: the tooltip's marks as text
    internal static string IconsText(UiValue emote)
    {
        string Mark(string name, bool on) => on ? name : "no " + name;
        return string.Join(", ", Mark("visible", emote.Truthy("visible")), Mark("audible", emote.Truthy("audible")), Mark("sound", emote.Truthy("sound")),
            Mark("hands", emote.Truthy("hands")), Mark("params", emote.Truthy("use_params")));
    }

    // tgui-core's capitalizeFirst: the first letter upper case, the rest as it is
    private static string CapitalizeFirst(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // tg: tgui/packages/tgui/interfaces/EmotePanel.tsx:300
    public static UiNode Render(TguiContext c) => c.Window(630, 500, null,
        new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(Content(c)));
}
