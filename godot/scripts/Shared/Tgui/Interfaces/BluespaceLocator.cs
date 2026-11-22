using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BluespaceLocator.tsx (gaps TP1): the bluespace
// locator's tracked implants (teleportation.dm:35 ui_data), searched by name, with the selected one's arrow turned to its direction
// and coloured by its distance. tg's styled divs are the kit's stacks and sections; the arrow's turn is set at once (tg eases it
// over 0.3 s by the shortest way round). The selected name and the search are the window's local state.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BluespaceLocator
{
    // tg: BluespaceLocator.tsx:17
    private static readonly Dictionary<string, double> DirectionToIcon = new()
    {
        ["north"] = 0,
        ["northeast"] = 45,
        ["east"] = 90,
        ["southeast"] = 135,
        ["south"] = 180,
        ["southwest"] = 225,
        ["west"] = 270,
        ["northwest"] = 315,
    };

    // tg: BluespaceLocator.tsx:28
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> trackimplants = data.Get("trackimplants")?.Items ?? Array.Empty<UiValue>();
        double trackingrange = data.Num("trackingrange");

        // tg: BluespaceLocator.tsx:85  no implants at all
        if (trackimplants.Count == 0)
        {
            return c.Window(550, 500, null, new UiNode(UiKind.Stack) { Key = "none", Fill = true, Vertical = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Key = "dish", Icon = "satellite-dish", Size = 4, Color = "rgba(100, 150, 255, 0.5)" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "not_detected", TextAlign = "center", FontSize = "1.2rem", Text = "Bluespace signatures not detected" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "no_implants", TextAlign = "center", FontSize = "0.9rem", Color = "label", Text = "No active tracking implants detected within operational range" }),
                new UiNode(UiKind.StackItem) { Grow = 1 }));
        }

        string searchQuery = c.UseState("searchQuery", "");
        string selectedName = c.UseState<string>("selectedImplantName", trackimplants[0].Str("name"));
        List<UiValue> filtered = trackimplants.Where(i => i.Str("name").Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        // tg: the effect that keeps the selection among the filtered ones
        UiValue selected = filtered.FirstOrDefault(i => i.Str("name") == selectedName) ?? filtered.FirstOrDefault();

        return c.Window(550, 500, null, new UiNode(UiKind.Stack) { Key = "locator", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Width = "170px" }.With(new UiNode(UiKind.Section) { Key = "targets", Fill = true, Scrollable = true }.With(
                new UiNode(UiKind.Input)
                {
                    Key = "search",
                    Fluid = true,
                    Tooltip = "Search targets...",
                    TextValue = searchQuery,
                    OnInput = value => c.SetState("searchQuery", value ?? ""),
                },
                new UiNode(UiKind.Stack) { Key = "list", Vertical = true }.With(filtered.Select(implant =>
                {
                    string name = implant.Str("name");
                    double distance = implant.Num("distance");
                    bool isSelected = selected != null && selected.Str("name") == name;
                    return new UiNode(UiKind.StackItem) { Key = "target:" + name }.With(new UiNode(UiKind.Button)
                    {
                        Key = "select:" + name,
                        Fluid = true,
                        Icon = "user",
                        Selected = isSelected,
                        Text = name + " " + Dots(distance, trackingrange),
                        OnClick = () => c.SetState("selectedImplantName", name),
                    });
                }).ToArray()))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "pointer", Fill = true }.With(Pointer(filtered, selected, trackingrange)))));
    }

    // tg: BluespaceLocator.tsx:233  the three distance dots (filled up to the implant's third of the range)
    private static string Dots(double distance, double trackingrange)
        => new string(Enumerable.Range(1, 3).Select(dot => distance <= trackingrange / 3 * dot ? '●' : '○').ToArray());

    // tg: BluespaceLocator.tsx:76
    private static string ArrowColor(double distance, double trackingrange)
    {
        if (distance == 0)
            return "green";
        if (distance > trackingrange * 2 / 3)
            return "red";
        if (distance > trackingrange * 1 / 3)
            return "orange";
        return "green";
    }

    // tg: BluespaceLocator.tsx:258
    private static UiNode Pointer(List<UiValue> filtered, UiValue selected, double trackingrange)
    {
        if (filtered.Count == 0)
        {
            return new UiNode(UiKind.Stack) { Key = "no_match", Fill = true, Vertical = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Key = "search_icon", Icon = "search", Size = 3, Color = "rgba(200, 200, 200, 0.5)" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { TextAlign = "center", FontSize = "1.2rem", Text = "No targets matching search criteria" }),
                new UiNode(UiKind.StackItem) { Grow = 1 });
        }
        if (selected == null)
            return null;
        double distance = selected.Num("distance");
        double rotation = distance == 0 ? 0 : DirectionToIcon.GetValueOrDefault(selected.Str("direction") ?? "", 0);
        return new UiNode(UiKind.Stack) { Key = "selected", Fill = true, Vertical = true, Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 },
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon)
            {
                Key = "arrow",
                Icon = distance == 0 ? "dot-circle" : "arrow-up",
                Size = 12,
                Rotation = rotation,
                Color = ArrowColor(distance, trackingrange),
            }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "name", TextAlign = "center", Bold = true, FontSize = "1.3rem", Text = selected.Str("name") }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "distance", TextAlign = "center", FontSize = "1.1rem", Text = "Distance: " + TguiFormat.JsNumber(distance) }),
            new UiNode(UiKind.StackItem) { Grow = 1 });
    }
}
