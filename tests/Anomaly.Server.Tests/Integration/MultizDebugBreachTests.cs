using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner (2026-11-13) on tg's multi-z debug map through the real server: the air of the upper decks vents sideways into space (DV411: the map patch
/// makes the airless open space outside them space), and a player the wind blows out of a hull breach drifts on into space (DV412).
/// </summary>
public class MultizDebugBreachTests
{
    // the repository's maps, looked for from the test binary and from this source file (a build outside the repository)
    internal static string RepoMaps([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        foreach (string start in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(source) })
            for (string dir = start; dir != null; dir = Path.GetDirectoryName(dir))
            {
                string candidate = Path.Combine(dir, "maps");
                if (File.Exists(Path.Combine(candidate, "map_jsons", "multiz_debug.json"))) return candidate;
            }
        return null;
    }

    internal static RealHeadlessWorld Boot(string maps)
        => new(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 },
            new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "multiz_debug", MapsFolder = maps }, strict: false);

    private static float Pressure(int x, int y, int z) => Dm.num(((turf_open)locate(x, y, z)).air.return_pressure());

    [Fact]
    public void TheTopDecksAirlockVentsIntoTheSpaceBeyondIt()
    {
        string maps = RepoMaps();
        if (maps == null)
            return; // the converted maps are not here (tools/convert-maps.sh)
        using RealHeadlessWorld w = Boot(maps);
        w.Join("Watcher");
        obj_machinery_door_airlock door;
        float before;
        using (w.World.Enter())
        {
            // level 3's one airlock opens north onto a step and a shaft that went down two decks of airless open space to the space of level 1
            door = Enumerable.Range(1, 52).SelectMany(x => Enumerable.Range(1, 52).Select(y => Dm.locate<obj_machinery_door_airlock>(locate(x, y, 8)))).Single(d => d != null);
            int dx = (int)door.x, dy = (int)door.y;
            Assert.Null(Dm.locate<obj_structure_fans_tiny>(locate(dx, dy, 8))); // the map has not tg's tiny fan there, which held the deck's air at the door (taken away in the map's own file)
            Assert.IsType<turf_open_floor_iron_airless>(locate(dx, dy + 1, 8)); // a step of airless floor before it
            Assert.IsType<turf_open_space_openspace>(locate(dx, dy + 2, 8));
            Assert.IsType<turf_open_space_openspace>(locate(dx, dy + 2, 7));
            Assert.IsType<turf_open_space_openspace>(locate(1, 1, 8));
            Assert.IsType<turf_open_openspace>(locate(13, 30, 8)); // the open space inside the station keeps its air
            before = Pressure(dx, dy - 1, 8);
            Assert.True(before > 90f, "the deck holds air: " + before);
            Waiting.Detach(door.open(forced: 2));
        }
        w.Run(20 * 4);
        using (w.World.Enter())
        {
            Assert.False(Dm.truthy(door.density));
            float after = Pressure((int)door.x, (int)door.y - 1, 8);
            Assert.True(after < before * 0.9f, $"the deck vents through the open airlock: {before} -> {after} kPa");
        }
    }

    [Fact]
    public void APlayerBlownOutOfAHullBreachDriftsOnIntoSpace()
    {
        string maps = RepoMaps();
        if (maps == null)
            return;
        using RealHeadlessWorld w = Boot(maps);
        RealHeadlessWorld.Player p = w.Join("Blown");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        Assert.IsType<mob_living_carbon_human>(own);
        using (w.World.Enter())
        {
            ADD_TRAIT(own, TRAIT_GODMODE, "test");
            Assert.IsAssignableFrom<turf_closed>(locate(42, 20, 6));
            Assert.IsAssignableFrom<turf_open_space>(locate(43, 20, 6));
            Waiting.NoSleep(own.forceMove(locate(41, 20, 6))); // the ring corridor inside the east hull
        }
        w.Run(40);
        using (w.World.Enter())
            Waiting.NoSleep(locate(42, 20, 6).ChangeTurf(typeof(turf_open_space))); // a breach in the hull, walls on both sides of it
        var xs = new List<int>();
        w.Run(20 * 15, _ =>
        {
            using (w.World.Enter())
            {
                int x = (int)own.x;
                if (xs.Count == 0 || xs[^1] != x)
                    xs.Add(x);
            }
        });
        // tg's walls beside the breach are handholds that stop the drift a tile out; blown, it goes on
        Assert.True(xs[^1] >= 46, "blown east past the hull: " + string.Join(",", xs));
    }

    // the owner (2026-11-18): items and creatures stopped at the hull's edge after a breach instead of flying out into space (DV412)
    [Theory]
    [InlineData(typeof(obj_item_wrench))]
    [InlineData(typeof(obj_structure_closet))]
    [InlineData(typeof(mob_living_basic_mouse))]
    public void AThingBlownOutOfAHullBreachDriftsOnIntoSpace(Type type)
    {
        string maps = RepoMaps();
        if (maps == null)
            return;
        using RealHeadlessWorld w = Boot(maps);
        w.Join("Watcher");
        atom_movable thing;
        using (w.World.Enter())
        {
            thing = (atom_movable)Dm.@new(type, locate(41, 20, 6));
            if (thing is mob_living living)
                ADD_TRAIT(living, TRAIT_GODMODE, "test");
        }
        w.Run(40);
        using (w.World.Enter())
            Waiting.NoSleep(locate(42, 20, 6).ChangeTurf(typeof(turf_open_space)));
        var xs = new List<int>();
        w.Run(20 * 15, _ =>
        {
            using (w.World.Enter())
            {
                int x = (int)thing.x;
                if (xs.Count == 0 || xs[^1] != x)
                    xs.Add(x);
            }
        });
        Assert.True(xs[^1] >= 46, "blown on past the hull: " + string.Join(",", xs));
    }

    // the owner (2026-11-15): the dev menu's delete of a roof tile over the atrium left its air in place: the hole was not linked and nothing woke the air
    [Fact]
    public void ADeletedRoofTileOverTheAtriumVentsItIntoSpace()
    {
        string maps = RepoMaps();
        if (maps == null)
            return;
        using RealHeadlessWorld w = Boot(maps);
        w.Join("Watcher");
        w.Run(20);
        float before;
        using (w.World.Enter())
        {
            Assert.IsType<turf_open_openspace>(locate(28, 32, 8)); // the atrium of the top deck, under the roof (DV436)
            before = Pressure(28, 32, 8);
            Assert.True(before > 90f, "the atrium holds air: " + before);
            DevCommands.Delete(locate(28, 32, 9) as turf);
            Assert.IsType<turf_open_space_openspace>(locate(28, 32, 9));
        }
        w.Run(20 * 2);
        using (w.World.Enter())
            Assert.True(Pressure(28, 32, 8) < before * 0.5f, "the atrium vents up through the hole: " + Pressure(28, 32, 8));
    }

    // the owner (2026-11-15): a noclipping mob's Space and Ctrl stopped at every floor (tg's /mob/living/can_z_move: an incorporeal mob ignores obstacles)
    [Fact]
    public void ANoclipperGoesThroughTheFloorsToEveryLevel()
    {
        string maps = RepoMaps();
        if (maps == null)
            return;
        using RealHeadlessWorld w = Boot(maps);
        RealHeadlessWorld.Player p = w.Join("Ghostly");
        mob_living body = w.MobOf(p);
        using (w.World.Enter())
        {
            DevCommands.Fly(body);
            DevCommands.NoClip(body);
            Waiting.NoSleep(body.forceMove(locate(30, 33, 7)));
        }
        w.Run(20);
        void Press(KeybindingCode key)
        {
            p.Session.Keybinding(key);
            w.Run(30);
            p.Session.Keybinding(key, up: true);
            w.Run(5);
        }
        for (int i = 0; i < 6; i++)
            Press(KeybindingCode.ZLevelUp);
        using (w.World.Enter())
            Assert.Equal(13f, body.z);
        for (int i = 0; i < 12; i++)
            Press(KeybindingCode.ZLevelDown);
        using (w.World.Enter())
            Assert.Equal(1f, body.z);
    }

    // the owner (2026-11-15): climbing the stairs froze the client for half a second (each level's sky mask looked up the whole stack, refilled at
    // every change of level); the one pass down each column gives every cell what IsOpenToSky says
    [Fact]
    public void TheClientsOnePassSkyMaskOfTheStackIsEachCellsOwn()
    {
        string maps = RepoMaps();
        if (maps == null)
            return;
        using RealHeadlessWorld w = Boot(maps);
        RealHeadlessWorld.Player p = w.Join("Skywatcher");
        w.Run(20);
        ReplicaWorld replica = p.Session.Replica;
        var cells = new Anomaly.Client.Shared.Light.ReplicaLightCells(replica, replica.Map.Width, replica.Map.Height);
        int width = cells.MaxX + 1, size = width * (cells.MaxY + 1);
        var levels = Enumerable.Range(0, 13).Select(_ => new byte[size]).ToArray();
        cells.FillSkyStack(levels, 1);
        int open = 0;
        for (int z = 1; z <= 13; z++)
            for (int y = 1; y <= cells.MaxY; y++)
                for (int x = 1; x <= cells.MaxX; x++)
                {
                    bool sky = cells.IsOpenToSky(x, y, z);
                    Assert.True((levels[z - 1][y * width + x] == 255) == sky, $"({x},{y},{z})");
                    open += sky ? 1 : 0;
                }
        Assert.True(open > 0);
        // a column refilled alone writes only its own cells
        foreach (byte[] level in levels)
            Array.Clear(level);
        cells.FillSkyStack(levels, 1, 28, 32, 28, 32);
        Assert.Equal(levels.Sum(l => l.Count(b => b != 0)), Enumerable.Range(1, 13).Count(z => cells.IsOpenToSky(28, 32, z)));
    }
}
