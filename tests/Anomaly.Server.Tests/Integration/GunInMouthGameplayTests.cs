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
/// Package T, slice T4a, fix round 1: tg's <c>/obj/item/gun/proc/handle_suicide</c> (gun.dm:662-706), which a click aimed at the mouth by look reaches
/// from the next tile (fire_gun's <c>flag</c>: the target adjacent, the user, or on the user). Through a real server and clients, with tg's laser gun
/// (20 burn a beam, no embedding, no falloff) and its camera aim at the head (DV95, so the zone is no dice roll): the gun pointed at the head, no shot for
/// tg's 12 second do-after, then "pulls the trigger!" and five times the ordinary shot's damage; spared when the do-after breaks or the zone moves off
/// the mouth; on oneself; an empty gun's click; from further away an ordinary shot at the head; and a monkey, which tg counts as a human.
/// </summary>
public class GunInMouthGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string Laser = "/obj/item/gun/energy/laser";

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

    // The camera ray from the player's eye to (x, y) at `up` metres, as the client's pick ray holds it (AimedShotGameplayTests.AimAt).
    private static ShotAim AimAt(RealHeadlessWorld.Player p, double x, double y, double up)
    {
        (float ox, float oy, int z) = p.Session.Prediction.RenderPosition();
        double eastE = (ox - 0.5) * 2, northE = (oy - 0.5) * 2, upE = WorldScale.DefaultEyeHeight;
        double dx = (x - 0.5) * 2 - eastE, dy = (y - 0.5) * 2 - northE, dz = up - upE, length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return PlaySession.AimOf(new PickRay(eastE, northE, upE, z, dx / length, dy / length, dz / length, 10));
    }

    // At the head's middle row (tg's icon-y 27 of the mob-scaled picture), at the mob's server position.
    private static ShotAim AtHead(RealHeadlessWorld w, RealHeadlessWorld.Player shooter, mob_living target)
    {
        (double x, double y) = In(w, () => LocomotionSeam.TryGetState(w.World, target.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
            ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(target.x), Dm.num(target.y)));
        return AimAt(shooter, x, y, 26.5 * 0.05);
    }

    private static bool Click(RealHeadlessWorld.Player p, mob target, AimZone zone, ShotAim? aim)
        => p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, zone == AimZone.Mouth ? (byte)24 : (byte)29, zone), ClickButton.Left, ClickModifiers.None, aim);

    private static float Burn(RealHeadlessWorld w, mob_living_carbon mob, string zone)
        => In(w, () => Dm.num(((obj_item_bodypart)mob.get_bodypart(zone)).burn_dam));

    // Runs `ticks` ticks; the head's burn at the first tick it is no longer `before` (the tick a beam lands, before tg's wound code heals any of it), else `before`.
    private static float HeadBurnWhenItLands(RealHeadlessWorld w, mob_living_carbon mob, float before, int ticks)
    {
        float landed = before;
        w.Run(ticks, _ =>
        {
            if (landed == before)
                landed = Burn(w, mob, BODY_ZONE_HEAD);
        });
        return landed;
    }

    private static float OtherBurn(RealHeadlessWorld w, mob_living_carbon mob)
        => In(w, () => Dm.each_cast<obj_item_bodypart>(mob.bodyparts).Where(b => !Dm.eq(b.body_zone, BODY_ZONE_HEAD)).Sum(b => Dm.num(b.burn_dam)));

    private sealed record Range(RealHeadlessWorld W, RealHeadlessWorld.Player Alice, RealHeadlessWorld.Player Bob, mob_living_carbon_human AliceMob,
        mob_living_carbon_human BobMob, obj_item_gun Gun, List<string> AliceTexts, List<string> BobTexts)
    {
        // the names tg's messages use (a joined player gets a random character name)
        public string A => In(W, () => Dm.str(AliceMob.name));
        public string B => In(W, () => Dm.str(BobMob.name));
    }

    // Alice with the gun in her hand and Bob `tiles` east of her, facing her.
    private static Range Setup(ulong seed, int tiles, string gun = Laser)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        bob.Yaw = Math.PI / 2; // Bob faces west, toward Alice (Godot's yaw turns left)
        var aliceTexts = new List<string>();
        var bobTexts = new List<string>();
        alice.Client.ServerTextReceived += t => aliceTexts.Add(t.Html);
        bob.Client.ServerTextReceived += t => bobTexts.Add(t.Html);
        string given = In(w, () =>
        {
            turf start = get_turf(aliceMob);
            Waiting.NoSleep(bobMob.forceMove(locate(Dm.num(start.x) + tiles, Dm.num(start.y), Dm.num(start.z))));
            return DevCommands.Give(aliceMob, gun);
        });
        w.Run(Second);
        var held = In(w, () => aliceMob.get_active_held_item() as obj_item_gun);
        Assert.True(held != null, given);
        Assert.Equal(tiles == 1, In(w, () => Dm.truthy(Waiting.NoSleep(aliceMob.Adjacent(bobMob)))));
        return new Range(w, alice, bob, aliceMob, bobMob, held, aliceTexts, bobTexts);
    }

    // The damage of the beam the laser has chambered (tg's laser: 20).
    private static float Chambered(RealHeadlessWorld w, obj_item_gun gun) => In(w, () => Dm.num(gun.chambered.loaded_projectile.damage));

    [Fact]
    public void At_the_next_tile_the_mouth_is_tgs_twelve_second_shot_of_five_times_the_damage()
    {
        Range r = Setup(51, 1);
        using RealHeadlessWorld w = r.W;
        float beam = Chambered(w, r.Gun);
        Assert.True(beam > 0);

        // the ordinary point-blank shot at the head, for comparison: the beam's damage on the head, nothing elsewhere
        Assert.True(Click(r.Alice, r.BobMob, AimZone.Head, AtHead(w, r.Alice, r.BobMob)));
        float ordinary = HeadBurnWhenItLands(w, r.BobMob, 0f, Second);
        Assert.Equal(beam, ordinary);
        Assert.Equal(0f, OtherBurn(w, r.BobMob));
        w.Run(2 * Second); // the fire delay
        Assert.Equal(beam, Chambered(w, r.Gun)); // the next beam is an ordinary one

        // the mouth: tg's handle_suicide, the gun at his head; nothing fires during the 12 second do-after
        r.AliceTexts.Clear();
        r.BobTexts.Clear();
        Assert.True(Click(r.Alice, r.BobMob, AimZone.Mouth, AtHead(w, r.Alice, r.BobMob)));
        w.Run(Second);
        Assert.Equal(BODY_ZONE_PRECISE_MOUTH, In(w, () => r.AliceMob.zone_selected));
        Assert.Contains(r.AliceTexts, t => t.Contains($"{r.A} points", StringComparison.Ordinal) && t.Contains($"{r.B}'s head, ready to pull the trigger...", StringComparison.Ordinal));
        Assert.Contains(r.BobTexts, t => t.Contains($"{r.A} points", StringComparison.Ordinal) && t.Contains("at your head, ready to pull the trigger...", StringComparison.Ordinal));
        Assert.Equal(1f, In(w, () => r.Gun.fire_cd));
        float head = Burn(w, r.BobMob, BODY_ZONE_HEAD); // the head now (the ordinary shot's burn)
        w.Run(10 * Second); // 11 seconds in
        Assert.Equal(head, Burn(w, r.BobMob, BODY_ZONE_HEAD));
        Assert.DoesNotContain(r.AliceTexts, t => t.Contains("pulls the trigger", StringComparison.Ordinal));

        // the do-after ends: the trigger, the beam times five (gun.dm:694) on the head (zone_override BODY_ZONE_HEAD). The head's burn is read at the
        // tick the beam lands, before tg's wound code acts on it (a burn wound gives determination, which heals the limb a little each second)
        float landed = HeadBurnWhenItLands(w, r.BobMob, head, 2 * Second);
        Assert.Contains(r.AliceTexts, t => t.Contains($"{r.A} pulls the trigger!", StringComparison.Ordinal));
        Assert.Contains(r.BobTexts, t => t.Contains($"{r.A} pulls the trigger!", StringComparison.Ordinal));
        float execution = landed - head;
        output.WriteLine($"ordinary head shot {ordinary} burn, the mouth's {execution} burn, {Burn(w, r.BobMob, BODY_ZONE_HEAD)} a second later");
        // no armour on a bare head and the same beam: exactly five times the ordinary shot
        Assert.Equal(5 * ordinary, execution, 2);
        Assert.Equal(0f, OtherBurn(w, r.BobMob));
    }

    [Fact]
    public void The_do_after_broken_by_the_target_moving_spares_him_and_the_next_shot_fires()
    {
        Range r = Setup(52, 1);
        using RealHeadlessWorld w = r.W;
        Assert.True(Click(r.Alice, r.BobMob, AimZone.Mouth, AtHead(w, r.Alice, r.BobMob)));
        w.Run(Second);
        Assert.Equal(1f, In(w, () => r.Gun.fire_cd));
        // Bob steps to the tile north of his, still next to Alice (diagonally): tg's do_after breaks on the target's move
        In(w, () => Waiting.NoSleep(r.BobMob.forceMove(get_step(r.BobMob, NORTH))));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(r.BobMob.Adjacent(r.AliceMob)))));
        Assert.Contains(r.AliceTexts, t => t.Contains($"{r.A} has decided to spare {r.B}", StringComparison.Ordinal));
        Assert.Contains(r.BobTexts, t => t.Contains($"{r.A} has decided to spare your life!", StringComparison.Ordinal));
        Assert.Equal(0f, In(w, () => r.Gun.fire_cd));
        w.Run(12 * Second);
        Assert.Equal(0f, Burn(w, r.BobMob, BODY_ZONE_HEAD));
        Assert.DoesNotContain(r.AliceTexts, t => t.Contains("pulls the trigger", StringComparison.Ordinal));
        // fire_cd was cleared: the next shot, at his head, fires at once
        float beam = Chambered(w, r.Gun);
        Assert.True(Click(r.Alice, r.BobMob, AimZone.Head, AtHead(w, r.Alice, r.BobMob)));
        Assert.Equal(beam, HeadBurnWhenItLands(w, r.BobMob, 0f, Second));
    }

    [Fact]
    public void The_zone_moved_off_the_mouth_during_the_wait_spares_him()
    {
        Range r = Setup(53, 1);
        using RealHeadlessWorld w = r.W;
        Assert.True(Click(r.Alice, r.BobMob, AimZone.Mouth, AtHead(w, r.Alice, r.BobMob)));
        w.Run(Second);
        // tg's zone selector: the head
        In(w, () => ((atom_movable_screen_zone_sel)r.AliceMob.hud_used.screen_objects[HUD_MOB_ZONE_SELECTOR]).set_selected_zone(BODY_ZONE_HEAD, r.AliceMob));
        Assert.Equal(BODY_ZONE_HEAD, In(w, () => r.AliceMob.zone_selected));
        w.Run(12 * Second);
        Assert.Contains(r.AliceTexts, t => t.Contains($"{r.A} has decided to spare {r.B}", StringComparison.Ordinal));
        Assert.DoesNotContain(r.AliceTexts, t => t.Contains("pulls the trigger", StringComparison.Ordinal));
        Assert.Equal(0f, Burn(w, r.BobMob, BODY_ZONE_HEAD));
        Assert.Equal(0f, In(w, () => r.Gun.fire_cd));
    }

    [Fact]
    public void On_oneself_the_gun_goes_in_the_mouth_and_fires_after_the_wait()
    {
        Range r = Setup(54, 3);
        using RealHeadlessWorld w = r.W;
        float beam = Chambered(w, r.Gun);
        // a click on her own body at the mouth (no camera aim: a shot at oneself hits directly, tg's roll at distance 0 is certain)
        Assert.True(Click(r.Alice, r.AliceMob, AimZone.Mouth, null));
        w.Run(Second);
        Assert.Contains(r.AliceTexts, t => t.Contains("You stick", StringComparison.Ordinal) && t.Contains("in your mouth, ready to pull the trigger...", StringComparison.Ordinal));
        Assert.Equal(0f, Burn(w, r.AliceMob, BODY_ZONE_HEAD));
        float landed = HeadBurnWhenItLands(w, r.AliceMob, 0f, 12 * Second);
        Assert.Contains(r.AliceTexts, t => t.Contains("You pull the trigger!", StringComparison.Ordinal));
        Assert.Equal(5 * beam, landed, 2);
        Assert.Equal(0f, OtherBurn(w, r.AliceMob));
    }

    [Fact]
    public void An_empty_gun_clicks_after_the_wait_and_hurts_nobody()
    {
        Range r = Setup(55, 1, "/obj/item/gun/ballistic/automatic/pistol/no_mag");
        using RealHeadlessWorld w = r.W;
        var heard = new List<SoundEvent>();
        r.Alice.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        Assert.Null(In(w, () => r.Gun.chambered));
        float before = In(w, () => Dm.num(r.BobMob.get_brute_loss()) + Dm.num(r.BobMob.get_fire_loss()));
        Assert.True(Click(r.Alice, r.BobMob, AimZone.Mouth, AtHead(w, r.Alice, r.BobMob)));
        w.Run(Second);
        Assert.Contains(r.AliceTexts, t => t.Contains($"{r.B}'s head, ready to pull the trigger...", StringComparison.Ordinal));
        w.Run(12 * Second);
        Assert.Contains(r.AliceTexts, t => t.Contains($"{r.A} pulls the trigger!", StringComparison.Ordinal));
        Assert.Contains(heard, e => e.File != null && e.File.Contains("dry_fire", StringComparison.Ordinal));
        Assert.Equal(before, In(w, () => Dm.num(r.BobMob.get_brute_loss()) + Dm.num(r.BobMob.get_fire_loss())));
        Assert.Equal(0f, In(w, () => r.Gun.fire_cd));
    }

    [Fact]
    public void From_further_away_the_mouth_is_an_ordinary_shot_that_hits_the_head()
    {
        Range r = Setup(56, 3);
        using RealHeadlessWorld w = r.W;
        float beam = Chambered(w, r.Gun);
        Assert.True(Click(r.Alice, r.BobMob, AimZone.Mouth, AtHead(w, r.Alice, r.BobMob)));
        float landed = HeadBurnWhenItLands(w, r.BobMob, 0f, Second);
        Assert.Equal(BODY_ZONE_PRECISE_MOUTH, In(w, () => r.AliceMob.zone_selected));
        // no do-after: fire_gun's flag is false, so the beam flies at once and the server's six-zone crossing reads the head (DV95)
        Assert.DoesNotContain(r.AliceTexts, t => t.Contains("ready to pull the trigger", StringComparison.Ordinal));
        Assert.Equal(beam, landed);
        Assert.Equal(0f, OtherBurn(w, r.BobMob));
    }

    /// <summary>
    /// Fix round 2: tg's staff of healing overrides handle_suicide with a bare <c>return</c> (staff.dm:171), which the port has not translated (the
    /// staff is an inert generated type, D112; not-translated.md section 47). It cannot reach the base handle_suicide:
    /// - no player holds it: put_in_hands refuses it (DV27: its own tg pickup and dropped are untranslated), so a dev give leaves it on the floor
    ///   and a click on it there does not pick it up;
    /// - and were it held, fire_gun stops before the handle_suicide branch, at can_trigger_gun's DV27 guard for an inert gun, returning NONE as tg's
    ///   fire_gun returns its override's null: no "ready to pull the trigger", no fire_cd, no wait. tg's caller then goes on the same way
    ///   (interact_with_atom returns that value, chambered_attack_block being FALSE).
    /// </summary>
    [Fact]
    public void The_staff_of_healing_never_reaches_the_gun_in_the_mouth()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 58, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        In(w, () =>
        {
            turf start = get_turf(aliceMob);
            Waiting.NoSleep(bobMob.forceMove(locate(Dm.num(start.x) + 1, Dm.num(start.y), Dm.num(start.z))));
        });
        string given = In(w, () => DevCommands.Give(aliceMob, "/obj/item/gun/magic/staff/healing"));
        w.Run(Second);
        Assert.Contains("on the floor", given);
        Assert.Null(In(w, () => aliceMob.get_active_held_item()));
        var staff = In(w, () => Dm.each(get_turf(aliceMob).contents).OfType<obj_item_gun_magic_staff_healing>().Single());
        Assert.True(InertSeam.IsInert(staff));
        // a click on it with the empty hand does not pick it up
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, staff.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Null(In(w, () => aliceMob.get_active_held_item()));

        // fire_gun itself, as a click with the mouth selected would call it from the next tile (flag TRUE) and on oneself
        In(w, () => aliceMob.zone_selected = BODY_ZONE_PRECISE_MOUTH);
        foreach (mob_living_carbon_human target in new[] { bobMob, aliceMob })
        {
            Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(aliceMob.Adjacent(target)))));
            Assert.Equal(NONE, In(w, () => Waiting.NoSleep(staff.fire_gun(target, aliceMob, TRUE, ""))));
            Assert.Equal(0f, In(w, () => staff.fire_cd));
        }
        Assert.Contains("inert guard: /obj/item/gun/magic/staff/healing can_trigger_gun", In(w, () => DmWorldState.Of(w.World).DeferredSites));
        w.Run(13 * Second);
        Assert.DoesNotContain(texts, t => t.Contains("ready to pull the trigger", StringComparison.Ordinal) || t.Contains("pulls the trigger", StringComparison.Ordinal));
    }

    [Fact]
    public void A_monkey_is_a_human_to_tg_so_the_gun_goes_to_its_head_too()
    {
        Range r = Setup(57, 3);
        using RealHeadlessWorld w = r.W;
        float beam = Chambered(w, r.Gun);
        // tg: ishuman(A) is istype(A, /mob/living/carbon/human), and a monkey is /mob/living/carbon/human/species/monkey
        var monkey = In(w, () => (mob_living_carbon_human)DevCommands.Spawn(typeof(mob_living_carbon_human_species_monkey), get_step(r.AliceMob, EAST) as turf, 1, 8, out _).Single());
        // its tg AI (/datum/ai_controller/monkey) wanders, which would break the do-after on its move: deleted, the monkey stands still
        In(w, () => qdel(monkey.ai_controller as datum));
        w.Run(Second);
        Assert.Null(In(w, () => monkey.ai_controller));
        turf stands = In(w, () => get_turf(monkey));
        Assert.True(In(w, () => ishuman(monkey) && Dm.truthy(Waiting.NoSleep(monkey.Adjacent(r.AliceMob)))));
        // the camera aim at a row the server's crossing reads as the monkey's head in every frame (its table, DV95), so the zone is no dice roll
        List<int> headRows = Enumerable.Range(1, 32).Where(y => new byte[] { 1, 2, 4, 8 }.All(d => In(w, () => ShotGeometry.ZoneAt(monkey, d, 16, y)) == BODY_ZONE_HEAD)).ToList();
        Assert.NotEmpty(headRows);
        int row = headRows[headRows.Count / 2];
        output.WriteLine($"the monkey's head rows on the server: {string.Join(",", headRows)}; aimed at {row}");
        (double mx, double my) = In(w, () => LocomotionSeam.TryGetState(w.World, monkey.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
            ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(monkey.x), Dm.num(monkey.y)));
        Assert.True(Click(r.Alice, monkey, AimZone.Mouth, AimAt(r.Alice, mx, my, (row - 0.5) * 0.05)));
        w.Run(Second);
        Assert.Contains(r.AliceTexts, t => t.Contains("points", StringComparison.Ordinal) && t.Contains("head, ready to pull the trigger...", StringComparison.Ordinal));
        Assert.Equal(0f, Burn(w, monkey, BODY_ZONE_HEAD));
        float landed = HeadBurnWhenItLands(w, monkey, 0f, 12 * Second);
        Assert.Same(stands, In(w, () => get_turf(monkey)));
        Assert.Contains(r.AliceTexts, t => t.Contains($"{r.A} pulls the trigger!", StringComparison.Ordinal));
        Assert.Equal(5 * beam, landed, 2);
    }
}
