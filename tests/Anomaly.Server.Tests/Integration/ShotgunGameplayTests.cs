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
/// M7x slice D (the shotguns, docs/ai/specs/weapons.md 3) on a real server with two players: Alice holds a riot shotgun and shoots Bob with
/// her camera's aim (the six rubber pellets each fly on their own cone and hurt him by the crossing test), finds the next pull a click
/// until she pumps (the use-in-hand key), shoots again, and loads two buckshot shells into the tube one click at a time from her other
/// hand, hearing the shell go in. Everything goes through the client's session as a player's input.
/// </summary>
public class ShotgunGameplayTests(ITestOutputHelper output)
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

    private static void ClickScreen(RealHeadlessWorld.Player p, atom item)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, item.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));

    private static ShotAim AimAt(RealHeadlessWorld.Player p, double x, double y, double up)
    {
        (float ox, float oy, int z) = p.Session.Prediction.RenderPosition();
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (x - 0.5) * 2 - eastE, dy = (y - 0.5) * 2 - northE, dz = up - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
    }

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // Bob stands 'tiles' away from Alice on a clear line; true when he is east of her.
    private static turf PlaceBob(RealHeadlessWorld w, mob alice, mob bob, int tiles)
    {
        turf at = In(w, () =>
        {
            foreach (int dir in new[] { EAST, WEST, NORTH, SOUTH })
            {
                turf t = get_turf(alice);
                bool ok = true;
                for (int i = 0; i < tiles && ok; i++)
                {
                    t = get_step(t, dir) as turf;
                    ok = t != null && Clear(t);
                }
                if (ok)
                    return t;
            }
            return null;
        });
        Assert.NotNull(at);
        In(w, () => Waiting.NoSleep(bob.forceMove(at)));
        return at;
    }

    [Fact]
    public void A_riot_shotgun_is_pumped_between_shots_and_loaded_a_shell_at_a_time_through_a_players_clicks()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var heardBob = new List<SoundEvent>();
        var heardAlice = new List<SoundEvent>();
        bob.Session.Replica.SoundsReceived += events => heardBob.AddRange(events);
        alice.Session.Replica.SoundsReceived += events => heardAlice.AddRange(events);
        w.Run(10);
        PlaceBob(w, aliceMob, bobMob, 3);
        w.Run(10);

        obj_item_gun_ballistic_shotgun_riot gun = In(w, () =>
        {
            var g = Dm.@new<obj_item_gun_ballistic_shotgun_riot>(get_turf(aliceMob));
            Waiting.NoSleep(aliceMob.put_in_active_hand(g));
            return g;
        });
        w.Run(Second);
        Assert.Same(gun, In(w, () => aliceMob.get_active_held_item()));
        Assert.Equal(5f, In(w, () => gun.magazine.ammo_count()));

        (double bx, double by) = In(w, () => LocomotionSeam.TryGetState(w.World, bobMob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
            ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(bobMob.x), Dm.num(bobMob.y)));
        var bobTarget = new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest);
        float Hurt() => In(w, () => Dm.num(bobMob.get_brute_loss()) + Dm.num(bobMob.get_stamina_loss()));

        // --- the first pull: six rubber pellets (brute 3 and stamina 10 each where they land), heard by Bob
        float before = Hurt();
        heardBob.Clear();
        Assert.True(alice.Session.Click(bobTarget, ClickButton.Left, ClickModifiers.None, AimAt(alice, bx, by, 0.8)));
        w.Run(2 * Second);
        float afterFirst = Hurt();
        output.WriteLine($"first volley: {afterFirst - before} hurt");
        Assert.Contains(heardBob, e => e.File == "sound/items/weapons/gun/shotgun/shot.ogg");
        Assert.True(afterFirst > before, "some pellets hit");
        Assert.True(afterFirst - before <= 6 * 13.01f, "no more than six pellets");
        // the spent shell stays in the chamber until the pump
        Assert.Null(In(w, () => gun.chambered?.loaded_projectile));
        Assert.Equal(5f, In(w, () => gun.magazine.ammo_count()));

        // --- the second pull is a click: nothing flies, the gun clicks
        In(w, () => aliceMob.next_click = -1);
        heardAlice.Clear();
        Assert.True(alice.Session.Click(bobTarget, ClickButton.Left, ClickModifiers.None, AimAt(alice, bx, by, 0.8)));
        w.Run(2 * Second);
        Assert.Equal(afterFirst, Hurt(), 0.01);
        Assert.Contains(heardAlice, e => e.File == "sound/items/weapons/gun/general/dry_fire.ogg");

        // --- the pump (the use-in-hand key): the spent shell out, the next shell in the chamber
        heardAlice.Clear();
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.Contains(heardAlice, e => e.File == "sound/items/weapons/gun/shotgun/rack.ogg");
        Assert.NotNull(In(w, () => gun.chambered?.loaded_projectile));
        Assert.Equal(4f, In(w, () => gun.magazine.ammo_count()));
        Assert.Equal(1, In(w, () => Dm.each<obj_item_ammo_casing>(get_turf(aliceMob).contents).Count()));

        // --- and the pull after the pump shoots again
        In(w, () => aliceMob.next_click = -1);
        Assert.True(alice.Session.Click(bobTarget, ClickButton.Left, ClickModifiers.None, AimAt(alice, bx, by, 0.8)));
        w.Run(2 * Second);
        Assert.True(Hurt() > afterFirst, "the second volley hurts him more");
        Assert.Null(In(w, () => gun.chambered?.loaded_projectile));

        // --- two buckshot shells loaded one by one: the shell in the other hand, a click on the shotgun in the first (the loading sound each time)
        for (int i = 1; i <= 2; i++)
        {
            In(w, () =>
            {
                var shell = Dm.@new<obj_item_ammo_casing_shotgun_buckshot>(get_turf(aliceMob));
                Waiting.NoSleep(aliceMob.put_in_inactive_hand(shell));
            });
            w.Run(5);
            alice.Session.Keybinding(KeybindingCode.SwapHands);
            w.Run(Second / 2);
            heardAlice.Clear();
            ClickScreen(alice, gun);
            w.Run(Second);
            Assert.Equal(4f + i, In(w, () => gun.magazine.ammo_count()));
            Assert.Contains(heardAlice, e => e.File == "sound/items/weapons/gun/shotgun/insert_shell.ogg");
            alice.Session.Keybinding(KeybindingCode.SwapHands);
            w.Run(Second / 2);
            Assert.Same(gun, In(w, () => aliceMob.get_active_held_item()));
        }
        Assert.Equal(6f, In(w, () => gun.magazine.ammo_count()));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
