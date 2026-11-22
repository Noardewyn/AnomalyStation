using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E15: plumbing (datums/components/plumbing, modules/plumbing, RPLD.dm) in a booted toy room. A synthesizer and a
/// plumbed water tank supply a duct line that a chemical tank demands from: SSplumbing's requests pull an equal share from each supplier
/// (the synthesizer's chosen reagent and the tank's water); unanchoring the chemical tank drops it from the net and anchoring it again
/// joins it back. The plumbing holder's round-robin transfer and the filter holder's blacklist; the synthesizer's window; the disposer,
/// the acclimator's fill-heat-empty cycle, the reaction chamber's intake and emptying (its reactions wait for chemistry), the pill
/// press's pills, the automatic buffers' modes; the plumbing constructor's designs, a machine built on a floor and taken back.
/// </summary>
public class PlumbingTests
{
    private const int Second = 20;

    // the synthesizer at (5,7) supplies south into the ducts at (5,6) and (5,5); the plumbed water tank at (4,5), turned east, supplies
    // into (5,5); the chemical tank at (6,5) demands from its west side (5,5)
    private sealed record Line(AudioRoom R, mob_living_carbon_human Bob, obj_machinery_plumbing_synthesizer Synth, obj_structure_reagent_dispensers_plumbed Water,
        obj_machinery_plumbing_tank Tank);

    private static Line Plumbed(ulong seed)
    {
        obj_machinery_plumbing_synthesizer synth = null;
        obj_structure_reagent_dispensers_plumbed water = null;
        obj_machinery_plumbing_tank tank = null;
        var r = new AudioRoom(13, 11, seed: seed, mapExtras: _ =>
        {
            synth = Dm.@new<obj_machinery_plumbing_synthesizer>(locate(5, 7, 1));
            Dm.@new<obj_machinery_duct>(locate(5, 6, 1));
            Dm.@new<obj_machinery_duct>(locate(5, 5, 1));
            water = Dm.@new<obj_structure_reagent_dispensers_plumbed>(locate(4, 5, 1));
            water.dir = EAST;
            tank = Dm.@new<obj_machinery_plumbing_tank>(locate(6, 5, 1));
        });
        mob_living_carbon_human bob = r.Join(2, 2, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return new Line(r, bob, synth, water, tank);
    }

    private static datum_reagents Holder(atom a) => (datum_reagents)a.reagents;

    // the plumbing components allow duplicates, so tg's GetComponent refuses them: the first of GetComponents
    private static datum_component_plumbing Plumbing(datum a) => (datum_component_plumbing)((DmList)a.GetComponents(typeof(datum_component_plumbing)))[1];

    private static float Volume(AudioRoom r, atom a, Type reagent) => r.In(() => Dm.num(Holder(a).get_reagent_amount(reagent)));

    private static object Act(AudioRoom r, mob who, datum target, string iface, string action, params (string Key, object Value)[] args) => r.In(() =>
    {
        usr = who;
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        datum_tgui ui = Dm.@new<datum_tgui>(who, target, iface, "test");
        return Waiting.NoSleep(target.ui_act(action, p, ui, null));
    });

    private static DmList Data(AudioRoom r, mob who, datum target) => r.In(() => (DmList)Waiting.NoSleep(target.ui_data(who)));

    [Fact]
    public void ASynthesizerAndAPlumbedTankFillAChemicalTankThroughTheDucts()
    {
        var g = Plumbed(15001);
        g.R.In(() =>
        {
            // the map-loaded machines are anchored and connected: the synthesizer and the water tank supply the net, the chemical tank
            // demands from it (SSplumbing processes the demander)
            var synthPlumbing = Plumbing(g.Synth);
            var waterPlumbing = Plumbing(g.Water);
            var tankPlumbing = Plumbing(g.Tank);
            Assert.Equal((float)SOUTH, synthPlumbing.supply_connects);
            Assert.Equal((float)EAST, waterPlumbing.supply_connects);
            Assert.Equal(((float)WEST, (float)EAST), (tankPlumbing.demand_connects, tankPlumbing.supply_connects));
            var net = (datum_ductnet)tankPlumbing.ducts[Dm.text("[]", WEST)];
            Assert.NotNull(net);
            Assert.Same(net, synthPlumbing.ducts[Dm.text("[]", SOUTH)]);
            Assert.Same(net, waterPlumbing.ducts[Dm.text("[]", EAST)]);
            Assert.Equal(2, net.suppliers.len);
            Assert.Equal(1, net.demanders.len);
            Assert.Contains(tankPlumbing, Dm.each(SSplumbing.processing));
            // the duct beside the tank draws its connection stub toward it
            Assert.Equal("nduct_n_e_w", Dm.each<obj_machinery_duct>(locate(5, 5, 1)).Single().icon_state);
        });

        // the synthesizer makes nothing until a reagent is picked: the tank fills with the water tank's water alone
        g.R.Step(3 * Second);
        Assert.True(Volume(g.R, g.Tank, typeof(datum_reagent_water)) > 0);
        Assert.Equal(0f, g.R.In(() => Holder(g.Synth).total_volume));

        // its window picks silicon: it makes its five units, which the chemical tank takes from it as from the water tank
        Assert.Equal(TRUE, Act(g.R, g.Bob, g.Synth, "ChemSynthesizer", "select", ("reagent", "Silicon")));
        Assert.Equal(typeof(datum_reagent_silicon), g.R.In(() => g.Synth.reagent_id));
        float water = Volume(g.R, g.Tank, typeof(datum_reagent_water));
        g.R.Step(6 * Second);
        float silicon = Volume(g.R, g.Tank, typeof(datum_reagent_silicon));
        Assert.True(silicon > 0, "the synthesizer's silicon reaches the tank");
        Assert.True(Volume(g.R, g.Tank, typeof(datum_reagent_water)) > water, "the water tank keeps supplying");
        Assert.True(g.R.In(() => Holder(g.Synth).total_volume) <= 5);
        Assert.True(g.R.In(() => Holder(g.Tank).total_volume) <= 400);
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void UnanchoringTheChemicalTankDropsItFromTheNetAndAnchoringItJoinsItBack()
    {
        var g = Plumbed(15002);
        obj_machinery_duct duct = g.R.In(() => Dm.each<obj_machinery_duct>(locate(5, 5, 1)).Single());
        g.R.In(() =>
        {
            var tankPlumbing = Plumbing(g.Tank);
            Waiting.NoSleep(g.Tank.set_anchored(FALSE));
            Assert.Equal(0, tankPlumbing.ducts.len);
            Assert.Null(duct.neighbours[g.Tank]);
            Assert.DoesNotContain(tankPlumbing, Dm.each(SSplumbing.processing));
            Assert.Equal("nduct_n_w", duct.icon_state);

            Waiting.NoSleep(g.Tank.set_anchored(TRUE));
            Assert.Same(duct.net, tankPlumbing.ducts[Dm.text("[]", WEST)]);
            Assert.Equal((float)EAST, Dm.num(duct.neighbours[g.Tank]));
            Assert.Contains(tankPlumbing, Dm.each(SSplumbing.processing));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlumbingHolderTransfersRoundRobinAndTheFilterHolderSkipsItsBlacklist()
    {
        var r = new AudioRoom(7, 7, seed: 15003);
        r.In(() =>
        {
            // _plumb_reagents.dm:6: transferring 5 from 15 water, 15 silicon, 15 hydrogen leaves 10, 15, 15
            var from = Dm.@new<datum_reagents_plumbing>(100f, 0f);
            var to = Dm.@new<datum_reagents>(100f, 0f);
            Waiting.NoSleep(from.add_reagent(typeof(datum_reagent_water), 15));
            Waiting.NoSleep(from.add_reagent(typeof(datum_reagent_silicon), 15));
            Waiting.NoSleep(from.add_reagent(typeof(datum_reagent_hydrogen), 15));
            Assert.Equal(5f, Dm.num(Waiting.NoSleep(from.trans_to(to, 5))));
            Assert.Equal((10f, 15f, 15f), (Dm.num(from.get_reagent_amount(typeof(datum_reagent_water))), Dm.num(from.get_reagent_amount(typeof(datum_reagent_silicon))),
                Dm.num(from.get_reagent_amount(typeof(datum_reagent_hydrogen)))));
            // NONE: proportional, like the parent
            Assert.Equal(6f, Dm.num(Waiting.NoSleep(from.trans_to(to, 6, methods: NONE))));
            Assert.Equal(8.5f, Dm.num(from.get_reagent_amount(typeof(datum_reagent_water))), 3);

            // the filter's holder leaves the blacklisted reagents behind
            var filter = Dm.@new<datum_reagents_plumbing_filter>(100f, 0f);
            var out_ = Dm.@new<datum_reagents>(100f, 0f);
            Waiting.NoSleep(filter.add_reagent(typeof(datum_reagent_water), 10));
            Waiting.NoSleep(filter.add_reagent(typeof(datum_reagent_silicon), 10));
            Assert.Equal(10f, Dm.num(Waiting.NoSleep(filter.trans_to(out_, 20, remove_blacklisted: Dm.list(typeof(datum_reagent_water))))));
            Assert.Equal((10f, 0f), (Dm.num(filter.get_reagent_amount(typeof(datum_reagent_water))), Dm.num(filter.get_reagent_amount(typeof(datum_reagent_silicon)))));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSynthesizersWindowListsItsReagentsAndTakesTgsAmounts()
    {
        var g = Plumbed(15004);
        DmList data = Data(g.R, g.Bob, g.Synth);
        Assert.Equal(5f, Dm.num(data["amount"]));
        Assert.Null(data["current_reagent"]);
        var chemicals = Dm.each_cast<DmList>(data["chemicals"]).Select(c => Dm.str(c["id"])).ToList();
        Assert.Equal(25, chemicals.Count);
        Assert.Contains("Silicon", chemicals);
        Assert.Contains("Welding Fuel", chemicals);
        Assert.Equal(new object[] { 0f, 1f, 2f, 3f, 4f, 5f }, g.R.In(() => Dm.each((DmList)((DmList)Waiting.NoSleep(g.Synth.ui_static_data(g.Bob)))["possible_amounts"]).ToArray()));

        Assert.Equal(TRUE, Act(g.R, g.Bob, g.Synth, "ChemSynthesizer", "amount", ("target", "3")));
        Assert.Equal(3f, g.R.In(() => g.Synth.amount));
        Assert.NotEqual(TRUE, Act(g.R, g.Bob, g.Synth, "ChemSynthesizer", "amount", ("target", "7")));
        Assert.Equal(3f, g.R.In(() => g.Synth.amount));
        // a reagent outside its list (blood) is refused
        Assert.NotEqual(TRUE, Act(g.R, g.Bob, g.Synth, "ChemSynthesizer", "select", ("reagent", "Blood")));
        Assert.Null(g.R.In(() => g.Synth.reagent_id));
        Act(g.R, g.Bob, g.Synth, "ChemSynthesizer", "select", ("reagent", "Hydrogen"));
        Assert.Equal("Hydrogen", Dm.str(Data(g.R, g.Bob, g.Synth)["current_reagent"]));
        // the overlay takes the reagent's colour
        g.R.In(() => Assert.Contains(Dm.each(g.Synth.overlays), o => o is IDmAppearance a && a.icon_state == "synthesizer_overlay"
            && Dm.eq(a.color, Dm.initial(typeof(datum_reagent_hydrogen), (datum_reagent p) => p.color))));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheDisposerDestroysItsReagentsWhenSwitchedOn()
    {
        var r = new AudioRoom(9, 9, seed: 15005);
        mob_living_carbon_human bob = r.Join(2, 2, "Bob");
        var d = r.In(() => Dm.@new<obj_machinery_plumbing_disposer>(locate(5, 5, 1), null, null));
        r.In(() =>
        {
            Waiting.NoSleep(d.set_anchored(TRUE));
            d.begin_processing();
            Waiting.NoSleep(Holder(d).add_reagent(typeof(datum_reagent_water), 40));
        });
        r.Step(2 * Second);
        Assert.Equal(40f, r.In(() => Holder(d).total_volume));
        Assert.Equal(TRUE, Act(r, bob, d, "ChemDisposer", "toggle_power"));
        Assert.Equal(TRUE, Act(r, bob, d, "ChemDisposer", "change_volume", ("volume", "10")));
        Assert.Equal("disposal_working", r.In(() => d.icon_state));
        // its next SSmachines fire takes two seconds' worth (following the fire's own tick: the Master's stagger moves it)
        for (int i = 0; i < 3 * Second && r.In(() => Holder(d).total_volume) == 40; i++)
            r.Step();
        float left = r.In(() => Holder(d).total_volume);
        Assert.True(left < 40 && left >= 10, $"{left}u left after a fire at 10u a second");
        Assert.Equal((25f, 10f), (Dm.num(((DmList)r.In(() => Waiting.NoSleep(d.ui_static_data(bob))))["max_volume"]), Dm.num(Data(r, bob, d)["disposal_rate"])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAcclimatorFillsHeatsToItsTargetAndEmpties()
    {
        var r = new AudioRoom(9, 9, seed: 15006);
        mob_living_carbon_human bob = r.Join(2, 2, "Bob");
        var a = r.In(() => Dm.@new<obj_machinery_plumbing_acclimator>(locate(5, 5, 1), null, null));
        r.In(() =>
        {
            // what wrench_act does (_plumb_machinery.dm:70, acclimator.dm:45)
            Waiting.NoSleep(a.set_anchored(TRUE));
            a.begin_processing();
            a.update_appearance(UPDATE_ICON_STATE);
        });
        Assert.Equal(TRUE, Act(r, bob, a, "ChemAcclimator", "set_target_temperature", ("temperature", "350")));
        Assert.Equal(TRUE, Act(r, bob, a, "ChemAcclimator", "change_volume", ("volume", "20")));
        r.In(() =>
        {
            Assert.Equal((350f, 20f), (Dm.num(a.target_temperature), a.max_volume));
            Assert.Equal("acclimator_fill", a.icon_state);
            Waiting.NoSleep(Holder(a).add_reagent(typeof(datum_reagent_water), 20, reagtemp: 300));
            // the component's request sees a full intake and starts heating (chemical_acclimator.dm:10)
            var comp = Plumbing(a);
            Waiting.NoSleep(comp.send_request(WEST));
            Assert.Equal((float)AC_HEATING, a.acclimate_state);
        });
        r.Step(30 * Second);
        r.In(() =>
        {
            Assert.Equal((float)AC_EMPTYING, a.acclimate_state);
            Assert.Equal(350f, Holder(a).chem_temp);
            Assert.Equal("acclimator_empty", a.icon_state);
            // emptied, it fills again
            Waiting.NoSleep(Holder(a).clear_reagents());
            Assert.Equal((float)AC_FILLING, a.acclimate_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheReactionChamberTakesItsRequiredReagentsThenEmpties()
    {
        obj_machinery_plumbing_synthesizer synth = null;
        obj_machinery_plumbing_reaction_chamber chamber = null;
        obj_machinery_plumbing_tank tank = null;
        // the synthesizer supplies south into (5,6); the chamber at (5,5) demands from its north and supplies south into the tank at (5,4)
        var r = new AudioRoom(11, 11, seed: 15007, mapExtras: _ =>
        {
            synth = Dm.@new<obj_machinery_plumbing_synthesizer>(locate(5, 7, 1));
            Dm.@new<obj_machinery_duct>(locate(5, 6, 1));
            chamber = Dm.@new<obj_machinery_plumbing_reaction_chamber>(locate(5, 5, 1));
            tank = Dm.@new<obj_machinery_plumbing_tank>(locate(5, 3, 1));
            tank.dir = WEST; // its west demand turns north (on_parent_dir_change)
            Dm.@new<obj_machinery_duct>(locate(5, 4, 1));
        });
        mob_living_carbon_human bob = r.Join(2, 2, "Bob");
        r.Step(Second);
        r.In(() =>
        {
            synth.reagent_id = typeof(datum_reagent_water);
            chamber.required_reagents[typeof(datum_reagent_water)] = 20f;
        });
        DmList data = Data(r, bob, chamber);
        Assert.Equal("Water", Dm.str(((DmList)((DmList)data["reagents"])[1])["name"]));
        r.Step(8 * Second);
        // it took its 20 units, set emptying and then gave them on to the tank
        r.Step(8 * Second);
        Assert.True(Volume(r, tank, typeof(datum_reagent_water)) >= 20, $"{Volume(r, tank, typeof(datum_reagent_water))}u in the tank");
        Assert.Equal(TRUE, Act(r, bob, chamber, "ChemMixingChamber", "temperature", ("target", "400")));
        Assert.Equal(400f, r.In(() => Dm.num(chamber.target_temperature)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePillPressPacksPillsOfItsChosenVolume()
    {
        var r = new AudioRoom(9, 9, seed: 15008);
        mob_living_carbon_human bob = r.Join(2, 2, "Bob");
        var press = r.In(() => Dm.@new<obj_machinery_plumbing_pill_press>(locate(5, 5, 1), null, null));
        DmList data = Data(r, bob, press);
        Assert.Equal(("pills", 10f), (Dm.str(data["packaging_category"]), Dm.num(data["current_volume"])));
        DmList statics = (DmList)r.In(() => Waiting.NoSleep(press.ui_static_data(bob)));
        var categories = Dm.each_cast<DmList>(statics["packaging_types"]).Select(c => Dm.str(c["cat_name"])).ToList();
        Assert.Equal(new[] { "pills", "patches", "Bottles" }, categories);
        Assert.StartsWith("chemmaster32x32 ", Dm.str(((DmList)((DmList)((DmList)statics["packaging_types"])[1])["products"])[1] is DmList p ? p["class_name"] : null));
        Assert.Equal(TRUE, Act(r, bob, press, "ChemPress", "change_current_volume", ("volume", "15")));
        Assert.Equal(TRUE, Act(r, bob, press, "ChemPress", "change_product_name", ("name", "test")));
        r.In(() =>
        {
            Waiting.NoSleep(press.set_anchored(TRUE));
            press.begin_processing();
            Waiting.NoSleep(Holder(press).add_reagent(typeof(datum_reagent_water), 40));
        });
        r.Step(5 * Second);
        var pills = r.In(() => Dm.each<obj_item_reagent_containers_applicator_pill>(locate(5, 5, 1)).ToList());
        Assert.True(pills.Count >= 2);
        r.In(() =>
        {
            Assert.Equal("test pill", pills[0].name);
            Assert.Equal(15f, Holder(pills[0]).total_volume);
            Assert.Equal(3f, pills[0].layers_remaining);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AutomaticBuffersWaitForEachOtherBeforeSending()
    {
        var r = new AudioRoom(9, 9, seed: 15009);
        mob_living_carbon_human bob = r.Join(2, 2, "Bob");
        var (a, b) = r.In(() => (Dm.@new<obj_machinery_plumbing_buffer>(locate(5, 5, 1), null, null), Dm.@new<obj_machinery_plumbing_buffer>(locate(6, 5, 1), null, null)));
        var driver = r.In(() => Dm.@new<obj_item_screwdriver>(get_turf(bob)));
        r.In(() =>
        {
            Waiting.NoSleep(a.set_anchored(TRUE));
            Waiting.NoSleep(b.set_anchored(TRUE));
            Waiting.NoSleep(a.screwdriver_act(bob, driver));
            Assert.Equal(2, a.connections.len);
            Assert.Same(a.connections, b.connections);
            Assert.Equal(1f, Dm.num(((DmList)Waiting.NoSleep(a.ui_data(bob)))["connections"]));
        });
        Assert.Equal(TRUE, Act(r, bob, a, "ChemAutomaticBuffer", "set_threshold", ("threshold", "50")));
        r.In(() =>
        {
            Waiting.NoSleep(Holder(a).add_reagent(typeof(datum_reagent_water), 60));
            Assert.Equal((float)AB_IDLE, a.mode);
            Assert.Equal((float)AB_UNREADY, b.mode);
            Waiting.NoSleep(Holder(b).add_reagent(typeof(datum_reagent_water), 100));
            Assert.Equal(((float)AB_READY, (float)AB_READY), (a.mode, b.mode));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheConstructorBuildsTheSelectedMachineAndTakesItBack()
    {
        var r = new AudioRoom(11, 9, seed: 15010);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        var rpld = r.In(() =>
        {
            var item = Dm.@new<obj_item_construction_plumbing>(get_turf(bob));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(item))));
            return item;
        });
        r.In(() =>
        {
            Assert.Equal(("Synthesizers", typeof(obj_machinery_plumbing_synthesizer)), (Dm.str(rpld.selected_category), rpld.blueprint));
            DmList statics = (DmList)Waiting.NoSleep(rpld.ui_static_data(bob));
            Assert.Equal(new[] { "Synthesizers", "Distributors", "Storage" }, Dm.each_cast<DmList>(statics["categories"]).Select(c => Dm.str(c["cat_name"])));
            DmList data = (DmList)Waiting.NoSleep(rpld.ui_data(bob));
            Assert.Equal((3f, "omni", "plumbing_layer4", 200f), (Dm.num(data["piping_layer"]), Dm.str(data["selected_color"]), Dm.str(data["layer_icon"]), Dm.num(data["matterLeft"])));
        });
        // the Storage tab's first design: the chemical tank (20 units of matter)
        Assert.Equal(TRUE, Act(r, bob, rpld, "RapidPlumbingDevice", "recipe", ("category", "Storage"), ("id", "0")));
        Assert.Equal(typeof(obj_machinery_plumbing_tank), r.In(() => rpld.blueprint));
        r.Command(() => ClientSeam.Click(bob.client, r.In(() => locate(5, 4, 1)), "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(4 * Second);
        var tank = r.In(() => Dm.each<obj_machinery_plumbing_tank>(locate(5, 4, 1)).Single());
        Assert.Equal((180f, 0f), r.In(() => (rpld.matter, Dm.num(tank.anchored))));
        // a second machine on the same tile is refused: machines never overlap
        Assert.Equal(FALSE, r.In(() => Waiting.NoSleep(rpld.canPlace(locate(5, 4, 1)))));
        // a duct on the third layer and in red
        Act(r, bob, rpld, "RapidPlumbingDevice", "recipe", ("category", "Distributors"), ("id", "0"));
        Act(r, bob, rpld, "RapidPlumbingDevice", "piping_layer", ("piping_layer", "2"));
        Act(r, bob, rpld, "RapidPlumbingDevice", "color", ("paint_color", "red"));
        r.Command(() => ClientSeam.Click(bob.client, r.In(() => locate(5, 5, 1)), "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(2 * Second);
        var duct = r.In(() => Dm.each<obj_machinery_duct>(locate(5, 5, 1)).Single());
        Assert.Equal(((float)SECOND_DUCT_LAYER, COLOR_RED), r.In(() => (duct.duct_layer, duct.duct_color)));
        // the unanchored tank is taken back with its matter
        r.Command(() => ClientSeam.Click(bob.client, tank, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(3 * Second);
        Assert.True(r.In(() => QDELETED(tank)));
        Assert.Equal(199f, r.In(() => rpld.matter));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheExaminesShowTheMachinesContentsTheBuffersStateAndTheConstructorsMatter()
    {
        var r = new AudioRoom(9, 9, seed: 15011);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        List<string> Examine(atom a) => r.In(() => Dm.each(Waiting.NoSleep(a.examine(bob))).Select(Dm.str).ToList());
        var tank = r.In(() => Dm.@new<obj_machinery_plumbing_tank>(locate(5, 4, 1), null, null));
        List<string> lines = Examine(tank);
        Assert.Contains(lines, l => l.Contains("The maximum volume display reads: <b>400u capacity</b>. Contains:"));
        Assert.Contains(lines, l => l.Contains("Nothing."));
        Assert.Contains(lines, l => l.Contains("Needs to be <b>anchored</b> to start operations."));
        r.In(() =>
        {
            Waiting.NoSleep(tank.set_anchored(TRUE));
            Waiting.NoSleep(Holder(tank).add_reagent(typeof(datum_reagent_water), 12.5f));
        });
        lines = Examine(tank);
        Assert.Contains(lines, l => l.Contains("12.5u of Water"));
        Assert.Contains(lines, l => l.Contains("It's <b>anchored</b> in place."));
        var buffer = r.In(() => Dm.@new<obj_machinery_plumbing_buffer>(locate(3, 4, 1), null, null));
        lines = Examine(buffer);
        Assert.Contains(lines, l => l.Contains("It activates at a threshold of 100u of reagents"));
        Assert.Contains(lines, l => l.Contains("It is filling up on reagents."));
        var rpld = r.In(() => Dm.@new<obj_item_construction_plumbing>(locate(4, 4, 1)));
        lines = Examine(rpld);
        Assert.Contains("It currently holds 200/200 matter-units.", lines);
        Assert.Contains("You can scroll your mouse wheel to change the piping layer.", lines);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRcdFamilysExaminesShowTheUpgradesTheDecorationAndThePlastic()
    {
        var r = new AudioRoom(9, 9, seed: 15012);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        List<string> Examine(atom a) => r.In(() => Dm.each(Waiting.NoSleep(a.examine(bob))).Select(Dm.str).ToList());
        var rcd = r.In(() => Dm.@new<obj_item_construction_rcd_loaded_upgraded>(locate(4, 4, 1)));
        List<string> lines = Examine(rcd);
        Assert.Contains(lines, l => l.StartsWith("It currently holds "));
        Assert.Contains("It has the following upgrades installed:", lines);
        Assert.Contains(r.In(() => Dm.str(Dm.initial(typeof(obj_item_rcd_upgrade_frames), (atom a) => a.name))), lines);
        Assert.Contains(r.In(() => Dm.str(Dm.initial(typeof(obj_item_rcd_upgrade_simple_circuits), (atom a) => a.name))), lines);
        var rdd = r.In(() => Dm.@new<obj_item_construction_rdd>(locate(4, 4, 1)));
        string decoration = r.In(() => Dm.str(Dm.initial(rdd.selected_decoration, (atom a) => a.name)));
        Assert.Contains("<span class='info'>Currently set to produce: <span class='bold'>" + decoration + "</span>.</span>", Examine(rdd));
        var grass = r.In(() => Dm.@new<obj_structure_decoration_grass_first>(locate(5, 4, 1)));
        Assert.Contains(Examine(grass), l => l.Contains("It's made of cheap, hollow plastic."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryPlumbingMachineIsMadeAndDeletedCleanly()
    {
        var r = new AudioRoom(9, 9, seed: 15013);
        foreach (Type type in new[] { typeof(obj_machinery_iv_drip_plumbing), typeof(obj_machinery_plumbing_reaction_chamber_chem), typeof(obj_machinery_plumbing_layer_manifold),
            typeof(obj_machinery_plumbing_bottler), typeof(obj_machinery_plumbing_receiver), typeof(obj_structure_reagent_dispensers_plumbed_storage) })
        {
            r.In(() =>
            {
                qdel((datum)Dm.@new(type, locate(5, 5, 1)));
            });
        }
        r.In(() =>
        {
            foreach (object path in Dm.each(SSgarbage.items))
            {
                var item = (datum_qdel_item)SSgarbage.items[path];
                Assert.False(Dm.truthy(item.no_hint), item.name + " failed to return a qdel hint");
            }
        });
        r.AssertNoRuntimes();
    }
}
