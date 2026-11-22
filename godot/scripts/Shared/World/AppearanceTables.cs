using Anomaly.Protocol;

namespace Anomaly.Client.Shared.World;

/// <summary>
/// The public resource, string and appearance tables of a scripted world (the fake server's Snapshot carries them), with
/// interning: one entry per distinct (icon, state, layer, plane, pixel offsets). The server's replicator does the same for the real
/// world's appearances (docs/ai/specs/replication.md); this is its small stand-in for the fake feed.
/// </summary>
public sealed class AppearanceTables
{
    private readonly List<string> _resources = new();
    private readonly List<string> _strings = new();
    private readonly List<AppearanceEntry> _appearances = new();
    private readonly Dictionary<string, TableRef> _resourceIndex = new();
    private readonly Dictionary<string, TableRef> _stringIndex = new();
    private readonly Dictionary<(string, string, float, short, short, byte, short), TableRef> _appearanceIndex = new();

    /// <summary>The public resource table (index 1 is element 0).</summary>
    public IReadOnlyList<string> Resources => _resources;

    /// <summary>The public string table.</summary>
    public IReadOnlyList<string> Strings => _strings;

    /// <summary>The public appearance table.</summary>
    public IReadOnlyList<AppearanceEntry> Appearances => _appearances;

    /// <summary>Starts the tables with entries another builder already made (their references stay valid: same indices).</summary>
    public void Seed(IEnumerable<string> resources, IEnumerable<string> strings, IEnumerable<AppearanceEntry> appearances)
    {
        if (_resources.Count + _strings.Count + _appearances.Count != 0)
            throw new InvalidOperationException("only empty tables can be seeded");
        foreach (string resource in resources)
            Resource(resource);
        foreach (string text in strings)
            String(text);
        _appearances.AddRange(appearances);
    }

    /// <summary>The reference of a resource path, added on first use; none for null.</summary>
    public TableRef Resource(string path)
    {
        if (path == null)
            return TableRef.None;
        if (!_resourceIndex.TryGetValue(path, out TableRef reference))
        {
            _resources.Add(path);
            reference = TableRef.Public((uint)_resources.Count);
            _resourceIndex[path] = reference;
        }
        return reference;
    }

    /// <summary>The reference of a string, added on first use; none for null.</summary>
    public TableRef String(string text)
    {
        if (text == null)
            return TableRef.None;
        if (!_stringIndex.TryGetValue(text, out TableRef reference))
        {
            _strings.Add(text);
            reference = TableRef.Public((uint)_strings.Count);
            _stringIndex[text] = reference;
        }
        return reference;
    }

    /// <summary>An appearance of an icon and state with one overlay of the same icon (a closet's door overlay, closets.dm closet_update_overlays).</summary>
    public TableRef AppearanceWithOverlay(string icon, string iconState, string overlayState, float layer = 3f, short pixelX = 0, short pixelY = 0)
    {
        var key = (icon ?? string.Empty, iconState ?? string.Empty, overlayState ?? string.Empty, layer, pixelX, pixelY);
        if (_overlaidIndex.TryGetValue(key, out TableRef reference))
            return reference;
        var entry = new AppearanceEntry { Icon = Resource(icon), IconState = String(iconState), Layer = layer, PixelX = pixelX, PixelY = pixelY };
        entry.Overlays.Add(Appearance(icon, overlayState, layer));
        _appearances.Add(entry);
        reference = TableRef.Public((uint)_appearances.Count);
        _overlaidIndex[key] = reference;
        return reference;
    }

    private readonly Dictionary<(string, string, string, float, short, short), TableRef> _overlaidIndex = new();

    /// <summary>Adds an appearance as it is (not shared with an equal one).</summary>
    public TableRef Add(AppearanceEntry entry)
    {
        _appearances.Add(entry);
        return TableRef.Public((uint)_appearances.Count);
    }

    /// <summary>The reference of an appearance made of an icon and state (<c>none</c> when both are null).</summary>
    public TableRef Appearance(string icon, string iconState, float layer = 3f, short pixelX = 0, short pixelY = 0, byte dir = 0, short plane = 0)
    {
        if (icon == null && iconState == null)
            return TableRef.None;
        var key = (icon ?? string.Empty, iconState ?? string.Empty, layer, pixelX, pixelY, dir, plane);
        if (!_appearanceIndex.TryGetValue(key, out TableRef reference))
        {
            _appearances.Add(new AppearanceEntry
            {
                Icon = Resource(icon),
                IconState = String(iconState),
                Dir = dir,
                Layer = layer,
                Plane = plane,
                PixelX = pixelX,
                PixelY = pixelY,
            });
            reference = TableRef.Public((uint)_appearances.Count);
            _appearanceIndex[key] = reference;
        }
        return reference;
    }
}
