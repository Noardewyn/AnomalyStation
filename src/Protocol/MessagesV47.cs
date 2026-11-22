namespace Anomaly.Protocol;

public sealed partial class Delta
{
    /// <summary>Version 47: tg's <c>SSsun.azimuth</c> (degrees, clockwise from north), the sun the sky draws; NaN before the sun exists.</summary>
    public float SunAzimuth { get; set; } = float.NaN;
}
