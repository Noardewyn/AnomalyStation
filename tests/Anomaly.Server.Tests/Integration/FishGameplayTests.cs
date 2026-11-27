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
/// Through the real server: a joined cook cuts a fish on a table into fillets with a kitchen knife, and a joined player mounts a
/// fish on a wall trophy mount and takes it down again.
/// </summary>
public class FishGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static bool RunUntil(RealHeadlessWorld w, Func<bool> done, int seconds)
    {
        for (int t = 0; t < Second * seconds; t++)
        {
            if (In(w, done))
                return true;
            w.Run(1);
        }
        return In(w, done);
    }

    [Fact]
    public void A_cook_cuts_a_fish_into_fillets()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14621, TimeoutSteps = 12000 });
        RealHeadlessWorld.Player p = w.Join("Cook");
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);
        var me = (mob_living_carbon_human)w.MobOf(p);
        Do(w, () => Waiting.Detach(SSjob.equip_rank(me, SSjob.get_job_type(typeof(datum_job_cook)) as datum_job, me.client)));
        w.Run(Second / 2);
        Do(w, () => Waiting.NoSleep(me.drop_all_held_items()));
        int bootRuntimes = w.World.RuntimeErrorCount;

        // a goldfish on a table a tile east, a kitchen knife in hand
        var (fish, spot, expected) = In(w, () =>
        {
            var spot = (turf)get_step(me, EAST);
            Dm.@new<obj_structure_table>(spot);
            var fish = Dm.@new<obj_item_fish_goldfish>(spot);
            var knife = Dm.@new<obj_item_knife_kitchen>(me.loc);
            Waiting.NoSleep(me.put_in_active_hand(knife));
            int expected = (int)max(round(fish.num_fillets * Dm.num(fish.size) / FISH_FILLET_NUMBER_SIZE_DIVISOR, 1), 1);
            return (fish, spot, expected);
        });
        w.Run(Second);
        Assert.Contains(replica.Entities, e => e.Id == fish.dm_id);

        Assert.True(p.Session.Click(On(fish), ClickButton.Left, ClickModifiers.None));
        Assert.True(RunUntil(w, () => QDELETED(fish), 10), "the cook cut the fish up");
        int fillets = In(w, () => Dm.each<obj_item_food_fishmeat>(spot.contents).Count());
        output.WriteLine($"fillets: {fillets} (expected {expected})");
        Assert.Equal(expected, fillets);
        var fillet = In(w, () => Dm.each<obj_item_food_fishmeat>(spot.contents).First());
        w.Run(Second / 2);
        Assert.Contains(replica.Entities, e => e.Id == fillet.dm_id);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_player_mounts_a_fish_and_takes_it_down()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14622, TimeoutSteps = 12000 });
        RealHeadlessWorld.Player p = w.Join("Angler");
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);
        var me = (mob_living_carbon_human)w.MobOf(p);
        Do(w, () => Waiting.NoSleep(me.drop_all_held_items()));
        int bootRuntimes = w.World.RuntimeErrorCount;

        var (mount, fish) = In(w, () =>
        {
            var mount = Dm.@new<obj_structure_fish_mount>(get_step(me, NORTH));
            var fish = Dm.@new<obj_item_fish_goldfish>(me.loc);
            Waiting.NoSleep(me.put_in_active_hand(fish));
            return (mount, fish);
        });
        w.Run(Second);
        Assert.Contains(replica.Entities, e => e.Id == mount.dm_id);

        Assert.True(p.Session.Click(On(mount), ClickButton.Left, ClickModifiers.None));
        Assert.True(RunUntil(w, () => mount.mounted_fish == fish, 6), "the fish is mounted");
        Assert.True(In(w, () => Dm.@in(fish, mount.vis_contents)), "the mount shows the fish");
        Assert.Equal(In(w, () => me.name), In(w, () => Dm.str(fish.catcher_name)));

        // the right click takes it down into the empty hand
        Assert.True(p.Session.Click(On(mount), ClickButton.Right, ClickModifiers.None));
        Assert.True(RunUntil(w, () => fish.loc == me, 6), "the fish is back in the hand");
        Assert.Null(In(w, () => mount.mounted_fish));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
