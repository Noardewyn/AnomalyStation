using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// M2 P8: the glue around the predictor (scope 2d): one frame per tick of the input clock with four-frame redundancy, the
/// camera's partial step (a key shows on the next frame and nothing jumps at the tick's end or on a release), the reconcile
/// path on every private block, the input clock's nudge. The controller and the predictor themselves are M2 P4's (tests in
/// tests/Anomaly.Core.Tests/Motion); here they run behind this class on a plain open room.
/// </summary>
public class OwnPredictionTests
{
    // A walled room 40 x 40 tiles, nothing else.
    private sealed class Room : ICollisionView
    {
        public bool IsSolid(int x, int y, int z) => x < 2 || y < 2 || x > 39 || y > 39 || z != 1;
        public TileEdges EdgesOf(int x, int y, int z) => TileEdges.None;
        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into) { }
    }

    // D325: north stairs at (10, 12, 1) climb to (10, 13, 2), two open levels walled at the same edges
    private sealed class Stairs : ICollisionView
    {
        public bool IsSolid(int x, int y, int z) => x < 2 || y < 2 || x > 39 || y > 39 || z is < 1 or > 2;
        public TileEdges EdgesOf(int x, int y, int z) => TileEdges.None;
        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into) { }
        public int LevelShift(int x, int y, int z, byte dir) => x == 10 && y == 12 && z == 1 && dir == 1 ? 1 : 0;
    }

    [Theory]
    [InlineData(60)]
    [InlineData(144)]
    [InlineData(240)]
    public void TheDrawnPointPastTheTopStairIsOnTheLevelAbove(double fps)
    {
        // DV370: the render-only partial step climbs as the tick's end will, so the eye never drops a level under the flight's top
        var p = new OwnPrediction(new Stairs());
        p.OnOwnState(State(MotionState.AtTile(10, 10, 1)));
        bool climbed = false;
        Run(p, 2.0, fps, MoveKeys.Forward, 0, _ => { }, _ =>
        {
            (float _, float y, int z) = p.RenderPosition();
            if ((int)MathF.Round(y) >= 13)
                Assert.Equal(2, z);
            climbed |= z == 2;
        });
        Assert.True(climbed);
        Assert.Equal(2, p.Predicted.Z);
    }

    private static OwnState State(MotionState s, uint acked = 0, int depth = 1, MotionParams? p = null)
        => new(1, acked, true, s, p ?? MotionParams.DefaultRun, (byte)depth);

    private static OwnPrediction Started(out Room room, int tileX = 10, int tileY = 10)
    {
        room = new Room();
        var prediction = new OwnPrediction(room);
        prediction.OnOwnState(State(MotionState.AtTile(tileX, tileY, 1)));
        return prediction;
    }

    private static void Run(OwnPrediction p, double seconds, double fps, MoveKeys keys, double yaw, Action<MotionInputMessage> send, Action<double> perFrame = null)
    {
        double dt = 1.0 / fps;
        for (double t = 0; t < seconds - 1e-9; t += dt)
        {
            p.Frame(dt, keys, yaw, send);
            perFrame?.Invoke(t);
        }
    }

    [Fact]
    public void NothingIsSentBeforeTheServerHasToldTheState()
    {
        var p = new OwnPrediction(new Room());
        Assert.False(p.Active);
        int sent = 0;
        Run(p, 1.0, 60, MoveKeys.Forward, 0, _ => sent++);
        Assert.Equal(0, sent);
        p.OnOwnState(State(MotionState.AtTile(5, 5, 1)));
        Assert.True(p.Active);
        Assert.False(p.TileDriven);
        Assert.Equal(0u, p.NewestSeq);
    }

    [Fact]
    public void AGhostSendsTheWishWithoutPredictingAndTheCapsuleComesBackFromTheServersState()
    {
        // D151: a mob that is not a capsule (a ghost) is moved by tg's keyLoop on the server: frames still carry the wish
        var p = new OwnPrediction(new Room());
        int sent = 0;
        p.OnOwnState(new OwnState(1, 0, false, default, default, 0));
        Assert.True(p.Active);
        Assert.True(p.TileDriven);
        Run(p, 1.0, 60, MoveKeys.Forward, 0, _ => sent++);
        Assert.True(sent >= 18, "frames sent: " + sent);
        uint seq = p.NewestSeq;
        // back in a body (Re-enter Corpse): the prediction starts from the server's state and the seq goes on
        p.OnOwnState(State(MotionState.AtTile(5, 5, 1)));
        Assert.False(p.TileDriven);
        Assert.Equal(MotionState.AtTile(5, 5, 1).X, p.Predicted.X);
        Run(p, 0.2, 60, MoveKeys.None, 0, _ => sent++);
        Assert.True(p.NewestSeq > seq);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    [InlineData(240)]
    public void OneFramePerTickWhateverTheFrameRateWithFourFrameRedundancy(double fps)
    {
        OwnPrediction p = Started(out _);
        var messages = new List<List<MotionInput>>();
        Run(p, 10.0, fps, MoveKeys.Forward, 0, m => messages.Add(m.Frames.ToList()));
        // 20 ticks a second for 10 s, within a tick or two of the clock's phase.
        Assert.InRange(messages.Count, 196, 204);
        Assert.Equal((uint)messages.Count, p.NewestSeq);
        for (int i = 0; i < messages.Count; i++)
        {
            List<MotionInput> frames = messages[i];
            Assert.InRange(frames.Count, 1, MotionPredictor.Redundancy);
            Assert.Equal((uint)(i + 1), frames[^1].Seq);
            for (int f = 1; f < frames.Count; f++)
                Assert.Equal(frames[f - 1].Seq + 1, frames[f].Seq);
            Assert.Equal(Math.Min(i + 1, MotionPredictor.Redundancy), frames.Count);
        }
    }

    [Fact]
    public void TheFramesCarryTheWorldSpaceWishAndTheViewYaw()
    {
        OwnPrediction p = Started(out _);
        var frames = new List<MotionInput>();
        // Facing west (+90 degrees in Godot's yaw): forward is west.
        Run(p, 1.0, 60, MoveKeys.Forward, Math.PI / 2, m => frames.Add(m.Frames[^1]));
        MotionInput steady = frames[^1];
        Assert.Equal(-127, steady.MoveX);
        Assert.Equal(0, steady.MoveY);
        Assert.Equal((ushort)49152, steady.Yaw); // the compass bearing of west
        // A diagonal is as fast as a straight line.
        frames.Clear();
        Run(p, 1.0, 60, MoveKeys.Forward | MoveKeys.Right, 0, m => frames.Add(m.Frames[^1]));
        MotionInput d = frames[^1];
        Assert.Equal(d.MoveX, d.MoveY);
        Assert.InRange(Math.Sqrt(d.MoveX * d.MoveX + d.MoveY * d.MoveY), 126, 128);
    }

    [Fact]
    public void AKeyHeldForAPartOfATickIsAFractionOfAStep()
    {
        OwnPrediction p = Started(out _);
        var frames = new List<MotionInput>();
        // Idle until the first frame, then hold for exactly 2 of 5 render frames in a tick (at 100 fps, 5 frames per tick).
        Run(p, 0.5, 100, MoveKeys.None, 0, m => frames.Add(m.Frames[^1]));
        frames.Clear();
        for (int tick = 0; tick < 10; tick++)
        {
            for (int f = 0; f < 5; f++)
                p.Frame(0.01, f < 2 ? MoveKeys.Forward : MoveKeys.None, 0, m => frames.Add(m.Frames[^1]));
        }
        Assert.NotEmpty(frames);
        foreach (MotionInput frame in frames.Skip(1))
            Assert.InRange((int)frame.MoveY, 40, 62); // about 0.4 of 127
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void TheCameraNeverJumpsBackOrForwardMoreThanTheSpeedAllows(double fps)
    {
        OwnPrediction p = Started(out _, 10, 10);
        double dt = 1.0 / fps;
        // Keys held and released at odd moments, including in the middle of ticks.
        bool[] held = new bool[(int)(8 * fps)];
        var random = new Random(11);
        bool down = false;
        for (int i = 0; i < held.Length; i++)
        {
            if (random.Next(0, 17) == 0)
                down = !down;
            held[i] = down;
        }
        float last = float.NaN;
        float worstStep = 0;
        float worstBack = 0;
        for (int i = 0; i < held.Length; i++)
        {
            p.Frame(dt, held[i] ? MoveKeys.Forward : MoveKeys.None, 0, null);
            (float x, float y, _) = p.RenderPosition();
            if (!float.IsNaN(last))
            {
                float step = y - last;
                worstStep = Math.Max(worstStep, step);
                worstBack = Math.Min(worstBack, step);
            }
            last = y;
        }
        // Full speed is a third of a tile per tick: 0.0167 tile per frame at 1/3 / (fps/20)... plus the tick's rounding.
        float maxPerFrame = (float)(MotionMath.TilesPerTickFor(1.5f, 1f) * 20.0 / fps);
        Assert.True(worstStep <= maxPerFrame * 1.35f, $"a frame moved {worstStep} tiles, the speed allows {maxPerFrame}");
        Assert.True(worstBack > -0.004f, $"the camera went back by {worstBack} tiles in a frame");
    }

    [Fact]
    public void AKeyPressedMidTickMovesTheCameraOnTheNextFrame()
    {
        OwnPrediction p = Started(out _, 10, 10);
        Run(p, 0.5, 144, MoveKeys.None, 0, null);
        (_, float y0, _) = p.RenderPosition();
        p.Frame(1.0 / 144, MoveKeys.Forward, 0, null);
        (_, float y1, _) = p.RenderPosition();
        Assert.True(y1 > y0 + 0.001f, $"{y0} -> {y1}");
        // And the server-side state has not moved yet: the partial step is render-only.
        Assert.Equal(10 * 256, p.Predicted.Y);
    }

    [Fact]
    public void TheCameraEqualsThePredictionWhenStoppedAndAfterTheServerAgrees()
    {
        OwnPrediction p = Started(out Room room, 10, 10);
        var sent = new List<MotionInputMessage>();
        var server = MotionState.AtTile(10, 10, 1);
        var frames = new Queue<MotionInput>();
        double now = 0;
        uint queued = 0;
        for (int i = 0; i < 400; i++)
        {
            now += 1.0 / 60;
            MoveKeys keys = now < 3 ? MoveKeys.Forward | MoveKeys.Right : MoveKeys.None;
            p.Frame(1.0 / 60, keys, 0.4, m =>
            {
                foreach (MotionInput f in m.Frames)
                {
                    if (f.Seq > queued)
                    {
                        frames.Enqueue(f);
                        queued = f.Seq;
                    }
                }
            });
            // The server: applies a frame a tick later than it arrived, answers with its state two ticks behind.
            if (i % 3 == 0 && frames.Count > 1)
            {
                MotionInput frame = frames.Dequeue();
                MotionStep.Step(ref server, frame, MotionParams.DefaultRun, room, null);
                p.OnOwnState(State(server, frame.Seq));
            }
        }
        PredictionCounters c = p.Counters;
        Assert.True(c.Reconciliations > 50);
        Assert.Equal(0, c.Corrections);
        Assert.Equal(0, c.Mismatches);
        (float x, float y, _) = p.RenderPosition();
        Assert.Equal(MotionMath.ToTiles(p.Predicted.X), x, 4);
        Assert.Equal(MotionMath.ToTiles(p.Predicted.Y), y, 4);
    }

    [Fact]
    public void AnUnexpectedServerStateIsACorrectionThatIsReportedAndSmoothed()
    {
        OwnPrediction p = Started(out _, 10, 10);
        Run(p, 1.0, 60, MoveKeys.None, 0, null);
        var seen = new List<Reconciliation>();
        p.Corrected += seen.Add;
        // The server says the capsule is half a tile east of where the prediction has it.
        MotionState moved = MotionState.AtTile(10, 10, 1);
        moved.X += 128;
        p.OnOwnState(State(moved, p.NewestSeq));
        Assert.Single(seen);
        Assert.Equal(CorrectionKind.Smoothed, seen[0].Kind);
        Assert.InRange(seen[0].ErrorTiles, 0.45f, 0.55f);
        (float vx, _) = p.VisualOffset;
        Assert.True(Math.Abs(vx) > 0.2f);
        // The camera starts from where it was and slides over about 100 ms.
        (float x0, _, _) = p.RenderPosition();
        Assert.InRange(x0, 9.95f, 10.1f);
        Run(p, 0.2, 60, MoveKeys.None, 0, null);
        (float x1, _, _) = p.RenderPosition();
        Assert.Equal(10.5f, x1, 2);
        Assert.Equal(1, p.Counters.Corrections);
        // A teleport (another level, far away) snaps.
        p.OnOwnState(State(MotionState.AtTile(30, 30, 1), p.NewestSeq));
        Assert.Equal(1, p.Counters.Snaps);
    }

    [Fact]
    public void TheInputClockSpeedsUpWhenTheServersBufferIsEmptyAndSlowsWhenItIsFull()
    {
        OwnPrediction p = Started(out _, 10, 10);
        for (int i = 0; i < 30; i++)
            p.OnOwnState(State(MotionState.AtTile(10, 10, 1), 0, depth: 0));
        Assert.True(p.ClockRate > 1.01, "an empty buffer: " + p.ClockRate);
        Assert.True(p.ClockRate <= 1.0 + InputClock.MaxNudge + 1e-9);
        for (int i = 0; i < 60; i++)
            p.OnOwnState(State(MotionState.AtTile(10, 10, 1), 0, depth: 3));
        Assert.True(p.ClockRate < 0.99, "a full buffer: " + p.ClockRate);
        Assert.True(p.ClockRate >= 1.0 - InputClock.MaxNudge - 1e-9);
        Assert.Equal(3, p.BufferDepth);
    }

    [Fact]
    public void TheOwnCapsuleIsLeftOutOfItsOwnCollisionView()
    {
        var inner = new CapsuleView(new CapsuleObstacle(1, 2560, 2560, 0.175f), new CapsuleObstacle(2, 2600, 2560, 0.175f), new CapsuleObstacle(3, 5000, 2560, 0.175f));
        var view = new OwnExcludingView(inner, () => 1);
        var into = new List<CapsuleObstacle>();
        view.GatherCapsules(2560, 2560, 1, 1000, 0, into);
        Assert.Equal(new long[] { 2, 3 }, into.Select(c => c.Id));
        // What the caller already had in the list stays.
        into.Clear();
        into.Add(new CapsuleObstacle(1, 0, 0, 0.1f));
        view.GatherCapsules(2560, 2560, 1, 1000, 0, into);
        Assert.Equal(new long[] { 1, 2, 3 }, into.Select(c => c.Id));
        Assert.True(view.IsSolid(0, 0, 1));
    }

    private sealed class CapsuleView : ICollisionView
    {
        private readonly CapsuleObstacle[] _all;
        public CapsuleView(params CapsuleObstacle[] all) => _all = all;
        public bool IsSolid(int x, int y, int z) => x < 1;
        public TileEdges EdgesOf(int x, int y, int z) => TileEdges.None;
        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
        {
            foreach (CapsuleObstacle c in _all)
            {
                if (c.Id != exclude)
                    into.Add(c);
            }
        }
    }
}
