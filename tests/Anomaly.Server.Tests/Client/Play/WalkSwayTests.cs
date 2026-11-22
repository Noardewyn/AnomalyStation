using Anomaly.Client.Shared;
using Xunit;

namespace Anomaly.Server.Tests.Client.Play;

public class WalkSwayTests
{
    private static WalkSway Walk(double metresPerSecond, double seconds)
    {
        var sway = new WalkSway();
        const double dt = 1.0 / 60;
        for (double t = 0; t < seconds; t += dt)
            sway.Step(metresPerSecond * dt, dt, WalkSway.CapsuleStepMetres);
        return sway;
    }

    [Fact]
    public void AtRestNothingMoves()
    {
        WalkSway sway = Walk(0, 1);
        Assert.Equal(0, sway.Roll);
        Assert.Equal(0, sway.Lift);
    }

    [Fact]
    public void WalkingRocksBothWaysWithinTheTilt()
    {
        var sway = new WalkSway();
        double min = 0, max = 0;
        const double dt = 1.0 / 60;
        for (int i = 0; i < 120; i++)
        {
            sway.Step(WalkSway.WalkMetresPerSecond * dt, dt, WalkSway.CapsuleStepMetres);
            min = Math.Min(min, sway.Roll);
            max = Math.Max(max, sway.Roll);
            Assert.True(sway.Lift >= 0 && sway.Lift <= WalkSway.LiftMetres * 1.0001);
        }
        Assert.True(max > WalkSway.RollRadians * 0.8 && min < -WalkSway.RollRadians * 0.8);
    }

    [Fact]
    public void RunningRocksFurtherThanWalkingButIsCapped()
    {
        var sway = new WalkSway();
        double peak = 0;
        const double dt = 1.0 / 60;
        for (int i = 0; i < 120; i++)
        {
            sway.Step(13.0 * dt, dt, WalkSway.CapsuleStepMetres);
            peak = Math.Max(peak, Math.Abs(sway.Roll));
        }
        Assert.True(peak > WalkSway.RollRadians * 1.2 && peak <= WalkSway.RollRadians * WalkSway.RunGain * 1.0001);
    }

    [Fact]
    public void StoppingSettlesAndATeleportIsNoStep()
    {
        WalkSway sway = Walk(WalkSway.WalkMetresPerSecond, 1);
        for (int i = 0; i < 60; i++)
            sway.Step(0, 1.0 / 60, WalkSway.CapsuleStepMetres);
        Assert.Equal(0, sway.Roll);
        sway.Step(10, 1.0 / 60, WalkSway.CapsuleStepMetres); // five tiles in a frame
        Assert.Equal(0, sway.Roll);
    }

    [Fact]
    public void AHeardFootstepLandsTheStep()
    {
        // unheard, the first footfall comes a footstep spacing into the walk; a footstep heard three quarters of the way in lands it soon after
        Assert.InRange(FirstFootfall(landAt: null), WalkSway.CapsuleStepMetres * 0.98, WalkSway.CapsuleStepMetres * 1.02);
        Assert.InRange(FirstFootfall(landAt: WalkSway.CapsuleStepMetres * 0.75), WalkSway.CapsuleStepMetres * 0.75, WalkSway.CapsuleStepMetres * 0.75 + 0.5);
    }

    private static double FirstFootfall(double? landAt)
    {
        var sway = new WalkSway();
        const double dt = 1.0 / 240;
        double metres = 0;
        bool landed = false, rose = false;
        while (metres < 10)
        {
            sway.Step(WalkSway.WalkMetresPerSecond * dt, dt, WalkSway.CapsuleStepMetres);
            metres += WalkSway.WalkMetresPerSecond * dt;
            if (landAt is double at && !landed && metres >= at)
            {
                sway.Land();
                landed = true;
            }
            rose |= sway.Roll > WalkSway.RollRadians * 0.1;
            if (rose && sway.Roll <= 0)
                return metres;
        }
        return double.NaN;
    }

    [Fact]
    public void ALandedWalkKeepsTheFootstepsCadence()
    {
        // walking on, the footfalls (roll back to 0) come every footstep spacing
        var sway = new WalkSway();
        const double dt = 1.0 / 240;
        double metres = 0, lastSign = 0;
        var falls = new List<double>();
        for (int i = 0; i < 2400; i++)
        {
            sway.Step(WalkSway.WalkMetresPerSecond * dt, dt, WalkSway.CapsuleStepMetres);
            metres += WalkSway.WalkMetresPerSecond * dt;
            double sign = Math.Sign(sway.Roll);
            if (sign != 0 && lastSign != 0 && sign != lastSign)
                falls.Add(metres);
            if (sign != 0)
                lastSign = sign;
        }
        Assert.True(falls.Count >= 3);
        for (int i = 1; i < falls.Count; i++)
            Assert.InRange(falls[i] - falls[i - 1], WalkSway.CapsuleStepMetres * 0.98, WalkSway.CapsuleStepMetres * 1.02);
    }

    [Fact]
    public void HeardFootstepsTeachTheStepLength()
    {
        // the drawn path between footsteps is 3.2 m, not the 2.86 given: after a few heard steps the foot is down when each one sounds
        var sway = new WalkSway();
        const double dt = 1.0 / 120, heardEvery = 3.2;
        double sinceHeard = 0, lastRoll = double.NaN;
        for (int i = 0; i < 120 * 12; i++)
        {
            sway.Step(WalkSway.WalkMetresPerSecond * dt, dt, WalkSway.CapsuleStepMetres);
            sinceHeard += WalkSway.WalkMetresPerSecond * dt;
            if (sinceHeard >= heardEvery)
            {
                sinceHeard -= heardEvery;
                lastRoll = sway.Roll;
                sway.Land();
            }
        }
        Assert.True(Math.Abs(lastRoll) < WalkSway.RollRadians * 0.1, $"roll at the last footstep {lastRoll}");
    }
}

