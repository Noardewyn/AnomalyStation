namespace Anomaly.Core.DmShim;

// Owner: M2 P3 (the overlay snapshot engine part, Appearance.cs): the per-world interning of appearances.

public sealed partial class DmWorldState
{
    // The world's appearances, interned (BYOND keeps one appearance per distinct look). Snapshots are never removed while
    // the world lives: the set of looks is small and bounded in practice (icon states, damage levels, colours), and a
    // table that never forgets keeps a snapshot's identity independent of the GC. Not state for the hash: it only gives
    // equal appearances one identity.
    private Dictionary<DmAppearance, DmAppearance> _appearances;

    /// <summary>The interned snapshot equal to <paramref name="made"/> (it itself when it is the first of its look).</summary>
    internal DmAppearance InternAppearance(DmAppearance made)
    {
        _appearances ??= new Dictionary<DmAppearance, DmAppearance>(DmAppearanceComparer.Instance);
        if (_appearances.TryGetValue(made, out DmAppearance existing)) return existing;
        _appearances.Add(made, made);
        return made;
    }

    /// <summary>How many distinct appearances the world has interned (tests and measurements).</summary>
    internal int InternedAppearanceCount => _appearances?.Count ?? 0;
}
