using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Anomaly.Client.Shared.Speech;

// D308: tg's TTS server runs each line through ffmpeg: the speaker's voice_filter and the line's tts_filter (a filter chain: machines,
// masks, the megaphone), the "silicon" special filter (two convolution reverbs, tools/tts/tts-api/tts-api.py) and the radio's clicks.
// The port's clients synthesize the lines (DV262), so they apply the same chains here. Engine-free: no Godot API here (it is compiled
// into tests/Anomaly.Server.Tests too).

/// <summary>A filter chain parsed for one line: the pitch and tempo it asks of the synthesis, then the sample filters.</summary>
public sealed class VoicePlan
{
    /// <summary>rubberband's pitch (and asetrate's), times the speaker's pitch preference.</summary>
    public float Pitch { get; set; } = 1f;

    /// <summary>atempo's tempo: above 1 the line is said faster.</summary>
    public float Tempo { get; set; } = 1f;

    public List<(string Name, Dictionary<string, string> Options, List<string> Positional)> Stages { get; } = new();

    /// <summary>
    /// The filter chain tg passes (filters joined by commas, each <c>name=key=value:key=value</c> or positional values). A filter graph
    /// (labels in brackets, several chains) plays the line without it.
    /// </summary>
    // shortcut: ffmpeg's filter graphs (the security hailer, a few tongues and fish organs: split and mixed pitch-shifted copies, noise)
    // are not rebuilt, so those voices sound plain; rebuild them when one of those speakers is common.
    public static VoicePlan Parse(string filter)
    {
        var plan = new VoicePlan();
        if (string.IsNullOrWhiteSpace(filter) || filter.Contains('[') || filter.Contains(';'))
            return plan;
        foreach (string part in filter.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            string name = eq < 0 ? part : part[..eq];
            var options = new Dictionary<string, string>();
            var positional = new List<string>();
            if (eq >= 0)
            {
                foreach (string arg in part[(eq + 1)..].Split(':'))
                {
                    int kv = arg.IndexOf('=');
                    if (kv < 0)
                        positional.Add(arg);
                    else
                        options[arg[..kv]] = arg[(kv + 1)..];
                }
            }
            switch (name)
            {
                case "rubberband":
                    plan.Pitch *= Number(options.GetValueOrDefault("pitch"), 1f);
                    break;
                case "atempo":
                    plan.Tempo *= Fraction(positional.FirstOrDefault() ?? options.GetValueOrDefault("tempo"), 1f);
                    break;
                default:
                    plan.Stages.Add((name, options, positional));
                    break;
            }
        }
        return plan;
    }

    private static float Fraction(string text, float fallback)
    {
        if (text != null && text.Contains('/'))
        {
            string[] p = text.Split('/');
            return Number(p[0], fallback) / Number(p[1], 1f);
        }
        return Number(text, fallback);
    }

    internal static float Number(string text, float fallback)
    {
        if (string.IsNullOrEmpty(text))
            return fallback;
        bool db = text.EndsWith("dB", System.StringComparison.OrdinalIgnoreCase);
        string number = db ? text[..^2] : text;
        if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return fallback;
        return db ? MathF.Pow(10f, value / 20f) : value;
    }
}

/// <summary>The sample side of tg's TTS server's ffmpeg filters, the silicon reverbs and the radio's clicks.</summary>
public static class VoiceEffects
{
    /// <summary>Runs <paramref name="plan"/>'s filters over a mono line in place order; unknown filters pass the sound unchanged.</summary>
    public static float[] Apply(float[] samples, int rate, VoicePlan plan)
    {
        foreach ((string name, Dictionary<string, string> o, List<string> p) in plan.Stages)
        {
            string Arg(string key, int index) => o.TryGetValue(key, out string v) ? v : index >= 0 && index < p.Count ? p[index] : null;
            float F(string key, int index, float fallback) => VoicePlan.Number(Arg(key, index), fallback);
            switch (name)
            {
                case "highpass":
                    Biquad(samples, BiquadCoefficients.HighPass(rate, F("f", 0, 3000), F("w", 3, 0.707f)));
                    break;
                case "lowpass":
                    Biquad(samples, BiquadCoefficients.LowPass(rate, F("f", 0, 500), F("w", 3, 0.707f)));
                    break;
                case "equalizer":
                    Biquad(samples, BiquadCoefficients.Peaking(rate, F("f", 0, 1000), F("w", 2, 1f), F("g", 3, 0f)));
                    break;
                case "lowshelf":
                    Biquad(samples, BiquadCoefficients.LowShelf(rate, F("f", 1, 100), F("g", 0, 0f)));
                    break;
                case "volume":
                    Scale(samples, F("volume", 0, 1f));
                    break;
                case "alimiter":
                    Limiter(samples, rate, F("level_in", 0, 1f), F("level_out", 1, 1f), F("limit", 2, 1f), F("attack", 3, 5f), F("release", 4, 50f));
                    break;
                case "acompressor":
                    Compressor(samples, rate, F("threshold", 2, 0.125f), F("ratio", 3, 2f), F("attack", 4, 20f), F("release", 5, 250f), F("makeup", 6, 1f));
                    break;
                case "aecho":
                    samples = Echo(samples, rate, F("in_gain", 0, 0.6f), F("out_gain", 1, 0.3f), Arg("delays", 2) ?? "1000", Arg("decays", 3) ?? "0.5");
                    break;
                case "acrusher":
                    Crusher(samples, F("level_in", -1, 1f), F("level_out", -1, 1f), F("bits", -1, 8f), F("mix", -1, 0.5f), (int)F("samples", -1, 1f));
                    break;
                case "crystalizer":
                    Crystalizer(samples, F("i", 0, 2f));
                    break;
                case "acontrast":
                    Contrast(samples, F("contrast", 0, 33f));
                    break;
                // aemphasis (a record's emphasis curve), aresample, aformat, apad: no audible part of their own here
            }
        }
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Math.Clamp(samples[i], -1f, 1f);
        return samples;
    }

    /// <summary>The line read faster by <paramref name="factor"/> (its pitch up as much): a linear resample.</summary>
    public static float[] Resample(float[] samples, float factor)
    {
        if (factor <= 0f || MathF.Abs(factor - 1f) < 1e-4f || samples.Length == 0)
            return samples;
        int length = Math.Max(1, (int)(samples.Length / factor));
        var result = new float[length];
        for (int i = 0; i < length; i++)
        {
            float at = i * factor;
            int a = Math.Min((int)at, samples.Length - 1);
            int b = Math.Min(a + 1, samples.Length - 1);
            result[i] = samples[a] + (samples[b] - samples[a]) * (at - a);
        }
        return result;
    }

    /// <summary>
    /// tg's "silicon" special filter (tts-api.py): the line padded with 2 s, mixed 8:1 with itself through the synth impulse (afir dry 10,
    /// wet 10), that mixed 10:1 with itself through the room impulse, two equalizer cuts (7710 Hz -6 dB, 33 Hz -10 dB) and a limiter at 0.5.
    /// The impulses are tg's (tools/tts/tts-api), at the line's rate.
    /// </summary>
    public static float[] Silicon(float[] samples, int rate, float[] synthImpulse, float[] roomImpulse)
    {
        var padded = new float[samples.Length + 2 * rate];
        Array.Copy(samples, padded, samples.Length);
        float[] reverb1 = Convolve(padded, Normalized(synthImpulse));
        var mix1 = new float[padded.Length];
        for (int i = 0; i < padded.Length; i++)
            mix1[i] = (8f * padded[i] + 100f * reverb1[i]) / 9f;
        float[] reverb2 = Convolve(mix1, Normalized(roomImpulse));
        var mix2 = new float[padded.Length];
        for (int i = 0; i < padded.Length; i++)
            mix2[i] = (10f * mix1[i] + reverb2[i]) / 11f;
        Biquad(mix2, BiquadCoefficients.Peaking(rate, 7710f, 0.6f, -6f));
        Biquad(mix2, BiquadCoefficients.Peaking(rate, 33f, 0.44f, -10f));
        Limiter(mix2, rate, 1f, 1f, 0.5f, 5f, 20f);
        return mix2;
    }

    /// <summary>
    /// D309 (the owner, 2026-11-03: radio lines sounded like speech in the room): a walkie-talkie's narrow band (two high-passes at 450 Hz,
    /// two low-passes at 2.8 kHz), hard compression, saturation and a presence bump. tg's TTS server adds only the clicks to a radio line.
    /// </summary>
    public const string RadioChain = "highpass=f=450,highpass=f=450,lowpass=f=2800,lowpass=f=2800,"
        + "acompressor=threshold=0.04:ratio=6:attack=3:release=80:makeup=5,acontrast=50,equalizer=f=1800:w=1:g=5,volume=0.6";

    /// <summary>D309: <see cref="RadioChain"/> over the line, with a band-limited hiss under it.</summary>
    public static float[] Radio(float[] samples, int rate, Random random)
    {
        float[] voice = Apply(samples, rate, VoicePlan.Parse(RadioChain));
        var hiss = new float[voice.Length];
        for (int i = 0; i < hiss.Length; i++)
            hiss[i] = (float)(random.NextDouble() * 2.0 - 1.0) * 0.05f;
        Biquad(hiss, BiquadCoefficients.HighPass(rate, 1200, 0.707));
        Biquad(hiss, BiquadCoefficients.LowPass(rate, 4000, 0.707));
        for (int i = 0; i < voice.Length; i++)
            voice[i] = Math.Clamp(voice[i] + hiss[i], -1f, 1f);
        return voice;
    }

    /// <summary>
    /// D312 (the owner, 2026-11-03: a whisper sounded like plain speech): the voice without its pitch, as breath through the same mouth. Each
    /// frame keeps its spectral envelope (the magnitudes averaged over 300 Hz, which hides the voice's harmonics) under random phases; the
    /// lows under 500 Hz are cut and the line keeps its loudness (the server's volume makes it quieter).
    /// </summary>
    public static float[] Whisper(float[] samples, int rate, Random random)
    {
        const int n = 1024, hop = n / 4;
        var window = new float[n];
        for (int i = 0; i < n; i++)
            window[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n));
        int width = Math.Max(1, 150 * n / rate);
        var output = new float[samples.Length];
        var weight = new float[samples.Length];
        var frame = new System.Numerics.Complex[n];
        var magnitude = new double[n / 2 + 1];
        for (int start = hop - n; start < samples.Length; start += hop)
        {
            for (int i = 0; i < n; i++)
                frame[i] = start + i >= 0 && start + i < samples.Length ? samples[start + i] * window[i] : 0;
            Fft(frame, false);
            for (int k = 0; k <= n / 2; k++)
                magnitude[k] = frame[k].Magnitude;
            for (int k = 0; k <= n / 2; k++)
            {
                double sum = 0;
                int from = Math.Max(0, k - width), to = Math.Min(n / 2, k + width);
                for (int j = from; j <= to; j++)
                    sum += magnitude[j];
                double phase = k == 0 || k == n / 2 ? 0 : random.NextDouble() * 2 * Math.PI;
                frame[k] = System.Numerics.Complex.FromPolarCoordinates(sum / (to - from + 1), phase);
                if (k > 0 && k < n / 2)
                    frame[n - k] = System.Numerics.Complex.Conjugate(frame[k]);
            }
            Fft(frame, true);
            for (int i = 0; i < n; i++)
            {
                int at = start + i;
                if (at < 0 || at >= samples.Length)
                    continue;
                output[at] += (float)(frame[i].Real / n) * window[i];
                weight[at] += window[i] * window[i];
            }
        }
        for (int i = 0; i < output.Length; i++)
            output[i] = weight[i] > 1e-3f ? output[i] / weight[i] : 0f;
        Biquad(output, BiquadCoefficients.HighPass(rate, 500, 0.707));
        float before = Rms(samples), after = Rms(output);
        if (after > 0f)
            Scale(output, before / after);
        for (int i = 0; i < output.Length; i++)
            output[i] = Math.Clamp(output[i], -1f, 1f);
        return output;
    }

    /// <summary>
    /// D312 (the owner: a shout sounded like plain speech): a shouted line is said 3 semitones higher and a little faster, its lows thinned,
    /// its presence (2.2 kHz) raised and its loudness pressed hard, as a strained voice.
    /// </summary>
    public const string YellChain = "highpass=f=180,equalizer=f=2200:w=1:g=6,acompressor=threshold=0.08:ratio=5:attack=2:release=60:makeup=4,acontrast=40";

    public const float YellPitch = 1.189207f; // 2^(3/12)
    public const float YellTempo = 1.08f;

    private static float Rms(float[] x)
    {
        double sum = 0;
        foreach (float v in x)
            sum += v * v;
        return x.Length == 0 ? 0f : (float)Math.Sqrt(sum / x.Length);
    }

    /// <summary>tg's radio clicks around a line (tts-api.py: a random start and end click).</summary>
    public static float[] WithClicks(float[] samples, float[] start, float[] end)
    {
        var result = new float[(start?.Length ?? 0) + samples.Length + (end?.Length ?? 0)];
        int at = 0;
        if (start != null)
        {
            Array.Copy(start, 0, result, at, start.Length);
            at += start.Length;
        }
        Array.Copy(samples, 0, result, at, samples.Length);
        if (end != null)
            Array.Copy(end, 0, result, at + samples.Length, end.Length);
        return result;
    }

    /// <summary>A PCM WAV file (8, 16 or 32 bits) as mono samples at <paramref name="rate"/>; null when it is no such file.</summary>
    public static float[] ReadWav(byte[] bytes, int rate)
    {
        if (bytes == null || bytes.Length < 44 || bytes[0] != 'R' || bytes[1] != 'I' || bytes[8] != 'W')
            return null;
        int channels = 1, sourceRate = 0, bits = 16, at = 12;
        while (at + 8 <= bytes.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(bytes, at, 4);
            int size = BitConverter.ToInt32(bytes, at + 4);
            int body = at + 8;
            if (id == "fmt ")
            {
                channels = BitConverter.ToInt16(bytes, body + 2);
                sourceRate = BitConverter.ToInt32(bytes, body + 4);
                bits = BitConverter.ToInt16(bytes, body + 14);
            }
            else if (id == "data" && sourceRate > 0 && channels > 0 && bits is 8 or 16 or 32)
            {
                int width = bits / 8;
                int frames = Math.Min(size, bytes.Length - body) / (width * channels);
                var mono = new float[frames];
                for (int f = 0; f < frames; f++)
                {
                    float sum = 0f;
                    for (int c = 0; c < channels; c++)
                    {
                        int o = body + (f * channels + c) * width;
                        sum += bits switch
                        {
                            8 => (bytes[o] - 128) / 128f,
                            16 => BitConverter.ToInt16(bytes, o) / 32768f,
                            _ => BitConverter.ToInt32(bytes, o) / 2147483648f,
                        };
                    }
                    mono[f] = sum / channels;
                }
                return Resample(mono, sourceRate / (float)rate);
            }
            at = body + size + (size & 1);
        }
        return null;
    }

    private static void Scale(float[] x, float gain)
    {
        for (int i = 0; i < x.Length; i++)
            x[i] *= gain;
    }

    private static void Biquad(float[] x, (double B0, double B1, double B2, double A1, double A2) c)
    {
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double y = c.B0 * x[i] + c.B1 * x1 + c.B2 * x2 - c.A1 * y1 - c.A2 * y2;
            x2 = x1;
            x1 = x[i];
            y2 = y1;
            y1 = y;
            x[i] = (float)y;
        }
    }

    // ffmpeg's alimiter: the input gain, a gain that falls at once to keep the peaks under the limit and recovers over the release
    private static void Limiter(float[] x, int rate, float levelIn, float levelOut, float limit, float attackMs, float releaseMs)
    {
        float recover = 1f - MathF.Exp(-1f / (Math.Max(releaseMs, 1f) * 0.001f * rate));
        float gain = 1f;
        for (int i = 0; i < x.Length; i++)
        {
            float s = x[i] * levelIn;
            float peak = MathF.Abs(s);
            float needed = peak > limit ? limit / peak : 1f;
            gain = needed < gain ? needed : gain + (1f - gain) * recover;
            x[i] = s * gain * levelOut;
        }
    }

    // ffmpeg's acompressor: a peak envelope, and above the threshold the level divided by the ratio in the log domain, then the makeup
    private static void Compressor(float[] x, int rate, float threshold, float ratio, float attackMs, float releaseMs, float makeup)
    {
        float attack = 1f - MathF.Exp(-1f / (Math.Max(attackMs, 0.01f) * 0.001f * rate));
        float release = 1f - MathF.Exp(-1f / (Math.Max(releaseMs, 0.01f) * 0.001f * rate));
        float envelope = 0f;
        for (int i = 0; i < x.Length; i++)
        {
            float level = MathF.Abs(x[i]);
            envelope += (level - envelope) * (level > envelope ? attack : release);
            float gain = envelope > threshold ? threshold * MathF.Pow(envelope / threshold, 1f / Math.Max(ratio, 1f)) / envelope : 1f;
            x[i] *= gain * makeup;
        }
    }

    // ffmpeg's aecho: the input and its delayed copies, each delays|decays pair one copy, then the output gain
    private static float[] Echo(float[] x, int rate, float inGain, float outGain, string delays, string decays)
    {
        float[] d = delays.Split('|').Select(v => VoicePlan.Number(v, 0f)).ToArray();
        float[] k = decays.Split('|').Select(v => VoicePlan.Number(v, 0f)).ToArray();
        int longest = (int)(d.DefaultIfEmpty(0f).Max() * 0.001f * rate);
        var y = new float[x.Length + longest];
        for (int i = 0; i < y.Length; i++)
        {
            float s = i < x.Length ? x[i] * inGain : 0f;
            for (int e = 0; e < Math.Min(d.Length, k.Length); e++)
            {
                int j = i - (int)(d[e] * 0.001f * rate);
                if (j >= 0 && j < x.Length)
                    s += x[j] * k[e];
            }
            y[i] = s * outGain;
        }
        return y;
    }

    // ffmpeg's acrusher (linear mode): fewer bits and each value held for <samples> samples, mixed with the dry sound
    private static void Crusher(float[] x, float levelIn, float levelOut, float bits, float mix, int hold)
    {
        float steps = MathF.Pow(2f, Math.Clamp(bits, 1f, 64f)) / 2f;
        float held = 0f;
        hold = Math.Max(1, hold);
        for (int i = 0; i < x.Length; i++)
        {
            float s = x[i] * levelIn;
            if (i % hold == 0)
                held = MathF.Round(s * steps) / steps;
            x[i] = (s * (1f - mix) + held * mix) * levelOut;
        }
    }

    // ffmpeg's crystalizer: each sample pushed away from the one before by the intensity
    private static void Crystalizer(float[] x, float intensity)
    {
        float previous = 0f;
        for (int i = 0; i < x.Length; i++)
        {
            float s = x[i];
            x[i] = s + (s - previous) * intensity;
            previous = s;
        }
    }

    // ffmpeg's acontrast: d = x * pi/2; y = sin(d + c * sin(4d)), c = contrast / 750
    private static void Contrast(float[] x, float contrast)
    {
        float c = contrast / 750f;
        for (int i = 0; i < x.Length; i++)
        {
            float d = Math.Clamp(x[i], -1f, 1f) * MathF.PI / 2f;
            x[i] = MathF.Sin(d + c * MathF.Sin(d * 4f));
        }
    }

    // ffmpeg's afir irnorm=1: the impulse scaled to a sum of magnitudes of 1
    private static float[] Normalized(float[] impulse)
    {
        float sum = impulse.Sum(MathF.Abs);
        return sum <= 0f ? impulse : impulse.Select(v => v / sum).ToArray();
    }

    /// <summary>The convolution of <paramref name="x"/> with <paramref name="h"/>, as long as <paramref name="x"/> (through an FFT).</summary>
    public static float[] Convolve(float[] x, float[] h)
    {
        int n = 1;
        while (n < x.Length + h.Length - 1)
            n <<= 1;
        var a = new System.Numerics.Complex[n];
        var b = new System.Numerics.Complex[n];
        for (int i = 0; i < x.Length; i++)
            a[i] = x[i];
        for (int i = 0; i < h.Length; i++)
            b[i] = h[i];
        Fft(a, false);
        Fft(b, false);
        for (int i = 0; i < n; i++)
            a[i] *= b[i];
        Fft(a, true);
        var y = new float[x.Length];
        for (int i = 0; i < y.Length; i++)
            y[i] = (float)(a[i].Real / n);
        return y;
    }

    // An iterative radix-2 FFT in place (n a power of two); the inverse is unscaled.
    private static void Fft(System.Numerics.Complex[] a, bool inverse)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;
            if (i < j)
                (a[i], a[j]) = (a[j], a[i]);
        }
        for (int length = 2; length <= n; length <<= 1)
        {
            double angle = 2 * Math.PI / length * (inverse ? 1 : -1);
            var step = new System.Numerics.Complex(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += length)
            {
                System.Numerics.Complex w = 1;
                for (int k = 0; k < length / 2; k++)
                {
                    System.Numerics.Complex u = a[i + k], v = a[i + k + length / 2] * w;
                    a[i + k] = u + v;
                    a[i + k + length / 2] = u - v;
                    w *= step;
                }
            }
        }
    }
}

/// <summary>RBJ's audio EQ cookbook biquads, as ffmpeg's highpass, lowpass, equalizer and lowshelf (width type q) build them.</summary>
public static class BiquadCoefficients
{
    public static (double, double, double, double, double) HighPass(int rate, double f, double q)
    {
        (double cos, double alpha) = Prewarp(rate, f, q);
        return Normalize((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    public static (double, double, double, double, double) LowPass(int rate, double f, double q)
    {
        (double cos, double alpha) = Prewarp(rate, f, q);
        return Normalize((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    public static (double, double, double, double, double) Peaking(int rate, double f, double q, double gainDb)
    {
        double a = Math.Pow(10, gainDb / 40);
        (double cos, double alpha) = Prewarp(rate, f, q);
        return Normalize(1 + alpha * a, -2 * cos, 1 - alpha * a, 1 + alpha / a, -2 * cos, 1 - alpha / a);
    }

    public static (double, double, double, double, double) LowShelf(int rate, double f, double gainDb)
    {
        double a = Math.Pow(10, gainDb / 40);
        (double cos, double alpha) = Prewarp(rate, f, 0.707);
        double s = 2 * Math.Sqrt(a) * alpha;
        return Normalize(a * ((a + 1) - (a - 1) * cos + s), 2 * a * ((a - 1) - (a + 1) * cos), a * ((a + 1) - (a - 1) * cos - s),
            (a + 1) + (a - 1) * cos + s, -2 * ((a - 1) + (a + 1) * cos), (a + 1) + (a - 1) * cos - s);
    }

    private static (double Cos, double Alpha) Prewarp(int rate, double f, double q)
    {
        double w0 = 2 * Math.PI * Math.Clamp(f, 1, rate / 2.0 - 1) / rate;
        return (Math.Cos(w0), Math.Sin(w0) / (2 * Math.Max(q, 0.01)));
    }

    private static (double, double, double, double, double) Normalize(double b0, double b1, double b2, double a0, double a1, double a2) =>
        (b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
}
