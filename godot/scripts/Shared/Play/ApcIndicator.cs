using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared;

public static class ApcIndicator
{
    // Transform tg's north-pointing arrow into world east/north, then into the camera's screen bearing.
    public static double Angle(Affine transform, double cameraBearing)
        => Math.Atan2(transform.B, transform.E) - cameraBearing;
}
