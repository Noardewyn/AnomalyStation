using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// The dev menu's player markers (the owner, 2026-11-04): a mark over every player's mob of the own level in the world, from D315's
/// trackers (<see cref="MinimapMobsMessage"/>, the server's minimapmobs): the players only, each with its name and its distance in tiles.
/// </summary>
public static class PlayerMarkerList
{
    /// <summary>A player's mark: a key that stays with the mob between messages (its name and which of that name it is), its tile and text.</summary>
    public readonly record struct Mark(string Key, int X, int Y, string Text);

    public static List<Mark> Of(MinimapMobsMessage mobs, float ownX, float ownY)
    {
        var marks = new List<Mark>();
        var seen = new Dictionary<string, int>();
        foreach (MinimapMob mob in mobs?.Mobs ?? new List<MinimapMob>())
        {
            if (!mob.Player)
                continue;
            string name = string.IsNullOrEmpty(mob.Name) ? "a player" : mob.Name;
            seen[name] = seen.TryGetValue(name, out int n) ? n + 1 : 0;
            int tiles = (int)MathF.Round(MathF.Sqrt((mob.X - ownX) * (mob.X - ownX) + (mob.Y - ownY) * (mob.Y - ownY)));
            marks.Add(new Mark(name + "#" + seen[name], mob.X, mob.Y, name + "\n" + tiles + (tiles == 1 ? " tile" : " tiles")));
        }
        return marks;
    }
}
