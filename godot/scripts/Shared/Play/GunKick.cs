namespace Anomaly.Client.Shared;

// Owner: DV96 (the owner, 2026-11-21: the anti-tank rifle's kick snapped up in a frame, every other gun's was too weak to feel). Engine-free.

/// <summary>
/// The first-person gun kick, as shooters play it: <see cref="AimShare"/> of a shot's kick turns the aim itself (the crosshair climbs, and a burst
/// climbs further: the player pulls it back down with the mouse), the rest punches the view on a critically damped spring that comes back by itself.
/// The aim the player did not pull back comes back once the gun stops kicking (<see cref="Compensate"/> takes off what the mouse pulled against it,
/// so the view never comes back past where it was aimed). A heavy kick rises and returns slower and also shoves the eye back, widens the field of
/// view and rolls the view toward the kick's side.
/// </summary>
public sealed class GunKick
{
    /// <summary>The share of a kick that turns the aim (the rest is the spring's punch of the view).</summary>
    public const float AimShare = 0.5f;

    /// <summary>The most one kick turns, degrees (tg's anti-tank rifle asks for 45), and the most the aim kicks of a burst add up to.</summary>
    public const float MostDegrees = 15f, AimMostDegrees = 25f;

    /// <summary>A kick of this many degrees or less is light, of <see cref="HeavyAtDegrees"/> or more fully heavy.</summary>
    public const float LightToDegrees = 2f, HeavyAtDegrees = 10f;

    /// <summary>The spring's speed, per second, for a light and a fully heavy kick: its punch peaks after 1/omega seconds (36 ms, 110 ms).</summary>
    public const float LightOmega = 28f, HeavyOmega = 9f;

    /// <summary>How long after the last kick the aim starts coming back, seconds (light; a heavy kick waits longer).</summary>
    public const float RecoverAfter = 0.15f;

    private const float Degree = MathF.PI / 180f;
    private float _punchUp, _punchRight, _speedUp, _speedRight, _omega = LightOmega;
    private float _aimUp, _aimRight, _appliedUp, _appliedRight, _takeUp, _takeRight;
    private float _since = 10f, _heavy, _degrees, _rollSide = 1f;

    /// <summary>The held gun's own recoil tuning (the dev panel's, held_items.json): 1 as the server sends it.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>The view's punch now, radians: up and to the right.</summary>
    public float PunchUp => _punchUp;

    public float PunchRight => _punchRight;

    /// <summary>The last kick's size, degrees, and how heavy it was (0 to 1).</summary>
    public float LastDegrees => _degrees;

    public float Heavy => _heavy;

    /// <summary>The side the last kick rolls the view and the held gun to (1 right, -1 left).</summary>
    public float RollSide => _rollSide;

    /// <summary>Seconds since the last kick.</summary>
    public float SinceKick => _since;

    /// <summary>The aim kick not yet come back, radians (up, right).</summary>
    public (float Up, float Right) AimLeft => (_aimUp, _aimRight);

    /// <summary>The eye's shove back along the view now, metres (negative: back).</summary>
    public float Forward => -MathF.Min(0.012f * Punch, 0.1f) * (0.3f + 0.7f * _heavy);

    /// <summary>The field of view's widening now, degrees.</summary>
    public float FovDegrees => MathF.Min(0.35f * Punch, 4f) * _heavy;

    /// <summary>The view's roll now, radians.</summary>
    public float Roll => _rollSide * Punch * Degree * 0.35f * _heavy;

    // the punch's size, degrees
    private float Punch => MathF.Sqrt(_punchUp * _punchUp + _punchRight * _punchRight) / Degree;

    /// <summary>A shot's kick: <paramref name="upRadians"/> up and <paramref name="rightRadians"/> to the right (times <see cref="Scale"/>).</summary>
    public void Kick(float upRadians, float rightRadians)
    {
        upRadians *= Scale;
        rightRadians *= Scale;
        float size = MathF.Sqrt(upRadians * upRadians + rightRadians * rightRadians);
        if (size <= 1e-6f)
            return;
        if (size > MostDegrees * Degree)
        {
            float k = MostDegrees * Degree / size;
            (upRadians, rightRadians, size) = (upRadians * k, rightRadians * k, MostDegrees * Degree);
        }
        _degrees = size / Degree;
        _heavy = Math.Clamp((_degrees - LightToDegrees) / (HeavyAtDegrees - LightToDegrees), 0f, 1f);
        _omega = LightOmega + (HeavyOmega - LightOmega) * _heavy;
        // a critically damped spring pushed with speed v peaks at v / (omega e) after 1/omega
        float push = (1f - AimShare) * _omega * MathF.E;
        _speedUp += upRadians * push;
        _speedRight += rightRadians * push;
        _aimUp += upRadians * AimShare;
        _aimRight += rightRadians * AimShare;
        float aim = MathF.Sqrt(_aimUp * _aimUp + _aimRight * _aimRight), most = AimMostDegrees * Degree;
        if (aim > most)
            (_aimUp, _aimRight) = (_aimUp * most / aim, _aimRight * most / aim);
        _rollSide = rightRadians >= 0f ? 1f : -1f;
        _since = 0f;
    }

    /// <summary>One frame of <paramref name="dt"/> seconds.</summary>
    public void Advance(float dt)
    {
        if (dt <= 0f)
            return;
        _since += dt;
        Spring(ref _punchUp, ref _speedUp, _omega, dt);
        Spring(ref _punchRight, ref _speedRight, _omega, dt);
        if (_since > RecoverAfter + 0.25f * _heavy)
        {
            float back = 1f - MathF.Exp(-dt * (8f - 5f * _heavy));
            _aimUp -= _aimUp * back;
            _aimRight -= _aimRight * back;
        }
        // the aim follows its kick about as fast as the punch rises
        float follow = 1f - MathF.Exp(-dt * _omega * 1.5f);
        float up = (_aimUp - _appliedUp) * follow, right = (_aimRight - _appliedRight) * follow;
        _appliedUp += up;
        _appliedRight += right;
        _takeUp += up;
        _takeRight += right;
        if (_since > 3f && MathF.Abs(_punchUp) + MathF.Abs(_punchRight) < 1e-5f)
            _punchUp = _punchRight = _speedUp = _speedRight = 0f;
    }

    /// <summary>The aim's turn since the last take, radians (up, right): the caller turns the view's own pitch and yaw by it.</summary>
    public (float Up, float Right) TakeAim()
    {
        (float, float) taken = (_takeUp, _takeRight);
        _takeUp = _takeRight = 0f;
        return taken;
    }

    /// <summary>
    /// The mouse turned the view <paramref name="upRadians"/> up and <paramref name="rightRadians"/> right: what it pulled against the aim kick is
    /// no longer to come back.
    /// </summary>
    public void Compensate(float upRadians, float rightRadians)
    {
        Pull(ref _aimUp, ref _appliedUp, upRadians);
        Pull(ref _aimRight, ref _appliedRight, rightRadians);
    }

    // a critically damped spring's exact step: x(t) = (x0 + (v0 + w x0) t) e^(-w t)
    private static void Spring(ref float x, ref float v, float omega, float t)
    {
        float decay = MathF.Exp(-omega * t), carry = v + omega * x;
        x = (x + carry * t) * decay;
        v = (v - omega * carry * t) * decay;
    }

    private static void Pull(ref float aim, ref float applied, float moved)
    {
        if (aim * moved >= 0f)
            return;
        float pulled = MathF.Min(MathF.Abs(moved), MathF.Abs(aim)) * MathF.Sign(aim);
        aim -= pulled;
        applied -= pulled;
    }

    public void Reset()
    {
        _punchUp = _punchRight = _speedUp = _speedRight = 0f;
        _aimUp = _aimRight = _appliedUp = _appliedRight = _takeUp = _takeRight = 0f;
        _since = 10f;
        _heavy = _degrees = 0f;
    }
}
