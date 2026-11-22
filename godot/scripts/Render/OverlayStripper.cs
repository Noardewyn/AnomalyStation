using System.Linq;
using System.Threading;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Render;

/// <summary>
/// The appearances of things whose pictures leave some overlays out because those are drawn another way (a C-4 charge stuck to a wall, a door, a table:
/// a model at its attach point, docs/ai/specs/models.md): <see cref="Strip"/> names a copy of an appearance without them, which this source resolves (everything
/// else is the inner source's). The copy has a reference of its own, so the compositor's and the atlas's caches keep the two apart (an item or a mob that
/// carries the same overlay keeps it in its picture).
/// </summary>
public sealed class OverlayStripper : IAppearanceSource
{
    private const uint Base = 0xF0000000u;
    private static int _count; // static: the caches outlive a map's renderer

    private readonly IAppearanceSource _inner;
    private readonly Func<TableRef, bool> _hidden;
    private readonly Dictionary<TableRef, TableRef> _stripped = new();
    private readonly Dictionary<TableRef, AppearanceEntry> _entries = new();
    private readonly HashSet<TableRef> _plain = new();

    public OverlayStripper(IAppearanceSource inner, Func<TableRef, bool> hidden)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _hidden = hidden ?? throw new ArgumentNullException(nameof(hidden));
    }

    /// <summary>The appearance without its hidden overlays: itself when it has none.</summary>
    public TableRef Strip(TableRef appearance)
    {
        if (appearance.IsNone || _entries.ContainsKey(appearance))
            return appearance;
        if (_stripped.TryGetValue(appearance, out TableRef known))
            return known;
        if (_plain.Contains(appearance) || !_inner.TryGetAppearance(appearance, out AppearanceEntry entry))
            return appearance;
        if (entry.OverlayCount == 0 || !entry.Overlays.Any(o => _hidden(o)))
        {
            _plain.Add(appearance); // an appearance never changes (a look that gains an overlay is a new one)
            return appearance;
        }
        var look = new TableRef(unchecked(Base + (uint)Interlocked.Increment(ref _count)), true);
        _entries[look] = entry.WithOverlays(o => !_hidden(o));
        return _stripped[appearance] = look;
    }

    /// <summary>True when <paramref name="appearance"/> has an overlay drawn another way.</summary>
    public bool HasHidden(TableRef appearance) => Strip(appearance) != appearance;

    public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry)
        => _entries.TryGetValue(reference, out entry) || _inner.TryGetAppearance(reference, out entry);

    public string ResolveString(TableRef reference) => _inner.ResolveString(reference);

    public string ResolveResource(TableRef reference) => _inner.ResolveResource(reference);
}
