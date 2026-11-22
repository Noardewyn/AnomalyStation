using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Pushing (the owner, 2026-10-29: a pushed crate mispredicted), PullPredictionTests' rig: the player walks north into a crate, tg's
/// PushAM moves the crate a tile (living.dm:385; DV535: the capsule walks on after it), again each move delay. The server flags what a mob pushes (EntityFlags.Pushable) and the client guesses the push (OwnGuesses.OnPush), so
/// the pusher is never corrected and the pushed thing is drawn moving at once.
/// </summary>
public class PushPredictionTests
{
    private readonly ITestOutputHelper _output;

    public PushPredictionTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void PushingACrateCostsThePusherNoCorrection(int latencySteps)
        => Push(latencySteps, at => Dm.@new<obj_structure_closet_crate>(at), pushed: true);

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void PushingACanisterCostsThePusherNoCorrection(int latencySteps)
        => Push(latencySteps, at => Dm.@new<obj_machinery_portable_atmospherics_canister>(at), pushed: true);

    // set_anchored: the server's Pushable flag goes off (the sweep or the next read), the client guesses no push and walks into it as into a wall
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void AnAnchoredCrateIsNotGuessedPushed(int latencySteps)
        => Push(latencySteps, at =>
        {
            var crate = Dm.@new<obj_structure_closet_crate>(at);
            Waiting.NoSleep(crate.set_anchored(1));
            return crate;
        }, pushed: false);

    private void Push(int latencySteps, Func<turf, atom_movable> make, bool pushed)
    {
        var net = new LoopbackOptions { Seed = 5, TimeoutSteps = 400, MinLatencySteps = latencySteps, MaxLatencySteps = latencySteps };
        using var w = new RealHeadlessWorld(net, new ServerConfig { Seed = 7, RoomWidth = 32, RoomHeight = 32, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f });
        RealHeadlessWorld.Player p = w.Join("Pusher", inputBufferTarget: latencySteps > 0 ? 2 : 1);
        mob_living me = w.MobOf(p);
        (int x, int y) = In(w, () =>
        {
            bool Open(int cx, int cy) => Builtins.locate(cx, cy, 1) is turf_open_indestructible t && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));
            for (int cy = 2; cy + 10 <= (int)Dm.world.maxy; cy++)
                for (int cx = 2; cx < (int)Dm.world.maxx; cx++)
                    if (Enumerable.Range(0, 10).All(dy => Open(cx, cy + dy)))
                        return (cx, cy);
            throw new InvalidOperationException("no open 1 x 10 floor");
        });
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(x, y, 1)));
        atom_movable thing = In(w, () => make((turf)Builtins.locate(x, y + 1, 1)));
        w.Run(400); // a full sweep of the replicator: an anchored var set raw is seen
        long corrections = p.Counters.Corrections, snaps = p.Counters.Snaps;
        p.Yaw = 0;
        p.Keys = MoveKeys.Forward;
        float maxError = 0;
        bool drawnAhead = false;
        w.Run(30, _ =>
        {
            maxError = Math.Max(maxError, p.Session.Prediction.TryGetLastReconciliation(out Reconciliation r) ? r.ErrorTiles : 0);
            if (p.Session.Replica.TryGet(thing.dm_id, out ReplicaEntity e) && e.State.Y > e.ServerState.Y)
                drawnAhead = true;
        });
        p.Keys = MoveKeys.None;
        w.Run(15);
        int thingY = In(w, () => (int)Dm.num(thing.y)), meY = In(w, () => (int)Dm.num(me.y));
        long c = p.Counters.Corrections - corrections, s = p.Counters.Snaps - snaps;
        _output.WriteLine($"{latencySteps * 50} ms: me y {y} -> {meY}, {thing.GetType().Name} {y + 1} -> {thingY}; corrections {c}, snaps {s}, max error {maxError:0.000}, drawn ahead {drawnAhead}");
        if (pushed)
        {
            Assert.True(thingY >= y + 3, "it was pushed");
            Assert.InRange(meY, thingY - 2, thingY - 1); // DV535: the pusher walks on into the freed tile itself, so it may stop a tile short of it
            Assert.True(drawnAhead); // drawn where it is pushed before the server's Delta says so (the input runs a frame ahead even on localhost)
        }
        else
        {
            Assert.Equal(y + 1, thingY);
            Assert.Equal(y, meY);
            Assert.False(drawnAhead);
        }
        Assert.Equal(0, c); // the push and the step after it were predicted (a 1-tile correction each push before)
        Assert.Equal(0, s);
        Assert.Equal(0f, maxError);
        Assert.Equal(0, p.Session.Guesses.PendingPushes);
        Assert.False(p.Session.Replica.HasGuesses);
        MotionState server = w.ServerState(p);
        Assert.Equal((server.X, server.Y), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
