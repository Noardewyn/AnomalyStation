using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 slice 1: the power network core (modules/power/powernet.dm, power.dm, cable.dm, terminal.dm; controllers/subsystem/
/// machines.dm:77, :94, :170) in a booted toy room with a player, through his clicks: SSmachines' Initialize builds one
/// network per connected run of cables (makepowernets), wirecutters cut a cable (wirecutter_act, deconstruct: a piece of coil,
/// cut_cable_from_powernet, the far side's network on the next tick's timer), the coil lays it back where the player clicks
/// (turf.item_interaction, place_turf: the networks merge), a ring stays one network when cut, and the power accounting over
/// SSmachines' cycles (add_avail, add_load, add_delayedload, surplus, avail and the reset of fire).
/// </summary>
public class PowernetTests
{
    private const int Second = 20;
    private const int LineY = 4, ShortY = 7, RingX = 9, RingY = 4;

    // Plating (UNDERFLOOR_INTERACTABLE) under every cable; a line of five at y 4 (x 2-6), a separate line of three at y 7
    // (x 2-4), and a ring of eight around (9, 4).
    private static readonly (int X, int Y)[] Line = { (2, LineY), (3, LineY), (4, LineY), (5, LineY), (6, LineY) };
    private static readonly (int X, int Y)[] Short = { (2, ShortY), (3, ShortY), (4, ShortY) };
    private static readonly (int X, int Y)[] Ring =
    {
        (RingX - 1, RingY - 1), (RingX, RingY - 1), (RingX + 1, RingY - 1), (RingX + 1, RingY),
        (RingX + 1, RingY + 1), (RingX, RingY + 1), (RingX - 1, RingY + 1), (RingX - 1, RingY),
    };

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed = 7101, Action<World> extras = null)
    {
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            foreach (var (x, y) in Line.Concat(Short).Concat(Ring).Append((RingX, RingY)).Append((4, LineY + 1)))
                Dm.@new<turf_open_floor_plating>(locate(x, y, 1));
            foreach (var (x, y) in Line.Concat(Short).Concat(Ring))
                Dm.@new<obj_structure_cable>(locate(x, y, 1));
            extras?.Invoke(w);
        });
        mob_living_carbon_human bob = r.Join(4, LineY + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static obj_structure_cable CableAt(AudioRoom r, int x, int y)
        => r.In(() => Dm.locate<obj_structure_cable>(locate(x, y, 1)));

    private static datum_powernet Net(AudioRoom r, int x, int y) => r.In(() => CableAt(r, x, y).powernet);

    private static int Cables(AudioRoom r, datum_powernet net) => r.In(() => (int)net.cables.len);

    private static void MachinesTick(AudioRoom r, int runs = 1) => r.Step(runs * r.In(() => (int)Dm.num(SSmachines.wait)) * 2);

    [Fact]
    public void SSmachinesBuildsOneNetworkPerConnectedRunOfCables()
    {
        var (r, _) = Room();
        r.In(() =>
        {
            datum_powernet line = CableAt(r, 2, LineY).powernet, shortLine = CableAt(r, 2, ShortY).powernet, ring = CableAt(r, RingX, RingY - 1).powernet;
            Assert.NotNull(line);
            Assert.NotNull(shortLine);
            Assert.NotNull(ring);
            Assert.All(Line, p => Assert.Same(line, CableAt(r, p.X, p.Y).powernet));
            Assert.All(Short, p => Assert.Same(shortLine, CableAt(r, p.X, p.Y).powernet));
            Assert.All(Ring, p => Assert.Same(ring, CableAt(r, p.X, p.Y).powernet));
            Assert.Equal(5f, line.cables.len);
            Assert.Equal(3f, shortLine.cables.len);
            Assert.Equal(8f, ring.cables.len);
            Assert.NotSame(line, shortLine);
            Assert.NotSame(line, ring);
            // makepowernets (machines.dm:77): every network is SSmachines', every cable of the global list has one
            Assert.Equal(3f, SSmachines.powernets.len);
            Assert.All(Dm.each_cast<obj_structure_cable>(GLOB.cable_list), c => Assert.True(Dm.@in(c, c.powernet.cables)));
            // the cable's links (connect_cable): the middle of the line links east and west
            Assert.Equal(EAST + WEST, CableAt(r, 4, LineY).linked_dirs);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WirecuttersCutACableIntoAPieceOfCoilAndTheNetworkSplitsThenTheCoilLaysItBackAndTheyMerge()
    {
        var (r, bob) = Room();
        datum_powernet before = Net(r, 2, LineY);
        obj_structure_cable cut = CableAt(r, 4, LineY);

        // the cut: a click on the cable with wirecutters (tool_act -> wirecutter_act, cable.dm:210)
        WallTests.Hold<obj_item_wirecutters>(r, bob);
        WallTests.Click(r, bob, cut, 0.05f);
        r.In(() =>
        {
            Assert.True(QDELETED(cut));
            Assert.Null(Dm.locate<obj_structure_cable>(locate(4, LineY, 1)));
            // atom_deconstruct (cable.dm:156): a piece of coil of the cable's colour where it was
            obj_item_stack_cable_coil piece = Dm.locate<obj_item_stack_cable_coil>(locate(4, LineY, 1));
            Assert.NotNull(piece);
            Assert.Equal(1f, Dm.num(piece.amount));
            Assert.Equal("yellow", piece.cable_color);
            Assert.Equal("coil1", piece.icon_state);
            Assert.Equal("cable piece", piece.name);
            // disconnect_cable (cable.dm:134): the neighbours no longer link to the cut tile and show it
            Assert.Equal(WEST, CableAt(r, 3, LineY).linked_dirs);
            Assert.Equal(EAST, CableAt(r, 5, LineY).linked_dirs);
            Assert.Equal("l2-8", CableAt(r, 3, LineY).icon_state);
            Assert.Equal("l2-4", CableAt(r, 5, LineY).icon_state);
        });
        r.Step(2); // the timer of auto_propagate_cut_cable (cable.dm:469: a 0 delay, the next tick)
        r.In(() =>
        {
            datum_powernet west = CableAt(r, 2, LineY).powernet, east = CableAt(r, 6, LineY).powernet;
            Assert.Same(before, east); // the first linked neighbour in GLOB.cardinals' order (east before west) keeps the old network
            Assert.NotSame(west, east);
            Assert.Same(west, CableAt(r, 3, LineY).powernet);
            Assert.Same(east, CableAt(r, 5, LineY).powernet);
            Assert.Equal(2f, west.cables.len);
            Assert.Equal(2f, east.cables.len);
            Assert.Contains(east, Dm.each_cast<datum_powernet>(SSmachines.powernets));
            Assert.Equal(4f, SSmachines.powernets.len);
        });

        // the lay: a click on the plating with the coil (turf.dm:417 -> place_turf, cable.dm:688)
        obj_item_stack_cable_coil coil = WallTests.Hold<obj_item_stack_cable_coil>(r, bob, 10);
        WallTests.Click(r, bob, r.In(() => locate(4, LineY, 1)), 0.05f);
        r.In(() =>
        {
            obj_structure_cable laid = CableAt(r, 4, LineY);
            Assert.NotNull(laid);
            Assert.Equal(9f, Dm.num(coil.amount));
            Assert.Equal(EAST + WEST, laid.linked_dirs);
            Assert.Equal("l2-4-8", laid.icon_state);
            Assert.Equal("l2-4-8", CableAt(r, 3, LineY).icon_state);
            // mergeConnectedNetworks (cable.dm:340): one network again, the larger absorbing the rest
            datum_powernet net = laid.powernet;
            Assert.All(Line, p => Assert.Same(net, CableAt(r, p.X, p.Y).powernet));
            Assert.Equal(5f, net.cables.len);
            Assert.Equal(3f, SSmachines.powernets.len);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACutRingStaysOneNetwork()
    {
        var (r, _) = Room();
        datum_powernet before = Net(r, RingX, RingY - 1);
        r.In(() => Waiting.NoSleep(CableAt(r, RingX - 1, RingY - 1).deconstruct()));
        r.Step(2);
        r.In(() =>
        {
            // the far neighbour's propagate_network walks the ring round to the near one: all seven move to the new network,
            // and the emptied old one is deleted (remove_cable, powernet.dm:43)
            datum_powernet after = CableAt(r, RingX, RingY - 1).powernet;
            Assert.NotSame(before, after);
            Assert.True(QDELETED(before));
            Assert.DoesNotContain(before, Dm.each_cast<datum_powernet>(SSmachines.powernets));
            Assert.Equal(7f, after.cables.len);
            Assert.All(Ring.Skip(1), p => Assert.Same(after, CableAt(r, p.X, p.Y).powernet));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MergePowernetsKeepsTheLargerNetwork()
    {
        var (r, _) = Room();
        r.In(() =>
        {
            datum_powernet line = CableAt(r, 2, LineY).powernet, shortLine = CableAt(r, 2, ShortY).powernet;
            Assert.Same(line, merge_powernets(shortLine, line)); // power.dm:426: net2 is larger, they switch
            Assert.True(QDELETED(shortLine));
            Assert.Equal(8f, line.cables.len);
            Assert.All(Short, p => Assert.Same(line, CableAt(r, p.X, p.Y).powernet));
            Assert.Null(merge_powernets(line, line));
            Assert.Null(merge_powernets(line, null));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePowerAccountingRunsOverSSmachinesCycles()
    {
        obj_machinery_power_terminal term = null;
        var (r, _) = Room(extras: _ => term = Dm.@new<obj_machinery_power_terminal>(locate(6, LineY, 1)));
        datum_powernet net = Net(r, 2, LineY);
        r.In(() =>
        {
            // propagate_network (power.dm:412) connected the anchored terminal on the cable node it lies on
            Assert.Same(net, term.powernet);
            Assert.Same(term, net.nodes[term]);
            Assert.Same(CableAt(r, 6, LineY), (obj_structure_cable)get_turf(term).get_cable_node(CABLE_LAYER_2));
            Assert.Equal(0f, Dm.num(term.surplus()));

            // what a generator adds is avail only after the next reset (powernet.dm:92)
            Assert.True(Dm.truthy(term.add_avail(5000)));
            Assert.Equal(5000f, net.newavail);
            Assert.Equal(0f, net.avail);
        });
        MachinesTick(r);
        r.In(() =>
        {
            Assert.Equal(5000f, net.avail);
            Assert.Equal(0f, net.newavail);
            Assert.Equal(5000f, Dm.num(term.avail()));
            Assert.True(Dm.truthy(term.avail(4000)));
            Assert.False(Dm.truthy(term.avail(6000)));

            // loads come off the surplus at once, a delayed load at the next reset
            term.add_load(2000);
            Assert.Equal(3000f, Dm.num(term.surplus()));
            Assert.Equal(3000f, Dm.num(CableAt(r, 3, LineY).surplus())); // a cable reads its network the same way
            term.add_delayedload(1500);
            term.add_avail(1000);
            Assert.Equal(0f, Dm.num(term.delayed_surplus())); // clamp(1000 - 1500, 0, 1000)
            Assert.Equal(1000f, Dm.num(term.newavail()));
            CableAt(r, 2, LineY).add_avail(1000);
            Assert.Equal(2000f, net.newavail);
            Assert.Equal(500f, Dm.num(term.delayed_surplus()));
        });
        MachinesTick(r);
        r.In(() =>
        {
            // reset: netexcess is what was left last cycle, the delayed load becomes the load, the new power the power
            Assert.Equal(3000f, net.netexcess);
            Assert.Equal(1500f, Dm.num(net.load));
            Assert.Equal(0f, net.delayedload);
            Assert.Equal(2000f, net.avail);
            Assert.Equal(500f, Dm.num(term.surplus()));
        });
        MachinesTick(r);
        r.In(() =>
        {
            Assert.Equal(500f, net.netexcess);
            Assert.Equal(0f, Dm.num(net.load));
            Assert.Equal(0f, net.avail);
            Assert.Equal(0f, Dm.num(term.surplus()));
            // a machine off the network has no power to read
            Assert.True(Dm.truthy(term.disconnect_from_network()));
            Assert.Null(term.powernet);
            Assert.False(Dm.truthy(net.nodes[term]));
            Assert.False(Dm.truthy(term.add_avail(10)));
            Assert.Equal(0f, Dm.num(term.surplus()));
            Assert.True(Dm.truthy(term.connect_to_network()));
            Assert.Same(net, term.powernet);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADestroyedGrilleUpdatesTheCableIconsOnItsTurf()
    {
        var (r, _) = Room(extras: _ => Dm.@new<obj_structure_grille>(locate(4, LineY, 1)));
        r.In(() =>
        {
            obj_structure_cable under = CableAt(r, 4, LineY);
            Assert.Equal("l2-4-8-node", under.icon_state); // update_icon_state (cable.dm:186): a grille makes a node
            under.icon_state = "stale";
            qdel(Dm.locate<obj_structure_grille>(locate(4, LineY, 1)));
            // the grille's Destroy (grille.dm:37) runs update_cable_icons_on_turf before its ..(), while the grille is still on
            // the turf, so the cable's icon is computed again with the grille's node (tg's order)
            Assert.Equal("l2-4-8-node", under.icon_state);
            Assert.Null(Dm.locate<obj_structure_grille>(locate(4, LineY, 1)));
        });
        r.AssertNoRuntimes();
    }
}
