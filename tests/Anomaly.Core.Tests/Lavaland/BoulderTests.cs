using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Lavaland;

/// <summary>Lavaland L2c (D304): tg's ore vents and their boulders, the boulder retrieval matrix, the refinery and the smelter.</summary>
public class BoulderTests
{
    private const int Second = 20;

    private static datum_material Mat(Type type) => (datum_material)Waiting.NoSleep(SSmaterials.get_material(type));

    private static obj_item_boulder Boulder(turf at, params (Type Mat, float Amount)[] mats)
    {
        var boulder = Dm.@new<obj_item_boulder>(at);
        DmList list = Dm.list();
        foreach (var (mat, amount) in mats)
            list[mat] = amount;
        Waiting.NoSleep(boulder.set_custom_materials(list));
        return boulder;
    }

    [Fact]
    public void The_starter_vent_makes_boulders_each_fire_and_the_brm_teleports_them_in()
    {
        TgToyRoom r = TgToyRoom.Boot(12, 12);
        r.In(() =>
        {
            var vent = Dm.@new<obj_structure_ore_vent_starter_resources>(locate(3, 3, 1));
            Assert.Equal(TRUE, vent.tapped);
            Assert.Contains(vent, Dm.each<obj_structure_ore_vent>(SSore_generation.processed_vents));
            Assert.DoesNotContain(vent, Dm.each<obj_structure_ore_vent>(SSore_generation.possible_vents)); // a unique vent
            Assert.Equal("ore_vent_active", vent.icon_state);
            Assert.Contains(Dm.each<obj_effect_abstract>(vent.vis_contents), v => v.icon_state == "well"); // the tapped well

            // each fire: the boulders lying on the vent, and a new one
            Waiting.NoSleep(SSore_generation.fire());
            var boulder = Assert.Single(Dm.each<obj_item_boulder>(SSore_generation.available_boulders));
            Assert.Same(locate(3, 3, 1), boulder.loc);
            Assert.Equal(BOULDER_SIZE_SMALL, boulder.boulder_size);
            Assert.EndsWith("_small", boulder.icon_state);
            Assert.InRange(boulder.durability, 2, BOULDER_SIZE_SMALL);
            Assert.All(Dm.each<datum_material>(boulder.custom_materials), m => Assert.True(m.GetType() == typeof(datum_material_iron) || m.GetType() == typeof(datum_material_glass)));
            // three rolls of the vent's minerals, each a whole number of half sheets, boosted by the station's network (x1.33)
            float total = Dm.each<datum_material>(boulder.custom_materials).Sum(m => Dm.num(boulder.custom_materials[m]));
            Assert.True(total >= 3 * HALF_SHEET_MATERIAL_AMOUNT, $"total {total}");

            Waiting.NoSleep(SSore_generation.fire());
            Assert.Equal(2, Dm.each<obj_item_boulder>(SSore_generation.available_boulders).Count());

            // at MAX_BOULDERS_PER_VENT on the vent no more are made
            for (int i = 0; i < MAX_BOULDERS_PER_VENT; i++)
                Dm.@new<obj_item_boulder>(locate(3, 3, 1));
            Waiting.NoSleep(SSore_generation.fire());
            Assert.Equal(12, Dm.each<obj_item_boulder>(SSore_generation.available_boulders).Count());

            // the BRM pulls one in by hand, onto its own tile
            var brm = Dm.@new<obj_machinery_brm>(locate(8, 8, 1));
            var miner = Dm.@new<mob_living_carbon_human_consistent>(locate(8, 7, 1));
            Assert.Equal(TRUE, Waiting.NoSleep(brm.attack_hand(miner)));
            var pulled = Assert.Single(Dm.each<obj_item_boulder>(locate(8, 8, 1)));
            Assert.Equal(11, Dm.each<obj_item_boulder>(SSore_generation.available_boulders).Count());
            Assert.DoesNotContain(pulled, Dm.each<obj_item_boulder>(SSore_generation.available_boulders));
            // a boulder on its tile blocks the next
            brm.manual_teleport_cooldown = 0;
            Assert.Equal(-1f, Dm.num(Waiting.NoSleep(brm.pre_collect_boulder())));

            // right click: automatic retrieval
            Waiting.NoSleep(brm.attack_hand_secondary(miner));
            Assert.Equal(TRUE, brm.toggled_on);
            Assert.Equal("brm-toggled", brm.icon_state);
        });
        Assert.Empty(r.RuntimeText);
    }

    [Fact]
    public void The_smelter_takes_the_metals_and_the_refinery_the_rest_of_a_boulder()
    {
        TgToyRoom r = TgToyRoom.Boot(12, 12);
        r.In(() =>
        {
            var smelter = Dm.@new<obj_machinery_bouldertech_refinery_smelter>(locate(4, 4, 1));
            var refinery = Dm.@new<obj_machinery_bouldertech_refinery>(locate(8, 4, 1));
            var miner = Dm.@new<mob_living_carbon_human_consistent>(locate(4, 5, 1));
            var boulder = Boulder(locate(4, 6, 1), (typeof(datum_material_iron), 20 * SHEET_MATERIAL_AMOUNT), (typeof(datum_material_glass), 10 * SHEET_MATERIAL_AMOUNT));
            Assert.Equal(2f, smelter.boulders_held_max); // two tier-1 matter bins (RefreshParts)

            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(smelter.item_interaction(miner, boulder)));
            Assert.Same(smelter, boulder.loc);
            boulder.durability = 1;
            smelter.process();

            // the iron became sheets (no silo), the glass stays in the boulder, ejected onto the smelter's tile
            Assert.Equal(20f, Dm.each<obj_item_stack_sheet_iron>(locate(4, 4, 1)).Sum(s => Dm.num(s.amount)));
            Assert.Same(locate(4, 4, 1), boulder.loc);
            Assert.Equal(new[] { typeof(datum_material_glass) }, Dm.each<datum_material>(boulder.custom_materials).Select(m => m.GetType()));
            Assert.True(smelter.points_held > 0);
            Assert.Null(boulder.processed_by);

            // the refinery takes the glass and the boulder crumbles
            refinery.accept_cooldown = 0;
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(refinery.item_interaction(miner, boulder)));
            boulder.durability = 0;
            refinery.process();
            Assert.Equal(10f, Dm.each<obj_item_stack_sheet_glass>(locate(8, 4, 1)).Sum(s => Dm.num(s.amount)));
            Assert.True(QDELETED(boulder));
        });
        Assert.Empty(r.RuntimeText);
    }

    [Fact]
    public void A_pickaxe_cracks_a_boulder_into_one_ore_less_than_a_machine_and_a_boulder_floats_as_a_platform()
    {
        TgToyRoom r = TgToyRoom.Boot(12, 12);
        r.In(() =>
        {
            var miner = Dm.@new<mob_living_carbon_human_consistent>(locate(5, 5, 1));
            var boulder = Boulder(locate(5, 6, 1), (typeof(datum_material_iron), 10 * SHEET_MATERIAL_AMOUNT));
            var pickaxe = Dm.@new<obj_item_pickaxe>(locate(5, 5, 1));
            ADD_TRAIT(pickaxe, TRAIT_INSTANTLY_PROCESSES_BOULDERS, "test");
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(boulder.item_interaction(miner, pickaxe)));
            Assert.True(QDELETED(boulder));
            Assert.Equal(9f, Dm.each<obj_item_stack_ore_iron>(locate(5, 6, 1)).Sum(o => Dm.num(o.amount)));

            var rock = Boulder(locate(7, 7, 1), (typeof(datum_material_iron), SHEET_MATERIAL_AMOUNT));
            var platform = Assert.IsType<obj_structure_lattice_catwalk_boulder>(rock.create_platform(locate(7, 8, 1), miner));
            Assert.True(QDELETED(rock));
            Assert.Null(Boulder(locate(7, 7, 1)).create_platform(locate(7, 8, 1), miner)); // one platform a tile
        });
        // the platform sinks after its lifespan (PLATFORM_LIFE_DEFAULT) and the cracks' two seconds
        r.Step((PLATFORM_LIFE_DEFAULT / 10 + 3) * Second);
        r.In(() => Assert.Empty(Dm.each<obj_structure_lattice_catwalk_boulder>(locate(7, 8, 1))));
        Assert.Empty(r.RuntimeText);
    }

    [Fact]
    public void A_scanner_discovers_a_vent_and_the_labour_camp_pit_gives_iron_boulders()
    {
        TgToyRoom r = TgToyRoom.Boot(12, 12);
        obj_structure_ore_vent_random vent = null;
        obj_item_t_scanner_adv_mining_scanner scanner = null;
        mob_living_carbon_human_consistent miner = null;
        obj_structure_gulag_vent pit = null;
        r.In(() =>
        {
            vent = Dm.@new<obj_structure_ore_vent_random>(locate(4, 4, 1));
            // made after the map's load: four lavaland minerals and a random size
            Assert.Equal(4, Dm.each<object>(vent.mineral_breakdown).Count());
            Assert.All(Dm.each<object>(vent.mineral_breakdown), m => Assert.True(Dm.truthy(GLOB.ore_vent_minerals_lavaland[m])));
            Assert.Matches("^(small|medium|large) ore vent$", vent.name);
            Assert.Contains(vent, Dm.each<obj_structure_ore_vent>(SSore_generation.possible_vents));
            Assert.Equal(FALSE, vent.tapped);
            Assert.Equal("ore_vent", vent.icon_state);

            miner = Dm.@new<mob_living_carbon_human_consistent>(locate(4, 5, 1));
            scanner = Dm.@new<obj_item_t_scanner_adv_mining_scanner>(locate(4, 5, 1));
            pit = Dm.@new<obj_structure_gulag_vent>(locate(8, 8, 1));
            Waiting.Detach(vent.item_interaction(miner, scanner));
        });
        r.Step(5 * Second); // the 4-second scan
        r.In(() =>
        {
            Assert.Equal(TRUE, vent.discovered);
            Assert.Contains("This vent was first discovered by", vent.ore_string);
            var made = (obj_item_boulder)Waiting.NoSleep(vent.produce_boulder());
            Assert.All(Dm.each<datum_material>(made.custom_materials), m => Assert.True(Dm.truthy(vent.mineral_breakdown[m.GetType()])));
            Assert.Equal(vent.boulder_size, made.boulder_size);
            // tapped vents are not scanned again
            vent.tapped = TRUE;
            Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(vent.item_interaction(miner, scanner)));

            Waiting.NoSleep(miner.forceMove(locate(8, 7, 1)));
            Waiting.Detach(pit.interact(miner));
        });
        r.Step(9 * Second); // the 8-second haul
        r.In(() =>
        {
            var boulder = Assert.Single(Dm.each<obj_item_boulder_gulag_vent>(locate(8, 7, 1)));
            Assert.Equal(PLATFORM_LIFE_GULAG, boulder.platform_lifespan);
            Assert.Equal(16f * SHEET_MATERIAL_AMOUNT, Dm.num(boulder.custom_materials[Mat(typeof(datum_material_iron))]));
        });
        Assert.Empty(r.RuntimeText);
    }
}
