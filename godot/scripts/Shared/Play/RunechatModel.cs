namespace Anomaly.Client.Shared;

// Owner: M9 slice A (DV160). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The client half of tg's /datum/chatmessage (code/datums/chatmessage.dm): finish_image_generation's stacking of the lines over one
// speaker, their exponential decay and their fades, and the qdel at the end of the line's life. The server sends the finished maptext
// (protocol version 38's Runechat); the view measures its height (tg's MeasureText at CHAT_MESSAGE_WIDTH) and draws the lines.

/// <summary>One runechat line: tg's <c>/datum/chatmessage</c> with its image's alpha and pixel_z animations.</summary>
public sealed class RunechatLine
{
    public long Target { get; init; }

    public string Maptext { get; init; }

    /// <summary>The measured height in map pixels (tg's <c>mheight</c>).</summary>
    public float Height { get; init; }

    /// <summary>tg's <c>approx_lines</c>.</summary>
    public float ApproxLines { get; init; }

    /// <summary>tg's <c>animate_start</c>, seconds.</summary>
    public double AnimateStart { get; init; }

    /// <summary>tg's <c>animate_lifespan</c>, seconds.</summary>
    public double AnimateLifespan { get; internal set; }

    /// <summary>When tg's qdel timer deletes it (lifespan plus the grace period), seconds.</summary>
    public double DeleteAt { get; init; }

    /// <summary>The layer order: a newer line is drawn over an older one.</summary>
    public long Order { get; init; }

    // The alpha animation: linear segments through these keys (time, alpha 0..255); before the first and after the last it holds.
    internal readonly List<(double T, float A)> AlphaKeys = new();

    // The pixel_z animation: from ZFrom at ZStart to ZTo over the spawn time. ZTo is the image's pixel_z as DM reads it.
    internal float ZFrom, ZTo;
    internal double ZStart;

    /// <summary>The line's alpha, 0..1.</summary>
    public float Alpha(double now)
    {
        if (AlphaKeys.Count == 0)
            return 0f;
        if (now <= AlphaKeys[0].T)
            return AlphaKeys[0].A / 255f;
        for (int i = 1; i < AlphaKeys.Count; i++)
        {
            (double t1, float a1) = AlphaKeys[i];
            if (now <= t1)
            {
                (double t0, float a0) = AlphaKeys[i - 1];
                double f = t1 > t0 ? (now - t0) / (t1 - t0) : 1;
                return (float)(a0 + (a1 - a0) * f) / 255f;
            }
        }
        return AlphaKeys[^1].A / 255f;
    }

    /// <summary>The line's height over the speaker in map pixels (tg's <c>pixel_z</c>, animated).</summary>
    public float PixelZ(double now)
    {
        double f = RunechatModel.SpawnTime > 0 ? Math.Clamp((now - ZStart) / RunechatModel.SpawnTime, 0, 1) : 1;
        return (float)(ZFrom + (ZTo - ZFrom) * f);
    }

    /// <summary>tg's <c>get_current_alpha</c>.</summary>
    internal float GetCurrentAlpha(double timeSpent)
    {
        if (timeSpent < RunechatModel.SpawnTime)
            return (float)(timeSpent / RunechatModel.SpawnTime * 255);
        double timeBeforeFade = AnimateLifespan - RunechatModel.EolFade;
        if (timeSpent <= timeBeforeFade)
            return 255f;
        return (float)((1 - (timeSpent - timeBeforeFade) / RunechatModel.EolFade) * 255);
    }

    // animate(m.message, pixel_z = z, time = CHAT_MESSAGE_SPAWN_TIME, flags = ANIMATION_PARALLEL)
    internal void AnimateZ(float z, double now)
    {
        ZFrom = PixelZ(now);
        ZTo = z;
        ZStart = now;
    }
}

/// <summary>The runechat lines one client sees, by speaker (tg's <c>client.seen_messages</c>).</summary>
public sealed class RunechatModel
{
    /// <summary>tg's <c>CHAT_MESSAGE_SPAWN_TIME</c>, seconds.</summary>
    public const double SpawnTime = 0.2;

    /// <summary>tg's <c>CHAT_MESSAGE_EOL_FADE</c>, seconds.</summary>
    public const double EolFade = 0.7;

    /// <summary>tg's <c>CHAT_MESSAGE_GRACE_PERIOD</c>, seconds.</summary>
    public const double GracePeriod = 0.2;

    /// <summary>tg's <c>CHAT_MESSAGE_EXP_DECAY</c>.</summary>
    public const double ExpDecay = 0.7;

    /// <summary>tg's <c>CHAT_MESSAGE_HEIGHT_DECAY</c>.</summary>
    public const double HeightDecay = 0.9;

    /// <summary>tg's <c>CHAT_MESSAGE_APPROX_LHEIGHT</c>, map pixels.</summary>
    public const float ApproxLineHeight = 11f;

    /// <summary>tg's <c>CHAT_MESSAGE_WIDTH</c>, map pixels.</summary>
    public const int Width = 112;

    /// <summary>tg's <c>CHAT_MESSAGE_ICON_SIZE</c>, map pixels.</summary>
    public const int IconSize = 9;

    /// <summary>The speaker's <c>maptext_height</c> (an atom's default, 32), where a new line starts.</summary>
    public const float StartingHeight = 32f;

    private readonly Dictionary<long, List<RunechatLine>> _seen = new();
    private long _order;

    /// <summary>The lines by speaker, oldest first.</summary>
    public IReadOnlyDictionary<long, List<RunechatLine>> Seen => _seen;

    /// <summary>
    /// tg's <c>finish_image_generation</c>: a new line over <paramref name="target"/> lasting <paramref name="lifespanDeciseconds"/>; the older lines over
    /// it move up out of its way and their lives are cut by the exponential decay. <paramref name="height"/> is the measured height in map pixels.
    /// </summary>
    public RunechatLine Add(long target, string maptext, int lifespanDeciseconds, float height, double now)
    {
        double lifespan = lifespanDeciseconds / 10.0;
        float approxLines = Math.Max(1f, height / ApproxLineHeight);
        float startingHeight = StartingHeight;
        if (_seen.TryGetValue(target, out List<RunechatLine> lines))
        {
            int idx = 1;
            float combinedHeight = approxLines;
            foreach (RunechatLine m in lines)
            {
                combinedHeight += m.ApproxLines;
                double timeSpent = now - m.AnimateStart;
                double timeBeforeFade = m.AnimateLifespan - EolFade;
                if (timeSpent >= timeBeforeFade)
                {
                    Rise(m, height, startingHeight, now);
                    continue;
                }

                double remaining = timeBeforeFade * Math.Pow(ExpDecay, idx++) * Math.Pow(HeightDecay, combinedHeight);
                // Ensure we don't accidentially spike alpha up or something silly like that
                float alpha = m.GetCurrentAlpha(timeSpent);
                m.AlphaKeys.Clear();
                m.AlphaKeys.Add((now, alpha));
                if (remaining > 0)
                {
                    double t = now;
                    if (timeSpent < SpawnTime)
                    {
                        t += SpawnTime - timeSpent; // We haven't even had the time to fade in yet!
                        m.AlphaKeys.Add((t, 255f));
                    }
                    t += remaining; // Stay faded in for a while, then
                    m.AlphaKeys.Add((t, 255f));
                    m.AlphaKeys.Add((t + EolFade, 0f)); // Fade out
                    m.AnimateLifespan = remaining + EolFade;
                }
                else
                    m.AlphaKeys.Add((now + EolFade, 0f)); // Your time has come my son
                Rise(m, height, startingHeight, now);
            }
        }
        else
            _seen[target] = lines = new List<RunechatLine>();

        var line = new RunechatLine
        {
            Target = target,
            Maptext = maptext,
            Height = height,
            ApproxLines = approxLines,
            AnimateStart = now,
            AnimateLifespan = lifespan,
            DeleteAt = now + lifespan + GracePeriod,
            Order = ++_order,
        };
        line.ZFrom = line.ZTo = startingHeight;
        line.ZStart = now;
        // Fade in, stay faded in, fade out
        line.AlphaKeys.Add((now, 0f));
        line.AlphaKeys.Add((now + SpawnTime, 255f));
        line.AlphaKeys.Add((now + lifespan - EolFade, 255f));
        line.AlphaKeys.Add((now + lifespan, 0f));
        lines.Add(line);
        return line;
    }

    // The pixel_z branch of finish_image_generation, for a line that is already fading and for one that is not.
    private static void Rise(RunechatLine m, float mheight, float startingHeight, double now)
    {
        if (m.ZTo < startingHeight)
        {
            float maxHeight = m.ZTo + m.ApproxLines * ApproxLineHeight - startingHeight;
            if (maxHeight > 0)
                m.AnimateZ(m.ZTo + maxHeight, now);
        }
        else if (mheight + startingHeight >= m.ZTo)
            m.AnimateZ(m.ZTo + mheight, now);
    }

    /// <summary>tg's qdel timers: the lines past their life (and their grace period) are dropped.</summary>
    public void Prune(double now)
    {
        List<long> empty = null;
        foreach ((long target, List<RunechatLine> lines) in _seen)
        {
            lines.RemoveAll(l => now >= l.DeleteAt);
            if (lines.Count == 0)
                (empty ??= new List<long>()).Add(target);
        }
        if (empty != null)
        {
            foreach (long target in empty)
                _seen.Remove(target);
        }
    }

    /// <summary>Forgets everything (a reconnect).</summary>
    public void Clear() => _seen.Clear();

    /// <summary>
    /// Which of tg's chat icons goes in front of a line, from the classes generate_image gave its span (<c>&lt;span class='center ...'&gt;</c>):
    /// <c>virtual-speaker</c> is the radio, <c>emote</c> the emote; null for none.
    /// </summary>
    public static string IconStateOf(string maptext)
    {
        if (string.IsNullOrEmpty(maptext))
            return null;
        System.Text.RegularExpressions.Match match = ClassesRegex.Match(maptext);
        if (!match.Success)
            return null;
        string[] classes = match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (Array.IndexOf(classes, "virtual-speaker") >= 0)
            return "radio";
        if (Array.IndexOf(classes, "emote") >= 0)
            return "emote";
        return null;
    }

    private static readonly System.Text.RegularExpressions.Regex ClassesRegex = new("<span class='center([^']*)'>", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
