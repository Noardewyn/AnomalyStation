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
                zones.Append(BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, (byte)x, (byte)y) switch
                {
                    AimZone.Head => 'H', AimZone.Chest => 'C', AimZone.LeftArm => 'l', AimZone.RightArm => 'r',
                    AimZone.LeftLeg => 'L', AimZone.RightLeg => 'R', _ => '.',
                });
            zones.Append('\n');
        }
        output.WriteLine(zones.ToString());
        Assert.Equal(AimZone.Head, BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, 16, 27));
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
                if (BodyZoneAim.ZoneAt(tables, compositor, seen.State.Appearance, 2, (byte)x, (byte)y) == AimZone.Head)
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
                if (zone == AimZone.Head)
                    lyingHead++;
            }
        // The turned picture still offers every zone, and the head is about as large as it was (nearest sampling of a quarter turn is exact).
        Assert.Contains(AimZone.Head, zones);
        Assert.Contains(AimZone.Chest, zones);
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
