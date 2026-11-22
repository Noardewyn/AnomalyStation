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
/// M7x slice D (full auto, docs/ai/specs/weapons.md 3) on a real server with two players: Alice holds a WT-550 and presses the left button
/// on Bob (the client's MouseDown with her camera's aim): the autorifle fires at tg's rate, one shot every 0.3 seconds, while the button is
/// held, each bullet aimed by her camera, and stops on the release (the MouseUp). The press fires no extra click shot (tg's full auto
/// takes over the click).
/// </summary>
public class FullAutoGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

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

    private static ShotAim AimAt(RealHeadlessWorld.Player p, double x, double y, double up)
    {
        (float ox, float oy, int z) = p.Session.Prediction.RenderPosition();
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (x - 0.5) * 2 - eastE, dy = (y - 0.5) * 2 - northE, dz = up - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
    }

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    [Fact]
    public void A_wt550_fires_every_three_deciseconds_while_the_button_is_held_and_stops_when_it_is_released()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 62, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var heardBob = new List<SoundEvent>();
        bob.Session.Replica.SoundsReceived += events => heardBob.AddRange(events);
        w.Run(10);
        turf at = In(w, () =>
        {
            for (int tiles = 6; tiles >= 3; tiles--) // the farthest clear line (the full auto fires at a man at least two tiles away)
            {
                foreach (int dir in new[] { EAST, WEST, NORTH, SOUTH })
                {
                    turf t = get_turf(aliceMob);
                    bool ok = true;
                    for (int i = 0; i < tiles && ok; i++)
                    {
                        t = get_step(t, dir) as turf;
                        ok = t != null && Clear(t);
                    }
                    if (ok)
                        return t;
                }
            }
            return null;
        });
        Assert.NotNull(at);
        In(w, () => Waiting.NoSleep(bobMob.forceMove(at)));
        w.Run(10);
        obj_item_gun_ballistic_automatic_wt550 gun = In(w, () =>
        {
            var g = Dm.@new<obj_item_gun_ballistic_automatic_wt550>(get_turf(aliceMob));
            Waiting.NoSleep(aliceMob.put_in_active_hand(g));
            return g;
        });
        w.Run(Second);
        Assert.Same(gun, In(w, () => aliceMob.get_active_held_item()));
        float full = In(w, () => gun.get_ammo());
        (double bx, double by) = In(w, () => LocomotionSeam.TryGetState(w.World, bobMob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
            ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(bobMob.x), Dm.num(bobMob.y)));
        var bobTarget = new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest);
        ShotAim aim = AimAt(alice, bx, by, 0.8);

        // --- the press: the first shot at once and then one every 0.3 seconds; tg's own autofire takes the press over, so the click that
        // follows a press (the client sends one on the release) does not shoot as well
        heardBob.Clear();
        alice.Session.MouseDown(bobTarget, ClickButton.Left, ClickModifiers.None, aim, w.Now);
        w.Run(3 * Second);
        int heard = heardBob.Count(e => e.File == "sound/items/weapons/gun/smg/shot.ogg");
        float spent = full - In(w, () => gun.get_ammo());
        output.WriteLine($"held for 3 seconds: {spent} rounds fired, {heard} shots heard");
        Assert.InRange(spent, 10f, 11f); // t = 0, 0.3, ... 3.0
        Assert.InRange(heard, 9, 11);
        Assert.True(In(w, () => Dm.num(bobMob.get_brute_loss())) > 0, "the bullets hit him");

        // --- the release: no more shots
        alice.Session.MouseUp(bobTarget, ClickModifiers.None, aim);
        w.Run(Second / 2);
        float afterRelease = In(w, () => gun.get_ammo());
        heardBob.Clear();
        w.Run(2 * Second);
        Assert.Equal(afterRelease, In(w, () => gun.get_ammo()));
        Assert.DoesNotContain(heardBob, e => e.File == "sound/items/weapons/gun/smg/shot.ogg");
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
