using System.IO;
using Anomaly.Client.Shared.Editor;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The way a map's things face through the real server on MetaStation (DV56, DV508): a modelled thing whose map says the way it faces keeps it
/// through the round's start and the client sees it, a random spawner's pick takes its spawner's way, and the rest face away from the wall
/// behind them. The owner's turns of 2026-10 to 2026-11, once kept in <c>map_patches/metastation.json</c>, are the map's own <c>dir</c> vars now
/// (docs/ai/specs/map-editor.md 5.15): this is the test that they hold. The dev commands <c>rotate</c> and <c>mount</c> turn and hang for the round.
/// </summary>
public class MapDirGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public MapDirGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static string TypeOf(atom a) => DmTypes.PathOf(a.GetType());

    [MetaServerFact]
    public void The_maps_turns_hold_through_the_round_start_and_reach_the_client()
    {
        var config = new ServerConfig
        {
            Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true,
            Lobby = true, NoInterest = true,
        };
        MapDocument map = MapDocument.Parse(File.ReadAllText(Path.Combine(MetaServer.Folder(), "map_files", "MetaStation", "MetaStation.as3dmap")), "MetaStation");
        var rules = ModelDirRules.Parse(File.ReadAllText(ModelDirRules.Locate(null)));
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 20000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        p.Session.DevCommand("startnow");
        w.Run(10 * Second);

        var wrong = new List<string>();
        int held = 0, picks = 0;
        for (int y = 1; y <= map.Height; y++)
        {
            for (int x = 1; x <= map.Width; x++)
            {
                MapModel tile = map[x, y, 1];
                if (ReferenceEquals(tile, map.Default))
                    continue;
                foreach (MapMember member in tile.Members)
                {
                    if (MapEdits.VarOf(member, "dir") is not { } literal || !MapEdits.IsThing(member.Path))
                        continue;
                    int dir = int.Parse(literal, System.Globalization.CultureInfo.InvariantCulture);
                    bool spawner = member.Path.StartsWith("/obj/effect/spawner/random/", StringComparison.Ordinal);
                    // the member's own thing, where the models' rule would turn it; a spawner's pick, where it is a thing a model draws to be turned
                    List<atom> things = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, 1)).contents)
                        .Where(a => spawner ? rules.Of(a)?.Rotate == true : TypeOf(a) == member.Path && rules.Of(a) is { AwayFromWall: true }).ToList());
                    // (two members of one tile with two ways, or a spawner beside a thing of the map: which thing is whose is not told here)
                    if (things.Count != 1 || tile.Members.Count(m => m.Path == member.Path) != 1)
                        continue;
                    int server = In(w, () => (int)Dm.num(things[0].dir));
                    ReplicaEntity seen = p.Session.Replica.Entities.FirstOrDefault(e => e.Id == things[0].dm_id);
                    if (server == dir && seen?.State.Dir == dir)
                        (held, picks) = (held + 1, picks + (spawner ? 1 : 0));
                    else
                        wrong.Add($"{member.Path} ({x},{y}) {TypeOf(things[0])}: the map says {dir}, the server {server}, the client {seen?.State.Dir}");
                }
            }
        }
        _output.WriteLine($"{held} turns held ({picks} of them a spawner's pick); {wrong.Count} wrong:\n" + string.Join("\n", wrong));
        Assert.Empty(wrong);
        // the owner's turns are in the map: hundreds of modelled things, dozens of them a spawner's pick
        Assert.True(held >= 250 && picks >= 30, $"{held} held, {picks} picks");
    }

    [MetaServerFact]
    public void The_dev_commands_turn_a_closet_and_hang_an_alarm_for_the_round()
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var replies = new List<string>();
        p.Client.ServerTextReceived += m => replies.Add(m.Html);

        // a closet turned a quarter clockwise: the server's dir and the client's
        obj_structure_closet closet = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(65, 94, 1)).contents).OfType<obj_structure_closet>().First());
        int before = In(w, () => (int)Dm.num(closet.dir));
        p.Session.DevCommand($"rotate #{closet.dm_id}");
        w.Run(Second);
        _output.WriteLine(string.Join(" | ", replies));
        Assert.Equal(DevTurns.Turn(before, clockwise: true), In(w, () => (int)Dm.num(closet.dir)));
        Assert.Equal(DevTurns.Turn(before, clockwise: true), p.Session.Replica.Entities.Single(e => e.Id == closet.dm_id).State.Dir);

        // A mounted fire alarm and, near it, a wall with open floor south of it (hanging on its south side: the offset goes north).
        (obj_machinery_firealarm alarm, turf wall) = In(w, () =>
        {
            foreach (obj_machinery_firealarm a in Dm.each_cast<obj_machinery_firealarm>(GlobalProcs.SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_firealarm))))
            {
                if (a.GetComponent(typeof(datum_component_atom_mounted)) == null || a.loc is not turf at)
                    continue;
                for (int dx = -6; dx <= 6; dx++)
                    for (int dy = -6; dy <= 6; dy++)
                        if (Builtins.locate((int)at.x + dx, (int)at.y + dy, (int)at.z) is turf_closed c && Builtins.locate((int)c.x, (int)c.y - 1, (int)c.z) is turf_open && (int)c.y - 1 != (int)at.y)
                            return (a, (turf)c);
            }
            return (null, null);
        });
        Assert.NotNull(alarm);
        (int toX, int toY, int z, int reach) = In(w, () => ((int)wall.x, (int)wall.y, (int)wall.z, (int)Math.Max(Math.Abs(alarm.pixel_x), Math.Abs(alarm.pixel_y))));
        Assert.True(reach >= 16);
        long id = alarm.dm_id;
        p.Session.DevCommand($"mount #{id} {toX} {toY} {z} south");
        w.Run(Second);

        // Not torn off and deconstructed by the move (atom_mounted.on_move): on the floor in front of the face, its offset and dir toward the wall, mounted on it.
        Assert.False(In(w, () => GlobalProcs.QDELETED(alarm)));
        Assert.Equal((toX, toY - 1), In(w, () => ((int)((turf)alarm.loc).x, (int)((turf)alarm.loc).y)));
        Assert.Equal((0, reach, (int)Dm.NORTH), In(w, () => ((int)alarm.pixel_x, (int)alarm.pixel_y, (int)Dm.num(alarm.dir))));
        Assert.NotNull(In(w, () => alarm.GetComponent(typeof(datum_component_atom_mounted))));
        // The client sees the new cell and the new offset.
        ReplicaEntity seen = p.Session.Replica.Entities.Single(e => e.Id == id);
        Assert.Equal((toX, toY - 1), ((int)seen.State.X, (int)seen.State.Y));
        Assert.True(p.Session.Replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        Assert.Equal((0, reach), ((int)look.PixelX, (int)look.PixelY));
        // Refused, and nothing moves: a wall side that is not open.
        p.Session.DevCommand($"mount #{id} {toX} {toY - 1} {z} south");
        w.Run(Second);
        Assert.Equal((toX, toY - 1), In(w, () => ((int)((turf)alarm.loc).x, (int)((turf)alarm.loc).y)));
    }
}
