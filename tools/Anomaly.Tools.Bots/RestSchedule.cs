namespace Anomaly.Tools.Bots;

/// <summary>The timings of <see cref="RestSchedule"/>, in seconds of the bot's own clock (20 ticks each).</summary>
public sealed class RestSettings
{
    /// <summary>The fewest seconds standing between getting up and lying down again.</summary>
    public double MinUpSeconds { get; init; } = 5;

    /// <summary>The most seconds standing.</summary>
    public double MaxUpSeconds { get; init; } = 15;

    /// <summary>The fewest seconds lying.</summary>
    public double MinDownSeconds { get; init; } = 3;

    /// <summary>The most seconds lying.</summary>
    public double MaxDownSeconds { get; init; } = 6;

    /// <summary>Ticks a Rest may take to show as lying in the replicated posture before the bot calls it refused (a floored body, a gate): 2 s.</summary>
    public int ConfirmTicks { get; init; } = 40;
}

/// <summary>What the rest schedule wants this tick.</summary>
/// <param name="SendRest">True when the bot should send <c>ActionKind.Rest</c> now (to lie down or to get up).</param>
/// <param name="MayMove">False while the bot lies: it cannot move and sends no movement.</param>
public readonly record struct RestStep(bool SendRest, bool MayMove);

/// <summary>
/// The rest behaviour's timer (M2 scope 2f): every 5 to 15 s of the bot's clock it sends Rest to lie down, stays 3 to 6 s
/// without asking to move, and sends Rest again to get up. It reads the replicated posture when it knows it: a Rest that
/// did not make the body lie (tg refuses it while floored) is dropped instead of answered by a second Rest that would lie
/// the body down, and a body that is already lying (a shove knocked it down) is not asked to rest.
/// </summary>
public sealed class RestSchedule
{
    private readonly BotRandom _random;
    private readonly RestSettings _settings;
    private bool _down;
    private bool _started;
    private bool _confirmed;
    private long _nextAt;
    private long _downSince;
    private long _getUpAt;

    public RestSchedule(BotRandom random, RestSettings settings = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _settings = settings ?? new RestSettings();
    }

    /// <summary>True while the bot believes it lies.</summary>
    public bool Down => _down;

    /// <summary>Times it asked to lie down.</summary>
    public int LieDowns { get; private set; }

    /// <summary>Times it asked to get up.</summary>
    public int GetUps { get; private set; }

    /// <summary>Times a Rest did not make the body lie.</summary>
    public int Refused { get; private set; }

    /// <summary>Forgets the timer (a new connection).</summary>
    public void Reset()
    {
        _down = false;
        _started = false;
    }

    /// <summary>
    /// One tick at <paramref name="now"/> (the bot's clock). <paramref name="lying"/> is the replicated posture of the own
    /// body, or null when it is not known.
    /// </summary>
    public RestStep Update(long now, bool? lying)
    {
        if (!_started)
        {
            _started = true;
            _down = false;
            _nextAt = now + Ticks(_settings.MinUpSeconds, _settings.MaxUpSeconds);
        }

        if (!_down)
        {
            if (now < _nextAt) return new RestStep(false, true);
            if (lying == true)
            {
                // Already down for another reason: look again in a second.
                _nextAt = now + 20;
                return new RestStep(false, true);
            }
            _down = true;
            _confirmed = false;
            _downSince = now;
            _getUpAt = now + Ticks(_settings.MinDownSeconds, _settings.MaxDownSeconds);
            LieDowns++;
            return new RestStep(true, false);
        }

        if (lying == true || lying == null) _confirmed = true;
        if (!_confirmed && now - _downSince >= _settings.ConfirmTicks)
        {
            Refused++;
            Stand(now);
            return new RestStep(false, true);
        }
        if (_confirmed && lying == false)
        {
            // Up again without a Rest of ours (tg got it up).
            Stand(now);
            return new RestStep(false, true);
        }
        if (now >= _getUpAt)
        {
            GetUps++;
            Stand(now);
            return new RestStep(true, true);
        }
        return new RestStep(false, false);
    }

    private void Stand(long now)
    {
        _down = false;
        _nextAt = now + Ticks(_settings.MinUpSeconds, _settings.MaxUpSeconds);
    }

    private int Ticks(double minSeconds, double maxSeconds)
    {
        int min = (int)Math.Round(minSeconds * Bot.UpdatesPerSecond);
        int max = (int)Math.Round(maxSeconds * Bot.UpdatesPerSecond);
        return _random.Next(min, Math.Max(min, max));
    }
}

/// <summary>
/// A toggle the bot presses now and then and presses back after a while (walk/run, combat mode): rare, so that it does not
/// dominate, and paired, so that the bot does not stay walking or in combat mode for good.
/// </summary>
public sealed class ToggleSchedule
{
    private readonly BotRandom _random;
    private readonly double _minGapSeconds;
    private readonly double _maxGapSeconds;
    private readonly double _minHoldSeconds;
    private readonly double _maxHoldSeconds;
    private bool _started;
    private long _nextAt;

    public ToggleSchedule(BotRandom random, double minGapSeconds, double maxGapSeconds, double minHoldSeconds, double maxHoldSeconds)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _minGapSeconds = minGapSeconds;
        _maxGapSeconds = maxGapSeconds;
        _minHoldSeconds = minHoldSeconds;
        _maxHoldSeconds = maxHoldSeconds;
    }

    /// <summary>True while the toggle is pressed (an odd number of presses).</summary>
    public bool On { get; private set; }

    /// <summary>Presses so far.</summary>
    public int Presses { get; private set; }

    /// <summary>Forgets the timer and the state (a new connection: the body is a new one).</summary>
    public void Reset()
    {
        _started = false;
        On = false;
    }

    /// <summary>One tick at <paramref name="now"/>: true when the bot should press the toggle now.</summary>
    public bool Update(long now)
    {
        if (!_started)
        {
            _started = true;
            _nextAt = now + Ticks(_minGapSeconds, _maxGapSeconds);
        }
        if (now < _nextAt) return false;

        On = !On;
        Presses++;
        _nextAt = now + (On ? Ticks(_minHoldSeconds, _maxHoldSeconds) : Ticks(_minGapSeconds, _maxGapSeconds));
        return true;
    }

    private int Ticks(double minSeconds, double maxSeconds)
    {
        int min = (int)Math.Round(minSeconds * Bot.UpdatesPerSecond);
        int max = (int)Math.Round(maxSeconds * Bot.UpdatesPerSecond);
        return _random.Next(min, Math.Max(min, max));
    }
}
