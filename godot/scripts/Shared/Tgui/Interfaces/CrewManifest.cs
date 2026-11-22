using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CrewManifest.jsx (M9 slice E: the crew manifest of the
// ghosts and the lobby, GLOB.manifest's window). Engine-free (compiled into the tests too). A department's colour (tg's
// --department-color) is its section title's colour.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CrewManifest
{
    // tg: CrewManifest.jsx:7
    private static readonly string[] CommandJobs =
    {
        "Head of Personnel",
        "Head of Security",
        "Chief Engineer",
        "Research Director",
        "Chief Medical Officer",
        "Quartermaster",
    };

    // tg: CrewManifest.jsx:16
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<KeyValuePair<string, UiValue>> manifest = c.Data.Get("manifest")?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>();
        UiValue positions = c.Data.Get("positions");

        var sections = manifest.Select(dept =>
        {
            UiValue position = positions?.Get(dept.Key);
            IReadOnlyList<UiValue> exceptions = position?.Get("exceptions")?.Items ?? Array.Empty<UiValue>();
            string title = dept.Key + (dept.Key != "Misc" ? " (" + TguiFormat.JsNumber(position?.Num("open") ?? 0) + " positions open)" : "");
            return new UiNode(UiKind.Section) { Key = "dept:" + dept.Key, Title = title, Color = position?.Str("color") }.With(
                new UiNode(UiKind.Table).With(dept.Value.Items.Select((member, i) =>
                {
                    string rank = member.Str("rank");
                    string trim = member.Str("trim");
                    var icons = new List<UiNode>();
                    if (exceptions.Any(e => e.Text == rank))
                        icons.Add(new UiNode(UiKind.Icon) { Key = "infinity", Icon = "infinity", Tooltip = "No position limit" });
                    if (trim == "Captain")
                        icons.Add(new UiNode(UiKind.Icon) { Key = "captain", Icon = "star", Color = "yellow", Tooltip = "Captain" });
                    if (CommandJobs.Contains(trim))
                        icons.Add(new UiNode(UiKind.Icon) { Key = "command", Icon = "chevron-up", Color = "yellow", Tooltip = "Member of command" });
                    return new UiNode(UiKind.TableRow) { Key = "member:" + i }.With(
                        new UiNode(UiKind.TableCell) { Key = "name", Width = "50%", Text = member.Str("name") },
                        new UiNode(UiKind.TableCell) { Key = "icons", Width = "40px" }.With(icons),
                        new UiNode(UiKind.TableCell) { Key = "rank", Width = "50%", Text = rank });
                })));
        });

        return c.Window(350, 500, "Crew Manifest", new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(sections))));
    }
}
