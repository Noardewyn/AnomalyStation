namespace Anomaly.Client.Shared.Audio;

/// <summary>
/// DV228: a looping file whose ends fade (tg's tunnel buzz, <c>source_corridor2.ogg</c>, drops 14 dB in its last quarter second)
/// dips audibly at every turn of the loop, as it does in BYOND. Such a loop plays as a seamless copy: its last
/// <see cref="CrossfadeSeconds"/> crossfaded (equal power) into its first, the copy that much shorter. A file whose ends hold the
/// level of its middle plays as it is.
/// </summary>
public static class LoopSeam
{
    public const float WindowSeconds = 0.25f;
    public const float CrossfadeSeconds = 0.5f;

    /// <summary>An end window this many decibels under the middle third's mean is a fade.</summary>
    public const float FadeDb = 6f;

    /// <summary>The seamless loop of interleaved stereo <paramref name="frames"/> at <paramref name="rate"/>, or null when its ends do not fade or it is too short to crossfade.</summary>
    public static float[] Seamless(float[] frames, int rate)
    {
        int n = frames.Length / 2;
        int window = Math.Max(1, (int)(rate * WindowSeconds));
        int fade = (int)(rate * CrossfadeSeconds);
        if (n < 4 * fade || n < 6 * window)
            return null;
        float body = 0;
        int windows = 0;
        for (int start = n / 3; start + window <= 2 * n / 3; start += window, windows++)
            body += Db(frames, start, window);
        body /= Math.Max(1, windows);
        float edge = Math.Min(Db(frames, 0, window), Db(frames, n - window, window));
        if (edge >= body - FadeDb)
            return null;

        int length = n - fade;
        var loop = new float[2 * length];
        Array.Copy(frames, loop, 2 * length);
        for (int i = 0; i < fade; i++)
        {
            float t = (i + 0.5f) / fade;
            float headGain = MathF.Sin(t * MathF.PI / 2), tailGain = MathF.Cos(t * MathF.PI / 2);
            for (int c = 0; c < 2; c++)
                loop[2 * i + c] = frames[2 * i + c] * headGain + frames[2 * (length + i) + c] * tailGain;
        }
        return loop;
    }

    private static float Db(float[] frames, int start, int count)
    {
        double sum = 0;
        for (int i = 2 * start; i < 2 * (start + count); i++)
            sum += frames[i] * frames[i];
        return 10f * MathF.Log10((float)(sum / (2.0 * count)) + 1e-12f);
    }
}
