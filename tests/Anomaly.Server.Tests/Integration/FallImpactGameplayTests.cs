using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV435 through the real server: a fall hurts when the body reaches the floor, not when it leaves the level above. The faller is drawn
/// falling under gravity with no jump, cannot steer in the air, and a fall of two levels lands later and hurts more than one of one.
/// </summary>
public class FallImpactGameplayTests
{
    private static string MultizFixture()
    {
        for (string dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tests", "fixtures", "maps", "t2", "multiz");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("tests/fixtures/maps/t2/multiz not found");
    }

    private static float Brute(RealHeadlessWorld w, mob own)
    {
        using (w.World.Enter())
            return Dm.num(((mob_living)own).get_brute_loss());
    }

    [Fact]
    public void AWalkerFallingALevelIsHurtOnlyWhenItReachesTheFloor()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 }, MapJoinTests.MapConfig("multiz", MultizFixture()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Faller");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        Assert.True(own is mob_living_carbon_human, "joined as " + own?.GetType().Name);
        using (w.World.Enter())
            Waiting.NoSleep(own.forceMove(locate(2, 3, 2)));
        w.Run(20);
        float before = Brute(w, own);
        Assert.True(p.Client.Replica.TryGet(p.Client.OwnEntity, out ReplicaEntity drawnOwn));
        double Drawn() => p.Session.Poses.TryGetPose(drawnOwn, out RenderPose pose) ? pose.Z + pose.Rise / TileSpace.LevelHeight : double.NaN;

        p.Yaw = -Math.PI / 2; // east, onto the hole at (3, 3, 2)
        p.Keys = MoveKeys.Forward;
        int leftAt = -1, hurtAt = -1;
        double last = Drawn();
        for (int i = 0; i < 60 && hurtAt < 0; i++)
        {
            w.Run(1);
            bool falling;
            using (w.World.Enter())
                falling = FlightSeam.FallsToImpact(own);
            if (falling && leftAt < 0)
            {
                leftAt = i;
                p.Keys = MoveKeys.Left; // steering in the air does nothing
            }
            float brute = Brute(w, own);
            if (falling)
                Assert.True(brute == before, "hurt in the air at tick " + i);
            else if (brute > before)
                hurtAt = i;
            double h = Drawn();
            Assert.True(Math.Abs(h - last) < 0.45, "the drawn height jumps from " + last + " to " + h + " at tick " + i);
            last = h;
        }
        p.Keys = MoveKeys.None;
        Assert.True(leftAt >= 0, "the walker never fell");
        Assert.True(hurtAt > 0, "the fall never hurt");
        // a level is 2 m: 0.64 s, 13 ticks at 20 a second, from leaving the upper floor to the hit
        Assert.InRange(hurtAt - leftAt, 11, 15);
        using (w.World.Enter())
        {
            Assert.Same(locate(3, 3, 1), own.loc); // it fell straight down: the held key did not move it in the air
            Assert.Equal(10f, Dm.num(((mob_living)own).get_brute_loss()) - before); // living.dm:107, both legs
        }
        w.Run(10);
        Assert.Equal(1, Drawn(), 2);
    }

    [Fact]
    public void AFallFromTheThirdLevelToTheFirstLandsLaterAndHurtsMoreThanOneLevel()
    {
        string maps = MultizDebugBreachTests.RepoMaps();
        if (maps == null)
            return; // the converted maps are not here (tools/convert-maps.sh)
        using RealHeadlessWorld w = MultizDebugBreachTests.Boot(maps);
        RealHeadlessWorld.Player p = w.Join("Faller");
        mob own = w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o as mob : null;
        Assert.True(own is mob_living_carbon_human, "joined as " + own?.GetType().Name);
        // a shaft inside the station: the open space of level 8 at (13, 30), opened down to a bare floor on level 6 (DV436: the decks are levels 6 to 8)
        using (w.World.Enter())
        {
            Assert.IsType<turf_open_openspace>(locate(13, 30, 8));
            if (locate(13, 30, 7) is not turf_open_openspace)
                Waiting.NoSleep(locate(13, 30, 7).ChangeTurf(typeof(turf_open_openspace)));
            Waiting.NoSleep(locate(13, 30, 6).ChangeTurf(typeof(turf_open_floor_iron)));
            foreach (int z in new[] { 6, 7 })
                foreach (obj thing in Dm.each<obj>(locate(13, 30, z)).ToList())
                    qdel(thing); // the window under the hole would take the fall
        }
        w.Run(20);
        float before = Brute(w, own);
        using (w.World.Enter())
            Waiting.NoSleep(own.forceMove(locate(13, 30, 8)));
        int hurtAt = -1;
        bool passedLevel2 = false;
        for (int i = 0; i < 60 && hurtAt < 0; i++)
        {
            w.Run(1);
            using (w.World.Enter())
            {
                passedLevel2 |= Dm.num(own.z) == 7;
                if (FlightSeam.FallsToImpact(own))
                    Assert.True(Dm.num(((mob_living)own).get_brute_loss()) == before, "hurt in the air at tick " + i);
                else if (Dm.num(((mob_living)own).get_brute_loss()) > before)
                    hurtAt = i;
            }
        }
        Assert.True(passedLevel2, "the faller never passed level 2");
        // two levels, 4 m: 0.9 s, 18 ticks
        Assert.InRange(hurtAt, 16, 20);
        using (w.World.Enter())
        {
            Assert.Same(locate(13, 30, 6), own.loc);
            Assert.True(Dm.num(((mob_living)own).get_brute_loss()) - before > 10f, "two levels hurt more than one level's 10");
        }
    }
}
