using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Protocol version 95 (DV586): the own replica knows the melee click cooldown the server would set now, the active hand's
/// attack_speed through the mob's modifiers, so the client swings and whooshes only on clicks the server takes as blows.
/// </summary>
public class MeleeCooldownReplicationTests
{
    [Fact]
    public void TheOwnReplicaHasTheActiveHandsAttackSpeed()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob_living me = w.MobOf(alice);
        w.Run(10);
        Assert.Equal(800, alice.Session.Replica.Own.MeleeCooldownMs); // an empty hand: tg's CLICK_CD_MELEE

        using (w.World.Enter())
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(Dm.@new<obj_item_melee_beesword>(me.loc)))));
        w.Run(5);
        Assert.Equal(200, alice.Session.Replica.Own.MeleeCooldownMs); // the bee sword's CLICK_CD_RAPID

        using (w.World.Enter())
            me.next_move_modifier = 2; // a slowing effect (tg's debuffs multiply it)
        w.Run(5);
        Assert.Equal(400, alice.Session.Replica.Own.MeleeCooldownMs);
    }
}
