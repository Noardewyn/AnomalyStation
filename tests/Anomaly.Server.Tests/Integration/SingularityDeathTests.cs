using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's M5 play test (2026-10-16): a singularity killed the player, the client dropped with "bad Delta (CountTooLarge)"
/// and every reconnect failed the same way. Through the real server on MetaStation: a stage three singularity beside the player
/// eats them; their client stays connected, and a second client joining afterwards plays too.
/// </summary>
public class SingularityDeathTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public SingularityDeathTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [MetaServerTheory]
    [InlineData(600f)]
    [InlineData(2200f)]
    public void A_player_the_singularity_eats_stays_connected_and_a_new_client_still_joins(float energy)
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 71, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        mob_living me = w.MobOf(p);
        using (w.World.Enter())
            _output.WriteLine(DevCommands.Singularity((turf)Builtins.locate(me.x + 2, me.y, me.z), energy));
        for (int s = 0; s < 60 && p.Client.Phase == FeedPhase.Playing; s++)
        {
            // Dead, the player presses a movement key (tg's client/Move ghostizes them) and flies about.
            p.Keys = In(w, () => Dm.num(me.stat)) >= 3 /* hard crit or dead */ && s % 4 < 2 ? MoveKeys.Forward : MoveKeys.None;
            p.Yaw = s * 0.7;
            w.Run(Second);
        }
        _output.WriteLine($"Alice: {p.Client.Phase} {p.Client.CloseReason}; her body {In(w, () => me.stat)}, her mob now {In(w, () => w.MobOf(p)?.GetType().Name)}, deleted {In(w, () => GlobalProcs.QDELETED(me))}");
        Assert.Equal(FeedPhase.Playing, p.Client.Phase);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        w.Run(5 * Second);
        Assert.Equal(FeedPhase.Playing, bob.Client.Phase);
    }

    // The owner's play test (2026-10-16): the ghost was far too fast (tg moves it every tick). DV54: ten tiles a second, DV68: 30 per cent
    // less, flown as a capsule (2026-10-19); and the ghost's client is told where its body lies.
    [MetaServerFact]
    public void A_ghost_flies_seven_tiles_a_second_and_its_client_is_told_where_its_body_lies()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 73, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        mob_living body = w.MobOf(p);
        var cell = In(w, () => new MapCell((int)body.x, (int)body.y, (int)body.z));
        using (w.World.Enter())
            Waiting.NoSleep(body.death());
        w.Run(5);
        p.Yaw = Math.PI / 2; // west
        p.Keys = MoveKeys.Forward;
        w.Run(2);
        mob ghost = In(w, () => body.mind?.current == body ? (mob)Dm.each(Dm.world).OfType<mob_dead_observer>().FirstOrDefault() : null);
        Assert.NotNull(ghost);
        float startX = In(w, () => ghost.x);
        w.Run(2 * Second);
        p.Keys = MoveKeys.None;
        w.Run(3);
        float tiles = startX - In(w, () => ghost.x);
        _output.WriteLine($"the ghost flew {tiles} tiles in 2 s; its client marks the body at {p.Session.Replica.OwnBody} (the body at {cell})");
        Assert.InRange(tiles, 12.5f, 15.5f); // DV68: 0.35 tiles a tick (tg's every tick would be 40 and more)
        Assert.Equal(cell, p.Session.Replica.OwnBody);
    }
}
