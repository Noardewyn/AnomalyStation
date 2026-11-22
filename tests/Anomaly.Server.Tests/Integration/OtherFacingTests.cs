using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Xunit.Abstractions;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's report (2026-10-09): another player's billboard always faces the viewer. What a second client receives of a
/// player who turns: the capsule's yaw (the picture's facing, BillboardMath.ChooseDir) and tg's dir.
/// </summary>
public class OtherFacingTests(ITestOutputHelper output)
{
    [Fact]
    public void A_second_client_sees_the_other_players_yaw_and_dir_follow_the_view()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 3, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living bobMob = w.MobOf(bob);
        w.Run(10);
        foreach ((double yaw, byte expectYaw, byte expectDir) in new[] { (-Math.PI / 2, (byte)64, (byte)4), (Math.PI, (byte)128, (byte)2), (Math.PI / 2, (byte)192, (byte)8) })
        {
            bob.Yaw = yaw;
            bob.Keys = MoveKeys.Forward;
            w.Run(6);
            bob.Keys = MoveKeys.None;
            w.Run(10);
            ReplicaEntity seen = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
            Assert.True(seen.HasPos, "Bob is a capsule mob for Alice");
            EntitySample s = seen.Sample(0);
            Assert.InRange(s.Yaw, (byte)(expectYaw - 4), (byte)(expectYaw + 4));
            Assert.Equal(expectDir, seen.State.Dir);
        }
        // The picture's direction count, as the client's billboard asks it (BillboardMath.ChooseDir draws south below 2).
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root != null)
        {
            ReplicaEntity seenBob = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
            int dirs = compositor.Directions(new Tables(alice.Session.Replica), seenBob.State.Appearance);
            output.WriteLine($"Bob's picture has {dirs} directions");
            Assert.True(dirs >= 4, $"Bob's picture has {dirs} directions");
            var tables = new Tables(alice.Session.Replica);
            Assert.True(compositor.TryCompose(tables, seenBob.State.Appearance, 2, 0, out ComposedRaster south));
            byte[] southPixels = (byte[])south.Image.Pixels.Clone();
            Assert.True(compositor.TryCompose(tables, seenBob.State.Appearance, 1, 0, out ComposedRaster north));
            Assert.False(southPixels.AsSpan().SequenceEqual(north.Image.Pixels), "Bob drawn from behind (north) differs from his front (south)");
        }
    }

    // The client's ReplicaTables (godot/scripts/Render, not in this test project): the replica's tables for the compositor.
    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }
}
