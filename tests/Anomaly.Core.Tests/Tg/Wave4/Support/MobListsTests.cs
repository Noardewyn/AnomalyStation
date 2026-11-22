using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Support;

/// <summary>
/// The mob list bookkeeping of code/modules/mob/mob_lists.dm: <c>|=</c> keeps a mob once, <c>+=</c> (the suicide list)
/// does not, the QDELETED guards, and a mob without a client never reaches the player lists.
/// </summary>
public class MobListsTests
{
    private static mob_test_support_mob Mob() => Dm.@new<mob_test_support_mob>(locate(1, 1, 1));

    private static int Count(DmList list, object item) => Dm.each(list).Cast<object>().Count(x => ReferenceEquals(x, item));

    [Fact]
    public void TheMobListKeepsAMobOnceHoweverOftenItIsAdded()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        mob.add_to_mob_list();
        mob.add_to_mob_list();
        Assert.Equal(1, Count(GLOB.mob_list, mob));
        mob.remove_from_mob_list();
        Assert.Equal(0, GLOB.mob_list.Count);
        mob.remove_from_mob_list(); // removing what is not there is no error
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void TheAliveAndDeadListsAreIdempotentAndSkipADeletedMob()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        mob.add_to_alive_mob_list();
        mob.add_to_alive_mob_list();
        mob.add_to_dead_mob_list();
        mob.add_to_dead_mob_list();
        Assert.Equal(1, Count(GLOB.alive_mob_list, mob));
        Assert.Equal(1, Count(GLOB.dead_mob_list, mob));
        // No client: the player lists are not touched.
        Assert.Equal(0, GLOB.alive_player_list.Count + GLOB.dead_player_list.Count + GLOB.keyloop_list.Count + GLOB.player_list.Count);
        mob.remove_from_alive_mob_list();
        mob.remove_from_dead_mob_list();
        Assert.Equal(0, GLOB.alive_mob_list.Count + GLOB.dead_mob_list.Count);

        var deleted = Mob();
        // QDELETED(src) without running another package's Destroy chain: the mark qdel leaves (gc_destroyed = world.time).
        deleted.gc_destroyed = world.time;
        deleted.add_to_alive_mob_list();
        deleted.add_to_dead_mob_list();
        Assert.Equal(0, GLOB.alive_mob_list.Count + GLOB.dead_mob_list.Count);
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void TheSuicideListAddsWithPlusSoAMobCanBeThereTwice()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        mob.add_to_mob_suicide_list();
        mob.add_to_mob_suicide_list();
        Assert.Equal(2, Count(GLOB.suicided_mob_list, mob)); // GLOB.suicided_mob_list += src (mob_lists.dm:25)
        mob.remove_from_mob_suicide_list();
        Assert.Equal(1, Count(GLOB.suicided_mob_list, mob));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void ThePlayerListProcsKeepTheirOrderUpToTheParts()
    {
        using var w = new SupportWorld();
        DmWorldState.Of(w.World).NotTranslatedThrows = false; // (M2 P2: the dead-player part runs, SSlag_switch read as no measure)
        var listener = Dm.@new<obj_test_support_listener>();
        listener.RegisterSignal(SSdcs, COMSIG_GLOB_PLAYER_LOGIN, nameof(obj_test_support_listener.on_mock));
        listener.RegisterSignal(SSdcs, COMSIG_GLOB_PLAYER_LOGOUT, nameof(obj_test_support_listener.on_mock));

        var dead = Mob();
        dead.stat = DEAD;
        dead.add_to_player_list();
        Assert.Equal(1, Count(GLOB.player_list, dead));
        Assert.Equal(1, Count(GLOB.dead_player_list, dead));
        Assert.Equal(1, Count(GLOB.keyloop_list, dead)); // M2 P2: no lag switch measure is on (SSlag_switch__gap, mob_lists.dm:74)
        Assert.True(Dm.eq(listener.times_called, 1));

        var alive = Mob();
        alive.add_to_player_list();
        Assert.Equal(1, Count(GLOB.alive_player_list, alive));
        Assert.Equal(1, Count(GLOB.keyloop_list, alive));
        Assert.True(Dm.eq(listener.times_called, 2));

        alive.remove_from_player_list();
        dead.remove_from_player_list();
        Assert.Equal(0, GLOB.player_list.Count + GLOB.alive_player_list.Count + GLOB.dead_player_list.Count + GLOB.keyloop_list.Count);
        Assert.True(Dm.eq(listener.times_called, 4));
        Assert.DoesNotContain(w.Runtimes, e => e.Message.Contains("code/modules/mob/mob_lists.dm:74"));
        // is_antag (mob_helpers.dm:256) is translated (M1 P1): a mob without a mind is no antagonist, :106-107 runs through
        Assert.All(w.Runtimes, e => Assert.IsType<DmNotTranslatedException>(e));
    }

    [Fact]
    public void ANewPlayerIsNeverADeadPlayerAndTheAntagListLetsGo()
    {
        using var w = new SupportWorld();
        var new_player = new mob_dead_new_player();
        new_player.add_to_current_dead_players();
        Assert.Equal(0, GLOB.dead_player_list.Count);

        var mob = Mob();
        Dm.combine(ref GLOB.current_living_antags, mob);
        mob.remove_from_current_living_antags();
        Assert.Equal(0, GLOB.current_living_antags.Count);
        Assert.Empty(w.Runtimes);
    }
}
