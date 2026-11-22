using System;
using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CellularEmporium.tsx (gaps K1, D327): the changeling's
// cellular emporium, from /datum/cellular_emporium's ui_static_data and ui_data (cellular_emporium.dm:31, :59): the genetic points and
// the readapt button, the search, the compact toggle, the category tabs in tg's order and the abilities with their pictures (the
// changeling action background under the ability's icon), requirements, cost buttons and texts. The window's background picture is not
// drawn (the kit has no background image); the title's dna icon and count are the section's title text. Original copyright and license:
// tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CellularEmporium
{
    private const string SearchKey = "searchText";
    private const string CompactKey = "compactMode";
    private const string CategoryKey = "selectedCategory";
    private const string ActionsIcon = "icons/mob/actions/actions_changeling.dmi";

    // tg: CellularEmporium.tsx:24  Ability
    private sealed record Ability(string Name, string Desc, string Icon, string Helptext, string Path, double GeneticPointRequired,
        double AbsorbsRequired, double DnaRequired, string Category);

    // tg: CellularEmporium.tsx:16  CATEGORY_ORDER
    private static readonly string[] CategoryOrder = { "combat", "stealth", "utility", "stings" };

    // tg: CellularEmporium.tsx:49
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        List<Ability> abilities = data.Arr("abilities").Select(a => new Ability(a.Str("name"), a.Str("desc"), a.Str("icon"), a.Str("helptext"),
            a.Str("path"), a.Num("genetic_point_required"), a.Num("absorbs_required"), a.Num("dna_required"), a.Str("category"))).ToList();
        double can_readapt = data.Num("can_readapt");
        double genetic_points_count = data.Num("genetic_points_count");
        HashSet<string> owned_abilities = data.Arr("owned_abilities").Select(p => p.AsStr()).ToHashSet();
        double absorb_count = data.Num("absorb_count");
        double dna_count = data.Num("dna_count");

        string searchText = c.UseState(SearchKey, "");
        bool compactMode = c.UseState(CompactKey, false);

        // tg: CellularEmporium.tsx:64  the categories: tg's order first, then the others as they come
        List<string> allCategories = abilities.Select(a => a.Category).Distinct().ToList();
        List<string> sortedCategories = CategoryOrder.Where(allCategories.Contains).Concat(allCategories.Where(cat => !CategoryOrder.Contains(cat))).ToList();
        string selectedCategory = c.UseState(CategoryKey, sortedCategories.FirstOrDefault() ?? "utility");

        // tg: CellularEmporium.tsx:73  filteredItems: the search over name, desc and helptext, else the category; by name
        List<Ability> filteredItems = (searchText.Length > 0
                ? abilities.Where(item => string.Join(" ", item.Name, item.Desc, item.Helptext).Contains(searchText, StringComparison.OrdinalIgnoreCase))
                : abilities.Where(item => item.Category == selectedCategory))
            .OrderBy(a => a.Name, StringComparer.CurrentCulture).ToList();

        // tg: CellularEmporium.tsx:105  the readapt button in the title
        var readapt = new UiNode(UiKind.Button)
        {
            Key = "readapt", Icon = "undo", Color = "good", Disabled = can_readapt == 0,
            Tooltip = can_readapt != 0 ? "Un-evolve all abilities and refund genetic points." : "Need more DNA to readapt.",
            Text = "Readapt (" + TguiFormat.JsNumber(can_readapt) + ")",
            OnClick = () => c.Act("readapt"),
        };

        var tabs = new UiNode(UiKind.Tabs) { Key = "categories", Vertical = true, Fill = true };
        foreach (string category in sortedCategories)
        {
            string picked = category;
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "category:" + category,
                Text = category.Length > 0 ? char.ToUpperInvariant(category[0]) + category[1..] : category,
                Selected = category == selectedCategory,
                OnClick = () =>
                {
                    c.SetState(CategoryKey, picked);
                    if (searchText.Length > 0)
                        c.SetState(SearchKey, "");
                },
            });
        }

        // tg: CellularEmporium.tsx:125  the search, the compact toggle and the tabs
        var left = new UiNode(UiKind.Stack) { Key = "left", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "search" }.With(new UiNode(UiKind.Input)
            {
                Key = "search", AutoFocus = true, Fluid = true, TextValue = searchText, Tooltip = "Search...",
                OnInput = value => c.SetState(SearchKey, value),
            }),
            new UiNode(UiKind.StackItem) { Key = "compact" }.With(new UiNode(UiKind.Button)
            {
                Key = "compact", Fluid = true, TextAlign = "center",
                Icon = compactMode ? "maximize" : "minimize",
                Tooltip = compactMode ? "Detailed view" : "Compact view",
                OnClick = () => c.SetState(CompactKey, !compactMode),
            }),
            new UiNode(UiKind.StackItem) { Key = "categories", Grow = 1 }.With(tabs));

        // tg: CellularEmporium.tsx:166
        UiNode right = filteredItems.Count == 0
            ? new UiNode(UiKind.NoticeBox) { Key = "empty", Text = searchText.Length > 0 ? "No abilities found." : "No abilities in this category." }
            : ItemList(c, searchText.Length > 0 || compactMode, filteredItems, owned_abilities, genetic_points_count, absorb_count, dna_count);

        // tg: CellularEmporium.tsx:88  <Window width={900} height={520}>
        return c.Window(900, 520, null,
            new UiNode(UiKind.Section)
            {
                Key = "emporium", Fill = true, Title = TguiFormat.JsNumber(genetic_points_count) + " DNA", Color = "#DD66DD", Buttons = readapt,
            }.With(new UiNode(UiKind.Stack) { Key = "body", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "left", Width = "180px" }.With(left),
                new UiNode(UiKind.StackItem) { Key = "right", Grow = 1 }.With(right))));
    }

    // tg: CellularEmporium.tsx:208  ItemList
    private static UiNode ItemList(TguiContext c, bool compactMode, List<Ability> items, HashSet<string> owned_abilities, double genetic_points_count,
        double absorb_count, double dna_count)
    {
        string iconSize = compactMode ? "32px" : "64px";
        var list = new UiNode(UiKind.Stack) { Key = "items", Vertical = true };
        foreach (Ability ability in items)
        {
            Ability bought = ability;
            bool owned = owned_abilities.Contains(ability.Path);
            bool canAfford = !owned
                && ability.GeneticPointRequired <= genetic_points_count
                && ability.AbsorbsRequired <= absorb_count
                && ability.DnaRequired <= dna_count;

            // tg: CellularEmporium.tsx:229  requirementTooltip
            var requirements = new List<string> { TguiFormat.JsNumber(ability.GeneticPointRequired) + " DNA" };
            if (ability.AbsorbsRequired > 0)
                requirements.Add(TguiFormat.JsNumber(ability.AbsorbsRequired) + " absorptions");
            if (ability.DnaRequired > 0)
                requirements.Add(TguiFormat.JsNumber(ability.DnaRequired) + " DNA");
            string requirementTooltip = string.Join(", ", requirements);

            // tg: CellularEmporium.tsx:239  costDisplay
            string costDisplay = ability.DnaRequired > 0
                ? "Cost: " + TguiFormat.JsNumber(ability.DnaRequired) + " DNA"
                : "Cost: " + TguiFormat.JsNumber(ability.GeneticPointRequired) + " DNA";

            // tg: CellularEmporium.tsx:249  the changeling background under the ability's icon
            var picture = new UiNode(UiKind.Overlay) { Key = "picture", Width = iconSize, Height = iconSize }.With(
                new UiNode(UiKind.Box) { Key = "background", DmIcon = ActionsIcon, DmIconState = "bg_changeling", Width = iconSize, Height = iconSize },
                new UiNode(UiKind.Box) { Key = "icon", DmIcon = ActionsIcon, DmIconState = ability.Icon, Width = iconSize, Height = iconSize });

            UiNode content;
            if (compactMode)
            {
                // tg: CellularEmporium.tsx:283
                UiNode name = owned
                    ? new UiNode(UiKind.Box) { Key = "name", Bold = true, Color = "#44bd46", Icon = "check", Text = ability.Name }
                    : new UiNode(UiKind.Box) { Key = "name", Bold = true, Text = ability.Name };
                content = new UiNode(UiKind.Stack) { Key = "row", Align = "center" }.With(
                    new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, Opacity = owned ? 0.5 : 1 }.With(name),
                    new UiNode(UiKind.StackItem) { Key = "info" }.With(new UiNode(UiKind.Icon)
                    {
                        Key = "info", Icon = "info-circle", Tooltip = string.IsNullOrEmpty(ability.Helptext) ? ability.Desc : ability.Helptext,
                    }),
                    new UiNode(UiKind.StackItem) { Key = "buy" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "buy", Color = canAfford ? "average" : "bad", Disabled = owned || !canAfford, Tooltip = requirementTooltip, Text = costDisplay,
                        OnClick = () => HandleBuy(c, bought),
                    }));
            }
            else
            {
                // tg: CellularEmporium.tsx:322
                var buy = new UiNode(UiKind.Button)
                {
                    Key = "buy", Disabled = owned || !canAfford, Color = owned ? "good" : canAfford ? "average" : "bad", Tooltip = requirementTooltip,
                    Text = costDisplay, OnClick = () => HandleBuy(c, bought),
                };
                var section = new UiNode(UiKind.Section) { Key = "ability", Title = owned ? ability.Name + " (Owned)" : ability.Name, Buttons = buy }.With(
                    new UiNode(UiKind.Box) { Key = "desc", Opacity = 0.8, Text = ability.Desc, Wrap = true });
                if (!string.IsNullOrEmpty(ability.Helptext))
                    section.With(new UiNode(UiKind.Box) { Key = "helptext", Color = "#44bd46", Text = ability.Helptext, Wrap = true });
                content = section;
            }

            list.With(new UiNode(UiKind.StackItem) { Key = "ability:" + ability.Path }.With(
                new UiNode(UiKind.Section) { Key = "item:" + ability.Path }.With(new UiNode(UiKind.Stack) { Key = "item" }.With(
                    new UiNode(UiKind.StackItem) { Key = "picture" }.With(picture),
                    new UiNode(UiKind.StackItem) { Key = "content", Grow = 1 }.With(content)))));
        }
        return new UiNode(UiKind.Section) { Key = "list", Fill = true, Scrollable = true }.With(list);
    }

    // tg: CellularEmporium.tsx:84  handleBuy
    private static void HandleBuy(TguiContext c, Ability ability) => c.Act("evolve", ("path", ability.Path));
}
