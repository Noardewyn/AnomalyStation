using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.MultiZ;

/// <summary>
/// D325: tg's multi-z debug map (_maps/map_files/debug/multiz.dmm, converted as maps/map_jsons/multiz_debug.json) booted whole:
/// its multi deck pipe adapters (pipes/multiz.dm) and cable hubs (power/multiz.dm) join their networks across the decks.
/// </summary>
public class MultizDebugMapTests
{
    private static string Folder()
    {
        string folder = MetaMaps.Folder();
        return folder != null && File.Exists(Path.Combine(folder, "map_jsons", "multiz_debug.json")) ? folder : null;
    }

    private static World Boot(string folder, out TgTestWorld.Log log)
    {
        return TgTestWorld.Boot(out log, configure: w => MapSeam.Select(w, new MapSelection(folder, "multiz_debug")), compiledMap: TgWorld.CompiledMap, maxTicks: 400);
    }

    // what stands on the debug map's three decks (CentCom's and Lavaland's levels hold disposal bins and the rest too)
    private static List<T> All<T>() where T : atom
    {
        var found = new List<T>();
        for (int z = 1; z <= Dm.num(world.maxz); z++)
        {
            if (!Dm.truthy(is_station_level(z)))
                continue;
            for (int y = 1; y <= Dm.num(world.maxy); y++)
                for (int x = 1; x <= Dm.num(world.maxx); x++)
                    foreach (T a in Dm.each<T>(locate(x, y, z)))
                        found.Add(a);
        }
        return found;
    }

    private static datum_transport_controller_linear TestVator()
    {
        DmList lifts = (DmList)SStransport.transports_by_type[TRANSPORT_TYPE_ELEVATOR];
        Assert.NotNull(lifts);
        foreach (datum_transport_controller_linear lift in Dm.each<datum_transport_controller_linear>(lifts))
        {
            if (Dm.eq(lift.specific_transport_id, "test_vator"))
                return lift;
        }
        throw new Xunit.Sdk.XunitException("no test_vator elevator");
    }

    private static List<obj_structure_transport_linear> Platforms(datum_transport_controller_linear lift)
    {
        var platforms = new List<obj_structure_transport_linear>();
        foreach (obj_structure_transport_linear p in Dm.each<obj_structure_transport_linear>(lift.transport_modules))
            platforms.Add(p);
        return platforms;
    }

    [Fact]
    public void TheElevatorLinksItsPlatformsPanelsButtonsAndDoorsAtBoot()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = Boot(folder, out TgTestWorld.Log log);
        world.Step(20);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            datum_transport_controller_linear lift = TestVator();
            List<obj_structure_transport_linear> platforms = Platforms(lift);
            Assert.Equal(9, platforms.Count); // a 3 x 3 car of public platforms, one level
            Assert.Single(platforms.Select(p => p.z).Distinct());
            int named = 0;
            foreach (obj_machinery_elevator_control_panel panel in All<obj_machinery_elevator_control_panel>())
            {
                Assert.Same(lift, panel.lift_weakref?.resolve());
                Assert.Equal(3, panel.linked_elevator_destination.len); // the shaft runs through the three decks, roofed over the top one (DV436)
                // DV239: the map's names are keyed by tg's z (its station starts at 2), top floor first (the owner, 2026-11-13: the middle and top were swapped)
                var names = Dm.each(panel.linked_elevator_destination).Select(z => $"{z}={panel.linked_elevator_destination[z]}").ToList();
                // a panel without the names says "Floor [z - 1]" of tg's z: 6 for the bottom deck over the five space levels (DV436)
                Assert.Equal((panel.preset_destination_names != null ? new[] { "8=Top Floor", "7=Middle Floor", "6=Bottom Floor" } : new[] { "8=Floor 8", "7=Floor 7", "6=Floor 6" }), names);
                named += panel.preset_destination_names != null ? 1 : 0;
            }
            Assert.Equal(1, named);
            foreach (obj_machinery_button_elevator button in All<obj_machinery_button_elevator>())
                Assert.Same(lift, ((obj_item_assembly_control_elevator)button.device_instance).lift_weakref?.resolve());
            Assert.NotEmpty(GLOB.elevator_doors.Items());
            Assert.Contains(All<obj_machinery_lift_indicator>(), i => i.lift_ref?.resolve() == lift);
        }
    }

    [Fact]
    public void ACallButtonOnAnotherLevelBringsTheCarWithItsRiderAndOpensTheDoorsThere()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = Boot(folder, out TgTestWorld.Log log);
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME);
        world.Step(20);
        datum_transport_controller_linear lift;
        obj_machinery_button_elevator button;
        mob_living_carbon_human rider;
        float startZ;
        using (world.Enter())
        {
            lift = TestVator();
            startZ = Platforms(lift)[0].z;
            button = All<obj_machinery_button_elevator>().First(b => b.z != startZ);
            obj_structure_transport_linear seat = Platforms(lift)[4];
            rider = Dm.@new<mob_living_carbon_human>(seat.loc);
        }
        mob_living_carbon_human caller;
        using (world.Enter())
            caller = Dm.@new<mob_living_carbon_human>(button.loc);
        world.Step(2);
        // a hand's click on the call button (the owner's report, 2026-11-09: the call did nothing in the game) calls the car
        using (world.Enter())
            Waiting.Detach(caller.ClickOn(button));
        // the indicators show the way the car goes while it moves (COMSIG_LIFT_SET_DIRECTION)
        world.Step(20 * 3);
        using (world.Enter())
            Assert.Contains(All<obj_machinery_lift_indicator>(), i => i.lift_ref?.resolve() == lift && Dm.eq(i.current_lift_direction, button.z > startZ ? UP : DOWN));
        // the doors close and 1.1 s pass, then 3 s and a tick per level (move_to_zlevel), the doors open 0.7 s after arriving
        world.Step(20 * 9);
        using (world.Enter())
        {
            List<obj_structure_transport_linear> platforms = Platforms(lift);
            Assert.All(platforms, p => Assert.Equal(button.z, p.z));
            Assert.Equal(button.z, rider.z); // group_move carried the rider
            Assert.Contains(platforms, p => p.loc == rider.loc);
            Assert.Equal(0f, Dm.num(Dm.bitand(lift.controller_status, CONTROLS_LOCKED))); // the controls unlock on arrival
            // DV239: the hall lanterns number the floors from 1 at the bottom deck, as tg's do from its z 2
            Assert.All(All<obj_machinery_lift_indicator>().Where(i => i.lift_ref?.resolve() == lift), i => Assert.Equal(button.z - 5, Dm.num(i.current_lift_floor)));
            foreach (obj_machinery_door door in Dm.each<obj_machinery_door>(GLOB.elevator_doors))
            {
                if (Dm.eq(door.z, button.z))
                    Assert.Equal(0f, Dm.num(door.density)); // open where the car stands
                else
                    Assert.Equal(1f, Dm.num(door.density)); // shut on the other levels
            }
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void ABinFlushesThroughTheMultiZTrunksIntoTheBinOnTheOtherDeck()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = Boot(folder, out TgTestWorld.Log log);
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME);
        world.Step(20);
        List<obj_machinery_disposal_bin> bins;
        obj_item_wrench wrench;
        using (world.Enter())
        {
            bins = All<obj_machinery_disposal_bin>();
            Assert.Equal(2, bins.Count);
            Assert.All(bins, b => Assert.NotNull(b.trunk));
            Assert.NotEqual(bins[0].z, bins[1].z);
            wrench = Dm.@new<obj_item_wrench>(bins[0]);
            Waiting.Detach(bins[0].flush__proc());
        }
        world.Step(200);
        using (world.Enter())
        {
            // the other deck's bin expels it (trunk.transfer to the linked bin: its expel throws the holder's contents)
            Assert.IsAssignableFrom<turf>(wrench.loc);
            Assert.Equal(bins[1].z, wrench.z);
            Assert.Empty(log.Runtimes);
        }
    }

    [Fact]
    public void TheMultiDeckPipeAdaptersAndCableHubsJoinTheirNetworksAcrossTheDecks()
    {
        string folder = Folder();
        if (folder == null)
            return; // the converted maps are not here (tools/convert-maps.sh)
        World world = Boot(folder, out TgTestWorld.Log log);
        world.Step(20);
        using (world.Enter())
        {
            int linkedPipes = 0, linkedHubs = 0;
            List<obj_machinery_atmospherics_pipe_multiz> adapters = All<obj_machinery_atmospherics_pipe_multiz>();
            Assert.NotEmpty(adapters);
            foreach (obj_machinery_atmospherics_pipe_multiz low in adapters)
            {
                object above = GET_TURF_ABOVE(get_turf(low));
                foreach (obj_machinery_atmospherics_pipe_multiz high in Dm.each<obj_machinery_atmospherics_pipe_multiz>(above))
                {
                    if (!Dm.eq(high.piping_layer, low.piping_layer))
                        continue;
                    Assert.NotNull(low.parent);
                    Assert.Same(low.parent, high.parent); // one pipeline over both decks
                    linkedPipes++;
                }
            }
            Assert.True(linkedPipes > 0, "no adapter pair over two decks on the map");

            List<obj_structure_cable_multilayer_multiz> hubs = All<obj_structure_cable_multilayer_multiz>();
            Assert.NotEmpty(hubs);
            foreach (obj_structure_cable_multilayer_multiz low in hubs)
            {
                obj_structure_cable_multilayer_multiz high = Dm.locate<obj_structure_cable_multilayer_multiz>(GET_TURF_ABOVE(get_turf(low)));
                if (high == null)
                    continue;
                Assert.NotNull(low.powernet);
                Assert.Same(low.powernet, high.powernet); // one powernet over both decks
                linkedHubs++;
            }
            Assert.True(linkedHubs > 0, "no hub pair over two decks on the map");
        }
    }
    // DV395 (the owner, 2026-11-10): an explosion hurt only its own level and a singularity ate only its own; both now reach the linked levels, each a tile away
    [Fact]
    public void AnExplosionAndASingularityReachTheLinkedLevels()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = Boot(folder, out TgTestWorld.Log log);
        world.Step(20);
        using (world.Enter())
        {
            turf middle = locate(25, 23, 7) as turf;
            var blast = ((DmList)prepare_explosion_turfs(2, middle)).Items().ToList();
            Assert.Contains(locate(25, 23, 6), blast);
            Assert.Contains(locate(25, 23, 8), blast);
            Assert.Contains(locate(27, 25, 8), blast); // a corner of the second shell on the level above
            Assert.DoesNotContain(locate(28, 23, 7), blast);
            // nearest first: the turf over the epicenter is in the first shell, before the second ring of its own level
            Assert.True(blast.IndexOf(locate(25, 23, 8)) < blast.IndexOf(locate(27, 23, 7)));
            var bottom = ((DmList)prepare_explosion_turfs(1, locate(25, 23, 6) as turf)).Items().ToList();
            Assert.Contains(locate(24, 22, 7), bottom);
            Assert.DoesNotContain(locate(25, 23, 8), bottom); // two levels up is past a range of 1

            var singularity = Dm.@new<obj_singularity>(middle);
            var component = (datum_component_singularity)singularity.singularity_component.resolve();
            component.turfs_to_consume?.Cut();
            component.eat();
            var eaten = component.turfs_to_consume.Items().ToList();
            Assert.Contains(locate(25, 23, 6), eaten);
            Assert.Contains(locate(25, 23, 8), eaten);
            Assert.Empty(log.Runtimes);
        }
    }
    // the owner (2026-11-10): flying (the dev fly), tg's Move Upwards and Downwards keys take a mob a level up or down where it can fly (open space over it);
    // with noclip it passes the floors as tg's ghost does
    [Fact]
    public void AFlyerMovesUpThroughOpenSpaceAndANoclipperThroughTheFloor()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = Boot(folder, out TgTestWorld.Log log);
        world.Step(20);
        mob_living_carbon_human flyer, ghostly, grounded;
        using (world.Enter())
        {
            flyer = Dm.@new<mob_living_carbon_human>(locate(35, 17, 6)); // the room under the atrium: open space over it
            grounded = Dm.@new<mob_living_carbon_human>(locate(25, 23, 6)); // a floor over it
            ghostly = Dm.@new<mob_living_carbon_human>(locate(24, 23, 6));
            DevCommands.Fly(flyer);
            DevCommands.Fly(grounded);
            DevCommands.NoClip(ghostly);
            DevCommands.Level(flyer, up: true);
            DevCommands.Level(grounded, up: true);
            DevCommands.Level(ghostly, up: true);
        }
        world.Step(40); // tg's do_after of a second
        using (world.Enter())
        {
            Assert.Equal(7f, flyer.z);
            Assert.Equal(6f, grounded.z); // the floor over it stops a flyer
            Assert.Equal(7f, ghostly.z);
            DevCommands.Level(ghostly, up: false);
        }
        world.Step(5);
        using (world.Enter())
            Assert.Equal(6f, ghostly.z);
    }

    // DV436 (the owner, 2026-11-14): five space levels under the decks and five over them, a flyer goes down from the bottom deck's space into them
    [Fact]
    public void FiveSpaceLevelsLieUnderAndOverTheDecks()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = Boot(folder, out TgTestWorld.Log log);
        world.Step(20);
        mob_living_carbon_human flyer;
        using (world.Enter())
        {
            Assert.IsType<turf_open_space_basic>(locate(1, 1, 1));
            Assert.IsType<turf_open_space_openspace>(locate(1, 1, 2));
            Assert.IsType<turf_open_space_openspace>(locate(43, 20, 6)); // the bottom deck's space outside its east hull
            Assert.IsType<turf_open_space_openspace>(locate(1, 1, 13));
            flyer = Dm.@new<mob_living_carbon_human>(locate(43, 20, 6));
            DevCommands.Fly(flyer);
            DevCommands.Level(flyer, up: false);
        }
        world.Step(40);
        using (world.Enter())
        {
            Assert.Equal(5f, flyer.z);
            Assert.Empty(log.Runtimes);
        }
    }

    // The play test of 2026-11-14: CentCom (255 x 255), loaded after this 52 x 52 station (DV239), was centred at a negative offset
    // and cropped, its docks with it, and with seed 8260116172687697000 Lavaland's rivers ran over a colossus's turf (CHANGETURF_SKIP),
    // which the cave generator's population still named. The map has no emergency_home: a called emergency shuttle's checks found no
    // dock to ripple and ran into ripple_area's null on every check.
    [Fact]
    public void CentComAndLavalandLoadWholeAndACalledShuttleFindsNoDockQuietly()
    {
        string folder = Folder();
        if (folder == null)
            return;
        World world = TgTestWorld.Boot(out TgTestWorld.Log log, seed: 8260116172687697000, configure: w => MapSeam.Select(w, new MapSelection(folder, "multiz_debug")), compiledMap: TgWorld.CompiledMap, maxTicks: 400);
        world.Step(20);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            Assert.Equal(255f, Dm.world.maxx);
            string Level(string dock) => ((datum_space_level)SSmapping.z_list[((atom)SSshuttle.getDock(dock)).z]).name;
            Assert.All(new[] { "emergency_away", "backup_away", "ferry_away", "pod_away" }, dock => Assert.Equal("CentCom", Level(dock)));
            Assert.All(new[] { "mining_away", "laborcamp_away", "lavaland_common_away" }, dock => Assert.Equal("Lavaland", Level(dock)));

            obj_docking_port_mobile_emergency emergency = SSshuttle.emergency;
            emergency.mode = SHUTTLE_CALL;
            emergency.setTimer(SHUTTLE_RIPPLE_TIME / 2);
        }
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME); // SSshuttle fires in the round
        world.Step(8 * 20); // past the ripple time and the arrival, which finds no dock and waits again (tg's setTimer(20))
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            Assert.Equal(SHUTTLE_CALL, SSshuttle.emergency.mode);
        }
    }
}
