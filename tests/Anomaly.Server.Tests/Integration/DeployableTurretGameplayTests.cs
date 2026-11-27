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
/// The heavy machine gun turret (structures/deployable_turret.dm) on a real server with two players: Alice finds Buckle on the gun's
/// radial and drops herself onto it, her hands fill with the turret's controls, and her click at Bob with her camera's aim turns the gun
/// towards him and fires its three-shot burst, which he hears and which hurts him; the resist key gets her out and the controls go.
/// Everything goes through the client's session as a player's input.
/// </summary>
public class DeployableTurretGameplayTests(ITestOutputHelper output)
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

    private static ClickTarget Entity(long id) => new(TargetKind.Entity, id, 0, 0, 0, 16, 16);

    private static ShotAim AimAt(RealHeadlessWorld.Player p, double x, double y, double up)
    {
        (float ox, float oy, int z) = p.Session.Prediction.RenderPosition();
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (x - 0.5) * 2 - eastE, dy = (y - 0.5) * 2 - northE, dz = up - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
    }

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // Bob stands 'tiles' away from Alice on a clear line, in the first of the four directions that has one.
    private static (turf At, int Dir) PlaceBob(RealHeadlessWorld w, mob alice, mob bob, int tiles)
    {
        (turf at, int dir) = In(w, () =>
        {
            foreach (int d in new[] { EAST, WEST, NORTH, SOUTH })
            {
                turf t = get_turf(alice);
                bool ok = true;
                for (int i = 0; i < tiles && ok; i++)
                {
                    t = get_step(t, d) as turf;
                    ok = t != null && Clear(t);
                }
                if (ok)
                    return (t, d);
            }
            return ((turf)null, 0);
        });
        Assert.NotNull(at);
        In(w, () => Waiting.NoSleep(bob.forceMove(at)));
        return (at, dir);
    }

    [Fact]
    public void A_player_mans_the_heavy_machine_gun_from_its_radial_and_shoots_another_player()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14920, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var heardBob = new List<SoundEvent>();
        var heardAlice = new List<SoundEvent>();
        bob.Session.Replica.SoundsReceived += events => heardBob.AddRange(events);
        alice.Session.Replica.SoundsReceived += events => heardAlice.AddRange(events);
        var hints = new List<ContextHintsMessage>();
        alice.Client.ContextHintsReceived += hints.Add;
        w.Run(10);
        (_, int towardsBob) = PlaceBob(w, aliceMob, bobMob, 4);
        In(w, () =>
        {
            foreach (obj_item held in Dm.each_cast<obj_item>(aliceMob.held_items).Where(i => i != null).ToList())
                Waiting.NoSleep(aliceMob.dropItemToGround(held));
        });
        var gun = In(w, () => Dm.@new<obj_machinery_deployable_turret_hmg>(get_turf(aliceMob)));
        w.Run(Second);

        // the gun's radial offers Buckle, a drop of herself onto it (the server's context hints)
        hints.Clear();
        alice.Session.Rehover();
        alice.Session.Hover(Entity(gun.dm_id), w.Now);
        w.Run(4);
        ContextHintsMessage answer = hints.LastOrDefault(h => h.Target == gun.dm_id);
        Assert.NotNull(answer);
        RadialEntry buckle = RadialModel.Build(answer, 0, true).Single(r => r.Label == "Buckle");
        Assert.Equal(RadialKind.DropSelf, buckle.Kind);
        Assert.True(alice.Session.Drop(new DragRelease(Entity(aliceMob.dm_id), Entity(gun.dm_id), ClickButton.Left, ClickModifiers.None, 255)));
        w.Run(Second);
        Assert.Same(gun, In(w, () => aliceMob.buckled));
        Assert.Equal(2, In(w, () => Dm.each<obj_item_gun_control>(aliceMob.held_items).Count()));
        Assert.Contains(heardAlice, e => e.File == "sound/vehicles/mecha/mechmove01.ogg");

        // her click at Bob with her aim: the gun turns to him and fires its burst
        (double bx, double by) = In(w, () => LocomotionSeam.TryGetState(w.World, bobMob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
            ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(bobMob.x), Dm.num(bobMob.y)));
        var bobTarget = new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest);
        float before = In(w, () => Dm.num(bobMob.get_brute_loss()));
        heardBob.Clear();
        Assert.True(alice.Session.Click(bobTarget, ClickButton.Left, ClickModifiers.None, AimAt(alice, bx, by, 0.8)));
        w.Run(2 * Second);
        Assert.Equal(towardsBob, In(w, () => (int)Dm.num(gun.dir)));
        int shots = heardBob.Count(e => e.File == "sound/items/weapons/gun/hmg/hmg.ogg");
        output.WriteLine($"Bob heard {shots} shots and took {In(w, () => Dm.num(bobMob.get_brute_loss())) - before} brute");
        Assert.Equal(3, shots);
        Assert.True(In(w, () => Dm.num(bobMob.get_brute_loss())) > before, "the burst hurt Bob");

        // the resist key gets her out: the controls go
        alice.Session.Keybinding(KeybindingCode.Resist);
        w.Run(2 * Second);
        Assert.Null(In(w, () => aliceMob.buckled));
        Assert.Empty(In(w, () => Dm.each<obj_item_gun_control>(aliceMob.held_items).ToList()));
    }
}
