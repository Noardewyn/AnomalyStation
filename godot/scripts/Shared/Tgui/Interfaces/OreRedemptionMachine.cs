using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/OreRedemptionMachine.tsx (Lavaland L2a, D304).
// The window reads tg's ui_data of the ore redemption machine (machine_redemption.dm:246) and sends Claim, Release and Smelt.
// The tab is tgui's useSharedState, kept as this window's own state; Button.Input is an input that commits the amount (as
// NtosFileManager's). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class OreRedemptionMachine
{
    private const string CompactKey = "compact", SearchKey = "searchItem", TabKey = "tab";

    // tg: OreRedemptionMachine.tsx:43
    public static UiNode Render(TguiContext c)
    {
        bool compact = c.UseState(CompactKey, false);
        string searchItem = c.UseState(SearchKey, "");
        // tg: OreRedemptionMachine.tsx:48  <Window title="Ore Redemption Machine" width={435} height={500}>
        return c.Window(435, 500, "Ore Redemption Machine",
            new UiNode(UiKind.Stack) { Key = "root", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "id" }.With(IdSection(c, compact)),
                new UiNode(UiKind.StackItem) { Key = "points" }.With(PointsSection(c)),
                new UiNode(UiKind.NoticeBox) { Key = "notice", Color = "info", Text = "This machine only accepts ore. Gibtonite and Slag are not accepted." },
                new UiNode(UiKind.StackItem) { Key = "search" }.With(MaterialSearchHeader(c, searchItem)),
                new UiNode(UiKind.StackItem) { Key = "materials", Grow = 1 }.With(MaterialSection(c, compact, searchItem))));
    }

    // tg: OreRedemptionMachine.tsx:79
    private static UiNode MaterialRow(TguiContext c, bool compact, UiValue material)
    {
        double amount = material.Num("amount");
        double sheet_amounts = Math.Floor(amount);
        const int print_amount = 5;
        const int max_sheets = 50;
        double value = material.Num("value");
        string id = material.Str("id");
        var row = new UiNode(UiKind.TableRow) { Key = "mat:" + id, Candystripe = true };
        if (!compact)
            row.With(new UiNode(UiKind.TableCell) { Key = "icon" }.With(
                new UiNode(UiKind.Box) { Key = "pic", DmIcon = material.Str("icon"), DmIconState = material.Str("icon_state"), Width = "18px", Height = "18px" }));
        return row.With(
            new UiNode(UiKind.TableCell) { Key = "name", Text = TguiFormat.ToTitleCase(material.Str("name")) },
            new UiNode(UiKind.TableCell) { Key = "amount", Color = "label", Text = TguiFormat.FormatSiUnit(sheet_amounts, 0) + " " + (amount == 1 ? "sheet" : "sheets") },
            new UiNode(UiKind.TableCell) { Key = "release" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "x1",
                    Color = "transparent",
                    Tooltip = value != 0 ? TguiFormat.JsNumber(value) + " cr" : "No cost",
                    Text = "x1",
                    OnClick = () => Release(c, material, 1),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "x5",
                    Color = "transparent",
                    Tooltip = value != 0 ? TguiFormat.JsNumber(value * print_amount) + " cr" : "No cost",
                    Text = "x" + print_amount,
                    OnClick = () => Release(c, material, print_amount),
                },
                new UiNode(UiKind.Input)
                {
                    Key = "max",
                    Width = 6.0,
                    Tooltip = "[Max: " + TguiFormat.JsNumber(sheet_amounts < max_sheets ? sheet_amounts : max_sheets) + "]",
                    OnCommit = text => Release(c, material, text),
                }));
    }

    // tg: OreRedemptionMachine.tsx:236  onRelease: Release for a material, Smelt for an alloy
    private static void Release(TguiContext c, UiValue material, object amount)
    {
        if (material.Str("category") == "material")
            c.Act("Release", ("material_ref", material.Str("id")), ("sheets", amount));
        else
            c.Act("Smelt", ("path", material.Str("id")), ("sheets", amount));
    }

    // tg: OreRedemptionMachine.tsx:139
    private static UiNode IdSection(TguiContext c, bool compact)
    {
        UiValue user = c.Data.Get("user");
        bool hasUser = user != null && !user.IsNull;
        string name = hasUser ? user.Str("name") : "";
        string cash = hasUser ? user.Str("cash") : "";
        return new UiNode(UiKind.Section) { Key = "idSection" }.With(
            new UiNode(UiKind.Stack) { Key = "row" }.With(
                new UiNode(UiKind.StackItem) { Key = "card" }.With(
                    new UiNode(UiKind.Icon) { Key = "idcard", Icon = "id-card", Size = 3, Color = hasUser ? "green" : "red" }),
                new UiNode(UiKind.StackItem) { Key = "info", Grow = 1 }.With(
                    new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name", Text = string.IsNullOrEmpty(name) ? "No Name Detected" : name },
                        new UiNode(UiKind.LabeledItem) { Key = "balance", Label = "Point Balance", Text = string.IsNullOrEmpty(cash) || cash == "0" ? "No Balance Detected" : cash })),
                new UiNode(UiKind.StackItem) { Key = "compact" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "compact",
                        Color = compact ? "red" : "green",
                        Text = "Compact",
                        OnClick = () => c.SetState(CompactKey, !compact),
                    })));
    }

    // tg: OreRedemptionMachine.tsx:176
    private static UiNode PointsSection(TguiContext c)
    {
        UiValue data = c.Data;
        double unclaimedPoints = data.Num("unclaimedPoints");
        string disconnected = data.Str("disconnected");
        return new UiNode(UiKind.Section) { Key = "pointsSection" }.With(
            new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "points", Grow = 1 }.With(
                    new UiNode(UiKind.Icon) { Key = "coins", Icon = "coins", Color = "gold" },
                    new UiNode(UiKind.Box) { Key = "label", Color = "label", Text = " Unclaimed points:" },
                    new UiNode(UiKind.Box) { Key = "value", Text = " " + TguiFormat.JsNumber(unclaimedPoints) }),
                new UiNode(UiKind.StackItem) { Key = "claim" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "claim",
                        Text = "Claim",
                        Disabled = unclaimedPoints == 0 || !string.IsNullOrEmpty(disconnected),
                        Tooltip = string.IsNullOrEmpty(disconnected) ? null : disconnected,
                        OnClick = () => c.Act("Claim"),
                    })));
    }

    // tg: OreRedemptionMachine.tsx:208
    private static UiNode MaterialSection(TguiContext c, bool compact, string searchItem)
    {
        string tab = c.UseState(TabKey, "material");
        IEnumerable<UiValue> materials = c.Data.Arr("materials").Where(m => m != null && !m.IsNull);
        // createSearch: a case-insensitive substring of the name
        IEnumerable<UiValue> filtered = searchItem.Length > 0
            ? materials.Where(m => m.Str("name").Contains(searchItem, StringComparison.OrdinalIgnoreCase))
            : materials.Where(m => m.Str("category") == tab);
        var table = new UiNode(UiKind.Table) { Key = "materials" };
        foreach (UiValue material in filtered)
            table.With(MaterialRow(c, compact, material));
        return new UiNode(UiKind.Section) { Key = "materialSection", Fill = true, Scrollable = true }.With(table);
    }

    // tg: OreRedemptionMachine.tsx:256
    private static UiNode MaterialSearchHeader(TguiContext c, string searchItem)
    {
        string tab = c.UseState(TabKey, "material");
        UiNode Tab(string key, string text) => new UiNode(UiKind.Tab)
        {
            Key = key,
            Icon = "list",
            Selected = tab == key,
            Text = text,
            OnClick = () =>
            {
                c.SetState(TabKey, key);
                if (searchItem.Length > 0)
                    c.SetState(SearchKey, "");
            },
        };
        return new UiNode(UiKind.Section) { Key = "searchSection" }.With(
            new UiNode(UiKind.Stack) { Key = "row" }.With(
                new UiNode(UiKind.StackItem) { Key = "tabs", Grow = 1 }.With(
                    new UiNode(UiKind.Tabs).With(Tab("material", "Materials"), Tab("alloy", "Alloys"))),
                new UiNode(UiKind.StackItem) { Key = "search" }.With(
                    // <SearchBar expensive query={searchItem} placeholder="Search Material..." onSearch=...>
                    new UiNode(UiKind.Input)
                    {
                        Key = "search",
                        Icon = "search",
                        TextValue = searchItem,
                        Tooltip = "Search Material...",
                        OnInput = value =>
                        {
                            c.SetState(SearchKey, value);
                            if (value.Length > 0)
                                c.SetState(TabKey, "material");
                        },
                    })));
    }
}
