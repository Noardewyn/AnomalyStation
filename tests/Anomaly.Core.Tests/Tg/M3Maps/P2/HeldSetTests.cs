using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Xunit;

namespace Anomaly.Core.Tests.Tg.M3Maps.P2;

[DmPath("/obj/test_p2_held_thing")]
public class obj_test_p2_held_thing : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

[DmPath("/turf/test_p2_held_floor")]
public class turf_test_p2_held_floor : turf
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

[DmPath("/area/test_p2_held_room")]
public class area_test_p2_held_room : area
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

[DmPath("/area/test_p2_held_other")]
public class area_test_p2_held_other : area
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>M3 P2: the area-membership index (Builtin/HeldSet.cs) against a plain ordered list, and the contents paths that use it.</summary>
public class HeldSetTests
{
    // Unregistered atoms (no world needed): HeldSet only compares references.
    private static atom[] Things(int n)
    {
        var things = new atom[n];
        for (int i = 0; i < n; i++) things[i] = new obj_test_p2_held_thing();
        return things;
    }

    private static void AssertSame(List<atom> expected, HeldSet set)
    {
        Assert.Equal(expected.Count, set.Count);
        int i = 0;
        foreach (atom thing in set)
        {
            Assert.Same(expected[i], thing);
            i++;
        }
        Assert.Equal(expected.Count, i);
        for (int k = 0; k < expected.Count; k++)
        {
            Assert.Same(expected[k], set[k]);
            Assert.Equal(k, set.IndexOf(expected[k]));
            Assert.True(set.Contains(expected[k]));
        }
    }

    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 40)]
    [InlineData(3, 300)]
    [InlineData(4, 3000)]
    public void Random_operations_match_an_ordered_list(int seed, int size)
    {
        var rng = new Random(seed);
        atom[] pool = Things(size);
        var expected = new List<atom>();
        var set = new HeldSet();
        for (int step = 0; step < size * 6; step++)
        {
            int op = rng.Next(10);
            atom thing = pool[rng.Next(size)];
            if (op < 5)
            {
                bool added = !expected.Contains(thing);
                if (added) expected.Add(thing);
                Assert.Equal(added, set.Add(thing));
            }
            else if (op < 9)
            {
                bool removed = expected.Remove(thing);
                Assert.Equal(removed, set.Remove(thing));
            }
            else if (expected.Count > 0)
            {
                atom old = expected[rng.Next(expected.Count)];
                atom replacement = new obj_test_p2_held_thing();
                expected[expected.IndexOf(old)] = replacement;
                set.Replace(old, replacement);
            }
            if (step % 7 == 0 || size <= 40) AssertSame(expected, set);
        }
        AssertSame(expected, set);
        foreach (atom thing in pool)
        {
            if (!expected.Contains(thing)) Assert.Equal(-1, set.IndexOf(thing));
        }
        set.Clear();
        Assert.Empty(set);
    }

    [Fact]
    public void Removing_from_the_front_keeps_positions_exact()
    {
        atom[] pool = Things(5000);
        var set = new HeldSet();
        foreach (atom thing in pool) set.Add(thing);
        for (int i = 0; i < pool.Length - 1; i++)
        {
            Assert.True(set.Remove(pool[i]));
            Assert.Equal(0, set.IndexOf(pool[i + 1]));
            Assert.Equal(pool.Length - i - 2, set.IndexOf(pool[^1]));
        }
        Assert.Same(pool[^1], Assert.Single(set));
    }

    [Fact]
    public void Enumeration_detects_a_change()
    {
        atom[] pool = Things(3);
        var set = new HeldSet();
        set.Add(pool[0]);
        set.Add(pool[1]);
        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (atom thing in set) set.Add(pool[2]);
        });
    }

    [Fact]
    public void Replace_refuses_an_atom_already_held_or_a_missing_one()
    {
        atom[] pool = Things(3);
        var set = new HeldSet();
        set.Add(pool[0]);
        set.Add(pool[1]);
        Assert.Throws<InvalidOperationException>(() => set.Replace(pool[0], pool[1]));
        Assert.Throws<InvalidOperationException>(() => set.Replace(pool[2], pool[1]));
    }

    // A world of w x h floors in one room (world.area), with a second area.
    private static World MapWorld(int w, int h)
    {
        var world = new World(3);
        MapGrid.Attach(world);
        using (world.Enter())
        {
            Dm.world.turf = typeof(turf_test_p2_held_floor);
            Dm.world.area = typeof(area_test_p2_held_room);
            Dm.world.maxx = w;
            Dm.world.maxy = h;
            Dm.world.maxz = 1;
        }
        return world;
    }

    [Fact]
    public void Appending_turfs_through_contents_moves_them_in_order()
    {
        World world = MapWorld(20, 20);
        using (world.Enter())
        {
            var room = (area)Builtins.locate(1, 1, 1).loc;
            var other = Dm.@new<area_test_p2_held_other>();
            var moved = new List<atom>();
            for (int y = 20; y >= 1; y--)
            {
                for (int x = 1; x <= 20; x += 3)
                {
                    turf t = Builtins.locate(x, y, 1);
                    Dm.append(other.contents, t);
                    moved.Add(t);
                    Assert.Same(other, t.loc);
                }
            }
            Assert.Equal(moved.Count, other.contents.Count);
            for (int i = 0; i < moved.Count; i++) Assert.Same(moved[i], other.contents[i + 1]);
            Assert.Equal(400 - moved.Count, room.contents.Count);
            // the room keeps the grid order of the turfs that stayed
            int k = 1;
            for (int y = 1; y <= 20; y++)
            {
                for (int x = 1; x <= 20; x++)
                {
                    if ((x - 1) % 3 == 0) continue;
                    Assert.Same(Builtins.locate(x, y, 1), room.contents[k++]);
                }
            }
            // two appends between engine accesses, then an insertion among the held atoms: the general path
            turf a = Builtins.locate(2, 1, 1);
            turf b = Builtins.locate(3, 1, 1);
            Dm.append(other.contents, a);
            Dm.append(other.contents, b);
            other.contents.Insert(1, Builtins.locate(5, 1, 1));
            Assert.Same(other, b.loc);
            Assert.Same(other, a.loc);
            Assert.Same(Builtins.locate(5, 1, 1), other.contents[1]);
            Assert.Same(b, other.contents[other.contents.Count]);
        }
    }

    [Fact]
    public void A_turf_replacement_keeps_its_place_in_the_area()
    {
        World world = MapWorld(10, 10);
        using (world.Enter())
        {
            var room = (area)Builtins.locate(1, 1, 1).loc;
            turf before = Builtins.locate(4, 2, 1);
            int position = (int)room.contents.Find(before);
            Dm.append(Dm.@new<area_test_p2_held_other>().contents, Builtins.locate(1, 1, 1)); // a hole in front of it
            turf after = Dm.@new<turf_test_p2_held_floor>(Builtins.locate(4, 2, 1));
            Assert.Same(after, Builtins.locate(4, 2, 1));
            Assert.Equal(position - 1, (int)room.contents.Find(after));
            Assert.Equal(0f, room.contents.Find(before));
        }
    }
}
