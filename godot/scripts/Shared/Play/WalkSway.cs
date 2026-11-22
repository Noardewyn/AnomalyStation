namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// DEVIATION(DV434): tg's mob sprite glides between tiles with no step; a first-person 3D view shows that as a picture sliding over the floor
// (the owner, 2026-11-15), so a moving standing mob's picture rocks side to side and bobs once a step, in time with its footsteps.

/// <summary>
/// A walking mob picture's sway: the picture tilts toward the stepping foot and lifts a little between footfalls, one footfall a footstep
/// sound's spacing (<see cref="CapsuleStepMetres"/>, <see cref="TileStepMetres"/>), and a heard footstep lands the foot then (<see cref="Land"/>).
/// It fades in and out with the speed so a stop settles instead of snapping.
/// </summary>
public sealed class WalkSway
{
    /// <summary>
    /// DV233: a capsule mob sounds a footstep every other stride of 183/256 of a tile (LocomotionSeam.DefaultStride): 2.86 m, about 4.5 steps a
    /// second at tg's run (1.5 ds a tile), 1.75 at its walk (4 ds).
    /// </summary>
    public const double CapsuleStepMetres = 2 * 183 / 256.0 * 2.0;

    /// <summary>A mob moved tile by tile (an AI's) sounds tg's footstep every other move: two tiles.</summary>
    public const double TileStepMetres = 4.0;

    /// <summary>An exosuit stomps every move (tg's mecha play_stepsound on each step): a tile.</summary>
    public const double ExosuitStepMetres = 2.0;

    /// <summary>More than this in a frame is a jump (a teleport, a level change), not a step.</summary>
    public const double JumpMetres = 5.0;

    /// <summary>tg's walk speed (4 ds a tile: 5 m/s), where the sway is full; a run (13 m/s) rocks up to <see cref="RunGain"/> times as far.</summary>
    public const double WalkMetresPerSecond = 5.0;

    public const double RunGain = 1.5;

    /// <summary>The tilt at the walk's step (radians, about 1 degree) and the lift between steps (metres): 0.49 of the first 0.035 and 0.03 (the owner, 2026-11-18 and 2026-11-19: too strong, 30 % less each time).</summary>
    public const double RollRadians = 0.01715, LiftMetres = 0.0147;

    /// <summary>How fast the sway follows the speed (per second): about a tenth of a second to settle.</summary>
    public const double Follow = 12.0;

    /// <summary>How fast a heard footstep pulls the step onto it (per second): the foot lands within about a tenth of a second, not at a jump.</summary>
    public const double Pull = 20.0;

    private double _phase;
    private double _amount;
    private double _pull; // the phase still to add to land on the last heard footstep

    /// <summary>The tilt now (radians, positive leans the picture's top to its left).</summary>
    public double Roll { get; private set; }

    /// <summary>The lift now (metres, up).</summary>
    public double Lift { get; private set; }

    /// <summary>
    /// Advances by <paramref name="metres"/> travelled on the ground in <paramref name="seconds"/>, a footfall every <paramref name="stepMetres"/>
    /// (the phase goes half a turn a step: the two feet a turn).
    /// </summary>
    public void Step(double metres, double seconds, double stepMetres)
    {
        if (seconds <= 0)
            return;
        if (metres > JumpMetres)
            metres = 0;
        _given = stepMetres;
        _sinceStep += metres;
        double target = Math.Min(metres / seconds / WalkMetresPerSecond, RunGain);
        _amount += (target - _amount) * Math.Min(1.0, seconds * Follow);
        double pulled = _pull * Math.Min(1.0, seconds * Pull);
        _pull -= pulled;
        _phase = (_phase + metres / (_learned > 0 ? _learned : stepMetres) * Math.PI + pulled) % (2 * Math.PI);
        if (_amount < 0.01)
        {
            // at rest the next walk starts from a planted foot
            _amount = 0;
            _phase = 0;
            _pull = 0;
            _sinceStep = 0;
        }
        double s = Math.Sin(_phase);
        Roll = RollRadians * _amount * s;
        Lift = LiftMetres * _amount * Math.Abs(s);
    }

    /// <summary>
    /// A footstep of this mob was heard now: the step under way lands on it (the nearest footfall, a step half done goes on to land), and
    /// the distance drawn since the last one, in a steady walk, teaches the step's length (the drawn path is not quite the server's strides).
    /// </summary>
    public void Land()
    {
        double walked = _sinceStep;
        _sinceStep = 0;
        if (_amount <= 0)
            return;
        double guess = _learned > 0 ? _learned : _given;
        if (_amount > 0.5 && guess > 0 && walked > guess * 0.6 && walked < guess * 1.6)
            _learned = guess + (walked - guess) * Learn;
        double at = _phase + _pull;
        _pull = Math.Round(at / Math.PI, MidpointRounding.AwayFromZero) * Math.PI - _phase;
    }

    /// <summary>How much of the gap between the step's length and a heard step's a footstep teaches (a jittered stop or start teaches little).</summary>
    public const double Learn = 0.3;

    private double _sinceStep; // the metres drawn since the last heard footstep
    private double _learned; // the step's length the footsteps taught; 0 before any
    private double _given; // the length the caller gave last
}
