using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Xenobiology and cytology on the converted MetaStation: the lab's two slime management consoles boot live, the one beside the
/// monkey recycler linked to it; a console takes a pen's slime into its storage and spits it out again; the extract fridge, the vat,
/// the microscope, the biopsy tool and the boxes of petri dishes are live; a mouse sample grows in the lab's vat; and nothing in
/// xenobiology's or cytology's files throws over the ticks.
/// </summary>
public class XenobiologyMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public XenobiologyMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private void Step(int ticks)
    {
        for (int tick = 0; tick < ticks; tick++)
            _meta.World.Step();
    }

    [MetaFact]
    public void Metas_lab_consoles_vat_and_microscope_work()
    {
        Step(40);
        int runtimesBefore = _meta.Log.Runtimes.Count;
        obj_machinery_vatgrower vat;
        using (_meta.World.Enter())
        {
            var consoles = Dm.each<obj_machinery_computer_camera_advanced_xenobio>().ToList();
            var recycler = Dm.each<obj_machinery_monkey_recycler>().Single();
            _output.WriteLine($"Meta's lab: {consoles.Count} consoles in {string.Join(", ", consoles.Select(c => Dm.str((get_area(c) as area).name)))}, recycler in {Dm.str((get_area(recycler) as area).name)}");
            Assert.Equal(2, consoles.Count);
            Assert.All(consoles, c => Assert.False(InertSeam.IsInert(c)));
            Assert.Contains(consoles, c => ReferenceEquals(c.connected_recycler_ref?.resolve(), recycler));

            vat = Dm.each<obj_machinery_vatgrower>().Single();
            var microscope = Dm.each<obj_structure_microscope>().Single();
            var fridge = Dm.each<obj_machinery_smartfridge_extract_preloaded>().Single();
            Assert.False(InertSeam.IsInert(vat));
            Assert.False(InertSeam.IsInert(microscope));
            Assert.False(InertSeam.IsInert(fridge));
            Assert.False(InertSeam.IsInert(Dm.each<obj_item_biopsy_tool>().Single()));
            var dishes = Dm.each<obj_item_storage_box_petridish>().SelectMany(b => Dm.each<obj_item_petri_dish>(b.contents)).ToList();
            Assert.True(dishes.Count >= 2, "the boxes hold their dishes: " + dishes.Count);
            Assert.All(dishes, d => Assert.False(InertSeam.IsInert(d)));

            // a pen's slime: the console beside the recycler takes it up and spits it out on the same tile
            var console = consoles.First(c => c.connected_recycler_ref?.resolve() != null);
            var slime = Dm.each<mob_living_basic_slime>().FirstOrDefault(s => Dm.eq(get_area(s), get_area(console)) || get_dist(s, console) < 20);
            Assert.NotNull(slime);
            var tile = slime.loc as turf_open;
            var scientist = Dm.@new<mob_living_carbon_human>(get_step(console, SOUTH));
            Waiting.NoSleep(console.slime_pickup(scientist, slime));
            Assert.Same(console, slime.loc);
            Waiting.NoSleep(console.slime_place(tile, scientist));
            Assert.Same(tile, slime.loc);

            // a mouse sample in the lab's vat, with its protein
            var dish = dishes[0];
            var sample = Dm.@new<datum_biological_sample>();
            Waiting.NoSleep(sample.GenerateSample(CELL_LINE_TABLE_MOUSE, null, 1, 0));
            dish.deposit_sample(scientist, sample);
            Assert.Equal(ITEM_INTERACT_SUCCESS, vat.deposit_sample(scientist, dish));
            Waiting.NoSleep(((datum_reagents)vat.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment_protein), 100));
            Assert.True(Dm.truthy(vat.is_operational), "the lab's vat has power");
        }
        // the fixture's world does not fire SSmachines: the vat's process runs by hand, once per two seconds as SSmachines would
        for (int i = 0; i < 30; i++)
        {
            using (_meta.World.Enter())
                vat.process(2f);
            Step(40);
        }
        using (_meta.World.Enter())
        {
            // the resampler is off on Meta's vat: a grown sample is gone, with its mice beside the vat
            var line = vat.biological_sample == null ? null : Dm.each<datum_micro_organism_cell_line>(vat.biological_sample.micro_organisms).Single();
            int mice = Dm.each<mob_living_basic_mouse>(range(3, vat)).Count(m => HAS_TRAIT(m, TRAIT_VATGROWN));
            _output.WriteLine($"the vat's mouse line after 60 s: growth {line?.growth}, {mice} vat-grown mice, soup {((datum_reagents)vat.reagents).total_volume} u");
            Assert.True(mice == 2 || line?.growth > 0, "the sample grew");
        }
        var runtimes = _meta.Log.Runtimes.Skip(runtimesBefore)
            .Where(e => (e.ToString() ?? "").Contains("xenobio", StringComparison.Ordinal) || (e.ToString() ?? "").Contains("vatgrow", StringComparison.Ordinal)
                || (e.ToString() ?? "").Contains("slime_extract", StringComparison.Ordinal) || (e.ToString() ?? "").Contains("swab", StringComparison.Ordinal))
            .ToList();
        foreach (var e in runtimes)
            _output.WriteLine("runtime: " + (e.Message.Length > 300 ? e.Message[..300] : e.Message));
        Assert.Empty(runtimes);
    }
}
