using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package SR1 on the converted MetaStation: the storage leftovers mapped there or spawned by its lockers boot live (the test tube
/// racks with their tubes and the sprite that counts them, the shipping supplies box, the gum packet with its four wrappers and their
/// gum, the hitman's and the surgical duffel bags with their contents, the fishing toolboxes, the sabre's
/// sheath with its sabre), with no runtime from the files the package translated.
/// </summary>
public class StorageSR1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public StorageSR1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "storage/belt", "storage/bags", "storage/backpack", "storage/basket", "storage/sixpack", "storage/garment", "storage/medkit",
        "storage/lockbox", "storage/dufflebags", "boxes/", "toolboxes/", "storage/subtypes/",
    };

    // what a turf holds, and what that holds, all the way down (the lockers, the boxes, the bags)
    private static IEnumerable<atom> Walk(IEnumerable<atom> things)
    {
        foreach (atom thing in things)
        {
            yield return thing;
            foreach (atom inner in Walk(Dm.each<atom>(thing.contents)))
                yield return inner;
        }
    }

    [MetaFact]
    public void Metas_storage_leftovers_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, emptyRacks = 0, badRackSprites = 0, emptyBoxes = 0, shortGum = 0, emptySheaths = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in Walk(locate(x, y, 1).dm_held))
                    {
                        string kind = thing switch
                        {
                            obj_item_storage_test_tube_rack => "test tube rack",
                            obj_item_storage_box_shipping => "shipping box",
                            obj_item_storage_box_gum => "gum packet",
                            obj_item_storage_bubblegum_wrapper => "gum wrapper",
                            obj_item_storage_backpack_duffelbag_syndie_hitman => "hitman's duffel bag",
                            obj_item_storage_backpack_duffelbag_sec_surgery => "surgical duffel bag",
                            obj_item_storage_toolbox_fishing => "fishing toolbox",
                            obj_item_storage_box_lights_bulbs or obj_item_storage_box_lights_tubes => "box of bulbs or tubes",
                            obj_item_storage_belt_sheath => "sheath",
                            obj_item_storage_bag_garment => "garment bag",
                            obj_item_storage_box_survival => "survival box",
                            _ => null,
                        };
                        if (kind == null)
                            continue;
                        counts[kind] = counts.GetValueOrDefault(kind) + 1;
                        if (InertSeam.IsInert(thing))
                            inert++;
                        switch (thing)
                        {
                            case obj_item_storage_test_tube_rack_full rack:
                                if (Dm.each<obj_item_reagent_containers_cup_tube>(rack.contents).Count() == 0)
                                    emptyRacks++;
                                break;
                            case obj_item_storage_test_tube_rack rack:
                                float tubes = Dm.length(rack.contents);
                                if (rack.icon_state != (tubes > 0 ? Dm.text("rack[]", tubes) : "rack"))
                                    badRackSprites++;
                                break;
                            case obj_item_storage_box_gum gum when Dm.each<obj_item_storage_bubblegum_wrapper>(gum.contents).Count() != 4:
                                shortGum++;
                                break;
                            case obj_item_storage_box_shipping or obj_item_storage_backpack_duffelbag_syndie_hitman or obj_item_storage_backpack_duffelbag_sec_surgery
                                or obj_item_storage_box_lights_bulbs or obj_item_storage_box_lights_tubes when Dm.length(thing.contents) == 0:
                                emptyBoxes++;
                                break;
                            case obj_item_storage_belt_sheath_sabre sheath when Dm.length(sheath.contents) == 0 || sheath.icon_state != "sheath-full":
                                emptySheaths++;
                                break;
                        }
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert, {emptyRacks} empty full racks, {badRackSprites} racks with a wrong sprite, {shortGum} short gum packets,"
                + $" {emptyBoxes} empty boxes and bags, {emptySheaths} sabre sheaths without their sabre; SR1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            // the heads' garment bags come with their lockers' contents, which tg fills on the first opening (none is open at round start)
            foreach (string one in new[] { "test tube rack", "shipping box", "gum packet", "hitman's duffel bag", "surgical duffel bag", "fishing toolbox", "sheath" })
                Assert.True(counts.GetValueOrDefault(one) >= 1, "no " + one);
            Assert.Equal(4 * counts.GetValueOrDefault("gum packet"), counts.GetValueOrDefault("gum wrapper"));
            Assert.Equal(0, inert);
            Assert.Equal(0, emptyRacks);
            Assert.Equal(0, badRackSprites);
            Assert.Equal(0, shortGum);
            Assert.Equal(0, emptyBoxes);
            Assert.Equal(0, emptySheaths);
            Assert.Empty(runtimes);
        }
    }
}
