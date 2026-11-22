namespace Anomaly.Protocol;

public sealed partial class ReplicaWorld
{
    /// <summary>The sun's azimuth of the last Delta (tg's <c>SSsun.azimuth</c>, degrees clockwise from north); NaN until one names it.</summary>
    public float SunAzimuth { get; private set; } = float.NaN;
}
