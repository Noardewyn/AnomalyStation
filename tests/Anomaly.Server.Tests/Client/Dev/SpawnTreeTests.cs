using Anomaly.Client.Shared.Dev;

namespace Anomaly.Server.Tests.Client.Dev;

/// <summary>
/// The list of types as a tree by their paths (the map editor's palette and the dev menu's spawn panel, the engine-free half): the types fold
/// under the branches of their paths, a branch says how many it holds, the part of the path every type shares is no level, and the branches
/// over a type bring its row into view.
/// </summary>
public class SpawnTreeTests
{
    private static readonly Lazy<SpawnCatalog> Catalog = new(() => SpawnCatalog.Build(areas: true));

    private static SpawnEntry Type(string path) => new(path, path.Substring(path.LastIndexOf('/') + 1), null, null, string.Empty, SpawnTab.Objects, 0, false, false);

    // The rows as text: two spaces a level, + a folded branch, - an open one, * a branch that is no type of the list, and how many types a branch holds.
    private static List<string> Shown(SpawnTree tree, params string[] open)
    {
        var rows = new List<SpawnRow>();
        tree.Rows(new HashSet<string>(open), rows);
        return rows.Select(r => new string(' ', 2 * r.Depth) + (r.Branch ? r.Open ? "- " : "+ " : string.Empty) + r.Leaf + (r.Entry == null ? "*" : string.Empty)
            + (r.Branch ? $" ({r.Types})" : string.Empty)).ToList();
    }

    [Fact]
    public void ATreeFoldsTheTypesUnderTheirPathsAndLeavesTheSharedPartOut()
    {
        var tree = new SpawnTree(new[]
        {
            Type("/obj/machinery/light"), Type("/obj/machinery/power/rtg"), Type("/obj/machinery/power/rtg/advanced"), Type("/obj/machinery/power/smes"),
            Type("/obj/structure/table"),
        });
        // folded: the top rows alone; machinery and structure are no types of the list, and say how many they hold
        Assert.Equal(new[] { "+ machinery* (4)", "+ structure* (1)" }, Shown(tree));
        Assert.Equal(new[] { "- machinery* (4)", "  light", "  + power* (3)", "+ structure* (1)" }, Shown(tree, "/obj/machinery"));
        // a branch that is a type of the list too: rtg, which holds advanced
        Assert.Equal(new[] { "- machinery* (4)", "  light", "  - power* (3)", "    - rtg (1)", "      advanced", "    smes", "+ structure* (1)" },
            Shown(tree, "/obj/machinery", "/obj/machinery/power", "/obj/machinery/power/rtg"));
        // an open branch under a folded one shows nothing
        Assert.Equal(new[] { "+ machinery* (4)", "+ structure* (1)" }, Shown(tree, "/obj/machinery/power"));
    }

    [Fact]
    public void ATypeThatIsTheSharedPathStandsFirstAndOverNamesTheBranchesToOpen()
    {
        // (in any order, a path twice)
        var tree = new SpawnTree(new[]
        {
            Type("/turf/open/floor/iron/dark"), Type("/turf/open/floor"), Type("/turf/open/floor/iron"), Type("/turf/open/floor/wood"), Type("/turf/open/floor/iron"),
        });
        Assert.Equal(new[] { "floor", "+ iron (1)", "wood" }, Shown(tree));
        Assert.Equal(new[] { "/turf/open/floor/iron" }, tree.Over("/turf/open/floor/iron/dark"));
        Assert.Empty(tree.Over("/turf/open/floor/wood"));
        Assert.Empty(tree.Over("/turf/closed/wall"));
        Assert.Equal(new[] { "floor", "- iron (1)", "  dark", "wood" }, Shown(tree, tree.Over("/turf/open/floor/iron/dark").ToArray()));

        Assert.Empty(Shown(new SpawnTree(Array.Empty<SpawnEntry>())));
        Assert.Equal(new[] { "wrench" }, Shown(new SpawnTree(new[] { Type("/obj/item/wrench") })));

        var flat = new List<SpawnRow>();
        SpawnTree.Flat(new[] { Type("/obj/item/wrench"), Type("/obj/machinery/light") }, flat);
        Assert.Equal(new[] { ("/obj/item/wrench", 0, false), ("/obj/machinery/light", 0, false) }, flat.Select(r => (r.Path, r.Depth, r.Branch)));
    }

    [Fact]
    public void TheTreeOfTgsObjectsHoldsEveryTypeOfTheListOnceInTheListsOrder()
    {
        var list = new List<SpawnEntry>();
        Catalog.Value.Filter(SpawnTab.Objects, -1, null, mapping: true, abstracts: true, list);
        var tree = new SpawnTree(list);
        var rows = new List<SpawnRow>();
        tree.Rows(new HashSet<string>(), rows);
        // folded, the twenty thousand objects are a short list of tg's own branches
        Assert.True(rows.Count > 3 && rows.Count < list.Count / 20, $"{rows.Count} top rows of {list.Count} types");
        Assert.Contains(rows, r => r.Path == "/obj/machinery" && r.Branch && r.Depth == 0 && r.Types > 500);
        Assert.Contains(rows, r => r.Path == "/obj/item" && r.Branch && r.Depth == 0);

        // the way down to a type: with the branches over it open, its row is there with its type
        IReadOnlyList<string> over = tree.Over("/obj/machinery/power/rtg");
        Assert.Equal(new[] { "/obj/machinery", "/obj/machinery/power" }, over);
        tree.Rows(new HashSet<string>(over), rows);
        SpawnRow rtg = rows.Single(r => r.Path == "/obj/machinery/power/rtg");
        Assert.Equal((2, "rtg", "/obj/machinery/power/rtg"), (rtg.Depth, rtg.Leaf, rtg.Entry?.Path));

        // with every branch open the tree shows every type of the list once, in the list's order
        tree.Rows(new HashSet<string>(list.SelectMany(e => tree.Over(e.Path))), rows);
        Assert.Equal(list.Select(e => e.Path), rows.Where(r => r.Entry != null).Select(r => r.Path));

        // the areas of the map editor's fourth list fold the same way
        Catalog.Value.Filter(SpawnTab.Areas, -1, null, mapping: true, abstracts: true, list);
        var areas = new SpawnTree(list);
        Assert.Equal(new[] { "/area/station", "/area/station/engineering" }, areas.Over("/area/station/engineering/main"));
    }
}
