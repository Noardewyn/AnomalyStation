using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosRecords.jsx (gaps S4a, D327): the Medical and
// Security Records apps, the crew's records filtered by a search. A hidden section is left out. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosRecords
{
    // tg: NtosRecords.jsx:8
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string searchTerm = c.UseState("searchTerm", "");
        string mode = data.Str("mode");
        IReadOnlyList<UiValue> records = data.Arr("records");

        var content = new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(
            new UiNode(UiKind.Section) { Key = "head", TextAlign = "center", Text = "NANOTRASEN PERSONNEL RECORDS (CLASSIFIED)" },
            new UiNode(UiKind.Section) { Key = "search" }.With(new UiNode(UiKind.Input)
            {
                Key = "filter",
                Tooltip = "Filter results...",
                TextValue = searchTerm,
                Fluid = true,
                TextAlign = "center",
                OnInput = value => c.SetState("searchTerm", value),
            }));

        if (mode == "security")
        {
            var isMatchingSearchTerms = TguiFormat.CreateSearch<UiValue>(searchTerm, r =>
                r.Str("name") + " " + r.Str("rank") + " " + r.Str("species") + " " + r.Str("gender") + " " + Plain(r.Get("age")) + " " + r.Str("fingerprint"));
            int i = 0;
            foreach (UiValue record in records)
            {
                i++;
                if (!(searchTerm == "" || isMatchingSearchTerms(record)))
                    continue;
                string wanted = record.Str("wanted");
                content.With(new UiNode(UiKind.Section) { Key = "record:" + i }.With(
                    new UiNode(UiKind.Box) { Key = "name", Bold = true, Icon = "user", Text = record.Str("name") },
                    new UiNode(UiKind.Box) { Key = "rank", Text = "Rank: " + record.Str("rank") },
                    new UiNode(UiKind.Box) { Key = "species", Text = "Species: " + record.Str("species") },
                    new UiNode(UiKind.Box) { Key = "gender", Text = "Gender: " + record.Str("gender") },
                    new UiNode(UiKind.Box) { Key = "age", Text = "Age: " + Plain(record.Get("age")) },
                    new UiNode(UiKind.Box) { Key = "fingerprint", Text = "Fingerprint Hash: " + record.Str("fingerprint") },
                    new UiNode(UiKind.Box) { Key = "wanted", Text = "Criminal Status: " + (string.IsNullOrEmpty(wanted) ? "DELETED" : wanted) }));
            }
        }
        if (mode == "medical")
        {
            var isMatchingSearchTerms = TguiFormat.CreateSearch<UiValue>(searchTerm, r =>
                r.Str("name") + " " + r.Str("bloodtype") + " " + r.Str("mental_status") + " " + r.Str("physical_status"));
            int i = 0;
            foreach (UiValue record in records)
            {
                i++;
                if (!(searchTerm == "" || isMatchingSearchTerms(record)))
                    continue;
                var section = new UiNode(UiKind.Section) { Key = "record:" + i }.With(
                    new UiNode(UiKind.Box) { Key = "name", Bold = true, Icon = "user", Text = record.Str("name") },
                    new UiNode(UiKind.Box) { Key = "bloodtype", Text = "Bloodtype: " + record.Str("bloodtype") },
                    new UiNode(UiKind.Box) { Key = "minor", Text = "Minor Disabilities:" });
                AddEntries(section, "mi", record.Get("mi_dis"));
                section.With(new UiNode(UiKind.Box) { Key = "major", Text = "Major Disabilities:" });
                AddEntries(section, "ma", record.Get("ma_dis"));
                section.With(new UiNode(UiKind.Box) { Key = "notes", Text = "Notes: " + record.Str("notes") });
                content.With(section);
            }
        }

        return NtosWindow.Render(c, 600, 800, null, content);
    }

    // record.mi_dis?.split('<br>').map((entry, index) => <Box key={index}>&#8226; {entry}</Box>)
    private static void AddEntries(UiNode section, string key, UiValue value)
    {
        if (value == null || value.IsNull || value.Text == null)
            return;
        string[] entries = value.Text.Split("<br>");
        for (int index = 0; index < entries.Length; index++)
            section.With(new UiNode(UiKind.Box) { Key = key + ":" + index, Text = "• " + entries[index] });
    }

    // A record field as JavaScript prints it in a template string (a number, a text, or "null")
    private static string Plain(UiValue value) =>
        value == null || value.IsNull ? "null" : value.Text ?? TguiFormat.JsNumber(value.Number);
}
