using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// The world's order of atoms (dm-conformance.md, object-model.md section 5): <c>for (x in world)</c>, <c>world.contents</c>
/// and the loop without <c>in</c> give mobs, then the other movables, then areas, then turfs, each group in id order, as
/// OpenDream's <c>AtomManager.EnumerateAtoms</c> does. tg's <c>SSatoms.InitializeAtoms</c> relies on it: objects are
/// initialized before the turf whose <c>levelupdate</c> hides the undertile ones (M3 P4).
/// </summary>
public class WorldOrderTests
{
    // GLOB's gas overlays (/obj/effect/overlay/gas, generate_gas_overlays, M4b slice 3) are movables in nullspace, made before
    // the test's own: the world gives them too, as BYOND does; the order checks are about the test's atoms.
    private static List<object> Seen(IEnumerable<object> source) => source.Where(x => x is not obj_effect_overlay_gas).ToList();

    private static int Group(object atom) => ((IDmAtom)atom).dm_world_order;

    [Fact]
    public void TheWorldGivesMobsThenMovablesThenAreasThenTurfsEachInIdOrder()
    {
        var t = new TurfAreaWorld(3, 2, 1);
        using (t.World.Enter())
        {
            // M5 slice 7: the GLOB init's stock part singletons each make their physical part in nullspace
            // (stock_part_datum.dm:24, physical_object_reference); the world lists them too, so they are left out here.
            var nullspace = new HashSet<object>(Dm.each(Dm.world).Cast<object>().Where(x => x is atom_movable { loc: null }));
            // Created after the grid's turfs and area: in id order they would come last.
            var thing = Dm.@new<obj>(locate(1, 1, 1));
            var person = Dm.@new<mob>(locate(2, 1, 1));
            var other = Dm.@new<obj>(locate(3, 2, 1));

            List<object> loop = Seen(Dm.each(Dm.world).Cast<object>().Where(x => !nullspace.Contains(x)));
            List<object> contents = Seen(Dm.each(Dm.world.contents).Cast<object>().Where(x => !nullspace.Contains(x)));
            List<object> without = Seen(Dm.each<atom>().Cast<object>().Where(x => !nullspace.Contains(x)));

            foreach (List<object> order in new[] { loop, contents, without })
            {
                Assert.Same(person, order[0]);
                Assert.Same(thing, order[1]);
                Assert.Same(other, order[2]);
                Assert.IsAssignableFrom<area>(order[3]);
                Assert.All(order.Skip(4), x => Assert.IsAssignableFrom<turf>(x));
                Assert.Equal(order.Select(Group).OrderBy(g => g), order.Select(Group));
                // Within a group, id order.
                foreach (IGrouping<int, object> group in order.GroupBy(Group))
                {
                    List<long> ids = group.Select(x => ((IDmObject)x).dm_id).ToList();
                    Assert.Equal(ids.OrderBy(id => id), ids);
                }
            }
            Assert.Equal(loop, contents);
            Assert.Equal(loop, without);
        }
    }

    [Fact]
    public void AnAtomCreatedDuringTheLoopIsMetInALaterGroup()
    {
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            Dm.@new<obj>(locate(1, 1, 1));
            mob made = null;
            var seen = new List<object>();
            foreach (object x in Dm.each(Dm.world))
            {
                seen.Add(x);
                if (made == null && x is area)
                    made = Dm.@new<mob>(locate(2, 1, 1)); // the mob group is over: not met
            }
            Assert.NotNull(made);
            Assert.DoesNotContain(made, seen);
            Assert.Contains(made, Dm.each(Dm.world.contents).Cast<object>());
        }
    }
}
