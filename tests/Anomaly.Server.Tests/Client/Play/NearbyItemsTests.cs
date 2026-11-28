using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 12, slice 4C-2: what the Nearby strip lists from the replicated world: items only (tg's /obj/item type), lying on the player's
/// tile and the eight round it on the same level, nearest first (the own tile, the straight neighbours, the diagonal ones, by entity within a tile).
/// </summary>
public class NearbyItemsTests
{
    private static NearbyThing T(long id, string type, int x, int y, int z = 1, bool visual = false)
        => new(id, type, x, y, z, "thing " + id, new TableRef((uint)id, true), visual);

    [Fact]
    public void TheItemsOnTheOwnTileAndTheEightRoundItAreListedNearestFirst()
    {
        // the player at (10, 10, 1); one item on each of the nine tiles, given in a scrambled order, two on the own tile and two to the north
        var things = new[]
        {
            T(19, "/obj/item/wrench", 9, 9), T(12, "/obj/item/pen", 10, 11), T(17, "/obj/item/crowbar", 11, 9), T(11, "/obj/item/screwdriver", 10, 10),
            T(15, "/obj/item/stack/sheet/iron", 9, 10), T(16, "/obj/item/multitool", 11, 11), T(14, "/obj/item/wirecutters", 10, 9),
            T(13, "/obj/item/toy/crayon/red", 11, 10), T(18, "/obj/item/flashlight", 9, 11), T(10, "/obj/item/pen", 10, 10), T(20, "/obj/item/pen", 10, 11),
        };
        List<StorageItem> list = NearbyItems.List(things, 10, 10, 1);
        // the own tile (10, 11 by entity), north (12, 20), east (13), south (14), west (15), north-east (16), south-east (17), south-west (19), north-west (18)
        Assert.Equal(new long[] { 10, 11, 12, 20, 13, 14, 15, 16, 17, 19, 18 }, list.Select(i => i.Entity));
        // each with its name and picture from the replica, no count (a stack's amount is its picture's maptext)
        Assert.Equal(new StorageItem(13, "thing 13", new TableRef(13, true), 0), list[4]);
        // the same set in another order lists the same
        Assert.Equal(list, NearbyItems.List(things.Reverse(), 10, 10, 1));
    }

    [Fact]
    public void OnlyItemsAreListedNeverMobsStructuresMachinesWallMountsVisualsOrThingsFurtherAwayOrOnAnotherLevel()
    {
        var things = new[]
        {
            T(1, "/obj/item/pen", 10, 10),
            T(2, "/mob/living/carbon/human", 10, 10), // a mob, the player's own included
            T(3, "/obj/structure/closet", 11, 10), // a structure (what is inside it is not replicated: only things on a turf are)
            T(4, "/obj/machinery/vending/cola", 9, 10), // a machine
            T(5, "/obj/structure/table", 10, 11),
            T(6, "/obj/item/pen", 10, 10, visual: true), // a short-lived visual drawn on something
            T(7, "/obj/item/pen", 12, 10), // two tiles away
            T(8, "/obj/item/pen", 10, 8),
            T(9, "/obj/item/pen", 10, 10, z: 2), // on another level
            T(10, "/obj/itemization", 10, 10), // not tg's item type (a whole path segment)
            T(11, "/obj/effect/decal/cleanable/blood", 10, 10),
            T(12, null, 10, 10), // a type the client does not know
            T(13, "/obj/item", 11, 11), // tg's item type itself
            T(14, "/obj/item/radio/intercom", 11, 10), // a wall mount of tg's item type on the tile in front of its wall: drawn on the wall
            T(15, "/obj/item/radio/intercom/prison", 10, 9),
        };
        Assert.Equal(new long[] { 1, 13 }, NearbyItems.List(things, 10, 10, 1).Select(i => i.Entity));
        Assert.Empty(NearbyItems.List(null, 10, 10, 1));
        Assert.Empty(NearbyItems.List(Array.Empty<NearbyThing>(), 10, 10, 1));
    }

    [Fact]
    public void AtMostTheStripsCellsAreListedTheNearestKept()
    {
        // a pile of 100 items on the east tile and one on the own tile: the own tile's first, then the pile's lowest entities
        IEnumerable<NearbyThing> pile = Enumerable.Range(0, 100).Select(i => T(1000 + i, "/obj/item/stack/rods", 11, 10)).Append(T(5000, "/obj/item/pen", 10, 10));
        List<StorageItem> list = NearbyItems.List(pile, 10, 10, 1);
        Assert.Equal(NearbyItems.MaxItems, list.Count);
        Assert.Equal(5000, list[0].Entity);
        Assert.Equal(Enumerable.Range(1000, NearbyItems.MaxItems - 1).Select(i => (long)i), list.Skip(1).Select(i => i.Entity));
    }

    [Fact]
    public void WithoutAnOwnMobOnATurfNothingIsListed()
    {
        Assert.Empty(NearbyItems.Of(null));
        Assert.Empty(NearbyItems.Of(new ReplicaWorld()));
    }
}
