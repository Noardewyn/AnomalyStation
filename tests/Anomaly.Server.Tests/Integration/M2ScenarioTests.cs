using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Tools.Bots;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M2's "Done when" through the real server, end to end (M2 integration, item 6): two clients, a headless Godot
/// client (<see cref="PlaySession"/>, predicting its own capsule) and a bot (an idle one: it stands still to be shoved; the
/// predicting bots run in BotServerTests and the soak), over the loopback at 100 ms one way. They walk; a
/// shove moves the other a full tile (D105); one lies down and gets up and the other sees the posture; a dev command
/// applies each damage type (brute and burn to a zone, toxin and oxygen), the doll of the private block shows the zones and
/// the other client sees the injury overlays; 150 toxin damage puts a human in hard crit and the missed breaths kill it in
/// tg's tick window (P2's and T1's 3,000-3,160 ticks), and the other client sees the dead body lying.
/// </summary>
public class M2ScenarioTests
{
    private readonly ITestOutputHelper _output;

    public M2ScenarioTests(ITestOutputHelper output) => _output = output;

    private static (int X, int Y) Tile(RealHeadlessWorld w, mob m)
    {
        using (w.World.Enter())
            return ((int)m.x, (int)m.y);
    }

    private static ReplicaEntity SeenBy(ReplicaWorld replica, mob m) => replica.Entities.Single(e => e.Id == m.dm_id);

    private static List<FoundZone> Doll(ReplicaWorld replica)
    {
        foreach (ScreenEntry entry in replica.Screen.Values)
        {
            var zones = new List<FoundZone>();
            HudReader.Collect(replica, entry.Appearance, zones);
            if (zones.Count > 0) return zones;
        }
        return new List<FoundZone>();
    }

    [Fact]
    public void TwoClientsWalkShoveLieDownTakeDamageAndDieAtTgsTimings()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 400, MinLatencySteps = 2, MaxLatencySteps = 2 });
        RealHeadlessWorld.Player alice = w.Join("Alice", inputBufferTarget: 2);
        Bot bob = w.AddBot("Bob", BotBehaviour.Idle, predict: false);
        mob_living aliceMob = w.MobOf(alice);
        var bobMob = w.World.Objects.TryGet(w.Host.AvatarOf("Bob"), out object o) ? o as mob_living : null;
        Assert.NotNull(bobMob);

        // Walk: Alice runs north for a second; Bob's replica follows her capsule.
        MotionState before = w.ServerState(alice);
        alice.Keys = MoveKeys.Forward;
        w.Run(20);
        alice.Keys = MoveKeys.None;
        w.Run(10);
        MotionState after = w.ServerState(alice);
        Assert.True(after.Y - before.Y > MotionMath.SubunitsPerTile, "Alice walked north");
        ReplicaEntity aliceSeen = SeenBy(bob.Replica, aliceMob);
        Assert.Equal((after.X, after.Y), (aliceSeen.State.PosX, aliceSeen.State.PosY));
        Assert.Equal(0, alice.Counters.Corrections);

        // Shove: Bob stands a tile east of Alice; her right click moves him a full tile east.
        (int ax, int ay) = Tile(w, aliceMob);
        using (w.World.Enter())
            Waiting.NoSleep(bobMob.forceMove(Builtins.locate(ax + 1, ay, 1)));
        w.Run(20);
        Assert.True(alice.Session.Click(bobMob.dm_id, ClickButton.Right, ClickModifiers.None));
        w.Run(30);
        Assert.Equal((ax + 2, ay), Tile(w, bobMob));
        Assert.True(LocomotionSeam.TryGetState(w.World, bobMob.dm_id, out MotionState bobState, out _));
        ReplicaEntity bobSeen = SeenBy(alice.Session.Replica, bobMob);
        Assert.Equal((bobState.X, bobState.Y), (bobSeen.State.PosX, bobSeen.State.PosY));

        // Lie down and get up: Bob's replica shows Alice's posture.
        alice.Session.Rest();
        w.Run(10);
        Assert.Equal(LYING_DOWN, aliceMob.body_position);
        Assert.True(PostureBits.IsLying(SeenBy(bob.Replica, aliceMob).State.Posture), "Bob sees Alice lying");
        alice.Session.Rest();
        w.Run(60); // tg's get-up do_after
        Assert.Equal(STANDING_UP, aliceMob.body_position);
        Assert.False(PostureBits.IsLying(SeenBy(bob.Replica, aliceMob).State.Posture), "Bob sees Alice standing again");

        // Damage of each type: the doll of Alice's private block and the injury overlays Bob sees.
        alice.Session.DevCommand("damage me brute 40 chest");
        alice.Session.DevCommand("damage me burn 30 l_arm");
        w.Run(5);
        alice.Session.DevCommand("damage me tox 10");
        alice.Session.DevCommand("damage me oxy 10");
        w.Run(10);
        Assert.Equal(40f, Dm.num(aliceMob.get_brute_loss()), 1);
        // the burn's own processing may already have healed a little of it (0.4 a step), depending on where the boot left its phase
        Assert.InRange(Dm.num(aliceMob.get_fire_loss()), 29f, 30.05f);
        Assert.True(Dm.num(aliceMob.get_tox_loss()) >= 10f);
        Assert.True(Dm.num(aliceMob.get_oxy_loss()) > 0f);
        List<FoundZone> doll = Doll(alice.Session.Replica);
        Assert.True(doll.Single(z => z.Zone.Zone == "chest").Zone.Level >= 1, "the chest's zone on Alice's doll");
        Assert.True(doll.Single(z => z.Zone.Zone == "l_arm").Zone.Level >= 1, "the left arm's zone on Alice's doll");
        Assert.Equal(0, doll.Single(z => z.Zone.Zone == "r_leg").Zone.Level);
        // tg's damage overlays (dam_mob.dmi "<type>_<zone>_<brute><burn>") are public: Bob's tables have them.
        Assert.Contains(bob.Replica.Strings, s => s.Contains("_chest_", StringComparison.Ordinal) && !s.EndsWith("_00", StringComparison.Ordinal));
        Assert.Contains(bob.Replica.Strings, s => s.Contains("_l_arm_0", StringComparison.Ordinal));
        // No health number is in Bob's bytes: the doll is private (D40), Bob's replica has no screen entry of Alice's.
        Assert.DoesNotContain(bob.Replica.Strings, s => s.StartsWith("chest", StringComparison.Ordinal) && HealthZones.TryParseState(s, out _));

        // Crit and death: a full heal, then 150 toxin damage: hard crit, and the missed breaths kill at tg's pace.
        alice.Session.DevCommand("fullheal me");
        w.Run(5);
        Assert.Equal(100f, aliceMob.health);
        alice.Session.DevCommand("damage me tox 150");
        long damagedAt = 0;
        for (int i = 0; i < 20 && damagedAt == 0; i++)
        {
            w.Run(1);
            if (Dm.num(aliceMob.get_tox_loss()) >= 150f)
                damagedAt = w.World.Tick;
        }
        Assert.NotEqual(0, damagedAt);
        Assert.Equal(HARD_CRIT, aliceMob.stat);
        long diedAt = 0;
        for (int i = 0; i < 3400 && diedAt == 0; i++)
        {
            w.Run(1);
            if (aliceMob.stat == DEAD)
                diedAt = w.World.Tick;
        }
        long ticks = diedAt - damagedAt;
        _output.WriteLine($"died {ticks} ticks after 150 toxin damage");
        Assert.InRange(ticks, 3000, 3400); // tg's missed breaths are random (and the master draws one first-fire offset per subsystem, so another subsystem changes the draws)
        w.Run(10);
        Assert.True(PostureBits.IsLying(SeenBy(bob.Replica, aliceMob).State.Posture), "Bob sees the dead body lying");
        Assert.True(Doll(alice.Session.Replica).All(z => z.Zone.Kind == ZoneKind.Dead), "Alice's doll is dead");
        Assert.Equal(0, w.World.RuntimeErrorCount);
        Assert.Equal(string.Empty, w.Err.ToString());
    }
}
