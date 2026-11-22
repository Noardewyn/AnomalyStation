using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DestinationTagger.tsx (D325: the disposal system's mail).
// The window reads tg's ui_data of the destination tagger (sortingmachinery.dm:310) and sends change with the 1-based index of the
// picked location. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class DestinationTagger
{
    // tg: DestinationTagger.tsx:43
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> locations = data.Get("locations").Items;
        int currentTag = (int)data.Num("currentTag");
        // tg: DestinationTagger.tsx:28  sortDestinations: upper case, alphabetical, each keeping its 1-based id
        var sorted = locations.Select((name, index) => (Name: name.AsStr().ToUpperInvariant(), Id: index + 1))
            .OrderBy(d => d.Name, StringComparer.Ordinal).ToList();
        string title = currentTag < 1 || currentTag > locations.Count
            ? "Please Select A Location"
            : "Current Destination: " + locations[currentTag - 1].AsStr();
        // tg: DestinationTagger.tsx:47  <Window theme="retro" title="TagMaster 2.4" width={420} height={500}>
        c.WindowTheme = "retro";
        return c.Window(420, 500, "TagMaster 2.4",
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = title }.With(
                        sorted.Select(location => new UiNode(UiKind.Button)
                        {
                            Key = location.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            Checkbox = currentTag == location.Id,
                            Height = "2em",
                            Width = "15em",
                            Text = location.Name,
                            OnClick = () => c.Act("change", ("index", location.Id)),
                        })))));
    }
}
