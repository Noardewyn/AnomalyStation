using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package P2: the client's hydroponics windows without the engine: SeedExtractor.tsx (with SeedTable.tsx), Biogenerator.tsx and
/// PlantAnalyzer/ read tg's ui_data the way the TSX does and send tg's acts.
/// </summary>
public class HydroponicsWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface view, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, view(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Traits = "\"trait_db\":[{\"path\":\"/datum/plant_gene/trait/repeated_harvest\",\"name\":\"Perennial Growth\",\"icon\":\"cubes-stacked\",\"description\":\"It may be harvested multiple times from the same plant.\"}]";

    private const string Wheat = "{\"key\":\"abc\",\"amount\":3,\"name\":\"Wheat seed pack\",\"lifespan\":25,\"endurance\":15,\"maturation\":6,\"production\":1,\"yield\":4,\"potency\":15,"
        + "\"instability\":20,\"icon\":\"icons/obj/service/hydroponics/seeds.dmi\",\"icon_state\":\"seed-wheat\",\"volume_mod\":1,\"volume_units\":100,"
        + "\"traits\":[\"/datum/plant_gene/trait/repeated_harvest\"],\"reagents\":[{\"name\":\"Nutriment\",\"rate\":0.04}],\"mutatelist\":[\"Oat Stalks\",\"Meatwheat\"],"
        + "\"grind_results\":[\"Flour\"],\"distill_reagent\":\"Beer\",\"juice_name\":\"\"}";

    [Fact]
    public void The_seed_extractor_lists_its_piles_with_tgs_stats_and_takes_or_scraps_one()
    {
        var (context, tree, acts) = Render(SeedExtractor.Render, "SeedExtractor", "{\"seeds\":[" + Wheat + "],\"cycle_seconds\":20," + Traits + "}");
        Assert.Equal((900.0, 500.0), (context.WindowWidth, context.WindowHeight));
        UiNode row = ByKey(tree, "seed:abc");
        Assert.Contains("3x Wheat seed pack", row.AllText());
        Assert.Equal(("icons/obj/service/hydroponics/seeds.dmi", "seed-wheat"), (row.Walk().First(n => n.DmIcon != null).DmIcon, row.Walk().First(n => n.DmIcon != null).DmIconState));
        Assert.Equal(15.0, ByKey(tree, "abc:potency").Value);
        Assert.Equal("8m20s", row.Walk().Single(n => n.Key == "lifespan").Text); // formatTime(25 * 20 * 10, 'short')
        Assert.Equal("2m", row.Walk().Single(n => n.Key == "maturation").Text);
        Assert.Contains(row.Walk(), n => n.Kind == UiKind.Icon && n.Icon == "dna" && n.Tooltip == "Mutates into: Oat Stalks, Meatwheat");
        Assert.Contains(row.Walk(), n => n.Kind == UiKind.Icon && n.Icon == "wine-bottle" && n.Tooltip == "Ferments into: Beer");
        Assert.Contains(row.Walk(), n => n.Key == "abc:trait:/datum/plant_gene/trait/repeated_harvest");
        ByKey(tree, "take:abc").OnClick();
        ByKey(tree, "scrap:abc").OnClick();
        Assert.Equal(new[] { ("take", "{\"item\":\"abc\"}"), ("scrap", "{\"item\":\"abc\"}") }, acts);
        var (_, empty, _) = Render(SeedExtractor.Render, "SeedExtractor", "{\"seeds\":[],\"cycle_seconds\":20," + Traits + "}");
        Assert.Contains(empty.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "No seeds found.");
    }

    [Fact]
    public void The_seed_table_sorts_by_a_stat_and_searches_by_name()
    {
        string oats = Wheat.Replace("\"key\":\"abc\"", "\"key\":\"oat\"").Replace("Wheat seed pack", "Oat seed pack").Replace("\"potency\":15", "\"potency\":5");
        var local = new TguiLocalState();
        string json = "{\"seeds\":[" + Wheat + "," + oats + "],\"cycle_seconds\":20," + Traits + "}";
        var (_, tree, _) = Render(SeedExtractor.Render, "SeedExtractor", json, local);
        Assert.Equal(new[] { "seed:oat", "seed:abc" }, tree.Walk().Where(n => n.Key?.StartsWith("seed:") == true).Select(n => n.Key)); // by name
        ByKey(tree, "sortButton:potency").OnClick();
        (_, tree, _) = Render(SeedExtractor.Render, "SeedExtractor", json, local);
        Assert.Equal(new[] { "seed:abc", "seed:oat" }, tree.Walk().Where(n => n.Key?.StartsWith("seed:") == true).Select(n => n.Key)); // the highest potency first
        ByKey(tree, "searchInput").OnInput("whe");
        (_, tree, _) = Render(SeedExtractor.Render, "SeedExtractor", json, local);
        Assert.Equal(new[] { "seed:abc" }, tree.Walk().Where(n => n.Key?.StartsWith("seed:") == true).Select(n => n.Key));
    }

    private const string Biogen = "{\"beaker\":1,\"biomass\":123.456,\"processing\":0,\"max_output\":50,\"efficiency\":1,\"can_process\":1,\"beakerCurrentVolume\":10,\"beakerMaxVolume\":50,"
        + "\"reagent_color\":\"#ffffff\",\"max_visual_biomass\":5000,\"categories\":[{\"name\":\"Food\",\"items\":[{\"path\":\"/datum/design/biogen/milk\",\"name\":\"Synthetic Milk\","
        + "\"icon\":\"biogen_milk\",\"is_reagent\":1,\"cost\":0.4}]},{\"name\":\"Materials\",\"items\":[{\"path\":\"/datum/design/biogen/cloth\",\"name\":\"Cloth\",\"icon\":\"biogen_cloth\",\"is_reagent\":0,\"cost\":50}]}]}";

    [Fact]
    public void The_biogenerator_shows_its_biomass_beaker_and_designs_and_prints_one()
    {
        var local = new TguiLocalState();
        var (context, tree, acts) = Render(Biogenerator.Render, "Biogenerator", Biogen, local);
        Assert.Equal((400.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("123.46 units", ByKey(tree, "biomassBar").Text);
        Assert.Equal("10 of 50 units", ByKey(tree, "beakerBar").Text);
        Assert.True(ByKey(tree, "tab:Food").Selected);
        UiNode milk = ByKey(tree, "item:/datum/design/biogen/milk");
        Assert.Equal((TguiSpritesheets.Design32, "biogen_milk"), (ByKey(milk, "icon").DmIcon, ByKey(milk, "icon").DmIconState));
        Assert.Equal(10.0, ByKey(tree, "amountInput:/datum/design/biogen/milk").Value); // min(max(space 40, 1), 10)
        Assert.Equal("4", ByKey(tree, "create:/datum/design/biogen/milk").Text);
        ByKey(tree, "create:/datum/design/biogen/milk").OnClick();
        ByKey(tree, "activate").OnClick();
        ByKey(tree, "eject").OnClick();
        Assert.Equal(new[] { "create", "activate", "eject" }, acts.Select(a => a.Action));
        Assert.Equal("{\"design_path\":\"/datum/design/biogen/milk\",\"amount\":10}", acts[0].Params);

        // the materials tab: cloth costs 50 a sheet, more than the biomass for three
        ByKey(tree, "tab:Materials").OnClick();
        (_, tree, _) = Render(Biogenerator.Render, "Biogenerator", Biogen, local);
        UiNode cloth = ByKey(tree, "create:/datum/design/biogen/cloth");
        Assert.False(cloth.Disabled);
        Assert.Equal(2.0, ByKey(tree, "amountInput:/datum/design/biogen/cloth").MaxValue); // floor(123.456 / 50)
        var (_, empty, _) = Render(Biogenerator.Render, "Biogenerator", Biogen.Replace("\"beaker\":1", "\"beaker\":0"));
        Assert.Contains(empty.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "No liquid container");
    }

    [Fact]
    public void The_plant_analyzer_shows_a_tray_and_its_seed_and_switches_to_the_chemicals()
    {
        string tray = "\"tray_data\":{\"plant_health\":12,\"plant_age\":3,\"is_dead\":0,\"name\":\"hydroponics tray (Wheat Stalks)\",\"icon\":\"icons/obj/service/hydroponics/equipment.dmi\","
            + "\"icon_state\":\"hydrotray3\",\"water\":80,\"water_max\":100,\"nutri\":5,\"nutri_max\":20,\"yield_mod\":1,\"being_pollinated\":0,\"self_sustaining\":1,\"light_level\":0.5,"
            + "\"weeds\":2,\"weeds_max\":10,\"pests\":0,\"pests_max\":10,\"toxins\":0,\"toxins_max\":100,\"reagents\":[{\"name\":\"E-Z Nutrient\",\"volume\":5,\"color\":\"#376400\"}]}";
        string seed = "\"seed_data\":{\"name\":\"Wheat Stalks\",\"icon\":\"icons/obj/service/hydroponics/growing.dmi\",\"icon_state\":\"wheat-harvest\",\"product\":\"wheat\","
            + "\"product_icon\":\"icons/obj/service/hydroponics/harvest.dmi\",\"product_icon_state\":\"wheat\",\"potency\":15,\"yield\":4,\"instability\":20,\"maturation\":6,"
            + "\"production\":1,\"lifespan\":25,\"endurance\":15,\"weed_rate\":1,\"weed_chance\":5,\"graft_gene\":\"/datum/plant_gene/trait/repeated_harvest\",\"removable_traits\":[],"
            + "\"core_traits\":[],\"distill_reagent\":\"Beer\",\"juice_name\":\"\",\"grind_results\":[\"Flour\"],\"reagents\":[{\"name\":\"Nutriment\",\"rate\":0.04}],\"volume_mod\":1,"
            + "\"volume_units\":100,\"mutatelist\":[\"Oat Stalks\"],\"unique_labels\":[],\"unique_collapsibles\":[]}";
        string json = "{\"active_tab\":1,\"cycle_seconds\":20," + Traits + "," + tray + "," + seed + ",\"plant_data\":null,\"graft_data\":null}";
        var (context, tree, acts) = Render(PlantAnalyzer.Render, "PlantAnalyzer", json);
        Assert.Equal((475.0, 625.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Hydroponics tray (Wheat Stalks)", ByKey(tree, "trayStats").Title);
        Assert.Equal("80 / 100", ByKey(tree, "waterBar").Text);
        Assert.Equal("50%", ByKey(tree, "lightBar").Text);
        Assert.Equal("yellow", ByKey(tree, "autogrow").Color);
        Assert.Equal("12 / 15", ByKey(tree, "healthBar").Text);
        Assert.Equal("60 / 500 seconds", ByKey(tree, "ageBar").Text); // 3 cycles of 20 s of a 25-cycle lifespan
        Assert.Equal("2 minutes", ByKey(tree, "maturation").Text);
        Assert.Equal("Perennial Growth", ByKey(tree, "graftGene").Text);
        ByKey(tree, "tab:chem").OnClick();
        Assert.Equal(("setTab", "{\"tab\":2}"), acts[0]);

        (_, tree, _) = Render(PlantAnalyzer.Render, "PlantAnalyzer", json.Replace("\"active_tab\":1", "\"active_tab\":2"));
        Assert.Contains("E-Z Nutrient", ByKey(tree, "trayChems").AllText());
        Assert.Contains("~1u", ByKey(tree, "seedChems").AllText()); // round(0.04 * 100 * 0.15)
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "trayStats");
    }
}
