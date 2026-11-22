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
/// M7 slice 1 (cable.dm:210, :441, :688; power.dm:389, :418): on MetaStation a player beside an exposed cable of the station's
/// grid, with wirecutters in hand, clicks the cable: his client sees it gone and the piece of coil it leaves, its neighbours'
/// icons end at the cut, and on the server the network splits in two. He picks the piece up and clicks the bare plating with
/// it: the cable is laid again where he clicked, his client sees it linked to both neighbours, and the two networks merge.
/// </summary>
public class CableCutGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    // The cables that `from` reaches without passing `cut` (the links propagate_network walks, cable.dm:403).
    private static HashSet<obj_structure_cable> Side(obj_structure_cable from, obj_structure_cable cut)
    {
        var seen = new HashSet<obj_structure_cable>(ReferenceEqualityComparer.Instance) { from };
        var queue = new Queue<obj_structure_cable>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            foreach (obj_structure_cable next in Dm.each_cast<obj_structure_cable>(queue.Dequeue().get_cable_connections()))
            {
                if (next != cut && seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        return seen;
    }

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // An exposed east-west cable on plating in a powered station room, alone on its tile with no power machine, whose cut
    // splits its network into two runs of five cables or more, and an open tile north of it for the player.
    private static obj_structure_cable PickCable()
    {
        foreach (obj_structure_cable c in Dm.each_cast<obj_structure_cable>(GLOB.cable_list).OrderBy(c => c.dm_id))
        {
            if (c.loc is not turf_open t || !Dm.eq(c.z, 1) || t.underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
                continue;
            if (c.GetType() != typeof(obj_structure_cable) || !Dm.eq(c.linked_dirs, EAST + WEST) || !Clear(t))
                continue;
            if (Dm.each_cast<atom>(t.contents).Any(a => a != c && (a is obj_structure_cable || a is obj_machinery_power)))
                continue;
            if (!Clear(get_step(t, NORTH)))
                continue;
            var west = Dm.locate<obj_structure_cable>(get_step(t, WEST));
            var east = Dm.locate<obj_structure_cable>(get_step(t, EAST));
            if (west == null || east == null || west.powernet != c.powernet || east.powernet != c.powernet)
                continue;
            if (get_area(c) is not area room || !Dm.@in(room.type, GLOB.the_station_areas) || !Dm.truthy(room.requires_power))
                continue;
            HashSet<obj_structure_cable> westSide = Side(west, c);
            if (westSide.Contains(east) || westSide.Count < 5 || Side(east, c).Count < 5)
                continue;
            return c;
        }
        return null;
    }

    [MetaServerFact]
    public void A_cut_cable_leaves_a_piece_of_coil_and_splits_its_network_and_laying_it_back_merges_them()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 73, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;

        obj_structure_cable cable = In(w, PickCable);
        Assert.NotNull(cable);
        int x = In(w, () => (int)cable.x), y = In(w, () => (int)cable.y), z = In(w, () => (int)cable.z);
        obj_structure_cable west = In(w, () => Dm.locate<obj_structure_cable>(locate(x - 1, y, z)));
        obj_structure_cable east = In(w, () => Dm.locate<obj_structure_cable>(locate(x + 1, y, z)));
        datum_powernet grid = In(w, () => cable.powernet);
        output.WriteLine($"the cable at {x},{y} in {In(w, () => Dm.str(((area)get_area(cable)).name))}: its network has {In(w, () => grid.cables.len)} cables and {In(w, () => grid.nodes.len)} machines");
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x, y + 1, z))));
        var cutters = In(w, () => Dm.@new<obj_item_wirecutters>(me.loc));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(cutters)))));
        // the live grid shocks a bare hand half the time (cable.dm:215, the shocks slice: ShockGameplayTests); an engineer's
        // insulated gloves keep this test on the cut and the lay
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_gloves_color_yellow>(me.loc), ITEM_SLOT_GLOVES)))));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        string IconOf(atom a)
        {
            ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
            return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
        }
        Assert.Equal("l2-4-8", IconOf(cable));
        Assert.Equal(In(w, () => west.icon_state), IconOf(west));

        // the cut: a click on the cable with the wirecutters
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, cable.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        obj_item_stack_cable_coil piece = In(w, () => Dm.locate<obj_item_stack_cable_coil>(locate(x, y, z)));
        Assert.True(In(w, () => QDELETED(cable)), "the cable is cut");
        Assert.NotNull(piece);
        Assert.Equal(1f, In(w, () => Dm.num(piece.amount)));
        Assert.DoesNotContain(replica.Entities, e => e.Id == cable.dm_id);
        Assert.Equal("coil1", IconOf(piece)); // the coil's update_icon_state (cable.dm:552): a piece of one
        // the neighbours' links end at the cut (disconnect_cable, cable.dm:134), and the client draws them so
        Assert.Equal(In(w, () => west.icon_state), IconOf(west));
        Assert.Equal(In(w, () => east.icon_state), IconOf(east));
        Assert.DoesNotContain("4", In(w, () => west.icon_state).Split('-').Skip(1));
        datum_powernet westNet = In(w, () => west.powernet), eastNet = In(w, () => east.powernet);
        Assert.NotNull(westNet);
        Assert.NotNull(eastNet);
        Assert.NotSame(westNet, eastNet); // the network split (cut_cable_from_powernet, cable.dm:441)
        Assert.True(In(w, () => westNet == grid || eastNet == grid), "one side keeps the old network");
        output.WriteLine($"after the cut: west {In(w, () => westNet.cables.len)} cables, east {In(w, () => eastNet.cables.len)}; the client sees {IconOf(west)} and {IconOf(east)}");

        // the lay: the cutters put away, the piece picked up, a click on the bare plating with it
        In(w, () => qdel(cutters));
        w.Run(2);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, piece.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(piece, In(w, () => me.get_active_held_item()));
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, x, y, z), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        obj_structure_cable laid = In(w, () => Dm.locate<obj_structure_cable>(locate(x, y, z)));
        Assert.NotNull(laid);
        Assert.True(In(w, () => QDELETED(piece)), "the piece is used up");
        Assert.Equal("l2-4-8", IconOf(laid));
        Assert.Equal(In(w, () => west.icon_state), IconOf(west));
        datum_powernet merged = In(w, () => laid.powernet);
        Assert.Same(merged, In(w, () => west.powernet)); // mergeConnectedNetworks (cable.dm:340)
        Assert.Same(merged, In(w, () => east.powernet));
        Assert.Contains(merged, In(w, () => Dm.each_cast<datum_powernet>(SSmachines.powernets).ToList()));
        Assert.True(In(w, () => QDELETED(westNet) || QDELETED(eastNet)), "the smaller network was absorbed");
        output.WriteLine($"after the lay: one network of {In(w, () => merged.cables.len)} cables; the client sees {IconOf(laid)}");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the cut or the lay ran into a runtime
    }
}
