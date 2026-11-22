using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Uplink/index.tsx, GenericUplink.tsx,
// PrimaryObjectiveMenu.tsx and ObjectiveElement.tsx (syndicate package A2, D316): the syndicate uplink of /datum/component/uplink
// (datums/components/uplink.dm). DV44: the item list tg fetches as the uplink.json asset comes with the static data ("uplink_json");
// tg's Dimmer over a locked shop is the kit's Modal. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Uplink
{
    private const string TabKey = "currentTab";
    private const string CategoryKey = "selectedCategory";
    private const string SearchKey = "searchText";
    private const string CompactKey = "compactMode";

    // the item a row shows (GenericUplink's Item with index.tsx's extraData)
    internal sealed record Item(string Id, string Name, string Icon, string IconState, string Category, string Desc, bool LockOthers,
        string Cost, string PopulationTooltip, bool InsufficientPopulation, bool Disabled, string Ref);

    // tg: Uplink/index.tsx:89  Uplink
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double telecrystals = data.Num("telecrystals");
        double joined_population = data.Num("joined_population");
        IReadOnlyList<UiValue> primary_objectives = data.Field("primary_objectives") is { Kind: UiValueKind.Array } p ? p.Items : null;
        bool can_renegotiate = data.Truthy("can_renegotiate");
        IReadOnlyList<UiValue> extra_purchasable = data.Arr("extra_purchasable");
        UiValue extra_purchasable_stock = data.Field("extra_purchasable_stock");
        UiValue current_stock = data.Field("current_stock");
        bool lockable = data.Truthy("lockable");
        double purchased_items = data.Num("purchased_items");
        bool shop_locked = data.Truthy("shop_locked");

        // tg: index.tsx:106  populateServerData: the asset's items this uplink may sell, and their categories in the asset's order
        double uplinkFlag = data.Num("uplink_flag");
        string uplinkRole = data.Str("assigned_role", null);
        string uplinkSpecies = data.Str("assigned_species", null);
        bool debug = data.Truthy("debug");
        UiValue serverData = data.Field("uplink_json");
        List<UiValue> allItems = serverData.Arr("items").Where(value =>
        {
            if (value.Arr("restricted_roles").Count > 0 && !value.Arr("restricted_roles").Any(r => r.Text == uplinkRole) && !debug)
                return false;
            if (value.Arr("restricted_species").Count > 0 && !value.Arr("restricted_species").Any(r => r.Text == uplinkSpecies) && !debug)
                return false;
            return ((long)value.Num("purchasable_from") & (long)uplinkFlag) != 0;
        }).ToList();
        var availableCategories = new List<string>();
        foreach (UiValue item in allItems)
        {
            if (!availableCategories.Contains(item.Str("category")))
                availableCategories.Add(item.Str("category"));
        }
        List<string> allCategories = serverData.Arr("categories").Select(v => v.AsStr()).Where(availableCategories.Contains).ToList();

        // tg: index.tsx:165  render: the extra purchasable items (the sales) join the list, and their categories the tabs
        var itemsToAdd = new List<UiValue>(allItems);
        itemsToAdd.AddRange(extra_purchasable);
        foreach (UiValue item in extra_purchasable)
        {
            if (!allCategories.Contains(item.Str("category")))
                allCategories.Add(item.Str("category"));
        }
        var items = new List<Item>();
        foreach (UiValue item in itemsToAdd)
        {
            double population_minimum = item.Num("population_minimum");
            bool hasEnoughPop = joined_population == 0 || joined_population >= population_minimum;

            string reference = item.Str("ref", null);
            UiValue stockValue = reference != null ? extra_purchasable_stock?.Get(reference) : current_stock?.Get(item.Str("stock_key"));
            double? stock = stockValue is { Kind: UiValueKind.Number } ? stockValue.Number : null;
            bool canBuy = telecrystals >= item.Num("cost") && (stock == null || stock > 0);
            bool lockOthers = item.Truthy("lock_other_purchases");
            string costOverride = item.Str("cost_override_string");
            items.Add(new Item(
                item.Str("id"),
                item.Str("name"),
                item.Str("icon", null),
                item.Str("icon_state", null),
                item.Str("category"),
                item.Str("desc"),
                lockOthers,
                costOverride.Length > 0 ? costOverride : TguiFormat.JsNumber(item.Num("cost")) + " TC",
                "This item is not cleared for operations performed against stations crewed by fewer than " + TguiFormat.JsNumber(population_minimum) + " people.",
                !hasEnoughPop,
                !canBuy || !hasEnoughPop || (lockOthers && purchased_items > 0),
                reference));
        }

        int currentTab = c.UseState(TabKey, 0);
        c.WindowTheme = "syndicate";

        // tg: index.tsx:240  the tabs (Primary Objectives and Market, when there are primary objectives) and the Lock button
        var header = new UiNode(UiKind.Stack) { Key = "header", Fill = true };
        if (primary_objectives != null)
        {
            header.With(new UiNode(UiKind.StackItem) { Key = "tabs", Grow = 1 }.With(new UiNode(UiKind.Tabs) { Key = "tabs", Fluid = true }.With(
                new UiNode(UiKind.Tab) { Key = "tab:objectives", Icon = "star", Text = "Primary Objectives", Selected = currentTab == 0, OnClick = () => c.SetState(TabKey, 0) },
                new UiNode(UiKind.Tab) { Key = "tab:market", Icon = "store", Text = "Market", Selected = currentTab == 2, OnClick = () => c.SetState(TabKey, 2) })));
        }
        if (lockable)
        {
            header.With(new UiNode(UiKind.StackItem) { Key = "lock" }.With(
                new UiNode(UiKind.Button) { Key = "lock", Icon = "lock", Color = "transparent", TextAlign = "center", Text = "Lock", OnClick = () => c.Act("lock") }));
        }

        var body = new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 };
        if (currentTab == 0 && primary_objectives != null)
            body.With(PrimaryObjectiveMenu(c, primary_objectives, can_renegotiate));
        else
        {
            body.With(GenericUplink(c, TguiFormat.JsNumber(telecrystals) + " TC", allCategories, items));
            // tg: index.tsx:300  <Dimmer>SHOP LOCKED</Dimmer> over the market
            if (shop_locked && !debug)
            {
                body.With(new UiNode(UiKind.Modal) { Key = "locked" }.With(
                    new UiNode(UiKind.Box) { Key = "locked", Color = "red", FontSize = "36px", Text = "SHOP LOCKED" }));
            }
        }

        return c.Window(700, 600, null,
            new UiNode(UiKind.Stack) { Key = "uplink", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "top" }.With(new UiNode(UiKind.Section) { Key = "top" }.With(header)),
                body));
    }

    // tg: Uplink/GenericUplink.tsx:25  GenericUplink: the currency (buy raw TC), the compact toggle, the search, the categories and the items
    internal static UiNode GenericUplink(TguiContext c, string currency, List<string> categories, List<Item> allItems, Action<Item> handleBuy = null)
    {
        string searchText = c.UseState(SearchKey, "");
        string selectedCategory = c.UseState(CategoryKey, categories.FirstOrDefault());
        bool compactMode = c.UseState(CompactKey, false);
        List<Item> items = allItems.Where(value => searchText.Length == 0
            ? value.Category == selectedCategory
            : value.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        var tabs = new UiNode(UiKind.Tabs) { Key = "categories", Vertical = true, Fill = true };
        foreach (string category in categories)
        {
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "category:" + category,
                Text = category,
                Selected = category == selectedCategory,
                OnClick = () =>
                {
                    c.SetState(CategoryKey, category);
                    if (searchText.Length > 0)
                        c.SetState(SearchKey, "");
                },
            });
        }

        var left = new UiNode(UiKind.Stack) { Key = "left", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "currency" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "buy_raw_tc", Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "buy_raw_tc", Bold = true, Fluid = true, TextAlign = "center", Text = currency, OnClick = () => c.Act("buy_raw_tc"),
                }),
                new UiNode(UiKind.StackItem) { Key = "compact" }.With(new UiNode(UiKind.Button)
                {
                    Key = "compact", Fluid = true, TextAlign = "center",
                    Icon = compactMode ? "maximize" : "minimize",
                    Tooltip = compactMode ? "Detailed view" : "Compact view",
                    OnClick = () => c.SetState(CompactKey, !compactMode),
                }))),
            new UiNode(UiKind.StackItem) { Key = "search" }.With(new UiNode(UiKind.Input)
            {
                Key = "search", AutoFocus = true, Fluid = true, TextValue = searchText, Tooltip = "Search...",
                OnInput = value => c.SetState(SearchKey, value),
            }),
            new UiNode(UiKind.StackItem) { Key = "categories", Grow = 1 }.With(tabs));

        UiNode right = items.Count == 0
            ? new UiNode(UiKind.NoticeBox) { Key = "empty", Text = searchText.Length == 0 ? "No items in this category." : "No results found." }
            : ItemList(c, searchText.Length > 0 || compactMode, items, handleBuy ?? (item => HandleBuy(c, item)));

        return new UiNode(UiKind.Stack) { Key = "generic", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "left", Width = "160px" }.With(left),
            new UiNode(UiKind.StackItem) { Key = "right", Grow = 1 }.With(right));
    }

    // tg: Uplink/GenericUplink.tsx:156  ItemList: a section per item with its picture, name, cost button and description
    private static UiNode ItemList(TguiContext c, bool compactMode, List<Item> items, Action<Item> handleBuy)
    {
        var list = new UiNode(UiKind.Stack) { Key = "items", Vertical = true };
        int index = 0;
        foreach (Item item in items)
        {
            Item bought = item;
            var buy = new UiNode(UiKind.Button) { Key = "buy", Disabled = item.Disabled, Text = item.Cost, OnClick = () => handleBuy(bought) };
            var picture = new UiNode(UiKind.Box)
            {
                Key = "icon", DmIcon = item.Icon, DmIconState = item.IconState,
                Width = compactMode ? "32px" : "64px", Height = compactMode ? "32px" : "64px",
            };
            UiNode content;
            if (compactMode)
            {
                var name = new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, Opacity = item.InsufficientPopulation ? 0.5 : 1 };
                name.With(item.InsufficientPopulation
                    ? new UiNode(UiKind.Box) { Key = "name", Bold = true, Icon = "lock", Text = item.Name, Tooltip = item.PopulationTooltip }
                    : new UiNode(UiKind.Box) { Key = "name", Bold = true, Text = item.Name });
                content = new UiNode(UiKind.Stack) { Key = "row", Align = "center" }.With(
                    name,
                    new UiNode(UiKind.StackItem) { Key = "info" }.With(new UiNode(UiKind.Icon) { Key = "info", Icon = "info-circle", Tooltip = DescText(item) }),
                    new UiNode(UiKind.StackItem) { Key = "buy" }.With(buy));
            }
            else
            {
                var section = new UiNode(UiKind.Section) { Key = "item", Title = item.Name, Buttons = buy };
                if (item.InsufficientPopulation)
                    section.With(new UiNode(UiKind.Box) { Key = "population", Opacity = 0.5, Icon = "lock", Text = item.PopulationTooltip, Wrap = true });
                section.With(new UiNode(UiKind.Box) { Key = "desc", Opacity = 0.75 }.With(Desc(item)));
                content = section;
            }
            list.With(new UiNode(UiKind.StackItem) { Key = "item:" + index++ }.With(
                new UiNode(UiKind.Section) { Key = "item:" + item.Name }.With(new UiNode(UiKind.Stack) { Key = "item" }.With(
                    new UiNode(UiKind.StackItem) { Key = "picture" }.With(picture),
                    new UiNode(UiKind.StackItem) { Key = "content", Grow = 1 }.With(content)))));
        }
        return new UiNode(UiKind.Section) { Key = "list", Fill = true, Scrollable = true }.With(list);
    }

    // tg: index.tsx:196  the item's desc: its description, and a notice when it locks the other purchases
    private static UiNode Desc(Item item)
    {
        var desc = new UiNode(UiKind.Fragment) { Key = "desc" }.With(new UiNode(UiKind.Box) { Key = "text", Text = item.Desc, Wrap = true });
        if (item.LockOthers)
            desc.With(new UiNode(UiKind.NoticeBox) { Key = "lock", Text = LockNotice });
        return desc;
    }

    private static string DescText(Item item) => item.LockOthers ? item.Desc + "\n" + LockNotice : item.Desc;

    private const string LockNotice = "Taking this item will lock you from further purchasing from the marketplace. Additionally, if you have already purchased an item, you will not be able to purchase this.";

    // tg: index.tsx:285  handleBuy: a listed item by its path, a sale by its ref
    private static void HandleBuy(TguiContext c, Item item)
    {
        if (item.Ref == null)
            c.Act("buy", ("path", item.Id));
        else
            c.Act("buy", ("ref", item.Ref));
    }

    // tg: Uplink/PrimaryObjectiveMenu.tsx:11  PrimaryObjectiveMenu
    private static UiNode PrimaryObjectiveMenu(TguiContext c, IReadOnlyList<UiValue> primary_objectives, bool can_renegotiate)
    {
        var objectives = new UiNode(UiKind.Stack) { Key = "objectives", Vertical = true };
        int index = 0;
        foreach (UiValue prim_obj in primary_objectives)
            objectives.With(new UiNode(UiKind.StackItem) { Key = "objective:" + index++ }.With(ObjectiveElement(prim_obj.Str("task_name"), prim_obj.Str("task_text"))));

        var section = new UiNode(UiKind.Section) { Key = "primary", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Box) { Key = "welcome", Bold = true, FontSize = "1.2em", Color = "green", TextAlign = "center", Text = "WELCOME, AGENT." },
            new UiNode(UiKind.Box) { Key = "intro", Bold = true, FontSize = "1.2em", TextAlign = "center", Text = "Your Primary Objectives are as follows. Complete these at all costs." },
            objectives);
        if (can_renegotiate)
        {
            section.With(new UiNode(UiKind.Box) { Key = "renegotiate", TextAlign = "center" }.With(new UiNode(UiKind.Button)
            {
                Key = "renegotiate", Text = "Renegotiate Contract",
                Tooltip = "Replace your existing primary objectives with a custom one. This action can only be performed once.",
                OnClick = () => c.Act("renegotiate_objectives"),
            }));
        }
        section.With(
            new UiNode(UiKind.Box) { Key = "version", FontSize = "0.8em", TextAlign = "center", Text = "SyndOS Version 3.17" },
            new UiNode(UiKind.Box) { Key = "secure", FontSize = "0.8em", Color = "green", TextAlign = "center", Text = "Connection Secure" });
        return section;
    }

    // tg: Uplink/ObjectiveElement.tsx:16  ObjectiveElement: the title bar (Uplink.scss's gold gradient) over the content (dark)
    private static UiNode ObjectiveElement(string name, string description)
    {
        return new UiNode(UiKind.Stack) { Key = "element", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "titlebar", Background = "#9b7d45" }.With(new UiNode(UiKind.Box) { Key = "name", Bold = true, Text = name }),
            new UiNode(UiKind.StackItem) { Key = "content", Background = "rgba(0, 0, 0, 0.4)" }.With(new UiNode(UiKind.Box) { Key = "description", Text = description, Wrap = true }));
    }
}
