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
/// Gaps package P2 (D298): hydroponics in a booted toy room. A seed planted in a tray with water and nutrients grows through its stages
/// (the tray's plant picture and status lights follow) and is harvested; weeds and pests come and are handled (the cultivator, the pest
/// killer, a weed invasion of an empty tray); the plant mutates into another species; the seed extractor makes seeds of a plant; the
/// biogenerator turns plants into biomass and prints an item; a graft moves a gene to another plant (tg's way of moving genes since the
/// DNA manipulator's removal); the plant analyzer reads a tray; bees make honeycomb; and every type spawns and goes with no runtimes.
/// </summary>
public class HydroponicsTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static T Place<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.@new<T>(locate(x, y, 1)));

    private static void Hold(AudioRoom r, mob_living_carbon_human who, obj_item item) => r.In(() =>
    {
        Waiting.NoSleep(who.drop_all_held_items());
        Waiting.NoSleep(who.put_in_active_hand(item));
    });

    /// <summary>A click with what is in the hand, then the click cooldown (CLICK_CD_MELEE) passes.</summary>
    private static void Use(AudioRoom r, mob_living_carbon_human who, atom target)
    {
        Click(r, who, target);
        r.Step(Second);
    }

    /// <summary>One growth cycle of the tray, as its process runs it when cycledelay has passed (SSmachines' 2 s).</summary>
    private static void Cycle(AudioRoom r, obj_machinery_hydroponics tray, int n = 1)
    {
        for (int i = 0; i < n; i++)
            r.In(() =>
            {
                tray.lastcycle = Dm.num(world.time) - tray.cycledelay - 1;
                tray.process(2);
            });
    }

    private static List<string> OverlayStates(AudioRoom r, obj_machinery_hydroponics tray) => r.In(() =>
        Dm.each(Waiting.NoSleep(tray.update_overlays()) as DmList).Select(o => (o as mutable_appearance)?.icon_state ?? Dm.str(o)).ToList());

    private static obj_machinery_hydroponics_constructable Tray(AudioRoom r)
    {
        var tray = Place<obj_machinery_hydroponics_constructable>(r, 4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(((datum_reagents)tray.reagents).add_reagent(typeof(datum_reagent_plantnutriment_eznutriment), 10));
            tray.waterlevel = tray.maxwater;
        });
        return tray;
    }

    private static obj_item_seeds Seed<T>(AudioRoom r, mob_living_carbon_human bob) where T : obj_item_seeds
    {
        var seed = r.In(() => Dm.@new<T>(bob.loc));
        r.In(() =>
        {
            seed.maturation = 3;
            seed.set_production(2);
            seed.set_yield(3);
            seed.set_instability(0);
            seed.set_endurance(50);
            seed.set_weed_chance(0);
        });
        return seed;
    }

    [Fact]
    public void ASeedGrowsThroughItsStagesAndIsHarvested()
    {
        var (r, bob) = Room(9201);
        var feed = Listen(r);
        var tray = Tray(r);
        Assert.False(InertSeam.IsInert(tray));
        var seed = Seed<obj_item_seeds_wheat>(r, bob);
        Hold(r, bob, seed);
        Use(r, bob, tray);
        Assert.Same(seed, r.In(() => tray.myseed));
        Assert.Equal(HYDROTRAY_PLANT_GROWING, r.In(() => tray.plant_status));
        Assert.True(feed.Any("You plant"));
        Assert.Contains("wheat-grow1", OverlayStates(r, tray));
        Assert.Equal("hydroponics tray (Wheat Stalks)", r.In(() => tray.name));

        // the plant grows a stage a cycle and shows it; water and nutrients drain
        Cycle(r, tray, 2);
        Assert.Equal(2, r.In(() => Dm.num(tray.age)));
        Assert.Contains("wheat-grow4", OverlayStates(r, tray)); // round(2 / 3 * 6)
        Assert.True(r.In(() => tray.waterlevel) < r.In(() => tray.maxwater), "the plant drinks");

        // past its production it is ready: the harvest light comes on and a hand gathers the crop
        Cycle(r, tray, 4);
        Assert.Equal(HYDROTRAY_PLANT_HARVESTABLE, r.In(() => tray.plant_status));
        List<string> states = OverlayStates(r, tray);
        Assert.Contains("over_harvest3", states);
        Assert.Contains("wheat-harvest", states);
        float expected = r.In(() => Dm.num(tray.myseed.getYield()));
        Hold(r, bob, null);
        Use(r, bob, tray);
        int crop = r.In(() => Dm.each<obj_item_food_grown_wheat>().Count());
        Assert.Equal(expected, crop);
        // the crop's nutriment takes the plant's tastes (COMSIG_REAGENT_GROWN_IN_PLANT)
        var nutriment = r.In(() => ((datum_reagents)Dm.each<obj_item_food_grown_wheat>().First().reagents).has_reagent(typeof(datum_reagent_consumable_nutriment)) as datum_reagent);
        Assert.NotNull(nutriment);
        Assert.Equal(1f, r.In(() => Dm.num(((DmList)nutriment.data)["wheat"])));
        Assert.Null(r.In(() => tray.myseed)); // wheat is not perennial: the tray empties
        Assert.Equal(HYDROTRAY_NO_PLANT, r.In(() => tray.plant_status));
        Assert.True(feed.Any("You harvest " + expected + " items from the Wheat Stalks."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WeedsAndPestsComeAndAreHandled()
    {
        var (r, bob) = Room(9202);
        var tray = Tray(r);
        r.In(() =>
        {
            tray.adjust_weedlevel(6);
            tray.adjust_pestlevel(6);
        });
        List<string> states;
        r.In(() => Waiting.NoSleep(tray.set_seed(Dm.@new<obj_item_seeds_wheat>(tray))));
        states = OverlayStates(r, tray);
        Assert.Contains("over_alert3", states);

        // the cultivator uproots the weeds
        Hold(r, bob, Place<obj_item_cultivator>(r, 3, 4));
        Use(r, bob, tray);
        Assert.Equal(0, r.In(() => tray.weedlevel));

        // pest killer poured in kills pests over the cycles (and is a little toxic)
        var bottle = Place<obj_item_reagent_containers_cup_bottle_killer_pestkiller>(r, 3, 4);
        Hold(r, bob, bottle);
        Use(r, bob, tray);
        float pests = r.In(() => tray.pestlevel);
        Cycle(r, tray);
        Assert.True(r.In(() => tray.pestlevel) < pests, "the pest killer kills pests");
        Assert.True(r.In(() => tray.toxic) > 0, "the pest killer is toxic");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/slosh.ogg"); // a bottle is drainable: it splashes

        // an empty tray overgrown with weeds is invaded by a weed plant
        r.In(() => Waiting.NoSleep(tray.remove_plant()));
        r.In(() => tray.adjust_weedlevel(10));
        for (int i = 0; i < 40 && r.In(() => tray.myseed) == null; i++)
        {
            r.In(() => tray.adjust_weedlevel(10));
            Cycle(r, tray);
        }
        Assert.NotNull(r.In(() => tray.myseed));
        Assert.Equal(0, r.In(() => tray.weedlevel));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APlantMutatesIntoAnotherSpecies()
    {
        var (r, _) = Room(9203);
        var tray = Tray(r);
        r.In(() => Waiting.NoSleep(tray.set_seed(Dm.@new<obj_item_seeds_cabbage>(tray))));
        r.In(() => tray.set_plant_health(tray.myseed.endurance));
        float potency = r.In(() => Dm.num(tray.myseed.potency));
        r.In(() => Waiting.NoSleep(tray.mutatespecie()));
        Assert.IsType<obj_item_seeds_replicapod>(r.In(() => tray.myseed)); // the cabbage's only mutation
        Assert.Equal(0, r.In(() => Dm.num(tray.age)));
        r.Step(Second);
        Assert.Contains("hydroponics tray (Replica Pod)", r.In(() => tray.name));

        // the stats drift on a hard mutation and the species' traits move with a graft
        r.In(() => Waiting.NoSleep(tray.set_seed(Dm.@new<obj_item_seeds_tomato>(tray))));
        r.In(() => tray.hardmutate(potmut: 50));
        Assert.True(r.In(() => tray.myseed.potency) != null);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSeedExtractorMakesSeedsOfAPlant()
    {
        var (r, bob) = Room(9204);
        var extractor = Place<obj_machinery_seed_extractor>(r, 4, 4);
        var apple = r.In(() => Dm.@new<obj_item_food_grown_apple>(bob.loc));
        Hold(r, bob, apple);
        Use(r, bob, extractor);
        Assert.True(r.In(() => QDELETED(apple)));
        int made = r.In(() => Dm.each<obj_item_seeds_apple>(extractor.loc).Count());
        Assert.InRange(made, 2, 5); // rand(1, 4) * the servo's tier, plus one (tg's 0 to t_max loop)

        // seeds stored in the machine show as a pile in its window data
        var seed = r.In(() => Dm.each<obj_item_seeds_apple>(extractor.loc).First());
        Hold(r, bob, seed);
        Use(r, bob, extractor);
        DmList data = r.In(() => Waiting.NoSleep(extractor.ui_data(bob)) as DmList);
        DmList pile = r.In(() => (data["seeds"] as DmList)[1] as DmList);
        Assert.Equal("Apple seed pack", r.In(() => Dm.str(pile["name"])));
        Assert.Equal(1, r.In(() => Dm.num(pile["amount"])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBiogeneratorTurnsPlantsIntoBiomassAndPrintsAnItem()
    {
        var (r, bob) = Room(9205);
        var biogen = Place<obj_machinery_biogenerator>(r, 4, 4);
        for (int i = 0; i < 4; i++)
        {
            var potato = r.In(() => Dm.@new<obj_item_food_grown_potato>(bob.loc));
            Hold(r, bob, potato);
            Use(r, bob, biogen);
        }
        Assert.Equal(4, r.In(() => Dm.num(biogen.get_content_count())));
        r.In(() => Waiting.NoSleep(biogen.start_process()));
        Assert.Equal(1, r.In(() => biogen.processing));
        r.Step(6 * Second);
        Assert.Equal(0, r.In(() => biogen.processing));
        float biomass = r.In(() => biogen.biomass);
        Assert.True(biomass > 0, "the potatoes became biomass");
        Assert.Contains(r.In(() => Dm.each(Waiting.NoSleep(biogen.update_overlays()) as DmList).Select(o => (o as mutable_appearance)?.icon_state).ToList()), s => s != null && s.StartsWith("biogenerator_o_biomass_", StringComparison.Ordinal));

        // the window lists the biogenerator's designs (its auto-unlocking techweb); printing cloth costs biomass and drops the sheet
        DmList statics = r.In(() => Waiting.NoSleep(biogen.ui_static_data(bob)) as DmList);
        Assert.Equal(3, r.In(() => (statics["categories"] as DmList).len));
        r.In(() => biogen.biomass = 500);
        r.In(() =>
        {
            datum_design cloth = Dm.@new<datum_design_biogen_cloth>();
            Waiting.NoSleep(biogen.create_product(cloth, 1));
        });
        Assert.NotNull(r.In(() => Dm.locate<obj_item_stack_sheet_cloth>(biogen.loc)));
        Assert.True(r.In(() => biogen.biomass) < 500, "the print cost biomass");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGraftMovesAGeneToAnotherPlant()
    {
        var (r, bob) = Room(9206);
        var donor = Tray(r);
        var receiver = Place<obj_machinery_hydroponics_constructable>(r, 5, 4);
        r.In(() =>
        {
            Waiting.NoSleep(donor.set_seed(Dm.@new<obj_item_seeds_tomato_blue>(donor))); // blue tomatoes are slippery
            donor.set_plant_health(donor.myseed.endurance);
            donor.set_plant_status(HYDROTRAY_PLANT_HARVESTABLE);
            donor.myseed.graft_gene = typeof(datum_plant_gene_trait_slip);
            Waiting.NoSleep(receiver.set_seed(Dm.@new<obj_item_seeds_wheat>(receiver)));
            receiver.set_plant_health(receiver.myseed.endurance);
        });
        Hold(r, bob, Place<obj_item_secateurs>(r, 3, 4));
        Use(r, bob, donor);
        var graft = r.In(() => Dm.locate<obj_item_graft>(bob.loc) ?? Dm.locate<obj_item_graft>(donor.loc));
        Assert.NotNull(graft);
        Assert.Equal(1, r.In(() => donor.myseed.grafts_taken));
        Assert.Contains("Blue-Tomato Plants", r.In(() => Dm.str(graft.name)));

        Hold(r, bob, graft);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(5, 5, 1))));
        Use(r, bob, receiver);
        Assert.NotNull(r.In(() => receiver.myseed.get_gene(typeof(datum_plant_gene_trait_slip))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlantAnalyzerReadsATray()
    {
        var (r, bob) = Room(9207);
        var tray = Tray(r);
        r.In(() => Waiting.NoSleep(tray.set_seed(Dm.@new<obj_item_seeds_wheat>(tray))));
        r.In(() => tray.set_plant_health(12));
        var analyzer = Place<obj_item_plant_analyzer>(r, 3, 4);
        r.In(() => Waiting.NoSleep(analyzer.analyze(null, tray)));
        DmList data = r.In(() => analyzer.last_scan_data);
        DmList tray_data = r.In(() => data["tray_data"] as DmList);
        Assert.Equal(r.In(() => tray.maxwater), r.In(() => Dm.num(tray_data["water"])));
        Assert.Equal(12, r.In(() => Dm.num(tray_data["plant_health"])));
        DmList seed_data = r.In(() => data["seed_data"] as DmList);
        Assert.Equal("Wheat Stalks", r.In(() => Dm.str(seed_data["name"])));
        Assert.Contains("Flour", r.In(() => Dm.each(seed_data["grind_results"] as DmList).Select(Dm.str).ToList()));
        DmList statics = r.In(() => Waiting.NoSleep(analyzer.ui_static_data(bob)) as DmList);
        Assert.True(r.In(() => (statics["trait_db"] as DmList).len) > 40, "every plant gene is in the trait table");

        // the analyzer on a tray's board moves the status lights (the oldstyle tray), and back
        var board = Place<obj_item_circuitboard_machine_hydroponics>(r, 3, 4);
        Hold(r, bob, analyzer);
        Use(r, bob, board);
        Assert.Same(typeof(obj_machinery_hydroponics_constructable_oldstyle), r.In(() => board.build_path));
        Assert.StartsWith("Hydroponics Tray (Alt)", r.In(() => Dm.str(board.name)));
        Use(r, bob, board);
        Assert.Same(typeof(obj_machinery_hydroponics_constructable), r.In(() => board.build_path));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BeesMakeHoneycomb()
    {
        var (r, _) = Room(9208);
        var hive = Place<obj_structure_beebox_premade>(r, 6, 4);
        Assert.NotNull(r.In(() => hive.queen_bee));
        Assert.Equal(3, r.In(() => hive.honey_frames.len));
        Assert.Equal(16, r.In(() => hive.bees.len)); // the queen and 15 workers: 30 honeycomb places * BEES_RATIO
        r.In(() => hive.bee_resources = 100);
        r.Step(3 * Second); // SSobj processes the hive
        Assert.True(r.In(() => hive.honeycombs.len) >= 1, "the full resource bar became a honeycomb");
        Assert.True(r.In(() => hive.bee_resources) < 100);

        // a bee pollinates a tray: healthier plant and the hive's resources
        var tray = Tray(r);
        r.In(() =>
        {
            Waiting.NoSleep(tray.set_seed(Dm.@new<obj_item_seeds_wheat>(tray)));
            tray.set_plant_health(5);
            hive.bee_resources = 0;
        });
        var bee = r.In(() => Dm.each_cast<mob_living_basic_bee>(hive.bees).First(b => !Dm.truthy(b.is_queen)));
        r.In(() => bee.pollinate(tray));
        Assert.True(r.In(() => Dm.num(tray.plant_health)) > 5);
        Assert.True(r.In(() => hive.bee_resources) > 0);
        Assert.Equal(1, r.In(() => tray.recent_bee_visit));

        // a space bear raids the stocked hive (its targeting strategy accepts it) and drops a honeycomb at its feet
        var bear = Place<mob_living_basic_bear>(r, 6, 6);
        int combs = r.In(() => (int)hive.honeycombs.len);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(Dm.@new<datum_targeting_strategy_stocked_beehive>().is_valid_target(bear, hive, 9)))));
        r.In(() => bear.extract_combs(hive));
        Assert.Equal(combs - 1, r.In(() => (int)hive.honeycombs.len));
        Assert.Contains(r.In(() => Dm.each_cast<atom>(((turf)get_turf(bear)).contents).ToList()), a => a is obj_item_food_honeycomb);

        // the plumbing fermenter turns a grown apple into its cider (a quarter of the seed's potency)
        var fermenter = Place<obj_machinery_plumbing_fermenter>(r, 2, 6);
        var apple = r.In(() => Dm.@new<obj_item_food_grown_apple>(locate(2, 5, 1)));
        object cider = r.In(() => apple.distill_reagent);
        float potency = r.In(() => Dm.num(((obj_item_seeds)apple.seed).potency));
        r.In(() => Waiting.NoSleep(fermenter.ferment(apple)));
        Assert.Equal(potency * 0.25f, r.In(() => Dm.num(((datum_reagents)fermenter.reagents).get_reagent_amount(cider))), 2);
        Assert.True(r.In(() => QDELETED(apple)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGlowshroomIsPlantedGlowsSpreadsAndDecays()
    {
        var (r, bob) = Room(9211);
        var shroom = r.In(() => Dm.@new<obj_item_food_grown_mushroom_glowshroom>(bob.loc));
        Hold(r, bob, shroom);
        r.In(() => Waiting.NoSleep(shroom.attack_self(bob)));
        var planted = r.In(() => Dm.each<obj_structure_glowshroom>().Single());
        Assert.Same(r.In(() => bob.loc), r.In(() => planted.loc));
        Assert.True(r.In(() => QDELETED(shroom)));
        Assert.Equal(1, r.In(() => GLOB.glowshrooms));
        Assert.True(r.In(() => Dm.num(planted.light_range)) > 0, "the glow gene lights the shroom");
        Assert.Equal(1, r.In(() => planted.generation));

        // the analyzer reads its seed
        var analyzer = Place<obj_item_plant_analyzer>(r, 3, 4);
        r.In(() => Waiting.NoSleep(analyzer.analyze(null, planted)));
        Assert.Equal("Glowshrooms", r.In(() => Dm.str((analyzer.last_scan_data["seed_data"] as DmList)["name"])));

        // it spreads over the floor around it (tiled: the room's indestructible floor is no /turf/open/floor): the children are the
        // next generation
        r.In(() =>
        {
            for (int x = 1; x <= 5; x++)
                for (int y = 2; y <= 6; y++)
                    if (locate(x, y, 1) is turf_open t && t != planted.loc)
                        Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor_iron)));
        });
        r.Step(Second);
        r.In(() =>
        {
            var seed = (obj_item_seeds)planted.myseed;
            seed.potency = 100;
            seed.yield = 10;
            planted.spread_into_adjacent_chance = 100;
            planted.Spread();
        });
        List<obj_structure_glowshroom> all = r.In(() => Dm.each<obj_structure_glowshroom>().ToList());
        Assert.True(all.Count > 1, "the shroom spreads: " + r.In(() => $"shares {TURF_SHARES(get_turf(planted) as turf)}, floors {Dm.each<turf_open_floor>(oview(2, planted)).Count()}, turf {get_turf(planted)?.GetType().Name}"));
        Assert.All(all.Where(g => g != planted), g => Assert.Equal(2, r.In(() => g.generation)));
        Assert.Equal(all.Count, r.In(() => GLOB.glowshrooms));

        // it decays: its seed's endurance drops with its integrity until it dies
        float integrity = r.In(() => Dm.num(planted.get_integrity()));
        r.In(() => Waiting.NoSleep(planted.Decay(10)));
        Assert.Equal(integrity - 10, r.In(() => Dm.num(planted.get_integrity())));
        r.In(() => ((obj_item_seeds)planted.myseed).endurance = MIN_PLANT_ENDURANCE + 1);
        r.In(() => Waiting.NoSleep(planted.Decay(10)));
        Assert.True(r.In(() => QDELETED(planted)));
        Assert.Equal(all.Count - 1, r.In(() => GLOB.glowshrooms));
        r.AssertNoRuntimes();
    }

    // the types of the package (the abstract ones are never made, as tg's valid_subtypesof)
    private static readonly Type[] Roots =
    {
        typeof(obj_item_seeds), typeof(obj_item_food_grown), typeof(obj_item_grown), typeof(obj_machinery_hydroponics), typeof(obj_machinery_seed_extractor),
        typeof(obj_machinery_biogenerator), typeof(obj_structure_fermenting_barrel), typeof(obj_structure_beebox), typeof(obj_item_soil_sack), typeof(obj_item_graft),
        typeof(obj_item_cultivator), typeof(obj_item_hatchet), typeof(obj_item_secateurs), typeof(obj_item_geneshears), typeof(obj_item_plant_analyzer),
        typeof(obj_item_reagent_containers_cup_watering_can), typeof(obj_item_reagent_containers_cup_bottle_nutrient), typeof(obj_item_reagent_containers_cup_bottle_killer),
        typeof(obj_item_reagent_containers_spray_pestspray), typeof(obj_item_reagent_containers_spray_weedspray), typeof(obj_item_storage_bag_plants),
        typeof(obj_item_honey_frame), typeof(obj_item_food_honeycomb), typeof(obj_item_bee_smoker), typeof(obj_item_queen_bee), typeof(obj_item_trash_bee),
        typeof(mob_living_basic_bee), typeof(obj_structure_punji_sticks), typeof(obj_structure_hedge), typeof(obj_item_clothing_head_utility_beekeeper_head),
        typeof(obj_item_clothing_suit_utility_beekeeper_suit), typeof(obj_item_bouquet), typeof(obj_structure_glowshroom),
        typeof(obj_item_circuitboard_machine_hydroponics),
    };

    [Fact]
    public void EveryTypeSpawnsLiveAndGoes()
    {
        var (r, _) = Room(9209);
        List<Type> types = r.In(() => Roots.SelectMany(root => Dm.each_cast<Type>(Dm.typesof(root)))
            .Where(t => !Dm.eq(Dm.initial(t, "abstract_type"), t)).Distinct().ToList());
        // the other packages' subtypes stay inert: the clown operatives' bombanana, the heretic's poppy, lavaland's ash flora
        List<Type> others = types.Where(InertSeam.IsInertType).ToList();
        Assert.All(others, t => Assert.True(t.Name.Contains("bombanana") || t.Name.Contains("blessed") || t.Name.Contains("ash_flora"), t.Name + " is inert"));
        types = types.Except(others).ToList();
        Assert.True(types.Count > 350, "types: " + types.Count);
        foreach (Type type in types)
        {
            atom thing = r.In(() => Dm.@new(type, locate(2, 2, 1)) as atom);
            r.Step(1);
            r.In(() => { if (!QDELETED(thing)) qdel(thing); });
        }
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }
}
