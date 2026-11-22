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
/// Gaps package F1 (D298) through the real server on MetaStation: an atmospheric technician takes a firepack from Meta's atmospherics lockers, switches
/// its nozzle to the resin launcher and shoots a capsule down the hall; the client sees the resin foam (tg's mfoam) and then the resin walls (atmos_resin).
/// </summary>
public class ChemSmokeFoamGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public ChemSmokeFoamGameplayTests(ITestOutputHelper output) => _output = output;

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

    private static List<string> States(ReplicaWorld replica, RealHeadlessWorld w, atom thing)
    {
        Assert.True(replica.TryGet(In(w, () => thing.dm_id), out ReplicaEntity seen), "the client does not see " + In(w, () => thing.type));
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State ?? "").ToList();
    }

    [MetaServerFact]
    public void An_atmos_technician_shoots_resin_from_the_firepack_and_the_client_sees_the_foam_and_the_walls()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 16, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Atmos");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // the outfit and a firepack from Meta's atmospherics lockers
        var pack = In(w, () => Dm.each<obj_item_watertank_atmos>(world).FirstOrDefault());
        Assert.NotNull(pack);
        Do(w, () =>
        {
            foreach (obj_item worn in Dm.each<obj_item>(me.get_equipped_items(INCLUDE_POCKETS)))
                qdel(worn);
            Waiting.NoSleep(me.equip_outfit_and_loadout(Dm.@new<datum_outfit_job_atmos>()));
        });
        w.Run(Second);

        // a stretch of open floor three tiles long from where he stands
        var (sx, sy, sz, tx, ty) = In(w, () =>
        {
            foreach (turf_open start in Dm.each<turf_open>(world))
            {
                if (start.z != me.z || Dm.truthy(start.density) || start is turf_open_space || Dm.each<atom_movable>(start).Any(a => Dm.truthy(a.density)))
                    continue;
                foreach (float dir in new float[] { EAST, NORTH, WEST, SOUTH })
                {
                    turf t1 = get_step(start, dir), t2 = t1 == null ? null : get_step(t1, dir), t3 = t2 == null ? null : get_step(t2, dir);
                    bool clear = new[] { t1, t2, t3 }.All(t => t is turf_open open && open is not turf_open_space && !Dm.truthy(open.density) && !Dm.each<atom_movable>(open).Any(a => Dm.truthy(a.density)))
                        && get_area(start) == get_area(t3);
                    if (clear)
                        return ((int)start.x, (int)start.y, (int)start.z, (int)t3.x, (int)t3.y);
                }
            }
            throw new Xunit.Sdk.XunitException("no open hall on Meta");
        });
        Do(w, () =>
        {
            Waiting.NoSleep(me.forceMove(locate(sx, sy, sz)));
            Waiting.NoSleep(pack.forceMove(me.loc));
            if (me.back != null)
                Waiting.NoSleep(me.dropItemToGround(me.back as obj_item));
            Waiting.NoSleep(me.equip_to_slot_or_del(pack, ITEM_SLOT_BACK));
            Waiting.NoSleep(pack.toggle_mister(me));
        });
        w.Run(Second);
        var nozzle = In(w, () => me.get_active_held_item()) as obj_item_extinguisher_mini_nozzle;
        Assert.NotNull(nozzle);

        // Z in hand: the extinguisher becomes the resin launcher
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.Equal(RESIN_LAUNCHER, In(w, () => Dm.num(nozzle.nozzle_mode)));

        // the shot down the hall: the capsule bursts into resin foam the client sees as tg's mfoam
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, tx, ty, sz), ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second);
        var foam = In(w, () => Dm.each<obj_effect_particle_effect_fluid_foam_metal_resin>(world).ToList());
        _output.WriteLine("resin foam nodes: " + foam.Count);
        Assert.True(foam.Count > 3, "the capsule burst into resin foam");
        Assert.Contains("mfoam", States(replica, w, foam[0]));

        // eight seconds later the foam sets into the see-through resin walls
        w.Run(9 * Second);
        var walls = In(w, () => Dm.each<obj_structure_foamedmetal_resin>(world).ToList());
        _output.WriteLine("resin walls: " + walls.Count);
        Assert.True(walls.Count > 3, "the resin foam set into walls");
        Assert.Contains("atmos_resin", States(replica, w, walls[0]));
        Assert.Equal(0, In(w, () => Dm.each<obj_effect_particle_effect_fluid_foam>(world).Count()));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
