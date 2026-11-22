using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosCrewManifest.jsx (M9 slice E: the Plexagon Crew List
// app). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosCrewManifest
{
    // tg: NtosCrewManifest.jsx:7
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<KeyValuePair<string, UiValue>> manifest = c.Data.Get("manifest")?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>();
        var departments = manifest.Select(dept => new UiNode(UiKind.Section) { Key = "dept:" + dept.Key, Title = dept.Key }.With(
            new UiNode(UiKind.Table).With(dept.Value.Items.Select((entry, i) => new UiNode(UiKind.TableRow) { Key = "entry:" + i, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Bold = true, Text = entry.Str("name") },
                new UiNode(UiKind.TableCell) { Text = "(" + entry.Str("rank") + ")" })))));
        return NtosWindow.Render(c, 400, 480, null, new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Section)
            {
                Title = "Crew Manifest",
                Buttons = new UiNode(UiKind.Button) { Key = "print", Icon = "print", Text = "Print", OnClick = () => c.Act("PRG_print") },
            }.With(departments))));
    }
}
