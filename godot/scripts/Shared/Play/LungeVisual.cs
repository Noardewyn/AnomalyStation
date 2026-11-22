namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The owner's play test (2026-10-09): tg's attack lunge (do_attack_animation, atoms_movable.dm:1545), which the server sends as
// EntityFlags.Lunge with a direction for the tick of the attack; the client plays tg's two animate steps.

/// <summary>tg's attack lunge as a pure function of its age: the picture moves 8 pixels toward the target and back, turning 15 degrees.</summary>
public static class LungeVisual
{
    /// <summary>tg's offset: 8 pixels on each axis the direction has.</summary>
    public const double Pixels = 8;

    /// <summary>The first step: <c>time = 1</c> (a decisecond), <c>BACK_EASING | EASE_IN</c>.</summary>
    public const double OutSeconds = 0.1;

    /// <summary>The second step: <c>time = 2</c>, <c>SINE_EASING</c>, back to the start.</summary>
    public const double BackSeconds = 0.2;

    /// <summary>tg's turn at the peak, <c>Turn(15 * turn_dir)</c>.</summary>
    public const double TurnDegrees = 15;

    /// <summary>The whole lunge (seconds).</summary>
    public const double Seconds = OutSeconds + BackSeconds;

    /// <summary>How far along the offset is (0 at rest, 1 at the peak) <paramref name="ageSeconds"/> after the lunge began.</summary>
    public static double Amount(double ageSeconds)
    {
        if (ageSeconds <= 0 || ageSeconds >= Seconds)
            return 0;
        if (ageSeconds < OutSeconds)
        {
            // BYOND's BACK_EASING with EASE_IN: t^2 ((s + 1) t - s), s = 1.70158 (it pulls back a little first)
            double t = ageSeconds / OutSeconds;
            const double s = 1.70158;
            return t * t * ((s + 1) * t - s);
        }
        double u = (ageSeconds - OutSeconds) / BackSeconds;
        return 0.5 + 0.5 * Math.Cos(Math.PI * u); // SINE_EASING (in and out) from the peak back to rest
    }

    /// <summary>The offset in tg pixels (east, north) of a lunge toward BYOND direction <paramref name="dir"/>.</summary>
    public static (double East, double North) Offset(byte dir, double ageSeconds)
    {
        double a = Amount(ageSeconds) * Pixels;
        double east = (dir & 4) != 0 ? a : (dir & 8) != 0 ? -a : 0;
        double north = (dir & 1) != 0 ? a : (dir & 2) != 0 ? -a : 0;
        return (east, north);
    }

    /// <summary>
    /// tg's turn direction: 1, -1 toward the west; north or south picks either at random (<paramref name="coin"/>: a fair
    /// coin of the caller's), and a diagonal with west takes the west's -1 as tg's later branch does.
    /// </summary>
    public static int TurnSign(byte dir, bool coin)
    {
        int turn = 1;
        if ((dir & 3) != 0)
            turn = coin ? -1 : 1;
        if ((dir & 8) != 0)
            turn = -1;
        return turn;
    }

    /// <summary>The turn (degrees, BYOND's clockwise) <paramref name="ageSeconds"/> into a lunge with <paramref name="turnSign"/>; it follows the offset's curve.</summary>
    public static double Turn(int turnSign, double ageSeconds) => TurnDegrees * turnSign * Amount(ageSeconds);
}
