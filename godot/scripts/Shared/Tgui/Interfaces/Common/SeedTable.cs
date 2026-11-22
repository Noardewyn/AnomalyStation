using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SeedTable.tsx (gaps package P2: the seed extractor's
// table; tg's botanical encyclopedia, an NTOS app, uses it too). Original copyright and license: tgstation, AGPL-3.0 (code). See
// ../tgstation/LICENSE.
//
// The header's sort boxes are buttons (the kit has no clickable Box), their tooltips the kit's; the trait, mutation, reagent, juice and
// distill tooltips are the kit's Floating popups (shown while the icon is hovered). The row's top border is the kit's table rule.

public static class SeedTable
{
    // tg: SeedTable.tsx:62  export const SeedTable = (props: SeedTableProps) => ...
    public static UiNode Render(TguiContext c, IReadOnlyList<UiValue> seeds, IReadOnlyList<UiValue> trait_db, double cycle_seconds, Func<UiValue, UiNode[]> renderActions = null)
    {
        string searchText = c.UseState("searchText", "");
        string sortField = c.UseState("sortField", "name");
        Func<UiValue, bool> search = TguiFormat.CreateSearch<UiValue>(searchText, item => item.Str("name"));
        IEnumerable<UiValue> seeds_filtered = searchText.Length > 0 ? seeds.Where(search) : seeds;
        // tg: SeedTable.tsx:69  sortBy(seeds_filtered, [(item) => item[sortField]]); a numeric field sorts by number, the name by text
        List<UiValue> seeds_sorted = sortField == "name"
            ? TguiFormat.SortBy(seeds_filtered, item => item.Str("name"))
            : seeds_filtered.OrderBy(item => item.Num(sortField)).ToList();
        if (sortField != "name")
            seeds_sorted.Reverse();

        var table = new UiNode(UiKind.Table) { Key = "seeds" };
        // tg: SeedTable.tsx:77  the header row: the search input and the sortable stat columns
        var header = new UiNode(UiKind.TableRow) { Key = "header", Bold = true }.With(
            new UiNode(UiKind.TableCell) { Key = "search" }.With(new UiNode(UiKind.Input)
            {
                Key = "searchInput",
                AutoFocus = true,
                Tooltip = "Search...",
                TextValue = searchText,
                Fluid = true,
                OnInput = value => c.SetState("searchText", value),
            }),
            new UiNode(UiKind.TableCell) { Key = "spacer1" },
            new UiNode(UiKind.TableCell) { Key = "spacer2" },
            SortCell(c, "potency", "PTN", "Potency: Determines product mass, reagent volume and strength of effects."),
            SortCell(c, "yield", "YLD", "Yield: The number of products gathered in a single harvest."),
            SortCell(c, "instability", "INS", "Instability: The likelihood of the plant to randomize stats or mutate. Affects quality of resulting food & drinks."),
            SortCell(c, "endurance", "END", "Endurance: The health pool of the plant that delays death. Improves quality of resulting food & drinks."),
            SortCell(c, "lifespan", "LFS", "Lifespan: The age at which the plant starts decaying. Improves quality of resulting food & drinks."),
            SortCell(c, "maturation", "MTR", "Maturation: The age required for the first harvest."),
            SortCell(c, "production", "PRD", "Production: The period of product regrowth."),
            new UiNode(UiKind.TableCell) { Key = "reset", TextAlign = "right" }.With(
                sortField != "name"
                    ? new[] { new UiNode(UiKind.Button) { Key = "resetSort", Icon = "refresh", Tooltip = "Reset sorting", OnClick = () => c.SetState("sortField", "name") } }
                    : Array.Empty<UiNode>()));
        table.With(header);

        foreach (UiValue item in seeds_sorted)
        {
            string key = item.Str("key");
            double amount = item.Num("amount");
            // tg: SeedTable.tsx:191  the trait, mutation, reagent, juice and distill icons
            var icons = new List<UiNode>();
            foreach (UiValue trait in item.Arr("traits"))
            {
                UiNode tip = TraitTooltip(trait.AsStr(), trait_db, key);
                if (tip != null)
                    icons.Add(tip);
            }
            IReadOnlyList<UiValue> mutatelist = item.Arr("mutatelist");
            if (mutatelist.Count > 0)
                icons.Add(new UiNode(UiKind.Icon) { Key = key + ":dna", Icon = "dna", Tooltip = "Mutates into: " + string.Join(", ", mutatelist.Select(m => m.AsStr())) });
            if (item.Arr("reagents").Count > 0)
                icons.Add(new UiNode(UiKind.Floating) { Key = key + ":reagents", Content = () => ReagentTooltip(item) }.With(new UiNode(UiKind.Icon) { Icon = "blender" }));
            if (item.Str("juice_name").Length > 0)
                icons.Add(new UiNode(UiKind.Icon) { Key = key + ":juice", Icon = "glass-water", Tooltip = "Juicing result: " + item.Str("juice_name") });
            if (item.Str("distill_reagent").Length > 0)
                icons.Add(new UiNode(UiKind.Icon) { Key = key + ":distill", Icon = "wine-bottle", Tooltip = "Ferments into: " + item.Str("distill_reagent") });

            table.With(new UiNode(UiKind.TableRow) { Key = "seed:" + key }.With(
                // tg: SeedTable.tsx:180  <DmIcon icon={item.icon} icon_state={item.icon_state} width="32px" height="32px" />
                new UiNode(UiKind.TableCell) { Key = "icon" }.With(new UiNode(UiKind.Box) { DmIcon = item.Str("icon"), DmIconState = item.Str("icon_state"), Width = "32px", Height = "32px" }),
                new UiNode(UiKind.TableCell) { Key = "name", Text = (amount > 0 ? TguiFormat.JsNumber(amount) + "x " : "") + item.Str("name") },
                new UiNode(UiKind.TableCell) { Key = "icons", TextAlign = "right" }.With(icons),
                new UiNode(UiKind.TableCell) { Key = "potency" }.With(Level(key + ":potency", item.Num("potency"), 100)),
                new UiNode(UiKind.TableCell) { Key = "yield" }.With(Level(key + ":yield", item.Num("yield"), 10)),
                new UiNode(UiKind.TableCell) { Key = "instability" }.With(Level(key + ":instability", item.Num("instability"), 100, reverse: true)),
                new UiNode(UiKind.TableCell) { Key = "endurance" }.With(Level(key + ":endurance", item.Num("endurance"), 100)),
                new UiNode(UiKind.TableCell) { Key = "lifespan", TextAlign = "center", Text = TguiFormat.FormatTime(item.Num("lifespan") * cycle_seconds * 10, "short") },
                new UiNode(UiKind.TableCell) { Key = "maturation", TextAlign = "center", Text = TguiFormat.FormatTime(item.Num("maturation") * cycle_seconds * 10, "short") },
                new UiNode(UiKind.TableCell) { Key = "production", TextAlign = "center", Text = TguiFormat.FormatTime(item.Num("production") * cycle_seconds * 10, "short") },
                new UiNode(UiKind.TableCell) { Key = "actions", TextAlign = "right" }.With(renderActions?.Invoke(item) ?? Array.Empty<UiNode>())));
        }

        var section = new UiNode(UiKind.Section) { Key = "seedtable" }.With(table);
        // tg: SeedTable.tsx:276
        if (seeds_sorted.Count == 0)
            section.With(new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No seeds found." });
        return section;
    }

    // tg: SeedTable.tsx:87  a header cell that sorts by its field on a click
    private static UiNode SortCell(TguiContext c, string field, string text, string tooltip) =>
        new UiNode(UiKind.TableCell) { Key = "sort:" + field }.With(new UiNode(UiKind.Button)
        {
            Key = "sortButton:" + field,
            Text = text,
            Tooltip = tooltip,
            TextAlign = "center",
            OnClick = () => c.SetState("sortField", field),
        });

    // tg: SeedTable.tsx:285  export const Level = (props) => <ProgressBar value maxValue ranges>{props.value}</ProgressBar>
    public static UiNode Level(string key, double value, double max, bool reverse = false) => new(UiKind.ProgressBar)
    {
        Key = key,
        Value = value,
        MinValue = 0,
        MaxValue = max,
        Text = TguiFormat.JsNumber(value),
        Ranges = reverse
            ? new[] { ("good", 0.0, max * 0.2), ("average", max * 0.2, max * 0.6), ("bad", max * 0.6, max) }
            : new[] { ("bad", 0.0, max * 0.2), ("good", max * 0.8, max) },
    };

    // tg: SeedTable.tsx:311  export const ReagentTooltip = (props) => <Table>...</Table>
    public static UiNode ReagentTooltip(UiValue item) => ReagentTooltip(item.Arr("reagents"), item.Arr("grind_results"), item.Num("potency"), item.Num("volume_mod"), item.Num("volume_units"));

    public static UiNode ReagentTooltip(IReadOnlyList<UiValue> reagents, IReadOnlyList<UiValue> grind_results, double potency, double volume_mod, double volume_units)
    {
        double rate_total = reagents.Sum(r => r.Num("rate"));
        var reagent_volumes = new List<double>();
        var reagent_percentages = new List<double>();
        foreach (UiValue reagent in reagents)
        {
            reagent_percentages.Add(reagent.Num("rate") / Math.Max(1, rate_total));
            reagent_volumes.Add(Math.Max(Math.Round(volume_units * (potency / 100) * (reagent.Num("rate") / Math.Max(1, rate_total)) * volume_mod, MidpointRounding.AwayFromZero), 1));
        }
        var table = new UiNode(UiKind.Table) { Key = "reagentTooltip" }.With(
            new UiNode(UiKind.TableRow) { Key = "head", Bold = true }.With(new UiNode(UiKind.TableCell) { Text = "Reagents on grind:" }));
        for (int i = 0; i < reagents.Count; i++)
        {
            table.With(new UiNode(UiKind.TableRow) { Key = "r" + i }.With(
                new UiNode(UiKind.TableCell) { Text = reagents[i].Str("name") },
                new UiNode(UiKind.TableCell) { TextAlign = "right", Text = TguiFormat.JsNumber(reagent_volumes[i]) + "u" },
                new UiNode(UiKind.TableCell) { TextAlign = "right", Text = (rate_total > 1 ? "~" : "") + TguiFormat.JsNumber(Math.Round(reagent_percentages[i] * 100, MidpointRounding.AwayFromZero)) + "%" }));
        }
        table.With(
            new UiNode(UiKind.TableRow) { Key = "total", Bold = true }.With(
                new UiNode(UiKind.TableCell) { Text = "Total" },
                new UiNode(UiKind.TableCell) { TextAlign = "right", Text = TguiFormat.JsNumber(Math.Round(reagent_volumes.Sum(), MidpointRounding.AwayFromZero)) + "u" },
                new UiNode(UiKind.TableCell) { TextAlign = "right", Text = TguiFormat.JsNumber(Math.Round(rate_total * 100, MidpointRounding.AwayFromZero)) + "%" }),
            new UiNode(UiKind.TableRow) { Key = "capacity", Bold = true }.With(
                new UiNode(UiKind.TableCell) { Text = "Capacity" },
                new UiNode(UiKind.TableCell) { TextAlign = "right", Text = TguiFormat.JsNumber(volume_units) + "u" },
                new UiNode(UiKind.TableCell)));
        if (grind_results.Count > 0)
        {
            table.With(new UiNode(UiKind.TableRow) { Key = "grindHead", Bold = true }.With(new UiNode(UiKind.TableCell) { Text = "Nutriments turn into:" }));
            for (int i = 0; i < grind_results.Count; i++)
                table.With(new UiNode(UiKind.TableRow) { Key = "g" + i }.With(new UiNode(UiKind.TableCell) { Text = grind_results[i].AsStr() }));
        }
        return table;
    }

    // tg: SeedTable.tsx:398  export const TraitTooltip = (props) => trait_db.find(t => t.path === props.path) ...
    public static UiNode TraitTooltip(string path, IReadOnlyList<UiValue> trait_db, string keyPrefix, bool grafting = false, bool removable = false)
    {
        UiValue trait = trait_db.FirstOrDefault(t => t.Str("path") == path);
        if (trait == null)
            return null;
        UiNode Content()
        {
            var table = new UiNode(UiKind.Table) { Key = "traitTooltip" };
            if (grafting)
                table.With(new UiNode(UiKind.TableRow) { Key = "graft" }.With(new UiNode(UiKind.TableCell) { Text = "Graft gains the following trait:" }));
            table.With(new UiNode(UiKind.TableRow) { Key = "name", Bold = true }.With(new UiNode(UiKind.TableCell).With(
                new UiNode(UiKind.Icon) { Icon = trait.Str("icon") },
                new UiNode(UiKind.Box) { Text = trait.Str("name") })));
            if (removable)
                table.With(new UiNode(UiKind.TableRow) { Key = "removable" }.With(new UiNode(UiKind.TableCell) { Text = "Removable trait." }));
            if (trait.Str("description").Length > 0)
                table.With(new UiNode(UiKind.TableRow) { Key = "desc" }.With(new UiNode(UiKind.TableCell) { Text = trait.Str("description") }));
            return table;
        }
        return new UiNode(UiKind.Floating) { Key = keyPrefix + ":trait:" + path, Content = Content }.With(
            new UiNode(UiKind.Icon) { Icon = grafting ? "scissors" : trait.Str("icon") });
    }
}
