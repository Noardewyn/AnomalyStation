using System.Collections.Concurrent;
using System.IO;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Speech;

namespace Anomaly.Client.Speech;

/// <summary>
/// D308: tg's TTS server's own sounds (tools/tts/tts-api: the radio's clicks, the silicon voice's impulses), which the sound tool copies to
/// the asset root's tts/ folder, as mono samples at a line's rate. A missing file is null: the line plays without it.
/// </summary>
public static class SpeechSounds
{
    private static readonly ConcurrentDictionary<(string Name, int Rate), float[]> Cache = new();

    public static float[] Get(string name, int rate) => Cache.GetOrAdd((name, rate), key =>
    {
        string path = Path.Combine(IconPaths.Root, "tts", key.Name + ".wav");
        return File.Exists(path) ? VoiceEffects.ReadWav(File.ReadAllBytes(path), key.Rate) : null;
    });
}
