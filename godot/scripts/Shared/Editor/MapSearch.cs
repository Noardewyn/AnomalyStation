using System.Linq;
using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>A member of a tile that a search found.</summary>
public readonly record struct MapFound(int X, int Y, int Z, int Member);

/// <summary>The map editor's search (docs/ai/specs/map-editor.md 5.5): the members of a map's tiles that hold every word asked for.</summary>
public static class MapSearch
{
    /// <summary>
    /// The members whose text (the type's path with the vars the map sets, as the file writes them) or whose type's name holds every word
    /// of the query, whatever the case; in the order of the levels, then of the rows from the south, then from the west.
    /// </summary>
    /// <param name="nameOf">A type's name by its path (the palette's), or null to search the texts alone.</param>
    /// <param name="limit">The most members that are listed.</param>
    /// <param name="total">How many members hold the words, listed or not.</param>
    public static List<MapFound> Find(MapDocument map, string query, Func<string, string> nameOf, int limit, out int total)
    {
        ArgumentNullException.ThrowIfNull(map);
        var found = new List<MapFound>();
        total = 0;
        string[] words = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return found;
        // tiles with the same members share a model: each is read once
        var holders = new Dictionary<MapModel, int[]>(ReferenceEqualityComparer.Instance);
        for (int z = 1; z <= map.Levels; z++)
        {
            for (int y = 1; y <= map.Height; y++)
            {
                for (int x = 1; x <= map.Width; x++)
                {
                    MapModel tile = map[x, y, z];
                    if (!holders.TryGetValue(tile, out int[] members))
                        holders[tile] = members = Enumerable.Range(0, tile.Members.Count).Where(i => Holds(tile.Members[i], words, nameOf)).ToArray();
                    foreach (int member in members)
                    {
                        total++;
                        if (found.Count < limit)
                            found.Add(new MapFound(x, y, z, member));
                    }
                }
            }
        }
        return found;
    }

    private static bool Holds(MapMember member, string[] words, Func<string, string> nameOf)
    {
        string text = MapDocument.TextOf(new[] { member });
        string name = nameOf?.Invoke(member.Path);
        return words.All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase) || name?.Contains(word, StringComparison.OrdinalIgnoreCase) == true);
    }
}
