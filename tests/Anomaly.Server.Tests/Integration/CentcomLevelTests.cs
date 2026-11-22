using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>D299: tg's CentCom map is its own level, and its docks are the real ones.</summary>
public class CentcomLevelTests
{
    [MetaServerFact]
    public void The_shuttles_wait_at_the_docks_of_the_centcom_level()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 71, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        using (w.World.Enter())
        {
            foreach (string id in new[] { "emergency_away", "cargo_away", "ferry_away", "backup_away", "pod_away" })
            {
                var dock = (obj_docking_port_stationary)SSshuttle.getDock(id);
                Assert.True(Dm.truthy(is_centcom_level(dock.z)), id + " stands on the CentCom level");
            }
            Assert.True(Dm.truthy(is_station_level(1f)), "the station stays on z 1 (DV239)");
            Assert.Same(SSshuttle.emergency, ((obj_docking_port_stationary)SSshuttle.getDock("emergency_away")).get_docked());
            Assert.Same(SSshuttle.supply, ((obj_docking_port_stationary)SSshuttle.getDock("cargo_away")).get_docked());
            Assert.NotSame(SSshuttle.emergency, SSshuttle.backup_shuttle);
            Assert.True(Dm.each<area_centcom>(Dm.world).Any(), "tg's CentCom areas are on the map");
        }
    }

    [MetaServerFact]
    public void A_shuttle_leaving_a_dock_marks_its_old_tiles_with_the_flights_direction()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 72, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        const float z = 1f; // it arrives from CentCom directly; it jumps to transit from the station
        bool docked = false;
        object way = null;
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 100;
            SSshuttle.emergency_escape_time = 600;
            SSticker.round_start_time = Dm.num(Dm.world.time) - 12000;
            way = SSshuttle.emergency.preferred_direction;
        }
        alice.Session.DevCommand("shuttle call");
        bool seen = false;
        for (int i = 0; i < 6000 && !seen; i++)
        {
            w.Run(1);
            using (w.World.Enter())
            {
                docked |= SSshuttle.emergency.z == z;
                if (docked && SSshuttle.emergency.z != z) // scanning the world is slow: only once it has jumped (the marks last 9 ds)
                {
                    seen = Dm.each<obj_effect_temp_visual_circle_wave_orange>(Dm.world).Any(m => m.z == z && m.icon == null && Dm.eq(m.dir, way));
                    break;
                }
            }
        }
        Assert.True(seen, "the shuttle's old tiles at the station carry the marks the client draws its streak from");
    }

    [MetaServerFact]
    public void The_engines_are_part_of_the_ship_and_losing_them_slows_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 73, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        using (w.World.Enter())
        {
            obj_docking_port_mobile ship = SSshuttle.emergency;
            float engines = ship.engine_list.len;
            Assert.True(engines > 0, "the map's engines joined the ship");
            float full = ship.initial_engine_power;
            Assert.True(full > 0);
            Assert.Equal(full, ship.current_engine_power);
            Assert.Equal(1f, ship.engine_coeff);
            foreach (obj_machinery_power_shuttle_engine engine in Dm.each<obj_machinery_power_shuttle_engine>(ship.engine_list.Copy()))
                qdel(engine);
            Assert.Equal(0f, ship.engine_list.len);
            Assert.Equal(2f, ship.engine_coeff); // tg: losing them all doubles the time
        }
    }

    [MetaServerFact]
    public void Ripples_fade_in_before_a_shuttle_lands()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 74, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        w.Join("Alice");
        obj_effect_abstract_ripple ripple;
        using (w.World.Enter())
        {
            SSshuttle.supply.create_ripples((obj_docking_port_stationary)SSshuttle.getDock("cargo_home"), 100);
            Assert.True(SSshuttle.supply.ripples.len > 0);
            ripple = (obj_effect_abstract_ripple)SSshuttle.supply.ripples[1];
        }
        w.Run(20);
        using (w.World.Enter())
        {
            Assert.Equal("medi_holo_no_anim", ripple.icon_state); // the fade's timer ran: its Initialize is tg's, no longer inert
            SSshuttle.supply.remove_ripples();
        }
    }
}
