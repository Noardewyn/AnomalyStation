using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

public class GasSensorPlacementTests
{
    [Theory]
    [InlineData("/obj/machinery/air_sensor")]
    [InlineData("/obj/machinery/air_sensor/oxygen_tank")]
    [InlineData("/obj/machinery/air_sensor/supermatter")]
    public void EveryGasSensorSubtypeUsesTheFloorPlane(string path)
    {
        Assert.True(PropKinds.IsFloorHatch(path));
        Assert.True(FloorLayers.TryLift(0, 0, path, out _));
    }
}
