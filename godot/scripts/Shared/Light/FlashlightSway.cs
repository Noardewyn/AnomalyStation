namespace Anomaly.Client.Shared.Light;

// Engine-free. D288: the own flashlight's beam sways as a hand holds it (the owner, 2026-10-29, after S.T.A.L.K.E.R.'s torch): a slow drift
// while standing, a swing toward a turn of the view that settles back on a spring, and a sway in step with the walk, more when running.
// tg has nothing like it (its flashlight is a cone drawn from above).

/// <summary>The own beam's sway off the way it is aimed, in degrees (yaw to the right, pitch up).</summary>
public sealed class FlashlightSway
{
    /// <summary>
    /// The tuning (the View tab's sliders): the idle drift's size, how far the beam swings toward a turn per degree a second of it (seconds;
    /// negative: it lags behind the turn), the most it swings, and the walk's sway at a walk (degrees; running sways it more).
    /// </summary>
    public static float IdleDegrees = 1.5f, TurnSeconds = 0.04f, TurnMaxDegrees = 10f, WalkDegrees = 1.2f; // the owner's (2026-10-29)

    private const float Stiffness = 60f, Damping = 9f; // the turn's spring: under-damped, a little swing back when the turn stops
    private float _yaw, _pitch, _yawSpeed, _pitchSpeed;

    /// <summary>
    /// One frame of <paramref name="dt"/> seconds at <paramref name="now"/>: the view turns at <paramref name="yawRate"/> and
    /// <paramref name="pitchRate"/> degrees a second (right and up), <paramref name="stridePhase"/> is the walk's phase (radians, a step every
    /// pi) and <paramref name="stride"/> how hard one walks (0 standing, 1 at a walk, more running). Returns the beam's sway.
    /// </summary>
    public (float Yaw, float Pitch) Update(double now, float dt, float yawRate, float pitchRate, double stridePhase, float stride)
    {
        dt = Math.Clamp(dt, 0f, 0.1f);
        Spring(ref _yaw, ref _yawSpeed, Math.Clamp(yawRate * TurnSeconds, -TurnMaxDegrees, TurnMaxDegrees), dt);
        Spring(ref _pitch, ref _pitchSpeed, Math.Clamp(pitchRate * TurnSeconds, -TurnMaxDegrees, TurnMaxDegrees), dt);
        float t = (float)(now % 10000.0);
        float idleYaw = IdleDegrees * (MathF.Sin(t * 0.9f) + 0.5f * MathF.Sin(t * 2.3f + 1f)) / 1.5f;
        float idlePitch = IdleDegrees * (MathF.Sin(t * 1.3f + 2f) + 0.4f * MathF.Sin(t * 3.1f)) / 1.4f;
        // across once a stride, down at each step (HeldPose.Bob's figure eight)
        float walk = WalkDegrees * stride, side = (float)Math.Sin(stridePhase), dip = 1f - (float)Math.Abs(Math.Cos(stridePhase));
        return (_yaw + idleYaw + walk * side, _pitch + idlePitch - 0.6f * walk * dip);
    }

    private static void Spring(ref float at, ref float speed, float target, float dt)
    {
        speed += (Stiffness * (target - at) - Damping * speed) * dt;
        at += speed * dt;
    }
}
