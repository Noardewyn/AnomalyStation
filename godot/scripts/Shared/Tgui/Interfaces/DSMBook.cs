using System;
using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DSMBook.tsx: the SDSM-35, the psychologist's manual
// of the brain traumas, the trauma-like quirks, the addictions and the phobias, sorted by name in the paginated book. A blank page
// shows the DeForest logo's name (tg's logo is an SVG mask). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class DSMBook
{
    // tg: DSMBook.tsx:20
    private static bool CheckIdenticalTraumaNames(UiValue trauma) => trauma.Str("full_name").ToLowerInvariant() == trauma.Str("scan_name").ToLowerInvariant();

    // tg: DSMBook.tsx:24
    private static double EstimateHeight(UiValue entry)
    {
        const double titleHeight = 40;
        double subtitleHeight = CheckIdenticalTraumaNames(entry) ? 0 : 20;
        double descHeight = Math.Ceiling(entry.Str("desc").Length / 50.0) * 12.5;
        double symptomsHeight = Math.Ceiling(entry.Str("symptoms").Length / 50.0) * 12.5;
        const double extraSpacing = 40;
        return titleHeight + subtitleHeight + descHeight + symptomsHeight + extraSpacing;
    }

    // tg: DSMBook.tsx:40  renderDSMEntry
    private static UiNode RenderEntry(UiValue entry)
    {
        var title = new UiNode(UiKind.Stack) { Key = "entry_title", Vertical = true }.With(new UiNode(UiKind.StackItem) { Key = "full_name", Text = entry.Str("full_name") });
        if (!CheckIdenticalTraumaNames(entry))
            title.With(new UiNode(UiKind.StackItem) { Key = "aka", FontSize = "10px", Text = "...aka \"" + entry.Str("scan_name") + "\"" });
        return new UiNode(UiKind.Section) { Key = "trauma", Width = "100%", Title = entry.Str("full_name"), LabelNode = title }.With(
            new UiNode(UiKind.Stack) { Key = "body", Vertical = true, Background = "white", Width = "100%" }.With(
                new UiNode(UiKind.StackItem) { Key = "desc" }.With(
                    new UiNode(UiKind.Box) { Key = "desc-label", Color = "label", Text = "Description:" },
                    new UiNode(UiKind.Box) { Key = "desc", Wrap = true, Text = entry.Str("desc") }),
                new UiNode(UiKind.StackItem) { Key = "symptoms" }.With(
                    new UiNode(UiKind.Box) { Key = "symptoms-label", Color = "label", Text = "Diagnosis:" },
                    new UiNode(UiKind.Box) { Key = "symptoms", Wrap = true, Text = entry.Str("symptoms") })));
    }

    // tg: DSMBook.tsx:73  renderTOCEntry
    private static UiNode RenderTocEntry(UiValue entry)
    {
        var stack = new UiNode(UiKind.Stack) { Key = "toc", Align = "end" }.With(new UiNode(UiKind.StackItem) { Key = "full_name", Text = entry.Str("full_name") });
        if (!CheckIdenticalTraumaNames(entry))
            stack.With(new UiNode(UiKind.StackItem) { Key = "scan_name", FontSize = "10px", Text = "(" + entry.Str("scan_name") + ")" });
        return stack;
    }

    // tg: DSMBook.tsx:100
    public static UiNode Render(TguiContext c)
    {
        List<UiValue> traumaEntries = c.Data.Arr("traumas").OrderBy(t => t.Str("full_name"), StringComparer.Ordinal).ToList();
        return PaginatedBook.BookUI(c, traumaEntries, "SDSM-35", EstimateHeight, RenderEntry, RenderTocEntry, PaginatedBook.DeForestPage);
    }
}
