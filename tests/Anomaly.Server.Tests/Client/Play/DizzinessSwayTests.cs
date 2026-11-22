using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// M8 slice B (DV106): tg's dizziness wobbles the player's view (dizziness.dm: three relative <c>animate</c> steps of the client's
/// pixel_x and pixel_y, 3 ds each, the last back to where it was). The server sends the chain's points as a view turn (the recoil kind),
/// and the first-person camera sways through each point and back (CameraLayer.Recoil).
/// </summary>
public class DizzinessSwayTests
{
    [Fact]
    public void ADizzinessChainSwaysTheViewThroughEachPointAndBack()
    {
        var camera = new CameraLayer();
        camera.Reset(false);
        // facing north: pixel_x to the east is a turn right, pixel_y to the north a turn down (the view pushed back is up)
        camera.Recoil(new[] { (16, 0, 3), (16, -16, 3), (0, 0, 3) }, facingDegrees: 0);
        float half = 1.5f * MathF.PI / 180f; // half a tile: 1.5 of RecoilDegreesPerTile
        camera.Update(0.1, false, 0, false); // (a frame is at most 0.25 s)
        camera.Update(0.1, false, 0, false);
        CameraOffsets first = camera.Update(0.1, false, 0, false);
        Assert.Equal(half, first.YawRadians, 4);
        Assert.Equal(0f, first.PitchRadians, 4);
        camera.Update(0.1, false, 0, false);
        camera.Update(0.1, false, 0, false);
        CameraOffsets second = camera.Update(0.1, false, 0, false);
        Assert.Equal(half, second.YawRadians, 4);
        Assert.Equal(half, second.PitchRadians, 4); // pixel_y -16: the view pushed south, behind the facing: up
        Assert.True(camera.Recoiling);
        for (int i = 0; i < 10; i++)
            camera.Update(0.05, false, 0, false);
        Assert.False(camera.Recoiling);
        CameraOffsets rest = camera.Update(0.05, false, 0, false);
        Assert.Equal(0f, rest.YawRadians);
        Assert.Equal(0f, rest.PitchRadians);
    }

    [Fact]
    public void ANewChainStartsFromWhereTheViewIs()
    {
        var camera = new CameraLayer();
        camera.Reset(false);
        camera.Recoil(new[] { (32, 0, 2), (32, 0, 2), (0, 0, 2) }, facingDegrees: 0);
        camera.Update(0.2, false, 0, false); // at the first point: 3 degrees right
        camera.Recoil(new[] { (0, 0, 2), (16, 0, 2), (0, 0, 2) }, facingDegrees: 0);
        CameraOffsets halfway = camera.Update(0.1, false, 0, false);
        Assert.Equal(1.5f * MathF.PI / 180f, halfway.YawRadians, 4); // from 3 toward 0
    }
}
