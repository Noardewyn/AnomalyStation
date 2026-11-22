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
/// M7x slice C (the ballistic guns, docs/ai/specs/weapons.md 3) on a real server with two players: Alice, dressed as tg's detective (the
/// jumpsuit and the detective's full holster by hand: the outfit's items cannot be equipped in strict worlds before the implants of
/// M8, D180), opens the holster and draws the .38 revolver with clicks, shoots Bob (the shot is heard by his client, the lead bullet
/// does tg's 25 brute), dumps the cylinder and reloads it from a speed loader taken from the holster by clicks, and with her camera's
/// aim (slice A) shoots Bob in the arm: the arm takes the damage, nothing else, and the second hit disables it.
/// </summary>
public class BallisticGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const string Hitscan = "/obj/projectile/beam/emitter/hitscan";

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

    private static float Brute(RealHeadlessWorld w, mob_living_carbon_human mob, string zone)
        => In(w, () => Dm.num(((obj_item_bodypart)mob.get_bodypart(zone)).brute_dam));

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    [Fact]
    public void A_detective_draws_the_revolver_from_the_holster_reloads_it_from_a_speed_loader_and_shoots_a_mans_arm()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 41, TimeoutSteps = 9000 });
        In(w, () => { DevCommands.ProjectileSpeed(1f); return 0; }); // DV211: tg's flight, so a bullet's per-tile falloff is tg's number
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var heardBob = new List<SoundEvent>();
        var heardAlice = new List<SoundEvent>();
        bob.Session.Replica.SoundsReceived += events => heardBob.AddRange(events);
        alice.Session.Replica.SoundsReceived += events => heardAlice.AddRange(events);
        bob.Yaw = Math.PI / 2; // Bob faces west, toward Alice
        w.Run(10);

        // --- Bob stands four tiles east of Alice in the open (or west, whichever is clear); Alice's holster and jumpsuit
        turf bobAt = In(w, () =>
        {
            foreach (int dir in new[] { EAST, WEST, NORTH, SOUTH })
            {
                turf at = get_turf(aliceMob);
                bool ok = true;
                for (int i = 0; i < 4 && ok; i++)
                {
                    at = get_step(at, dir) as turf;
                    ok = at != null && Clear(at);
                }
                if (ok)
                    return at;
            }
            return null;
        });
        Assert.NotNull(bobAt);
        bool east = In(w, () => Dm.num(bobAt.x) > Dm.num(aliceMob.x));
        In(w, () => Waiting.NoSleep(bobMob.forceMove(bobAt)));
        obj_item_storage_belt_holster_detective_full holster = null;
        In(w, () =>
        {
            if (aliceMob.w_uniform == null)
                Waiting.NoSleep(aliceMob.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(get_turf(aliceMob)), ITEM_SLOT_ICLOTHING));
            if (aliceMob.belt != null)
                qdel(aliceMob.belt); // the assistant's PDA
            holster = Dm.@new<obj_item_storage_belt_holster_detective_full>(get_turf(aliceMob));
            Waiting.NoSleep(aliceMob.equip_to_slot_or_del(holster, ITEM_SLOT_BELT));
        });
        w.Run(Second);
        Assert.Same(holster, In(w, () => aliceMob.belt));
        Assert.True(In(w, () => HAS_TRAIT(aliceMob, TRAIT_GUNFLIP)));
        obj_item_gun_ballistic_revolver_c38_detective gun = In(w, () => Dm.each(holster.contents).OfType<obj_item_gun_ballistic_revolver_c38_detective>().Single());
        var loaders = In(w, () => Dm.each(holster.contents).OfType<obj_item_ammo_box_speedloader_c38>().ToList());
        Assert.Equal(2, loaders.Count);
        Assert.Null(In(w, () => aliceMob.get_active_held_item()));

        // --- she opens the holster with a click on it in its slot, and a click on the revolver in the open holster draws it
        ClickScreen(alice, holster);
        w.Run(Second);
        output.WriteLine("storage open: " + In(w, () => string.Join(",", Dm.each(holster.atom_storage.is_using).Select(m => ((mob)m).name))));
        bool Shown(atom a) => alice.Session.Replica.Screen.Values.Any(e => e.Group == HudGroup.Storage && e.VisChildren != null && e.VisChildren.Contains(a.dm_id)) || alice.Session.Replica.Screen.ContainsKey(a.dm_id);
        output.WriteLine("revolver in the storage window: " + Shown(gun));
        ClickScreen(alice, gun);
        w.Run(Second);
        Assert.Same(gun, In(w, () => aliceMob.get_active_held_item()));
        Assert.Equal(6f, In(w, () => gun.get_ammo(FALSE, FALSE)));
        output.WriteLine("drawn");

        // --- a click on Bob: the lead .38 does tg's 25 brute (the armour of a bare man is none), the shot is heard by his client
        float bruteBefore = In(w, () => Dm.num(bobMob.get_brute_loss()));
        heardBob.Clear();
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        // A wound the bullet opens (tg's random wound roll) gives Bob a second wind (determination, other_reagents.dm:3147: the wounded
        // limb heals 0.167 brute a second, M8 slice B), so after two seconds a little under the shot's 25 is left; no wound, the 25 stay.
        // The .38 may also embed (tg's 45% embed roll, embedding.dm: its impact adds the shrapnel's w_class * impact_pain_mult, part of it
        // brute: 2.4 for the .38's), and while it is embedded its pain may roll (15% a second, embedding.dm:454-470: 1.8 brute for
        // the .38's, pain_mult 3 less its 40% stamina part), up to three times in the two seconds.
        float bruteNow = In(w, () => Dm.num(bobMob.get_brute_loss()));
        bool embedded = In(w, () => Dm.each_cast<obj_item_bodypart>(bobMob.bodyparts).Any(b => Dm.truthy(b.embedded_objects?.len)));
        Assert.InRange(bruteNow, bruteBefore + 24, bruteBefore + 25 + (embedded ? 2.4f + 3 * 1.8f : 0f) + 0.01f);
        bool wounded = In(w, () => Dm.truthy(Dm.length(bobMob.all_wounds)));
        output.WriteLine("wounded: " + wounded);
        Assert.Equal(wounded, In(w, () => Dm.truthy(bobMob.has_reagent(typeof(datum_reagent_determination)))));
        Assert.Contains(heardBob, e => e.File == "sound/items/weapons/gun/revolver/shot.ogg");
        Assert.Equal(5f, In(w, () => gun.get_ammo(FALSE, FALSE)));
        Assert.Equal(6f, In(w, () => gun.get_ammo(FALSE, TRUE))); // the spent casing stays in the cylinder

        // --- she dumps the cylinder (the use-in-hand key): five live rounds and the spent casing fall at her feet
        heardAlice.Clear();
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.Equal(0f, In(w, () => gun.get_ammo(FALSE, TRUE)));
        Assert.Equal(6, In(w, () => Dm.each<obj_item_ammo_casing>(get_turf(aliceMob).contents).Count()));
        Assert.Contains(heardAlice, e => e.File == "sound/items/weapons/gun/revolver/empty.ogg");

        // --- the speed loader out of the holster: the other, empty hand made the active one (a click with a gun in hand would use the gun
        // on the loader), a click on the loader in the open holster takes it, and a click on the revolver in the other hand pours its
        // six rounds in
        obj_item_ammo_box_speedloader_c38 loader = loaders[0];
        alice.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(Second);
        if (!In(w, () => Dm.truthy(Dm.@in(aliceMob, holster.atom_storage.is_using))))
        {
            ClickScreen(alice, holster);
            w.Run(Second);
        }
        Assert.True(Shown(loader), "the loader is in the open holster");
        ClickScreen(alice, loader);
        w.Run(Second);
        Assert.Same(loader, In(w, () => aliceMob.get_active_held_item()));
        heardAlice.Clear();
        ClickScreen(alice, gun);
        w.Run(Second);
        Assert.Equal(6f, In(w, () => gun.get_ammo(FALSE, FALSE)));
        Assert.Equal(0, In(w, () => loader.stored_ammo.Count));
        Assert.Contains(heardAlice, e => e.File == "sound/items/weapons/gun/revolver/load_bullet.ogg");
        alice.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(Second);
        Assert.Same(gun, In(w, () => aliceMob.get_active_held_item()));

        // --- the camera's aim at Bob's arm: first find the arm with the test beam of slice A (the dev hook, which takes her clicks)
        (double bx, double by) = In(w, () => LocomotionSeam.TryGetState(w.World, bobMob.dm_id, out Anomaly.Core.Motion.MotionState s, out _)
            ? (s.X / 256.0, s.Y / 256.0) : (Dm.num(bobMob.x), Dm.num(bobMob.y)));
        alice.Session.DevCommand("shoot me " + Hitscan);
        w.Run(5);
        double armOffset = double.NaN;
        string armZone = null;
        foreach (double off in new[] { 0.10, -0.10, 0.14, -0.14, 0.18, -0.18, 0.22, -0.22, 0.26, -0.26, 0.30, -0.30 })
        {
            In(w, () => Waiting.NoSleep(bobMob.fully_heal(HEAL_ALL)));
            Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18), ClickButton.Left, ClickModifiers.None, AimAt(alice, bx, by + off, 0.9)));
            w.Run(8);
            foreach (string zone in new[] { BODY_ZONE_L_ARM, BODY_ZONE_R_ARM })
            {
                if (In(w, () => Dm.num(((obj_item_bodypart)bobMob.get_bodypart(zone)).burn_dam)) > 0)
                {
                    armOffset = off;
                    armZone = zone;
                }
            }
            if (armZone != null)
                break;
        }
        output.WriteLine($"the {armZone} is at offset {armOffset}");
        Assert.NotNull(armZone);
        In(w, () => Waiting.NoSleep(bobMob.fully_heal(HEAL_ALL)));
        alice.Session.DevCommand("shoot off");
        w.Run(5);
        Assert.Null(In(w, () => aliceMob.client.dm_dev_shot));

        // --- the revolver at that arm: the arm takes the lead and nothing else, and the bullets wound it
        int shots = 0;
        obj_item_bodypart arm = In(w, () => (obj_item_bodypart)bobMob.get_bodypart(armZone));
        for (; shots < 5 && In(w, () => Dm.each<datum_wound>(arm.wounds).Count()) == 0; shots++)
        {
            In(w, () => aliceMob.next_click = -1);
            Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18), ClickButton.Left, ClickModifiers.None, AimAt(alice, bx, by + armOffset, 0.9)));
            w.Run(Second);
        }
        output.WriteLine($"{shots} shots: the arm has {Brute(w, bobMob, armZone)} brute and {In(w, () => Dm.each<datum_wound>(arm.wounds).Count())} wound(s)");
        Assert.True(shots > 0);
        // a bullet that embeds (tg's random embed) adds its impact damage on top of the lead's 25
        if (In(w, () => Dm.num(Dm.length(arm.embedded_objects))) > 0)
            Assert.True(Brute(w, bobMob, armZone) > 25f * shots);
        else
            Assert.InRange(Brute(w, bobMob, armZone), 25f * shots - 1f, 25f * shots + 0.01f); // the second wind above may have healed a little since the wound opened
        foreach (string zone in new[] { BODY_ZONE_HEAD, BODY_ZONE_CHEST, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG, armZone == BODY_ZONE_L_ARM ? BODY_ZONE_R_ARM : BODY_ZONE_L_ARM })
            Assert.Equal(0f, Brute(w, bobMob, zone));
        Assert.NotEmpty(In(w, () => Dm.each<datum_wound>(arm.wounds).ToList()));
        Assert.Equal(0, w.World.RuntimeErrorCount);

    }
}
