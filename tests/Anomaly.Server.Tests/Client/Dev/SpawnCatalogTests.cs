using Anomaly.Client.Shared.Dev;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Dev;

/// <summary>
/// The dev menu's spawn panel and gas tools (the owner, 2026-10-13), the client's engine-free half: tg's spawn panel's list
/// of every /obj, /turf and /mob type with its icon, name and mapping flag, the dev menu's categories and search, and tg's
/// gases with their overlay states and colours.
/// </summary>
public class SpawnCatalogTests
{
    private readonly ITestOutputHelper _out;

    public SpawnCatalogTests(ITestOutputHelper output) => _out = output;

    private static readonly Lazy<SpawnCatalog> Catalog = new(SpawnCatalog.Build);

    [Fact]
    public void TheCatalogHasTgsTypesWithTheirIconsNamesAndTabs()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        SpawnCatalog catalog = Catalog.Value;
        _out.WriteLine($"{catalog.Entries.Count} types in {watch.ElapsedMilliseconds} ms");
        Assert.True(catalog.Entries.Count > 10000, catalog.Entries.Count.ToString());

        SpawnEntry wrench = catalog.Find("/obj/item/wrench");
        Assert.NotNull(wrench);
        Assert.Equal((SpawnTab.Objects, "wrench", "icons/obj/tools.dmi", "wrench"), (wrench.Tab, wrench.Name, wrench.Icon, wrench.IconState));
        Assert.Equal("Tools", SpawnCatalog.CategoriesOf(SpawnTab.Objects)[wrench.Category].Name);
        Assert.True(wrench.IsItem);

        SpawnEntry wall = catalog.Find("/turf/closed/wall");
        Assert.Equal((SpawnTab.Turfs, "Walls"), (wall.Tab, SpawnCatalog.CategoriesOf(SpawnTab.Turfs)[wall.Category].Name));
        SpawnEntry human = catalog.Find("/mob/living/carbon/human");
        Assert.Equal((SpawnTab.Mobs, "Humans"), (human.Tab, SpawnCatalog.CategoriesOf(SpawnTab.Mobs)[human.Category].Name));

        // tg's mapping flag and abstract types
        Assert.True(catalog.Find("/obj/effect/landmark/start").Mapping);
        Assert.False(wrench.Mapping);
        Assert.DoesNotContain(catalog.Entries, e => e.Path.StartsWith("/datum", StringComparison.Ordinal));
    }

    [Fact]
    public void FullTileWindowsAreListedWithTheTurfsTooAndTheFavouritesFilter()
    {
        // the owner (2026-11-10): a full-tile window is a block like a wall; the favourites show only the starred types
        SpawnCatalog catalog = Catalog.Value;
        SpawnEntry[] windows = catalog.Entries.Where(e => e.Path == "/obj/structure/window/reinforced/fulltile").ToArray();
        Assert.Equal(new[] { SpawnTab.Objects, SpawnTab.Turfs }, windows.Select(e => e.Tab));
        Assert.Equal("Full-tile windows", SpawnCatalog.CategoriesOf(SpawnTab.Turfs)[windows[1].Category].Name);
        Assert.False(SpawnCatalog.IsBlockWindow("/obj/structure/window/reinforced"));
        // the Ctrl + click picker finds the Turfs tab's copy when asked for it, the first otherwise
        Assert.Equal(SpawnTab.Turfs, catalog.Find("/obj/structure/window/reinforced/fulltile", SpawnTab.Turfs).Tab);
        Assert.Equal(SpawnTab.Objects, catalog.Find("/obj/structure/window/reinforced/fulltile").Tab);
        Assert.Equal(SpawnTab.Turfs, catalog.Find("/turf/closed/wall", SpawnTab.Objects).Tab);

        var list = new List<SpawnEntry>();
        var only = new HashSet<string> { "/turf/closed/wall", "/obj/structure/window/reinforced/fulltile", "/obj/item/wrench" };
        catalog.Filter(SpawnTab.Turfs, -1, "", false, false, list, only);
        Assert.Equal(new[] { "/obj/structure/window/reinforced/fulltile", "/turf/closed/wall" }, list.Select(e => e.Path).OrderBy(p => p, StringComparer.Ordinal));
        catalog.Filter(SpawnTab.Turfs, -1, "wall", false, false, list, only);
        Assert.Equal(new[] { "/turf/closed/wall" }, list.Select(e => e.Path));
    }

    [Fact]
    public void TheCategoryIsTheMostSpecificPrefix()
    {
        string Name(string path) => SpawnCatalog.CategoriesOf(SpawnCatalog.TabOf(path).Value)[SpawnCatalog.CategoryOf(SpawnCatalog.TabOf(path).Value, path)].Name;
        Assert.Equal("Medical", Name("/obj/item/stack/medical/gauze"));
        Assert.Equal("Materials", Name("/obj/item/stack/sheet/iron"));
        Assert.Equal("Tools", Name("/obj/item/storage/toolbox/mechanical"));
        Assert.Equal("Storage", Name("/obj/item/storage/backpack"));
        Assert.Equal("Food and drink", Name("/obj/item/reagent_containers/cup/glass/coffee"));
        Assert.Equal("Chemistry", Name("/obj/item/reagent_containers/cup/beaker"));
        Assert.Equal("Doors and windows", Name("/obj/machinery/door/airlock"));
        Assert.Equal("Machinery", Name("/obj/machinery/vending/cola"));
        Assert.Equal("Other objects", Name("/obj/vehicle"));
        Assert.Equal("Other items", Name("/obj/item/toy"));
        Assert.Equal("Other items", Name("/obj/item/clothingx")); // a prefix is a whole segment
        Assert.Null(SpawnCatalog.TabOf("/datum/gas"));
        Assert.Null(SpawnCatalog.TabOf("/object"));
    }

    [Fact]
    public void TheSearchMatchesEveryWordOfTheNameOrPath()
    {
        SpawnCatalog catalog = SpawnCatalog.Of(new[]
        {
            new SpawnEntry("/obj/item/wrench", "wrench", null, "icons/obj/tools.dmi", "wrench", SpawnTab.Objects, 2, false, false),
            new SpawnEntry("/obj/item/wrench/caravan", "experimental wrench", null, null, "", SpawnTab.Objects, 2, false, false),
            new SpawnEntry("/obj/effect/landmark/start", "start", null, null, "", SpawnTab.Objects, 16, true, false),
            new SpawnEntry("/turf/closed/wall", "wall", null, null, "", SpawnTab.Turfs, 2, false, false),
            new SpawnEntry("/obj/item/gun", "gun", null, null, "", SpawnTab.Objects, 1, false, true),
        });
        var found = new List<SpawnEntry>();
        catalog.Filter(SpawnTab.Objects, -1, "WRENCH", false, false, found);
        Assert.Equal(new[] { "/obj/item/wrench", "/obj/item/wrench/caravan" }, found.Select(e => e.Path));
        catalog.Filter(null, -1, "experimental item", false, false, found);
        Assert.Equal("/obj/item/wrench/caravan", Assert.Single(found).Path);
        catalog.Filter(SpawnTab.Objects, -1, "", false, false, found);
        Assert.Equal(2, found.Count); // no mapping helper, no abstract type
        catalog.Filter(SpawnTab.Objects, -1, "", true, true, found);
        Assert.Equal(4, found.Count);
        catalog.Filter(null, 2, "", false, false, found);
        Assert.Equal(3, found.Count); // the category index of each tab
    }

    [Fact]
    public void TheGasesAreTgsWithTheirOverlaysAndColours()
    {
        IReadOnlyList<GasEntry> gases = GasCatalog.All;
        Assert.True(gases.Count >= 20, gases.Count.ToString());
        Assert.Equal("n2", gases[0].Id); // the most common: rarity 1000
        GasEntry plasma = GasCatalog.Find("plasma");
        Assert.Equal(("Plasma", "/datum/gas/plasma", "plasma", 0xFFC0CBu), (plasma.Name, plasma.Path, plasma.Overlay, plasma.Rgb));
        Assert.Null(GasCatalog.Find("o2").Overlay); // oxygen is invisible
        Assert.Equal(0x0000FFu, GasCatalog.Find("o2").Rgb);
        Assert.Equal(gases.Count, gases.Select(g => g.Id).Distinct().Count());
    }
}
