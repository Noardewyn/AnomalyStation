using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Anomaly.Core.Motion;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// The M2 contracts of the motion core (src/Core/Motion): the project's shape (no references, optimized in every
/// configuration) and the parts the contracts step implemented itself (units, quantization, the tile of a coordinate, the
/// event buffer). The controller and the predictor are M2 P4's, with their own tests in this folder.
/// </summary>
public class MotionContractTests
{
    [Fact]
    public void TheMotionCoreReferencesNothingButTheRuntime()
    {
        Assembly motion = typeof(MotionStep).Assembly;
        string[] references = motion.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain(references, n => n.StartsWith("Anomaly", StringComparison.Ordinal) || n.StartsWith("Godot", StringComparison.Ordinal));
    }

    [Fact]
    public void TheMotionCoreIsCompiledOptimizedInEveryConfiguration()
    {
        // WORKLOG C0 and object-model.md section 8: like the core, so the client's replay equals the server's step bit for bit.
        var debuggable = typeof(MotionStep).Assembly.GetCustomAttribute<DebuggableAttribute>();
        Assert.True(debuggable == null || !debuggable.IsJITOptimizerDisabled);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(127, 0)]
    [InlineData(128, 1)]
    [InlineData(255, 1)]
    [InlineData(256, 1)]
    [InlineData(383, 1)]
    [InlineData(384, 2)]
    [InlineData(-128, 0)]
    [InlineData(-129, -1)]
    public void ATileCoversHalfATileOnEitherSideOfItsCentre(int q, int tile)
    {
        Assert.Equal(tile, MotionMath.TileOf(q));
    }

    [Fact]
    public void QuantizationRoundsHalvesUpAndRoundTripsTileCentres()
    {
        Assert.Equal(256 * 7, MotionMath.Quantize(7f));
        Assert.Equal(1, MotionMath.Quantize(0.5f / 256f));        // half a subunit rounds up
        Assert.Equal(0, MotionMath.Quantize(0.49f / 256f));
        Assert.Equal(-1, MotionMath.Quantize(-1f / 256f));
        Assert.Equal(7.5f, MotionMath.ToTiles(MotionMath.Quantize(7.5f)));
        for (int q = -1000; q <= 1000; q += 7)
            Assert.Equal(q, MotionMath.Quantize(MotionMath.ToTiles(q)));
    }

    [Fact]
    public void SpeedFollowsTgsSlowdownAndTheScale()
    {
        // RUN_DELAY 1.5 ds per tile: a third of a tile per 0.5 ds tick, 6.67 tiles/s, 13.3 m/s at 2 m tiles (D106).
        Assert.Equal(1f / 3f, MotionMath.TilesPerTickFor(1.5f, 1f), 6);
        Assert.Equal(0.125f, MotionMath.TilesPerTickFor(4f, 1f), 6);      // WALK_DELAY 4
        Assert.Equal(1f / 6f, MotionMath.TilesPerTickFor(1.5f, 0.5f), 6); // MOVE_SPEED_SCALE 0.5
        Assert.Equal(1f, MotionMath.TilesPerTickFor(0f, 1f));             // at most a tile per tick at scale 1
        Assert.Equal(MotionMath.TilesPerTickFor(1.5f, 1f), MotionParams.DefaultRun.TilesPerTick);
        Assert.Equal(0.175f, MotionParams.DefaultRun.Radius);
    }

    [Fact]
    public void AStateAtATileStandsOnItsCentre()
    {
        MotionState s = MotionState.AtTile(12, 30, 2, yaw: 16384);
        Assert.Equal((12 * 256, 30 * 256, 2), (s.X, s.Y, s.Z));
        Assert.Equal((12, 30), (s.TileX, s.TileY));
        Assert.Equal(Posture.Standing, s.Posture);
        Assert.Equal(MotionDriver.Grounded, s.Driver);
        Assert.False(s.Slide.Active);
    }

    [Fact]
    public void TheEventBufferGrowsAndClears()
    {
        var events = new MotionEvents();
        for (int i = 0; i < 20; i++)
            events.Add(new MotionEvent(MotionEventKind.Crossed, i, 0, i + 1, 0, MotionDir.East, 0));
        Assert.Equal(20, events.Count);
        Assert.Equal(19, events[19].FromX);
        Assert.Throws<ArgumentOutOfRangeException>(() => events[20]);
        events.Clear();
        Assert.Equal(0, events.Count);
    }
}
