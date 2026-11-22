using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Server.Tests.Integration.DragGestureGameplayTests;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8z slice C (D238, D239) on MetaStation through the real server and two predicting clients: Alice climbs onto a table by the radial's
/// Climb (a drop of herself onto it, tg's 2 s and the 2 s stun; Bob reads name + " climbs onto the table."), walks onto the next table and
/// steps down to the floor with no prediction correction, while Bob walking into the same table is stopped by it as before. She vaults a
/// railing both ways, climbs a closed crate (no stun) and, starting to climb the table again, is knocked off by Bob's click.
/// </summary>
public class ClimbGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        Do(w, () => Waiting.NoSleep(m.forceMove(locate(x, y, m.z))));
        w.Run(Second);
    }

    private static (int X, int Y) Tile(RealHeadlessWorld w, mob m) => In(w, () => ((int)m.x, (int)m.y));

    private static bool Free(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // Two plain tables side by side (west, east) with free floor west of them and a free row south of all three: (the west table, floor x, y).
    private static (obj_structure_table West, obj_structure_table East, int X, int Y) TablePair(RealHeadlessWorld w, int z) => In(w, () =>
    {
        foreach (obj_structure_table table in Dm.each<obj_structure_table>(Dm.world).Where(t => Dm.eq(t.z, z) && t.GetType() == typeof(obj_structure_table)))
        {
            int x = (int)Dm.num(table.x), y = (int)Dm.num(table.y);
            obj_structure_table east = Dm.each_cast<atom>(((turf)locate(x + 1, y, z)).contents).OfType<obj_structure_table>().FirstOrDefault();
            if (east == null || east.GetType() != typeof(obj_structure_table) || Dm.each_cast<atom>(((turf)locate(x, y, z)).contents).Count(a => Dm.truthy(a.density)) != 1
                || Dm.each_cast<atom>(((turf)locate(x + 1, y, z)).contents).Count(a => Dm.truthy(a.density)) != 1)
                continue;
            if (Free((turf)locate(x - 1, y, z)) && Free((turf)locate(x - 2, y, z)) && Free((turf)locate(x - 1, y - 1, z)))
                return (table, east, x - 1, y);
        }
        return (null, null, 0, 0);
    });

    // A free 3x3 floor patch on z: its centre.
    private static (int X, int Y) OpenPatch(RealHeadlessWorld w, int z, int nearX, int nearY) => In(w, () =>
    {
        for (int r = 1; r < 60; r++)
        {
            for (int x = nearX - r; x <= nearX + r; x++)
            {
                for (int y = nearY - r; y <= nearY + r; y++)
                {
                    bool ok = true;
                    for (int dx = -1; dx <= 1 && ok; dx++)
                        for (int dy = -1; dy <= 1 && ok; dy++)
                            ok = locate(x + dx, y + dy, z) is turf t && Free(t);
                    if (ok)
                        return (x, y);
                }
            }
        }
        return (0, 0);
    });

    private static void Climb(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob me, atom thing)
    {
        Assert.True(p.Session.Drop(new DragRelease(Entity(me.dm_id), Entity(thing.dm_id), ClickButton.Left, ClickModifiers.None, 255))); // the radial's choice
        w.Run(3); // the texts reach the clients
    }

    [MetaServerFact]
    public void A_player_climbs_a_table_walks_over_tables_vaults_a_railing_climbs_a_crate_and_is_knocked_off()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 37, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice"), bob = w.Join("Bob");
        var a = (mob_living_carbon_human)w.MobOf(alice);
        var b = (mob_living_carbon_human)w.MobOf(bob);
        int z = In(w, () => (int)a.z);
        string name = In(w, () => Dm.text("[]", a.name));
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var aliceTexts = new List<ServerText>();
        var bobTexts = new List<ServerText>();
        alice.Client.ServerTextReceived += aliceTexts.Add;
        bob.Client.ServerTextReceived += bobTexts.Add;
        var hints = new List<ContextHintsMessage>();
        alice.Client.ContextHintsReceived += hints.Add;
        static bool Told(List<ServerText> texts, string text) => texts.Any(t => t.Html.Contains(text, StringComparison.Ordinal));
        w.Run(Second);

        // ---- 1. The radial on a table offers Climb (a drop of herself onto it); tg's 2 s, then she stands on it, stunned for 2 s.
        (obj_structure_table t1, obj_structure_table t2, int fx, int fy) = TablePair(w, z);
        Assert.NotNull(t1);
        output.WriteLine($"tables at ({fx + 1},{fy}) and ({fx + 2},{fy})");
        Put(w, a, fx, fy);
        Put(w, b, fx, fy - 1);
        alice.Yaw = 0;
        bob.Yaw = 0;
        hints.Clear();
        w.Run(4);
        alice.Session.Rehover();
        alice.Session.Hover(Entity(t1.dm_id), w.Now);
        w.Run(4);
        ContextHintsMessage answer = hints.LastOrDefault(h => h.Target == t1.dm_id);
        Assert.NotNull(answer);
        RadialEntry climb = RadialModel.Build(answer, 0, true).Single(r => r.Label == "Climb");
        Assert.Equal(RadialKind.DropSelf, climb.Kind);

        Climb(w, alice, a, t1);
        Assert.True(Told(aliceTexts, "You start climbing onto the table..."));
        Assert.True(Told(bobTexts, name + " starts climbing onto the table."));
        w.Run(Second * 3 / 2 - 3);
        Assert.Equal((fx, fy), Tile(w, a)); // 1.5 s: still climbing
        w.Run(Second);
        Assert.Equal((fx + 1, fy), Tile(w, a));
        Assert.True(Told(aliceTexts, "You climb onto the table."));
        Assert.True(Told(bobTexts, name + " climbs onto the table."));
        Assert.True(In(w, () => a.IsStun() != null && HAS_TRAIT(a, TRAIT_ON_CLIMBABLE)));
        w.Run(Second * 2);
        Assert.Null(In(w, () => a.IsStun()));
        output.WriteLine("on the table");

        // ---- 2. She walks onto the next table and back down to the floor: the predictor walks with the server (D239), no correction.
        w.Run(Second);
        long before = alice.Counters.Corrections + alice.Counters.Snaps + alice.Counters.Mismatches;
        alice.Keys = MoveKeys.Right;
        for (int i = 0; i < Second && Tile(w, a).X != fx + 2; i++)
            w.Run(1);
        alice.Keys = MoveKeys.None;
        w.Run(Second / 2);
        Assert.Equal((fx + 2, fy), Tile(w, a));
        Assert.True(In(w, () => HAS_TRAIT(a, TRAIT_ON_CLIMBABLE)));
        Assert.True(In(w, () => LocomotionSeam.IsClimbWalkable(w.World, fx + 2, fy, z) && LocomotionSeam.IsSolid(w.World, fx + 2, fy, z)));
        alice.Keys = MoveKeys.Left;
        for (int i = 0; i < 2 * Second && Tile(w, a).X != fx - 1; i++)
            w.Run(1);
        alice.Keys = MoveKeys.None;
        w.Run(Second);
        Assert.Equal((fx - 1, fy), Tile(w, a));
        Assert.False(In(w, () => HAS_TRAIT(a, TRAIT_ON_CLIMBABLE)));
        MotionState server = w.ServerState(alice);
        Assert.Equal((server.X, server.Y), (alice.Session.Prediction.Predicted.X, alice.Session.Prediction.Predicted.Y));
        Assert.Equal(before, alice.Counters.Corrections + alice.Counters.Snaps + alice.Counters.Mismatches);
        output.WriteLine($"walked over the tables and down, corrections {alice.Counters.Corrections}, snaps {alice.Counters.Snaps}");

        // ---- 3. Back on the floor she cannot walk onto the table; nor can Bob, who never climbed (regression: a table is a wall to a walker).
        Put(w, b, fx, fy);
        long bobBefore = bob.Counters.Corrections + bob.Counters.Snaps + bob.Counters.Mismatches;
        bob.Keys = MoveKeys.Right;
        w.Run(Second);
        bob.Keys = MoveKeys.None;
        w.Run(Second / 2);
        Assert.Equal((fx, fy), Tile(w, b));
        Assert.Equal(bobBefore, bob.Counters.Corrections + bob.Counters.Snaps + bob.Counters.Mismatches);
        Put(w, b, fx, fy - 1);

        // ---- 4. A railing (dir south) on a free patch: from its own tile she vaults south over it, from the south back north onto its tile.
        (int px, int py) = OpenPatch(w, z, fx, fy);
        Assert.NotEqual(0, px);
        var railing = In(w, () => Dm.@new<obj_structure_railing>(locate(px, py, z)));
        w.Run(2);
        Assert.Equal((int)Dm.SOUTH, In(w, () => (int)Dm.num(railing.dir)));
        Put(w, a, px, py);
        Climb(w, alice, a, railing);
        w.Run(Second * 5 / 2);
        Assert.Equal((px, py - 1), Tile(w, a));
        w.Run(Second * 2); // the stun
        Climb(w, alice, a, railing);
        w.Run(Second * 5 / 2);
        Assert.Equal((px, py), Tile(w, a));
        Do(w, () => qdel(railing));
        w.Run(Second * 2);
        output.WriteLine("railing vaulted both ways");

        // ---- 5. A closed crate: climbed in tg's crate_climb_time, no stun; she stands on it and can step off.
        var crate = In(w, () => Dm.@new<obj_structure_closet_crate>(locate(px + 1, py, z)));
        w.Run(2);
        float crateTime = In(w, () => Dm.num(crate.crate_climb_time));
        Climb(w, alice, a, crate);
        w.Run((int)(crateTime / 10 * Second) + Second / 2);
        Assert.Equal((px + 1, py), Tile(w, a));
        Assert.True(In(w, () => a.IsStun() == null && HAS_TRAIT(a, TRAIT_ON_CLIMBABLE)));
        Assert.True(In(w, () => LocomotionSeam.IsClimbWalkable(w.World, px + 1, py, z)));
        alice.Keys = MoveKeys.Left;
        for (int i = 0; i < Second && Tile(w, a).X != px; i++)
            w.Run(1);
        alice.Keys = MoveKeys.None;
        w.Run(Second / 2);
        Assert.Equal((px, py), Tile(w, a));
        output.WriteLine("crate climbed");

        // ---- 6. She starts climbing the table again; Bob's click on it knocks her off (tg's 4 s paralysis, the line to both).
        Put(w, a, fx, fy);
        Climb(w, alice, a, t1);
        w.Run(Second / 2);
        Assert.True(bob.Session.Click(t1.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.True(Told(aliceTexts, "You're knocked off the table!"));
        Assert.True(Told(bobTexts, name + " is knocked off the table."));
        Assert.True(In(w, () => a.IsParalyzed() != null));
        w.Run(Second * 2);
        Assert.Equal((fx, fy), Tile(w, a));
        output.WriteLine("knocked off");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
