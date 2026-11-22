using Anomaly.Client.Shared;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// M4c slice 7 (D146, D70, DV42): the own view in zero gravity without an engine. The glide of tile-driven things (<see cref="OwnGlide"/>:
/// a chain of steps is one even motion, a step that arrives early or late is not a jump); DV69: the own body is predicted drifting and
/// flying, entering and leaving zero gravity does not jump, a shove's slide plays its share of the tick in progress whatever the keys, the eye does not walk-bob
/// while weightless and floats instead (tg's 2 pixels, 2 seconds a cycle, fading in), the lean follows a sideways glide, and both levels
/// are saved with the other options.
/// </summary>
public class ZeroGViewTests
{
    private sealed class Room : ICollisionView
    {
        public bool IsSolid(int x, int y, int z) => x < 2 || y < 2 || x > 39 || y > 39 || z != 1;
        public TileEdges EdgesOf(int x, int y, int z) => TileEdges.None;
        public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into) { }
    }

    private const double Tick = 0.05;

    private static MotionState Weightless(int x, int y)
    {
        MotionState s = MotionState.AtTile(x, y, 1);
        s.Driver = MotionDriver.ZeroG;
        return s;
    }

    private static MotionState SlidingTo(MotionState s, int x, int y, ushort ticks)
    {
        MotionStep.Displace(ref s, x, y, 1, ticks, 2);
        return s;
    }

    [Fact]
    public void The_glide_moves_at_tgs_glide_size_and_a_chain_of_steps_is_one_even_motion()
    {
        var glide = new OwnGlide();
        glide.Start(10f, 10f, 1);
        glide.Follow(SlidingTo(Weightless(10, 10), 11, 10, 3));
        const float glideSize = 32f / 2.4f; // the CO2 jetpack's 1.2 ds a tile
        var speeds = new List<float>();
        float last = glide.X;
        int tile = 11;
        int[] steps = { 3, 5, 8, 10, 12, 15, 17, 20, 22 }; // tg's 2.4 ticks: 3, 2, 3, 2, 2, ...
        float at20 = 0f;
        for (int frame = 1; frame <= 90; frame++) // 30 ticks at 60 frames a second
        {
            if (frame % 3 == 0 && steps.Contains(frame / 3))
                glide.Follow(SlidingTo(Weightless(tile, 10), ++tile, 10, 3));
            glide.Advance(Tick / 3, glideSize);
            speeds.Add((glide.X - last) * 60f);
            last = glide.X;
            if (frame == 60)
                at20 = glide.X;
        }
        // The space play test (2026-10-15): one even motion at about tg's rate (8.33 tiles a second), no frame still or rushed between the
        // first steps and the last, about a step behind the server's tile, and at rest on the last tile.
        Assert.All(speeds, v => Assert.InRange(v, 0f, 1.2f * 8.34f));
        Assert.All(speeds.Skip(12).Take(48), v => Assert.InRange(v, 0.8f * 8.33f, 1.2f * 8.34f));
        Assert.InRange(at20, 17.0f, 18.34f);
        Assert.Equal(tile, glide.X, 3);
        Assert.Equal(10f, glide.Y);
    }

    [Fact]
    public void An_unknown_glide_size_takes_the_server_slides_rate_and_a_far_target_or_another_level_is_taken_at_once()
    {
        var glide = new OwnGlide();
        glide.Start(10f, 10f, 1);
        MotionState s = SlidingTo(Weightless(10, 10), 11, 10, 4);
        glide.Follow(s);
        for (int i = 0; i < 4; i++)
            glide.Advance(Tick, 0f);
        Assert.InRange(glide.X, 10.5f, 11f); // a tile in 4 ticks, braking onto it
        for (int i = 0; i < 4; i++)
            glide.Advance(Tick, 0f);
        Assert.Equal(11f, glide.X, 3);
        glide.Follow(Weightless(14, 10)); // a teleport
        Assert.Equal(14f, glide.X);
        glide.Follow(SlidingTo(Weightless(14, 10), 15, 10, 4) with { Z = 2 });
        Assert.Equal((15f, 2), (glide.X, glide.Z));
        glide.Start(15f, 10f, 1);
        glide.Follow(SlidingTo(Weightless(15, 10), 17, 10, 3)); // two tiles in one tick (a step and the drift's): one glide over both
        float previous = glide.X;
        for (int i = 0; i < 12; i++)
        {
            glide.Advance(Tick, 0f);
            Assert.InRange(glide.X - previous, 0f, 0.5f);
            previous = glide.X;
        }
        Assert.Equal(17f, glide.X, 3);
    }

    private static OwnState State(MotionState s, uint acked = 0)
        => new(1, acked, true, s, MotionParams.DefaultRun, 1);

    [Fact]
    public void RefusedKeysDoNotAdvanceTheRenderedDriftByAFullTick()
    {
        var p = new OwnPrediction(new Room());
        p.OnOwnState(new OwnState(1, 0, true, Weightless(10, 10),
            MotionParams.DefaultRun with { CanMove = false, DriftX = 26 }, 1));
        float last = p.RenderPosition().X;
        for (int i = 0; i < 60; i++)
        {
            p.Frame(Tick / 3, i % 2 == 0 ? MoveKeys.Forward : MoveKeys.None, 0, _ => { });
            float x = p.RenderPosition().X;
            Assert.InRange(x - last, 0.01f, 0.05f);
            Assert.Equal(10f, p.RenderPosition().Y);
            last = x;
        }
    }

    [Fact]
    public void The_own_body_in_zero_gravity_is_predicted_drifting_and_flying_without_a_jump()
    {
        // DV69: a weightless capsule is predicted as any other: no key moves it at the server's drift, a key the gate allows (a push-off, a
        // jetpack) at its speed, and in and out of zero gravity nothing jumps.
        var p = new OwnPrediction(new Room());
        p.OnOwnState(State(MotionState.AtTile(10, 10, 1)));
        var sent = new List<uint>();
        void Frames(int n, MoveKeys keys)
        {
            for (int i = 0; i < n; i++)
                p.Frame(Tick / 3, keys, -Math.PI / 2, m => sent.Add(m.Frames[^1].Seq)); // Godot's yaw -90 degrees: east
        }
        Frames(3, MoveKeys.None);
        // weightless, drifting east at tg's default inertia (a tile per 5 deciseconds: a tenth of a tile a tick)
        MotionParams drifting = MotionParams.DefaultRun with { CanMove = false, DriftX = 26 };
        p.OnOwnState(new OwnState(1, sent[^1], true, Weightless(10, 10), drifting, 1));
        Assert.True(p.Weightless);
        float last = p.RenderPosition().X, slowest = float.MaxValue, fastest = 0f;
        for (int i = 0; i < 30; i++)
        {
            Frames(1, MoveKeys.None);
            float x = p.RenderPosition().X;
            slowest = Math.Min(slowest, x - last);
            fastest = Math.Max(fastest, x - last);
            last = x;
        }
        Assert.InRange(slowest, 0.01f, 0.05f); // every frame moves, a third of a tick's drift
        Assert.InRange(fastest, 0.01f, 0.05f);
        Assert.InRange(last, 10.9f, 11.1f); // ten ticks at a tenth of a tile

        // a jetpack lets the key move it at its speed, east
        p.OnOwnState(new OwnState(1, sent[^1], true, p.Predicted, drifting with { CanMove = true }, 1));
        float before = p.RenderPosition().X;
        Frames(9, MoveKeys.Forward);
        Assert.InRange(p.RenderPosition().X - before, 0.8f, 1.2f); // three ticks at a third of a tile
        Assert.Equal(0, p.Counters.Corrections);
    }

    [Fact]
    public void A_shoves_slide_plays_its_share_of_the_tick_in_progress_whatever_the_keys()
    {
        var p = new OwnPrediction(new Room());
        p.OnOwnState(State(MotionState.AtTile(10, 10, 1)));
        MotionState shoved = MotionState.AtTile(10, 10, 1);
        MotionStep.Displace(ref shoved, 11, 10, 1, 4);
        p.OnOwnState(State(shoved));
        var xs = new List<float>();
        for (int i = 0; i < 24; i++)
        {
            p.Frame(Tick / 3, i < 12 && i % 6 < 3 ? MoveKeys.Left : MoveKeys.None, 0, null); // a key going down and up does not move the slide
            xs.Add(p.RenderPosition().X);
        }
        for (int i = 1; i < xs.Count; i++)
            Assert.InRange(xs[i] - xs[i - 1], -1e-4f, 0.25f / 3f + 1e-3f); // at most a third of a share a frame
        Assert.Equal(11f, xs[^1], 3);
    }

    private static CameraOffsets Run(CameraLayer layer, double seconds, bool walking, bool floating, float speed = 0f, float sideways = 0f, List<CameraOffsets> all = null)
    {
        CameraOffsets last = default;
        for (double t = 0; t < seconds - 1e-9; t += 1.0 / 120)
        {
            last = layer.Update(1.0 / 120, false, speed, walking, floating, sideways);
            all?.Add(last);
        }
        return last;
    }

    [Fact]
    public void Weightless_the_eye_does_not_walk_bob_and_floats_by_tgs_animation_instead()
    {
        var walk = new CameraLayer();
        var walked = new List<CameraOffsets>();
        Run(walk, 2, walking: true, floating: false, speed: 13f, all: walked);
        Assert.True(walked.Max(o => Math.Abs(o.Right)) > 0.01f, "a walk bobs");

        var layer = new CameraLayer();
        var floated = new List<CameraOffsets>();
        Run(layer, 1, walking: true, floating: true, speed: 13f); // the fade in (1 s); the stride's bob never starts
        Run(layer, 2, walking: true, floating: true, speed: 13f, all: floated);
        Assert.Equal(0f, floated.Max(o => Math.Abs(o.Right)), 4); // no sideways stride
        float amplitude = CameraLayer.FloatBobMetres / 2f;
        Assert.Equal(CameraLayer.StandingEye + amplitude, floated.Max(o => o.Up), 3); // tg's 2 pixels, about the eye
        Assert.Equal(CameraLayer.StandingEye - amplitude, floated.Min(o => o.Up), 3);
        Assert.Equal(0f, floated.Max(o => Math.Abs(o.RollRadians)), 4); // the roll is off by default
        // One cycle in 2 seconds: the eye is back where it was after 2 s.
        Assert.Equal(floated[0].Up, floated[^1].Up, 2);

        // Weight again: the float fades out over a second.
        Run(layer, 0.5, walking: false, floating: false);
        Assert.InRange(layer.FloatAmount, 0.45f, 0.55f);
        CameraOffsets settled = Run(layer, 0.6, walking: false, floating: false);
        Assert.Equal(0f, layer.FloatAmount);
        Assert.Equal(CameraLayer.StandingEye, settled.Up, 4);
    }

    [Fact]
    public void The_levels_switch_the_float_and_the_lean_off_halve_them_or_give_them_whole()
    {
        var off = new CameraLayer { FloatBob = ZeroGLevel.Off };
        var still = new List<CameraOffsets>();
        Run(off, 3, walking: false, floating: true, all: still);
        Assert.Equal(CameraLayer.StandingEye, still.Max(o => o.Up), 4);
        Assert.Equal(CameraLayer.StandingEye, still.Min(o => o.Up), 4);

        var half = new CameraLayer { FloatBob = ZeroGLevel.Reduced };
        var halved = new List<CameraOffsets>();
        Run(half, 1, walking: false, floating: true);
        Run(half, 2, walking: false, floating: true, all: halved);
        Assert.Equal(CameraLayer.StandingEye + CameraLayer.FloatBobMetres / 4f, halved.Max(o => o.Up), 3);

        // The lean: a glide to the right banks the view right (Godot's roll about the view axis is negative), the reduced level half as far.
        var lean = new CameraLayer { FloatBob = ZeroGLevel.Off, Roll = ZeroGLevel.On };
        CameraOffsets right = Run(lean, 2, walking: false, floating: true, sideways: 10f);
        Assert.Equal(-CameraLayer.MaxLeanRadians, right.RollRadians, 3);
        var leanHalf = new CameraLayer { FloatBob = ZeroGLevel.Off, Roll = ZeroGLevel.Reduced };
        CameraOffsets left = Run(leanHalf, 2, walking: false, floating: true, sideways: -20f);
        Assert.Equal(CameraLayer.MaxLeanRadians / 2f, left.RollRadians, 3);
        CameraOffsets grounded = Run(lean, 3, walking: false, floating: false, sideways: 10f);
        Assert.Equal(0f, grounded.RollRadians, 3); // never with weight
    }

    [Fact]
    public void The_zero_g_levels_are_saved_with_the_options()
    {
        var defaults = new PlayOptions();
        Assert.Equal(ZeroGLevel.On, defaults.ZeroGBob);
        Assert.Equal(ZeroGLevel.Off, defaults.ZeroGRoll);
        var options = new PlayOptions { ZeroGBob = ZeroGLevel.Reduced, ZeroGRoll = ZeroGLevel.On };
        string text = options.ToText();
        Assert.Contains("zero_g_bob=reduced\n", text);
        Assert.Contains("zero_g_roll=on\n", text);
        PlayOptions back = PlayOptions.Parse(text);
        Assert.Equal((ZeroGLevel.Reduced, ZeroGLevel.On), (back.ZeroGBob, back.ZeroGRoll));
        var errors = new List<string>();
        PlayOptions bad = PlayOptions.Parse("zero_g_bob=loud\nzero_g_roll=off\n", errors);
        Assert.Single(errors);
        Assert.Equal((ZeroGLevel.On, ZeroGLevel.Off), (bad.ZeroGBob, bad.ZeroGRoll));
        Assert.True(new PlayOptions().TrySet("zero_g_bob", "off", out _));
    }
}
