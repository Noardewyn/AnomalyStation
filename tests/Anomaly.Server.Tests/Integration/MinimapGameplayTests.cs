using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D315 through the real server with a headless client: a joining player's client gets the minimap of the level its mob stands on
/// (protocol version 67, tg's load_z picture), once, with the cell under the mob drawn and named after its area; on MetaStation the
/// station's minimap names tg's departments. The dev trackers (protocol version 68, the dev command minimapmobs): the living mobs of the
/// level but the own, players apart from the rest, twice a second while on; refused on a server whose dev commands are off.
/// </summary>
public class MinimapGameplayTests(ITestOutputHelper output)
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void AssertTheMobsCellIsDrawn(RealHeadlessWorld w, RealHeadlessWorld.Player player, MinimapMessage map)
    {
        mob_living me = w.MobOf(player);
        (int x, int y, int z, string areaName) = In(w, () => get_turf(me) is turf t ? ((int)t.x, (int)t.y, (int)t.z, ((area)t.loc).name) : default);
        Assert.Equal(z, map.Z);
        int cell = (y - map.MinY) * map.Width + (x - map.MinX);
        Assert.InRange(cell, 0, map.CellCount - 1);
        Assert.NotEqual(0, map.ExpandPixels()[cell]);
        int area = map.ExpandAreas()[cell];
        Assert.NotEqual(0, area);
        Assert.Equal(areaName, map.AreaNames[area - 1]);
    }

    // tg's hover text of every drawn cell: the name of the area of the turf there
    private static void AssertTheCellsAreNamedAfterTheirAreas(RealHeadlessWorld w, MinimapMessage map)
    {
        int[] areas = map.ExpandAreas();
        int[] pixels = map.ExpandPixels();
        using (w.World.Enter())
        {
            for (int cell = 0; cell < areas.Length; cell++)
            {
                Assert.Equal(pixels[cell] == 0, areas[cell] == 0);
                if (areas[cell] != 0)
                    Assert.Equal(((area)MapView.TurfAt(w.World, map.MinX + cell % map.Width, map.MinY + cell / map.Width, map.Z).loc).name, map.AreaNames[areas[cell] - 1]);
            }
        }
    }

    [Fact]
    public void A_joining_client_gets_the_minimap_of_its_level_once()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("p2load", MapJoinTests.FixtureMaps()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Mapper");
        w.Run(20);

        MinimapMessage map = Assert.Single(p.Client.Minimaps).Value;
        AssertTheMobsCellIsDrawn(w, p, map);
        AssertTheCellsAreNamedAfterTheirAreas(w, map);
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void Metas_station_minimap_names_tgs_departments()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 315, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Mapper");
        w.Run(20);

        MinimapMessage map = Assert.Single(p.Client.Minimaps).Value;
        output.WriteLine($"level {map.Z}: {map.Width} x {map.Height} from ({map.MinX}, {map.MinY}), {map.Colours.Count} colours, {map.AreaNames.Count} areas, {Codec.Encode(map).Length} bytes");
        Assert.Equal(In(w, () => (int)get_turf(w.MobOf(p)).z), map.Z); // the mob stands on the arrivals shuttle, which tg leaves out (isshuttleturf)
        AssertTheCellsAreNamedAfterTheirAreas(w, map);
        Assert.Subset(map.AreaCodes.ToHashSet(), new HashSet<string> { "CMD", "SEC", "MED", "SCI", "ENG", "CRG", "SRV", "MNT" });
        Assert.True(map.AreaNames.Count > 100, "Meta's station areas");
    }

    [Fact]
    public void The_dev_trackers_show_the_living_mobs_of_the_level_while_they_are_on()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("p2load", MapJoinTests.FixtureMaps()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        turf bobs = In(w, () => get_turf(w.MobOf(bob)));
        w.Host.EnqueueConsoleLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"spawn /mob/living/carbon/human {bobs.x} {bobs.y} {bobs.z}"));
        alice.Session.DevCommand("minimapmobs on");
        w.Run(25);

        MinimapMobsMessage seen = alice.Client.MinimapMobs;
        Assert.NotNull(seen);
        Assert.Equal((int)bobs.z, seen.Z);
        MinimapMob bobsDot = Assert.Single(seen.Mobs, m => m.Player); // Bob, not Alice herself
        Assert.Equal(((int)bobs.x, (int)bobs.y), (bobsDot.X, bobsDot.Y));
        Assert.Equal(In(w, () => w.MobOf(bob).name), bobsDot.Name);
        Assert.Contains(seen.Mobs, m => !m.Player && m.X == (int)bobs.x && m.Y == (int)bobs.y); // the human with no player
        Assert.Null(bob.Client.MinimapMobs); // only the session that asked

        alice.Session.DevCommand("minimapmobs off");
        w.Run(5);
        MinimapMobsMessage last = alice.Client.MinimapMobs;
        w.Run(25);
        Assert.Same(last, alice.Client.MinimapMobs);
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_server_whose_dev_commands_are_off_sends_no_trackers()
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "p2load", MapsFolder = MapJoinTests.FixtureMaps(), DevCommands = false };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, config, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Join("Bob");
        alice.Session.DevCommand("minimapmobs on");
        w.Run(25);
        Assert.Null(alice.Client.MinimapMobs);
    }
}
