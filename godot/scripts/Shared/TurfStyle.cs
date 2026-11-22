namespace Anomaly.Client.Shared;

/// <summary>What the client draws for a turf type.</summary>
public enum TurfKind : byte
{
    /// <summary>A floor tile.</summary>
    Open,

    /// <summary>A wall: a 2 x 3 x 2 m box (D33).</summary>
    Closed,

    /// <summary>Nothing: open space.</summary>
    Void,
}

/// <summary>A colour as three floats (0 to 1), so that this logic needs no engine type.</summary>
public readonly record struct Rgb(float R, float G, float B);

/// <summary>
/// The M1 placeholder look of a turf by its DM type path (the map travels as type paths, <c>MapGrid.TurfTypes</c>): a
/// <c>/turf/closed</c> is a wall, a <c>/turf/open/space</c> is nothing, any other turf is a floor. Colours come from a
/// stable hash of the path, so two types look different and a type always looks the same. The real models and materials
/// come with the map in M3.
/// </summary>
public static class TurfStyle
{
    /// <summary>The kind of the turf type <paramref name="path"/> (null or empty is a floor: tg's plain <c>/turf</c>).</summary>
    public static TurfKind Classify(string path)
    {
        if (string.IsNullOrEmpty(path))
            return TurfKind.Open;
        if (path.StartsWith("/turf/closed", StringComparison.Ordinal))
            return TurfKind.Closed;
        if (path.StartsWith("/turf/open/space", StringComparison.Ordinal))
            return TurfKind.Void;
        if (path.StartsWith("/turf/open/openspace", StringComparison.Ordinal))
            return TurfKind.Void; // D325: tg draws no open space (its picture is a placeholder): the level below shows through it
        if (path.StartsWith("/turf/cordon", StringComparison.Ordinal))
            return TurfKind.Void; // tg's INVISIBILITY_ABSTRACT world border around reservations: nobody sees it
        return TurfKind.Open;
    }

    /// <summary>
    /// Whether a floor of this type is polished: it shows a faint reflection of the room where the graphics have reflections on (the owner,
    /// 2026-11-13). The white, dark and tiled-room floors of tg's iron family, its mineral floors and the circuit and bluespace floors;
    /// the plain grey iron, plating, carpet and wood are matt.
    /// </summary>
    public static bool Polished(string path)
    {
        const string floor = "/turf/open/floor/";
        if (path == null || !path.StartsWith(floor, StringComparison.Ordinal))
            return false;
        string rest = path.Substring(floor.Length);
        if (rest.StartsWith("mineral", StringComparison.Ordinal) || rest.StartsWith("circuit", StringComparison.Ordinal) || rest.StartsWith("bluespace", StringComparison.Ordinal))
            return true;
        if (!rest.StartsWith("iron/", StringComparison.Ordinal))
            return false;
        string kind = rest.Substring(5);
        int end = kind.IndexOf('/');
        return (end < 0 ? kind : kind.Substring(0, end)) is "white" or "dark" or "showroomfloor" or "freezer" or "kitchen" or "cafeteria" or "checker";
    }

    /// <summary>Whether a floor of this type is tg's glass floor (plain, reinforced, plasma, titanium...): drawn see-through, as glass.</summary>
    public static bool GlassFloor(string path) => path != null && path.StartsWith("/turf/open/floor/glass", StringComparison.Ordinal);

    /// <summary>D325: whether a turf of this kind on the level above a cell closes that cell's top (a floor or a wall; not open space or space).</summary>
    public static bool ShowsUnderside(TurfKind above) => above != TurfKind.Void;

    /// <summary>
    /// Whether a turf of the level above shows plating to the cell under it: a floor tile lies on plating and a wall stands on it. Open space has
    /// none; and a glass floor has none either, it is its own underside, the same pane seen through from both sides (the owner, 2026-11-17: its
    /// underside was drawn with the plating's picture, so from under it glass came with the plating it had replaced).
    /// </summary>
    public static bool PlatingUnder(string above) => Classify(above) != TurfKind.Void && !GlassFloor(above);

    /// <summary>The colour of a wall or floor of this type (the floor's checker tint is the renderer's).</summary>
    public static Rgb Tint(string path)
    {
        float hue = Hash01(path ?? string.Empty);
        TurfKind kind = Classify(path);
        // A small shift around a base colour: walls blue-grey, floors neutral grey.
        float shift = (hue - 0.5f) * 0.12f;
        return kind == TurfKind.Closed
            ? new Rgb(0.46f + shift, 0.50f, 0.58f - shift)
            : new Rgb(0.46f + shift, 0.47f, 0.50f - shift);
    }

    /// <summary>FNV-1a of the path folded to [0, 1): stable across runs and machines (string.GetHashCode is not).</summary>
    private static float Hash01(string text)
    {
        uint hash = 2166136261;
        foreach (char c in text)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return (hash & 0xFFFF) / 65536f;
    }
}
