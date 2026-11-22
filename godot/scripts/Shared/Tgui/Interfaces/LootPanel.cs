using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LootPanel/ (index.tsx, GroupedContents.tsx, RawContents.tsx,
// LootBox.tsx, IconDisplay.tsx, types.ts; gaps package G1, D291). The window reads tg's ui_data of the loot panel (lootpanel/ui.dm:2): the
// items of a tile with their icon, name, type and ref, and sends tg's one act, grab, with the modifier keys held or the right button.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// The window's title bar buttons (the search field and the grouping toggle, tgui's Window buttons) are a row at the top of the content: the
// host's title bar has no such slot. The window grows by that row's height. Esc closes it as every window (tgui's own Esc sends close).

public static class LootPanel
{
    // tg: LootPanel/index.tsx:51
    private const double HeaderHeight = 38, ItemHeight = 38;

    /// <summary>The height of the search row that stands where tg's title bar buttons are.</summary>
    private const double SearchRow = 34;

    // tg: LootPanel/index.tsx:15
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> contents = c.Data.Arr("contents");

        // tg: index.tsx:19  limitations: items with different stack counts, charges etc.
        var groups = new List<(string Key, List<UiValue> Items)>();
        var byKey = new Dictionary<string, List<UiValue>>(StringComparer.Ordinal);
        foreach (UiValue item in contents)
        {
            string key = item.Get("path")?.Truthy == true ? item.Str("path") + item.Str("name") : item.Str("ref");
            if (!byKey.TryGetValue(key, out List<UiValue> list))
            {
                list = new List<UiValue>();
                byKey[key] = list;
                groups.Add((key, list));
            }
            list.Add(item);
        }

        bool grouping = c.UseState("grouping", true);
        string searchText = c.UseState("searchText", "");

        // tg: index.tsx:51  the window is as high as its rows, between one and ten of them
        double minHeight = HeaderHeight + ItemHeight;
        double maxHeight = HeaderHeight + ItemHeight * 10;
        double height = Math.Clamp(HeaderHeight + (!grouping ? contents.Count : groups.Count) * ItemHeight, minHeight, maxHeight);

        // tg: index.tsx:66  <Window width={300} height={height} buttons={...}>
        return c.Window(300, height + SearchRow, null,
            // tg: index.tsx:70  <Input onChange={setSearchText} placeholder="Search items..." value={searchText} />
            new UiNode(UiKind.Stack) { Key = "header" }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Input)
                {
                    Key = "search",
                    Fluid = true,
                    Tooltip = "Search items...",
                    TextValue = searchText,
                    OnInput = value => c.SetState("searchText", value),
                }),
                // tg: index.tsx:75
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "grouping",
                    Icon = grouping ? "layer-group" : "object-ungroup",
                    Selected = grouping,
                    Tooltip = "Toggle Grouping",
                    OnClick = () => c.SetState("grouping", !grouping),
                })),
            // tg: index.tsx:89  <Window.Content fitted scrollable={height === maxHeight}><Section>
            new UiNode(UiKind.Section) { Key = "contents", Scrollable = height == maxHeight }.With(
                grouping ? Grouped(c, groups, searchText) : Raw(c, contents, searchText)));
    }

    // tg: GroupedContents.tsx:12
    private static UiNode Grouped(TguiContext c, List<(string Key, List<UiValue> Items)> groups, string searchText)
    {
        Func<(string Key, List<UiValue> Items), bool> matches = TguiFormat.CreateSearch<(string Key, List<UiValue> Items)>(searchText, g => g.Items[0].Str("name"));
        var box = new UiNode(UiKind.Box) { Key = "groups" };
        foreach ((string Key, List<UiValue> Items) group in groups.Where(matches))
            box.With(LootBox(c, group.Items[0], group.Items.Count));
        return box;
    }

    // tg: RawContents.tsx:12
    private static UiNode Raw(TguiContext c, IReadOnlyList<UiValue> contents, string searchText)
    {
        Func<UiValue, bool> matches = TguiFormat.CreateSearch<UiValue>(searchText, item => item.Str("name"));
        var box = new UiNode(UiKind.Box) { Key = "items" };
        foreach (UiValue item in contents.Where(matches))
            box.With(LootBox(c, item, 0));
        return box;
    }

    // tg: LootBox.tsx:17  a group has its amount, an item alone has none
    private static UiNode LootBox(TguiContext c, UiValue item, int amount)
    {
        string name = item.Str("name");
        string shown = name.Length == 0 ? "???" : char.ToUpperInvariant(name[0]) + name[1..]; // tgui-core's capitalizeFirst
        string reference = item.Str("ref");

        return new UiNode(UiKind.Button)
        {
            Key = "box:" + (amount > 0 ? item.Str("path") + item.Str("name") : reference),
            Fluid = true,
            Color = "transparent",
            // tg: LootBox.tsx:33  onClick: act('grab', {alt, ctrl, ref, shift}); onContextMenu: act('grab', {right: true, ref})
            OnClickWith = click => c.Act("grab", click.Right
                ? new (string, object)[] { ("right", true), ("ref", reference) }
                : new (string, object)[] { ("alt", click.Alt), ("ctrl", click.Ctrl), ("ref", reference), ("shift", click.Shift) }),
        }.With(
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { MinWidth = "36px", Height = "42px" }.With(IconDisplay(item)),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "name", Text = shown }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "amount", Text = amount > 1 ? "x" + amount : "" })));
    }

    // tg: IconDisplay.tsx:18  the picture of an item: a spinner while it is made, a dumpster fire when it cannot be, else its icon and state
    private static UiNode IconDisplay(UiValue item)
    {
        string icon = item.Str("icon");
        string iconState = item.Str("icon_state");

        if (icon.Length == 0)
            return new UiNode(UiKind.Icon) { Key = "icon", Icon = "spinner", Size = 1.5, Color = "gray", Spin = true };
        if (icon == "n/a")
            return new UiNode(UiKind.Icon) { Key = "icon", Icon = "dumpster-fire", Size = 1.5, Color = "gray" };
        return new UiNode(UiKind.Box) { Key = "icon", DmIcon = icon, DmIconState = iconState.Length > 0 ? iconState : null, Width = "36px", Height = "36px" };
    }
}
