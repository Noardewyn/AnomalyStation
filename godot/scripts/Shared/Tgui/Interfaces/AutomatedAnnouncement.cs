using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AutomatedAnnouncement.tsx (M9 slice D: the
// Automated Announcement System's window). Engine-free (compiled into the tests too). The search Input's placeholder is its tooltip.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AutomatedAnnouncement
{
    private static IEnumerable<KeyValuePair<string, UiValue>> Map(UiValue entry, string key) =>
        entry.Get(key)?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>();

    // tg: tgui/packages/tgui/interfaces/AutomatedAnnouncement.tsx:39
    private static bool IsEntryMatch(UiValue entry, string search)
    {
        if (string.IsNullOrEmpty(search))
            return true;
        string s = search.ToLowerInvariant();
        return entry.Str("name").ToLowerInvariant().Contains(s, StringComparison.Ordinal)
            || Map(entry, "announcementLinesMap").Any(l => l.Key.ToLowerInvariant().Contains(s, StringComparison.Ordinal))
            || Map(entry, "announcementLinesMap").Any(l => (l.Value.Text ?? "").ToLowerInvariant().Contains(s, StringComparison.Ordinal))
            || Map(entry, "varsAndTooltipsMap").Any(v => v.Key.ToLowerInvariant().Contains(s, StringComparison.Ordinal));
    }

    // tg: tgui/packages/tgui/interfaces/AutomatedAnnouncement.tsx:34
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> configEntries = data.Get("config_entries")?.Items ?? Array.Empty<UiValue>();
        int maxLen = (int)data.Num("max_announcement_len");
        string search = c.UseState("search", "");

        string errorMessage = configEntries.Count == 0
            ? "No configurable options detected! Please report this to Nanotrasen."
            : "No match. Refine your search.";

        var sorted = TguiFormat.SortBy(configEntries.Where(e => IsEntryMatch(e, search)), e => e.Str("name"));

        UiNode Entry(UiValue entry)
        {
            string entryRef = entry.Str("entryRef");
            bool enabled = entry.Truthy("enabled");
            bool modifiable = entry.Truthy("modifiable");
            string general = entry.Str("generalTooltip");
            string tooltip = (string.IsNullOrEmpty(general) ? "" : general + "\n")
                + string.Join("\n", Map(entry, "varsAndTooltipsMap").Select(v => "%" + v.Key + " " + v.Value.Text));
            return new UiNode(UiKind.Section)
            {
                Key = entryRef,
                Title = entry.Str("name"),
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "info", Icon = "info", Tooltip = tooltip },
                    new UiNode(UiKind.Button)
                    {
                        Key = "toggle",
                        Icon = enabled ? "power-off" : "times",
                        Selected = enabled,
                        Disabled = !modifiable,
                        Tooltip = !modifiable ? "Editing disabled by CentCom!" : null,
                        Text = enabled ? "On" : "Off",
                        OnClick = () => c.Act("Toggle", ("entryRef", entryRef)),
                    }),
            }.With(new UiNode(UiKind.Table).With(Map(entry, "announcementLinesMap").Select(line => new UiNode(UiKind.TableRow) { Key = line.Key }.With(
                new UiNode(UiKind.TableCell) { Color = "label", Text = line.Key + ":" },
                new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Input)
                {
                    Key = "line:" + entryRef + ":" + line.Key,
                    Fluid = true,
                    TextValue = line.Value.Text ?? "",
                    Disabled = !modifiable,
                    MaxLength = maxLen,
                    OnCommit = v => c.Act("Text", ("entryRef", entryRef), ("lineKey", line.Key), ("newText", v)),
                })))));
        }

        return c.Window(500, 280, "Automated Announcement System",
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Search" }.With(new UiNode(UiKind.Input)
                    {
                        Key = "search",
                        Fluid = true,
                        Tooltip = "Name/Line/Var",
                        TextValue = search,
                        OnInput = v => c.SetState("search", v),
                    }))),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(sorted.Count == 0
                    ? new UiNode(UiKind.NoticeBox) { Key = "error", Text = errorMessage }
                    : new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(sorted.Select(Entry)))));
    }
}
