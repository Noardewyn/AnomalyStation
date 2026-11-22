using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7x closing check's find: a gun's firing effect (tg's temp_visual/dir_setting/firing_effect) sits in the shooter's vis_contents with the
/// shooter as its loc, so the replicator, which only knew flick visuals in nullspace, never sent it: no client drew a muzzle flash (D176) and
/// the own view model never kicked. Now both the shooter's and a watcher's client get it as a visual drawn on the shooter, and lose it with the effect.
/// </summary>
public class FiringEffectReplicationTests
{
    private const int Second = 20;
    private const string Firing = "/obj/effect/temp_visual/dir_setting/firing_effect";

    [Fact]
    public void A_guns_firing_effect_reaches_the_shooters_and_the_watchers_clients_as_a_visual_on_the_shooter()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 23, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        using (w.World.Enter())
        {
            Waiting.NoSleep(bobMob.forceMove(get_step(get_step(aliceMob, Dm.EAST), Dm.EAST)));
            var gun = Dm.@new<obj_item_gun_energy_disabler>(get_turf(aliceMob));
            Waiting.NoSleep(aliceMob.put_in_active_hand(gun));
        }
        w.Run(Second);

        var seen = new Dictionary<string, List<ReplicaEntity>> { ["Alice"] = new(), ["Bob"] = new() };
        void Watch(long _)
        {
            foreach ((string name, RealHeadlessWorld.Player p) in new[] { ("Alice", alice), ("Bob", bob) })
                foreach (ReplicaEntity e in p.Session.Replica.Entities)
                    if (e.TypePath != null && e.TypePath.StartsWith(Firing, StringComparison.Ordinal) && !seen[name].Any(x => x.Id == e.Id))
                        seen[name].Add(e);
        }
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
        w.Run(Second, Watch);

        foreach ((string name, List<ReplicaEntity> effects) in seen)
        {
            ReplicaEntity effect = Assert.Single(effects);
            Assert.True(effect.IsVisual, name + ": drawn on its host");
            Assert.Equal(aliceMob.dm_id, effect.VisHost);
        }
        // it is gone with the effect (3 deciseconds)
        Assert.DoesNotContain(alice.Session.Replica.Entities, e => e.TypePath != null && e.TypePath.StartsWith(Firing, StringComparison.Ordinal));
        Assert.DoesNotContain(bob.Session.Replica.Entities, e => e.TypePath != null && e.TypePath.StartsWith(Firing, StringComparison.Ordinal));
    }
}
