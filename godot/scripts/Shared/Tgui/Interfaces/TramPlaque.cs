using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TramPlaque.tsx (the tram's information plaque,
// signs_misc.dm's ui_static_data: the current tram's and the previous trams' serials, dates, distances and collisions).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TramPlaque
{
    // tg: TramPlaque.tsx:19
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> currentTram = data.Arr("currentTram");
        IReadOnlyList<UiValue> previousTrams = data.Arr("previousTrams");
        // tg maps the one-element list into text: an array's toString joins its items with commas
        string Join(Func<UiValue, string> pick) => string.Join(",", currentTram.Select(pick));

        var history = new UiNode(UiKind.Stack) { Key = "rows", Vertical = true, Fill = true };
        foreach (UiValue tram in previousTrams)
            history.Children.Add(new UiNode(UiKind.StackItem) { Key = tram.Str("serialNumber") }.With(Row(
                tram.Str("serialNumber"), tram.Str("mfgDate"), TguiFormat.JsNumber(tram.Num("distanceTravelled") / 1000) + " km", TguiFormat.JsNumber(tram.Num("tramCollisions")))));

        c.WindowTheme = "dark";
        return c.Window(600, 360, "Tram Information Plaque",
            new UiNode(UiKind.NoticeBox) { Key = "maker", Color = "info", Text = "SkyyTram Mk VI by Nakamura Engineering" },
            new UiNode(UiKind.Section) { Key = "current", Title = Join(t => t.Str("serialNumber")) + " - Constructed " + Join(t => t.Str("mfgDate")) }.With(
                new UiNode(UiKind.LabeledList) { Key = "list" }.With(
                    new UiNode(UiKind.LabeledItem) { Key = "distance", Label = "Distance Travelled", Text = Join(t => TguiFormat.JsNumber(t.Num("distanceTravelled") / 1000)) + " km" },
                    new UiNode(UiKind.LabeledItem) { Key = "collisions", Label = "Collisions", Text = Join(t => TguiFormat.JsNumber(t.Num("tramCollisions"))) })),
            new UiNode(UiKind.Section) { Key = "history", Title = "Tram History" }.With(
                Row("Serial", "Constructed", "Distance", "Collisions", bold: true),
                history));
    }

    private static UiNode Row(string serial, string date, string distance, string collisions, bool bold = false)
        => new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "serial", Grow = 1, Bold = bold, Text = serial },
            new UiNode(UiKind.StackItem) { Key = "date", Grow = 1, Bold = bold, Text = date },
            new UiNode(UiKind.StackItem) { Key = "distance", Grow = 1, Bold = bold, Text = distance },
            new UiNode(UiKind.StackItem) { Key = "collisions", Grow = 1, Bold = bold, Text = collisions });
}
