using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: contracts (W3.0). The per-world storage behind the /world vars of code/world.dm (world.turf, area, mob,
// name, view, params, system_type, sleep_offline), the map grid hook behind world.maxx/maxy/maxz, the host file seam
// and the Dm.Deferred counters. The Tg bootstrap sets tg's /world defaults; the shim only stores them.
public sealed partial class DmWorldState
{
    /// <summary>BYOND <c>world.turf</c>: the default turf type. Null until the bootstrap sets tg's (<c>/turf/open/space/basic</c>).</summary>
    public Type turf;

    /// <summary>BYOND <c>world.area</c>: the default area type. Null until the bootstrap sets tg's (<c>/area/space</c>).</summary>
    public Type area;

    /// <summary>BYOND <c>world.mob</c>: the type of a connecting player's mob. Null until the bootstrap sets tg's.</summary>
    public Type mob;

    /// <summary>BYOND <c>world.name</c>. Null until the bootstrap sets tg's.</summary>
    public string name;

    /// <summary>BYOND <c>world.view</c>: a number or a size text (<c>"15x15"</c>); BYOND's default is 5.</summary>
    public object view = 5;

    /// <summary>
    /// BYOND <c>world.params</c>: the world's start parameters, an associative list of text, empty by default (tg indexes it
    /// with its parameter names). Never null unless code assigns null.
    /// </summary>
    public DmList @params = new DmList();

    /// <summary>BYOND <c>world.system_type</c>: always <see cref="Dm.UNIX"/> in the port.</summary>
    public string system_type = Dm.UNIX;

    /// <summary>BYOND <c>world.sleep_offline</c>: stored, never acted on (the host decides when the world steps).</summary>
    public float sleep_offline;

    /// <summary>
    /// The map grid hook: called after <c>world.maxx</c>, <c>world.maxy</c> or <c>world.maxz</c> changes through the
    /// facade (<see cref="DmWorld"/>), with the size before the change (old maxx, old maxy, old maxz); the new size is
    /// in <see cref="maxx"/>, <see cref="maxy"/>, <see cref="maxz"/>. BYOND creates the turfs of a grown map
    /// (<c>world.turf</c> inside <c>world.area</c>) at the assignment; the shim cannot name turfs, so tg's map grid
    /// registers this callback and does it. Writing the fields directly calls nothing.
    /// </summary>
    public Action<float, float, float> MapResized { get; set; }

    private IDmFileSystem _files;

    /// <summary>
    /// The host file seam behind <c>file</c>, <c>fexists</c>, <c>fdel</c>, <c>fcopy</c>, <c>file2text</c>,
    /// <c>text2file</c>, <c>flist</c> and file output (<c>Dm.output_to</c> to a <see cref="DmFileRef"/>). The core has no
    /// file system of its own: by default every world gets its own empty <see cref="DmMemoryFiles"/>; a host that wants
    /// real files sets a <see cref="DmDirectoryFiles"/> (or its own implementation) before the world runs.
    /// </summary>
    public IDmFileSystem Files
    {
        get => _files ??= new DmMemoryFiles();
        set => _files = value;
    }

    /// <summary>
    /// Silicons SI6c (D326): the game's own files beside the world's (BYOND's <c>fexists</c> sees the game folder, tg's
    /// <c>sound/</c> tree among it): the Tg layer answers from the host's sound index (<c>SoundLengths</c>); null for none.
    /// </summary>
    public Func<string, bool> GameFileExists { get; set; }

    /// <summary>
    /// Silicons SI4d (D326): the images this world made at run time, by resource path (<see cref="icon.RuntimePrefix"/>: <c>rsc/MD5.png</c>):
    /// BYOND's resource cache of a file the world wrote (an <c>icon(file)</c> of a PNG). The replicator sends each to a client once.
    /// </summary>
    public Dictionary<string, byte[]> RuntimeResources { get; } = new(StringComparer.Ordinal);

    /// <summary>Silicons SI4d: <see cref="RuntimeResources"/>' paths in the order the world made them (a client is sent them in this order).</summary>
    public List<string> RuntimeResourceOrder { get; } = new();

    /// <summary>Silicons SI4d: takes an image into <see cref="RuntimeResources"/> (once per path).</summary>
    public void AddRuntimeResource(string path, byte[] png)
    {
        if (RuntimeResources.TryAdd(path, png))
            RuntimeResourceOrder.Add(path);
    }

    private int _deferredCount;
    private List<string> _deferredSites;
    private HashSet<string> _deferredSeen;

    /// <summary>How many times <c>Dm.Deferred</c> ran in this world (tests).</summary>
    public int DeferredCount => _deferredCount;

    /// <summary>The distinct <c>where</c> of the <c>Dm.Deferred</c> calls of this world, in order of their first call (tests).</summary>
    public IReadOnlyList<string> DeferredSites => (IReadOnlyList<string>)_deferredSites ?? Array.Empty<string>();

    // Counts one Dm.Deferred call; true for the first call at that site in this world.
    internal bool CountDeferred(string where)
    {
        _deferredCount++;
        if (!(_deferredSeen ??= new HashSet<string>(StringComparer.Ordinal)).Add(where ?? "")) return false;
        (_deferredSites ??= new List<string>()).Add(where ?? "");
        return true;
    }

    // The /world vars are world state (world.turf decides what a growing map is made of): hashed as their own
    // contributor, which adds nothing while every one is at its default, so creating the state never changes the hash.
    // The file seam, the grid hook and the Deferred counters are host plumbing and diagnostics: not hashed.
    internal static void HashWorldVars(World world, ref StateHasher h)
    {
        DmWorldState s = Of(world);
        bool defaults = s.turf == null && s.area == null && s.mob == null && s.name == null
                        && s.view is int five && five == 5 && s.@params != null && s.@params.Count == 0
                        && s.system_type == Dm.UNIX && s.sleep_offline == 0f;
        if (defaults) return;
        h.Add(s.turf == null ? "" : DmTypes.PathOf(s.turf));
        h.Add(s.area == null ? "" : DmTypes.PathOf(s.area));
        h.Add(s.mob == null ? "" : DmTypes.PathOf(s.mob));
        h.Add(s.name ?? "\0null");
        h.Add(s.view == null ? "\0null" : Dm.str(s.view));
        h.Add(s.@params == null ? "\0null" : ParamsText(s.@params));
        h.Add(s.system_type ?? "\0null");
        h.Add(s.sleep_offline);
    }

    private static string ParamsText(DmList list)
    {
        var parts = new List<string>();
        foreach ((object key, object value) in Dm.pairs(list))
            parts.Add(Dm.str(key) + "=" + Dm.str(value));
        return string.Join("&", parts);
    }
}
