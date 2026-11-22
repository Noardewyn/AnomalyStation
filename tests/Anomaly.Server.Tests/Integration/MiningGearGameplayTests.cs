using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package J6 (D298) through the real server on MetaStation: a miner in his job's outfit stands by the mining dock's rock. His automatic
/// scanner pings the ore (the client sees the ping in the rock), the accelerator's shot drills it (the client sees the rock gone and the ore),
/// and walking onto the ore puts it in his satchel.
/// </summary>
public class MiningGearGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public MiningGearGameplayTests(ITestOutputHelper output) => _output = output;

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

    private static bool Sees(ReplicaWorld replica, atom thing) => replica.TryGet(thing.dm_id, out _);

    private static List<string> HeldStates(ReplicaWorld replica, RealHeadlessWorld w, atom thing)
    {
        Assert.True(replica.TryGet(In(w, () => thing.dm_id), out ReplicaEntity seen));
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State ?? "").ToList();
    }

    [MetaServerFact]
    public void A_miner_scans_shoots_rock_and_his_satchel_picks_up_the_ore()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 16, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount;

        // the outfit: tg's miner kit
        Do(w, () =>
        {
            foreach (obj_item worn in Dm.each<obj_item>(me.get_equipped_items(INCLUDE_POCKETS)))
                qdel(worn);
            var job = Dm.@new<datum_outfit_job_miner>();
            Waiting.NoSleep(me.equip_outfit_and_loadout(job));
        });
        w.Run(Second);
        var bag = In(w, () => me.r_store) as obj_item_storage_bag_ore;
        Assert.NotNull(bag);
        Assert.True(In(w, () => Dm.each<obj_item_t_scanner_adv_mining_scanner_lesser>(me.back).Any()), "no scanner in his backpack");
        Assert.True(In(w, () => Dm.each<obj_item_stack_candela_beacon_thirty>(me.back).Any()), "no beacons in his backpack");

        // beside the rock of Meta's mining dock, turned into an iron deposit
        var (rx, ry, rz, ox, oy) = In(w, () =>
        {
            foreach (turf_closed_mineral rock in Dm.each<turf_closed_mineral>(world))
                foreach (float dir in new float[] { NORTH, SOUTH, EAST, WEST })
                    if (get_step(rock, dir) is turf_open open && !Dm.truthy(open.density))
                        return ((int)rock.x, (int)rock.y, (int)rock.z, (int)open.x, (int)open.y);
            throw new Xunit.Sdk.XunitException("no rock on Meta");
        });
        Do(w, () =>
        {
            Waiting.NoSleep(locate(rx, ry, rz).ChangeTurf(typeof(turf_closed_mineral_iron)));
            Waiting.NoSleep(me.forceMove(locate(ox, oy, rz)));
        });
        w.Run(Second);

        // the automatic scanner, switched on in hand: the client sees the ping stand in the rock
        w.Host.EnqueueConsoleLine("give Assistant /obj/item/t_scanner/adv_mining_scanner/lesser");
        w.Run(5);
        Assert.IsType<obj_item_t_scanner_adv_mining_scanner_lesser>(In(w, () => me.get_active_held_item()));
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        var ping = In(w, () => Dm.locate<obj_effect_temp_visual_mining_overlay>(locate(rx, ry, rz)));
        Assert.NotNull(ping);
        Assert.True(Sees(replica, ping), "the client does not see the ping");
        Assert.True(replica.TryGet(In(w, () => ping.dm_id), out ReplicaEntity pingSeen));
        Assert.Contains("rock_iron", CrewOutfitTests.Icons(replica, pingSeen.State.Appearance).Select(i => i.State ?? ""));
        Do(w, () => Waiting.NoSleep(me.dropItemToGround(me.get_active_held_item() as obj_item)));

        // the accelerator: empty in hand, charged after 1.6 s, and its shot drills the rock
        w.Host.EnqueueConsoleLine("give Assistant /obj/item/gun/energy/recharge/kinetic_accelerator");
        w.Run(5);
        var gun = In(w, () => me.get_active_held_item()) as obj_item_gun_energy_recharge_kinetic_accelerator;
        Assert.NotNull(gun);
        w.Run(2 * Second);
        Assert.True(In(w, () => Dm.truthy(gun.can_shoot())));
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, rx, ry, rz), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.False(In(w, () => locate(rx, ry, rz) is turf_closed_mineral), "the bolt did not drill the rock");
        var ore = In(w, () => Dm.locate<obj_item_stack_ore_iron>(locate(rx, ry, rz)));
        Assert.NotNull(ore);
        Assert.True(Sees(replica, ore), "the client does not see the ore");

        // dropped, the accelerator empties and the client sees it: tg's "_empty" overlay on its picture
        Do(w, () => Waiting.NoSleep(me.dropItemToGround(gun)));
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(gun.can_shoot())));
        Assert.Contains("kineticgun_empty", HeldStates(replica, w, gun));

        // the bag picks it up as he steps on it
        Do(w, () => Waiting.NoSleep(me.forceMove(locate(rx, ry, rz))));
        w.Run(Second / 2);
        Assert.Same(bag, In(w, () => ore.loc));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
