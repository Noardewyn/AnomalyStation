using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's play test (2026-10-09): a player aims at a body part of another player's picture (DV30) and hits it in combat
/// mode with a crowbar and with a fist (tg's attacked_by and species harm); the other client sees tg's attack lunge
/// (EntityFlags.Lunge for the tick of the attack) and the attacker's client the hit on the victim (EntityFlags.Hit).
/// </summary>
public class MeleeAimTests(ITestOutputHelper output)
{
    [Fact]
    public void The_body_part_under_the_point_is_the_zone_of_tgs_human_picture()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living bobMob = w.MobOf(bob);
        w.Run(10);
        ReplicaEntity seen = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
        var tables = new Tables(alice.Session.Replica);
        var zones = new System.Text.StringBuilder();
        for (int y = 32; y >= 1; y--)
        {
            for (int x = 1; x <= 32; x++)
                zones.Append(BodyZoneRegionTests.Letter(BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, (byte)x, (byte)y)));
            zones.Append('\n');
        }
        output.WriteLine(zones.ToString());
        // package T, slice T4a: (16, 27) is tg's eyes (get_zone_at: rows 25 to 27, columns 15 to 17), the head above them (16, 29) and beside them
        Assert.Equal(AimZone.Eyes, BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, 16, 27));
        Assert.Equal(AimZone.Head, BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, 16, 29));
        Assert.Equal(AimZone.Head, BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, 13, 26));
        Assert.Equal(AimZone.Chest, BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, 16, 18));
        // seen from the front (south), the mob's right side is on the viewer's left
        string text = zones.ToString();
        Assert.Contains('r', text);
        Assert.Contains('l', text);
        Assert.Contains('R', text);
        Assert.Contains('L', text);
        int rightArm = text.IndexOf('r') % 33, leftArm = text.IndexOf('l') % 33;
        Assert.True(rightArm < leftArm, "the right arm is drawn on the viewer's left from the front");
    }

    [Fact]
    public void A_lying_body_keeps_its_parts_under_the_turned_picture()
    {
        string root = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (root == null)
            return; // no tg icons here
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living bobMob = w.MobOf(bob);
        w.Run(10);
        ReplicaEntity seen = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(root)));
        var tables = new Tables(alice.Session.Replica);
        int standingHead = 0;
        for (int y = 1; y <= 32; y++)
            for (int x = 1; x <= 32; x++)
                if (BodyZoneMap.LimbOf(BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, (byte)x, (byte)y)) == AimZone.Head) // slice T4a: its eyes and mouth too
                    standingHead++;
        using (w.World.Enter())
            Waiting.NoSleep(bobMob.set_resting(Dm.TRUE, Dm.TRUE));
        w.Run(10);
        seen = alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id);
        Assert.True(PostureBits.IsLying(seen.State.Posture));
        var zones = new HashSet<AimZone>();
        int lyingHead = 0;
        for (int y = 1; y <= 32; y++)
            for (int x = 1; x <= 32; x++)
            {
                AimZone zone = BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, (byte)x, (byte)y);
                zones.Add(zone);
                if (BodyZoneMap.LimbOf(zone) == AimZone.Head)
                    lyingHead++;
            }
        // The turned picture still offers every zone, and the head is about as large as it was (nearest sampling of a quarter turn is exact).
        Assert.Contains(AimZone.Head, zones);
        Assert.Contains(AimZone.Eyes, zones);
        Assert.Contains(AimZone.Mouth, zones);
        Assert.Contains(AimZone.Chest, zones);
        Assert.Contains(AimZone.Groin, zones);
        Assert.Contains(AimZone.LeftLeg, zones);
        Assert.InRange(lyingHead, standingHead * 3 / 4, standingHead * 5 / 4 + 1);
    }

    [Fact]
    public void An_aimed_crowbar_hit_and_punches_hurt_and_the_other_client_sees_the_lunge()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living aliceMob = w.MobOf(alice);
        mob_living bobMob = w.MobOf(bob);
        w.Run(10);
        using (w.World.Enter())
        {
            Waiting.NoSleep(bobMob.forceMove(get_step(aliceMob, 4)));
            DevCommands.Give(aliceMob, "/obj/item/crowbar");
        }
        alice.Session.ToggleCombatMode();
        w.Run(10);
        Assert.True(Dm.truthy(aliceMob.combat_mode));

        bool lungeSeen = false, hitSeen = false;
        float before = Brute(w, bobMob);
        alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 27, AimZone.Head), ClickButton.Left, ClickModifiers.None);
        w.Run(20, _ =>
        {
            lungeSeen |= bob.Session.Replica.Entities.SingleOrDefault(e => e.Id == aliceMob.dm_id)?.LungeDir == 4;
            hitSeen |= alice.Session.Replica.Entities.SingleOrDefault(e => e.Id == bobMob.dm_id)?.WasHit == true;
        });
        Assert.Equal(BODY_ZONE_HEAD, aliceMob.zone_selected);
        Assert.True(Brute(w, bobMob) > before, "the crowbar hit (tg's attacked_by)");
        Assert.True(lungeSeen, "Bob's client saw Alice lunge east");
        Assert.True(hitSeen, "Alice's client saw tg's attack visual on Bob (the hit burst)");
        Assert.False(alice.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id).WasHit);
        Assert.Equal((byte)0, bob.Session.Replica.Entities.Single(e => e.Id == aliceMob.dm_id).LungeDir);

        // the fist: the crowbar is dropped, then tg's species harm, a punch at tg's click cooldown (a miss is tg's roll)
        using (w.World.Enter())
            Waiting.NoSleep(aliceMob.drop_all_held_items());
        float beforePunches = Brute(w, bobMob);
        for (int i = 0; i < 8; i++)
        {
            alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None);
            w.Run(20);
        }
        Assert.Equal(BODY_ZONE_CHEST, aliceMob.zone_selected);
        Assert.True(Brute(w, bobMob) > beforePunches, "the punches hurt (tg's species harm)");
        output.WriteLine($"brute {before} -> {beforePunches} -> {Brute(w, bobMob)}");
    }

    /// <summary>
    /// Package T, slice T4a (protocol 100): a click aimed at the eyes, the mouth or the groin selects tg's precise zone through tg's own zone selector,
    /// which the clicker's HUD then draws; and tg's own branches read it: a screwdriver aimed at the head hits the head, aimed at the eyes it is tg's
    /// eye stab (<c>/datum/element/eyestab</c>, COMSIG_ITEM_ATTACK when <c>zone_selected</c> is <c>BODY_ZONE_PRECISE_EYES</c>), which hurts the eyes.
    /// </summary>
    [Fact]
    public void A_click_on_the_eyes_mouth_or_groin_selects_tgs_precise_zone_and_a_screwdriver_in_the_eyes_is_tgs_eye_stab()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        mob_living aliceMob = w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(10);
        using (w.World.Enter())
            Waiting.NoSleep(bobMob.forceMove(get_step(aliceMob, 4)));
        w.Run(10);
        Assert.False(Dm.truthy(aliceMob.combat_mode));
        ScreenEntry selector = alice.Session.Replica.Screen.Values.Single(e => In(w, () => w.World.Objects.TryGet(e.Id, out object o) && o is atom_movable_screen_zone_sel));

        // an empty hand, out of combat mode (tg's help): each precise zone is selected, and Alice's HUD selector draws it (tg's overlay of that state)
        foreach ((AimZone zone, string tg) in new[] { (AimZone.Eyes, BODY_ZONE_PRECISE_EYES), (AimZone.Mouth, BODY_ZONE_PRECISE_MOUTH), (AimZone.Groin, BODY_ZONE_PRECISE_GROIN) })
        {
            Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 26, zone), ClickButton.Left, ClickModifiers.None));
            w.Run(20);
            Assert.Equal(tg, In(w, () => aliceMob.zone_selected));
            ScreenEntry shown = alice.Session.Replica.Screen[selector.Id];
            Assert.True(alice.Session.Replica.TryGetAppearance(shown.Appearance, out AppearanceEntry look));
            Assert.Contains(look.Overlays, o => alice.Session.Replica.TryGetAppearance(o, out AppearanceEntry overlay) && alice.Session.Replica.ResolveString(overlay.IconState) == tg);
        }

        // a screwdriver: at the head, tg's attack (no eye stab: the eyes are unhurt); at the eyes, tg's eye stab
        using (w.World.Enter())
            DevCommands.Give(aliceMob, "/obj/item/screwdriver");
        w.Run(10);
        Assert.IsType<obj_item_screwdriver>(In(w, () => aliceMob.get_active_held_item()));
        var eyes = In(w, () => bobMob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes);
        Assert.NotNull(eyes);
        Assert.Equal(0f, In(w, () => Dm.num(eyes.damage)));
        float before = Brute(w, bobMob);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 29, AimZone.Head), ClickButton.Left, ClickModifiers.None));
        w.Run(20);
        Assert.Equal(BODY_ZONE_HEAD, In(w, () => aliceMob.zone_selected));
        Assert.True(Brute(w, bobMob) > before, "the screwdriver hit him (tg's attacked_by)");
        Assert.Equal(0f, In(w, () => Dm.num(eyes.damage)));
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 27, AimZone.Eyes), ClickButton.Left, ClickModifiers.None));
        w.Run(20);
        Assert.Equal(BODY_ZONE_PRECISE_EYES, In(w, () => aliceMob.zone_selected));
        Assert.InRange(In(w, () => Dm.num(eyes.damage)), 2f, 4f); // eyestab.dm: apply_organ_damage(rand(2, 4))
    }

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static float Brute(RealHeadlessWorld w, mob_living mob)
    {
        using (w.World.Enter())
            return Dm.num(mob.get_brute_loss());
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }
}
