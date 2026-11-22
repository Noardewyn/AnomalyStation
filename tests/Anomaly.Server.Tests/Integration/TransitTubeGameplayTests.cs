using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// MetaStation's transit tube (station.dm, transit_tube_pod.dm) through the real server: a player stands on the boarding side of one of the map's two
/// pod dispensers, looks the way tg's boarding_dir says and walks into it; a temporary pod forms around them (the client sees its occupied state, which
/// the catalog's model draws), rides the tube and dissolves at the other dispenser, which lets the player out on its tile.
/// </summary>
public class TransitTubeGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [MetaServerFact]
    public void A_player_walks_into_a_dispenser_and_rides_to_the_other_one()
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true, NoInterest = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;

        List<obj_structure_transit_tube_station_dispenser> dispensers = In(w, () => Dm.each<obj_structure_transit_tube_station_dispenser>(Dm.world).Where(d => Dm.eq(d.z, z)).ToList());
        Assert.Equal(2, dispensers.Count);
        var from = dispensers[0];
        var to = dispensers[1];
        (int bx, int by, byte boarding) = In(w, () =>
        {
            float board = Dm.num(from.boarding_dir);
            turf side = get_step(from, REVERSE_DIR(board)) as turf;
            return ((int)Dm.num(side.x), (int)Dm.num(side.y), (byte)board);
        });
        output.WriteLine($"from ({In(w, () => from.x)},{In(w, () => from.y)}) boarding {boarding} at ({bx},{by}); to ({In(w, () => to.x)},{In(w, () => to.y)})");
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(locate(bx, by, z)));
        w.Run(Second);

        // the view looks the boarding way (Godot's yaw: 0 north, +90 degrees west), so the mob's dir is boarding_dir when it presses into the dispenser
        int dx = ((boarding & 4) != 0 ? 1 : 0) - ((boarding & 8) != 0 ? 1 : 0), dy = ((boarding & 1) != 0 ? 1 : 0) - ((boarding & 2) != 0 ? 1 : 0);
        p.Yaw = Math.Atan2(-dx, dy);
        p.Keys = MoveKeys.Forward;
        bool seenOccupiedPod = false;
        string podModel = null;
        ModelCatalog catalog = ModelCatalog.Parse(File.ReadAllText(Path.Combine(ModelsRoot(), ModelCatalog.FileName)));
        for (int i = 0; i < 30 * Second && In(w, () => me.loc != to.loc); i++)
        {
            w.Run(1);
            if (In(w, () => me.loc is obj_structure_transit_tube_pod))
                p.Keys = MoveKeys.None;
            foreach (ReplicaEntity e in replica.Entities.Where(e => e.TypePath == "/obj/structure/transit_tube_pod/dispensed"))
            {
                if (!replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look) || replica.ResolveString(look.IconState) != "temppod_occupied")
                    continue;
                seenOccupiedPod = true;
                podModel ??= catalog.Find(replica.ResolveResource(look.Icon), "temppod_occupied")?.Id;
            }
        }
        p.Keys = MoveKeys.None;
        w.Run(Second);

        Assert.True(seenOccupiedPod, "the client saw the occupied temporary pod");
        Assert.Equal("transit_temppod_occupied", podModel);
        Assert.Same(In(w, () => to.loc), In(w, () => me.loc));
        Assert.Empty(In(w, () => Dm.each<obj_structure_transit_tube_pod>(Dm.world).ToList()));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    private static string ModelsRoot()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "godot", "assets", "models");
            if (File.Exists(Path.Combine(candidate, ModelCatalog.FileName)))
                return candidate;
        }
        throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
    }
}
