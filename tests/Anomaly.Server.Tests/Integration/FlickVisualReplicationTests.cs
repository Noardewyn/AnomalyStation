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
/// Protocol version 10 (M4v slice 3, D143): tg's short-lived overlay images (flick_overlay_view: a flick visual in an atom's
/// vis_contents) reach the clients as entities drawn on their host (NetField.VisHost), with their look and animate() chain, and
/// leave when tg deletes them (docs/ai/specs/protocol.md section 19).
/// </summary>
public class FlickVisualReplicationTests(ITestOutputHelper output)
{
    private static string StateOf(RealHeadlessWorld.Player p, ReplicaEntity e)
    {
        Assert.True(p.Session.Replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry look));
        return p.Session.Replica.ResolveString(look.IconState);
    }

    // A visual that is not a human's eyelid (M10x: the eyelids are visuals of every human, drawn in its picture)
    private static bool Flicked(ReplicaEntity e) => e.IsVisual && e.TypePath != "/obj/effect/abstract/eyelid_effect";

    [Fact]
    public void A_pickup_shows_the_flying_image_on_the_floor_tile_to_the_viewers_and_it_expires()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living aliceMob = w.MobOf(alice);
        w.Run(10);
        obj_item_crowbar crowbar;
        int tileX, tileY;
        using (w.World.Enter())
        {
            turf next = get_step(aliceMob, 4) as turf; // east of Alice
            crowbar = Dm.@new<obj_item_crowbar>(next);
            tileX = (int)next.x;
            tileY = (int)next.y;
        }
        w.Run(3);
        long pickedAt;
        using (w.World.Enter())
        {
            // tg's put_in_hand with the animation (inventory.dm:182): do_pickup_animation (items.dm:1508) flies the item's
            // image from its tile toward the picker: source.flick_overlay_view(image, 0.4 SECONDS) and a chain of two steps
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_hand(crowbar, 1, ignore_anim: Dm.FALSE))));
            pickedAt = w.World.Tick;
        }
        ReplicaEntity seen = null;
        EntityAnimation chain = null;
        string state = null;
        w.Run(3, _ =>
        {
            seen ??= bob.Session.Replica.Entities.FirstOrDefault(e => Flicked(e));
            if (seen != null)
            {
                chain ??= seen.Animation;
                state ??= StateOf(bob, seen);
            }
        });
        Assert.NotNull(seen);
        long visualId = seen.Id;
        output.WriteLine($"picked at {pickedAt}: visual {visualId} on host {seen.VisHost} at ({seen.State.X},{seen.State.Y}), {state}, {chain?.Tracks.Length} track(s)");
        Assert.Equal(0, seen.VisHost); // on the turf
        Assert.Equal((tileX, tileY), (seen.State.X, seen.State.Y));
        Assert.Equal("crowbar", state);
        Assert.Contains(alice.Session.Replica.Entities, e => e.Id == visualId); // every viewer gets it
        Assert.NotNull(chain);
        AnimationTrack track = Assert.Single(chain.Tracks);
        Assert.Equal(2, track.Steps.Length);
        Assert.Equal(3f, track.Steps[0].Time);
        Assert.Equal(AnimationEasing.Cubic, track.Steps[0].Easing);
        Assert.Equal(-32f, track.Steps[0].Vars.PixelX); // toward Alice, a tile west
        Assert.Equal(175f, track.Steps[0].Vars.Alpha);
        Assert.Equal(0f, track.Steps[1].Vars.Alpha);
        Assert.True(bob.Session.Replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        Assert.Equal(0, look.Alpha); // the appearance holds the chain's end (BYOND)

        w.Run(MotionMath.TicksPerSecond / 2 + 2); // 0.4 s
        Assert.DoesNotContain(bob.Session.Replica.Entities, e => e.Id == visualId);
        Assert.DoesNotContain(alice.Session.Replica.Entities, e => Flicked(e));
    }

    [Fact]
    public void A_pressed_button_shows_its_flash_on_the_button_and_it_expires()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living aliceMob = w.MobOf(alice);
        w.Run(10);
        obj_machinery_button_door button;
        using (w.World.Enter())
        {
            button = Dm.@new<obj_machinery_button_door>(get_step(aliceMob, 4));
            button.req_access = Dm.list(ACCESS_CAPTAIN);
        }
        w.Run(3);
        using (w.World.Enter())
        {
            button.machine_stat = 0; // powered, whatever the test room's area says
            // tg's attempt_press without access (buttons.dm:301-304): flick_overlay_view("[base_icon_state]-overlay-error", 1 SECONDS)
            Waiting.NoSleep(button.attempt_press(aliceMob));
        }
        ReplicaEntity seen = null;
        string state = null;
        w.Run(3, _ =>
        {
            seen ??= bob.Session.Replica.Entities.FirstOrDefault(e => Flicked(e));
            if (seen != null) state ??= StateOf(bob, seen);
        });
        Assert.NotNull(seen);
        output.WriteLine($"visual {seen.Id} on host {seen.VisHost} (the button is {button.dm_id}): {state}");
        Assert.Equal(button.dm_id, seen.VisHost);
        Assert.Equal("button-overlay-error", state);
        Assert.Null(seen.Animation);
        long visualId = seen.Id;
        using (w.World.Enter())
            Assert.Equal(0f, button.vis_contents.len - 1); // the flash is in the button's vis_contents

        // The button's own appearance does not draw the flash (it is an entity of its own).
        ReplicaEntity host = bob.Session.Replica.Entities.Single(e => e.Id == button.dm_id);
        Assert.True(bob.Session.Replica.TryGetAppearance(host.State.Appearance, out AppearanceEntry hostLook));
        foreach (TableRef overlay in hostLook.Overlays)
        {
            Assert.True(bob.Session.Replica.TryGetAppearance(overlay, out AppearanceEntry o));
            Assert.NotEqual("button-overlay-error", bob.Session.Replica.ResolveString(o.IconState));
        }

        w.Run(MotionMath.TicksPerSecond + 2);
        Assert.DoesNotContain(bob.Session.Replica.Entities, e => e.Id == visualId);
        using (w.World.Enter())
            Assert.Equal(0f, button.vis_contents.len);
    }
}
