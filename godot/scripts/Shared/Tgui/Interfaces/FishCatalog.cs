using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/FishCatalog.tsx. The window reads tg's ui_static_data of
// the fish encyclopedia (fish_catalog.dm:19): the fish by name on the left, the chosen fish's waters, temperature, feed, acquisition,
// average size and weight, beauty score, fishing tips and illustration on the right. The illustration is tg's "fish" sprite sheet
// (the fish's sanitized icon file and state, "fish32x32 <id>"), drawn from the client's copy of tg's .dmi files (TguiSpritesheets, DV44).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class FishCatalog
{
    // tg: FishCatalog.tsx:38
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // sortBy(fish_info || [], [(fish) => fish.name])
        List<UiValue> fish_by_name = data.Arr("fish_info").OrderBy(f => f.Str("name"), StringComparer.Ordinal).ToList();
        // React's useState<FishInfo | null>(null): the chosen fish, kept by its name
        string currentName = c.UseState<string>("currentFish", null);
        UiValue currentFish = currentName == null ? null : fish_by_name.FirstOrDefault(f => f.Str("name") == currentName);
        string sponsored_by = data.Str("sponsored_by");

        // tg: FishCatalog.tsx:44  <Window width={500} height={300}>
        return c.Window(500, 300, null,
            new UiNode(UiKind.Stack) { Key = "root", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "list", Width = "140px" }.With(
                    new UiNode(UiKind.Section) { Key = "listSection", Fill = true, Scrollable = true }.With(
                        fish_by_name.Select(f =>
                        {
                            string name = f.Str("name");
                            return new UiNode(UiKind.Button)
                            {
                                Key = "fish-" + name,
                                Fluid = true,
                                Color = "transparent",
                                Selected = f == currentFish,
                                Text = name,
                                OnClick = () => c.SetState("currentFish", name),
                            };
                        }))),
                // tg: <Stack.Item grow basis={0}>
                new UiNode(UiKind.StackItem) { Key = "details", Grow = 1 }.With(
                    new UiNode(UiKind.Section)
                    {
                        Key = "detailsSection",
                        Fill = true,
                        Scrollable = true,
                        Title = currentFish != null ? TguiFormat.Capitalize(currentFish.Str("name")) : $"{sponsored_by} Fish Index",
                    }.With(currentFish != null ? Details(currentFish) : null))));
    }

    // tg: FishCatalog.tsx:77-127  the chosen fish's LabeledList
    private static UiNode Details(UiValue fish)
    {
        UiValue tips = fish.Get("fishing_tips");
        // {currentFish.fishing_tips.traits}: React renders the strings of the array one after another
        string traits = string.Concat(tips.Arr("traits").Select(t => t.AsStr()));
        // className={classes(['fish32x32', currentFish.icon])}
        string icon = fish.Str("icon");
        return new UiNode(UiKind.LabeledList) { Key = "info" }.With(
            new UiNode(UiKind.LabeledItem) { Label = "Description", Text = fish.Str("desc"), Wrap = true },
            new UiNode(UiKind.LabeledItem) { Label = "Water type", Text = fish.Str("fluid") },
            new UiNode(UiKind.LabeledItem) { Label = "Temperature", Text = $"{TguiFormat.JsNumber(fish.Num("temp_min"))} to {TguiFormat.JsNumber(fish.Num("temp_max"))}" },
            new UiNode(UiKind.LabeledItem) { Label = "Feeding", Text = fish.Str("feed") },
            new UiNode(UiKind.LabeledItem) { Label = "Acquisition", Text = fish.Str("source") },
            new UiNode(UiKind.LabeledItem) { Label = "Average size", Text = $"{TguiFormat.JsNumber(fish.Num("size"))} cm" },
            new UiNode(UiKind.LabeledItem) { Label = "Average weight", Text = $"{TguiFormat.JsNumber(fish.Num("weight"))} kiloclam" },
            new UiNode(UiKind.LabeledItem) { Label = "Aquarium Beauty Score", Text = fish.Str("beauty") },
            new UiNode(UiKind.LabeledItem) { Label = "Fishing and Aquarium tips" }.With(
                new UiNode(UiKind.LabeledList) { Key = "tips" }.With(
                    new UiNode(UiKind.LabeledItem) { Label = "Fishing locations", Text = tips.Str("spots"), Wrap = true },
                    new UiNode(UiKind.LabeledItem) { Label = "Favourite bait", Text = tips.Str("favorite_bait"), Wrap = true },
                    new UiNode(UiKind.LabeledItem) { Label = "Disliked bait", Text = tips.Str("disliked_bait"), Wrap = true },
                    new UiNode(UiKind.LabeledItem) { Label = "Behavior", Text = traits, Wrap = true })),
            new UiNode(UiKind.LabeledItem) { Label = "Illustration" }.With(
                new UiNode(UiKind.Box) { Key = "illustration", DmIcon = TguiSpritesheets.Fish32, DmIconState = icon, Width = "32px", Height = "32px" }));
    }
}
