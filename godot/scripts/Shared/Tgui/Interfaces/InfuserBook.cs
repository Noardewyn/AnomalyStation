using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/InfuserBook.tsx (gaps P3b, D327): the DNA infusion manual:
// the chapter tabs in rows of three (the restricted tier 3 shown and not clickable), the introduction, an entry's page with its tier
// button, description, threshold, qualities and source, and the page turning (the server plays the page sound). BlockQuote is a
// wrapped Box; the introduction's bold sentence is its own bold Box. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class InfuserBook
{
    // tg: InfuserBook.tsx:40
    private const double PAGE_HEIGHT = 30;

    private sealed record TierData(string Name, string Desc, string Icon);

    // tg: InfuserBook.tsx:42  TIER2TIERDATA
    private static readonly TierData[] Tier2TierData =
    {
        new("Lesser Mutant", "Lesser Mutants usually have a smaller list of potential mutations, and do not have bonuses for infusing many organs. Common species, cosmetics, and things of that sort are here. Always available!", "circle-o"),
        new("Regular Mutant", "Regular Mutants all have bonuses for infusing DNA into yourself, and are common enough to find consistently in a shift. Always available!", "circle-half-stroke"),
        new("Greater Mutant", "Greater Mutants have stronger upsides and downsides along with their bonus, and are harder to find in a shift. Must be unlocked by first unlocking a DNA Mutant bonus of a lower tier.", "circle"),
        new("Abberation", "We've been able to get stronger mutants out of vatgrown specimen, henceforth named \"Abberations\". Abberations have either strong utility purpose, anomalous qualities, or deadly capabilities.", "teeth"),
    };

    private static readonly string[] Tabs =
    {
        "Introduction",
        "Tier 0 - Lesser Mutants",
        "Tier 1 - Regular Mutants",
        "Tier 2 - Greater Mutants",
        "Tier 3 - Abberations - RESTRICTED",
    };

    // tg: InfuserBook.tsx:291  paginateEntries: the introduction's empty chapter, then one chapter per tier up to the highest
    private static List<List<UiValue>> PaginateEntries(IReadOnlyList<UiValue> collection)
    {
        double maxTier = -1;
        foreach (UiValue entry in collection)
            maxTier = System.Math.Max(maxTier, entry.Num("tier"));
        var pages = new List<List<UiValue>>();
        for (double tier = -1; tier <= maxTier; tier++)
        {
            double t = tier;
            pages.Add(collection.Where(entry => entry.Num("tier") == t).ToList());
        }
        return pages;
    }

    // tg: InfuserBook.tsx:85
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> entries = c.Data.Arr("entries");
        int chapter = c.UseState("chapter", 0);
        int pageInChapter = c.UseState("pageInChapter", 0);
        List<List<UiValue>> paginatedEntries = PaginateEntries(entries);
        UiValue currentEntry = chapter < paginatedEntries.Count && pageInChapter < paginatedEntries[chapter].Count ? paginatedEntries[chapter][pageInChapter] : null;

        void SwitchChapter(int newChapter)
        {
            if (chapter == newChapter)
                return;
            c.SetState("chapter", newChapter);
            c.SetState("pageInChapter", 0);
            c.Act("play_flip_sound"); // just so we can play a sound fx on page turn
        }

        void SetPage(int newPage)
        {
            int newChapter = chapter;
            int newPageInChapter;
            if (newPage < 0)
            {
                if (chapter == 0)
                    return;
                newChapter = chapter - 1;
                newPageInChapter = System.Math.Max(paginatedEntries[chapter - 1].Count - 1, 0);
            }
            else if (newPage > paginatedEntries[chapter].Count - 1)
            {
                if (chapter == 3)
                    return;
                newPageInChapter = 0;
                newChapter = chapter + 1;
            }
            else
                newPageInChapter = newPage;
            c.SetState("chapter", newChapter);
            c.SetState("pageInChapter", newPageInChapter);
            c.Act("play_flip_sound"); // just so we can play a sound fx on page turn
        }

        bool restrictedNext = chapter == 3 && pageInChapter == 0;

        var tabRows = new UiNode(UiKind.StackItem) { Key = "tabs" };
        for (int row = 0; row * 3 < Tabs.Length; row++)
        {
            var tabs = new UiNode(UiKind.Tabs) { Key = "tab-row:" + row, Height = "30px", Fill = true, Fluid = true };
            foreach (string tab in Tabs.Skip(row * 3).Take(3))
            {
                int tabIndex = System.Array.IndexOf(Tabs, tab);
                string tabIcon = tabIndex - 1 >= 0 && tabIndex - 1 < Tier2TierData.Length ? Tier2TierData[tabIndex - 1].Icon : "info";
                tabs.With(new UiNode(UiKind.Tab)
                {
                    Key = "tab:" + tabIndex,
                    Icon = tabIcon,
                    Selected = chapter == tabIndex,
                    OnClick = tabIndex == 4 ? null : () => SwitchChapter(tabIndex),
                }.With(new UiNode(UiKind.Box) { Key = "tab-name", Color = tabIndex == 4 ? "red" : null, Text = tab }));
            }
            tabRows.With(tabs);
        }

        int pages = (chapter < paginatedEntries.Count ? paginatedEntries[chapter].Count : 0) + (chapter == 0 ? 1 : 0);
        var turning = new UiNode(UiKind.StackItem) { Key = "turning", TextAlign = "center" }.With(new UiNode(UiKind.Stack) { FontSize = "18px", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "last", Grow = 2 }.With(new UiNode(UiKind.Button) { Key = "last-page", Fluid = true, Text = "Last Page", OnClick = () => SetPage(pageInChapter - 1) }),
            new UiNode(UiKind.StackItem) { Key = "count", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "page-count", Fill = true }.With(
                new UiNode(UiKind.Box) { Key = "page", TextAlign = "center", Text = "Page " + (pageInChapter + 1) + "/" + pages })),
            new UiNode(UiKind.StackItem) { Key = "next", Grow = 2 }.With(new UiNode(UiKind.Button)
            {
                Key = "next-page",
                Fluid = true,
                Color = restrictedNext ? "black" : null,
                Text = restrictedNext ? "RESTRICTED" : "Next Page",
                OnClick = () => SetPage(pageInChapter + 1),
            })));

        return c.Window(620, 500, "DNA Infusion Manual", new UiNode(UiKind.Stack) { Key = "book", Vertical = true }.With(
            tabRows,
            new UiNode(UiKind.StackItem) { Key = "page-body" }.With(chapter == 0 ? Instructions() : currentEntry != null ? Entry(currentEntry) : null),
            turning));
    }

    // tg: InfuserBook.tsx:201  InfuserInstructions
    private static UiNode Instructions() =>
        new UiNode(UiKind.Section) { Key = "instructions", Title = "DNA Infusion Guide", Height = PAGE_HEIGHT }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "q1", FontSize = "16px", Text = "What does it do?" },
            new UiNode(UiKind.StackItem) { Key = "a1" }.With(
                new UiNode(UiKind.Box) { Key = "a1-text", Color = "label", Wrap = true, Text = "DNA Infusion is the practice of integrating dead creature DNA into yourself, mutating one of your organs into a genetic slurry that sits somewhere between being yours or the creature's. While this does bring you further away from being human, and gives a slew of... unfortunate side effects, it also grants new capabilities." },
                new UiNode(UiKind.Box) { Key = "a1-bold", Color = "label", Bold = true, Wrap = true, Text = "Above all else, you have to understand that gene-mutants are usually very good at specific things, especially with their threshold bonuses." }),
            new UiNode(UiKind.StackItem) { Key = "q2", FontSize = "16px", Text = "I'm sold! How do I do it?" },
            new UiNode(UiKind.StackItem) { Key = "a2" }.With(
                new UiNode(UiKind.Box) { Key = "step1", Color = "label", Wrap = true, Text = "1. Load a dead creature into the machine. This is what you're infusing from." },
                new UiNode(UiKind.Box) { Key = "step2", Color = "label", Text = "2. Enter the machine, like you would the DNA scanner." },
                new UiNode(UiKind.Box) { Key = "step3", Color = "label", Text = "3. Have someone activate the machine externally." },
                new UiNode(UiKind.Box) { Key = "done", Color = "white", Wrap = true, Text = "And you're done! Note that the infusion source will be obliterated in the process." })));

    // tg: InfuserBook.tsx:246  InfuserEntry
    private static UiNode Entry(UiValue entry)
    {
        int tier = (int)entry.Num("tier");
        TierData tierData = tier >= 0 && tier < Tier2TierData.Length ? Tier2TierData[tier] : Tier2TierData[0];
        string threshold = entry.Str("threshold_desc", null);
        string name = entry.Str("name");
        var qualities = new UiNode(UiKind.StackItem) { Key = "qualities", Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "qualities-title", Text = "Qualities:" });
        int index = 0;
        foreach (UiValue quality in entry.Arr("qualities"))
            qualities.With(new UiNode(UiKind.Box) { Key = "quality:" + index++, Color = "label", Wrap = true, Text = "- " + quality.AsStr() });
        return new UiNode(UiKind.Section)
        {
            Key = "entry",
            Fill = true,
            Title = name + " Mutant",
            Height = PAGE_HEIGHT,
            Buttons = new UiNode(UiKind.Button) { Key = "tier", Tooltip = tierData.Desc, Icon = tierData.Icon, Text = tierData.Name },
        }.With(new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "quote" }.With(new UiNode(UiKind.Box)
            {
                Key = "desc",
                Wrap = true,
                Text = entry.Str("desc") + " " + (!string.IsNullOrEmpty(threshold) ? "If a subject infuses with enough DNA, " + threshold : ""),
            }),
            qualities,
            new UiNode(UiKind.Divider) { Key = "divider" },
            new UiNode(UiKind.StackItem) { Key = "source" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "source-pre", Text = "Created from infusing" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "source-mob", Color = name == "Rejected" ? "red" : "green", Text = entry.Str("infuse_mob_name") }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "source-post", Text = "DNA into a subject." })))));
    }
}
