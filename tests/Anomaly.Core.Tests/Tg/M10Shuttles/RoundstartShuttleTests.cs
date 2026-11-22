using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M10Shuttles;

/// <summary>
/// M10 slice G3 (D272): MetaStation's stationary ports load their round-start templates into a reservation and dock them with tg's
/// move code (SSshuttle.action_load), rotated to the dock where the template's port faces another way: the arrivals shuttle, the
/// mining, common mining and labour shuttles, the four escape pods and the auxiliary base stand at their docks, each with its area
/// and its turfs; the arrivals shuttle's chairs are the late joiners' spawns. CentCom's docks stand in a reservation (DV190): the
/// emergency, backup and ferry shuttles wait there; the cargo shuttle starts at Meta's cargo bay (D266).
/// </summary>
public class RoundstartShuttleTests
{
    private readonly ITestOutputHelper _output;

    public RoundstartShuttleTests(ITestOutputHelper output) => _output = output;

    [MetaFact]
    public void Metas_round_start_shuttles_stand_at_their_docks()
    {
        World world = MetaMaps.Boot(out TgTestWorld.Log log);
        using (world.Enter())
        {
            var docked = new Dictionary<string, (string Dock, float Dir, int Turfs, string Area)>();
            foreach (obj_docking_port_mobile m in Dm.each<obj_docking_port_mobile>(SSshuttle.mobile_docking_ports))
            {
                var dock = m.get_docked() as obj_docking_port_stationary;
                docked[Dm.str(m.shuttle_id)] = (Dm.str(dock?.shuttle_id), m.dir, (int)m.turf_count, m.loc?.loc?.GetType().Name);
                _output.WriteLine($"{m.shuttle_id}: at {m.x},{m.y},{m.z} dir {m.dir}, docked at {dock?.shuttle_id}, {m.turf_count} turfs of {m.loc?.loc?.GetType().Name}");
                bool centcom = Dm.str(dock?.shuttle_id)?.EndsWith("_away", StringComparison.Ordinal) == true;
                Assert.Equal(centcom ? 2f : 1f, m.z); // the station's docks on the station level; CentCom's stand-in docks on the reserved level (DV190)
                Assert.True(dock != null && dock.x == m.x && dock.y == m.y && dock.dir == m.dir, $"{m.shuttle_id} stands on its dock");
                Assert.True(m.shuttle_areas.len >= 1, $"{m.shuttle_id} has its areas"); // the emergency shuttle has two (the cabin and the brig)
            }
            Assert.Equal(new[] { "arrival", "backup", "cargo", "colony_drop", "emergency", "ferry", "laborcamp", "mining", "mining_common", "pod", "pod_2", "pod_3", "pod_4" }, docked.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(("arrival_stationary", 8f, 99, "area_shuttle_arrival"), docked["arrival"]);
            // the delta mining template's port faces east, Meta's mining dock west: turned 180 degrees
            Assert.Equal(("mining_home", 8f, 35, "area_shuttle_mining"), docked["mining"]);
            // the common mining template's port faces west, Meta's dock south: turned 90 degrees
            Assert.Equal(("commonmining_home", 2f, 35, "area_shuttle_mining"), docked["mining_common"]);
            Assert.Equal(("laborcamp_home", 8f, 45, "area_shuttle_labor"), docked["laborcamp"]);
            Assert.Equal(("aux_base_zone", 1f, 81, "area_shuttle_auxiliary_base"), docked["colony_drop"]);
            Assert.NotNull(SSshuttle.arrivals);
            // CentCom's stand-in docks (DV190): the emergency shuttle (Meta's, the map json's) waits at its CentCom dock, the backup at
            // its own, the ferry at its; the cargo shuttle starts at Meta's cargo bay (D266)
            Assert.Equal("emergency_away", docked["emergency"].Dock);
            Assert.Equal("backup_away", docked["backup"].Dock);
            Assert.Equal("ferry_away", docked["ferry"].Dock);
            Assert.Equal("cargo_away", docked["cargo"].Dock); // D299: it waits at CentCom for the console
            Assert.Equal("emergency", Dm.str(SSshuttle.emergency?.shuttle_id));
            Assert.Equal("backup", Dm.str(SSshuttle.backup_shuttle?.shuttle_id));
            Assert.Equal("cargo", Dm.str(SSshuttle.supply?.shuttle_id));
            Assert.Equal(SHUTTLE_IDLE, SSshuttle.emergency.mode);

            // the late joiners' spawns are the arrivals shuttle's chairs (arrivals.dm LateInitialize)
            Assert.NotEmpty(Dm.each(SSjob.latejoin_trackers));
            Assert.All(Dm.each(SSjob.latejoin_trackers), c => Assert.IsAssignableFrom<obj_structure_chair>(c));
            Assert.All(Dm.each<atom>(SSjob.latejoin_trackers), c => Assert.IsType<area_shuttle_arrival>(get_area(c)));
            _output.WriteLine($"late join chairs: {SSjob.latejoin_trackers.len}");

            // a supermatter cascade strands the idle emergency shuttle (delamination_effects.dm:117)
            var delam = Dm.@new<datum_sm_delam>();
            Waiting.NoSleep(delam.effect_strand_shuttle());
            Assert.Equal(SHUTTLE_STRANDED, SSshuttle.emergency.mode);
            Assert.True(Dm.truthy(SSshuttle.emergency_no_recall));
            Assert.True(Dm.truthy(SSshuttle.supermatter_cascade));

            List<Exception> errors = log.Runtimes.Where(e => !e.Message.Contains("not translated yet")).ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString().Split('\n')[0])));
        }
    }
}
