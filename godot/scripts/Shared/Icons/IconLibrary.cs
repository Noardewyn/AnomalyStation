using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Anomaly.Client.Shared.Icons;

/// <summary>Where the converted icons are read from (the files tools/Anomaly.Tools.Icons wrote): paths are relative with '/'.</summary>
public interface IIconFiles
{
    /// <summary>The bytes of the file at <paramref name="relativePath"/>; false when there is no such file.</summary>
    bool TryRead(string relativePath, out byte[] bytes);
}

/// <summary>The converted icons in a folder (normally <c>godot/assets/tg</c>).</summary>
public sealed class DirectoryIconFiles : IIconFiles
{
    private readonly string _root;

    public DirectoryIconFiles(string root) => _root = root;

    /// <summary>The folder.</summary>
    public string Root => _root;

    public bool TryRead(string relativePath, out byte[] bytes)
    {
        if (RuntimeImages.TryRead(relativePath, out bytes)) // protocol version 79: an image the server's world made
            return true;
        if (string.IsNullOrEmpty(relativePath) || relativePath.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
            return false;
        string path = Path.Combine(_root, relativePath);
        if (!File.Exists(path))
            return false;
        try
        {
            bytes = File.ReadAllBytes(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>
/// The client's <b>IconAtlas</b> (M2 P7): every icon the replicated appearances name, loaded lazily one file at a time. A resource is
/// <c>icons/obj/doors/airlock.dmi</c> (the converted .png and .json) or <c>gags:&lt;config type&gt;|&lt;colours&gt;</c> (a GAGS composition,
/// <see cref="GagsIcon"/>). Misses are remembered so a missing file costs one disk probe, not one per frame; problems (a missing file, state or
/// config) are recorded once in <see cref="Problems"/>. Main thread only.
/// <para>
/// With <see cref="Background"/> on (the game, once the map is built), <see cref="TryGetIconNow"/> reads and decodes a sheet on a worker
/// thread and answers "pending" until it is done, so a big sheet first named mid-game (tg's screen_full.dmi is 4800 x 4320, the explosion's
/// 96x96.dmi 2784 x 2688) no longer stalls a frame: the picture is drawn a few frames later. <see cref="TryGetIcon"/> stays blocking (it
/// waits for a load in flight rather than decoding twice) for the map's plan, the smoothing and the other readers that take a miss as final.
/// </para>
/// </summary>
public sealed class IconLibrary
{
    private readonly IIconFiles _files;
    private readonly Dictionary<string, IIcon> _icons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GagsConfig> _configs = new(StringComparer.Ordinal);
    private readonly List<string> _problems = new();
    private readonly HashSet<string> _problemSet = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<(IIcon Icon, string Problem)>> _loading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Lazy<(IIcon Icon, string Problem)>> _reads = new(StringComparer.Ordinal); // each load's read: whoever comes first does it

    public IconLibrary(IIconFiles files) => _files = files;

    /// <summary>On: <see cref="TryGetIconNow"/> and <see cref="Prefetch"/> load sheets on worker threads. Off (the default): every load blocks.</summary>
    public bool Background { get; set; }

    /// <summary>Sheets being read on worker threads.</summary>
    public int PendingCount => _loading.Count;

    /// <summary>Counts the background loads taken in (a caller that met "pending" tries again when it changes).</summary>
    public int CompletedLoads { get; private set; }

    /// <summary>Counts the "pending" answers of <see cref="TryGetIconNow"/>: a caller compares it before and after a draw to tell
    /// "still loading" from "missing".</summary>
    public int PendingMisses { get; private set; }

    /// <summary>Frame probe: an icon loaded (resource, milliseconds).</summary>
    public static Action<string, double> ProbeLoaded;

    /// <summary>The distinct problems met so far, in order (each a sentence).</summary>
    public IReadOnlyList<string> Problems => _problems;

    /// <summary>Raised with each new problem (the client logs it once).</summary>
    public event Action<string> ProblemFound;

    /// <summary>How many sheets and GAGS compositions are loaded.</summary>
    public int Loaded => _icons.Count;

    internal void Report(string problem)
    {
        if (_problemSet.Add(problem))
        {
            _problems.Add(problem);
            ProblemFound?.Invoke(problem);
        }
    }

    /// <summary>Adds an icon that is not a file (a test's picture, a generated one) under <paramref name="resource"/>.</summary>
    public void Register(string resource, IIcon icon) => _icons[resource] = icon;

    /// <summary>Forgets everything (a reconnect, or files regenerated).</summary>
    public void Clear()
    {
        _icons.Clear();
        _configs.Clear();
    }

    /// <summary>The icon of a resource string; false when its file is missing or unreadable.</summary>
    public bool TryGetIcon(string resource, out IIcon icon)
    {
        icon = null;
        if (string.IsNullOrEmpty(resource))
            return false;
        if (_icons.TryGetValue(resource, out icon))
            return icon != null;
        if (_loading.TryGetValue(resource, out Task<(IIcon Icon, string Problem)> inFlight))
        {
            Install(resource); // reads it now, or waits for the worker that is reading it
            icon = _icons[resource];
            return icon != null;
        }
        long probeStart = System.Diagnostics.Stopwatch.GetTimestamp();
        icon = resource.StartsWith("gags:", StringComparison.Ordinal) ? LoadGags(resource)
            : resource.StartsWith(IconOps.Prefix, StringComparison.Ordinal) ? IconOps.Load(this, resource) // M6 slice 7: an /icon's Blend recipe
            : LoadSheet(resource);
        ProbeLoaded?.Invoke(resource, System.Diagnostics.Stopwatch.GetElapsedTime(probeStart).TotalMilliseconds);
        _icons[resource] = icon;
        return icon != null;
    }

    /// <summary>
    /// As <see cref="TryGetIcon"/>, but with <see cref="Background"/> on a .dmi sheet not loaded yet is read on a worker thread: false with
    /// <paramref name="pending"/> set until it is done (try again on a later frame). GAGS and icon-ops resources are made in place as before.
    /// </summary>
    public bool TryGetIconNow(string resource, out IIcon icon, out bool pending)
    {
        pending = false;
        icon = null;
        if (string.IsNullOrEmpty(resource))
            return false;
        if (_icons.TryGetValue(resource, out icon))
            return icon != null;
        if (!Background || !IsSheet(resource))
            return TryGetIcon(resource, out icon);
        if (!_loading.TryGetValue(resource, out Task<(IIcon Icon, string Problem)> task))
            _loading[resource] = task = StartLoad(resource);
        // a sheet wanted for drawing whose load no worker has picked up within a quarter of a second is read here (tens of ms): behind a long
        // queue of the pool's work (the models' texture decodes) a new HUD button's icon waited minutes, so the button was not drawn at all
        if (!task.IsCompleted && task.Status == TaskStatus.WaitingToRun && WaitedTooLong(resource))
            task = Task.FromResult(default((IIcon, string)));
        if (task.IsCompleted)
        {
            Install(resource);
            icon = _icons[resource];
            return icon != null;
        }
        pending = true;
        PendingMisses++;
        return false;
    }

    // When each sheet still loading was first wanted for drawing (Stopwatch ticks).
    private readonly Dictionary<string, long> _wanted = new(StringComparer.Ordinal);

    private bool WaitedTooLong(string resource)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!_wanted.TryGetValue(resource, out long since))
        {
            _wanted[resource] = now;
            return false;
        }
        return System.Diagnostics.Stopwatch.GetElapsedTime(since, now).TotalSeconds > 0.25;
    }

    /// <summary>Starts reading a .dmi sheet on a worker thread (with <see cref="Background"/> on), so that it is ready when first drawn.</summary>
    public void Prefetch(string resource)
    {
        if (!Background || string.IsNullOrEmpty(resource) || !IsSheet(resource) || _icons.ContainsKey(resource) || _loading.ContainsKey(resource))
            return;
        _loading[resource] = StartLoad(resource);
    }

    /// <summary>Once per frame: takes in the sheets whose background loads finished; returns how many.</summary>
    public int Pump()
    {
        if (_loading.Count == 0)
            return 0;
        List<string> done = null;
        foreach (KeyValuePair<string, Task<(IIcon Icon, string Problem)>> kv in _loading)
        {
            if (kv.Value.IsCompleted)
                (done ??= new List<string>()).Add(kv.Key);
        }
        if (done == null)
            return 0;
        foreach (string resource in done)
            Install(resource);
        return done.Count;
    }

    private static bool IsSheet(string resource)
        => !resource.StartsWith("gags:", StringComparison.Ordinal) && !resource.StartsWith(IconOps.Prefix, StringComparison.Ordinal);

    private Task<(IIcon Icon, string Problem)> StartLoad(string resource)
    {
        var read = new Lazy<(IIcon Icon, string Problem)>(() =>
        {
            long probeStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var result = ReadSheet(resource);
            ProbeLoaded?.Invoke(System.Threading.Thread.CurrentThread.IsThreadPoolThread ? resource + " (background)" : resource, System.Diagnostics.Stopwatch.GetElapsedTime(probeStart).TotalMilliseconds);
            return result;
        });
        _reads[resource] = read;
        return Task.Run(() => read.Value);
    }

    // On the main thread: a finished load goes into the cache and its problem into the report. A load no worker has started is read here: waiting
    // for it behind the pool's queue froze the frame for seconds on a busy machine (13 s, 2026-10-30), where the read itself takes tens of ms.
    private void Install(string resource)
    {
        _loading.Remove(resource);
        _wanted.Remove(resource);
        _reads.Remove(resource, out Lazy<(IIcon Icon, string Problem)> read);
        (IIcon icon, string problem) = read.Value;
        _icons[resource] = icon;
        if (problem != null)
            Report(problem);
        CompletedLoads++;
    }

    /// <summary>The sheet of a .dmi resource (null for a GAGS resource or a missing file).</summary>
    public IconSheet GetSheet(string resource) => TryGetIcon(resource, out IIcon icon) ? icon as IconSheet : null;

    internal bool TryGetGags(string type, List<uint> colors, out GagsIcon icon)
    {
        icon = null;
        GagsConfig config = LoadConfig(type);
        if (config == null)
            return false;
        // A cached composition per config and colours, like tg's icon_cache of the config.
        string key = "gags:" + type + "|" + string.Join(string.Empty, colors.Select(c => "#" + c.ToString("x8")));
        if (_icons.TryGetValue(key, out IIcon cached))
        {
            icon = cached as GagsIcon;
            return icon != null;
        }
        icon = new GagsIcon(config, this, colors);
        _icons[key] = icon;
        return true;
    }

    private IIcon LoadSheet(string resource)
    {
        (IIcon icon, string problem) = ReadSheet(resource);
        if (problem != null)
            Report(problem);
        return icon;
    }

    // Reads and decodes a sheet; it touches nothing of the library, so a worker thread may run it.
    private (IIcon Icon, string Problem) ReadSheet(string resource)
    {
        string stem = resource.Replace('\\', '/');
        if (stem.StartsWith("./", StringComparison.Ordinal))
            stem = stem.Substring(2);
        // a .dmi, one of tgui's GIF pictures (M9 slice E) or the title's PNG: tools/Anomaly.Tools.Icons converts each into the same .png and .json
        if (stem.EndsWith(".dmi", StringComparison.OrdinalIgnoreCase) || stem.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) || stem.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            stem = stem.Substring(0, stem.Length - 4);
        if (!_files.TryRead(stem + ".json", out byte[] json) || !_files.TryRead(stem + ".png", out byte[] png))
            return (null, "the icon " + resource + " is not in the converted icons (run tools/Anomaly.Tools.Icons)");
        try
        {
            Raster pixels = PngDecoder.Decode(png);
            IconSheet sheet = IconSheet.Parse(System.Text.Encoding.UTF8.GetString(json), pixels);
            HandBoxes.Mirror(stem + ".dmi", sheet, pixels);
            return (sheet, null);
        }
        catch (InvalidDataException e)
        {
            return (null, "the icon " + resource + " is unreadable: " + e.Message);
        }
    }

    private IIcon LoadGags(string resource)
    {
        string body = resource.Substring("gags:".Length);
        int bar = body.IndexOf('|');
        string type = bar < 0 ? body : body.Substring(0, bar);
        List<uint> colors = GagsColors.Parse(bar < 0 ? string.Empty : body.Substring(bar + 1));
        GagsConfig config = LoadConfig(type);
        return config == null ? null : new GagsIcon(config, this, colors);
    }

    private GagsConfig LoadConfig(string type)
    {
        if (_configs.TryGetValue(type, out GagsConfig config))
            return config;
        config = null;
        if (_files.TryRead("gags/" + GagsConfig.FileOf(type), out byte[] json))
        {
            try
            {
                config = GagsConfig.Parse(System.Text.Encoding.UTF8.GetString(json));
            }
            catch (InvalidDataException e)
            {
                Report("the GAGS config " + type + " is unreadable: " + e.Message);
            }
        }
        else
        {
            Report("the GAGS config " + type + " is not in the converted icons (run tools/Anomaly.Tools.Icons)");
        }
        _configs[type] = config;
        return config;
    }
}
