using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package P5 (D298): the chemistry machines and reactions in a booted toy room. The dispenser fills a beaker through its
/// window, the ChemMaster moves its beaker into the buffer and prints pills and a tube, the reaction chamber heats a beaker to its
/// target and calomel forms above 374 K, the distillation apparatus boils a reagent over into its beaker, the grinder grinds and
/// juices through its radial, flash powder flashes, potassium and water explode, the EMP mix pulses, the catalysts and buffers act,
/// and every type spawns and goes with no runtimes.
/// </summary>
public class ChemistryMachinesTests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_machinery_chem_dispenser), typeof(obj_machinery_chem_dispenser_drinks), typeof(obj_machinery_chem_dispenser_drinks_fullupgrade), typeof(obj_machinery_chem_dispenser_drinks_beer),
        typeof(obj_machinery_chem_dispenser_drinks_beer_fullupgrade), typeof(obj_machinery_chem_dispenser_mutagen), typeof(obj_machinery_chem_dispenser_mutagensaltpeter), typeof(obj_machinery_chem_dispenser_fullupgrade),
        typeof(obj_machinery_chem_dispenser_abductor), typeof(obj_machinery_chem_dispenser_chem_synthesizer), typeof(obj_machinery_chem_heater), typeof(obj_machinery_chem_heater_debug), typeof(obj_machinery_chem_heater_withbuffer),
        typeof(obj_machinery_chem_master), typeof(obj_machinery_chem_master_condimaster), typeof(obj_machinery_chem_mass_spec), typeof(obj_machinery_reagentgrinder), typeof(obj_structure_chem_separator),
        typeof(obj_item_storage_portable_chem_mixer), typeof(obj_item_burner), typeof(obj_item_burner_oil), typeof(obj_item_burner_fuel), typeof(obj_item_thermometer), typeof(obj_item_thermometer_pen), typeof(obj_item_ph_meter),
        typeof(obj_item_reagent_containers_cup_mortar), typeof(obj_item_reagent_containers_cup_tube), typeof(obj_item_reagent_containers_chem_pack), typeof(obj_item_reagent_containers_condiment),
        typeof(obj_item_reagent_containers_condiment_saltshaker), typeof(obj_item_reagent_containers_condiment_peppermill), typeof(obj_item_reagent_containers_condiment_milk), typeof(obj_item_reagent_containers_condiment_enzyme),
        typeof(obj_item_reagent_containers_condiment_pack_ketchup), typeof(obj_item_reagent_containers_condiment_pack_sugar), typeof(obj_machinery_medipen_refiller),
    };

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        // the toy room has no APC: free power, as use_energy gives shuttles
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static obj_item_reagent_containers_cup_beaker Beaker(AudioRoom r, Func<atom> where, params (Type Reagent, float Amount)[] contents) => r.In(() =>
    {
        var beaker = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(where());
        foreach ((Type reagent, float amount) in contents)
            Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(reagent, amount, no_react: TRUE));
        return (obj_item_reagent_containers_cup_beaker)beaker;
    });

    private static float Amount(AudioRoom r, atom holder, Type reagent) => r.In(() => Dm.num(((datum_reagents)holder.reagents).get_reagent_amount(reagent)));

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance i ? i.icon_state : Dm.str(o)).ToList());

    private static void Hold(AudioRoom r, mob_living_carbon_human bob, obj_item item) => r.In(() => Waiting.NoSleep(bob.put_in_active_hand(item)));

    // the answer to tg's input window of that interface (TextInputModal) the player has open
    private static void Answer(AudioRoom r, mob_living_carbon_human bob, string iface, object entry)
    {
        r.Step(2);
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(bob.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "act/submit", Dm.list(Dm.kv("entry", entry))));
        r.Step(4);
    }

    private static void PickRadial(AudioRoom r, mob_living_carbon_human bob, string name)
    {
        r.Step(2);
        (long id, int index) = r.In(() =>
        {
            datum_radial_menu menu = Dm.each_cast<datum_radial_menu>(bob.client.dm_radial_menus).SingleOrDefault();
            Assert.True(menu != null, "no radial menu");
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            Assert.True(i >= 0, "no slice " + name + " in " + string.Join(", ", menu.dm_elements.Select(e => e.Name)));
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(bob.client, id, index));
        r.Step(2);
    }

    [Fact]
    public void TheDispenserFillsABeakerWithTheReagentChosen()
    {
        var (r, bob) = Room(9501);
        var dispenser = Place<obj_machinery_chem_dispenser>(r, 4, 4); // beside Bob
        var beaker = Beaker(r, () => bob.loc);
        Hold(r, bob, beaker);
        Take(bob);
        Click(r, bob, dispenser);
        Assert.Same(dispenser, r.In(() => beaker.loc));
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("ChemDispenser", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Contains(data.GetProperty("chemicals").EnumerateArray(), chem => chem.GetProperty("id").GetString() == "Mercury");
        Assert.Equal(30, data.GetProperty("amount").GetDouble());
        Assert.Contains("disp_beaker", Overlays(r, dispenser)); // the beaker shows on the dispenser

        Act(r, bob, window, "amount", ("target", 10));
        Act(r, bob, window, "dispense", ("reagent", "Mercury"));
        Act(r, bob, window, "dispense", ("reagent", "Chlorine"));
        Assert.Equal(10, Amount(r, beaker, typeof(datum_reagent_mercury)));
        Assert.Equal(10, Amount(r, beaker, typeof(datum_reagent_chlorine)));
        data = Data(r, bob, out _);
        Assert.Equal(20, data.GetProperty("beaker").GetProperty("currentVolume").GetDouble());
        Assert.True(r.In(() => dispenser.cell.charge < dispenser.cell.maxcharge), "dispensing draws the cell");

        // the right click ejects the beaker into Bob's hand
        r.In(() => Waiting.NoSleep(dispenser.attack_hand_secondary(bob, Dm.list())));
        Assert.Same(bob, r.In(() => beaker.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheChemMasterMakesPillsAndATubeFromItsBuffer()
    {
        var (r, bob) = Room(9502);
        var master = Place<obj_machinery_chem_master>(r, 4, 4);
        var beaker = Beaker(r, () => bob.loc, (typeof(datum_reagent_water), 40));
        Hold(r, bob, beaker);
        Take(bob);
        Click(r, bob, master);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("ChemMaster", InterfaceOf(payload));
        Assert.Contains("chemmaster_overlay_container", Overlays(r, master));

        Act(r, bob, window, "transfer", ("reagentRef", "/datum/reagent/water"), ("amount", 20), ("target", "buffer"));
        Assert.Equal(20, Amount(r, master, typeof(datum_reagent_water)));
        Assert.Equal(20, Amount(r, beaker, typeof(datum_reagent_water)));

        // the static data's pill category: select its first pill and print two of 10 units each
        JsonElement statics = payload.GetProperty("static_data");
        JsonElement pills = statics.GetProperty("categories").EnumerateArray().First(cat => cat.GetProperty("name").GetString() == "pills");
        string pillRef = pills.GetProperty("containers")[0].GetProperty("ref").GetString();
        Act(r, bob, window, "selectContainer", ("ref", pillRef));
        Act(r, bob, window, "create", ("itemCount", 2));
        Answer(r, bob, "TextInputModal", "water pill");
        r.Step(3 * Second);
        var printed = r.In(() => Dm.each<obj_item_reagent_containers_applicator_pill>().ToList());
        Assert.Equal(2, printed.Count);
        foreach (var pill in printed)
        {
            Assert.Equal("water pill", r.In(() => pill.name));
            Assert.Equal(10, Amount(r, pill, typeof(datum_reagent_water)));
        }
        Assert.Equal(0, r.In(() => ((datum_reagents)master.reagents).total_volume));

        // the rest into the buffer and a tube, the default container named after its reagent
        Act(r, bob, window, "transfer", ("reagentRef", "/datum/reagent/water"), ("amount", 1000), ("target", "buffer"));
        string tubeRef = statics.GetProperty("categories").EnumerateArray().First(cat => cat.GetProperty("name").GetString() == "tubes").GetProperty("containers")[0].GetProperty("ref").GetString();
        Act(r, bob, window, "selectContainer", ("ref", tubeRef));
        Act(r, bob, window, "create", ("itemCount", 1));
        Answer(r, bob, "TextInputModal", "Water tube");
        r.Step(2 * Second);
        var tube = r.In(() => Dm.each<obj_item_reagent_containers_cup_tube>().Single());
        Assert.Equal(20, Amount(r, tube, typeof(datum_reagent_water)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHeaterHeatsABeakerToItsTargetAndCalomelForms()
    {
        var (r, bob) = Room(9503);
        var heater = Place<obj_machinery_chem_heater_withbuffer>(r, 4, 4);
        Assert.Equal("mixer0b", r.In(() => heater.icon_state));
        Assert.Equal(20, Amount(r, heater, typeof(datum_reagent_reaction_agent_acidic_buffer)));
        var beaker = Beaker(r, () => bob.loc, (typeof(datum_reagent_mercury), 10), (typeof(datum_reagent_chlorine), 10));
        Hold(r, bob, beaker);
        Take(bob);
        Click(r, bob, heater);
        Assert.Equal("mixer1b", r.In(() => heater.icon_state));
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("ChemHeater", InterfaceOf(payload));
        Assert.Equal(0, Amount(r, beaker, typeof(datum_reagent_medicine_calomel))); // 374 K needed

        Act(r, bob, window, "temperature", ("target", 420));
        Act(r, bob, window, "power");
        r.Step(30 * Second);
        Assert.True(r.In(() => ((datum_reagents)beaker.reagents).chem_temp) > 374, "the chamber heated the beaker");
        Assert.True(Amount(r, beaker, typeof(datum_reagent_medicine_calomel)) > 0, "calomel formed");

        // the acidic buffer lowers the beaker's pH and is used up from the chamber's tank
        float ph = r.In(() => ((datum_reagents)beaker.reagents).ph);
        Act(r, bob, window, "acidBuffer", ("target", 1));
        Assert.True(r.In(() => ((datum_reagents)beaker.reagents).ph) < ph, "the buffer acidified the beaker");
        Assert.Equal(19, Amount(r, heater, typeof(datum_reagent_reaction_agent_acidic_buffer)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMedipenRefillerTakesABeakerAndRefillsASpentMedipen()
    {
        var (r, bob) = Room(9508);
        var refiller = Place<obj_machinery_medipen_refiller>(r, 4, 4);
        var beaker = Beaker(r, () => bob.loc, (typeof(datum_reagent_medicine_epinephrine), 30));
        Hold(r, bob, beaker);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(refiller.item_interaction(bob, beaker, Dm.list()))));
        Assert.Equal(10, Amount(r, refiller, typeof(datum_reagent_medicine_epinephrine))); // the beaker's 10 units a pour
        r.In(() => Waiting.NoSleep(beaker.forceMove(bob.loc)));

        var pen = r.In(() =>
        {
            var p = Dm.@new<obj_item_reagent_containers_hypospray_medipen>(bob.loc);
            Waiting.NoSleep(((datum_reagents)p.reagents).clear_reagents());
            p.used_up = TRUE;
            return p;
        });
        Hold(r, bob, pen);
        r.Command(() => Waiting.Detach(refiller.item_interaction(bob, pen, Dm.list())));
        r.Step(2);
        Assert.Contains("active", Overlays(r, refiller));
        r.Step(3 * Second);
        Assert.False(r.In(() => Dm.truthy(pen.used_up)), "the medipen is fresh again");
        Assert.True(Amount(r, pen, typeof(datum_reagent_medicine_epinephrine)) > 0, "the medipen holds epinephrine");
        Assert.Equal(0, Amount(r, refiller, typeof(datum_reagent_medicine_epinephrine)));
        Assert.DoesNotContain("active", Overlays(r, refiller));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSeparatorBoilsAReagentOverIntoItsBeaker()
    {
        var (r, bob) = Room(9504);
        var separator = Place<obj_structure_chem_separator>(r, 4, 4);
        r.In(() => Waiting.NoSleep(((datum_reagents)separator.reagents).add_reagent(typeof(datum_reagent_water), 20)));
        var output = Beaker(r, () => bob.loc);
        Hold(r, bob, output);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(separator.item_interaction(bob, output, Dm.list()))));
        var fuel = Beaker(r, () => bob.loc, (typeof(datum_reagent_fuel), 50), (typeof(datum_reagent_oxygen), 50));
        Hold(r, bob, fuel);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(separator.item_interaction_secondary(bob, fuel, Dm.list()))));
        r.In(() =>
        {
            separator.burner_knob = 10;
            Waiting.NoSleep(separator.toggle_burner(TRUE));
        });
        Assert.True(r.In(() => Dm.truthy(separator.burner_on)), "the burner lit");
        Assert.Contains("separator_burn", Overlays(r, separator));
        float boiling = r.In(() => Dm.num(separator.get_boiling_point(Dm.each<datum_reagent>(((datum_reagents)separator.reagents).reagent_list).First())));
        Assert.InRange(boiling, 400, 900);
        // the burner heats the flask each processing tick; where the subsystems' phases fall moves the boil-over a few seconds (gaps P4b's
        // stock market subsystem moved it past a minute), so the test looks each second for two minutes
        for (int i = 0; i < 120 && Amount(r, output, typeof(datum_reagent_water)) <= 0; i++)
            r.Step(Second);
        Assert.True(Amount(r, output, typeof(datum_reagent_water)) > 0, "the water boiled over at " + boiling + " K");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGrinderGrindsAndJuicesThroughItsRadial()
    {
        var (r, bob) = Room(9505);
        var grinder = Place<obj_machinery_reagentgrinder>(r, 4, 4);
        var beaker = Beaker(r, () => bob.loc);
        Hold(r, bob, beaker);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(grinder.item_interaction(bob, beaker, Dm.list()))));
        Assert.Contains("juicer-beaker", Overlays(r, grinder));
        var sheet = r.In(() => Dm.@new<obj_item_stack_sheet_mineral_plasma>(bob.loc, 2));
        Hold(r, bob, sheet);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(grinder.item_interaction(bob, sheet, Dm.list()))));

        r.Command(() => Waiting.Detach(grinder.ui_interact(bob)));
        PickRadial(r, bob, "grind");
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(grinder.operating)), "the grinder runs");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/blender.ogg");
        r.Step(7 * Second);
        Assert.True(Amount(r, beaker, typeof(datum_reagent_toxin_plasma)) > 0, "the plasma sheets ground into plasma");

        var tomato = r.In(() => Dm.@new<obj_item_food_grown_tomato>(bob.loc));
        Hold(r, bob, tomato);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(grinder.item_interaction(bob, tomato, Dm.list()))));
        r.Command(() => Waiting.Detach(grinder.ui_interact(bob)));
        PickRadial(r, bob, "juice");
        r.Step(Second);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/juicer.ogg");
        r.Step(7 * Second);
        Assert.True(Amount(r, beaker, typeof(datum_reagent_consumable_tomatojuice)) > 0, "the tomato juiced");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FlashPowderFlashesAndPotassiumAndWaterExplode()
    {
        var (r, bob) = Room(9506);
        var flash = Beaker(r, () => bob.loc, (typeof(datum_reagent_aluminium), 5), (typeof(datum_reagent_potassium), 5)); // the flash's range is 5/3 tiles
        r.In(() => Waiting.NoSleep(((datum_reagents)flash.reagents).add_reagent(typeof(datum_reagent_sulfur), 5)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(bob.IsParalyzed()) || Dm.truthy(bob.IsStun())), "the flash powder flashed Bob");
        Assert.Equal(0, Amount(r, flash, typeof(datum_reagent_flash_powder)));

        var boom = Beaker(r, () => locate(10, 4, 1), (typeof(datum_reagent_water), 30));
        r.In(() => Waiting.NoSleep(((datum_reagents)boom.reagents).add_reagent(typeof(datum_reagent_potassium), 30)));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(boom) || ((datum_reagents)boom.reagents).total_volume == 0), "the potassium explosion cleared its beaker");
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("explosion", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEmpMixPulsesAndTheCatalystsAndBuffersAct()
    {
        var (r, bob) = Room(9507);
        // a bluespace beaker: a large one holds 100 units, too few for a heavy range above 1 (36 units of each at least)
        var emp = r.In(() =>
        {
            var b = Dm.@new<obj_item_reagent_containers_cup_beaker_bluespace>(locate(8, 4, 1));
            Waiting.NoSleep(((datum_reagents)b.reagents).add_reagent(typeof(datum_reagent_uranium), 40, no_react: TRUE));
            Waiting.NoSleep(((datum_reagents)b.reagents).add_reagent(typeof(datum_reagent_iron), 40, no_react: TRUE));
            return b;
        });
        r.In(() => Waiting.NoSleep(((datum_reagents)emp.reagents).add_reagent(typeof(datum_reagent_aluminium), 40))); // 40 reacted: a heavy range of round(26.7 / 12) = 2
        r.Step(2);
        Assert.Equal(0, r.In(() => ((datum_reagents)emp.reagents).total_volume)); // the pulse clears the holder
        Assert.True(r.In(() => Dm.each<obj_effect_temp_visual_emp_pulse>().Any()), "the EMP left its pulse");

        // a speed catalyst in a reacting beaker speeds its equilibrium (consider_catalyst)
        var calomel = Beaker(r, () => bob.loc, (typeof(datum_reagent_mercury), 10), (typeof(datum_reagent_chlorine), 10), (typeof(datum_reagent_catalyst_agent_speed_medicine), 10));
        r.In(() =>
        {
            datum_reagents holder = (datum_reagents)calomel.reagents;
            holder.chem_temp = 450;
            Waiting.NoSleep(holder.handle_reactions());
        });
        r.Step(2);
        Assert.True(r.In(() => Dm.each<datum_equilibrium>(((datum_reagents)calomel.reagents).reaction_list).Any(e => e.speed_mod != 1)) || Amount(r, calomel, typeof(datum_reagent_medicine_calomel)) > 0,
            "the medicine catalyst sped the calomel");

        // the basic buffer poured into an acid raises its pH and is consumed
        var acid = Beaker(r, () => bob.loc, (typeof(datum_reagent_toxin_acid), 20));
        var buffer = Beaker(r, () => bob.loc, (typeof(datum_reagent_reaction_agent_basic_buffer), 10));
        float before = r.In(() => ((datum_reagents)acid.reagents).ph);
        r.In(() => Waiting.NoSleep(((datum_reagents)buffer.reagents).trans_to(acid, 5)));
        Assert.True(r.In(() => ((datum_reagents)acid.reagents).ph) > before, "the basic buffer raised the pH");
        Assert.Equal(0, Amount(r, acid, typeof(datum_reagent_reaction_agent_basic_buffer)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryTypeSpawnsLiveAndGoes()
    {
        var (r, _) = Room(9508);
        foreach (Type type in AllTypes)
        {
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
            atom thing = r.In(() => Dm.@new(type, locate(2, 2, 1)) as atom);
            r.Step(2);
            r.In(() => { if (!QDELETED(thing)) qdel(thing); });
        }
        foreach (Type reaction in new[] { typeof(datum_chemical_reaction_randomized_secret_sauce), typeof(datum_chemical_reaction_randomized_metalgen), typeof(datum_chemical_reaction_randomized_gorgium), typeof(datum_chemical_reaction_reagent_explosion_tatp) })
            Assert.True(r.In(() => GLOB.chemical_reactions_list[reaction] is datum_chemical_reaction), reaction.Name + " is in the reaction list");
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }
}
