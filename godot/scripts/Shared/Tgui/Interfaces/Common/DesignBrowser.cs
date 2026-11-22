using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Fabrication/DesignBrowser.tsx (silicons SI3, D326: the
// exosuit fabricator's designs; gaps RD1, D333: the Fabricator and Autolathe windows reuse it). Engine-free (compiled into the tests
// too). useSharedState is the window's state. The kit cannot scroll a section into view, so a subcategory's tab (tg: scrollIntoView)
// only names it; its section follows in the list. The VirtualList is the scrollable section; the kit's input has no placeholder. The
// busy Dimmer is the kit's Modal (over the window, where tg dims the designs' column).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class DesignBrowser
{
    /// <summary>tg: DesignBrowser.tsx:73  the meta-category that renders every design.</summary>
    public const string AllCategory = "All Designs";

    /// <summary>tg: DesignBrowser.tsx:78  the meta-category of the designs with no category.</summary>
    private const string Uncategorized = "/Uncategorized";

    /// <summary>tg: DesignBrowser.tsx:112  the meta-categories the browser does not render.</summary>
    private static readonly HashSet<string> BlacklistedCategories = new(StringComparer.Ordinal) { "initial", "core", "hacked" };

    /// <summary>tg: DesignBrowser.tsx:83  a category of the tree: its designs and its subcategories.</summary>
    public sealed class Category
    {
        public string Title;
        public string AnchorKey;
        public readonly Dictionary<string, UiValue> Descendants = new(StringComparer.Ordinal);
        public readonly List<UiValue> Children = new();
        public readonly Dictionary<string, Category> Subcategories = new(StringComparer.Ordinal);
    }

    // tg: DesignBrowser.tsx:123
    public static UiNode Render(TguiContext c, IReadOnlyList<UiValue> designs, Func<UiValue, UiNode> buildRecipeElement, Func<Category, UiNode> categoryButtons = null,
        bool busy = false)
    {
        string selectedCategory = c.UseState("selected_category", AllCategory);
        string searchText = c.UseState("search_text", "");

        void OnCategorySelected(string newCategory)
        {
            if (newCategory == selectedCategory)
                return;
            c.SetState("selected_category", newCategory);
            c.SetState("search_text", "");
        }

        // Build a root category from the designs.
        var root = new Category { Title = AllCategory, AnchorKey = AllCategory.Replace(" ", "") };

        // Sort every design into a single category in the tree.
        foreach (UiValue design in designs)
        {
            IReadOnlyList<UiValue> designCategories = design.Arr("categories");
            IEnumerable<string> categories = designCategories.Count == 0 ? new[] { Uncategorized } : designCategories.Select(v => v.Text ?? "");
            string path = design.Str("path");
            foreach (string category in categories)
            {
                // If the category is a blacklisted meta-category, skip it entirely.
                if (BlacklistedCategories.Contains(category))
                    continue;

                // Categories are slash-delimited; we always lead with a slash, so the first group is always empty.
                var nodes = new Queue<string>(category.Split('/').Skip(1));

                // Find where this goes, and put it there.
                Category parent = root;
                while (nodes.Count > 0)
                {
                    parent.Descendants[path] = design;
                    string node = nodes.Dequeue();
                    if (!parent.Subcategories.TryGetValue(node, out Category sub))
                        parent.Subcategories[node] = sub = new Category { Title = node, AnchorKey = node.Replace(" ", "") };
                    parent = sub;
                }

                // This is our leaf.
                parent.Descendants[path] = design;
                parent.Children.Add(design);
            }
        }

        var tabs = new UiNode(UiKind.Tabs) { Key = "categories", Vertical = true }.With(
            new UiNode(UiKind.Tab)
            {
                Key = "tab:" + AllCategory,
                Selected = selectedCategory == AllCategory,
                Text = "All Designs (" + root.Descendants.Count + ")",
                OnClick = () => OnCategorySelected(AllCategory),
            });
        foreach (Category category in root.Subcategories.Values.OrderBy(cat => cat.Title, StringComparer.Ordinal))
            BrowserTab(tabs, category, 0, 3, selectedCategory, OnCategorySelected);

        // Left column
        var left = new UiNode(UiKind.StackItem) { Key = "left", Width = "200px" }.With(
            new UiNode(UiKind.Section) { Key = "categoriesSection", Title = "Categories", Fill = true, Scrollable = true }.With(tabs));

        UiNode list;
        if (searchText.Length > 0)
        {
            list = new UiNode(UiKind.Fragment) { Key = "results" }.With(root.Descendants.Values
                .OrderBy(design => design.Str("name"), StringComparer.Ordinal)
                .Where(design => (design.Str("name") ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .Select(buildRecipeElement).ToArray());
        }
        else if (selectedCategory == AllCategory)
        {
            list = new UiNode(UiKind.Fragment) { Key = "all" }.With(root.Descendants.Values
                .OrderBy(design => design.Str("name"), StringComparer.Ordinal)
                .Select(buildRecipeElement).ToArray());
        }
        else
        {
            list = root.Subcategories.TryGetValue(selectedCategory, out Category selected)
                ? CategoryView(selected, 0, buildRecipeElement, categoryButtons)
                : new UiNode(UiKind.Fragment) { Key = "none" };
        }

        // Right column
        string title = searchText.Length > 0 ? "Results for \"" + searchText + "\"" : selectedCategory == AllCategory ? "All Designs" : selectedCategory;
        var right = new UiNode(UiKind.StackItem) { Key = "right", Grow = 1 }.With(
            new UiNode(UiKind.Section) { Key = "designs", Title = title, Fill = true }.With(
                new UiNode(UiKind.Stack) { Key = "column", Vertical = true, Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "search" }.With(new UiNode(UiKind.Input)
                    {
                        Key = "searchBar",
                        Fluid = true,
                        TextValue = searchText,
                        OnInput = value => c.SetState("search_text", value ?? ""),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(
                        new UiNode(UiKind.Section) { Key = "listSection", Fill = true, Scrollable = true }.With(list)),
                    // tg: DesignBrowser.tsx:323  {!!busy && <Dimmer><Icon name="cog" spin />{' Building items...'}</Dimmer>}
                    busy
                        ? new UiNode(UiKind.Modal) { Key = "busy" }.With(
                            new UiNode(UiKind.Box) { Key = "building", FontSize = "2em", TextAlign = "center", Icon = "cog", Spin = true, Text = " Building items..." })
                        : null)));

        return new UiNode(UiKind.Stack) { Key = "designBrowser", Fill = true }.With(left, right);
    }

    // tg: DesignBrowser.tsx:345  DesignBrowserTab: a top-level category selects; a deeper one names its section (see the header)
    private static void BrowserTab(UiNode tabs, Category category, int depth, int maxDepth, string selectedCategory, Action<string> setSelectedCategory)
    {
        tabs.With(new UiNode(UiKind.Tab)
        {
            Key = "tab:" + depth + ":" + category.Title,
            Selected = depth == 0 && selectedCategory == category.Title,
            Text = new string(' ', depth * 2) + category.Title + (depth == 0 ? " (" + category.Descendants.Count + ")" : ""),
            OnClick = depth == 0 ? () => setSelectedCategory(category.Title) : null,
        });
        if (depth < maxDepth && category.Subcategories.Count > 0 && (depth > 0 || selectedCategory == category.Title))
        {
            foreach (Category subcategory in category.Subcategories.Values.OrderBy(cat => cat.Title, StringComparer.Ordinal))
                BrowserTab(tabs, subcategory, depth + 1, maxDepth, selectedCategory, setSelectedCategory);
        }
    }

    /// <summary>
    /// A recipe's FabricatorRecipe__Icon and __Label in a row (a kit button lays its children over each other): the design's picture, a
    /// class of tg's "design" sheet (TguiSpritesheets.Design32, DV44), and its name. The fabricators' and the autolathe's windows share it.
    /// </summary>
    public static UiNode IconAndName(string icon, string name) =>
        new UiNode(UiKind.Stack) { Key = "iconAndName", Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Box) { Key = "pic", DmIcon = TguiSpritesheets.Design32, DmIconState = icon, Width = "32px", Height = "32px" }),
            new UiNode(UiKind.StackItem) { Key = "label" }.With(new UiNode(UiKind.Box) { Key = "name", Text = name }));

    // tg: DesignBrowser.tsx:466  CategoryView
    private static UiNode CategoryView(Category category, int depth, Func<UiValue, UiNode> buildRecipeElement, Func<Category, UiNode> categoryButtons)
    {
        var body = new UiNode(UiKind.Fragment) { Key = "body:" + category.AnchorKey }.With(
            category.Children.OrderBy(design => design.Str("name"), StringComparer.Ordinal).Select(buildRecipeElement).ToArray());
        foreach (string categoryName in category.Subcategories.Keys.OrderBy(k => k, StringComparer.Ordinal))
            body.With(CategoryView(category.Subcategories[categoryName], depth + 1, buildRecipeElement, categoryButtons));

        if (depth == 0 || category.Children.Count == 0)
            return body;

        return new UiNode(UiKind.Section)
        {
            Key = "section:" + category.AnchorKey,
            Title = category.Title,
            Buttons = categoryButtons?.Invoke(category),
        }.With(body);
    }
}
