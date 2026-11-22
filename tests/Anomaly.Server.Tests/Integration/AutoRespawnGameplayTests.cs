using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV580 (the owner, 2026-11-21): the dev auto-respawn through the real server. With <c>autorespawn on 3</c> a killed player sees
/// "Respawning in N seconds" on the screen (tg's /atom/movable/screen/text, replicated as the session's HUD maptext), and three seconds
/// later plays a new, living crew member at a spawn point; the corpse stays. A revive stops the countdown, and with the mode off the dead
/// stay dead.
/// </summary>
public class AutoRespawnGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // The countdown's texts in the player's client.screen (server side) and in the replica's screen entries (client side).
    private static string[] ServerCountdown(RealHeadlessWorld w, mob m)
        => In(w, () => m.client == null ? Array.Empty<string>()
            : Dm.each<atom_movable_screen_text>(m.client.screen).Select(t => t.maptext).Where(t => t != null && t.Contains("Respawning in")).ToArray());

    private static bool ReplicaShowsCountdown(RealHeadlessWorld.Player p)
    {
        ReplicaWorld replica = p.Session.Replica;
        return replica.Screen.Values.Any(e => replica.TryGetAppearance(e.Appearance, out AppearanceEntry a) && !a.Maptext.IsNone
            && replica.ResolveString(a.Maptext).Contains("Respawning in"));
    }

    [Fact]
    public void A_dead_player_counts_down_on_the_screen_and_respawns_in_a_new_body_at_a_spawn_point()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 21, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player p = w.Join("Alice");
        mob_living first = w.MobOf(p);
        Assert.NotNull(first);

        w.Host.EnqueueConsoleLine("autorespawn on 3");
        w.Run(2);
        Assert.Equal((true, 3f), w.Host.AutoRespawn);

        w.Host.EnqueueConsoleLine("kill Alice");
        w.Run(Second / 2);
        Assert.Equal((float)Defines.DEAD, In(w, () => first.stat));
        string text = Assert.Single(ServerCountdown(w, first));
        Assert.Contains("Respawning in 3 seconds", text);
        Assert.True(ReplicaShowsCountdown(p), "the client gets the countdown as HUD maptext");

        w.Run(Second);
        Assert.Contains("Respawning in 2 seconds", Assert.Single(ServerCountdown(w, first)));
        Assert.Same(first, w.MobOf(p));

        w.Run(2 * Second + 5);
        mob_living second = w.MobOf(p);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.IsType<mob_living_carbon_human>(second);
        Assert.NotEqual((float)Defines.DEAD, In(w, () => second.stat));
        Assert.Equal((float)Defines.DEAD, In(w, () => first.stat)); // the corpse stays where it died
        Assert.Null(In(w, () => first.client));
        Assert.Contains(In(w, () => (get_turf(second) is turf t ? (t.x, t.y, t.z) : (0f, 0f, 0f))),
            w.Host.SpawnPoints.Select(sp => ((float)sp.X, (float)sp.Y, (float)sp.Z)));
        Assert.Empty(ServerCountdown(w, second));
        w.Run(5);
        Assert.False(ReplicaShowsCountdown(p), "the countdown is gone with the respawn");
        Assert.Equal(second.dm_id, p.Session.Replica.OwnEntity);

        // a revive stops the countdown: the same body plays on
        w.Host.EnqueueConsoleLine("kill Alice");
        w.Run(Second);
        Assert.Single(ServerCountdown(w, second));
        w.Host.EnqueueConsoleLine("revive Alice");
        w.Run(4 * Second);
        Assert.Same(second, w.MobOf(p));
        Assert.NotEqual((float)Defines.DEAD, In(w, () => second.stat));
        Assert.Empty(ServerCountdown(w, second));

        // off: the dead stay dead
        w.Host.EnqueueConsoleLine("autorespawn off");
        w.Host.EnqueueConsoleLine("kill Alice");
        w.Run(5 * Second);
        Assert.Same(second, w.MobOf(p));
        Assert.Equal((float)Defines.DEAD, In(w, () => second.stat));
        Assert.Empty(ServerCountdown(w, second));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_ghost_of_a_dead_body_respawns_too()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 22, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player p = w.Join("Alice");
        mob_living first = w.MobOf(p);
        w.Host.EnqueueConsoleLine("autorespawn on 2");
        w.Host.EnqueueConsoleLine("kill Alice");
        w.Run(5);
        mob ghost = In(w, () => first.ghostize(Dm.TRUE) as mob);
        Assert.IsType<mob_dead_observer>(ghost);
        w.Run(Second / 2);
        Assert.Single(ServerCountdown(w, ghost)); // the ghost's new HUD gets the countdown back
        w.Run(2 * Second);
        mob_living second = w.MobOf(p);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.NotEqual((float)Defines.DEAD, In(w, () => second.stat));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
