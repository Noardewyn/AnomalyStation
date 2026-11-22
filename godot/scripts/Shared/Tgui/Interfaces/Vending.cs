using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Vending.tsx and common/LayoutToggle.tsx (gaps package S2,
// D291). The window reads tg's ui_static_data and ui_data of a vending machine (vending/vendor/ui_data.dm): the products, premium and
// contraband records with their categories, the stock, the buyer's account and the ad, and sends tg's acts, vend and select_colors (and
// LayoutToggle's change_ui_state).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// The products' pictures are their tg icon and state (tgui's DmIcon); a GAGS product comes with its client-composed GAGS name (DV44's way,
// ui_data.cs). tgui reads the layout from the player's preference (config.interface.layout), which the port sends as null: as in tg with no
// layout, the products are a grid and the toggle offers the grid; a click switches the window to the list.

public static class Vending
{
    // tg: common/LayoutToggle.tsx:12
    private const string LayoutGrid = "grid", LayoutList = "list";

    // tg: Vending.tsx:399
    private static readonly Dictionary<string, string> CategoryColors = new(StringComparer.Ordinal)
    {
        ["Contraband"] = "red",
        ["Premium"] = "yellow",
    };

    // tg: Vending.tsx:72
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool allProductsFree = data.Truthy("all_products_free");
        string ad = data.Str("ad");
        IReadOnlyList<KeyValuePair<string, UiValue>> categories = data.Obj("categories");

        string selectedCategory = c.UseState("selectedCategory", categories.Count > 0 ? categories[0].Key : null);
        string stockSearch = c.UseState("stockSearch", "");
        Func<UiValue, bool> stockSearchFn = TguiFormat.CreateSearch<UiValue>(stockSearch, item => item.Str("name"));

        List<UiValue> inventory = data.Arr("product_records").Concat(data.Arr("coin_records")).ToList();
        if (data.Truthy("extended_inventory"))
            inventory.AddRange(data.Arr("hidden_records"));

        // Just in case we still have undefined values in the list
        inventory = inventory.Where(item => item != null && item.Kind != UiValueKind.Null).ToList();

        if (stockSearch.Length >= 2)
            inventory = inventory.Where(stockSearchFn).ToList();

        // tg: :101  the categories that have a product in the inventory
        var filteredCategories = categories.Where(category => inventory.Any(product => product.Get("category") != null && product.Str("category") == category.Key)).ToList();

        var stack = new UiNode(UiKind.Stack) { Key = "vending", Fill = true, Vertical = true };
        if (!allProductsFree)
            stack.With(new UiNode(UiKind.StackItem).With(UserDetails(data)));
        if (ad.Length > 0)
            stack.With(new UiNode(UiKind.StackItem).With(AdSection(ad)));
        stack.With(new UiNode(UiKind.StackItem) { Grow = 1 }.With(ProductDisplay(c, inventory, stockSearch, selectedCategory)));
        if (stockSearch.Length < 2 && filteredCategories.Count > 1)
            stack.With(new UiNode(UiKind.StackItem).With(CategorySelector(c, filteredCategories, selectedCategory)));

        // tg: :115  <Window width={431} height={635}>
        return c.Window(431, 635, null, stack);
    }

    /** Displays user details if an ID is present and the user is on the station */
    // tg: Vending.tsx:152
    internal static UiNode UserDetails(UiValue data)
    {
        UiValue user = data.Get("user");
        bool hasUser = user != null && user.Kind == UiValueKind.Object;
        string userName = hasUser ? user.Str("name") : "";
        return new UiNode(UiKind.NoticeBox) { Key = "user", Color = hasUser ? "blue" : null }.With(
            new UiNode(UiKind.Stack) { Align = "center" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Icon = "id-card", Size = 1.5 }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box)
                {
                    Text = hasUser ? (userName.Length > 0 ? userName : "Unknown") + " | " + user.Str("job") : "No ID detected! Contact the Head of Personnel.",
                })));
    }

    // tg: Vending.tsx:174
    private static UiNode AdSection(string adDisplay)
    {
        return new UiNode(UiKind.NoticeBox) { Key = "ad", Color = "yellow" }.With(
            new UiNode(UiKind.Stack) { Align = "center" }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = adDisplay })));
    }

    /** Displays  products in a section, with user balance at top */
    // tg: Vending.tsx:187
    private static UiNode ProductDisplay(TguiContext c, List<UiValue> inventory, string stockSearch, string selectedCategory)
    {
        UiValue data = c.Data;
        bool allProductsFree = data.Truthy("all_products_free");
        UiValue user = data.Get("user") is { Kind: UiValueKind.Object } u ? u : null;
        string currencyName = data.Str("displayed_currency_name");
        // tg: :205  useState(getLayoutState(LAYOUT.Grid)): the player's layout preference, null in the port
        string toggleLayout = c.UseState<string>("layout", null);
        bool fluid = toggleLayout == LayoutList;

        var buttons = new UiNode(UiKind.Stack) { Key = "buttons" };
        if (!allProductsFree && user != null)
        {
            buttons.With(new UiNode(UiKind.StackItem) { FontSize = "16px", Color = "green" }.With(
                new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(user.Num("cash")) + currencyName, FontSize = "16px", Color = "green" },
                new UiNode(UiKind.Icon) { Icon = data.Str("displayed_currency_icon"), Color = "gold" }));
        }
        buttons.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
            {
                Key = "search",
                Tooltip = "Search...",
                TextValue = stockSearch,
                OnInput = value => c.SetState("stockSearch", value),
            }),
            LayoutToggle(c, toggleLayout));

        IEnumerable<UiValue> shown = inventory.Where(product =>
            stockSearch.Length == 0 && product.Get("category") != null ? product.Str("category") == selectedCategory : true);

        var section = new UiNode(UiKind.Section) { Key = "products", Fill = true, Scrollable = true, Title = "Products", Buttons = buttons };
        // tgui-core's non-fluid ImageButtons are inline blocks that wrap; the fluid ones stand one per row
        UiNode products = fluid ? new UiNode(UiKind.Box) { Key = "list" } : new UiNode(UiKind.Stack) { Key = "grid", Wrap = true };
        foreach (UiValue product in shown)
        {
            UiValue stock = data.Get("stock")?.Get(product.Str("path"));
            products.With(Product(c, product, stock, fluid));
        }
        return section.With(products);
    }

    /**
     * An individual listing for an item.
     */
    // tg: Vending.tsx:266
    private static UiNode Product(TguiContext c, UiValue product, UiValue productStock, bool fluid)
    {
        UiValue data = c.Data;
        bool allProductsFree = data.Truthy("all_products_free");
        UiValue user = data.Get("user") is { Kind: UiValueKind.Object } u ? u : null;

        bool colorable = product.Truthy("colorable");
        bool free = allProductsFree || (productStock?.Truthy("free") ?? false) || product.Num("price") == 0;
        bool discount = !product.Truthy("premium") && (user != null && (user.Truthy("is_captain_card") || data.Str("department") == user.Str("department")));
        double remaining = productStock?.Num("amount") ?? 0;
        double redPrice = Math.Round(product.Num("price") * data.Num("jobDiscount"), MidpointRounding.AwayFromZero);
        bool disabled = remaining == 0
            || (!allProductsFree && user == null)
            || (!free && user != null && (discount ? redPrice : product.Num("price")) > user.Num("cash"));

        string name = TguiFormat.CapitalizeAll(product.Str("name"));
        string reference = product.Str("ref");
        string icon = product.Str("icon");
        var button = new UiNode(UiKind.ImageButton)
        {
            Key = "product:" + product.Str("path"),
            DmIcon = icon.Length > 0 ? icon : null,
            DmIconState = icon.Length > 0 ? product.Str("icon_state") : null,
            Disabled = disabled,
            Buttons = colorable ? ProductColorSelect(c, disabled, product, fluid) : null,
            // tg: :297  act('vend', { ref: product.ref, premium: !!product.premium })
            OnClick = () => c.Act("vend", ("ref", reference), ("premium", product.Truthy("premium"))),
        };

        UiNode price = ProductPrice(data, discount, free, product, redPrice);
        if (fluid)
        {
            // tg: :335  ProductList: <ImageButton fluid imageSize={32}>
            button.Fluid = true;
            button.Size = 1;
            return button.With(new UiNode(UiKind.Stack) { TextAlign = "right", Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Grow = 1, TextAlign = "left" }.With(new UiNode(UiKind.Box) { Text = name }),
                new UiNode(UiKind.StackItem) { Width = 3.5 }.With(new UiNode(UiKind.Box)
                {
                    Text = TguiFormat.JsNumber(remaining) + " left",
                    FontSize = 0.8,
                    Color = "rgba(255, 255, 255, 0.5)",
                }),
                new UiNode(UiKind.StackItem) { Width = 3.5 }.With(price)));
        }

        // tg: :315  ProductGrid: <ImageButton tooltip={capitalizeAll(product.name)} buttonsAlt={price, x remaining}>
        button.Tooltip = name;
        button.ButtonsAlt = new UiNode(UiKind.Stack) { FontSize = 0.8 }.With(
            new UiNode(UiKind.StackItem) { Grow = 1, TextAlign = "left" }.With(price),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "x" + TguiFormat.JsNumber(remaining), Color = "lightgray", FontSize = 0.8 }));
        return button.With(new UiNode(UiKind.Box) { Text = name });
    }

    /**
     * In the case of customizable items, ie: shoes,
     * this displays a color wheel button that opens another window.
     */
    // tg: Vending.tsx:372
    private static UiNode ProductColorSelect(TguiContext c, bool disabled, UiValue product, bool fluid)
    {
        string reference = product.Str("ref");
        return new UiNode(UiKind.Button)
        {
            Width = fluid ? "32px" : "20px",
            Icon = "palette",
            Color = "transparent",
            Tooltip = "Change color",
            Disabled = disabled, // tg: pointerEvents none and opacity 0.5 when disabled
            OnClick = () => c.Act("select_colors", ("ref", reference)),
        };
    }

    /** The main button to purchase an item. */
    // tg: Vending.tsx:399
    private static UiNode ProductPrice(UiValue data, bool discount, bool free, UiValue product, double redPrice)
    {
        string standardPrice = TguiFormat.JsNumber(product.Num("price"));
        if (free)
            standardPrice = "FREE";
        else if (discount)
            standardPrice = TguiFormat.JsNumber(redPrice);
        return new UiNode(UiKind.Box)
        {
            Text = standardPrice + (!free ? data.Str("displayed_currency_name") : ""),
            FontSize = 0.85,
            Color = "gold",
        };
    }

    // tg: Vending.tsx:418
    private static UiNode CategorySelector(TguiContext c, List<KeyValuePair<string, UiValue>> categories, string selectedCategory)
    {
        var section = new UiNode(UiKind.Section) { Key = "categories" };
        var row = new UiNode(UiKind.Stack) { Wrap = true };
        foreach ((string name, UiValue category) in categories)
        {
            row.With(new UiNode(UiKind.Button)
            {
                Key = "category:" + name,
                Text = name,
                Selected = name == selectedCategory,
                Color = CategoryColors.TryGetValue(name, out string color) ? color : null,
                Icon = category.Str("icon"),
                OnClick = () => c.SetState("selectedCategory", name),
            });
        }
        return section.With(row);
    }

    /**
     * Allows the user to toggle between grid and list layouts, if preference on Default value.
     * Otherwise it'll be controlled by preferences.
     */
    // tg: common/LayoutToggle.tsx:27
    private static UiNode LayoutToggle(TguiContext c, string state)
    {
        return new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
        {
            Key = "layout",
            Icon = state == LayoutGrid ? "list" : "border-all",
            Tooltip = state == LayoutGrid ? "View as List" : "View as Grid",
            OnClick = () =>
            {
                string newState = state == LayoutGrid ? LayoutList : LayoutGrid;
                c.SetState("layout", newState);
                c.Act("change_ui_state", ("new_state", newState));
            },
        });
    }
}
