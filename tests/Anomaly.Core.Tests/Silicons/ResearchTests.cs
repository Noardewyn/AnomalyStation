using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI3 (D326): research and the exosuit fabricator. SSresearch makes every design and node and the station's science techweb
/// with its starter nodes researched; a node researched with its points unlocks its designs; the fabricator offers the techweb's
/// fabricator designs but the inert items' and the mechs' (DV384), takes a new design from the techweb, and prints a cyborg
/// endoskeleton from its materials; the cybernetic organs fail under an EMP and the best heart stabilises its owner.
/// </summary>
public class ResearchTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        // the toy room has no APC: free power
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return r;
    }

    private static datum_techweb_science Science(AudioRoom r) => r.In(() =>
        Dm.each<datum_techweb>(SSresearch.techwebs).OfType<datum_techweb_science>().Single());

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_controller_subsystem_research), typeof(datum_techweb), typeof(datum_techweb_science), typeof(datum_techweb_admin),
            typeof(datum_techweb_oldstation), typeof(datum_techweb_autounlocking_autolathe), typeof(datum_techweb_node), typeof(datum_techweb_node_ai),
            typeof(datum_techweb_node_alien_base), typeof(datum_techweb_node_syndicate_basic), typeof(datum_techweb_node_consoles),
            typeof(datum_scientific_partner), typeof(obj_machinery_mecha_part_fabricator), typeof(datum_design_mod_plating_civilian),
            typeof(datum_design_module_mod_storage), typeof(datum_design_toolbox), typeof(datum_design_board_ai_law_rack),
            typeof(obj_item_organ_heart_cybernetic_tier3), typeof(obj_item_organ_eyes_robotic_basic), typeof(obj_item_organ_lungs_cybernetic),
            typeof(obj_item_organ_liver_cybernetic), typeof(obj_item_organ_stomach_cybernetic), typeof(obj_item_organ_ears_cybernetic),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheStationTechwebStartsWithItsStarterNodesResearched()
    {
        var r = Room(9401);
        Assert.True(r.In(() => Dm.length(SSresearch.techweb_designs)) > 1000);
        Assert.True(r.In(() => Dm.length(SSresearch.techweb_nodes)) > 100);
        var web = Science(r);
        r.In(() =>
        {
            Assert.True(Dm.truthy(web.researched_nodes[typeof(datum_techweb_node_robotics)]));
            Assert.True(Dm.truthy(web.researched_nodes[typeof(datum_techweb_node_augmentation)]));
            Assert.True(Dm.truthy(web.researched_designs[typeof(datum_design_borg_suit)]), "the augmentation node unlocks the endoskeleton");
            Assert.True(Dm.truthy(web.researched_designs[typeof(datum_design_board_mechfab)]));
            // the robotics node's children are next
            Assert.True(Dm.truthy(web.available_nodes[typeof(datum_techweb_node_exodrone)]));
            // the alien node stays hidden
            Assert.False(Dm.truthy(web.available_nodes[typeof(datum_techweb_node_alien_base)]) || Dm.truthy(web.visible_nodes[typeof(datum_techweb_node_alien_base)]));
            // the design remembers what unlocks it, the prerequisite what it unlocks
            var suit = SSresearch.techweb_designs[typeof(datum_design_borg_suit)] as datum_design;
            Assert.Contains(typeof(datum_techweb_node_augmentation), Dm.each<object>(suit.unlocked_by));
            var robotics = SSresearch.techweb_nodes[typeof(datum_techweb_node_robotics)] as datum_techweb_node;
            Assert.Contains(typeof(datum_techweb_node_augmentation), Dm.each<object>(robotics.unlocked_nodes));
        });
        Assert.StartsWith("External plating for a MODsuit.", r.In(() => Dm.str((SSresearch.techweb_designs[typeof(datum_design_mod_plating_civilian)] as datum_design).desc)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ANodeResearchedWithItsPointsUnlocksItsDesigns()
    {
        var r = Room(9402);
        var web = Science(r);
        r.In(() =>
        {
            var node = SSresearch.techweb_nodes[typeof(datum_techweb_node_exodrone)] as datum_techweb_node;
            float price = Dm.num(node.get_price(web)[TECHWEB_POINT_TYPE_GENERIC]);
            Assert.True(price > 0);
            Assert.False(Waiting.NoSleep(web.research_node(node)), "no points, no research");
            web.adjust_points(TECHWEB_POINT_TYPE_GENERIC, price + 100);
            Assert.True(Waiting.NoSleep(web.research_node(node)));
            Assert.Equal(100f, Dm.num(web.research_points[TECHWEB_POINT_TYPE_GENERIC]));
            Assert.True(Dm.truthy(web.researched_nodes[typeof(datum_techweb_node_exodrone)]));
            Assert.True(Dm.truthy(web.researched_designs[typeof(datum_design_board_exodrone_console)]));
            Assert.Equal("Gen. Res.: 100", Dm.str(web.printout_points()));
        });
        r.AssertNoRuntimes();
    }

    private static obj_machinery_mecha_part_fabricator Fab(AudioRoom r)
    {
        var fab = r.In(() => Dm.@new<obj_machinery_mecha_part_fabricator>(locate(5, 6, 1)));
        r.Step();
        return fab;
    }

    [Fact]
    public void TheFabricatorOffersTheTechwebsDesignsButTheInertAndTheMechs()
    {
        var r = Room(9403);
        var fab = Fab(r);
        var web = Science(r);
        Assert.Same(web, r.In(() => fab.stored_research));
        var offered = r.In(() => Dm.each<object>(fab.cached_designs).ToList());
        Assert.Contains(typeof(datum_design_borg_suit), offered);
        Assert.Contains(typeof(datum_design_borg_chest), offered);
        Assert.Contains(typeof(datum_design_basic_cell), offered);
        Assert.Contains(typeof(datum_design_mod_shell), offered); // the starting Modular Suits node's shell, live since gaps MO1
        Assert.DoesNotContain(typeof(datum_design_ripley_chassis), offered); // DV384: the mechs are inert
        // a MOD department module (the magboots) is live since gaps MO2; DV384 still hides the circuit module (wiremod)
        r.In(() => Assert.True(obj_machinery_mecha_part_fabricator.offers_design(SSresearch.techweb_designs[typeof(datum_design_module_mod_magboot)] as datum_design)));
        Assert.DoesNotContain(typeof(datum_design_board_mechfab), offered); // a circuit imprinter's design

        // a design the techweb gains reaches the fabricator after its batching timer, and it says so
        var feed = Listen(r);
        r.Join(4, 5, "Bob");
        r.Step(Second);
        r.In(() => web.add_design(typeof(datum_design_cybernetic_heart_tier2)));
        r.Step(3 * Second);
        Assert.Contains(typeof(datum_design_cybernetic_heart_tier2), r.In(() => Dm.each<object>(fab.cached_designs).ToList()));
        Assert.True(feed.Any("Received 1 new design."), "the fabricator announces its new design");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFabricatorPrintsAnEndoskeletonFromItsMaterials()
    {
        var r = Room(9404);
        var fab = Fab(r);
        r.In(() => Waiting.NoSleep(fab.rmat.mat_container.insert_amount_mat(10 * SHEET_MATERIAL_AMOUNT, typeof(datum_material_iron))));
        float iron = r.In(() => Waiting.NoSleep(fab.rmat.mat_container.get_material_amount(typeof(datum_material_iron))));
        Assert.Equal(10 * SHEET_MATERIAL_AMOUNT, iron);

        r.In(() =>
        {
            var suit = SSresearch.techweb_designs[typeof(datum_design_borg_suit)] as datum_design;
            Assert.True(fab.add_to_queue(suit));
            fab.process_queue = TRUE;
            fab.begin_processing();
        });
        r.Step(Second);
        Assert.NotNull(r.In(() => fab.being_built));
        Assert.Equal("It's building Cyborg Endoskeleton.", r.In(() => fab.desc)); // \\a before a proper (capitalised) name is no article
        Assert.Equal(2.5f * SHEET_MATERIAL_AMOUNT, r.In(() => Waiting.NoSleep(fab.rmat.mat_container.get_material_amount(typeof(datum_material_iron)))));

        r.Step(52 * Second);
        Assert.Null(r.In(() => fab.being_built));
        Assert.Contains(r.In(() => Dm.each<atom>(locate(5, 5, 1).contents).ToList()), a => a is obj_item_robot_suit);
        Assert.Equal("Nothing is being built.", r.In(() => fab.desc));

        // not enough iron for a second one: the queue stops
        r.In(() =>
        {
            fab.add_to_queue(SSresearch.techweb_designs[typeof(datum_design_borg_suit)] as datum_design);
            fab.process_queue = TRUE;
            fab.begin_processing();
        });
        r.Step(Second);
        Assert.Null(r.In(() => fab.being_built));
        Assert.False(r.In(() => Dm.truthy(fab.process_queue)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmpFailsTheCyberneticLungsAndTheBestHeartStabilisesItsOwner()
    {
        var r = Room(9405);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.Step(Second);
        var lungs = r.In(() => Dm.@new<obj_item_organ_lungs_cybernetic>());
        r.In(() =>
        {
            lungs.emp_vulnerability = 100;
            Waiting.NoSleep(lungs.Insert(bob, special: TRUE));
        });
        r.In(() => Waiting.NoSleep(lungs.emp_act(EMP_HEAVY)));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(lungs.organ_flags, ORGAN_EMP))), "the lungs start failing");

        var heart = r.In(() => Dm.@new<obj_item_organ_heart_cybernetic_tier3>());
        r.In(() => Waiting.NoSleep(heart.Insert(bob, special: TRUE)));
        Assert.True(r.In(() => Dm.truthy(heart.stabilization_available)));
        r.In(() => heart.stabilize_heart());
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_NOSOFTCRIT)));
        Assert.False(r.In(() => Dm.truthy(heart.stabilization_available)));
        r.Step(11 * Second);
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_NOSOFTCRIT)), "the stabilisation lasts its duration");
        r.Step(5 * 60 * Second);
        Assert.True(r.In(() => Dm.truthy(heart.stabilization_available)), "and comes back after five minutes");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMachinesThatWaitedForResearchUseItsTechwebs()
    {
        var r = Room(9406);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.Step(Second);
        var web = Science(r);
        r.In(() =>
        {
            // the printers' auto-unlocking techwebs: the autolathe's initial designs, the smelter's reinforced glass, the biogenerator's
            var lathe = Dm.@new<obj_machinery_power_manufacturing_lathe>(locate(7, 3, 1));
            Assert.IsType<datum_techweb_autounlocking_autolathe>(lathe.stored_research);
            Assert.True(Dm.truthy(lathe.stored_research.researched_designs[typeof(datum_design_material_rods)]));
            Assert.Same(lathe.stored_research, GLOB.autounlock_techwebs[typeof(datum_techweb_autounlocking_autolathe)]);
            lathe.chosen_design_path = typeof(datum_design_material_rods);
            string text = string.Join("\n", Dm.each<object>(Waiting.NoSleep(lathe.examine(bob)) as DmList).Select(Dm.str));
            Assert.Contains("It is set to print Iron Rod.", text);
            Assert.Contains("0.5 sheets of iron", text);
            // sheets from its input side go into its store
            var sheets = Dm.@new<obj_item_stack_sheet_iron>(locate(7, 4, 1), 5);
            Assert.Equal(MANUFACTURING_SUCCESS, Waiting.NoSleep(lathe.receive_resource(sheets, locate(7, 4, 1), REVERSE_DIR(lathe.dir))));
            Assert.Equal(5 * SHEET_MATERIAL_AMOUNT, Waiting.NoSleep(lathe.materials.get_material_amount(typeof(datum_material_iron))));

            var orm = Dm.@new<obj_machinery_mineral_ore_redemption>(locate(9, 5, 1));
            Assert.IsType<datum_techweb_autounlocking_smelter>(orm.stored_research);
            var data = (DmList)Waiting.NoSleep(orm.ui_data(bob));
            Assert.Contains(Dm.each<DmList>(data["materials"]), m => Dm.eq(m["name"], "Reinforced Glass") && Dm.eq(m["category"], "alloy"));

            var biogenerator = Dm.@new<obj_machinery_biogenerator>(locate(9, 7, 1));
            Assert.IsType<datum_techweb_autounlocking_biogenerator>(biogenerator.stored_research);
            Assert.True(Dm.length(biogenerator.stored_research.researched_designs) > 10);

            // the operating computer and the medbot link to the station's techweb
            var computer = Dm.@new<obj_machinery_computer_operating>(locate(2, 7, 1));
            var medbot = Dm.@new<mob_living_basic_bot_medbot>(locate(4, 7, 1));
        });
        r.Step(Second);
        r.In(() =>
        {
            var computer = Dm.each<obj_machinery_computer_operating>(locate(2, 7, 1).contents).Single();
            Assert.Same(web, computer.linked_techweb);
            var stat = (DmList)Waiting.NoSleep(computer.ui_static_data(bob));
            Assert.Contains(Dm.each<DmList>(stat["techwebs"]), t => Dm.eq(t["web_id"], "SCIENCE"));
            var medbot = Dm.each<mob_living_basic_bot_medbot>(locate(4, 7, 1).contents).Single();
            Assert.Same(web, medbot.linked_techweb);

            // an item's research scan reads the nodes it unlocks and its materials
            var wrench = Dm.@new<obj_item_wrench>(locate(3, 5, 1));
            string scan = Dm.str(wrench.research_scan(bob));
            Assert.Contains("Research prospects:", scan);
            Assert.Contains("Extractable materials:</font> iron.", scan);
        });
        r.AssertNoRuntimes();
    }
}
