using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate stage G4b (D316) through the real server and two real clients: Alice holds the meat hook and clicks Bob four tiles away;
/// the hook flies, snags him and reels him in to her, and her client gets the chain's beam while it pulls.
/// </summary>
public class MeatHookGameplayTests(ITestOutputHelper output)
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    // Alice's camera ray from her eye to the point (east, north, up) in tile units and metres up (AimedShotGameplayTests.AimAt)
    private static ShotAim AimAt(RealHeadlessWorld.Player p, double x, double y, double up)
    {
        (float ox, float oy, int z) = p.Session.Prediction.RenderPosition();
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (x - 0.5) * 2 - eastE, dy = (y - 0.5) * 2 - northE, dz = up - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
    }

    [Fact]
    public void Alice_hooks_Bob_and_reels_him_in()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 51, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        alice.Yaw = -Math.PI / 2; // Alice faces east, toward Bob (Godot's yaw turns left)
        var hook = In(w, () =>
        {
            turf start = get_turf(aliceMob);
            Waiting.NoSleep(bobMob.forceMove(locate(Dm.num(start.x) + 4, Dm.num(start.y), Dm.num(start.z))));
            var h = Dm.@new<obj_item_gun_magic_hook>(start);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_active_hand(h))));
            return h;
        });
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;

        ShotAim aim = AimAt(alice, Dm.num(bobMob.x), Dm.num(bobMob.y), 1.0);
        bool chain = false;
        void Watch(long _) => chain |= alice.Session.Replica.Entities.Any(e => (e.TypePath ?? "").StartsWith("/obj/effect/ebeam", StringComparison.Ordinal));
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18), ClickButton.Left, ClickModifiers.None, aim));
        w.Run(40, Watch);

        output.WriteLine($"Alice at {In(w, () => aliceMob.x)}, Bob at {In(w, () => bobMob.x)}, charges {In(w, () => hook.charges)}");
        Assert.Equal(0f, In(w, () => hook.charges));
        Assert.True(In(w, () => get_dist(aliceMob, bobMob) <= 1), "Bob was not reeled in");
        Assert.True(In(w, () => Dm.num(bobMob.get_brute_loss()) > 0));
        Assert.True(chain, "Alice's client got no chain beam");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
