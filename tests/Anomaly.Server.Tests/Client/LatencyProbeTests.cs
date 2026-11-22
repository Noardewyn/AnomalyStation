using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>M1 P6: the input-to-motion and input-to-ack latency (WORKLOG question 1).</summary>
public class LatencyProbeTests
{
    [Fact]
    public void TheAckLatencyIsTheTimeFromSendingToTheDeltaThatCoversIt()
    {
        var probe = new LatencyProbe();
        probe.OnInputSent(1, HeldDirs.North, 1.00, 5, 5);
        probe.OnInputSent(2, HeldDirs.None, 1.20, 5, 5);
        probe.OnAck(0, 1.05);
        Assert.Equal(0, probe.Ack.Count);
        probe.OnAck(1, 1.08);
        Assert.Equal(1, probe.Ack.Count);
        Assert.Equal(80.0, probe.Ack.LastMs, 3);
        probe.OnAck(2, 1.31);
        Assert.Equal(2, probe.Ack.Count);
        Assert.Equal(110.0, probe.Ack.LastMs, 3);
        Assert.Equal(95.0, probe.Ack.MeanMs, 3);
        probe.OnAck(2, 1.40); // already acknowledged
        Assert.Equal(2, probe.Ack.Count);
    }

    [Fact]
    public void AFrameAckedWithALaterOneCountsTooAtTheSameMoment()
    {
        var probe = new LatencyProbe();
        probe.OnInputSent(1, HeldDirs.North, 0.0, 5, 5);
        probe.OnInputSent(2, HeldDirs.None, 0.1, 5, 5);
        probe.OnAck(2, 0.3);
        Assert.Equal(2, probe.Ack.Count);
        Assert.Equal(200.0, probe.Ack.LastMs, 3);
    }

    [Fact]
    public void TheMotionLatencyIsTheTimeFromTheKeyPressToTheFirstDrawnMovement()
    {
        var probe = new LatencyProbe();
        probe.OnOwnPose(5, 5, 1.0);
        probe.OnOwnPose(5, 5, 2.0);
        probe.OnInputSent(1, HeldDirs.North, 2.0, 5, 5);
        Assert.True(probe.Measuring);
        for (double t = 2.0; t < 2.3; t += 0.016)
        {
            probe.OnOwnPose(5, 5, t);
            Assert.True(probe.Measuring);
        }
        probe.OnOwnPose(5, 5.02f, 2.31); // below the threshold
        Assert.True(probe.Measuring);
        probe.OnOwnPose(5, 5.1f, 2.33);
        Assert.False(probe.Measuring);
        Assert.Equal(1, probe.Motion.Count);
        Assert.Equal(330.0, probe.Motion.LastMs, 3);
    }

    [Fact]
    public void AWalkThatNeverMovesIsDroppedAfterTheTimeout()
    {
        var probe = new LatencyProbe();
        probe.OnOwnPose(5, 5, 0.0);
        probe.OnInputSent(1, HeldDirs.East, 1.0, 5, 5);
        for (double t = 1.0; t < 4.5; t += 0.05)
            probe.OnOwnPose(5, 5, t);
        Assert.False(probe.Measuring);
        Assert.Equal(0, probe.Motion.Count);
    }

    [Fact]
    public void ReleasingBeforeAnythingMovedCancelsTheMeasurement()
    {
        var probe = new LatencyProbe();
        probe.OnOwnPose(5, 5, 0.0);
        probe.OnInputSent(1, HeldDirs.East, 1.0, 5, 5);
        probe.OnInputSent(2, HeldDirs.None, 1.1, 5, 5);
        Assert.False(probe.Measuring);
        probe.OnOwnPose(6, 5, 1.5);
        Assert.Equal(0, probe.Motion.Count);
    }

    [Fact]
    public void ANewWalkRightAfterAGlideIsNotMeasured()
    {
        // The own entity is still gliding from the last walk: its motion would read as an instant response.
        var probe = new LatencyProbe();
        probe.OnOwnPose(5, 5.0f, 2.00);
        probe.OnOwnPose(5, 5.3f, 2.05);
        probe.OnInputSent(1, HeldDirs.North, 2.10, 5, 5.3f);
        Assert.False(probe.Measuring);
        probe.OnOwnPose(5, 5.6f, 2.15);
        Assert.Equal(0, probe.Motion.Count);
    }

    [Fact]
    public void ChangingDirectionWhileWalkingIsNotAFreshStart()
    {
        var probe = new LatencyProbe();
        probe.OnOwnPose(5, 5, 0.0);
        probe.OnInputSent(1, HeldDirs.North, 1.0, 5, 5);
        probe.OnOwnPose(5, 5.2f, 1.3);
        Assert.Equal(1, probe.Motion.Count);
        probe.OnOwnPose(5, 5.6f, 1.6);
        probe.OnInputSent(2, HeldDirs.East, 1.65, 5, 5.6f); // turns without stopping
        Assert.False(probe.Measuring);
    }

    [Fact]
    public void TheMeanLooksBackOverTheLastSixteen()
    {
        var probe = new LatencyProbe();
        for (uint i = 1; i <= 20; i++)
        {
            probe.OnInputSent(i, HeldDirs.North, i, 0, 0);
            probe.OnAck(i, i + (i <= 4 ? 1.0 : 0.1)); // the first four took 1 s, the others 100 ms
        }
        Assert.Equal(20, probe.Ack.Count);
        Assert.Equal(100.0, probe.Ack.LastMs, 3);
        Assert.Equal(100.0, probe.Ack.MeanMs, 3);
    }
}
