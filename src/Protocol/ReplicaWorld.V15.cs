namespace Anomaly.Protocol;

// Version 15 (MessagesV15.cs, protocol.md section 24, DV40): the radial menus of this session. The replica keeps the open menus
// (their latest page) in the order they opened or last changed, checks each message against the tables, and raises RadialChanged.
// A MapGrid (the start of every join) forgets them; the server sends the open ones again after the join.

public sealed partial class ReplicaWorld
{
    private readonly List<RadialMenuMessage> _radials = new();
    private Action<RadialMenuMessage> _radialChangedHandlers;

    /// <summary>The open radial menus, oldest first (the client draws the last).</summary>
    public IReadOnlyList<RadialMenuMessage> Radials => _radials;

    /// <summary>The newest open radial menu, or null.</summary>
    public RadialMenuMessage TopRadial => _radials.Count > 0 ? _radials[^1] : null;

    /// <summary>Raised after each applied <see cref="RadialMenuMessage"/> (an open, a new page or a close).</summary>
    public event Action<RadialMenuMessage> RadialChanged
    {
        add => _radialChangedHandlers += value;
        remove => _radialChangedHandlers -= value;
    }

    /// <summary>
    /// Applies a <see cref="RadialMenuMessage"/>: throws <see cref="ProtocolException"/> (and changes nothing) for one before a Snapshot,
    /// an appearance outside its table, or a close of a menu that is not open.
    /// </summary>
    public void Apply(RadialMenuMessage radial)
    {
        ArgumentNullException.ThrowIfNull(radial);
        if (!_hasSnapshot)
            throw Bad("a RadialMenu message arrives before any Snapshot");
        int index = _radials.FindIndex(m => m.MenuId == radial.MenuId);
        if (radial.Kind == RadialMenuKind.Close)
        {
            if (index < 0)
                throw Bad("a radial menu " + radial.MenuId + " closes but is not open");
            _radials.RemoveAt(index);
            _radialChangedHandlers?.Invoke(radial);
            return;
        }
        CheckRadialRef(radial.Center, "its centre");
        CheckRadialRef(radial.CenterFocus, "its hovered centre");
        foreach (RadialSlice slice in radial.Elements)
        {
            CheckRadialRef(slice.Icon, "a slice's picture");
            CheckRadialRef(slice.Background, "a slice");
            CheckRadialRef(slice.BackgroundFocus, "a hovered slice");
        }
        if (index >= 0)
            _radials.RemoveAt(index);
        _radials.Add(radial);
        _radialChangedHandlers?.Invoke(radial);
    }

    private void CheckRadialRef(TableRef a, string what)
    {
        if (!a.IsNone && a.Index > (a.Private ? _privateAppearances.Count : _appearances.Count))
            throw Bad("a radial menu names an appearance outside its table (" + what + ")");
    }

    private void TakeMapCellsV15() => _radials.Clear();
}
