using System.Linq;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// ui-overhaul.md section 12, slice 4C-2: what the Tab inventory's Nearby strip lists. Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). Client only, from the world the client already has: the server replicates only the movables lying directly on a
// turf (ReplicatedFields.EntityRule), so a replicated entity of tg's item type (/obj/item and its subtypes) is never one in a container,
// a hand or a slot; it lies on the floor unless it is a wall mount (an intercom: tg's maps put it on the tile in front of its wall and the view draws
// it on the wall, PropKinds.IsWallMountType), which is not listed; mobs, structures and machines are other types. The client decides no reach: tg does when the item is acted on.
// DEVIATION(DV694): tg has no such list (its items are clicked where they lie); an item behind a window may be listed and a click on it does nothing.

/// <summary>One replicated thing as the Nearby strip looks at it: its entity, its DM type, its tile, its name and picture, and whether it is a short-lived visual.</summary>
public readonly record struct NearbyThing(long Entity, string TypePath, int X, int Y, int Z, string Name, TableRef Appearance, bool Visual = false);

/// <summary>The items lying round the player, for the Nearby strip (ui-overhaul.md section 12, slice 4C-2).</summary>
public static class NearbyItems
{
    /// <summary>The most items the strip lists (the nearest ones).</summary>
    public const int MaxItems = UiTokens.StorageMaxCells;

    /// <summary>
    /// The items among <paramref name="things"/> lying on the tile (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) and the eight round
    /// it, nearest first: that tile, then the four straight neighbours (north, east, south, west), then the four diagonal ones (north-east, south-east,
    /// south-west, north-west), by entity within a tile; at most <see cref="MaxItems"/>. Only things of tg's item type that are no wall mount, no visuals.
    /// </summary>
    public static List<StorageItem> List(IEnumerable<NearbyThing> things, int x, int y, int z)
    {
        var found = new List<(int Rank, NearbyThing Thing)>();
        foreach (NearbyThing t in things ?? Array.Empty<NearbyThing>())
        {
            int dx = t.X - x, dy = t.Y - y;
            if (t.Z == z && Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && !t.Visual && t.TypePath != null && PropKinds.IsItem(t.TypePath)
                && !PropKinds.IsWallMountType(t.TypePath)) // an intercom of tg's item type stands on the tile in front of its wall, drawn on the wall
                found.Add((Rank(dx, dy), t));
        }
        found.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Thing.Entity.CompareTo(b.Thing.Entity));
        var items = new List<StorageItem>(Math.Min(found.Count, MaxItems));
        for (int i = 0; i < found.Count && i < MaxItems; i++)
            items.Add(new StorageItem(found[i].Thing.Entity, found[i].Thing.Name, found[i].Thing.Appearance, 0));
        return items;
    }

    // the own tile; north, east, south, west; north-east, south-east, south-west, north-west (tg's y grows to the north)
    private static int Rank(int dx, int dy) => (dx, dy) switch
    {
        (0, 0) => 0,
        (0, 1) => 1,
        (1, 0) => 2,
        (0, -1) => 3,
        (-1, 0) => 4,
        (1, 1) => 5,
        (1, -1) => 6,
        (-1, -1) => 7,
        _ => 8,
    };

    /// <summary>The items lying round the own mob in <paramref name="replica"/> (<see cref="List"/>); none without an own mob on a turf.</summary>
    public static List<StorageItem> Of(ReplicaWorld replica)
    {
        if (replica == null || !replica.TryGet(replica.OwnEntity, out ReplicaEntity me))
            return new List<StorageItem>();
        EntityState own = me.State;
        return List(replica.Entities.Select(e =>
        {
            EntityState s = e.State;
            return new NearbyThing(e.Id, e.TypePath, s.X, s.Y, s.Z, s.Name, s.Appearance, s.IsVisual);
        }), own.X, own.Y, own.Z);
    }
}
