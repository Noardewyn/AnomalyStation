using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The Lavaland shuttles on the converted MetaStation: Meta's and Lavaland's mining, common mining and labour consoles boot live and
/// each lists the far end of its trip; and the auxiliary base: a miner on Lavaland sets its landing zone with the landing field
/// designator, the base's console drops the base there (its AuxBaseConsole window), and the mining shuttle beacon set beside the landed
/// base makes a landing site for the mining shuttle and turns the base's console into the mining shuttle's.
/// </summary>
public class MiningShuttleMetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MiningShuttleMetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static string[] Ids(DmList locations) => Dm.each<DmList>(locations).Select(l => Dm.str(l["id"])).ToArray();

    [MetaFact]
    public void Metas_mining_and_labour_consoles_boot_live_and_each_lists_the_far_end_of_its_trip()
    {
        using (_meta.World.Enter())
        {
            foreach (string id in new[] { "mining_home", "mining_away", "commonmining_home", "lavaland_common_away", "laborcamp_home", "laborcamp_away" })
                Assert.NotNull(SSshuttle.getDock(id));
            // the station's and Lavaland's (CentCom's ferry and supply rooms keep their own, unpowered, as tg maps them)
            var consoles = Dm.each<obj_machinery_computer_shuttle>(Dm.world).Where(c => (c is obj_machinery_computer_shuttle_mining || c is obj_machinery_computer_shuttle_labor) && (Dm.truthy(is_station_level(c.z)) || Dm.truthy(is_mining_level(c.z)))).ToList();
            foreach (obj_machinery_computer_shuttle console in consoles)
            {
                string[] ids = Ids(console.get_valid_destinations());
                _output.WriteLine($"{console.GetType().Name} at {console.x},{console.y},{console.z} ({get_area(console)?.GetType().Name}): {console.shuttleId} -> {string.Join(", ", ids)}");
                Assert.False(InertSeam.IsInert(console));
                Assert.NotEmpty(ids);
            }
            Assert.Equal(10, consoles.Count);
            string[] mining = Ids(consoles.First(c => c.GetType() == typeof(obj_machinery_computer_shuttle_mining)).get_valid_destinations());
            Assert.Contains("mining_away", mining);
            Assert.Contains("lavaland_common_away", Ids(consoles.First(c => c is obj_machinery_computer_shuttle_mining_common && get_area(c) is area_shuttle_mining).get_valid_destinations()));
            // the labour shuttle waits at the brig: every labour console offers the camp
            Assert.All(consoles.OfType<obj_machinery_computer_shuttle_labor>(), c => Assert.Equal(new[] { "laborcamp_away" }, Ids(c.get_valid_destinations())));

            // the auxiliary base, its console and its beacon, loaded from its template at the round start; the designators in the lockers
            Assert.NotNull(SSshuttle.getShuttle("colony_drop"));
            var aux = Assert.Single(Dm.each<obj_machinery_computer_auxiliary_base>(Dm.world), c => Dm.truthy(is_station_level(c.z)));
            Assert.False(InertSeam.IsInert(aux));
            Assert.Equal("colony_drop", aux.shuttleId);
            Assert.NotNull(aux.GetComponent(typeof(datum_component_gps)));
            Assert.False(InertSeam.IsInert(Assert.Single(Dm.each<obj_structure_mining_shuttle_beacon>(Dm.world), b => Dm.truthy(is_station_level(b.z)))));
            Assert.NotEmpty(Dm.each<obj_item_assault_pod_mining>(Dm.world));
            Assert.All(Dm.each<obj_item_assault_pod_mining>(Dm.world), d => Assert.False(InertSeam.IsInert(d)));

            var runtimes = _meta.Log.Runtimes.Where(e =>
            {
                string text = e.ToString() ?? "";
                return text.Contains("mine_items", StringComparison.Ordinal) || text.Contains("aux_base", StringComparison.Ordinal) || text.Contains("shuttle_console", StringComparison.Ordinal) || text.Contains("laborshuttle", StringComparison.Ordinal);
            }).ToList();
            Assert.Empty(runtimes);
        }
    }

    /// The first Lavaland surface turf whose footprint for <paramref name="port"/> (turned to <paramref name="dir"/>) fits <paramref name="fits"/>.
    private static turf FindSpot(obj_docking_port_mobile port, float z, float dir, Func<turf, bool> fits, int fromX = 20, int toX = 235, int fromY = 20, int toY = 235)
    {
        for (int y = fromY; y <= toY; y += 3)
        {
            for (int x = fromX; x <= toX; x += 3)
            {
                turf T = locate(x, y, z);
                if (T == null || T.loc is not area_lavaland_surface)
                    continue;
                DmList footprint = port.return_ordered_turfs(x, y, z, dir);
                if (Dm.each<object>(footprint).All(t => t is turf place && fits(place)))
                    return T;
            }
        }
        return null;
    }

    [MetaFact]
    public void The_auxiliary_base_drops_on_a_landing_zone_set_on_Lavaland_and_its_beacon_brings_the_mining_shuttle_beside_it()
    {
        var m = MetaRound.Boot(13030);
        obj_docking_port_mobile_auxiliary_base basePort = null;
        obj_machinery_computer_auxiliary_base console = null;
        obj_docking_port_mobile mining = null;
        turf zone = null;
        float lavaland = 0;
        m.In(() =>
        {
            basePort = (obj_docking_port_mobile_auxiliary_base)SSshuttle.getShuttle("colony_drop");
            console = Dm.each<obj_machinery_computer_auxiliary_base>(Dm.world).Single(c => Dm.truthy(is_station_level(c.z)));
            mining = (obj_docking_port_mobile)SSshuttle.getShuttle("mining");
            lavaland = ((obj_docking_port_stationary)SSshuttle.getDock("mining_away")).z;
            // where tg's set_landing_zone would take the base: Lavaland's open surface, no wall, lava or indestructible ground
            zone = FindSpot(basePort, lavaland, basePort.dir, place => place.loc is area_lavaland_surface && place is not turf_open_lava && place is not turf_open_indestructible && (place is not turf_closed || place is turf_closed_mineral));
            Assert.NotNull(zone);
        });
        _output.WriteLine($"landing zone at {zone.x},{zone.y}");

        // the miner sets the landing zone with the designator
        var miner = m.Join("Mina", m.In(() => (atom)SSshuttle.getDock("mining_away")));
        turf standBy = m.In(() => (turf)miner.loc);
        var designator = m.In(() =>
        {
            Waiting.NoSleep(miner.forceMove(zone));
            var d = Dm.@new<obj_item_assault_pod_mining>(zone);
            Assert.True(Dm.truthy(Waiting.NoSleep(miner.put_in_active_hand(d))));
            Waiting.Detach(d.attack_self(miner));
            return d;
        });
        m.Step(6 * Second);
        Assert.True(m.Feed.Any("Landing zone set."), string.Join(" | ", m.Feed.Chat.Select(c => c.Html)));
        Assert.True(m.Feed.Any("Auxiliary base landing zone coordinates locked in for"), string.Join(" | ", m.Feed.Chat.Select(c => c.Html)));
        string zoneId = m.In(() =>
        {
            Assert.True(QDELETED(designator));
            var dock = Dm.each<obj_docking_port_stationary>(SSshuttle.stationary_docking_ports).Single(d => Dm.str(d.shuttle_id).StartsWith("colony_drop(", StringComparison.Ordinal));
            Assert.Same(zone, dock.loc);
            Assert.Equal(basePort.width, dock.width);
            Assert.Contains(Dm.str(dock.shuttle_id), Dm.str(console.possible_destinations));
            return Dm.str(dock.shuttle_id);
        });
        // out of the way: the base crushes whoever stands where it lands
        m.In(() => Waiting.NoSleep(miner.forceMove(standBy)));

        // the base's console on the station drops it there
        var engineer = m.Join("Ernie", console, typeof(datum_id_trim_job_station_engineer));
        JsonElement data = m.Open(engineer, console, out string window, "AuxBaseConsole");
        _output.WriteLine("aux console: " + data);
        Assert.Equal("base", data.GetProperty("type").GetString());
        Assert.Contains(zoneId, data.GetProperty("locations").EnumerateArray().Select(l => l.GetProperty("id").GetString()));
        Assert.Equal(zoneId, data.GetProperty("destination").GetString());
        m.Act(engineer, window, "move", ("shuttle_id", zoneId));
        Assert.True(m.Feed.Any("Launch sequence activated! Prepare for drop!!"), string.Join(" | ", m.Feed.Chat.Select(c => c.Html)));
        Assert.Contains(m.Sounds.Heard, s => (s.File ?? "").EndsWith("warning-buzzer.ogg", StringComparison.Ordinal));
        m.FlyTo(basePort, zoneId, 3000);
        m.In(() =>
        {
            Assert.Equal(lavaland, console.z);
            Assert.Equal(0f, console.launch_warning);
            Assert.Equal(0f, console.blind_drop_ready);
            Assert.Equal(FALSE, console.launch_check(engineer));
            Assert.NotNull(SSshuttle.getDock("mining_public"));
        });

        // the beacon, carried out of the base, marks a landing site for the mining shuttle near it
        var beacon = m.In(() => Dm.each<obj_structure_mining_shuttle_beacon>(Dm.world).Single(b => get_area(b) == get_area(console)));
        turf site = m.In(() => FindSpot(mining, lavaland, beacon.dir, place => place.loc is area_lavaland_surface && get_dist(place, console) <= 14,
            (int)console.x - 14, (int)console.x + 14, (int)console.y - 14, (int)console.y + 14));
        Assert.NotNull(site);
        m.In(() =>
        {
            Waiting.NoSleep(beacon.forceMove(site));
            Waiting.NoSleep(miner.forceMove(get_step(site, NORTH)));
        });
        m.Step(2);
        m.In(() => Waiting.NoSleep(beacon.attack_hand(miner)));
        Assert.True(m.Feed.Any("Mining shuttle calibration successful! Shuttle interface available at base console."), string.Join(" | ", m.Feed.Chat.Select(c => c.Html)));
        m.In(() =>
        {
            Assert.Equal(1f, Dm.num(beacon.anchored));
            Assert.NotNull(SSshuttle.getDock("landing_zone_dock"));
            Assert.Equal("mining", console.shuttleId);
            Assert.Equal(0, console.req_one_access.len);
        });

        // the console now flies the mining shuttle down beside the base
        var miner2 = m.Join("Max", console);
        data = m.Open(miner2, console, out window, "AuxBaseConsole");
        _output.WriteLine("aux console, mining mode: " + data);
        Assert.Equal("shuttle", data.GetProperty("type").GetString());
        Assert.Contains("landing_zone_dock", data.GetProperty("locations").EnumerateArray().Select(l => l.GetProperty("id").GetString()));
        m.Act(miner2, window, "move", ("shuttle_id", "landing_zone_dock"));
        Assert.True(m.Feed.Any("Shuttle request uploaded. Please stand away from the doors."), string.Join(" | ", m.Feed.Chat.Select(c => c.Html)));
        m.FlyTo(mining, "landing_zone_dock");
        Assert.Equal(lavaland, m.In(() => mining.z));
    }
}
