using Anomaly.Core.Tg;

namespace Anomaly.Client.Shared.Models;

/// <summary>
/// The aim a solar panel's or tracker's head shows (<see cref="ModelRule.Sun"/>): its azimuth (tg's, degrees clockwise from north) and its tilt (degrees from flat).
/// A new aim is reached along an eased curve (slow at both ends) by the shorter way round, at about <see cref="DegreesPerSecond"/>: tg animates the panel's
/// overlay to it in two cubic halves of 2.5 s (solar.dm visually_turn_part), the 3D head turns slowly enough to be watched (DV74).
/// </summary>
public sealed class SunHeadMotion
{
    public const float DegreesPerSecond = 12f;
    public const float MinSeconds = 2f;

    private float _fromAzimuth, _fromTilt, _toAzimuth, _toTilt, _time, _duration;

    public SunHeadMotion(float azimuth, float tilt)
    {
        Azimuth = _fromAzimuth = _toAzimuth = azimuth;
        Tilt = _fromTilt = _toTilt = tilt;
    }

    public float Azimuth { get; private set; }
    public float Tilt { get; private set; }
    public bool Moving => _time < _duration;

    /// <summary>Starts toward a new aim from where the head is now; the same aim again changes nothing.</summary>
    public void Aim(float azimuth, float tilt)
    {
        if (MathF.Abs(Delta(_toAzimuth, azimuth)) < 0.01f && MathF.Abs(_toTilt - tilt) < 0.01f)
            return;
        _fromAzimuth = Azimuth;
        _fromTilt = Tilt;
        _toAzimuth = azimuth;
        _toTilt = tilt;
        _time = 0f;
        _duration = MathF.Max(MinSeconds, MathF.Max(MathF.Abs(Delta(_fromAzimuth, _toAzimuth)), MathF.Abs(_toTilt - _fromTilt)) / DegreesPerSecond);
    }

    public void Step(float dt)
    {
        if (!Moving)
            return;
        _time = MathF.Min(_duration, _time + dt);
        float x = _time / _duration;
        float s = x * x * (3f - 2f * x); // smoothstep: eases in and out
        Azimuth = (_fromAzimuth + Delta(_fromAzimuth, _toAzimuth) * s + 360f) % 360f;
        Tilt = _fromTilt + (_toTilt - _fromTilt) * s;
    }

    /// <summary>
    /// The tilt a head at <paramref name="azimuth"/> aims at: a panel still at the bearing it started at keeps its own tilt (DV197: <c>anomaly_start_tilt</c>);
    /// one that something turned, and every tracker, faces the sun's height.
    /// </summary>
    public static float TiltFor(long id, float azimuth, bool panel)
        => panel && MathF.Abs(Delta(azimuth, obj_machinery_power_solar.anomaly_start_azimuth(id))) < 0.1f
            ? obj_machinery_power_solar.anomaly_start_tilt(id)
            : 90f - ModelRules.SunElevationDegrees;

    private static float Delta(float from, float to) => ((to - from) % 360f + 540f) % 360f - 180f;
}
