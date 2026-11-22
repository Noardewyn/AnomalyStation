using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 21 (M5x slice 2, docs/ai/specs/protocol.md section 30): the CameraShake message (server to client, State), tg's
/// shake_camera path of the player's view. Bytes, round trips, refusals, and the replica's event.
/// </summary>
public class ProtocolV21Tests
{
    private static CameraShakeMessage Shake(params (int X, int Y, int T)[] steps)
    {
        var message = new CameraShakeMessage();
        foreach ((int x, int y, int t) in steps)
            message.Steps.Add(new CameraShakeStep(x, y, t));
        return message;
    }

    [Fact]
    public void TheVersionIsTwentyOneAndTheShakeIsMessageEighteen()
    {
        Assert.True(ProtocolVersion.Current >= 21);
        Assert.Equal((byte)18, (byte)MessageId.CameraShake);
        Assert.True(WireReader.IsKnownMessageId(18));
        Assert.True(WireReader.IsKnownMessageId(19)); // version 38: SayModal
    }

    [Fact]
    public void AShakeHasItsBytesAndRoundTrips()
    {
        // version 34's kind 0 (a shake); count 2; (5, -3, 1): zigzag 10, 5, 1; (0, 0, 3)
        Assert.Equal(new byte[] { 18, 0, 2, 10, 5, 1, 0, 0, 3 }, Codec.Encode(Shake((5, -3, 1), (0, 0, 3))));
        var rng = new Random(21);
        for (int i = 0; i < 50; i++)
        {
            var message = new CameraShakeMessage();
            for (int k = rng.Next(1, Limits.MaxCameraShakeSteps + 1); k > 0; k--)
                message.Steps.Add(new CameraShakeStep(rng.Next(-Limits.MaxCameraShakePixels, Limits.MaxCameraShakePixels + 1), rng.Next(-Limits.MaxCameraShakePixels, Limits.MaxCameraShakePixels + 1), rng.Next(1, Limits.MaxCameraShakeStepTime + 1)));
            byte[] bytes = Codec.Encode(message);
            Assert.True(bytes.Length <= Limits.MaxCameraShakeBytes);
            Assert.True(Codec.TryDecode(bytes, out CameraShakeMessage back, out ProtocolError error), error.ToString());
            Assert.Equal(message.Steps, back.Steps);
            Assert.Equal(bytes, Codec.Encode(back));
        }
    }

    [Fact]
    public void EmptyOverlongAndOutOfRangeShakesAreRefused()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(Shake()));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Shake((0, 0, 0))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Shake((Limits.MaxCameraShakePixels + 1, 0, 1))));
        Assert.False(Codec.TryDecode(new byte[] { 18, 0, 0 }, out CameraShakeMessage _, out _));
        Assert.False(Codec.TryDecode(new byte[] { 18, 0, 1, 0, 0, 0 }, out CameraShakeMessage _, out _)); // a step of no time
        Assert.False(Codec.TryDecode(new byte[] { 18, 0, 1, 0, 0, 1, 7 }, out CameraShakeMessage _, out ProtocolError trailing));
        Assert.Equal(ProtocolError.TrailingBytes, trailing);
    }

    [Fact]
    public void TheReplicaRaisesTheShakeAfterASnapshotOnly()
    {
        var replica = new ReplicaWorld();
        Assert.Throws<ProtocolException>(() => replica.Apply(Shake((1, 1, 1))));
        var grid = new MapGrid { Tick = 5, Width = 8, Height = 8, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor");
        grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        replica.Apply(grid);
        replica.Apply(new Snapshot { Tick = 6 });
        CameraShakeMessage seen = null;
        replica.CameraShakeReceived += m => seen = m;
        Assert.True(replica.TryApply(Codec.Encode(Shake((4, 4, 2))), out MessageId id, out _));
        Assert.Equal(MessageId.CameraShake, id);
        Assert.Equal(new CameraShakeStep(4, 4, 2), Assert.Single(seen.Steps));
        Assert.Equal(1, replica.CameraShakes);
    }
}
