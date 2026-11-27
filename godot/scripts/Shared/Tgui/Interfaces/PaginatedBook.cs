using System;
using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/PaginatedBook.tsx: the paginated book the DSM
// and IDC manuals draw. The table of contents first (thirty pixels a line), then the entries laid out by their estimated heights,
// two pages side by side, the page turner asking the server for the page sound. The TOC's dotted leader is a rule. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

internal static class PaginatedBook
{
    private sealed record TocEntry(UiValue Entry, int DisplayedPage)
    {
        public int EntryPage { get; set; } = -1;
    }

    private sealed record PageEntry(UiValue Entry, int Page);

    // tg: PaginatedBook.tsx:20  getTOCWithPages
    private static List<TocEntry> GetTocWithPages(IReadOnlyList<UiValue> entries, double windowHeight)
    {
        double pageHeight = windowHeight - 50;
        var pages = new List<TocEntry>();
        int currentPage = 1;
        double currentPageHeight = 0;
        foreach (UiValue entry in entries)
        {
            const double estimatedHeight = 30;
            if (currentPageHeight + estimatedHeight > pageHeight)
            {
                currentPage += 1;
                currentPageHeight = 0;
            }
            pages.Add(new TocEntry(entry, currentPage));
            currentPageHeight += estimatedHeight;
        }
        return pages;
    }

    // tg: PaginatedBook.tsx:50  getEntriesWithPages
    private static List<PageEntry> GetEntriesWithPages(IReadOnlyList<UiValue> entries, double windowHeight, int startPage, Func<UiValue, double> estimateHeight)
    {
        double pageHeight = windowHeight - 50;
        var pages = new List<PageEntry>();
        int currentPage = startPage;
        double currentPageHeight = 0;
        foreach (UiValue entry in entries)
        {
            double estimatedHeight = estimateHeight(entry);
            if (currentPageHeight + estimatedHeight > pageHeight)
            {
                currentPage += 1;
                currentPageHeight = 0;
            }
            pages.Add(new PageEntry(entry, currentPage));
            currentPageHeight += estimatedHeight;
        }
        return pages;
    }

    // tg: PaginatedBook.tsx:94  TOCEntryComponent
    private static UiNode TocEntryComponent(TguiContext c, TocEntry entry, Func<UiValue, UiNode> renderTocEntry) =>
        new UiNode(UiKind.Stack) { Key = "toc:" + entry.Entry.Str("id"), TextAlign = "center", FontSize = "13px" }.With(
            new UiNode(UiKind.StackItem) { Key = "name" }.With(renderTocEntry(entry.Entry)),
            new UiNode(UiKind.StackItem) { Key = "leader", Grow = 1 }.With(new UiNode(UiKind.Divider) { Key = "dots" }),
            new UiNode(UiKind.StackItem) { Key = "page", Width = "10%" }.With(new UiNode(UiKind.Button)
            {
                Key = "goto:" + entry.Entry.Str("id"),
                Fluid = true,
                Text = entry.EntryPage.ToString(System.Globalization.CultureInfo.InvariantCulture),
                OnClick = () => c.SetState("page", entry.EntryPage % 2 == 0 ? entry.EntryPage - 1 : entry.EntryPage),
            }));

    // tg: PaginatedBook.tsx:142  FakePage
    private static UiNode FakePage(TguiContext c, string key, List<TocEntry> tocDisplay, List<PageEntry> entryDisplay, Func<UiValue, UiNode> renderEntry,
        Func<UiValue, UiNode> renderTocEntry, Func<UiNode> blankPage)
    {
        var stack = new UiNode(UiKind.Stack) { Key = key, Vertical = true, Fill = true };
        foreach (TocEntry entry in tocDisplay)
            stack.With(new UiNode(UiKind.StackItem) { Key = "toc-item:" + entry.Entry.Str("id") }.With(TocEntryComponent(c, entry, renderTocEntry)));
        foreach (PageEntry entry in entryDisplay)
            stack.With(new UiNode(UiKind.StackItem) { Key = "entry-item:" + entry.Entry.Str("id") }.With(new UiNode(UiKind.Section) { Key = "entry:" + entry.Entry.Str("id") }.With(renderEntry(entry.Entry))));
        if (tocDisplay.Count == 0 && entryDisplay.Count == 0)
            stack.With(blankPage?.Invoke() ?? new UiNode(UiKind.StackItem) { Key = "blank", Text = "This page intentionally left blank." });
        return new UiNode(UiKind.Section) { Key = key, Fill = true }.With(stack);
    }

    // tg: PaginatedBook.tsx:228  PageTurn
    private static UiNode PageTurn(TguiContext c, int page, string title, int maxPage)
    {
        void SetPage(int p)
        {
            c.SetState("page", p);
            c.Act("play_flip_sound");
        }

        return new UiNode(UiKind.Stack) { Key = "page-turn" }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "first", Icon = "angles-left", Fluid = true, Disabled = page <= 1, OnClick = () => SetPage(1) }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "previous", Icon = "angle-left", Fluid = true, Disabled = page <= 1, OnClick = () => SetPage(page - 2) }),
            new UiNode(UiKind.StackItem) { Key = "numbers", TextAlign = "center", Grow = 1 }.With(new UiNode(UiKind.Stack) { Fill = true, FontSize = "15px" }.With(
                new UiNode(UiKind.StackItem) { Key = "left-page", Grow = 1, Bold = true, Text = page.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new UiNode(UiKind.StackItem) { Key = "title", Color = "label", Text = "~ " + title + " ~" },
                new UiNode(UiKind.StackItem) { Key = "right-page", Grow = 1, Bold = true, Text = (page + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) })),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "next", Icon = "angle-right", Fluid = true, Disabled = page + 1 >= maxPage, OnClick = () => SetPage(page + 2) }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = "last", Icon = "angles-right", Fluid = true, Disabled = page + 1 >= maxPage, OnClick = () => SetPage(maxPage - maxPage % 2),
            }));
    }

    // tg: PaginatedBook.tsx:313  BookUI
    internal static UiNode BookUI(TguiContext c, IReadOnlyList<UiValue> bookData, string title, Func<UiValue, double> estimateHeight, Func<UiValue, UiNode> renderEntry,
        Func<UiValue, UiNode> renderTocEntry, Func<UiNode> blankPage)
    {
        const double windowHeight = 565;
        List<TocEntry> allTocWithPages = GetTocWithPages(bookData, windowHeight);
        int finalTocPage = allTocWithPages.Aggregate(1, (maxPage, entry) => Math.Max(maxPage, entry.DisplayedPage));
        if (finalTocPage % 2 != 0)
            finalTocPage += 1;
        List<PageEntry> allEntriesWithPages = GetEntriesWithPages(bookData, windowHeight, finalTocPage + 1, estimateHeight);
        foreach (UiValue entry in bookData)
        {
            string id = entry.Str("id");
            PageEntry entryPage = allEntriesWithPages.FirstOrDefault(e => e.Entry.Str("id") == id);
            TocEntry tocEntry = allTocWithPages.FirstOrDefault(e => e.Entry.Str("id") == id);
            if (entryPage != null && tocEntry != null)
                tocEntry.EntryPage = entryPage.Page;
        }
        int lastPage = allEntriesWithPages.Aggregate(finalTocPage, (maxPage, entry) => Math.Max(maxPage, entry.Page));
        int page = c.UseState("page", 1);

        c.WindowTheme = "ntos_lightmode";
        return c.Window(765, windowHeight, title, new UiNode(UiKind.Stack) { Key = "book", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "turner" }.With(new UiNode(UiKind.Section) { Key = "turner" }.With(
                PageTurn(c, page, page <= finalTocPage ? "Table of Contents" : title, lastPage))),
            new UiNode(UiKind.StackItem) { Key = "pages", Grow = 1 }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "left", Grow = 1 }.With(FakePage(c, "left", allTocWithPages.Where(e => e.DisplayedPage == page).ToList(),
                    allEntriesWithPages.Where(e => e.Page == page).ToList(), renderEntry, renderTocEntry, blankPage)),
                new UiNode(UiKind.StackItem) { Key = "right", Grow = 1 }.With(FakePage(c, "right", allTocWithPages.Where(e => e.DisplayedPage == page + 1).ToList(),
                    allEntriesWithPages.Where(e => e.Page == page + 1).ToList(), renderEntry, renderTocEntry, blankPage))))));
    }

    // tg: DSMBook.tsx:4 and IDCBook.tsx:4  the DeForest logo of a blank page (styles/interfaces/DeForestLogo.scss): its company name
    internal static UiNode DeForestPage() => new UiNode(UiKind.StackItem) { Key = "deforest_logo", TextAlign = "center", Grow = 1, Color = "label", Text = "DeForest Medical Corporation" };
}
