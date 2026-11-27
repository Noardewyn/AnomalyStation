using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/FishAnalyzer.tsx. The scanned fish (one fish, or every
// fish of an aquarium with a search box): each with its picture, weight and size, its name, its food on its colour, its traits (each
// with its inheritance and description as a tooltip), its health and hunger bars, its evolutions (the fish, the chance, the new fish)
// and its temperature range and fluid, green when the aquarium suits it, red when not.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class FishAnalyzer
{
    // tg: FishAnalyzer.tsx:59
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool scanned = data.Truthy("fish_scanned");
        string searchText = c.UseState("searchText", "");
        Func<UiValue, bool> search = TguiFormat.CreateSearch<UiValue>(searchText, fish => fish.Str("fish_name"));
        IEnumerable<UiValue> fishes = data.Arr("fish_list");
        if (searchText.Length > 0)
            fishes = fishes.Where(search);
        // <Window title="Fish Analyzer" width={fish_scanned ? 530 : 700} height={fish_scanned ? 270 : 460}>
        return c.Window(scanned ? 530 : 700, scanned ? 270 : 460, "Fish Analyzer",
            new UiNode(UiKind.Stack) { Key = "body", Vertical = true, Fill = true, Background = "#402784" }.With(
                !scanned
                    ? new UiNode(UiKind.StackItem) { Key = "search-row" }.With(new UiNode(UiKind.Section) { Key = "search-section" }.With(new UiNode(UiKind.Input)
                    {
                        Key = "search",
                        AutoFocus = true,
                        Height = "20px",
                        Tooltip = "Search Fish...",
                        TextValue = searchText,
                        Fluid = true,
                        OnInput = value => c.SetState("searchText", value ?? ""),
                    }))
                    : null,
                new UiNode(UiKind.StackItem) { Key = "list-row", Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Key = "fish", Title = "Fish", Fill = true, Scrollable = true }.With(
                        new UiNode(UiKind.Stack) { Key = "fish-list", Wrap = true }.With(
                            fishes.Select((fish, index) => new UiNode(UiKind.StackItem)
                            {
                                Key = "entry-" + index,
                                Width = scanned ? "100%" : "44%",
                                Background = "#47218e",
                            }.With(FishItem(fish))).ToArray())))));
    }

    // tg: FishAnalyzer.tsx:127  FishItem
    private static UiNode FishItem(UiValue fish)
    {
        IReadOnlyList<UiValue> traits = fish.Arr("fish_traits");
        IReadOnlyList<UiValue> evolutions = fish.Arr("fish_evolutions");
        return new UiNode(UiKind.Stack) { Key = "item", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top" }.With(
                new UiNode(UiKind.Stack) { Key = "columns" }.With(
                    new UiNode(UiKind.StackItem) { Key = "picture-column", Width = "40px" }.With(
                        new UiNode(UiKind.Stack) { Key = "picture-stack", Vertical = true }.With(
                            new UiNode(UiKind.StackItem) { Key = "picture" }.With(new UiNode(UiKind.Box)
                            {
                                Key = "icon",
                                DmIcon = fish.Str("fish_icon"),
                                DmIconState = fish.Str("fish_icon_state"),
                                Width = "40px",
                                Height = "40px",
                                Background = "#1a0940",
                            }),
                            new UiNode(UiKind.StackItem) { Key = "weight", Text = fish.Str("fish_weight") + " kiloclam", FontSize = "10px" },
                            new UiNode(UiKind.StackItem) { Key = "size", Text = fish.Str("fish_size") + " cm", FontSize = "10px" })),
                    new UiNode(UiKind.StackItem) { Key = "name-column", Grow = 1 }.With(
                        new UiNode(UiKind.Stack) { Key = "name-stack", Vertical = true }.With(
                            new UiNode(UiKind.StackItem) { Key = "name", Text = DissectName(fish.Str("fish_name")).ToUpperInvariant(), Bold = true, FontSize = "13px" },
                            new UiNode(UiKind.StackItem)
                            {
                                Key = "food",
                                Text = fish.Str("fish_food"),
                                TextAlign = "center",
                                Background = fish.Str("fish_food_color"),
                                Color = "white",
                            },
                            new UiNode(UiKind.StackItem) { Key = "traits" }.With(traits.Count == 0
                                ? new UiNode(UiKind.Button) { Key = "no-traits", Text = "None", Color = "transparent", Tooltip = "Fish has no traits!" }
                                : new UiNode(UiKind.Stack) { Key = "trait-list", Vertical = true }.With(traits.Select((trait, index) => new UiNode(UiKind.StackItem) { Key = "trait-" + index }.With(
                                    new UiNode(UiKind.Button)
                                    {
                                        Key = "trait",
                                        Text = trait.Str("trait_name"),
                                        Color = "transparent",
                                        Tooltip = "Inheritance: " + trait.Str("trait_inherit") + "%\n" + trait.Str("trait_desc"),
                                    })).ToArray())))),
                    new UiNode(UiKind.StackItem) { Key = "bars-column", Grow = 1 }.With(
                        new UiNode(UiKind.Stack) { Key = "bars", Vertical = true }.With(
                            new UiNode(UiKind.StackItem) { Key = "health-row" }.With(new UiNode(UiKind.Box) { Key = "health-label", Text = "Health:" }, Bar("health", fish.Num("fish_health") / 100)),
                            new UiNode(UiKind.StackItem) { Key = "hunger-row" }.With(new UiNode(UiKind.Box) { Key = "hunger-label", Text = "Hunger:" }, Bar("hunger", fish.Num("fish_hunger"))),
                            new UiNode(UiKind.StackItem) { Key = "evolutions" }.With(evolutions.Count == 0
                                ? new UiNode(UiKind.Box) { Key = "no-evolutions", Text = "No evolutions!" }
                                : new UiNode(UiKind.Stack) { Key = "evolution-list", Vertical = true }.With(evolutions.Select((evolution, index) => new UiNode(UiKind.StackItem) { Key = "evolution-" + index }.With(
                                    EvolutionItem(evolution, fish))).ToArray())))))),
            new UiNode(UiKind.StackItem) { Key = "needs" }.With(
                new UiNode(UiKind.Stack) { Key = "needs-row" }.With(
                    new UiNode(UiKind.StackItem)
                    {
                        Key = "temperature",
                        Width = "25%",
                        Grow = 1,
                        Text = fish.Str("fish_min_temp") + "k - " + fish.Str("fish_max_temp") + "k",
                        Color = fish.Truthy("fish_suitable_temp") ? "#1ac400" : "red",
                    },
                    new UiNode(UiKind.StackItem)
                    {
                        Key = "fluid",
                        Grow = 1,
                        Text = DissectName(fish.Str("fish_fluid_type")),
                        Color = fish.Truthy("fish_fluid_compatible") ? "#1ac400" : "red",
                    })));
    }

    // <ProgressBar width="95%" value={...} ranges={{ good: [0.9, Infinity], average: [0.5, 0.9], bad: [-Infinity, 0.5] }} />
    private static UiNode Bar(string key, double value) => new UiNode(UiKind.ProgressBar)
    {
        Key = key,
        Width = "95%",
        Value = value,
        MinValue = 0,
        MaxValue = 1,
        Ranges = new[] { ("good", 0.9, double.PositiveInfinity), ("average", 0.5, 0.9), ("bad", double.NegativeInfinity, 0.5) },
    };

    // tg: FishAnalyzer.tsx:290  EvolutionItem: the fish, the chance with an arrow, the evolved fish
    private static UiNode EvolutionItem(UiValue evolution, UiValue fish)
        => new UiNode(UiKind.Stack) { Key = "evolution" }.With(
            new UiNode(UiKind.StackItem) { Key = "from" }.With(new UiNode(UiKind.Box)
            {
                Key = "from-icon",
                DmIcon = fish.Str("fish_icon"),
                DmIconState = fish.Str("fish_icon_state"),
                Width = "32px",
                Height = "32px",
            }),
            new UiNode(UiKind.StackItem) { Key = "chance" }.With(
                new UiNode(UiKind.Stack) { Key = "chance-stack", Vertical = true, Align = "center" }.With(
                    new UiNode(UiKind.StackItem) { Key = "probability", Text = evolution.Str("evolution_probability") + "%", FontSize = "8px" },
                    new UiNode(UiKind.StackItem) { Key = "arrow" }.With(new UiNode(UiKind.Icon) { Key = "arrow-right", Icon = "arrow-right", Color = "red" }))),
            new UiNode(UiKind.StackItem) { Key = "to" }.With(new UiNode(UiKind.Box)
            {
                Key = "to-icon",
                DmIcon = evolution.Str("evolution_icon"),
                DmIconState = evolution.Str("evolution_icon_state"),
                Width = "32px",
                Height = "32px",
            }));

    // tg: FishAnalyzer.tsx:329  dissectName: the first word, at most 12 letters
    private static string DissectName(string input)
    {
        string first = (input ?? "").Split(' ')[0];
        return first.Length > 12 ? first[..12] : first;
    }
}
