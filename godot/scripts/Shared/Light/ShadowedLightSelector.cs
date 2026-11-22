namespace Anomaly.Client.Shared.Light;

// Owner: M3 P7. Engine-free. docs/ai/specs/light-grid.md section 5.

/// <summary>A light source a shadowed Godot light could stand in for.</summary>
public readonly record struct ShadowCandidate(long Key, int X, int Y, float Range, float Power);

/// <summary>
/// Picks the sources that get a real shadowed Godot light (D114): the nearest live ones with a line of sight to the camera,
/// at most the preset's budget. Pure and allocation-light so it can run several times a second.
/// </summary>
public static class ShadowedLightSelector
{
    /// <summary>
    /// D288: which lights take the shadows first (the owner, 2026-10-29), lower first: 0 the effects (tg's fire, sparks, flashes and
    /// projectiles: what lights an explosion), 1 the lights on bodies (what a mob carries or wears), 2 the lamps and the machines.
    /// </summary>
    public static int Priority(string typePath, bool onBody) =>
        typePath != null && (typePath.StartsWith("/obj/effect", StringComparison.Ordinal) || typePath.StartsWith("/obj/projectile", StringComparison.Ordinal))
            ? 0
            : onBody ? 1 : 2;

    /// <summary>
    /// D302: the group of burning tiles a hotspot at this tile lights with: one lamp for each three by three tiles of the map.
    /// shortcut: a group ignores walls, so a fire across a wall may light only its own side's neighbours; split groups by room if a play test shows it.
    /// </summary>
    public static long FireGroup(int x, int y) => ((long)(x / 3) << 32) | (uint)(y / 3);

    /// <summary>
    /// The best <paramref name="budget"/> candidates by distance to the camera tile (<paramref name="camX"/>, <paramref name="camY"/>)
    /// that are within their own reach (<c>Range + 2</c> tiles) and whose tile has a line of sight to the camera over
    /// <paramref name="cells"/> (<see cref="HasLineOfSight"/>). Fills <paramref name="chosen"/> (cleared first) nearest first.
    /// </summary>
    public static void Select(ILightCells cells, int z, float camX, float camY, IEnumerable<ShadowCandidate> candidates, int budget, List<ShadowCandidate> chosen, List<(float Distance, ShadowCandidate Candidate)> scratch)
    {
        chosen.Clear();
        scratch.Clear();
        if (budget <= 0)
            return;
        foreach (ShadowCandidate c in candidates)
        {
            float dx = c.X - camX, dy = c.Y - camY;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > c.Range + 2f)
                continue;
            scratch.Add((d, c));
        }

        scratch.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Candidate.Key.CompareTo(b.Candidate.Key));
        foreach ((float _, ShadowCandidate c) in scratch)
        {
            if (!HasLineOfSight(cells, z, camX, camY, c.X, c.Y))
                continue;
            chosen.Add(c);
            if (chosen.Count >= budget)
                break;
        }
    }

    /// <summary>
    /// Whether the straight line from (<paramref name="x0"/>, <paramref name="y0"/>) to the tile (<paramref name="x1"/>,
    /// <paramref name="y1"/>) crosses no opaque tile other than the end tile (a lamp on a wall still sees the room). Sampled
    /// at half-tile steps over tile coordinates (tile centres at integers).
    /// </summary>
    public static bool HasLineOfSight(ILightCells cells, int z, float x0, float y0, int x1, int y1)
    {
        float dx = x1 - x0, dy = y1 - y0;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        int steps = (int)MathF.Ceiling(len * 2f);
        int endX = x1, endY = y1;
        for (int s = 0; s < steps; s++)
        {
            float t = s / (float)steps;
            int tx = (int)MathF.Round(x0 + dx * t);
            int ty = (int)MathF.Round(y0 + dy * t);
            if (tx == endX && ty == endY)
                return true;
            if (tx >= 1 && ty >= 1 && tx <= cells.MaxX && ty <= cells.MaxY && cells.IsOpaque(tx, ty, z))
                return false;
        }

        return true;
    }
}
