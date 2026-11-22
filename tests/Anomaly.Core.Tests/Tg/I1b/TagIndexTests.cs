using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Tg.I1b;

/// <summary>
/// M3 I1b (the stalls): locate(tag) through the registry's tag index gives what a scan of the live objects in id order
/// gives (BYOND ref /datum/var/tag). A mob's REF is its tag, so every weakref of a mob (the user of a callback, a door's
/// timer after a player opened it) resolved by a scan of every live object: seconds the first time on Meta.
/// </summary>
public class TagIndexTests
{
    [Fact]
    public void LocateOfATagFollowsRetagsDeletionsAndTheLowestIdWithoutAScan()
    {
        var room = TgToyRoom.Create(5, 5);
        using (room.World.Enter())
        {
            Assert.False(room.World.Objects.TagIndexIncomplete); // tg's objects all keep the index
            var crowd = new List<datum>();
            for (int i = 0; i < 2000; i++) crowd.Add(Dm.@new<datum>());
            // The tags are not mob_N: since M4b audio slice 6 the room initializes SSspatial_grid, whose 2500 oranges ears are
            // mobs with tg's generated mob_N tags.
            datum first = Dm.@new<datum>();
            datum second = Dm.@new<datum>();
            second.tag = "tagged_1";
            first.tag = "tagged_1"; // the same tag on two objects: the lower id wins, as the scan in id order gives
            Assert.Same(first, locate_ref("tagged_1"));
            first.tag = "tagged_2";
            Assert.Same(second, locate_ref("tagged_1"));
            Assert.Same(first, locate_ref("[tagged_2]"));
            Assert.Null(locate_ref("Tagged_2"));
            second.tag = null;
            Assert.Null(locate_ref("tagged_1"));
            room.World.Objects.Unregister(first.dm_id); // a hard delete
            Assert.Null(locate_ref("tagged_2"));
            Assert.False(room.World.Objects.TagIndexIncomplete);
            GC.KeepAlive(crowd);
        }
    }

    [Fact]
    public void AWeakrefOfAMobResolvesThroughTheIndex()
    {
        var room = TgToyRoom.Create(5, 5);
        using (room.World.Enter())
        {
            var mob = Dm.@new<mob_living_carbon_human>(Builtins.locate(2, 2, 1));
            Assert.False(string.IsNullOrEmpty(mob.tag)); // tg: mobs are tagged "mob_[id]" (DF_USE_TAG)
            datum_weakref weak = GlobalProcs.WEAKREF(mob) as datum_weakref;
            Assert.Same(mob, weak.resolve());
        }
    }
}
