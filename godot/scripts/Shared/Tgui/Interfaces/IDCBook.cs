using System;
using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/IDCBook.tsx: the IDC-27, the manual of the diseases and
// the advanced viruses' symptoms, sorted by form then name in the paginated book, each entry tinted by its form. A blank page shows
// the DeForest logo's name (tg's logo is an SVG mask). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class IDCBook
{
    // tg: IDCBook.tsx:24
    private static bool IsSymptom(UiValue entry) => entry.Str("form").ToLowerInvariant() == "symptom";

    // tg: IDCBook.tsx:28
    private static string FormToColor(string form) => form.ToLowerInvariant() switch
    {
        "symptom" => "lightgreen",
        "condition" or "infection" or "parasite" => "lightsalmon",
        "virus" or "bacteria" or "fungus" => "lightblue",
        _ => "white",
    };

    // tg: IDCBook.tsx:45
    private static UiNode FormatSpreadBy(string spreadBy)
    {
        (string Name, string Example)? known = spreadBy.ToLowerInvariant() switch
        {
            "airborne" => ("Airborne", "(e.g. coughing, sneezing)"),
            "skin contact" or "contact" => ("Contact", "(e.g. touching infected persons)"),
            "fluid contact" => ("Liquids", "(e.g. touching contaminated fluids)"),
            "blood" => ("Blood", "(e.g. tranfusion of infected blood)"),
            "none" => ("None", "(non-contagious)"),
            _ => null,
        };
        if (known is not (string name, string example))
            return new UiNode(UiKind.Box) { Key = "spread", Text = spreadBy };
        return new UiNode(UiKind.Stack) { Key = "spread", Align = "end" }.With(
            new UiNode(UiKind.StackItem) { Key = "name", Text = name },
            new UiNode(UiKind.StackItem) { Key = "example", FontSize = "10px", Text = example });
    }

    // tg: IDCBook.tsx:105
    private static double EstimateHeight(UiValue entry)
    {
        const double titleHeight = 40;
        const double extraSpacing = 40;
        if (IsSymptom(entry))
        {
            double descHeightSymptom = Math.Ceiling(entry.Str("desc").Length / 50.0) * 14;
            double illnessHeight = entry.Str("illness") != "Unidentified" ? 10 : 0;
            const double curedByHeightSymptom = 10;
            const double levelHeight = 10;
            return titleHeight + descHeightSymptom + illnessHeight + curedByHeightSymptom + extraSpacing + levelHeight;
        }
        double descHeight = Math.Ceiling(entry.Str("desc").Length / 50.0) * 14;
        const double agentHeight = 15;
        const double spreadByHeight = 15;
        const double curedByHeight = 15;
        return titleHeight + descHeight + agentHeight + spreadByHeight + curedByHeight + extraSpacing;
    }

    private static UiNode Line(string key, string label, UiNode value) =>
        new UiNode(UiKind.StackItem) { Key = key }.With(new UiNode(UiKind.Box) { Key = key + "-label", Color = "label", Text = label }, value);

    private static UiNode Line(string key, string label, string value) => Line(key, label, new UiNode(UiKind.Box) { Key = key, Wrap = true, Text = value });

    // tg: IDCBook.tsx:146  renderDSMEntry (tg's name for the IDC's entry)
    private static UiNode RenderEntry(UiValue entry)
    {
        var body = new UiNode(UiKind.Stack) { Key = "body", Vertical = true, Background = FormToColor(entry.Str("form")) };
        if (IsSymptom(entry))
        {
            body.With(Line("desc", "Description:", entry.Str("desc")), Line("level", "Level:", entry.Str("level")));
            if (entry.Str("illness") != "Unidentified")
                body.With(Line("illness", "Common associated illness:", "\"" + entry.Str("illness") + "\""));
            if (entry.Truthy("cured_by"))
                body.With(Line("cured_by", "Common cures:", entry.Str("cured_by")));
        }
        else
        {
            body.With(
                Line("agent", "Agent:", entry.Str("agent")),
                Line("desc", "Description:", entry.Str("desc")),
                Line("spread_by", "Spread By:", FormatSpreadBy(entry.Str("spread_by"))),
                Line("cured_by", "Cured By:", entry.Str("cured_by")));
        }
        var title = new UiNode(UiKind.Stack) { Key = "entry_title", Align = "end" }.With(
            new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, Text = entry.Str("name") },
            new UiNode(UiKind.StackItem) { Key = "form", FontSize = "10px", Text = "(" + entry.Str("form") + ")" });
        return new UiNode(UiKind.Section) { Key = "disease", Width = "100%", Title = entry.Str("name"), LabelNode = title }.With(body);
    }

    // tg: IDCBook.tsx:218  renderTOCEntry
    private static UiNode RenderTocEntry(UiValue entry) =>
        new UiNode(UiKind.Stack) { Key = "toc", Align = "end" }.With(
            new UiNode(UiKind.StackItem) { Key = "name", Text = entry.Str("name") },
            new UiNode(UiKind.StackItem) { Key = "form", FontSize = "10px", Text = "(" + entry.Str("form") + ")" });

    // tg: IDCBook.tsx:245
    public static UiNode Render(TguiContext c)
    {
        List<UiValue> diseaseEntries = c.Data.Arr("diseases").ToList();
        // a.form > b.form ? 1 : a.form < b.form ? -1 : a.name > b.name ? 1 : -1 (JavaScript's ordinal string comparison)
        diseaseEntries.Sort((a, b) =>
        {
            int form = string.CompareOrdinal(a.Str("form"), b.Str("form"));
            if (form != 0)
                return form > 0 ? 1 : -1;
            return string.CompareOrdinal(a.Str("name"), b.Str("name")) > 0 ? 1 : -1;
        });
        return PaginatedBook.BookUI(c, diseaseEntries, "IDC-27", EstimateHeight, RenderEntry, RenderTocEntry, PaginatedBook.DeForestPage);
    }
}
