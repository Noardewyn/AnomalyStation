namespace Anomaly.Client.Shared.Icons;

// Owner: the lying mobs' billboards (the owner's play test, 2026-10-13). Engine-free.

/// <summary>
/// Which way a lying mob faces, as its billboard needs it. The replicated yaw is where the player LOOKS (the camera), which a body on the floor
/// does not follow: it keeps the way it was facing when it went down. Fed once per frame with the pose; a standing mob passes through.
/// </summary>
public sealed class LyingFacing
{
    private bool _lying;
    private byte _yaw;

    /// <summary>The yaw byte (256 per turn, clockwise from north) to draw the frame for; <paramref name="poseYaw"/> while the mob stands.</summary>
    public byte Update(bool lying, byte poseYaw)
    {
        if (!lying)
        {
            _lying = false;
            return poseYaw;
        }
        if (!_lying)
        {
            _lying = true;
            _yaw = poseYaw;
        }
        return _yaw;
    }
}
