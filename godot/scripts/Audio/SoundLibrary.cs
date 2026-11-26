using System.IO;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Audio;

namespace Anomaly.Client.Audio;

/// <summary>
/// tg's sounds as Godot streams (M4b audio slice 3; apps.md 2.20). The sound tool copies tg's .ogg files into the asset root
/// (<c>godot/assets/tg</c>, git-ignored and <c>.gdignore</c>d like the icons: Godot never imports them) with an index
/// (<c>sound/index.json</c>); the files are read at run time with <c>AudioStreamOggVorbis.LoadFromFile</c> (or
/// <c>AudioStreamWav.LoadFromFile</c> for the one file the index marks <c>wav</c>, <c>AudioStreamMP3.LoadFromFile</c> for the one <c>mp3</c>), from where the index's <c>file</c> says
/// (<c>sound/...</c>, or <c>sound-nc/...</c>, D35). The root is <see cref="IconPaths.Root"/> (<c>ANOMALY_ICON_ROOT</c>) unless
/// <c>ANOMALY_SOUND_ROOT</c> names another. Loaded streams stay in a cache of about 64 MB (least recently used out first); a looping
/// play gets its own looping copy (sharing the data; a short loop whose ends fade, a decoded seamless copy, DV228), so the one-shot
/// stays a one-shot. A file that cannot be loaded is logged
/// once and plays nothing.
/// </summary>
public sealed class SoundLibrary
{
    public const long DefaultBudgetBytes = 64L * 1024 * 1024;

    private sealed class Loaded
    {
        public AudioStream OneShot;
        public AudioStream Loop;
    }

    private readonly SoundCache<Loaded> _cache;
    private readonly HashSet<string> _missing = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Loaded> _kept = new(StringComparer.Ordinal); // the start's preload (AssetPreload): outside the cache's budget, never evicted
    private readonly SoundIndex _index;

    public SoundLibrary(string root = null, long budgetBytes = DefaultBudgetBytes)
    {
        Root = root ?? DefaultRoot;
        _cache = new SoundCache<Loaded>(budgetBytes);
        string indexFile = Path.Combine(Root, "sound", "index.json");
        _index = SoundIndex.Load(indexFile, out string problem);
        if (_index == null)
            GD.Print($"[anomaly] audio: no sound index ({problem}); run tools/Anomaly.Tools.Sounds (tools/convert-tg.sh does): every sound is missing");
    }

    /// <summary><c>ANOMALY_SOUND_ROOT</c>, else the icons' root (the folder holding <c>sound/</c> and <c>sound-nc/</c>).</summary>
    public static string DefaultRoot
    {
        get
        {
            string overrideRoot = System.Environment.GetEnvironmentVariable("ANOMALY_SOUND_ROOT");
            return string.IsNullOrEmpty(overrideRoot) ? IconPaths.Root : overrideRoot;
        }
    }

    public string Root { get; }

    public bool HasIndex => _index != null;

    public int IndexCount => _index?.Count ?? 0;

    /// <summary>Distinct files that could not be loaded.</summary>
    public int MissingFiles => _missing.Count;

    public int LoadedFiles { get; private set; }

    public int CachedFiles => _cache.Count;

    public long CachedBytes => _cache.Bytes;

    /// <summary>The index's facts of a file (the mixer's sample rate and length).</summary>
    public SoundFileFacts? Facts(string path) => _index?.Facts(path);

    /// <summary>The stream of tg's path (a looping copy when <paramref name="repeat"/>), or null when it cannot be loaded.</summary>
    public AudioStream Get(string path, bool repeat)
    {
        if (string.IsNullOrEmpty(path) || _missing.Contains(path))
            return null;
        if (!_cache.TryGet(path, out Loaded loaded) && !_kept.TryGetValue(path, out loaded))
        {
            loaded = Load(path, out long bytes, out bool preloaded);
            if (loaded == null)
                return null;
            if (preloaded)
                _kept[path] = loaded;
            else
                _cache.Add(path, loaded, bytes);
        }
        if (!repeat)
            return loaded.OneShot;
        return loaded.Loop ??= LoopingCopy(path, loaded.OneShot);
    }

    private Loaded Load(string path, out long bytes, out bool preloaded)
    {
        bytes = 0;
        preloaded = false;
        string format = "ogg";
        string relative = path;
        if (_index != null && _index.TryGet(path, out SoundIndexEntry entry))
        {
            relative = entry.File;
            format = entry.Format;
            bytes = entry.Bytes;
        }
        else if (_index != null)
        {
            Miss(path, "not in the sound index");
            return null;
        }
        string file = Path.Combine(Root, relative);
        if (AssetPreload.TrySound(file, out AudioStream kept))
        {
            preloaded = true;
            LoadedFiles++;
            return new Loaded { OneShot = kept };
        }
        if (!File.Exists(file))
        {
            Miss(path, "no file " + file);
            return null;
        }
        AudioStream stream = null;
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        switch (format)
        {
            case "ogg":
                stream = LoadOgg(file);
                break;
            case "wav":
                stream = AudioStreamWav.LoadFromFile(file);
                break;
            case "mp3":
                stream = AudioStreamMP3.LoadFromFile(file);
                break;
            default:
                Miss(path, "the index's format " + format + " is not played");
                return null;
        }
        if (stream == null)
        {
            Miss(path, "Godot could not read " + file + " as " + format);
            return null;
        }
        AssetPreload.Miss("sound", path, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, blocked: true);
        if (bytes <= 0)
            bytes = new FileInfo(file).Length;
        LoadedFiles++;
        return new Loaded { OneShot = stream };
    }

    private static AudioStream LoopingCopy(string path, AudioStream oneShot)
    {
        switch (oneShot)
        {
            case AudioStreamOggVorbis ogg:
                if (SeamlessCopy(ogg) is AudioStreamWav seamless)
                    return seamless;
                // A shallow copy shares the packet data with the one-shot.
                var loop = (AudioStreamOggVorbis)ogg.Duplicate();
                loop.Loop = true;
                loop.LoopOffset = 0;
                return loop;
            case AudioStreamWav wav:
                var wloop = (AudioStreamWav)wav.Duplicate();
                wloop.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wloop.LoopBegin = 0;
                wloop.LoopEnd = (int)Math.Max(1, wav.GetLength() * wav.MixRate);
                return wloop;
        }
        GD.Print($"[anomaly] audio: {path} cannot loop; it plays once");
        return oneShot;
    }

    /// <summary>Loops up to this long are checked for fading ends (decoding is about a millisecond a second of sound).</summary>
    public const double SeamlessMaxSeconds = 60;

    private const int SeamPadFrames = 512;

    // DEVIATION(DV228): a short loop whose ends fade plays as a decoded copy with its tail crossfaded into its head.
    /// <summary>An Ogg Vorbis file read with its comments mended first (<see cref="VorbisComments"/>: Godot warns about each one without '=').</summary>
    public static AudioStreamOggVorbis LoadOgg(string file)
    {
        byte[] bytes = File.ReadAllBytes(file);
        VorbisComments.Repair(bytes);
        return AudioStreamOggVorbis.LoadFromBuffer(bytes);
    }

    private static AudioStreamWav SeamlessCopy(AudioStreamOggVorbis ogg)
    {
        double seconds = ogg.GetLength();
        if (seconds <= 0 || seconds > SeamlessMaxSeconds)
            return null;
        int rate = (int)AudioServer.GetMixRate(); // the playback mixes at the server's rate
        int total = (int)(seconds * rate);
        var frames = new float[2 * total];
        AudioStreamPlayback playback = ogg.InstantiatePlayback();
        playback.Start(0);
        for (int done = 0; done < total;)
        {
            Vector2[] chunk = playback.MixAudio(1f, Math.Min(4096, total - done));
            if (chunk.Length == 0)
                break;
            for (int i = 0; i < chunk.Length; i++)
            {
                frames[2 * (done + i)] = chunk[i].X;
                frames[2 * (done + i) + 1] = chunk[i].Y;
            }
            done += chunk.Length;
        }
        playback.Stop();
        float[] loop = LoopSeam.Seamless(frames, rate);
        if (loop == null)
            return null;
        // the loop's first frames again after its end: the resampler reads past the loop end, where silence would click
        int frameCount = loop.Length / 2, pad = Math.Min(SeamPadFrames, frameCount);
        var data = new byte[(loop.Length + 2 * pad) * 2];
        for (int i = 0; i < loop.Length + 2 * pad; i++)
        {
            short v = (short)Math.Clamp((int)MathF.Round(loop[i % loop.Length] * 32767f), short.MinValue, short.MaxValue);
            data[2 * i] = (byte)v;
            data[2 * i + 1] = (byte)(v >> 8);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            Stereo = true,
            MixRate = rate,
            Data = data,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopBegin = 0,
            LoopEnd = frameCount,
        };
    }

    private void Miss(string path, string why)
    {
        if (_missing.Add(path))
            GD.Print($"[anomaly] audio: {path}: {why}; it plays nothing");
    }
}
