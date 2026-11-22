using System.Diagnostics;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 on the booted MetaStation: tg's <c>cable_powernets</c> map test (code/modules/unit_tests/cable_powernets.dm), adapted to
/// the round start (below), the networks' structure after SSmachines' makepowernets, and the
/// measurement of ROADMAP M7: the time of SSmachines' powernet step (the reset of every network, machines.dm:94) at rest.
/// </summary>
public class MetaPowernetTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MetaPowernetTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    /// <summary>
    /// tg: code/modules/unit_tests/cable_powernets.dm:2 (/datum/unit_test/cable_powernets). ADAPTED: tg's map tests run after the
    /// round start, when SSmachines has fired and the charged SMESes' output has reached their networks (smes.dm:362, add_avail;
    /// the next fire's reset makes it avail, powernet.dm:96). Here the round is started (RUNLEVEL_GAME) and SSmachines stepped
    /// three fires; then the TEST_ASSERTs and the three TEST_FAILs are asserted, each failure printed with tg's message.
    /// </summary>
    [MetaFact]
    public void cable_powernets()
    {
        // the class's own fixture: the other tests here read only the networks' structure and time their reset
        TgTestWorld.RunLevel(_meta.World, RUNLEVEL_GAME);
        float Fired()
        {
            using (_meta.World.Enter())
                return SSmachines.times_fired;
        }
        float start = Fired();
        for (int t = 0; t < 400 && Fired() < start + 3; t++)
            _meta.World.Step();
        using (_meta.World.Enter())
        {
            Assert.True(SSmachines.times_fired >= start + 3, "SSmachines fired three times");
            var fails = new List<string>();
            foreach (datum_powernet powernets in Dm.each_cast<datum_powernet>(SSmachines.powernets))
            {
                //nodes (machines, which includes APCs and SMES)
                if (!Dm.truthy(length(powernets.nodes)))
                {
                    Assert.True(Dm.truthy(length(powernets.cables)), Dm.text("[] found with no nodes OR cables connected, something has gone horribly wrong.", powernets));

                    obj_structure_cable found_cable = powernets.cables[1] as obj_structure_cable;
                    //Check if they're a station area
                    area cable_area = get_area(found_cable) as area;
                    if (Dm.@in(cable_area.type, GLOB.the_station_areas) && Dm.truthy(cable_area.requires_power))
                        fails.Add(Dm.text("[] found with no nodes connected ([], [], [])).", powernets, found_cable.x, found_cable.y, found_cable.z)); // TEST_FAIL (:17)
                }

                //cables
                if (!Dm.truthy(length(powernets.cables)))
                {
                    Assert.True(Dm.truthy(length(powernets.nodes)), Dm.text("[] found with no cables OR nodes connected, something has gone horribly wrong.", powernets));

                    obj_machinery_power found_machine = powernets.nodes[1] as obj_machinery_power;
                    //Check if they're a station area
                    area machine_area = get_area(found_machine) as area;
                    if (Dm.@in(machine_area.type, GLOB.the_station_areas) && Dm.truthy(machine_area.requires_power))
                        fails.Add(Dm.text("[] found with no cables connected ([], [], []).", powernets, found_machine.x, found_machine.y, found_machine.z)); // TEST_FAIL (:28)
                }

                if (!Dm.truthy(powernets.avail) && !Dm.truthy(Dm.locate<obj_machinery_power_terminal>(powernets.nodes))) //No power roundstart, so check for an SMES connection (Solars, Turbine).
                {
                    obj_structure_cable random_cable = powernets.cables[1] as obj_structure_cable;
                    //Check if they're a station area
                    area cable_area = get_area(random_cable) as area;
                    if (Dm.@in(cable_area.type, GLOB.the_station_areas) && Dm.truthy(cable_area.requires_power))
                        fails.Add(Dm.text("[] found with no power roundstart, connected to a cable at ([], [], []).", powernets, random_cable.x, random_cable.y, random_cable.z)); // TEST_FAIL (:36)
                }
            }
            _output.WriteLine($"{SSmachines.powernets.len} networks after {SSmachines.times_fired} SSmachines fires: {fails.Count} TEST_FAILs");
            foreach (string fail in fails)
                _output.WriteLine(fail);
            Assert.Empty(fails);
        }
    }

    [MetaFact]
    public void Every_cable_of_Meta_is_on_one_network_and_the_networks_hold_only_cables_and_power_machines()
    {
        using (_meta.World.Enter())
        {
            DmList cables = GLOB.cable_list;
            Assert.True(cables.len > 3000, "Meta's cable nodes: " + cables.len); // ROADMAP M7: 3,631
            float onNets = 0, nodes = 0;
            var machineKinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (datum_powernet net in Dm.each_cast<datum_powernet>(SSmachines.powernets))
            {
                onNets += net.cables.len;
                foreach (obj_structure_cable c in Dm.each_cast<obj_structure_cable>(net.cables))
                    Assert.Same(net, c.powernet);
                foreach (obj_machinery_power m in Dm.each_cast<obj_machinery_power>(net.nodes))
                {
                    nodes++;
                    Assert.Same(net, m.powernet);
                    Assert.False(m is obj_machinery_power_apc, "an APC is reached through its terminal (apc_power_proc.dm:4)");
                    string kind = DmTypes.PathOf(m.GetType());
                    machineKinds[kind] = machineKinds.TryGetValue(kind, out int n) ? n + 1 : 1;
                }
            }
            Assert.All(Dm.each_cast<obj_structure_cable>(cables), c => Assert.NotNull(c.powernet));
            Assert.Equal(cables.len, onNets);
            _output.WriteLine($"{cables.len} cables on {SSmachines.powernets.len} networks, {nodes} machines: " + string.Join(", ", machineKinds.Select(k => $"{k.Key} {k.Value}")));
        }
    }

    /// <summary>
    /// ROADMAP M7: Meta's network at rest, the time of SSmachines' powernet step (machines.dm:93-95: the reset of every network,
    /// once per SSmachines fire, every 2 s). Target: under 0.5 ms per tick. Also prints makepowernets' time (the init's rebuild).
    /// </summary>
    [MetaFact]
    public void The_powernet_step_of_Meta_at_rest_takes_under_half_a_millisecond()
    {
        using (_meta.World.Enter())
        {
            void Step()
            {
                foreach (datum_powernet powernet in Dm.each_cast<datum_powernet>(SSmachines.powernets))
                    Waiting.NoSleep(powernet.reset()); //reset the power state.
            }

            for (int i = 0; i < 200; i++)
                Step(); // warm up
            const int Runs = 2000;
            var clock = Stopwatch.StartNew();
            double worst = 0;
            for (int i = 0; i < Runs; i++)
            {
                double before = clock.Elapsed.TotalMilliseconds;
                Step();
                worst = Math.Max(worst, clock.Elapsed.TotalMilliseconds - before);
            }
            double mean = clock.Elapsed.TotalMilliseconds / Runs;

            var build = Stopwatch.StartNew();
            SSmachines.makepowernets();
            double makeMs = build.Elapsed.TotalMilliseconds;

            float ticksPerFire = Dm.num(SSmachines.wait) / world.tick_lag;
            _output.WriteLine($"{GLOB.cable_list.len} cables, {SSmachines.powernets.len} networks: the powernet step {mean * 1000:0.0} us per SSmachines fire (worst {worst * 1000:0.0} us), {mean * 1000 / ticksPerFire:0.00} us per tick amortized over {ticksPerFire} ticks; makepowernets {makeMs:0.0} ms");
            Assert.True(mean < 0.5, $"the powernet step took {mean:0.000} ms");
            Assert.All(Dm.each_cast<obj_structure_cable>(GLOB.cable_list), c => Assert.NotNull(c.powernet));
        }
    }
}
