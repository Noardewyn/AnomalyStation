using System.Threading;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.World;

/// <summary>
/// M8x slice C (D210). tg's visual helpers (<c>/atom/movable/visual</c>: the cryo cell's occupant, cryo.dm:14) stay in their holder's
/// <c>vis_contents</c> and are sent as visuals of the holder (NetField.VisHost), so that their own <c>animate()</c> chain reaches the client.
/// BYOND draws a <c>vis_contents</c> object as part of its holder, sorted by layer and plane among the holder's overlays (the cell's cover
/// over the patient): <see cref="With"/> names a copy of the holder's appearance with the visual's appearance as one more overlay, at the
/// pixel offsets its chain gives it now, which this source resolves (everything else is the inner source's). Engine-free.
/// </summary>
public sealed class VisInsets : IAppearanceSource
{
    private const uint Base = 0xE0000000u; // OverlayStripper's copies start at 0xF0000000
    private const int MaxCopies = 4096;
    private static int _count; // static: the compositor's caches outlive a layer

    private readonly IAppearanceSource _inner;
    private readonly Dictionary<(TableRef Host, TableRef Inset, short X, short Y, byte Alpha), TableRef> _made = new();
    private readonly Dictionary<TableRef, AppearanceEntry> _entries = new();

    public VisInsets(IAppearanceSource inner) => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>
    /// The holder's appearance <paramref name="host"/> with the visual <paramref name="inset"/> drawn into it at the pixel offsets
    /// <paramref name="pixelX"/>, <paramref name="pixelY"/> (its own and its chain's) and the <paramref name="alpha"/> its chain gives it now
    /// (the stasis bed's mattress fading, M8y); the holder itself when either is unknown.
    /// </summary>
    public TableRef With(TableRef host, TableRef inset, int pixelX, int pixelY, int alpha = 255)
    {
        short x = (short)Math.Clamp(pixelX, short.MinValue, short.MaxValue), y = (short)Math.Clamp(pixelY, short.MinValue, short.MaxValue);
        byte a = (byte)Math.Clamp(alpha, 0, 255);
        if (_made.TryGetValue((host, inset, x, y, a), out TableRef known))
            return known;
        if (!TryGetAppearance(host, out AppearanceEntry holder) || !TryGetAppearance(inset, out AppearanceEntry visual))
            return host;
        if (_entries.Count >= MaxCopies)
        {
            _made.Clear(); // a copy is never reused once forgotten: the next one has a new reference, so no cache holds a stale picture
            _entries.Clear();
        }
        AppearanceEntry moved = visual.WithOverlays(_ => true);
        moved.PixelX = x;
        moved.PixelY = y;
        moved.Alpha = a;
        // DEVIATION(DV207): BYOND draws a "*" render target only through an appearance's render_source, which the wire does not carry
        // (tg's eyelids: the human's overlays take them by render_source): the inset is drawn itself instead
        moved.RenderTarget = TableRef.None;
        var movedRef = new TableRef(unchecked(Base + (uint)Interlocked.Increment(ref _count)), true);
        _entries[movedRef] = moved;
        AppearanceEntry copy = holder.WithOverlays(_ => true);
        copy.Overlays.Add(movedRef);
        var look = new TableRef(unchecked(Base + (uint)Interlocked.Increment(ref _count)), true);
        _entries[look] = copy;
        return _made[(host, inset, x, y, a)] = look;
    }

    public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry)
        => _entries.TryGetValue(reference, out entry) || _inner.TryGetAppearance(reference, out entry);

    public string ResolveString(TableRef reference) => _inner.ResolveString(reference);

    public string ResolveResource(TableRef reference) => _inner.ResolveResource(reference);
}
