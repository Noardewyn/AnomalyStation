using Anomaly.Client.Shared.Light;

namespace Anomaly.Server.Tests.Client.Light;

/// <summary>D288: the own flashlight's beam drifts a little standing, swings toward a turn and settles back, and sways more running than walking.</summary>
public class FlashlightSwayTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void StandingItDriftsWithinTheIdleSize()
    {
        var sway = new FlashlightSway();
        for (int i = 0; i < 600; i++)
        {
            (float yaw, float pitch) = sway.Update(i * Frame, Frame, 0f, 0f, 0.0, 0f);
            Assert.InRange(yaw, -FlashlightSway.IdleDegrees, FlashlightSway.IdleDegrees);
            Assert.InRange(pitch, -FlashlightSway.IdleDegrees, FlashlightSway.IdleDegrees);
        }
    }

    [Fact]
    public void ATurnSwingsItTowardTheTurnAndItSettlesBackWhenTheTurnStops()
    {
        var sway = new FlashlightSway();
        float turning = 0f;
        for (int i = 0; i < 30; i++)
            turning = sway.Update(0.0, Frame, 90f, 0f, 0.0, 0f).Yaw; // half a second turning right at 90 degrees a second
        Assert.True(turning > 2f, $"swung {turning}");
        Assert.True(turning <= FlashlightSway.TurnMaxDegrees + FlashlightSway.IdleDegrees);
        float settled = turning;
        for (int i = 0; i < 120; i++)
            settled = sway.Update(0.0, Frame, 0f, 0f, 0.0, 0f).Yaw;
        float idle = new FlashlightSway().Update(0.0, Frame, 0f, 0f, 0.0, 0f).Yaw; // the drift alone at the same time
        Assert.InRange(settled - idle, -0.1f, 0.1f);
        Assert.True(new FlashlightSway().Update(0.0, Frame, 1e6f, 0f, 0.0, 0f).Yaw <= FlashlightSway.TurnMaxDegrees, "a snap of the view swings it no further than the most");
    }

    [Fact]
    public void RunningSwaysItMoreThanWalking()
    {
        float Widest(float stride)
        {
            var sway = new FlashlightSway();
            float widest = 0f;
            for (int i = 0; i < 120; i++)
                widest = Math.Max(widest, Math.Abs(sway.Update(0.0, Frame, 0f, 0f, i * 0.1, stride).Yaw));
            return widest;
        }

        Assert.True(Widest(1f) > 0.9f * FlashlightSway.WalkDegrees);
        Assert.True(Widest(2.5f) > 2f * Widest(1f));
    }
}
