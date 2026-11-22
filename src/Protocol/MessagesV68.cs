namespace Anomaly.Protocol;

// Protocol version 68 (D315, docs/ai/specs/protocol.md section 69; the codec: Codec.V68.cs).
// - MinimapMobsMessage (server to client, State, after the tick's Delta): the dev trackers of the minimap, the living mobs of the session's
//   level twice a second while its dev command minimapmobs is on.

/// <summary>Version 68: a living mob on the minimap: its tile, whether a player has it and its name (tg's hover text of a blip).</summary>
public readonly record struct MinimapMob(int X, int Y, bool Player, string Name);

/// <summary>Version 68: the living mobs of level <see cref="Z"/> but the session's own, twice a second while the dev trackers are on.</summary>
public sealed class MinimapMobsMessage
{
    /// <summary>The level, 1 to <see cref="Limits.MaxMapLevels"/>.</summary>
    public int Z { get; set; }

    /// <summary>At most <see cref="Limits.MaxMinimapMobs"/>.</summary>
    public List<MinimapMob> Mobs { get; } = new();
}
