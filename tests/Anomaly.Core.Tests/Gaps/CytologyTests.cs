using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Cytology in a booted toy room (research/xenobiology/vatgrowing/): a swab takes a sample from old gibs and leaves it in a petri
/// dish, a biopsy tool takes one from a mouse, the microscope shows the dish's cell lines in its window and gives the dish back, and
/// a mouse cell line put in the growing vat and fed its reagents grows into two vat-grown mice. The vat's overlays show the resampler,
/// the soup and the sample. A sweep makes every cell line's sample and grows each once.
/// </summary>
public class CytologyTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(15, 11, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() =>
        {
            T made = Dm.@new<T>(who.loc);
            Waiting.NoSleep(who.put_in_active_hand(made));
            return made;
        });
        r.Step(1);
        return item;
    }

    private static obj_item_petri_dish DishOf(AudioRoom r, mob_living_carbon_human who, object table)
    {
        var dish = Hold<obj_item_petri_dish>(r, who);
        r.In(() =>
        {
            var sample = Dm.@new<datum_biological_sample>();
            Waiting.NoSleep(sample.GenerateSample(table, null, 1, 0));
            dish.deposit_sample(who, sample);
        });
        return dish;
    }

    private static datum_component_swabbing Swabbing(obj_item tool) => tool.GetComponent(typeof(datum_component_swabbing)) as datum_component_swabbing;

    [Fact]
    public void ASwabTakesASampleFromOldGibsAndLeavesItInAPetriDish()
    {
        var (r, feed, bob) = Room(13840);
        var gibs = r.In(() => Dm.@new<obj_effect_decal_cleanable_blood_gibs_old>(locate(5, 4, 1)));
        r.Step(1);
        var swab = Hold<obj_item_swab>(r, bob);
        Click(r, bob, gibs);
        r.Step(4 * Second); // tg's swab takes three seconds (swabbing.dm:114)
        r.In(() =>
        {
            var swabbed = Swabbing(swab).swabbed_items;
            Assert.Equal(1, Dm.length(swabbed));
            var sample = Dm.each<datum_biological_sample>(swabbed).Single();
            Assert.True(Dm.length(sample.micro_organisms) >= 2, "old gibs give two to four cell lines");
            // the swab shows its sample's colour (swab.dm:18)
            Assert.Contains(Dm.each<object>(swab.overlays), o => o is IDmAppearance a && a.icon_state == "swab_" + sample.sample_color);
        });
        Assert.True(feed.Any("You manage to collect a microbiological sample from"));

        var dish = r.In(() => Dm.@new<obj_item_petri_dish>(locate(5, 4, 1)));
        r.Step(1);
        Click(r, bob, dish);
        r.Step(Second);
        r.In(() =>
        {
            Assert.NotNull(dish.sample);
            Assert.Equal(0, Dm.length(Swabbing(swab).swabbed_items));
            Assert.Contains(Dm.each<object>(dish.overlays), o => o is IDmAppearance a && a.icon_state == "petri_dish_overlay");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBiopsyToolTakesASampleFromAMouse()
    {
        var (r, feed, bob) = Room(13841);
        var mouse = r.In(() => Dm.@new<mob_living_basic_mouse>(locate(5, 4, 1)));
        r.In(() => ADD_TRAIT(mouse, TRAIT_IMMOBILIZED, "test"));
        r.Step(Second);
        var tool = Hold<obj_item_biopsy_tool>(r, bob);
        Click(r, bob, mouse);
        r.Step(4 * Second);
        r.In(() =>
        {
            var sample = Dm.each<datum_biological_sample>(Swabbing(tool).swabbed_items).Single();
            Assert.Contains(Dm.each<object>(sample.micro_organisms), m => m is datum_micro_organism_cell_line_mouse);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMicroscopeShowsTheDishsCellLinesAndGivesTheDishBack()
    {
        var (r, feed, bob) = Room(13842);
        var microscope = r.In(() => Dm.@new<obj_structure_microscope>(locate(5, 4, 1)));
        r.Step(1);
        var dish = DishOf(r, bob, CELL_LINE_TABLE_MOUSE);
        r.In(() =>
        {
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(microscope.item_interaction(bob, dish, Dm.list())));
            Assert.Same(dish, microscope.current_dish);
            Assert.Same(microscope, dish.loc);
        });
        Take(bob);
        Click(r, bob, microscope);
        JsonElement payload = LastFullUpdate(Take(bob), out string windowId);
        Assert.Equal("Microscope", InterfaceOf(payload));
        // tg sends it all as static data (microscope.dm:36)
        Assert.True(payload.GetProperty("static_data").GetProperty("has_dish").GetInt32() == 1);
        JsonElement line = payload.GetProperty("static_data").GetProperty("cell_lines")[0];
        Assert.True(line.GetProperty("requireds").ValueKind == JsonValueKind.Object, line.GetRawText());
        Assert.Equal("cell line", line.GetProperty("type").GetString());
        Assert.Equal("Murine cells", line.GetProperty("desc").GetString());
        Assert.True(line.GetProperty("requireds").TryGetProperty("Protein", out _), line.GetRawText());
        Assert.True(line.GetProperty("supplementaries").TryGetProperty("Growth Serum", out _), line.GetRawText());
        Assert.Equal("icons/mob/simple/animal.dmi", line.GetProperty("icon").GetString());

        Act(r, bob, windowId, "eject_petridish");
        r.In(() =>
        {
            Assert.Null(microscope.current_dish);
            Assert.True(Dm.truthy(bob.is_holding(dish)) || Dm.eq(dish.loc, microscope.loc), "the dish is out");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMouseCellLineGrowsInTheVatIntoTwoVatGrownMice()
    {
        var (r, feed, bob) = Room(13843);
        var vat = r.In(() => Dm.@new<obj_machinery_vatgrower>(locate(8, 6, 1)));
        r.Step(Second);
        var dish = DishOf(r, bob, CELL_LINE_TABLE_MOUSE);
        r.In(() =>
        {
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(vat.item_interaction(bob, dish, Dm.list())));
            Assert.NotNull(vat.biological_sample);
            // the dish keeps its own sample: the vat clones it (vatgrower.dm:98)
            Assert.NotNull(dish.sample);
            var soup = (datum_reagents)vat.reagents;
            Waiting.NoSleep(soup.add_reagent(typeof(datum_reagent_consumable_nutriment_protein), 100));
            Waiting.NoSleep(soup.add_reagent(typeof(datum_reagent_consumable_nutriment), 50));
            vat.update_appearance();
        });
        r.Step(2);
        r.In(() =>
        {
            var states = Dm.each<object>(vat.overlays).OfType<IDmAppearance>().Select(a => a.icon_state).ToList();
            Assert.Contains("growing_vat_reagent", states);
            Assert.Contains("growing_vat_bubbles", states);
            Assert.Contains("growing_vat_off", states); // the resampler starts off (vatgrower.dm:17)
        });
        List<mob_living_basic_mouse> grown = null;
        for (int i = 0; i < 120 && (grown == null || grown.Count < 2); i++)
        {
            r.Step(Second);
            grown = r.In(() => Dm.each<mob_living_basic_mouse>(range(3, vat)).Where(m => HAS_TRAIT(m, TRAIT_VATGROWN)).ToList());
        }
        Assert.Equal(2, grown.Count);
        Assert.True(feed.Any("pops out of"));
        Assert.True(r.Sounds.Heard.Any(h => h.File == "sound/effects/splat.ogg"), "the vat's splat");
        // the resampler is off: the grown sample is gone (vatgrower.dm:166)
        r.In(() => Assert.Null(vat.biological_sample));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithTheResamplerOnTheGrownSampleStartsOver()
    {
        var (r, _, bob) = Room(13844);
        var vat = r.In(() => Dm.@new<obj_machinery_vatgrower>(locate(5, 4, 1)));
        r.Step(Second);
        Click(r, bob, vat); // the resampler toggles on
        r.In(() => Assert.True(Dm.truthy(vat.resampler_active)));
        var dish = DishOf(r, bob, CELL_LINE_TABLE_MOUSE);
        r.In(() =>
        {
            Waiting.NoSleep(vat.item_interaction(bob, dish, Dm.list()));
            var line = Dm.each<datum_micro_organism_cell_line>(vat.biological_sample.micro_organisms).Single();
            line.growth = 99;
            Waiting.NoSleep(((datum_reagents)vat.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment_protein), 10));
        });
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.NotNull(vat.biological_sample);
            var line = Dm.each<datum_micro_organism_cell_line>(vat.biological_sample.micro_organisms).Single();
            Assert.True(line.growth < 100, "the sample starts over: " + line.growth);
            Assert.Equal(1f, line.grow_count);
        });
        Assert.True(r.Sounds.Heard.Any(h => h.File == "sound/effects/servostep.ogg"), "the resampler's servo");
        r.AssertNoRuntimes();
    }

    /// <summary>
    /// Every cell line of tg's tables: a sample of it alone, put in a vat at full growth with its required reagents, grows once,
    /// and nothing throws. A cell line whose creature is still inert (a later package's) grows the inert creature, as tg would make it.
    /// </summary>
    [Fact]
    public void EveryCellLineGrowsItsCreatureWithoutAnError()
    {
        var (r, _, bob) = Room(13845);
        // the abstract bases (the organs', the clowns') grow nothing: tg never makes them
        List<Type> lines = r.In(() => Dm.each<Type>(subtypesof(typeof(datum_micro_organism_cell_line)))
            .Where(t => !InertSeam.IsInertType(t) && Dm.initial(t, "resulting_atom") != null && Dm.initial(t, "desc") != null && t != typeof(datum_micro_organism_cell_line_clown))
            .ToList());
        Assert.True(lines.Count > 40, "cell lines: " + lines.Count);
        var failures = new List<string>();
        int done = 0;
        foreach (Type lineType in lines)
        {
            if (done++ % 10 == 9)
                (r, _, bob) = Room(13845);
            obj_machinery_vatgrower vat = null;
            try
            {
                vat = r.In(() =>
                {
                    var v = Dm.@new<obj_machinery_vatgrower>(locate(8, 6, 1));
                    var sample = Dm.@new<datum_biological_sample>();
                    var line = Waiting.NoSleep(Dm.new_async(lineType)) as datum_micro_organism_cell_line;
                    Dm.append(ref sample.micro_organisms, line);
                    var dish = Dm.@new<obj_item_petri_dish>(locate(8, 5, 1));
                    dish.deposit_sample(bob, sample);
                    v.deposit_sample(bob, dish);
                    var grown = Dm.each<datum_micro_organism_cell_line>(v.biological_sample.micro_organisms).Single();
                    grown.growth = 99.9f;
                    foreach (object chem in Dm.each(grown.required_reagents))
                        Waiting.NoSleep(((datum_reagents)v.reagents).add_reagent(chem, 20));
                    return v;
                });
                r.Step(5 * Second);
            }
            catch (Exception e)
            {
                failures.Add(lineType.Name + ": " + e.Message);
            }
            if (r.Room.Runtimes.Count > 0)
            {
                failures.Add(lineType.Name + ": " + string.Join(" | ", r.Room.RuntimeText));
                r.Room.Runtimes.Clear();
            }
            r.In(() =>
            {
                foreach (atom_movable thing in Dm.each<atom_movable>(range(6, locate(8, 6, 1))).ToList())
                    if (thing != bob && (thing is mob_living || thing is obj_item || thing is obj_machinery_vatgrower))
                        qdel(thing);
            });
            r.Step(2);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void TheCytologyTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_machinery_vatgrower), typeof(obj_machinery_vatgrower_small), typeof(obj_structure_microscope), typeof(obj_item_petri_dish),
            typeof(obj_item_petri_dish_random), typeof(obj_item_swab), typeof(obj_item_biopsy_tool), typeof(datum_biological_sample),
            typeof(datum_micro_organism_cell_line_mouse), typeof(datum_micro_organism_virus), typeof(datum_component_swabbing),
            typeof(datum_element_swabable), typeof(mob_living_basic_vatbeast), typeof(datum_action_cooldown_tentacle_slap),
            typeof(datum_component_riding_creature_vatbeast), typeof(mob_living_basic_clown_longface), typeof(mob_living_basic_clown_banana),
            typeof(mob_living_basic_clown_mutant_glutton), typeof(datum_action_cooldown_regurgitate), typeof(datum_element_damage_threshold),
            typeof(obj_item_petri_dish_oldstation),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
    }
}
