using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PlantAnalyzer/ (index.tsx, Tray.tsx, Seed.tsx, Graft.tsx,
// Fallback.tsx; gaps package P2). The window reads the plant analyzer's ui_data (plant_analyzer.dm:153: the active tab and the last scan's
// tray, seed, plant and graft data) and ui_static_data (the trait table, the cycle length) and sends setTab. Not in the kit: the
// nutrients' stacked per-reagent bars (one bar of the total is drawn, coloured by the level as tg's border is), Blink (the over-100%
// warning is drawn still), the buttons' ellipsis. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PlantAnalyzer
{
    // tg: PlantAnalyzer/types.ts:3  enum PlantAnalyzerTabs
    private const double STATS = 1, CHEM = 2;

    // tg: PlantAnalyzer/index.tsx:14
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue graft_data = Present(data.Get("graft_data"));
        UiValue seed_data = Present(data.Get("seed_data"));
        UiValue tray_data = Present(data.Get("tray_data"));
        UiValue plant_data = Present(data.Get("plant_data"));
        double active_tab = data.Num("active_tab");

        var content = new List<UiNode>
        {
            // tg: index.tsx:21
            new UiNode(UiKind.Section) { Key = "tabs" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "tab:stats",
                    Fluid = true,
                    Align = "center",
                    Text = "Stats",
                    Selected = active_tab == STATS || graft_data != null,
                    OnClick = () => c.Act("setTab", ("tab", STATS)),
                }),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                {
                    Key = "tab:chem",
                    Fluid = true,
                    Align = "center",
                    Text = "Chemicals",
                    Selected = active_tab == CHEM && graft_data == null,
                    Disabled = graft_data != null,
                    OnClick = () => c.Act("setTab", ("tab", CHEM)),
                }))),
        };

        if (active_tab == STATS || graft_data != null)
        {
            if (graft_data != null)
                content.Add(Graft(data, graft_data));
            if (tray_data != null)
                content.Add(TrayStats(tray_data));
            if (seed_data != null)
                content.Add(SeedStats(data, seed_data, tray_data));
        }
        else
        {
            /* grafts don't have any chems */
            if (tray_data != null)
                content.Add(TrayChems(tray_data));
            if (seed_data != null)
                content.Add(SeedChems(seed_data));
            if (plant_data != null && seed_data != null)
                content.Add(PlantChems(seed_data, plant_data));
        }

        // tg: index.tsx:19  <Window width={475} height={625}><Window.Content scrollable>
        return c.Window(475, 625, null, content.ToArray());
    }

    private static UiValue Present(UiValue v) => v is { Kind: UiValueKind.Object } ? v : null;

    // tgui-core/string's capitalizeFirst: the first letter upper-cased, the rest kept
    private static string CapitalizeFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // tg: PlantAnalyzer/Fallback.tsx  <Icon name="spinner" size={2.8} spin />: the DmIcon's fallback (the kit draws the picture or nothing)
    private static UiNode Picture(string key, string icon, string state) =>
        new UiNode(UiKind.Box) { Key = key, DmIcon = icon, DmIconState = state, Width = "64px", Height = "64px" };

    private static (string, double, double)[] Ranges(params (string, double, double)[] r) => r;

    private static UiNode Bar(string key, double value, string text, double max = 1, (string, double, double)[] ranges = null) => new(UiKind.ProgressBar)
    {
        Key = key,
        Value = value,
        MaxValue = max,
        Text = text,
        Ranges = ranges,
    };

    private static readonly (string, double, double)[] Standard = Ranges(("good", 0.7, double.PositiveInfinity), ("average", 0.3, 0.7), ("bad", 0, 0.3));

    private static string N(double v) => TguiFormat.JsNumber(v);

    // tg: PlantAnalyzer/Tray.tsx:18  export function PlantAnalyzerTrayStats(props)
    private static UiNode TrayStats(UiValue tray_data)
    {
        bool self_sustaining = tray_data.Truthy("self_sustaining");
        bool being_pollinated = tray_data.Truthy("being_pollinated");
        double yield_mod = tray_data.Num("yield_mod");
        double nutri = tray_data.Num("nutri"), nutri_max = tray_data.Num("nutri_max");
        double toxins_max = tray_data.Num("toxins_max");
        return new UiNode(UiKind.Section) { Key = "trayStats", Title = CapitalizeFirst(tray_data.Str("name")) }.With(new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack) { Vertical = true, Align = "center", Width = "100px" }.With(
                new UiNode(UiKind.StackItem).With(Picture("trayIcon", tray_data.Str("icon"), tray_data.Str("icon_state"))),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack).With(
                    // tg: Tray.tsx:42
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "autogrow",
                        Icon = "hand-holding-droplet",
                        Color = self_sustaining ? "yellow" : "transparent",
                        Tooltip = self_sustaining ? "Autogrow active - providing light, reclaiming water and reducing weed and pest levels." : "Autogrow inactive",
                        Disabled = true,
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "pollinating",
                        Icon = "wind",
                        Color = being_pollinated ? "teal" : "transparent",
                        Tooltip = being_pollinated ? "Cross pollinating nearby plants - potentially sharing plant reagents." : "Not cross pollinating",
                        Disabled = true,
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "bees",
                        Icon = "sun",
                        Color = yield_mod > 1 ? "olive" : "transparent",
                        Tooltip = yield_mod > 1 ? "Pollinated by bees - doubling yield." : "No yield modifier",
                        Disabled = true,
                    }))))),
            new UiNode(UiKind.StackItem) { Width = "100%" }.With(new UiNode(UiKind.LabeledList).With(
                // tg: Tray.tsx:88
                new UiNode(UiKind.LabeledItem) { Key = "water", Label = "Water", Tooltip = "The plant starts withering without water, unless it is a mushroom." }.With(
                    Bar("waterBar", tray_data.Num("water") / tray_data.Num("water_max"), N(tray_data.Num("water")) + " / " + N(tray_data.Num("water_max")), ranges: Standard)),
                // tg: Tray.tsx:104  the reagents' stacked bars are drawn as one bar of the total (the border's colour, nutriToColor)
                new UiNode(UiKind.LabeledItem) { Key = "nutrients", Label = "Nutrients", Tooltip = "The plant starts withering without nutrients, unless it is a weed. Nutrients may affect plant and tray stats." }.With(
                    tray_data.Arr("reagents").Count > 0
                        ? new UiNode(UiKind.ProgressBar) { Key = "nutriBar", Value = nutri / nutri_max, Color = NutriToColor(nutri, nutri_max), Text = N(nutri) + " / " + N(nutri_max) }
                        : Bar("nutriBar", nutri / nutri_max, N(nutri) + " / " + N(nutri_max), ranges: Standard)),
                // tg: Tray.tsx:144
                new UiNode(UiKind.LabeledItem) { Key = "light", Label = "Light", Tooltip = "The plant withers when the light is below 40%. Mushrooms need only 20%." }.With(
                    Bar("lightBar", tray_data.Num("light_level"), N(Math.Round(tray_data.Num("light_level") * 100, MidpointRounding.AwayFromZero)) + "%", 1,
                        Ranges(("bad", 0, 0.2), ("average", 0.2, 0.4), ("good", 0.4, double.PositiveInfinity)))),
                // tg: Tray.tsx:162
                new UiNode(UiKind.LabeledItem) { Key = "weeds", Label = "Weeds", Tooltip = "Damage the Yield stat of the plant. At high level may invade the tray. Remove by cultivating." }.With(
                    Bar("weedsBar", tray_data.Num("weeds"), N(tray_data.Num("weeds")) + " / " + N(tray_data.Num("weeds_max")), tray_data.Num("weeds_max"),
                        Ranges(("good", double.NegativeInfinity, 5), ("average", 5, 10), ("bad", 10, tray_data.Num("weeds_max"))))),
                // tg: Tray.tsx:180
                new UiNode(UiKind.LabeledItem) { Key = "pests", Label = "Pests", Tooltip = "Damage the plant and reduce its Potency stat. Remove with pest controlling nutrients." }.With(
                    Bar("pestsBar", tray_data.Num("pests"), N(tray_data.Num("pests")) + " / " + N(tray_data.Num("pests_max")), tray_data.Num("pests_max"),
                        Ranges(("good", double.NegativeInfinity, 4), ("average", 4, 8), ("bad", 8, tray_data.Num("pests_max"))))),
                // tg: Tray.tsx:198
                new UiNode(UiKind.LabeledItem) { Key = "toxins", Label = "Toxins", Tooltip = "Damage the plant. Remove with detoxing nutrients." }.With(
                    Bar("toxinsBar", tray_data.Num("toxins"), N(tray_data.Num("toxins")) + " / " + N(toxins_max), toxins_max,
                        Ranges(("good", double.NegativeInfinity, 0), ("average", 1, Math.Round(toxins_max * 0.5, MidpointRounding.AwayFromZero)), ("bad", Math.Round(toxins_max * 0.5, MidpointRounding.AwayFromZero), double.PositiveInfinity))))))));
    }

    // tg: PlantAnalyzer/Tray.tsx:254
    private static string NutriToColor(double nutri, double maxNutri) => nutri < maxNutri * 0.3 ? "red" : nutri < maxNutri * 0.7 ? "yellow" : "green";

    // tg: PlantAnalyzer/Tray.tsx:218  export function PlantAnalyzerTrayChems(props)
    private static UiNode TrayChems(UiValue tray_data)
    {
        IReadOnlyList<UiValue> reagents = tray_data.Arr("reagents");
        var section = new UiNode(UiKind.Section) { Key = "trayChems", Title = CapitalizeFirst(tray_data.Str("name") + " Nutrients") };
        if (reagents.Count == 0)
            return section.With(new UiNode(UiKind.NoticeBox) { Key = "none", Color = "red", TextAlign = "center", Text = "No reagents detected" });
        var table = new UiNode(UiKind.Table) { Key = "reagents" }.With(new UiNode(UiKind.TableRow) { Key = "head", Bold = true }.With(
            new UiNode(UiKind.TableCell) { Text = "Reagent" },
            new UiNode(UiKind.TableCell) { Text = "Volume" }));
        for (int i = 0; i < reagents.Count; i++)
        {
            table.With(new UiNode(UiKind.TableRow) { Key = "r" + i, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Text = reagents[i].Str("name") },
                new UiNode(UiKind.TableCell) { Text = N(reagents[i].Num("volume")) + "u" }));
        }
        return section.With(table);
    }

    // tg: PlantAnalyzer/Graft.tsx:14  export function PlantAnalyzerGraft(props)
    private static UiNode Graft(UiValue data, UiValue graft_data)
    {
        string graft_gene = graft_data.Str("graft_gene");
        UiNode geneTip = graft_gene.Length > 0 ? SeedTable.TraitTooltip(graft_gene, data.Arr("trait_db"), "graft") : null;
        double weed_chance = graft_data.Num("weed_chance"), weed_rate = graft_data.Num("weed_rate");
        return new UiNode(UiKind.Section)
        {
            Key = "graft",
            Title = "Graft: " + CapitalizeFirst(graft_data.Str("name")),
            Buttons = geneTip,
        }.With(new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem).With(Picture("graftIcon", graft_data.Str("icon"), graft_data.Str("icon_state"))),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "endurance", Label = "Endurance" }.With(
                    Bar("enduranceBar", graft_data.Num("endurance") / 100, N(graft_data.Num("endurance")) + " / 100", ranges: Ranges(("good", 0.5, double.PositiveInfinity), ("average", 0.1, 0.5), ("bad", 0, 0.1)))),
                new UiNode(UiKind.LabeledItem) { Key = "lifespan", Label = "Lifespan" }.With(
                    Bar("lifespanBar", graft_data.Num("lifespan") / 100, N(graft_data.Num("lifespan")) + " / 100", ranges: Ranges(("good", 0.65, double.PositiveInfinity), ("average", 0.25, 0.65), ("bad", 0, 0.25)))),
                new UiNode(UiKind.LabeledItem) { Key = "yield", Label = "Yield" }.With(
                    Bar("yieldBar", graft_data.Num("yield") / 10, N(graft_data.Num("yield")) + " / 10", ranges: Standard)),
                // tg: Graft.tsx:81  the Instability row reads weed_chance (tg's own slip)
                new UiNode(UiKind.LabeledItem) { Key = "instability", Label = "Instability" }.With(
                    Bar("instabilityBar", weed_chance, N(weed_chance) + " / 100", 100, Ranges(("good", double.NegativeInfinity, 20), ("average", 20, 40), ("bad", 40, double.PositiveInfinity)))),
                new UiNode(UiKind.LabeledItem) { Key = "production", Label = "Production", Text = N(graft_data.Num("production") * data.Num("cycle_seconds")) + " seconds" },
                new UiNode(UiKind.LabeledItem)
                {
                    Key = "weeds",
                    Label = "Weeds",
                    Text = weed_chance != 0 && weed_rate != 0 ? N(weed_chance) + "% chance to grow by " + N(weed_rate) : "No weed growth",
                }))));
    }

    // tg: PlantAnalyzer/Seed.tsx:22  export function PlantAnalyzerSeedStats(props)
    private static UiNode SeedStats(UiValue data, UiValue seed_data, UiValue tray_data)
    {
        IReadOnlyList<UiValue> trait_db = data.Arr("trait_db");
        double cycle_seconds = data.Num("cycle_seconds");
        List<string> core_traits = seed_data.Arr("core_traits").Select(t => t.AsStr()).ToList();
        List<string> all_traits = core_traits.Concat(seed_data.Arr("removable_traits").Select(t => t.AsStr())).ToList();
        IReadOnlyList<UiValue> mutatelist = seed_data.Arr("mutatelist");
        double endurance = seed_data.Num("endurance"), lifespan = seed_data.Num("lifespan"), maturation = seed_data.Num("maturation");

        var left = new UiNode(UiKind.Stack) { Vertical = true, Align = "center", Width = "150px" }.With(
            new UiNode(UiKind.StackItem).With(Picture("seedIcon", seed_data.Str("icon"), seed_data.Str("icon_state"))));
        if (seed_data.Str("product_icon").Length > 0 && seed_data.Str("product_icon_state").Length > 0)
            left.With(new UiNode(UiKind.StackItem).With(Picture("productIcon", seed_data.Str("product_icon"), seed_data.Str("product_icon_state"))));
        // tg: Seed.tsx:59
        left.With(new UiNode(UiKind.StackItem) { Width = "100%" }.With(new UiNode(UiKind.Button)
        {
            Key = "graftGene",
            Fluid = true,
            Icon = "scissors",
            Disabled = true,
            Color = "teal",
            Tooltip = "Using secateurs on the plant will produce a graft containing the listed gene.",
            Text = TraitInfo(seed_data.Str("graft_gene"), trait_db)?.Str("name") ?? "No graft gene",
        }));
        if (mutatelist.Count > 0)
        {
            // tg: Seed.tsx:73
            left.With(new UiNode(UiKind.StackItem) { Width = "100%" }.With(new UiNode(UiKind.Collapsible) { Key = "mutations", Title = "Mutations:", Color = "olive" }.With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(mutatelist.Select(mutation => new UiNode(UiKind.StackItem) { Width = "100%" }.With(new UiNode(UiKind.Button)
                {
                    Key = "preview_" + mutation.AsStr(),
                    Fluid = true,
                    Icon = "dna",
                    Disabled = true,
                    Color = "olive",
                    Tooltip = "Once sufficiently unstable the plant may mutate into the listed plant.",
                    Text = mutation.AsStr(),
                })).ToArray()))));
        }

        var list = new UiNode(UiKind.LabeledList) { Key = "stats" };
        if (tray_data != null)
        {
            // tg: Seed.tsx:105
            list.With(new UiNode(UiKind.LabeledItem) { Key = "health", Label = "Health" }.With(
                Bar("healthBar", tray_data.Num("plant_health") / endurance, N(tray_data.Num("plant_health")) + " / " + N(endurance), ranges: Standard)));
        }
        list.With(new UiNode(UiKind.LabeledItem) { Key = "endurance", Label = "Endurance", Tooltip = "The health pool of the plant that delays withering. Improves quality of resulting food & drinks." }.With(
            Bar("enduranceBar", endurance / 100, N(endurance) + " / 100", ranges: Standard)));
        if (tray_data != null)
        {
            // tg: Seed.tsx:135
            var age = new UiNode(UiKind.LabeledItem) { Key = "age", Label = "Age" };
            if (tray_data.Truthy("is_dead"))
                age.With(new UiNode(UiKind.NoticeBox) { Key = "dead", Color = "red", TextAlign = "center" }.With(new UiNode(UiKind.Icon) { Icon = "skull" }, new UiNode(UiKind.Box) { Text = "Dead" }));
            else
                age.With(Bar("ageBar", tray_data.Num("plant_age"), N(tray_data.Num("plant_age") * cycle_seconds) + " / " + FormatPerSecond(lifespan, cycle_seconds, false), lifespan,
                    Ranges(("average", 0, maturation), ("good", maturation, lifespan), ("bad", lifespan, double.PositiveInfinity))));
            list.With(age);
        }
        list.With(
            new UiNode(UiKind.LabeledItem) { Key = "maturation", Label = "Maturation", Tooltip = "The age at which the plant starts growing products.", Text = FormatPerSecond(maturation, cycle_seconds) },
            new UiNode(UiKind.LabeledItem) { Key = "production", Label = "Production", Tooltip = "The time needed for a mature plant to (re)grow a product.", Text = FormatPerSecond(seed_data.Num("production"), cycle_seconds) },
            new UiNode(UiKind.LabeledItem) { Key = "lifespan", Label = "Lifespan", Tooltip = "The age at which the plant starts withering. Improves quality of resulting food & drinks.", Text = FormatPerSecond(lifespan, cycle_seconds) },
            new UiNode(UiKind.LabeledItem) { Key = "yield", Label = "Yield", Tooltip = "The number of products gathered in a single harvest." }.With(
                Bar("yieldBar", seed_data.Num("yield") / 10, N(seed_data.Num("yield")) + " / 10", ranges: Standard)),
            new UiNode(UiKind.LabeledItem) { Key = "potency", Label = "Potency", Tooltip = "Determines product mass, reagent volume and strength of effects." }.With(
                Bar("potencyBar", seed_data.Num("potency") / 100, N(seed_data.Num("potency")) + " / 100", ranges: Standard)),
            new UiNode(UiKind.LabeledItem) { Key = "instability", Label = "Instability", Tooltip = "The likelihood of the plant to randomize stats or mutate. Affects quality of resulting food & drinks." }.With(
                Bar("instabilityBar", seed_data.Num("instability"), N(seed_data.Num("instability")) + " / 100", 100, Ranges(("good", double.NegativeInfinity, 20), ("average", 20, 40), ("bad", 40, double.PositiveInfinity)))),
            new UiNode(UiKind.LabeledItem) { Key = "weeds", Label = "Weeds", Text = N(seed_data.Num("weed_chance")) + "% chance to grow by " + N(seed_data.Num("weed_rate")) + " every " + N(cycle_seconds) + " seconds" });
        foreach (UiValue label in seed_data.Arr("unique_labels"))
            list.With(new UiNode(UiKind.LabeledItem) { Key = "unique:" + label.Str("label"), Label = label.Str("label"), Text = label.Str("data") });

        // tg: Seed.tsx:240  <Collapsible title="Traits:" open color="brown">
        var traits = new UiNode(UiKind.Collapsible) { Key = "traits", Title = "Traits:", Open = true, Color = "brown" }.With(
            new UiNode(UiKind.Stack) { Vertical = true }.With(all_traits.Select(trait =>
            {
                UiValue traitInfo = TraitInfo(trait, trait_db);
                return new UiNode(UiKind.StackItem) { Width = "100%" }.With(new UiNode(UiKind.Button)
                {
                    Key = trait + "_removable",
                    Fluid = true,
                    Color = core_traits.Contains(trait) ? "transparent" : "blue",
                    Disabled = true,
                    Tooltip = traitInfo?.Str("description"),
                    Icon = traitInfo?.Str("icon"),
                    Text = traitInfo?.Str("name") is { Length: > 0 } n ? n : "Unknown Trait",
                });
            }).ToArray()));
        var right = new UiNode(UiKind.StackItem) { Width = "100%" }.With(list, traits);
        // tg: Seed.tsx:269  the unique collapsibles (kudzu's traits)
        foreach (UiValue collapsible in seed_data.Arr("unique_collapsibles"))
        {
            right.With(new UiNode(UiKind.Collapsible) { Key = "collapsible:" + collapsible.Str("label"), Title = collapsible.Str("label"), Color = "brown" }.With(
                new UiNode(UiKind.Stack) { Vertical = true }.With(collapsible.Obj("data").Select(entry => new UiNode(UiKind.StackItem) { Width = "100%" }.With(new UiNode(UiKind.Button)
                {
                    Key = entry.Key,
                    Fluid = true,
                    Color = "transparent",
                    Disabled = true,
                    Tooltip = entry.Value.AsStr(),
                    Text = entry.Key,
                })).ToArray())));
        }

        return new UiNode(UiKind.Section) { Key = "seedStats", Title = CapitalizeFirst(seed_data.Str("name")) }.With(new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem).With(left), right));
    }

    // tg: PlantAnalyzer/Seed.tsx:301  export function PlantAnalyzerSeedChems(props)
    private static UiNode SeedChems(UiValue seed_data)
    {
        IReadOnlyList<UiValue> reagents = seed_data.Arr("reagents");
        var section = new UiNode(UiKind.Section) { Key = "seedChems", Title = CapitalizeFirst(seed_data.Str("name") + " Genes") };
        if (reagents.Count == 0)
            return section.With(new UiNode(UiKind.NoticeBox) { Key = "none", Color = "green", TextAlign = "center", Text = "No reagent genes" });

        double totalPercentage = reagents.Sum(r => r.Num("rate") * 100);
        double totalVolume = reagents.Sum(r => ExpectedReagentVolume(r, seed_data));
        var table = new UiNode(UiKind.Table) { Key = "genes" }.With(new UiNode(UiKind.TableRow) { Key = "head", Bold = true }.With(
            new UiNode(UiKind.TableCell) { Text = "Reagent" },
            new UiNode(UiKind.TableCell) { Text = "Percentage" },
            new UiNode(UiKind.TableCell) { Text = "Expected Volume", Underline = true, Tooltip = "Assuming the plant does not exceed its maximum capacity, this is what you can expect to obtain from consuming or grinding the product of the seed." }));
        foreach (UiValue reagent in reagents)
        {
            table.With(new UiNode(UiKind.TableRow) { Key = "r:" + reagent.Str("name"), Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Text = reagent.Str("name") },
                new UiNode(UiKind.TableCell) { Text = N(reagent.Num("rate") * 100) + "%" },
                new UiNode(UiKind.TableCell) { Text = "~" + N(ExpectedReagentVolume(reagent, seed_data)) + "u" }));
        }
        var totalCell = new UiNode(UiKind.TableCell) { Text = N(totalPercentage) + "%" };
        if (totalPercentage > 100)
            totalCell.With(new UiNode(UiKind.Button) { Key = "over", Icon = "exclamation-triangle", Disabled = true, Color = "transparent", Tooltip = "Exceeds 100% - each reagent will be proportionally reduced in the product." });
        table.With(
            new UiNode(UiKind.TableRow) { Key = "total", Candystripe = true }.With(new UiNode(UiKind.TableCell) { Text = "Total" }, totalCell, new UiNode(UiKind.TableCell) { Text = "~" + N(totalVolume) + "u" }),
            new UiNode(UiKind.TableRow) { Key = "cap", Candystripe = true }.With(new UiNode(UiKind.TableCell) { Text = "Cap" }, new UiNode(UiKind.TableCell) { Text = "100%" },
                new UiNode(UiKind.TableCell) { Text = "~" + N(seed_data.Num("volume_mod") * seed_data.Num("volume_units")) + "u" }));
        IReadOnlyList<UiValue> grind = seed_data.Arr("grind_results");
        if (grind.Count > 0)
            table.With(new UiNode(UiKind.TableRow) { Key = "grind", Candystripe = true }.With(new UiNode(UiKind.TableCell) { Text = "Grinds nutriments into:" }, new UiNode(UiKind.TableCell),
                new UiNode(UiKind.TableCell) { Text = string.Join(", ", grind.Select(g => g.AsStr())) }));
        if (seed_data.Str("juice_name").Length > 0)
            table.With(new UiNode(UiKind.TableRow) { Key = "juice", Candystripe = true }.With(new UiNode(UiKind.TableCell) { Text = "Juices into:" }, new UiNode(UiKind.TableCell),
                new UiNode(UiKind.TableCell) { Text = seed_data.Str("juice_name") }));
        if (seed_data.Str("distill_reagent").Length > 0)
            table.With(new UiNode(UiKind.TableRow) { Key = "distill", Candystripe = true }.With(new UiNode(UiKind.TableCell) { Text = "Distills into:" }, new UiNode(UiKind.TableCell),
                new UiNode(UiKind.TableCell) { Text = seed_data.Str("distill_reagent") }));
        return section.With(table);
    }

    // tg: PlantAnalyzer/Seed.tsx:441  export function PlantAnalyzerPlantChems(props)
    private static UiNode PlantChems(UiValue seed_data, UiValue plant_data)
    {
        IReadOnlyList<UiValue> reagents = plant_data.Arr("reagents");
        var section = new UiNode(UiKind.Section) { Key = "plantChems", Title = CapitalizeFirst(seed_data.Str("name") + " Contents") };
        if (reagents.Count == 0)
            return section.With(new UiNode(UiKind.NoticeBox) { Key = "none", Color = "green", TextAlign = "center", Text = "No reagent genes" });
        var table = new UiNode(UiKind.Table) { Key = "contents" }.With(new UiNode(UiKind.TableRow) { Key = "head", Bold = true }.With(
            new UiNode(UiKind.TableCell) { Text = "Reagent" },
            new UiNode(UiKind.TableCell) { Text = "Volume" }));
        foreach (UiValue reagent in reagents)
            table.With(new UiNode(UiKind.TableRow) { Key = "r:" + reagent.Str("name"), Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Text = reagent.Str("name") },
                new UiNode(UiKind.TableCell) { Text = N(reagent.Num("volume")) }));
        table.With(new UiNode(UiKind.TableRow) { Key = "total", Candystripe = true }.With(
            new UiNode(UiKind.TableCell) { Text = "Total" },
            new UiNode(UiKind.TableCell) { Text = N(reagents.Sum(r => r.Num("volume"))) + "u" }));
        return section.With(table);
    }

    // tg: PlantAnalyzer/Seed.tsx:492
    private static double ExpectedReagentVolume(UiValue reagent, UiValue seed_data)
    {
        double baseVolume = seed_data.Num("volume_units") * seed_data.Num("volume_mod");
        return Math.Round(reagent.Num("rate") * baseVolume * (seed_data.Num("potency") / 100), MidpointRounding.AwayFromZero);
    }

    // tg: PlantAnalyzer/Seed.tsx:500
    private static string FormatPerSecond(double value, double cycle_seconds, bool simplify = true)
    {
        if (!simplify)
            return N(value * cycle_seconds) + " second" + (value * cycle_seconds > 1 ? "s" : "");
        double seconds = Math.Round(value * cycle_seconds, MidpointRounding.AwayFromZero);
        if (seconds < 60)
            return N(seconds) + " second" + (seconds > 1 ? "s" : "");
        double minutes = Math.Floor(seconds / 60);
        double remainingSeconds = seconds % 60;
        return N(minutes) + " minute" + (minutes > 1 ? "s" : "") + (remainingSeconds > 0 ? ", " + N(remainingSeconds) + " second" + (remainingSeconds > 1 ? "s" : "") : "");
    }

    // tg: PlantAnalyzer/Seed.tsx:522
    private static UiValue TraitInfo(string trait, IReadOnlyList<UiValue> trait_db) => trait_db.FirstOrDefault(t => t.Str("path") == trait);
}
