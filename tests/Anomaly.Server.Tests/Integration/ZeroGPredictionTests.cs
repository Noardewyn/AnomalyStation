using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M4c slice 7: the own view in zero gravity, measured as <see cref="RealServerPredictionTests"/> measures walking, on
/// MetaStation through the real client stack (<see cref="RealHeadlessWorld"/>), at 0 and 100 ms one way. A walk in the
/// arrivals corridor (gravity: the predicted capsule) is the reference; then, suited and with a CO2 jetpack outside the
/// Auxiliary Airlock (39,116, the jetpack gameplay test's spot), a push-off drift east with the stabilizer off and a
/// stabilized flight over the jetpack test's path ((44,113), (44,116), (40,116)). For each phase: the predictor's corrections,
/// snaps and mismatches, the key-to-first-drawn-motion time (<see cref="LatencyProbe"/>), the own view's lag behind the
/// server's capsule (the mean ticks between the server's centre and the drawn centre entering each tile) and the drawn speed
/// per tick (its largest value and its largest change between two ticks while moving: a snap or a jerk shows there).
/// </summary>
public class ZeroGPredictionTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public ZeroGPredictionTests(ITestOutputHelper output) => _output = output;

    [MetaServerFact]
    public void The_own_view_in_zero_gravity_at_0_ms() => Measure(0);

    [MetaServerFact]
    public void The_own_view_in_zero_gravity_at_100_ms() => Measure(2);

    private void Measure(int latencySteps)
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 17, TimeoutSteps = 6000, MinLatencySteps = latencySteps, MaxLatencySteps = latencySteps },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant", inputBufferTarget: latencySteps > 0 ? 2 : 1);
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount;
        string ms = $"{latencySteps * 50} ms";

        // The reference: a walk east along the arrivals corridor from the spawn (40,122), with gravity.
        Phase walk = Begin(w, p, "walk (gravity)");
        Hold(w, p, walk, (1, 0), Second);
        Rest(w, p, walk, Second / 2);
        Hold(w, p, walk, (-1, 0), Second);
        Rest(w, p, walk, Second / 2);
        Report(ms, walk.End());
        Assert.Equal((0L, 0L, 0L), (walk.Counters.Corrections, walk.Counters.Snaps, walk.Counters.Mismatches));
        Assert.Equal(0f, walk.MaxFloat);
        Assert.Equal(MotionDriver.Grounded, w.ServerState(p).Driver);

        SuitUp(w, p, me, out obj_item_tank_jetpack jet, out long toggle, out long stabilization);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(39, 116, z))));
        w.Run(Second);
        Assert.Equal(MotionDriver.ZeroG, w.ServerState(p).Driver);
        ClickScreen(p, toggle);
        w.Run(Second);
        Assert.Equal(1f, In(w, () => jet.on));
        Assert.Equal(0f, In(w, () => jet.stabilize));

        // A push east with the stabilizer off: the player drifts on after the key is let go; the stabilizer stops it.
        Phase drift = Begin(w, p, "push-off drift");
        Hold(w, p, drift, (1, 0), 3);
        Rest(w, p, drift, Second);
        ClickScreen(p, stabilization);
        Rest(w, p, drift, Second);
        Assert.Equal(1f, In(w, () => jet.stabilize));
        Report(ms, drift.End());
        AssertSmoothWeightless(drift);
        _output.WriteLine($"{ms}: drift path " + Path(drift.Rows));
        Assert.Null(In(w, () => me.drift_handler));

        // A stabilized flight over the jetpack test's path, resting at each turn.
        Phase flight = Begin(w, p, "jetpack flight");
        foreach ((int X, int Y) target in new[] { (44, 113), (44, 116), (40, 116) })
        {
            (int X, int Y) from = Tile(w, me);
            FlyTo(w, p, me, flight, target, latencySteps > 0 ? 1 : 0);
            Rest(w, p, flight, Second);
            _output.WriteLine($"{ms}: flew {from} -> {Tile(w, me)} (target {target})");
            (int X, int Y) at = Tile(w, me);
            Assert.True(Math.Abs(at.X - target.X) <= 1 + latencySteps && Math.Abs(at.Y - target.Y) <= 1 + latencySteps, $"flew to {at}, the target {target}");
        }
        Report(ms, flight.End());
        AssertSmoothWeightless(flight);
        _output.WriteLine($"{ms}: flight path " + Path(flight.Rows));

        MotionState server = w.ServerState(p);
        Assert.Equal((server.X, server.Y), (p.Session.Prediction.Predicted.X, p.Session.Prediction.Predicted.Y));

        // A drifting item (tg's newtonian_move east from the player's tile): the client draws it by its glide (EntityGlide), tile by tile.
        var crowbar = Give<obj_item>(w, me, "/obj/item/crowbar");
        In(w, () => Waiting.NoSleep(me.dropItemToGround(crowbar)));
        w.Run(5);
        (int X, int Y) dropped = In(w, () => ((int)crowbar.x, (int)crowbar.y));
        In(w, () => Waiting.NoSleep(crowbar.newtonian_move(GlobalProcs.dir2angle(Dm.EAST))));
        var item = new List<(long Tick, float SX, float SY, float DX, float DY)>();
        w.Run(2 * Second, tick =>
        {
            if (!p.Session.Replica.TryGet(crowbar.dm_id, out ReplicaEntity e) || !p.Session.Poses.TryGetPose(e, out RenderPose pose))
                return;
            (int ix, int iy) = In(w, () => ((int)crowbar.x, (int)crowbar.y));
            item.Add((tick, ix, iy, pose.X, pose.Y));
        });
        (int X, int Y) end = In(w, () => ((int)crowbar.x, (int)crowbar.y));
        (double itemLag, int itemCrossings) = CrossingLag(item);
        (double itemMax, double itemChange, _) = Speeds(item);
        _output.WriteLine($"{ms}, a drifting item: {dropped} -> {end}; drawn behind its loc by {itemLag * 50:0} ms over {itemCrossings} tile entries; "
            + $"drawn speed max {itemMax:0.00} tiles/tick, largest change {itemChange:0.00} tiles/tick; path " + Path(item));
        Assert.True(end.X > dropped.X, "the item drifted east");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // ---- the phases ----

    private sealed class Phase
    {
        public string Name;
        public RealHeadlessWorld.Player Player;
        public RealHeadlessWorld World;
        public PredictionCounters Start;
        public int ShakesStart;
        public int ProbeStart;
        public readonly List<(long Tick, float SX, float SY, float DX, float DY)> Rows = new();
        public PredictionCounters Counters;
        public int Shakes;
        public (double MeanMs, int Count) Motion;

        public float MaxFloat;
        public float MaxSideways;

        public void Sample(long tick)
        {
            // The camera layer as the game scene runs it each frame (here once a tick): the walk's sideways bob, the float.
            CameraOffsets eye = Player.Session.CameraFrame(RealHeadlessWorld.TickSeconds);
            MaxFloat = Math.Max(MaxFloat, Player.Session.Camera.FloatAmount);
            MaxSideways = Math.Max(MaxSideways, Math.Abs(eye.Right));
            MotionState s = World.ServerState(Player);
            (float x, float y, _) = Player.Session.Prediction.RenderPosition();
            Rows.Add((tick, MotionMath.ToTiles(s.X), MotionMath.ToTiles(s.Y), x, y));
        }

        public Phase End()
        {
            PredictionCounters now = Player.Counters;
            Counters = new PredictionCounters
            {
                Reconciliations = now.Reconciliations - Start.Reconciliations,
                Corrections = now.Corrections - Start.Corrections,
                Snaps = now.Snaps - Start.Snaps,
                Mismatches = now.Mismatches - Start.Mismatches,
                ServerDriven = now.ServerDriven - Start.ServerDriven,
                MaxErrorTiles = now.MaxErrorTiles,
            };
            Shakes = Player.Session.CorrectionShakes - ShakesStart;
            (_, double mean, int count) = Player.Session.Probe.Motion;
            Motion = (mean, count - ProbeStart);
            return this;
        }
    }

    private static Phase Begin(RealHeadlessWorld w, RealHeadlessWorld.Player p, string name) => new()
    {
        Name = name,
        Player = p,
        World = w,
        Start = p.Counters,
        ShakesStart = p.Session.CorrectionShakes,
        ProbeStart = p.Session.Probe.Motion.Count,
    };

    private static void Hold(RealHeadlessWorld w, RealHeadlessWorld.Player p, Phase phase, (int X, int Y) dir, int ticks)
    {
        p.Yaw = YawOf(dir.X, dir.Y);
        p.Keys = MoveKeys.Forward;
        w.Run(ticks, phase.Sample);
    }

    private static void Rest(RealHeadlessWorld w, RealHeadlessWorld.Player p, Phase phase, int ticks)
    {
        p.Keys = MoveKeys.None;
        w.Run(ticks, phase.Sample);
    }

    // Steers toward the target and lets go when it is within `lead` tiles (the frames on their way at a latency still step).
    private static void FlyTo(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob me, Phase phase, (int X, int Y) target, int lead)
    {
        for (int t = 0; t < 10 * Second; t++)
        {
            (int X, int Y) at = Tile(w, me);
            if (Math.Max(Math.Abs(at.X - target.X), Math.Abs(at.Y - target.Y)) <= lead)
                break;
            p.Yaw = YawOf(Math.Sign(target.X - at.X), Math.Sign(target.Y - at.Y));
            p.Keys = MoveKeys.Forward;
            w.Run(1, phase.Sample);
        }
        p.Keys = MoveKeys.None;
    }

    private void Report(string ms, Phase phase)
    {
        PredictionCounters c = phase.Counters;
        (double lagTicks, int crossings) = CrossingLag(phase.Rows);
        (double maxSpeed, double maxChange, double maxJump) = Speeds(phase.Rows);
        _output.WriteLine($"{ms}, {phase.Name}: reconciliations {c.Reconciliations} ({c.ServerDriven} server-driven), corrections {c.Corrections}, snaps {c.Snaps}, "
            + $"mismatches {c.Mismatches}, shakes {phase.Shakes}; key to drawn motion {phase.Motion.MeanMs:0} ms over {phase.Motion.Count} presses; "
            + $"drawn behind the server by {lagTicks * 50:0} ms over {crossings} tile entries; drawn speed max {maxSpeed:0.00} tiles/tick, "
            + $"largest change {maxChange:0.00} tiles/tick, largest step {maxJump:0.00} tiles; the eye's float {phase.MaxFloat:0.00}, sideways bob {phase.MaxSideways:0.000} m");
    }

    // Weightless (DV69): predicted as any capsule; a drift that starts on the server (tg's newtonian_move: a push off into space, a spray) is
    // at most a few small corrections, decayed in the view, never a snap or a shake; the view never jumps (no step of more than half a tile
    // a tick), the eye floats and does not walk-bob.
    private static void AssertSmoothWeightless(Phase phase)
    {
        PredictionCounters c = phase.Counters;
        Assert.True(c.Corrections <= 3, $"{c.Corrections} corrections");
        Assert.Equal((0L, 0), (c.Snaps, phase.Shakes));
        Assert.True(Speeds(phase.Rows).MaxJump <= 0.55, "the view never jumps");
        Assert.True(phase.MaxFloat > 0.99f, "the eye floats");
        Assert.True(phase.MaxSideways < 0.1f, $"the eye leans {phase.MaxSideways} m"); // DV42's lean into a sideways drift, no walk bob
    }

    private static string Path(List<(long Tick, float SX, float SY, float DX, float DY)> rows)
        => string.Join(" ", rows.Select(r => $"{r.Tick}:{r.SX:0.00},{r.SY:0.00}/{r.DX:0.00},{r.DY:0.00}"));

    // The mean ticks between the server's capsule centre entering a tile and the drawn centre entering the same tile (in order).
    internal static (double Ticks, int Count) CrossingLag(List<(long Tick, float SX, float SY, float DX, float DY)> rows)
    {
        var server = new List<(long Tick, int X, int Y)>();
        var drawn = new List<(long Tick, int X, int Y)>();
        foreach (var r in rows)
        {
            (int sx, int sy) = ((int)MathF.Round(r.SX), (int)MathF.Round(r.SY));
            (int dx, int dy) = ((int)MathF.Round(r.DX), (int)MathF.Round(r.DY));
            if (server.Count == 0 || (server[^1].X, server[^1].Y) != (sx, sy))
                server.Add((r.Tick, sx, sy));
            if (drawn.Count == 0 || (drawn[^1].X, drawn[^1].Y) != (dx, dy))
                drawn.Add((r.Tick, dx, dy));
        }
        double sum = 0;
        int n = 0;
        int j = 1;
        for (int i = 1; i < server.Count; i++)
        {
            while (j < drawn.Count && (drawn[j].X, drawn[j].Y) != (server[i].X, server[i].Y))
                j++;
            if (j >= drawn.Count)
                break;
            sum += drawn[j].Tick - server[i].Tick;
            n++;
            j++;
        }
        return (n == 0 ? 0 : sum / n, n);
    }

    // The drawn speed per tick: its largest value, its largest change between two ticks while moving, the largest step.
    internal static (double Max, double MaxChange, double MaxJump) Speeds(List<(long Tick, float SX, float SY, float DX, float DY)> rows)
    {
        double max = 0, change = 0, jump = 0, last = -1;
        for (int i = 1; i < rows.Count; i++)
        {
            double dx = rows[i].DX - rows[i - 1].DX, dy = rows[i].DY - rows[i - 1].DY;
            double v = Math.Sqrt(dx * dx + dy * dy);
            max = Math.Max(max, v);
            jump = Math.Max(jump, v);
            if (last > 0.01 && v > 0.01)
                change = Math.Max(change, Math.Abs(v - last));
            last = v;
        }
        return (max, change, jump);
    }

    // ---- the suit (JetpackGameplayTests' way) ----

    internal static void SuitUp(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human me, out obj_item_tank_jetpack jet, out long toggle, out long stabilization)
    {
        Give<obj_item_clothing_suit_space_eva>(w, me, "/obj/item/clothing/suit/space/eva");
        QuickEquip(w, p);
        Give<obj_item_clothing_head_helmet_space_eva>(w, me, "/obj/item/clothing/head/helmet/space/eva");
        QuickEquip(w, p);
        Give<obj_item_clothing_mask_breath>(w, me, "/obj/item/clothing/mask/breath");
        QuickEquip(w, p);
        var tank = Give<obj_item_tank_internals_oxygen>(w, me, "/obj/item/tank/internals/oxygen");
        ClickScreen(p, tank.dm_id, ClickModifiers.Alt);
        w.Run(Second);
        Assert.Same(tank, In(w, () => me.@internal));
        ClickScreen(p, In(w, () => ((atom)me.hud_used.screen_objects[Dm.text("item_slot:[]", Defines.ITEM_SLOT_SUITSTORE)]).dm_id));
        w.Run(Second);
        if (In(w, () => me.back) != null)
        {
            In(w, () => Waiting.NoSleep(me.dropItemToGround(me.back)));
            w.Run(5);
        }
        var pack = Give<obj_item_tank_jetpack_carbondioxide>(w, me, "/obj/item/tank/jetpack/carbondioxide");
        QuickEquip(w, p);
        Assert.Same(pack, In(w, () => me.back));
        jet = pack;
        toggle = ButtonOf<datum_action_item_action_toggle_jetpack>(w, me, pack);
        stabilization = ButtonOf<datum_action_item_action_jetpack_stabilization>(w, me, pack);
    }

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    internal static void ClickScreen(RealHeadlessWorld.Player p, long id, ClickModifiers modifiers = ClickModifiers.None)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), ClickButton.Left, modifiers));

    private static T Give<T>(RealHeadlessWorld w, mob_living_carbon_human me, string path) where T : obj_item
    {
        w.Host.EnqueueConsoleLine($"give Assistant {path}");
        w.Run(5);
        var held = In(w, () => me.get_active_held_item()) as T;
        Assert.NotNull(held);
        return held;
    }

    private static void QuickEquip(RealHeadlessWorld w, RealHeadlessWorld.Player p)
    {
        p.Session.Keybinding(KeybindingCode.QuickEquip);
        w.Run(Second);
    }

    private static long ButtonOf<T>(RealHeadlessWorld w, mob_living_carbon_human me, obj_item item) where T : datum_action
        => In(w, () => ((atom)Dm.each_cast<datum_action>(item.actions).Single(a => a is T).viewers[me.hud_used]).dm_id);

    private static (int X, int Y) Tile(RealHeadlessWorld w, mob me) => In(w, () => ((int)me.x, (int)me.y));

    // Godot's yaw for a world direction (MetaWalkTests: 0 faces north, +90 degrees west).
    private static double YawOf(int dx, int dy) => Math.Atan2(-dx, dy);
}
