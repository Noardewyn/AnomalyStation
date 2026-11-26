using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Anomaly.Client.Audio;
using Anomaly.Client.Shared.Audio;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Client.Render;

/// <summary>
/// The start's preload (docs/ai/specs/multiplayer.md, the client's hitches): from <c>App._Ready</c>, on worker threads and without holding the connect
/// screen up, what is cheap to keep and common to every round goes into memory: the bytes of every converted tg sprite sheet (their PNGs and
/// descriptions; decoding them all would take about a gigabyte, so the round's own are decoded at the join, <see cref="IconPrefetch"/>), every 3D
/// model parsed with its textures decoded, and every sound as Godot's compressed stream. The game reads them here before it touches the disk;
/// what is not in yet is read as before. GPU uploads stay on the main thread. <c>ANOMALY_PRELOAD=0</c> turns it off.
/// </summary>
public static class AssetPreload
{
    private static Task _files;
    private static Task _sounds;
    private static int _done;
    private static int _total = 1;
    private static string _iconRoot;
    private static readonly ConcurrentDictionary<string, byte[]> Icons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, GlbFile> Glbs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte[]> ModelImages = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<byte[], Raster> Decoded = new(ReferenceEqualityComparer.Instance);
    private static readonly ConcurrentDictionary<string, AudioStream> Sounds = new(StringComparer.OrdinalIgnoreCase);

    // Each part's workers: the rest of the pool stays for the connect screen's and the game's own work.
    private static int Workers => Math.Clamp(System.Environment.ProcessorCount / 4, 1, 6);

    /// <summary>The sheets' bytes and the models are in (or the preload is off).</summary>
    public static bool FilesDone => _files == null || _files.IsCompleted;

    /// <summary>The sounds are in (or the preload is off).</summary>
    public static bool SoundsDone => _sounds == null || _sounds.IsCompleted;

    /// <summary>How far the whole preload is, 0 to 1.</summary>
    public static double Progress => Math.Min(1.0, (double)Volatile.Read(ref _done) / Volatile.Read(ref _total));

    /// <summary>Starts the preload once (the paths are read here, on the main thread).</summary>
    public static void Start()
    {
        if (_files != null)
            return;
        IconLibrary.ProbeLoaded += (resource, ms) =>
        {
            if (resource.StartsWith("gags:", StringComparison.Ordinal) || resource.StartsWith(IconOps.Prefix, StringComparison.Ordinal))
                return; // made of sheets, no load of its own
            const string worker = " (background)";
            bool blocked = !resource.EndsWith(worker, StringComparison.Ordinal);
            Miss("sheet", blocked ? resource : resource[..^worker.Length], ms, blocked);
        };
        if (System.Environment.GetEnvironmentVariable("ANOMALY_PRELOAD") == "0" || !IconPaths.Exists)
            return;
        string icons = _iconRoot = Path.GetFullPath(IconPaths.Root);
        string models = ModelLibrary.Root;
        string sounds = SoundLibrary.DefaultRoot;
        var watch = Stopwatch.StartNew();
        _files = Task.Run(() =>
        {
            string[] iconFiles = Files(icons, "icons", "*.png", "*.json").Concat(Files(icons, "gags", "*.json")).ToArray();
            string[] glbs = Files(models, ".", "*.glb").ToArray();
            string[] pngs = Files(models, ".", "*.png").ToArray();
            Interlocked.Add(ref _total, iconFiles.Length + glbs.Length + pngs.Length);
            long iconBytes = 0;
            Parallel.ForEach(iconFiles, Options, file =>
            {
                byte[] bytes = Read(file);
                if (bytes != null)
                {
                    Icons[Relative(icons, file)] = bytes;
                    Interlocked.Add(ref iconBytes, bytes.Length);
                }
                Interlocked.Increment(ref _done);
            });
            long imageBytes = 0;
            Parallel.ForEach(pngs, Options, file =>
            {
                if (Read(file) is { } bytes)
                    ModelImages[Relative(models, file)] = Canonical(bytes);
                Interlocked.Increment(ref _done);
            });
            Parallel.ForEach(glbs, Options, file =>
            {
                try
                {
                    if (Read(file) is { } bytes)
                    {
                        GlbFile glb = GlbFile.Read(bytes, uri => GlbFile.TgIcon(uri) is string icon ? IconPng(icon) : null);
                        // a kit's modules each embed the same atlas: one copy is kept and decoded for all (755 embedded images, 128 distinct)
                        for (int i = 0; i < glb.Images.Count; i++)
                            if (glb.Images[i] is { } image && !SheetImages.ContainsKey(image))
                                glb.Images[i] = Canonical(image);
                        Glbs[Relative(models, file)] = glb;
                    }
                }
                catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException or InvalidOperationException)
                {
                    GD.PrintErr($"[anomaly] preload: {file} is not read ({e.Message}); the game reads it when it shows");
                }
                Interlocked.Increment(ref _done);
            });
            foreach (Raster r in Decoded.Values)
                imageBytes += r.Pixels.Length;
            GD.Print(string.Create(CultureInfo.InvariantCulture,
                $"[anomaly] preload: {Icons.Count} sheet files ({iconBytes / 1048576.0:0} MB), {Glbs.Count} models and {Decoded.Count} textures ({imageBytes / 1048576.0:0} MB decoded) in {watch.Elapsed.TotalSeconds:0.0} s"));
        });
        _sounds = Task.Run(() =>
        {
            SoundIndex index = SoundIndex.Load(Path.Combine(sounds, "sound", "index.json"), out _);
            if (index == null)
                return;
            SoundIndexEntry[] entries = index.Entries.ToArray();
            Interlocked.Add(ref _total, entries.Length);
            long bytes = 0;
            Parallel.ForEach(entries, Options, entry =>
            {
                string file = Path.Combine(sounds, entry.File);
                AudioStream stream = File.Exists(file)
                    ? entry.Format switch
                    {
                        "ogg" => SoundLibrary.LoadOgg(file),
                        "wav" => AudioStreamWav.LoadFromFile(file),
                        "mp3" => AudioStreamMP3.LoadFromFile(file),
                        _ => null,
                    }
                    : null;
                if (stream != null)
                {
                    Sounds[Key(file)] = stream;
                    Interlocked.Add(ref bytes, entry.Bytes);
                }
                Interlocked.Increment(ref _done);
            });
            GD.Print(string.Create(CultureInfo.InvariantCulture, $"[anomaly] preload: {Sounds.Count} sounds ({bytes / 1048576.0:0} MB) in {watch.Elapsed.TotalSeconds:0.0} s"));
        });
    }

    private static ParallelOptions Options => new() { MaxDegreeOfParallelism = Workers };

    private static IEnumerable<string> Files(string root, string folder, params string[] patterns)
    {
        string dir = Path.Combine(root, folder);
        return Directory.Exists(dir) ? patterns.SelectMany(p => Directory.EnumerateFiles(dir, p, SearchOption.AllDirectories)) : [];
    }

    private static byte[] Read(string file)
    {
        try
        {
            return File.ReadAllBytes(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static readonly ConcurrentDictionary<string, byte[]> ByContent = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<byte[], bool> SheetImages = new(ReferenceEqualityComparer.Instance);

    // The one copy of an image's bytes, decoded once: the first one seen.
    private static byte[] Canonical(byte[] png)
    {
        byte[] kept = ByContent.GetOrAdd(Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(png)), png);
        if (ReferenceEquals(kept, png))
            Decode(png);
        return kept;
    }

    private static void Decode(byte[] png)
    {
        try
        {
            Decoded.TryAdd(png, PngDecoder.Decode(png));
        }
        catch (InvalidDataException)
        {
            // the game decodes it again when it shows, and says why there
        }
    }

    private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

    private static string Key(string file) => Path.GetFullPath(file).Replace('\\', '/');

    // A tg sheet's PNG a model's texture names (ModelLibrary.ImageBytes' rule: "icons/....dmi" is the converted PNG beside its description).
    private static byte[] IconPng(string icon)
    {
        string png = icon[..^4] + ".png";
        byte[] bytes = Icons.TryGetValue(png, out byte[] kept) ? kept : Read(Path.Combine(_iconRoot, png));
        if (bytes != null)
            SheetImages[bytes] = true; // a sheet's picture: not decoded here (the join decodes the round's sheets)
        return bytes;
    }

    // ---- what was not ready when it was first used, once the world is shown ----

    private static readonly HashSet<string> Missed = new(StringComparer.Ordinal);
    private static volatile bool _watching;

    /// <summary>The world is shown (on) or a new one loads (off): from then on each sheet, model or sound loaded for the first time is logged.</summary>
    public static bool Watching
    {
        get => _watching;
        set => _watching = value;
    }

    /// <summary>The distinct sheets, models and sounds loaded after the world was shown (the client summary's <c>preload_misses</c>).</summary>
    public static int Misses
    {
        get
        {
            lock (Missed)
                return Missed.Count;
        }
    }

    /// <summary>
    /// A <paramref name="kind"/> (sheet, model, sound) loaded for the first time after the world was shown, <paramref name="ms"/> long, on the main
    /// thread (<paramref name="blocked"/>: the frame waited) or a worker: logged once each, so the join's common list can grow.
    /// </summary>
    public static void Miss(string kind, string name, double ms, bool blocked)
    {
        if (!_watching)
            return;
        lock (Missed)
        {
            if (!Missed.Add(kind + " " + name))
                return;
        }
        GD.Print(string.Create(CultureInfo.InvariantCulture, $"[anomaly] preload miss: {kind} {name} {ms:0.0} ms{(blocked ? ", in the frame" : ", on a worker")}"));
    }

    /// <summary>The preloaded icon files under <paramref name="folder"/> ("icons/hud/"), by their paths.</summary>
    public static IEnumerable<string> IconsUnder(string folder) => Icons.Keys.Where(k => k.StartsWith(folder, StringComparison.OrdinalIgnoreCase));

    /// <summary>A converted icon file's bytes by its path under the icons' root ("icons/obj/doors/airlock.png").</summary>
    public static bool TryIconBytes(string relative, out byte[] bytes)
    {
        bytes = null;
        return relative != null && Icons.TryGetValue(relative.Replace('\\', '/'), out bytes);
    }

    /// <summary>A model file parsed, by its path under the models' root.</summary>
    public static bool TryGlb(string relative, out GlbFile file)
    {
        file = null;
        return relative != null && Glbs.TryGetValue(relative.Replace('\\', '/'), out file);
    }

    /// <summary>A PNG of the models' folder, by its path under the models' root (the same bytes the decoded texture is kept for).</summary>
    public static bool TryModelImage(string relative, out byte[] bytes)
    {
        bytes = null;
        return relative != null && ModelImages.TryGetValue(relative.Replace('\\', '/'), out bytes);
    }

    /// <summary>The decoded texture of a model's PNG bytes (these very bytes: a parsed model's image or a models' folder PNG from here).</summary>
    public static bool TryDecoded(byte[] png, out Raster raster)
    {
        raster = null;
        return png != null && Decoded.TryGetValue(png, out raster);
    }

    /// <summary>A sound file's stream, by the file's path.</summary>
    public static bool TrySound(string file, out AudioStream stream)
    {
        stream = null;
        return file != null && Sounds.TryGetValue(Key(file), out stream);
    }

    /// <summary>The converted icons of <paramref name="root"/>, read from the preload when it is the game's own folder, else from the disk.</summary>
    public static IIconFiles IconFiles(string root)
    {
        var disk = new DirectoryIconFiles(root);
        return _iconRoot != null && string.Equals(Path.GetFullPath(root), _iconRoot, StringComparison.OrdinalIgnoreCase) ? new PreloadedIconFiles(disk) : disk;
    }

    private sealed class PreloadedIconFiles : IIconFiles
    {
        private readonly DirectoryIconFiles _disk;

        public PreloadedIconFiles(DirectoryIconFiles disk) => _disk = disk;

        // the server's own pictures first (DirectoryIconFiles' order), then the preload, then the disk
        public bool TryRead(string relativePath, out byte[] bytes)
            => RuntimeImages.TryRead(relativePath, out bytes) || TryIconBytes(relativePath, out bytes) || _disk.TryRead(relativePath, out bytes);
    }
}
