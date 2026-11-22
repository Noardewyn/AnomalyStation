namespace Anomaly.Protocol;

// Version 73 (DV360): a MapGrid cell's flaps bit, and the own capsule's PassesFlaps flag at the end of the private block's motion
// parameters (Codec.V2.cs), so the client predicts tg's airtight plastic flaps stopping a standing mob as the server does.

public static partial class CellFlags
{
    /// <summary>
    /// Version 73 (DV360): tg's plastic flaps that refuse a standing mob stand on the cell (<c>Anomaly.Core.Tg.LocomotionSeam.IsFlaps</c>):
    /// a standing capsule without <c>MotionParams.PassesFlaps</c> does not enter it. Not solid: a lying one crawls under.
    /// </summary>
    public const byte Flaps = 128;
}
