using System.Globalization;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Changelog.tsx (M10x slice H, D281): tg's changelog window,
// the lobby's Changelog button. Engine-free (compiled into the tests too).
// DEVIATION(DV200): the server's datum sends the port's CHANGELOG.md versions as tg's dates and the picked version's typed changes in
// its data (tg fetches a month's YAML asset and lists it by date and author); the header and footer name this project and tg, whose
// code it translates, instead of tg's own credits and Goon's licence.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Changelog
{
    // tg: Changelog.tsx:20  const icons
    private static readonly Dictionary<string, (string Icon, string Color)> Icons = new(StringComparer.Ordinal)
    {
        ["add"] = ("check-circle", "green"),
        ["admin"] = ("user-shield", "purple"),
        ["balance"] = ("balance-scale-right", "yellow"),
        ["bugfix"] = ("bug", "green"),
        ["code_imp"] = ("code", "green"),
        ["config"] = ("cogs", "purple"),
        ["expansion"] = ("check-circle", "green"),
        ["experiment"] = ("radiation", "yellow"),
        ["image"] = ("image", "green"),
        ["qol"] = ("hand-holding-heart", "green"),
        ["refactor"] = ("tools", "green"),
        ["rscadd"] = ("check-circle", "green"),
        ["rscdel"] = ("times-circle", "red"),
        ["server"] = ("server", "purple"),
        ["sound"] = ("volume-high", "green"),
        ["spellcheck"] = ("spell-check", "green"),
        ["map"] = ("map", "green"),
        ["tgs"] = ("toolbox", "purple"),
        ["tweak"] = ("wrench", "green"),
        ["unknown"] = ("info-circle", "label"),
        ["wip"] = ("hammer", "orange"),
    };

    // tg: Changelog.tsx:378
    public static UiNode Render(TguiContext c)
    {
        List<string> dates = c.Data.Arr("dates").Select(d => d.Text ?? "").ToList();
        string selected = c.Data.Str("selected");
        string picked = c.UseState("selectedDate", selected ?? dates.FirstOrDefault() ?? "");
        int selectedIndex = Math.Max(0, dates.IndexOf(picked));
        bool loaded = selected == picked; // tg's loaded_text: the picked version's data has come

        void Pick(int index)
        {
            c.SetState("selectedDate", dates[index]);
            c.Act("get_month", ("date", dates[index]));
        }

        UiNode DateDropdown(string key) => dates.Count == 0 ? null : new UiNode(UiKind.Stack) { Key = key }.With(
            new UiNode(UiKind.StackItem) { Key = "prev" }.With(new UiNode(UiKind.Button)
            {
                Icon = "chevron-left",
                Disabled = selectedIndex == 0,
                OnClick = () => Pick(selectedIndex - 1),
            }),
            new UiNode(UiKind.StackItem) { Key = "pick" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "dates",
                Options = dates,
                TextValue = dates[selectedIndex],
                Width = "150px",
                OnSelected = value => Pick(Math.Max(0, dates.IndexOf(value))),
            }),
            new UiNode(UiKind.StackItem) { Key = "next" }.With(new UiNode(UiKind.Button)
            {
                Icon = "chevron-right",
                Disabled = selectedIndex == dates.Count - 1,
                OnClick = () => Pick(selectedIndex + 1),
            }));

        var header = new UiNode(UiKind.Section) { Key = "header" }.With(
            new UiNode(UiKind.Box) { Key = "title", Text = "Anomaly Station", Bold = true, FontSize = "20px" },
            DateDropdown("dates-top"));

        UiNode changes = null;
        if (loaded && dates.Count > 0)
        {
            IReadOnlyList<UiValue> items = c.Data.Arr("changes");
            string date = c.Data.Str("date");
            string title = dates[selectedIndex] + (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day)
                ? " (" + day.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + ")" : "");
            changes = new UiNode(UiKind.Section) { Key = "changes:" + dates[selectedIndex], Title = title }.With(
                new UiNode(UiKind.Table).With(items.Select((change, i) =>
                {
                    (string icon, string color) = Icons.TryGetValue(change.Str("type"), out var known) ? known : Icons["unknown"];
                    return new UiNode(UiKind.TableRow) { Key = "change:" + i }.With(
                        new UiNode(UiKind.TableCell) { Key = "icon", Width = "24px" }.With(new UiNode(UiKind.Icon) { Icon = icon, Color = color }),
                        new UiNode(UiKind.TableCell) { Key = "text" }.With(new UiNode(UiKind.Box) { Text = change.Str("text"), Wrap = true }));
                })));
        }

        var footer = new UiNode(UiKind.Section) { Key = "footer" }.With(
            DateDropdown("dates-bottom")); // not tg's License paragraph (the owner, 2026-10-28)

        return c.Window(675, 650, "Changelog", new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                header,
                changes ?? new UiNode(UiKind.Box) { Key = "loading", Text = "Loading changelog data..." },
                footer))));
    }
}
