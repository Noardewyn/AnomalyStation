using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// Gaps ST1 (D142): the client draws the flora and the potted plants with tg's sprites. Every state flora.dm and kirbyplants.dm set
/// (the random styles' looks, the felled tree's stump, the Christmas trees, the withered and plastic plants, Potty and its light) is
/// in the converted sheets and draws pixels; Potty's blinking light overlay adds to its picture; and a flora piece a shovel uproots
/// (tg's <c>transform = M.Turn(pick(-90, 90))</c>, sent as the appearance's transform) is drawn turned a quarter clockwise.
/// </summary>
public class FloraPictureTests
{
    private static IEnumerable<string> Numbered(string prefix, int from, int to, string suffix = "")
        => Enumerable.Range(from, to - from + 1).Select(i => prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix);

    private static readonly (string Icon, string[] States)[] Sheets =
    {
        ("icons/obj/fluff/flora/plants.dmi", Enumerable.Range(1, 29).Select(i => "plant-" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture))
            .Concat(new[] { "applebush", "potty", "potty_light", "fern" }).ToArray()),
        ("icons/obj/fluff/flora/pinetrees.dmi", new[] { "pine_1", "pine_2", "pine_3", "pine_c", "pinepresents", "tree_stump", "festivus_pole", "anchored_rod" }),
        ("icons/obj/fluff/flora/deadtrees.dmi", Numbered("tree_", 1, 6).ToArray()),
        ("icons/obj/fluff/flora/jungletrees.dmi", Numbered("tree", 1, 6).ToArray()),
        ("icons/obj/fluff/flora/jungletreesmall.dmi", Numbered("tree", 1, 6).ToArray()),
        ("icons/obj/fluff/beach2.dmi", new[] { "palm1", "palm2" }),
        ("icons/obj/fluff/beach.dmi", new[] { "coconuts" }),
        ("icons/obj/fluff/flora/ausflora.dmi", Numbered("firstbush_", 1, 4).Concat(Numbered("reedbush_", 1, 4)).Concat(Numbered("leafybush_", 1, 3))
            .Concat(Numbered("palebush_", 1, 4)).Concat(Numbered("stalkybush_", 1, 3)).Concat(Numbered("grassybush_", 1, 4)).Concat(Numbered("sparsegrass_", 1, 3))
            .Concat(Numbered("fullgrass_", 1, 3)).Concat(Numbered("fernybush_", 1, 3)).Concat(Numbered("sunnybush_", 1, 3)).Concat(Numbered("genericbush_", 1, 4))
            .Concat(Numbered("pointybush_", 1, 4)).Concat(Numbered("lavendergrass_", 1, 4)).Concat(Numbered("ywflowers_", 1, 3)).Concat(Numbered("brflowers_", 1, 3))
            .Concat(Numbered("ppflowers_", 1, 3)).ToArray()),
        ("icons/obj/fluff/flora/snowflora.dmi", Numbered("snowgrass", 1, 3, "bb").Concat(Numbered("snowgrass", 1, 3, "gb")).Concat(Numbered("snowgrassall", 1, 3))
            .Concat(Numbered("snowbush", 1, 6)).ToArray()),
        ("icons/obj/fluff/flora/jungleflora.dmi", Numbered("grassa", 1, 5).Concat(Numbered("grassb", 1, 5)).Concat(Numbered("busha", 1, 3)).Concat(Numbered("bushb", 1, 3))
            .Concat(Numbered("bushc", 1, 3)).Concat(Numbered("rock", 1, 5)).ToArray()),
        ("icons/obj/fluff/flora/largejungleflora.dmi", Numbered("bush", 1, 3).Concat(Numbered("rocks", 1, 3)).ToArray()),
        ("icons/obj/fluff/flora/xenoflora.dmi", Numbered("lunar_plant", 1, 3).ToArray()),
        ("icons/obj/fluff/flora/rocks.dmi", Numbered("basalt", 1, 4).Concat(Numbered("lavarocks", 1, 3)).Concat(Numbered("lavarocks_siderite", 1, 3))
            .Concat(Numbered("lavarocks_shale", 1, 3)).Concat(Numbered("volcano_", 1, 4)).ToArray()),
        ("icons/obj/mining_zones/terrain.dmi", Numbered("siderite_growth", 1, 3).ToArray()),
    };

    [RealIconsFact]
    public void EveryLookOfTheFloraAndThePottedPlantsDraws()
    {
        IconLibrary library = RealIcons.NewLibrary();
        var missing = new List<string>();
        foreach ((string path, string[] states) in Sheets)
        {
            if (!library.TryGetIcon(path, out IIcon icon))
            {
                missing.Add(path);
                continue;
            }
            foreach (string name in states)
            {
                if (!icon.TryGetState(name, out IIconState state) || state.Get(0, 0).IsEmpty())
                    missing.Add(path + ":" + name);
            }
        }
        Assert.Empty(missing);
    }

    [RealIconsFact]
    public void PottysLightBlinksOverIt()
    {
        var tables = new TestTables();
        var compositor = new AppearanceCompositor(RealIcons.NewLibrary());
        const string plants = "icons/obj/fluff/flora/plants.dmi";
        TableRef light = tables.Add(plants, "potty_light");
        TableRef lit = tables.Add(plants, "potty", e => e.Overlays.Add(light));
        TableRef dark = tables.Add(plants, "potty");
        Assert.True(compositor.TryCompose(tables, lit, ByondDir.South, 0, out ComposedRaster withLight));
        Assert.True(compositor.TryCompose(tables, dark, ByondDir.South, 0, out ComposedRaster without));
        int differ = 0;
        for (int y = 0; y < without.Image.Height; y++)
        {
            for (int x = 0; x < without.Image.Width; x++)
            {
                if (withLight.Image.Get(x, y) != without.Image.Get(x, y))
                    differ++;
            }
        }
        Assert.True(differ > 0, "Potty's light overlay draws nothing over it");
    }

    [RealIconsFact]
    public void AnUprootedBushIsDrawnOnItsSide()
    {
        var tables = new TestTables();
        var compositor = new AppearanceCompositor(RealIcons.NewLibrary());
        const string ausflora = "icons/obj/fluff/flora/ausflora.dmi";
        matrix m = new matrix().Turn(90); // flora.dm:271, the clockwise pick
        TableRef upright = tables.Add(ausflora, "firstbush_1");
        TableRef uprooted = tables.Add(ausflora, "firstbush_1", e => e.Transform = new[] { m.a, m.b, m.c, m.d, m.e, m.f });
        Assert.True(compositor.TryCompose(tables, upright, ByondDir.South, 0, out ComposedRaster standing));
        Assert.True(compositor.TryCompose(tables, uprooted, ByondDir.South, 0, out ComposedRaster lying));
        int w = standing.Image.Width, h = standing.Image.Height;
        Assert.Equal((w, h), (lying.Image.Width, lying.Image.Height));
        int drawn = 0, wrong = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool shown = (standing.Image.Get(x, y) & 0xFF) != 0;
                drawn += shown ? 1 : 0;
                // BYOND's Turn(90) is clockwise: the top left pixel goes to the top right
                if (shown != ((lying.Image.Get(w - 1 - y, x) & 0xFF) != 0))
                    wrong++;
            }
        }
        Assert.True(drawn > 0, "the bush draws nothing");
        Assert.Equal(0, wrong);
    }
}
