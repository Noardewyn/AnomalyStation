using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>D304: tg's Lavaland map is its own level, and the mining and labour shuttles' docks are the real ones (DV236 retired).</summary>
public class LavalandLevelTests
{
    [MetaServerFact]
    public void The_mining_docks_stand_on_the_lavaland_level()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 73, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        int mining;
        using (w.World.Enter())
        {
            foreach (string id in new[] { "mining_away", "laborcamp_away", "lavaland_common_away" })
            {
                var dock = (obj_docking_port_stationary)SSshuttle.getDock(id);
                Assert.True(Dm.truthy(is_mining_level(dock.z)), id + " stands on the mining level");
            }
            Assert.True(Dm.truthy(is_station_level(1f)), "the station stays on z 1");
            float z = ((obj_docking_port_stationary)SSshuttle.getDock("mining_away")).z;
            Assert.True(Dm.truthy(SSmapping.level_trait(z, ZTRAIT_ASHSTORM)), "tg's ZTRAITS_LAVALAND");
            Assert.True(Dm.each<area_lavaland>(Dm.world).Any(), "tg's Lavaland areas are on the map");

            // the cave generator and the rivers ran over the unexplored areas
            int genturfs = 0, open = 0, rock = 0, lava = 0, ore = 0, gibtonite = 0, unmeasured = 0;
            for (int x = 1; x <= Dm.world.maxx; x++)
                for (int y = 1; y <= Dm.world.maxy; y++)
                {
                    turf t = locate(x, y, z);
                    if (t is turf_open_genturf) genturfs++;
                    else if (t is turf_open_misc_asteroid_basalt) open++;
                    else if (t is turf_closed_mineral m)
                    {
                        rock++;
                        if (Dm.truthy(m.mineral_type)) ore++;
                        if (m is turf_closed_mineral_gibtonite) gibtonite++;
                        if (m is turf_closed_mineral_random && m.open_turf_distance == -1) unmeasured++;
                    }
                    else if (t is turf_open_lava) lava++;
                }
            Assert.Equal(0, genturfs);
            Assert.True(open > 5000, "cave floors: " + open);
            Assert.True(rock > 5000, "cave walls: " + rock);
            Assert.True(lava > 7537 + 100, "the mapped lava and the rivers: " + lava);

            // SSore_generation measured each random rock's depth from the caves and filled the veins (tg's ~7 percent of the rock)
            Assert.True(ore > rock / 50, "ore rocks: " + ore + " of " + rock);
            Assert.True(gibtonite > 0, "gibtonite deposits");
            Assert.True(unmeasured < rock / 10, "random rocks with no measured depth: " + unmeasured);
            var total = (DmList)SSore_generation.ore_spread_probabilities["total"];
            Assert.True(Dm.num(total["count"]) > 1000, "the rocks randomized by depth: " + total["count"]);
            // the boss arenas: the stone tiles pick their sprite and keep lava and chasms off their turf; the necropolis walls and
            // Lavaland's water (smoothed with the basalt around it) are live
            var tiles = Dm.each<obj_structure_stone_tile>(Dm.world).Where(t => Dm.num(t.z) == z).ToList();
            Assert.True(tiles.Count > 500, "stone tiles: " + tiles.Count);
            Assert.All(tiles, t => Assert.Matches("^(pristine|cracked)_[a-z_]+[0-9]+$", t.icon_state));
            Assert.All(tiles, t => Assert.True(HAS_TRAIT(t.loc, TRAIT_LAVA_STOPPED) && HAS_TRAIT(t.loc, TRAIT_CHASM_STOPPED)));
            Assert.False(InertSeam.IsInert(Dm.each<turf_closed_indestructible_riveted_boss>(Dm.world).First()));
            var waters = Dm.each<turf_open_water_lavaland_atmos_basalt>(Dm.world).ToList();
            Assert.NotEmpty(waters);
            Assert.Contains(waters, t => t.basalt_junction != ALL_SMOOTHING_JUNCTIONS); // a shore draws the basalt outline
            Assert.DoesNotContain("ore_generation.dm", w.Err.ToString());
            mining = (int)z;
        }
        // D305: the join map names Lavaland's level, tg's ZTRAIT_NOPARALLAX, so the client draws its own sky there
        Assert.Equal(new[] { mining }, w.Replicator.ReadMapGrid().NoParallaxLevels);
        foreach (string file in new[] { "minerals.dm", "necropolis_gate.dm", "water.dm" })
            Assert.DoesNotContain(file, w.Err.ToString());
    }

    [MetaServerFact]
    public void Metas_ore_silo_is_the_stations_and_its_redemption_machine_links_to_it_at_round_start()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 76, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        w.Join("Alice");
        w.Run(20);
        using (w.World.Enter())
        {
            obj_machinery_ore_silo silo = GLOB.ore_silo_default;
            Assert.NotNull(silo);
            Assert.Equal(1f, silo.ID_required); // tg: only the station's silo asks for an ID
            var orm = Dm.each<obj_machinery_mineral_ore_redemption>(Dm.world).Single(m => Dm.truthy(is_station_level(m.z)));
            Assert.Same(silo, orm.materials.silo);
            Assert.Same(silo.materials, orm.materials.mat_container);
            Assert.True(Dm.each<obj_structure_ore_box>(Dm.world).Any(), "Lavaland's ore boxes");
        }
        foreach (string file in new[] { "machine_silo.dm", "machine_redemption.dm", "remote_materials.dm", "material_container.dm", "satchel_ore_box.dm" })
            Assert.DoesNotContain(file, w.Err.ToString());
    }

    [MetaServerFact]
    public void The_mapped_stacking_furnace_and_labour_machines_find_their_partners_at_round_start()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 77, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        using (w.World.Enter())
        {
            // Meta's cargo stacker and the labour shuttle's collection unit each have the console of their area
            var consoles = Dm.each<obj_machinery_mineral_stacking_unit_console>(Dm.world).ToList();
            Assert.Equal(2, consoles.Count);
            Assert.All(consoles, c => Assert.Same(c, c.machine.console));
            Assert.Single(consoles, c => c.machine is obj_machinery_mineral_stacking_machine_laborstacker);

            // the labour shuttle's collection unit and its claim console, which watches the shuttle's launch
            var claim = Dm.each<obj_machinery_mineral_labor_claim_console>(Dm.world).Single();
            Assert.Same(claim, claim.stacking_machine.labor_console);
            var laborShuttle = (obj_docking_port_mobile)SSshuttle.getShuttle("laborcamp");
            Assert.True(Dm.truthy(laborShuttle._listen_lookup?[COMSIG_SHUTTLE_SHOULD_MOVE]), "the claim console listens to the labour shuttle");

            // the camp's two furnaces have their consoles, and the unloading machines stand on Lavaland
            var furnaces = Dm.each<obj_machinery_mineral_processing_unit>(Dm.world).ToList();
            Assert.Equal(2, furnaces.Count);
            Assert.All(furnaces, f => Assert.IsType<obj_machinery_mineral_processing_unit_console>(f.mineral_machine));
            Assert.All(furnaces, f => Assert.True(Dm.truthy(is_mining_level(f.z))));
            Assert.Equal(2, Dm.each<obj_machinery_mineral_unloading_machine>(Dm.world).Count());
            Assert.Single(Dm.each<obj_machinery_mineral_labor_points_checker>(Dm.world));
        }
        foreach (string file in new[] { "machine_stacking.dm", "machine_processing.dm", "machine_unloading.dm", "laborstacker.dm" })
            Assert.DoesNotContain(file, w.Err.ToString());
    }

    [MetaServerFact]
    public void The_starter_vent_feeds_boulders_to_the_station_brm_and_the_bouldertech_machines_use_the_silo()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 78, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        using (w.World.Enter())
        {
            // Lavaland's tapped iron and glass vent stands on the mining epicenter
            var starter = Dm.each<obj_structure_ore_vent_starter_resources>(Dm.world).Single();
            Assert.Equal(1f, starter.tapped);
            Assert.Contains(starter, Dm.each<obj_structure_ore_vent>(SSore_generation.processed_vents));
            Assert.Equal(new[] { starter.loc }, Dm.each<turf>(GLOB.mining_center).ToArray());
            Assert.True(Dm.truthy(is_mining_level(starter.z)));
            // the biomes' boss vents are unique: no size or minerals from the subsystem
            Assert.All(Dm.each<obj_structure_ore_vent_boss>(Dm.world), v => Assert.DoesNotContain(v, Dm.each<obj_structure_ore_vent>(SSore_generation.possible_vents)));
            Assert.Single(Dm.each<obj_structure_gulag_vent>(Dm.world));

            // Meta's BRM, refinery and smelter; the two refiners are linked to the station's silo
            var brm = Dm.each<obj_machinery_brm>(Dm.world).Single();
            var refiners = Dm.each<obj_machinery_bouldertech>(Dm.world).ToList();
            Assert.Equal(2, refiners.Count);
            Assert.All(refiners, m => Assert.Same(GLOB.ore_silo_default, m.silo_materials.silo));

            // a fire of the subsystem makes a boulder on the vent, which the BRM teleports to the station
            Waiting.NoSleep(SSore_generation.fire());
            Assert.NotEmpty(Dm.each<obj_item_boulder>(SSore_generation.available_boulders));
            brm.manual_teleport_cooldown = 0;
            Assert.Equal(1f, Waiting.NoSleep(brm.pre_collect_boulder()));
            Assert.Single(Dm.each<obj_item_boulder>(brm.loc));
        }
        foreach (string file in new[] { "ore_vent.dm", "boss_vent.dm", "gulag_vent.dm", "vent_misc.dm", "boulder.dm", "_boulder_processing.dm", "refinery.dm", "brm.dm", "ore_generation.dm" })
            Assert.DoesNotContain(file, w.Err.ToString());
    }

    [MetaServerFact]
    public void A_lavaland_chasm_swallows_what_steps_in_unless_a_catwalk_spans_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 75, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(5);
        mob_living me = null;
        obj_item_crowbar crowbar = null;
        turf_open_chasm open = null, spanned = null;
        using (w.World.Enter())
        {
            me = w.MobOf(alice);
            var dock = (obj_docking_port_stationary)SSshuttle.getDock("mining_away");
            // two basalt tiles beside the mining dock become chasms, one with a catwalk over it
            turf a = locate(dock.x - 6, dock.y + 12, dock.z), b = locate(dock.x - 6, dock.y + 14, dock.z);
            open = Waiting.NoSleep(a.ChangeTurf(typeof(turf_open_chasm_lavaland))) as turf_open_chasm;
            spanned = Waiting.NoSleep(b.ChangeTurf(typeof(turf_open_chasm_lavaland))) as turf_open_chasm;
            Assert.NotNull(open);
            Assert.NotNull(open.GetComponent(typeof(datum_component_chasm)));
            Assert.False(InertSeam.IsInert(open));
            Dm.@new<obj_structure_lattice_catwalk>(spanned);
            Waiting.NoSleep(me.forceMove(spanned));
        }
        w.Run(20);
        using (w.World.Enter())
        {
            Assert.Same(spanned, me.loc);
            crowbar = Dm.@new<obj_item_crowbar>(spanned);
            Waiting.NoSleep(crowbar.forceMove(open));
            Waiting.NoSleep(me.forceMove(open));
        }
        w.Run(40);
        using (w.World.Enter())
        {
            Assert.IsType<obj_effect_abstract_chasm_storage>(crowbar.loc);
            Assert.IsType<obj_effect_abstract_chasm_storage>(me.loc);
            Assert.Equal(DEAD, me.stat);
        }
    }

    [MetaServerFact]
    public void Stepping_into_lava_sets_you_alight_and_a_heatproof_catwalk_makes_it_safe()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 74, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(5);
        turf_open_lava lava = null, safe = null;
        mob_living me = null;
        using (w.World.Enter())
        {
            me = w.MobOf(alice);
            float z = ((obj_docking_port_stationary)SSshuttle.getDock("mining_away")).z;
            // two lava tiles in the middle of lava, so the edge light and the neighbours do not matter
            for (int x = 2; x < Dm.world.maxx - 1 && safe == null; x++)
                for (int y = 2; y < Dm.world.maxy - 1 && safe == null; y++)
                    if (locate(x, y, z) is turf_open_lava t && Dm.each<turf>(RANGE_TURFS(1, t)).All(n => n is turf_open_lava))
                    {
                        if (lava == null) lava = t;
                        else if (get_dist(t, lava) > 3) safe = t;
                    }
            Assert.NotNull(safe);
            Assert.False(Dm.truthy(lava.light_on), "a lava tile inside lava casts no light; only the edge ones do");
            Dm.@new<obj_structure_lattice_catwalk_lava>(safe);
            Waiting.NoSleep(me.forceMove(lava));
        }
        w.Run(10);
        float burnInLava;
        using (w.World.Enter())
        {
            burnInLava = Dm.num(me.get_fire_loss());
            Assert.True(burnInLava > 0, "lava burns");
            Assert.True(Dm.truthy((me.has_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks)) as datum_status_effect_fire_handler_fire_stacks)?.on_fire), "and sets you alight");
            Waiting.NoSleep(me.fully_heal());
            me.extinguish_mob();
            Waiting.NoSleep(me.forceMove(safe));
        }
        w.Run(10);
        using (w.World.Enter())
            Assert.Equal(0f, Dm.num(me.get_fire_loss()));
    }
}
