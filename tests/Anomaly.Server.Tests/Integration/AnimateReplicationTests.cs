using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Protocol version 9 (M4v slice 1, D143): BYOND's animate() chains reach the clients as the entity's Animation field, with the
/// vars' values before the call as the chain's start (docs/ai/specs/protocol.md section 18).
/// </summary>
public class AnimateReplicationTests(ITestOutputHelper output)
{
    [Fact]
    public void A_dropped_item_glides_down_from_the_hand_on_the_replica()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob_living aliceMob = w.MobOf(alice);
        w.Run(10);
        obj_item_crowbar crowbar;
        using (w.World.Enter())
        {
            crowbar = Dm.@new<obj_item_crowbar>(aliceMob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_hands(crowbar))));
        }
        w.Run(5);
        Assert.DoesNotContain(alice.Session.Replica.Entities, e => e.Id == crowbar.dm_id); // in the hand: no entity
        long droppedAt;
        using (w.World.Enter())
        {
            // tg's dropItemToGround (inventory.dm) ends in do_drop_animation (items.dm:1563): from the mob's tile it starts 10 pixels up,
            // 6 to a side, transparent, turned 30 degrees and shrunk to 0.7, and animates back over 3 ds with CUBIC_EASING
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.dropItemToGround(crowbar))));
            droppedAt = w.World.Tick;
        }
        EntityAnimation seen = null;
        w.Run(3, _ => seen ??= alice.Session.Replica.Entities.SingleOrDefault(e => e.Id == crowbar.dm_id)?.Animation);
        Assert.NotNull(seen);
        AnimationTrack track = Assert.Single(seen.Tracks);
        output.WriteLine($"dropped at {droppedAt}, track from {track.StartTick}: start {track.Start}, step {track.Steps[0]}");
        Assert.False(track.Parallel);
        Assert.Equal(1, track.Loop);
        Assert.True(track.StartTick >= droppedAt && track.StartTick <= droppedAt + 1);
        AnimationStep step = Assert.Single(track.Steps);
        Assert.Equal(3f, step.Time);
        Assert.Equal(AnimationEasing.Cubic, step.Easing);
        Assert.Equal(AnimatedVar.PixelX | AnimatedVar.PixelY | AnimatedVar.Alpha | AnimatedVar.Transform, step.Vars.Mask);
        Assert.Equal(step.Vars.Mask, track.Start.Mask);
        Assert.Equal(0f, track.Start.Alpha);
        Assert.Equal(255f, step.Vars.Alpha);
        Assert.Equal(10f, track.Start.PixelY);
        Assert.Equal(6f, Math.Abs(track.Start.PixelX));
        Assert.Equal(0.7f, MathF.Sqrt(track.Start.A * track.Start.A + track.Start.D * track.Start.D), 3);
        Assert.Equal(1f, step.Vars.A, 3);

        // The animation ends: the field goes back to none, and a late joiner gets none.
        w.Run(MotionMath.TicksPerSecond);
        Assert.Null(alice.Session.Replica.Entities.Single(e => e.Id == crowbar.dm_id).Animation);
    }

    [Fact]
    public void A_staggered_mob_sways_on_the_replica()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living aliceMob = w.MobOf(alice);
        w.Run(10);
        using (w.World.Enter())
            Waiting.NoSleep(aliceMob.adjust_staggered(5 * SECONDS));
        AnimationTrack sway = null;
        w.Run(60, _ =>
        {
            EntityAnimation a = bob.Session.Replica.Entities.SingleOrDefault(e => e.Id == aliceMob.dm_id)?.Animation;
            sway ??= a?.Tracks.FirstOrDefault(t => t.Steps.Length == 3 && t.Steps[0].Vars.Has(AnimatedVar.PixelW));
        });
        Assert.NotNull(sway);
        output.WriteLine($"sway from {sway.StartTick}: {string.Join(", ", sway.Steps.Select(s => s.Vars.PixelW + " over " + s.Time))}");
        // tg's do_stagger_animation (staggered.dm:45): pixel_w +3, -6, +3 relative, a parallel chain over 0.8 s
        Assert.True(sway.Parallel);
        Assert.Equal(new[] { 3f, -6f, 3f }, sway.Steps.Select(s => s.Vars.PixelW));
        Assert.Equal(new[] { 2f, 4f, 2f }, sway.Steps.Select(s => s.Time));
        Assert.All(sway.Steps, s => Assert.Equal(AnimationStepFlags.Relative, s.Flags));
        Assert.True(sway.Start.Has(AnimatedVar.PixelW));
    }
}
