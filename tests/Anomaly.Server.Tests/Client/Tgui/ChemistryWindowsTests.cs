using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package P5: the client's chemistry windows without the engine: ChemDispenser.tsx, ChemMaster.tsx, ChemHeater.tsx,
/// ChemSeparator.tsx, MassSpec.tsx, PortableChemMixer.tsx, ChemDebugSynthesizer.tsx, Thermometer.jsx and Reagents read tg's ui_data
/// the way the TSX does and send tg's acts.
/// </summary>
public class ChemistryWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts, TguiLocalState Local) Render(TguiInterface view, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        local ??= new TguiLocalState();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, view(context), acts, local);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Beaker = "{\"maxVolume\":100,\"transferAmounts\":[5,10,30],\"pH\":7,\"currentVolume\":20,\"contents\":[{\"name\":\"Mercury\",\"volume\":10},{\"name\":\"Chlorine\",\"volume\":10}]}";

    [Fact]
    public void The_dispenser_lists_its_chemicals_and_dispenses_the_one_clicked()
    {
        string json = "{\"amount\":10,\"energy\":500,\"maxEnergy\":1000,\"displayedUnits\":5,\"displayedMaxUnits\":10,\"showpH\":1,\"hasBeakerInHand\":0,"
            + "\"chemicals\":[{\"title\":\"Mercury\",\"id\":\"Mercury\",\"pH\":7,\"color\":\"#484848\",\"pHCol\":\"green\"},{\"title\":\"Chlorine\",\"id\":\"Chlorine\",\"pH\":7.4,\"color\":\"#808080\",\"pHCol\":\"green\"}],"
            + "\"recipes\":{},\"recordingRecipe\":null,\"recipeReagents\":[],\"beaker\":" + Beaker + ",\"reaction_list\":{\"Calomel\":{\"id\":\"/datum/chemical_reaction/medicine/calomel\",\"bitflags\":8,\"lower_temperature\":374,\"upper_temperature\":500,\"lower_ph\":5,\"upper_ph\":9,"
            + "\"required_reagents\":[{\"name\":\"Mercury\",\"amount\":1,\"typepath\":\"/datum/reagent/mercury\"},{\"name\":\"Chlorine\",\"amount\":1,\"typepath\":\"/datum/reagent/chlorine\"}],\"required_catalysts\":[],\"description\":\"d\",\"color\":\"#19C832\"}},\"all_bitflags\":{\"HEALING\":16}}";
        var (context, tree, acts, local) = Render(ChemDispenser.Render, "ChemDispenser", json);
        Assert.Equal((565.0, 620.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("5 / 10 units", ByKey(tree, "energy").Text);
        Assert.Equal("Mercury", ByKey(tree, "dispense:Mercury:").Text);
        Assert.True(ByKey(tree, "amount:10").Selected);
        Assert.Contains(tree.Walk(), n => n.Text == "10 units of Mercury");
        ByKey(tree, "dispense:Mercury:").OnClick();
        ByKey(tree, "amount:30").OnClick();
        ByKey(tree, "remove:5").OnClick();
        Assert.Equal(new[] { ("dispense", "{\"reagent\":\"Mercury\"}"), ("amount", "{\"target\":30}"), ("remove", "{\"amount\":5}") }, acts);

        // the recipes panel: calomel's card, its base reagents dispensing and its temperature
        ByKey(tree, "showRecipes").OnClick();
        Assert.True(local.Get("showreactions", false));
        tree = ChemDispenser.Render(context);
        Assert.Equal(810.0, context.WindowWidth);
        Assert.Contains(tree.Walk(), n => n.Text == "Calomel");
        Assert.Contains(tree.Walk(), n => n.Text == "Heat between 374°K-500°K");
        Assert.Contains(tree.Walk(), n => n.Text == "1 part Mercury");
    }

    [Fact]
    public void The_chemmaster_moves_a_reagent_selects_a_container_and_prints()
    {
        string reagent = "{\"ref\":\"/datum/reagent/water\",\"name\":\"Water\",\"volume\":20,\"pH\":7,\"color\":\"#609bdf\",\"description\":\"wet\",\"purity\":1,\"metaRate\":0.2,\"overdose\":0,\"addictionTypes\":[]}";
        string json = "{\"isPrinting\":0,\"printingProgress\":null,\"printingTotal\":null,\"selectedPillDuration\":3,\"hasBeakerInHand\":0,\"customTransferAmount\":15,"
            + "\"beaker\":{\"maxVolume\":100,\"currentVolume\":20,\"contents\":[" + reagent + "]},\"buffer\":{\"maxVolume\":100,\"currentVolume\":20,\"contents\":[" + reagent + "]},"
            + "\"isTransfering\":1,\"suggestedContainerRef\":\"[0x2]\",\"selectedContainerRef\":\"[0x1]\",\"selectedContainerVolume\":30,\"selectedContainerCategory\":\"tubes\",\"maxPrintable\":50,\"maxPillDuration\":30,"
            + "\"categories\":[{\"name\":\"tubes\",\"containers\":[{\"ref\":\"[0x1]\",\"name\":\"tube\",\"icon\":\"icons/obj/medical/chemical_tanks.dmi\",\"icon_state\":\"test_tube\",\"volume\":30}]},"
            + "{\"name\":\"pills\",\"containers\":[{\"ref\":\"[0x2]\",\"name\":\"pill\",\"icon\":\"icons/obj/medical/chemical.dmi\",\"icon_state\":\"pill1\",\"volume\":50}]}]}";
        var (context, tree, acts, local) = Render(ChemMaster.Render, "ChemMaster", json);
        Assert.Equal((450.0, 620.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Text == "Water 20u");
        Assert.Equal("Moving reagents", ByKey(tree, "mode").Text);
        UiNode pill = ByKey(tree, "container:[0x2]");
        Assert.Equal(("icons/obj/medical/chemical.dmi", "pill1"), (pill.DmIcon, pill.DmIconState));
        Assert.True(ByKey(tree, "container:[0x1]").Selected);
        Assert.Contains(tree.Walk(), n => n.Text == " 20 u. each ");
        tree.Walk().First(n => n.Key == "10").OnClick();
        pill.OnClick();
        ByKey(tree, "print").OnClick();
        Assert.Equal(new[] { ("transfer", "{\"reagentRef\":\"/datum/reagent/water\",\"amount\":10,\"target\":\"buffer\"}"), ("selectContainer", "{\"ref\":\"[0x2]\"}"), ("create", "{\"itemCount\":1}") }, acts);

        // the analysis page of a reagent and back
        ByKey(tree, "analyze").OnClick();
        tree = ChemMaster.Render(context);
        Assert.Contains(tree.Walk(), n => n.Title == "Analysis Results");
        Assert.Contains(tree.Walk(), n => n.Text == "Good");
    }

    [Fact]
    public void The_heater_shows_its_buffers_and_reactions_and_sends_its_acts()
    {
        string json = "{\"targetTemp\":420,\"isActive\":1,\"upgradeLevel\":1,\"hasBeakerInHand\":0,\"beaker\":" + Beaker + ",\"currentTemp\":380,"
            + "\"activeReactions\":[{\"name\":\"Calomel\",\"danger\":0,\"overheat\":0,\"purityAlert\":2,\"quality\":1,\"inverse\":0,\"minPure\":0.15,\"reactedVol\":2,\"targetVol\":20}],"
            + "\"isFlashing\":-1,\"acidicBufferVol\":20,\"basicBufferVol\":0,\"dispenseVolume\":1}";
        var (context, tree, acts, _) = Render(ChemHeater.Render, "ChemHeater", json);
        Assert.Equal((350.0, 350.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("On", ByKey(tree, "power").Text);
        Assert.Equal("380 K", ByKey(tree, "reading").Text);
        Assert.Contains(tree.Walk(), n => n.Text == "Calomel");
        Assert.True(ByKey(tree, "injectBase").Disabled);
        ByKey(tree, "power").OnClick();
        ByKey(tree, "injectAcid").OnClick();
        ByKey(tree, "targetTemp").OnChange(450);
        Assert.Equal(new[] { ("power", (string)null), ("acidBuffer", "{\"target\":1}"), ("temperature", "{\"target\":450}") }, acts);
    }

    [Fact]
    public void The_separator_mass_spectrometer_mixer_synthesizer_and_thermometer_draw_and_act()
    {
        var (sep, sepTree, sepActs, _) = Render(ChemSeparator.Render, "ChemSeparator", "{\"flask\":{\"total_volume\":20,\"maximum_volume\":100,\"temp\":450.2,\"color\":\"#609bdf\"},\"beaker\":{\"total_volume\":0,\"maximum_volume\":120,\"temp\":300,\"color\":\"#000000\"},\"fuel\":null,\"burner_on\":1,\"knob\":5,\"condenser_on\":0,\"condenser_installed\":0,\"max_burner_knob_settings\":10}");
        Assert.Equal((370.0, 215.0), (sep.WindowWidth, sep.WindowHeight));
        Assert.Equal("20 of 100 units at 451K", ByKey(sepTree, "flaskBar").Text);
        ByKey(sepTree, "drain").OnClick();
        ByKey(sepTree, "knobValue").OnChange(7);
        Assert.Equal(new[] { ("drain", (string)null), ("knob", "{\"amount\":7}") }, sepActs);

        var (spec, specTree, specActs, _) = Render(MassSpec.Render, "MassSpec", "{\"lowerRange\":100,\"upperRange\":400,\"processing\":0,\"eta\":12,\"peakHeight\":20,\"hasBeakerInHand\":0,\"graphUpperRange\":400,"
            + "\"beaker1\":{\"currentVolume\":20,\"maxVolume\":100,\"contents\":[{\"name\":\"Water\",\"volume\":20,\"mass\":250,\"purity\":80,\"type\":\"Clean\",\"log\":\"Ready\"}]},\"beaker2\":{\"currentVolume\":0,\"maxVolume\":120,\"contents\":[]}}");
        Assert.Equal((1050.0, 650.0), (spec.WindowWidth, spec.WindowHeight));
        Assert.Contains(specTree.Walk(), n => n.Key == "peak:Water");
        Assert.Contains(specTree.Walk(), n => n.Text == "Eta of selection: 12 seconds");
        ByKey(specTree, "start").OnClick();
        ByKey(specTree, "left").OnChange(150);
        Assert.Equal(new[] { ("activate", (string)null), ("leftSlider", "{\"value\":150}") }, specActs);

        var (mix, mixTree, mixActs, _) = Render(PortableChemMixer.Render, "PortableChemMixer", "{\"amount\":10,\"chemicals\":[{\"title\":\"Water\",\"id\":\"Water\",\"volume\":50,\"pH\":7}],\"beaker\":" + Beaker + "}");
        Assert.Equal((500.0, 500.0), (mix.WindowWidth, mix.WindowHeight));
        Assert.Equal("(50) Water", ByKey(mixTree, "chem:Water").Text);
        ByKey(mixTree, "chem:Water").OnClick();
        Assert.Equal(("dispense", "{\"reagent\":\"Water\"}"), mixActs[0]);

        var (_, synthTree, synthActs, _) = Render(ChemDebugSynthesizer.Render, "ChemDebugSynthesizer", "{\"amount\":10,\"temp\":300,\"purity\":100,\"beaker\":null}");
        ByKey(synthTree, "makecup").OnClick();
        Assert.Equal(("makecup", (string)null), synthActs[0]);

        var (therm, thermTree, _, _) = Render(Thermometer.Render, "Thermometer", "{\"Temperature\":450}");
        Assert.Equal((70.0, 430.0), (therm.WindowWidth, therm.WindowHeight));
        Assert.Equal("450K", ByKey(thermTree, "bulb").Text);
    }

    [Fact]
    public void The_reaction_search_shows_a_recipe_a_reagent_and_the_library()
    {
        string json = "{\"selectedBitflags\":0,\"currentReagents\":[\"/datum/reagent/mercury\"],\"beakerSync\":0,\"linkedBeaker\":\"beaker\","
            + "\"reagent_mode_reagent\":{\"name\":\"Mercury\",\"id\":\"/datum/reagent/mercury\",\"desc\":\"silver\",\"reagentCol\":\"#484848\",\"pH\":7,\"pHCol\":\"green\",\"metaRate\":0.2,\"OD\":0,\"addictions\":[]},"
            + "\"reagent_mode_recipe\":{\"name\":\"Calomel\",\"id\":\"/datum/chemical_reaction/medicine/calomel\",\"hasProduct\":1,\"reagentCol\":\"#19C832\",\"thermodynamics\":[[374,0],[500,1],[900,1],[900,0]],\"explosive\":[[900,0],[900,1],[950,1],[950,0]],"
            + "\"lowerpH\":5,\"upperpH\":9,\"thermics\":\"Weakly endothermic\",\"thermoUpper\":30,\"minPurity\":0.15,\"inversePurity\":0,\"tempMin\":374,\"explodeTemp\":900,\"reqContainer\":null,\"subReactLen\":1,\"subReactIndex\":1,"
            + "\"products\":[{\"name\":\"Calomel\",\"id\":\"/datum/reagent/medicine/calomel\",\"ratio\":2,\"hasReagentCol\":\"default\"}],\"reactants\":[{\"name\":\"Mercury\",\"id\":\"/datum/reagent/mercury\",\"ratio\":1,\"color\":\"green\",\"tooltipBool\":0,\"tooltip\":null}],\"catalysts\":null,\"isColdRecipe\":0},"
            + "\"master_reaction_list\":[{\"name\":\"Calomel\",\"id\":\"/datum/chemical_reaction/medicine/calomel\",\"bitflags\":24,\"reactants\":[{\"name\":\"Mercury\",\"id\":\"/datum/reagent/mercury\"}]}],\"bitflags\":{\"HEALING\":16,\"EASY\":8}}";
        var (context, tree, acts, _) = Render(Reagents.Render, "Reagents", json);
        Assert.Equal((720.0, 850.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Text == "2u Calomel");
        Assert.Contains(tree.Walk(), n => n.Text == " Mercury");
        Assert.Contains(tree.Walk(), n => n.Key == "row:/datum/chemical_reaction/medicine/calomel");
        ByKey(tree, "click").OnClick();
        ByKey(tree, "HEALING").OnClick();
        ByKey(tree, "findReaction").OnClick();
        Assert.Equal(new[] { ("recipe_click", "{\"id\":\"/datum/chemical_reaction/medicine/calomel\"}"), ("toggle_tag_healing", (string)null), ("find_reagent_reaction", "{\"id\":\"/datum/reagent/mercury\"}") }, acts);
    }
}
