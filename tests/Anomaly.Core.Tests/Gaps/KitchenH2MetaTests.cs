using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package H2 (D333) on the converted MetaStation: its kitchen machines (the microwaves, the oven and range with its tray and soup
/// pot, the griddle, the deep fryer, the food processor, the gibber, the ice cream vat, the food cart, the coffee maker, the smart
/// fridges and drying racks), the meat spike and the donut boxes boot live, with no runtime from their files.
/// </summary>
public class KitchenH2MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public KitchenH2MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    // "plate.cs", not "plate": a map template's load (from_template) is on the stack of every runtime in a ruin
    private static readonly string[] Files =
    {
        "microwave", "oven", "stove", "griddle", "grill", "deep_fryer", "processor", "gibber", "icecream_vat", "food_cart",
        "coffeemaker", "smartfridge", "kitchen_spike", "bakeable", "grillable", "processable", "microwavable", "dryable", "plate.cs",
        "ice_cream_holder", "food_storage", "meat.dm", "donuts", "fancy", "quality_food_ingredient",
    };

    [MetaFact]
    public void Metas_kitchen_machines_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            var inert = new List<string>();
            int ovensWithoutTray = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        string kind = thing switch
                        {
                            obj_machinery_microwave => "microwave",
                            obj_machinery_oven => "oven",
                            obj_machinery_griddle => "griddle",
                            obj_machinery_deepfryer => "deep fryer",
                            obj_machinery_processor => "processor",
                            obj_machinery_gibber => "gibber",
                            obj_machinery_icecream_vat => "ice cream vat",
                            obj_machinery_food_cart => "food cart",
                            obj_machinery_coffeemaker => "coffee maker",
                            obj_machinery_smartfridge => "smart fridge",
                            obj_structure_kitchenspike => "meat spike",
                            obj_item_storage_fancy_donut_box => "donut box",
                            _ => null,
                        };
                        if (kind == null)
                            continue;
                        counts[kind] = counts.GetValueOrDefault(kind) + 1;
                        if (InertSeam.IsInert(thing))
                            inert.Add(thing.GetType().Name);
                        if (thing is obj_machinery_oven oven && oven.used_tray == null)
                            ovensWithoutTray++;
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key)) + $"; inert: {string.Join(", ", inert)}; ovens without a tray {ovensWithoutTray}; kitchen runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("microwave") >= 2, "too few microwaves");
            Assert.True(counts.GetValueOrDefault("oven") >= 1, "no oven");
            Assert.True(counts.GetValueOrDefault("smart fridge") >= 3, "too few smart fridges");
            Assert.True(counts.GetValueOrDefault("gibber") >= 1, "no gibber");
            Assert.True(counts.GetValueOrDefault("donut box") >= 1, "no donut box");
            Assert.Empty(inert);
            Assert.Equal(0, ovensWithoutTray);
            Assert.Empty(runtimes);
        }
    }
}
