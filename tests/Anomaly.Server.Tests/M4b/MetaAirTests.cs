using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Server.Tests.Integration;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.M4b;

/// <summary>
/// M4b slice 2 (the pipenets): on the real server's Meta, SSair builds tg's pipenets at init and the station's air
/// machinery (the air mix tank's output, the distro pumps, the air vents and the scrubbers) moves gas through them.
/// </summary>
public class MetaAirTests
{
    private readonly ITestOutputHelper _output;

    public MetaAirTests(ITestOutputHelper output) => _output = output;

    [MetaServerFact]
    public void Metas_pipenets_build_and_the_distro_fills()
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.SettleAir(world);
        world.Run(2);
        Stats boot = Report(world, "boot");
        Assert.True(boot.Networks > 100, "pipenets " + boot.Networks); // measured 122
        Assert.True(boot.Pipes > 5000, "live pipes " + boot.Pipes);
        Assert.Equal(0, boot.OrphanPipes); // setup_pipenets gives every pipe a pipeline
        Assert.True(boot.Vents > 250 && boot.VentsWithoutNode == 0, $"vents {boot.Vents}, without a node {boot.VentsWithoutNode}");
        Assert.True(boot.Scrubbers > 200, "scrubbers " + boot.Scrubbers);
        Assert.Equal(0f, boot.DistroMedian); // the distro starts empty (tg: pipes have no air of their own at roundstart)

        world.Run(1200);
        Stats minute = Report(world, "60 s");
        // The air mix tank's output siphon feeds "Air to Distro" (a pressure pump at tg's default target, ONE_ATMOSPHERE),
        // so the distro the vents hang on reaches one atmosphere within a minute (measured 101.2 kPa).
        Assert.InRange(minute.DistroMedian, 95f, 103f);
        Assert.Equal(boot.Networks, minute.Networks);
        Assert.Equal(0, minute.RebuildQueue);

        // The air alarms watch their rooms: at a settled minute almost all read no danger (measured 0)
        using (world.World.Enter())
        {
            var alarms = Dm.each_cast<obj_machinery_airalarm>(GlobalProcs.GLOB.air_alarms).ToList();
            var danger = alarms.Where(a => a.danger_level != AIR_ALARM_ALERT_NONE).ToList();
            _output.WriteLine($"air alarms {alarms.Count}, in danger {danger.Count}: " + string.Join(", ", danger.Take(10).Select(a => $"{a.name} {a.danger_level} ({a.x},{a.y})")));
            Assert.True(alarms.Count > 100, "air alarms " + alarms.Count); // measured 198
            Assert.True(danger.Count <= 5, "air alarms in danger at a settled minute: " + danger.Count);
        }
    }

    [MetaServerFact]
    public void A_breach_next_to_a_firelock_closes_its_row()
    {
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.SettleAir(world);
        world.Run(2);
        obj_machinery_door_firedoor firelock = null;
        turf hole = null;
        int firelocks = 0, closedAtStart = 0;
        using (world.World.Enter())
        {
            for (int y = 1; y <= MapView.Height(world.World); y++)
            for (int x = 1; x <= MapView.Width(world.World); x++)
            foreach (atom a in Dm.each_cast<atom>(MapView.TurfAt(world.World, x, y, 1).contents))
            {
                if (a is not obj_machinery_door_firedoor f || InertSeam.IsInert(f))
                    continue;
                firelocks++;
                if (Dm.truthy(f.density)) closedAtStart++;
                if (firelock != null)
                    continue;
                // a firelock with a plain floor two tiles east (the breach) and one tile east (its checked neighbour)
                turf east1 = Builtins.get_step(f, EAST), east2 = Builtins.get_step(east1, EAST);
                if (east1 is turf_open_floor && east2 is turf_open_floor && Dm.truthy(((turf)Builtins.get_step(f, WEST)) is turf_open_floor))
                {
                    firelock = f;
                    hole = east2;
                }
            }
            _output.WriteLine($"firelocks {firelocks}, closed at roundstart {closedAtStart}; testing the one at {firelock?.x},{firelock?.y}");
            Assert.True(firelocks > 300, "live firelocks " + firelocks);
            Assert.NotNull(firelock);
            _output.WriteLine(DevCommands.Breach(hole));
        }
        bool closed = false;
        for (int s2 = 0; s2 < 30 && !closed; s2++)
        {
            world.Run(20);
            using (world.World.Enter())
                closed = Dm.truthy(firelock.density);
        }
        using (world.World.Enter())
        {
            float t = ((turf_open)Builtins.get_step(firelock, EAST)).air.temperature;
            _output.WriteLine($"closed {closed}, alarm {firelock.alarm_type}, east neighbour {t:0.#} K");
        }
        Assert.True(closed, "the breach's cold air closes the firelock");
    }

    [MetaServerFact]
    public void A_deleted_outer_wall_vents_the_room_into_space()
    {
        // the owner's play test (2026-10-13): a wall with space behind it, deleted with the dev tool (tg's admin delete: the
        // wall's ScrapeAway to its plating), must let the room's air out as a removed floor does
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.SettleAir(world);
        world.Run(2);
        int wx = 0, wy = 0;
        using (world.World.Enter())
        {
            // a reinforced outer wall (most of Meta's hull) with space west of it and a room's connected floor east
            for (int y = 2; y < MapView.Height(world.World) && wx == 0; y++)
            for (int x = 2; x < MapView.Width(world.World) && wx == 0; x++)
            {
                if (MapView.TurfAt(world.World, x, y, 1) is not turf_closed_wall_r_wall) continue;
                turf w = MapView.TurfAt(world.World, x - 1, y, 1), e = MapView.TurfAt(world.World, x + 1, y, 1);
                if (w is turf_open_space && e is turf_open_floor f && Dm.num(f.air.return_pressure()) > 90f && Dm.length(e.atmos_adjacent_turfs) >= 2)
                    (wx, wy) = (x, y);
            }
            Assert.NotEqual(0, wx);
            _output.WriteLine(DevCommands.Delete(MapView.TurfAt(world.World, wx, wy, 1)));
            Assert.IsType<turf_open_floor_plating>(MapView.TurfAt(world.World, wx, wy, 1));
        }
        world.Run(40);
        using (world.World.Enter())
        {
            float hole = Dm.num(((turf_open)MapView.TurfAt(world.World, wx, wy, 1)).air.return_pressure());
            float inside = Dm.num(((turf_open)MapView.TurfAt(world.World, wx + 1, wy, 1)).air.return_pressure());
            _output.WriteLine($"after 2 s: the hole {hole:0.#} kPa, the floor inside {inside:0.#} kPa"); // measured 0 and 39.5
            Assert.True(hole < 5f && inside < 70f, $"the room vents through the hole: {hole}, {inside}");
        }
    }

    [MetaServerFact]
    public void A_vent_whose_pipe_is_deleted_turns_off_and_shows_it()
    {
        // the owner's play test (2026-10-14): the pipe feeding a room's vent, deleted with the dev tool. tg's vent_pump
        // process_atmos turns a vent without a node off (vent_pump.dm:245), and update_icon_nopipes shows vent_off (D142)
        using var world = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        MetaServer.SettleAir(world);
        world.Run(2);
        obj_machinery_atmospherics_components_unary_vent_pump vent = null;
        using (world.World.Enter())
        {
            for (int y = 1; y <= MapView.Height(world.World) && vent == null; y++)
            for (int x = 1; x <= MapView.Width(world.World) && vent == null; x++)
            foreach (var v in Dm.each_cast<atom>(MapView.TurfAt(world.World, x, y, 1).contents).OfType<obj_machinery_atmospherics_components_unary_vent_pump>())
                if (!InertSeam.IsInert(v) && Dm.truthy(v.on) && v.nodes[1] is obj_machinery_atmospherics_pipe)
                    vent = v;
            Assert.NotNull(vent);
            _output.WriteLine($"{vent.name} at ({vent.x},{vent.y}) {vent.icon_state}, its pipe {vent.nodes[1]}");
            Assert.Equal("vent_out", vent.icon_state);
            _output.WriteLine(DevCommands.Delete((atom)vent.nodes[1]));
            Assert.Null(vent.nodes[1]); // the pipe's Destroy nullified the vent's node
        }
        world.Run(10);
        using (world.World.Enter())
        {
            Assert.False(Dm.truthy(vent.on));
            Assert.Equal("vent_off", vent.icon_state);
        }
    }

    private sealed record Stats(int Networks, int Pipes, int OrphanPipes, int Vents, int VentsWithoutNode, int Scrubbers, float DistroMedian, int RebuildQueue);

    private Stats Report(RealHeadlessWorld world, string when)
    {
        using (world.World.Enter())
        {
            var ss = GlobalProcs.SSair;
            int pipes = 0, orphanPipes = 0, vents = 0, ventsOn = 0, ventsNoNode = 0, scrubbers = 0, scrubbersOn = 0;
            var ventPressures = new List<float>();
            var scrubPressures = new List<float>();
            for (int y = 1; y <= MapView.Height(world.World); y++)
            for (int x = 1; x <= MapView.Width(world.World); x++)
            foreach (atom a in Dm.each_cast<atom>(MapView.TurfAt(world.World, x, y, 1).contents))
            {
                if (a is obj_machinery_atmospherics_pipe p && !InertSeam.IsInert(p))
                {
                    pipes++;
                    if (p.parent == null) orphanPipes++;
                }
                if (a is obj_machinery_atmospherics_components_unary_vent_pump v && !InertSeam.IsInert(v))
                {
                    vents++;
                    if (Dm.truthy(v.on)) ventsOn++;
                    if (v.nodes[1] == null) ventsNoNode++;
                    else if (v.parents[1] is datum_pipeline vp) ventPressures.Add(Dm.num(vp.air.return_pressure()));
                }
                if (a is obj_machinery_atmospherics_components_unary_vent_scrubber s && !InertSeam.IsInert(s))
                {
                    scrubbers++;
                    if (Dm.truthy(s.on)) scrubbersOn++;
                    if (s.parents[1] is datum_pipeline sp) scrubPressures.Add(Dm.num(sp.air.return_pressure()));
                }
            }
            ventPressures.Sort();
            scrubPressures.Sort();
            string med(List<float> l) => l.Count == 0 ? "-" : $"min {l[0]:0.#} med {l[l.Count / 2]:0.#} max {l[^1]:0.#}";
            _output.WriteLine($"{when}: networks {ss.networks.len}, atmos machinery {ss.atmos_machinery.len}, rebuild queue {ss.rebuild_queue.len}, active turfs {ss.active_turfs.len}; pipes {pipes} (orphans {orphanPipes}); vents {vents} on {ventsOn} without node {ventsNoNode}, distro kPa {med(ventPressures)}; scrubbers {scrubbers} on {scrubbersOn}, waste kPa {med(scrubPressures)}");
            return new Stats((int)ss.networks.len, pipes, orphanPipes, vents, ventsNoNode, scrubbers, ventPressures.Count == 0 ? -1 : ventPressures[ventPressures.Count / 2], (int)ss.rebuild_queue.len);
        }
    }
}
