using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Gps.tsx (silicons SI2, D326: the GPS, the mining
// cyborg's and the miners'). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Gps
{
    // tg: Gps.tsx:38  coordsToVec
    private static double[] CoordsToVec(string coords) =>
        (coords ?? "").Split(", ").Select(s => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : double.NaN).ToArray();

    private sealed record Signal(string EntryTag, string Coords, double? Dist, double? Degrees, int Index);

    // tg: Gps.tsx:40
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string currentArea = data.Str("currentArea");
        string currentCoords = data.Str("currentCoords");
        bool globalmode = data.Truthy("globalmode");
        bool power = data.Truthy("power");
        string tag = data.Str("tag");
        bool updating = data.Truthy("updating");
        string searchName = c.UseState("searchName", "");
        var searchByName = TguiFormat.CreateSearch<Signal>(searchName, gps => gps.EntryTag);

        double[] here = CoordsToVec(currentCoords);
        var signals = data.Arr("signals").Select((signal, index) =>
        {
            // Calculate distance to the target. BYOND distance is capped to 127, that's why we roll our own calculations here.
            double? dist = null;
            if (signal.Get("dist") is UiValue d && d.Truthy)
            {
                double[] there = CoordsToVec(signal.Str("coords"));
                double sum = 0;
                for (int i = 0; i < System.Math.Min(here.Length, there.Length); i++)
                    sum += (here[i] - there[i]) * (here[i] - there[i]);
                dist = System.Math.Round(System.Math.Sqrt(sum), System.MidpointRounding.AwayFromZero);
            }
            else if (signal.Get("dist") is { Kind: UiValueKind.Number } zero)
                dist = zero.Number; // 0 is falsy: tgui keeps the 0 (signal.dist && ...)
            double? degrees = signal.Get("degrees") is { Kind: UiValueKind.Number } deg ? deg.Number : null;
            return new Signal(signal.Str("entrytag"), signal.Str("coords"), dist, degrees, index);
        })
        // Signals with distance metric go first, then alphabetically
        .OrderBy(s => s.Dist == null).ThenBy(s => s.EntryTag, System.StringComparer.Ordinal)
        .ToList();

        var control = new UiNode(UiKind.Section)
        {
            Key = "control",
            Title = "Control",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "power",
                Icon = "power-off",
                Selected = power,
                Text = power ? "On" : "Off",
                OnClick = () => c.Act("power"),
            },
        }.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "tag", Label = "Tag" }.With(new UiNode(UiKind.Button)
            {
                Key = "rename",
                Icon = "pencil-alt",
                Text = tag,
                OnClick = () => c.Act("rename"),
            }),
            new UiNode(UiKind.LabeledItem) { Key = "mode", Label = "Scan Mode" }.With(new UiNode(UiKind.Button)
            {
                Key = "updating",
                Icon = updating ? "unlock" : "lock",
                Color = !updating ? "bad" : null,
                Text = updating ? "AUTO" : "MANUAL",
                OnClick = () => c.Act("updating"),
            }),
            new UiNode(UiKind.LabeledItem) { Key = "range", Label = "Range" }.With(new UiNode(UiKind.Button)
            {
                Key = "globalmode",
                Icon = "sync",
                Selected = !globalmode,
                Text = globalmode ? "MAXIMUM" : "LOCAL",
                OnClick = () => c.Act("globalmode"),
            })));

        var content = new UiNode(UiKind.Fragment).With(control);
        if (power)
        {
            content.With(new UiNode(UiKind.Section) { Key = "location", Title = "Current Location" }.With(
                new UiNode(UiKind.Box) { Key = "where", FontSize = "18px", Text = currentArea + " (" + currentCoords + ")" }));

            var table = new UiNode(UiKind.Table) { Key = "signals" }.With(
                new UiNode(UiKind.TableRow) { Key = "head", Bold = true }.With(
                    new UiNode(UiKind.TableCell) { Key = "name", Text = "Name" },
                    new UiNode(UiKind.TableCell) { Key = "dir", Text = "Direction" },
                    new UiNode(UiKind.TableCell) { Key = "coords", Text = "Coordinates" }));
            foreach (Signal signal in signals.Where(searchByName))
            {
                var direction = new UiNode(UiKind.TableCell)
                {
                    Key = "dir",
                    Opacity = signal.Dist is double dist ? System.Math.Clamp(1.2 / System.Math.Log(System.Math.E + dist / 20), 0.4, 1) : null,
                };
                if (signal.Degrees is double degrees)
                    direction.With(new UiNode(UiKind.Icon) { Key = "arrow", Icon = "arrow-up", Rotation = degrees, Size = 1.2 });
                if (signal.Dist is double meters)
                    direction.With(new UiNode(UiKind.Box) { Key = "dist", Text = " " + meters + "m" });
                table.With(new UiNode(UiKind.TableRow) { Key = signal.EntryTag + signal.Coords + signal.Index, Candystripe = true }.With(
                    new UiNode(UiKind.TableCell) { Key = "name", Bold = true, Color = "label", Text = signal.EntryTag },
                    direction,
                    new UiNode(UiKind.TableCell) { Key = "coords", Text = signal.Coords }));
            }

            content.With(new UiNode(UiKind.Section)
            {
                Key = "detected",
                Title = "Detected Signals",
                Buttons = new UiNode(UiKind.Input)
                {
                    Key = "search",
                    Tooltip = "Search by name...",
                    Width = "200px",
                    TextValue = searchName,
                    OnInput = value => c.SetState("searchName", value),
                },
            }.With(table));
        }

        // <Window title="Global Positioning System" width={470} height={700}><Window.Content scrollable>
        return c.Window(470, 700, "Global Positioning System", content);
    }
}
