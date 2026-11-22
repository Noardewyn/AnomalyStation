using Anomaly.Core.Motion;

namespace Anomaly.Tools.Bots;

/// <summary>What a bot asks of its capsule for one tick: a world-space wish vector (each component -127 to 127, the length at most 127) and the yaw it looks at.</summary>
/// <param name="MoveX">East.</param>
/// <param name="MoveY">North.</param>
/// <param name="Yaw">A compass bearing, 65536 per turn, 0 north, clockwise.</param>
public readonly record struct BotWish(sbyte MoveX, sbyte MoveY, ushort Yaw)
{
    /// <summary>True when the wish vector is zero.</summary>
    public bool IsStill => MoveX == 0 && MoveY == 0;

    /// <summary>The wish toward the compass bearing <paramref name="turns"/> (0 to 1, 0 north, clockwise) at <paramref name="speed"/> of 127.</summary>
    public static BotWish Toward(double turns, int speed)
    {
        double angle = turns * (2.0 * Math.PI);
        int x = (int)Math.Round(Math.Sin(angle) * speed);
        int y = (int)Math.Round(Math.Cos(angle) * speed);
        return new BotWish((sbyte)Math.Clamp(x, -127, 127), (sbyte)Math.Clamp(y, -127, 127), YawOf(turns));
    }

    /// <summary>The yaw of a compass bearing given in turns.</summary>
    public static ushort YawOf(double turns) => (ushort)((int)Math.Round(turns * 65536.0) & 0xFFFF);

    /// <summary>The compass bearing in turns (0 to 1) from (0, 0) to (<paramref name="dx"/>, <paramref name="dy"/>): 0 is north (+y), 0.25 east (+x).</summary>
    public static double BearingOf(double dx, double dy)
    {
        double turns = Math.Atan2(dx, dy) / (2.0 * Math.PI);
        return turns < 0 ? turns + 1.0 : turns;
    }
}

/// <summary>The knobs of the wander behaviour (M2 risk: dense mobs jam small rooms and 1-tile corridors, so a bot looks ahead and re-picks when blocked).</summary>
public sealed class WanderSettings
{
    /// <summary>The fewest ticks a picked heading is held for.</summary>
    public int MinHoldTicks { get; init; } = 30;

    /// <summary>The most ticks a picked heading is held for.</summary>
    public int MaxHoldTicks { get; init; } = 100;

    /// <summary>
    /// Ticks without a change of the position after which the bot calls itself blocked and re-picks. It must cover the round
    /// trip and the server's jitter buffer, so that a heading just picked is not judged before its first step is seen.
    /// </summary>
    public int BlockedTicks { get; init; } = 10;

    /// <summary>A position that moved by less than this since the last time it moved (1/256 tile, both axes summed) counts as not moved.</summary>
    public int StillSubunits { get; init; } = 6;

    /// <summary>How far ahead a pick looks for walls, in tiles.</summary>
    public double LookAheadTiles { get; init; } = 4.0;

    /// <summary>The bot steers away when the way ahead along its heading is shorter than this, in tiles.</summary>
    public double SteerBelowTiles { get; init; } = 0.9;

    /// <summary>A pick takes a heading with at least this much free way (or the freest one when none has).</summary>
    public double OpenTiles { get; init; } = 2.0;

    /// <summary>The radius of the body the look-ahead sweeps, in tiles (the capsule's).</summary>
    public double RadiusTiles { get; init; } = MotionMath.DefaultRadius;

    /// <summary>The chance that a pick walks at <see cref="PartialSpeed"/> instead of full speed.</summary>
    public double PartialSpeedChance { get; init; } = 0.1;

    /// <summary>The wish length of a partial-speed pick (of 127).</summary>
    public int PartialSpeed { get; init; } = 64;
}

/// <summary>
/// The wander behaviour as a pure function of what the bot sees (M2 scope 2l): its continuous position (1/256 tile), the
/// solidity grid and its random stream. It holds a compass heading for a random time, looks ahead along it with the body's
/// radius and steers away from solid tiles, and re-picks when the position stops changing (blocked). Headings are the 16
/// compass points with a little jitter, so that a 1-tile corridor or a doorway is entered along its axis; a dead end offers
/// only the way back, and that is the one it takes. No transport and no codec: floats and <see cref="Math"/> are fine here,
/// a bot is not the core.
/// </summary>
public sealed class WanderPlanner
{
    private const int Headings = 16;
    private const double LookStepTiles = 0.125;

    // The look-ahead sweeps a body a little thinner than the capsule: a bot hugging a wall (the controller keeps its centre
    // one radius off the face) must not count that wall as ahead of a heading along it.
    private const double ProbeMarginTiles = 0.01;

    private readonly BotRandom _random;
    private readonly WanderSettings _settings;
    private readonly double[] _free = new double[Headings];
    private bool _hasHeading;
    private double _heading;
    private int _speed;
    private int _holdLeft;
    private int _anchorX;
    private int _anchorY;
    private int _stillTicks;

    public WanderPlanner(BotRandom random, WanderSettings settings = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _settings = settings ?? new WanderSettings();
        _speed = MotionInput.MaxMove;
    }

    /// <summary>The current compass heading in turns (0 to 1), meaningful once <see cref="Picks"/> is above 0.</summary>
    public double Heading => _heading;

    /// <summary>How many times it picked a heading (the first pick included).</summary>
    public int Picks { get; private set; }

    /// <summary>How many of the picks were because the bot was blocked (its position stopped changing).</summary>
    public int BlockedRepicks { get; private set; }

    /// <summary>How many of the picks were because a wall was ahead.</summary>
    public int Steers { get; private set; }

    /// <summary>Forgets the heading and the position (a new connection).</summary>
    public void Reset()
    {
        _hasHeading = false;
        _holdLeft = 0;
        _stillTicks = 0;
    }

    /// <summary>The bot could not move for a while on purpose (lying down): the blocked rule starts counting again.</summary>
    public void ResumeAfterPause()
    {
        _stillTicks = 0;
    }

    /// <summary>
    /// One tick. <paramref name="havePosition"/> is false while the bot's own entity is unknown (it then holds what it holds).
    /// <paramref name="posX"/> and <paramref name="posY"/> are the centre in 1/256 tile, <paramref name="z"/> the level.
    /// <paramref name="solidity"/> may be null (nothing is known about walls).
    /// </summary>
    public BotWish Update(bool havePosition, int posX, int posY, int z, ISolidity solidity)
    {
        if (!havePosition) return Current();

        double x = posX / 256.0;
        double y = posY / 256.0;
        if (!_hasHeading)
        {
            Pick(x, y, z, solidity, avoidCurrent: false);
            Anchor(posX, posY);
            return Current();
        }

        if (Math.Abs(posX - _anchorX) + Math.Abs(posY - _anchorY) > _settings.StillSubunits)
        {
            Anchor(posX, posY);
        }
        else
        {
            _stillTicks++;
        }
        _holdLeft--;

        if (_stillTicks >= _settings.BlockedTicks)
        {
            BlockedRepicks++;
            Pick(x, y, z, solidity, avoidCurrent: true);
        }
        else if (_holdLeft <= 0)
        {
            Pick(x, y, z, solidity, avoidCurrent: false);
        }
        else if (FreeDistance(x, y, z, _heading, _settings.SteerBelowTiles, solidity) < _settings.SteerBelowTiles)
        {
            Steers++;
            Pick(x, y, z, solidity, avoidCurrent: true);
        }
        return Current();
    }

    private void Anchor(int posX, int posY)
    {
        _anchorX = posX;
        _anchorY = posY;
        _stillTicks = 0;
    }

    private BotWish Current()
    {
        return _hasHeading ? BotWish.Toward(_heading, _speed) : default;
    }

    private void Pick(double x, double y, int z, ISolidity solidity, bool avoidCurrent)
    {
        Picks++;
        _stillTicks = 0;

        int current = (int)Math.Round(_heading * Headings) % Headings;
        for (int i = 0; i < Headings; i++)
        {
            _free[i] = FreeDistance(x, y, z, i / (double)Headings, _settings.LookAheadTiles, solidity);
        }

        // The headings within 45 degrees of the one that was blocked are not picked again, unless nothing else is free.
        bool avoid = avoidCurrent && _hasHeading;
        int chosen = Choose(current, avoid);
        if (chosen < 0 && avoid) chosen = Choose(current, avoid: false);

        double heading = chosen / (double)Headings;
        double jitter = (_random.NextDouble() - 0.5) / Headings;
        double jittered = heading + jitter;
        if (jittered < 0) jittered += 1.0;
        if (jittered >= 1.0) jittered -= 1.0;
        double need = Math.Min(_free[chosen], _settings.OpenTiles);
        if (FreeDistance(x, y, z, jittered, need, solidity) >= need) heading = jittered;

        _heading = heading;
        _hasHeading = true;
        _holdLeft = _random.Next(_settings.MinHoldTicks, Math.Max(_settings.MinHoldTicks, _settings.MaxHoldTicks));
        _speed = _random.Chance(_settings.PartialSpeedChance) ? Math.Clamp(_settings.PartialSpeed, 1, MotionInput.MaxMove) : MotionInput.MaxMove;
    }

    /// <summary>A heading with a free way of at least <see cref="WanderSettings.OpenTiles"/> chosen at random, else the freest one; -1 when every candidate is avoided.</summary>
    private int Choose(int current, bool avoid)
    {
        int good = 0;
        double best = -1;
        for (int i = 0; i < Headings; i++)
        {
            if (avoid && CircularDistance(i, current) <= 2) continue;
            if (_free[i] >= _settings.OpenTiles) good++;
            if (_free[i] > best) best = _free[i];
        }
        if (best < 0) return -1;

        if (good > 0)
        {
            int pick = _random.Next(0, good - 1);
            for (int i = 0; i < Headings; i++)
            {
                if (avoid && CircularDistance(i, current) <= 2) continue;
                if (_free[i] < _settings.OpenTiles) continue;
                if (pick-- == 0) return i;
            }
        }

        // Nothing is open (a dead end, a pocket): the freest way out, from a random start so that ties do not favour a side.
        int start = _random.Next(0, Headings - 1);
        for (int n = 0; n < Headings; n++)
        {
            int i = (start + n) % Headings;
            if (avoid && CircularDistance(i, current) <= 2) continue;
            if (_free[i] >= best) return i;
        }
        return -1;
    }

    private static int CircularDistance(int a, int b)
    {
        int d = Math.Abs(a - b) % Headings;
        return Math.Min(d, Headings - d);
    }

    /// <summary>How far, in tiles and up to <paramref name="max"/>, a body of the capsule's radius can move from (x, y) along the compass bearing before it touches a solid tile.</summary>
    public double FreeDistance(double x, double y, int z, double turns, double max, ISolidity solidity)
    {
        if (solidity == null) return max;
        double angle = turns * (2.0 * Math.PI);
        double sin = Math.Sin(angle);
        double cos = Math.Cos(angle);
        for (double d = LookStepTiles; d <= max + 1e-9; d += LookStepTiles)
        {
            if (Touches(x + sin * d, y + cos * d, z, solidity)) return d - LookStepTiles;
        }
        return max;
    }

    /// <summary>True when a body centred at (px, py) overlaps a solid tile.</summary>
    private bool Touches(double px, double py, int z, ISolidity solidity)
    {
        double r = _settings.RadiusTiles - ProbeMarginTiles;
        int x0 = (int)Math.Floor(px - r + 0.5);
        int x1 = (int)Math.Floor(px + r + 0.5);
        int y0 = (int)Math.Floor(py - r + 0.5);
        int y1 = (int)Math.Floor(py + r + 0.5);
        for (int tx = x0; tx <= x1; tx++)
        {
            for (int ty = y0; ty <= y1; ty++)
            {
                if (!solidity.IsSolid(tx, ty, z)) continue;
                // The nearest point of the tile's square [tx - 0.5, tx + 0.5] x [ty - 0.5, ty + 0.5] to the centre.
                double nx = Math.Clamp(px, tx - 0.5, tx + 0.5);
                double ny = Math.Clamp(py, ty - 0.5, ty + 0.5);
                double dx = px - nx;
                double dy = py - ny;
                if (dx * dx + dy * dy < r * r) return true;
            }
        }
        return false;
    }
}
