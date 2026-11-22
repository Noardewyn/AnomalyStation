using Anomaly.Protocol;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared.Maptext;

// HUD maptext (protocol version 18, D142): the texts of an appearance tree. A screen entry's appearance is a tree (its overlays and
// underlays, and the flattened vis_contents: the item in a hand, an action button's keybind overlay), and any entry of it may carry a
// maptext with its box. Each text is placed from the root icon's lower left corner: the box's own offset plus the pixel offsets of
// the entries between (pixel_x/y/w/z and the translation of a transform, tg's keybind text is shifted so). Scale, rotation and
// colour of the entries between are not applied to the text.

/// <summary>One maptext of an appearance tree.</summary>
/// <param name="Text">The markup (BYOND's maptext).</param>
/// <param name="Width">maptext_width in pixels.</param>
/// <param name="Height">maptext_height in pixels.</param>
/// <param name="X">The box's left from the root icon's left, in pixels (maptext_x plus the entries' offsets).</param>
/// <param name="Y">The box's bottom from the root icon's bottom, in pixels, y up.</param>
/// <param name="Source">Version 74 (gaps W1): the entry that carries it (an animation track names it as its <see cref="AnimationTrack.Child"/>).</param>
/// <param name="MaptextX">That entry's own maptext_x (where its animation ends: the client plays the chain's difference from it).</param>
/// <param name="Mask">That entry's alpha mask filter, when it has one (tg's status display masks its scrolling lines to its screen).</param>
public readonly record struct MaptextItem(string Text, int Width, int Height, float X, float Y, TableRef Source = default, float MaptextX = 0f, MaptextMask? Mask = null);

/// <summary>
/// Version 74: an alpha mask filter on a maptext's entry (BYOND's <c>alpha</c> filter with an icon): the icon state that clips the entry's picture, its
/// lower left at <paramref name="X"/>, <paramref name="Y"/> pixels from the root icon's lower left (the entry's own icon's corner plus the filter's x and y).
/// </summary>
public readonly record struct MaptextMask(string Icon, string State, float X, float Y);

/// <summary>Collects the maptexts of an appearance tree, cached per appearance (the tables are append-only: an entry never changes).</summary>
public sealed class MaptextCollector
{
    private const int MaxDepth = 12;
    private const int MaxItems = 16;

    private readonly Dictionary<(uint Index, bool Private, bool RootOffset), MaptextItem[]> _cache = new();
    private static readonly MaptextItem[] None = Array.Empty<MaptextItem>();

    /// <summary>The number of appearances cached.</summary>
    public int CachedCount => _cache.Count;

    /// <summary>Forgets everything (a reconnect: new tables).</summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// The maptexts of <paramref name="root"/> in draw order (underlays, the entry's own, overlays; later ones draw above). The root's own
    /// pixel offset counts when <paramref name="rootOffset"/> is set (the HUD's screen objects, as the compositor's).
    /// </summary>
    public MaptextItem[] Collect(IAppearanceSource tables, TableRef root, bool rootOffset = true)
    {
        if (root.IsNone) return None;
        var key = (root.Index, root.Private, rootOffset);
        if (_cache.TryGetValue(key, out MaptextItem[] known)) return known;
        List<MaptextItem> found = null;
        Walk(tables, root, 0f, 0f, rootOffset, 0, ref found);
        MaptextItem[] items = found == null ? None : found.ToArray();
        _cache[key] = items;
        return items;
    }

    // The entry's first alpha filter with an icon (BYOND multiplies the entry's picture, its maptext with it, by that icon's alpha).
    private static MaptextMask? MaskOf(IAppearanceSource tables, AppearanceEntry entry, float x, float y)
    {
        if (entry.Filters == null)
            return null;
        foreach (FilterEntry f in entry.Filters)
        {
            if (f.Kind != FilterKind.Alpha || f.Icon.IsNone || f.Flags != 0)
                continue;
            string icon = tables.ResolveResource(f.Icon);
            if (icon != null)
                return new MaptextMask(icon, tables.ResolveString(f.IconState), x + f.X, y + f.Y);
        }
        return null;
    }

    private static void Walk(IAppearanceSource tables, TableRef reference, float parentX, float parentY, bool useOwnOffset, int depth, ref List<MaptextItem> found)
    {
        if (depth > MaxDepth || !tables.TryGetAppearance(reference, out AppearanceEntry entry)) return;
        float x = parentX, y = parentY;
        if (useOwnOffset)
        {
            x += entry.PixelX + entry.PixelW;
            y += entry.PixelY + entry.PixelZ;
            if (entry.Transform is { Length: 6 } m)
            {
                x += m[2]; // the wire's a b c d e f: x' = a x + b y + c, y' = d x + e y + f
                y += m[5];
            }
        }

        foreach (TableRef child in entry.Underlays)
            Walk(tables, child, x, y, true, depth + 1, ref found);
        if (!entry.Maptext.IsNone)
        {
            string text = tables.ResolveString(entry.Maptext);
            if (!string.IsNullOrEmpty(text) && (found == null || found.Count < MaxItems))
            {
                found ??= new List<MaptextItem>();
                found.Add(new MaptextItem(text, entry.MaptextWidth, entry.MaptextHeight, x + entry.MaptextX, y + entry.MaptextY, reference, entry.MaptextX, MaskOf(tables, entry, x, y)));
            }
        }

        foreach (TableRef child in entry.Overlays)
            Walk(tables, child, x, y, true, depth + 1, ref found);
    }
}
