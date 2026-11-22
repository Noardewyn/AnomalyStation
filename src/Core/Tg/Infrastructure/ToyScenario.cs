using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// <summary>One line of the scenario's log: the tick it happened on, what it was and the details.</summary>
/// <param name="Tick">The world tick (<see cref="World.Tick"/>).</param>
/// <param name="Name">"command" for what the player did, the short name of a signal ("pre_move", "moved", "bump",
/// "do_after_began", "do_after_ended") or "do_after_result".</param>
/// <param name="Detail">The details, in the log's own spelling: a turf is "(x,y)", a wall "wall(x,y)".</param>
public readonly record struct ToyLogEntry(long Tick, string Name, string Detail)
{
    public override string ToString() => $"t{Tick} {Name} {Detail}";
}

/// <summary>The room after one command, as text.</summary>
/// <param name="Tick">The world tick of the command (the frame before any command is taken one tick earlier).</param>
/// <param name="Caption">What the command was ("start" for the frame before the first one).</param>
/// <param name="Rows">The room, north (the top row) to south: <c>#</c> wall, <c>.</c> floor, <c>o</c> obj, <c>@</c> mob.</param>
/// <param name="MobDir">The mob's <c>dir</c> (BYOND's number: NORTH 1, SOUTH 2, EAST 4, WEST 8).</param>
public sealed record ToyFrame(long Tick, string Caption, IReadOnlyList<string> Rows, float MobDir);

/// <summary>A do_after the scenario started: how it was asked and how it ended.</summary>
public sealed class ToyDoAfter
{
    internal ToyDoAfter(int number, long startTick)
    {
        Number = number;
        StartTick = startTick;
    }

    /// <summary>1 for the do_after that completes, 2 for the one a step cancels.</summary>
    public int Number { get; }

    /// <summary>The tick of the command that started it.</summary>
    public long StartTick { get; }

    /// <summary>The tick the awaiting caller got its result back on (-1 until it did).</summary>
    public long EndTick { get; internal set; } = -1;

    /// <summary>Whether do_after came back.</summary>
    public bool Done { get; internal set; }

    /// <summary>The DM value do_after returned: TRUE when the action completed, FALSE when it was cancelled.</summary>
    public object Result { get; internal set; }

    /// <summary>Whether do_after came back and returned a true value.</summary>
    public bool Succeeded => Done && Dm.truthy(Result);
}

/// <summary>
/// The scripted M0 scenario ("Done when" of docs/ROADMAP.md, M0), run in a world of its own: tg's real toy room
/// (<see cref="ToyRoom"/>) under the real Master Controller, one plain <c>/mob</c> and one plain <c>/obj</c> in it, and a
/// player who does six things on fixed ticks. It is host code, not tg code: it drives tg's <c>step</c> and <c>do_after</c> as
/// a player's verbs would, from a world phase (the player command phase, docs/ai/specs/waiting-model.md 7.1), and only
/// listens to the signals tg sends.
/// <list type="number">
/// <item>The mob steps east twice (the whole Move chain: <c>pre_move</c>, then <c>moved</c> for each step).</item>
/// <item>It steps south into the wall: the move fails, <c>bump</c> runs, and there is no <c>moved</c>.</item>
/// <item>It starts a do_after of 1 second on the obj and waits: the action completes (TRUE) 20 ticks later.</item>
/// <item>It starts a second do_after and steps east 5 ticks later: the step's <c>moved</c> cancels the action (FALSE), which the
/// awaiting caller learns one tick after the step.</item>
/// </list>
/// The ticks are offsets from <see cref="StartTick"/>, the tick of the first command, which is the first tick after the
/// Master has initialized, the run level is RUNLEVEL_GAME and the world has settled: the same start as the integration tests'
/// booted toy room. The scenario is deterministic: two runs of one seed give the same log, the same frames and the same
/// <see cref="StateHash"/>.
/// </summary>
public sealed class ToyScenario
{
    /// <summary>The seed <see cref="Run"/> uses when none is given.</summary>
    public const ulong DefaultSeed = 1;

    // The script: tick offsets from StartTick.
    public const int FirstStepAt = 0;
    public const int SecondStepAt = 2;
    public const int WallStepAt = 4;
    public const int FirstDoAfterAt = 6;
    public const int SecondDoAfterAt = 30;
    public const int CancelStepAt = 35;

    /// <summary>The scenario ends when this tick offset has run.</summary>
    public const int EndAt = 40;

    /// <summary>The length of a do_after in ticks (1 second is 10 ds, tg's tick is 0.5 ds).</summary>
    public const int DoAfterTicks = 20;

    // The mob's and the obj's start, in the toy room (the floor is x and y from 2 to 6).
    private const int MobX = 2, MobY = 2, ObjX = 4, ObjY = 3;

    // Master.Initialize sleeps 10 ds and initializes at tick 20; the boot settles up to tick 30, as TgToyRoom.Boot does.
    private const long SettleTick = 30;
    private const long MaxBootTicks = 200;

    private readonly List<ToyLogEntry> _log = new();
    private readonly List<ToyFrame> _frames = new();
    private readonly List<Exception> _runtimes = new();
    private readonly List<(long At, Func<string> Do, string Caption)> _script = new();
    private int _next;
    private mob _mob;
    private obj _obj;
    private ToyDoAfter _first;
    private ToyDoAfter _second;
    private int _began;
    private int _ended;

    private ToyScenario(ulong seed)
    {
        Seed = seed;
        World = new World(seed) { RuntimeErrorSink = _runtimes.Add };
    }

    /// <summary>
    /// Runs the scenario in a new world of <paramref name="seed"/> and returns it finished. <paramref name="prepare"/>, if
    /// given, runs on the new world before the boot, as a host installs its seams (M1: a test installs a replication hint
    /// sink there to show that hints change nothing).
    /// </summary>
    public static ToyScenario Run(ulong seed = DefaultSeed, Action<World> prepare = null)
    {
        var scenario = new ToyScenario(seed);
        prepare?.Invoke(scenario.World);
        scenario.Execute();
        return scenario;
    }

    public ulong Seed { get; }

    /// <summary>The world the scenario ran in, left at the last tick (<see cref="StartTick"/> + <see cref="EndAt"/>).</summary>
    public World World { get; }

    /// <summary>The tick of the first command; every script offset (<see cref="FirstStepAt"/> ...) counts from it.</summary>
    public long StartTick { get; private set; }

    /// <summary>What the player did and what tg sent, in the order it happened.</summary>
    public IReadOnlyList<ToyLogEntry> Log => _log;

    /// <summary>The room before the first command, then after each command.</summary>
    public IReadOnlyList<ToyFrame> Frames => _frames;

    /// <summary>The first do_after: it completes.</summary>
    public ToyDoAfter CompletedDoAfter => _first;

    /// <summary>The second do_after: a step cancels it.</summary>
    public ToyDoAfter CancelledDoAfter => _second;

    /// <summary>The runtime errors the world reported (none in a healthy run).</summary>
    public IReadOnlyList<Exception> Runtimes => _runtimes;

    /// <summary><see cref="World.ComputeStateHash"/> at the last tick.</summary>
    public ulong StateHash { get; private set; }

    private void Execute()
    {
        // tg's strings/ files, as the server mounts them (M10 slice F: the ticker's Initialize and the station name read them)
        if (TgStrings.Locate(AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory()) is string tg)
            TgStrings.Mount(DmWorldState.Of(World).Files, tg);
        TgWorld.Boot(World, static _ => ToyRoom.Build());
        while ((!Initialized() || World.Tick < SettleTick) && World.Tick < MaxBootTicks)
            World.Step();
        if (!Initialized())
            throw new InvalidOperationException($"the Master did not finish initializing within {MaxBootTicks} ticks");

        using (World.Enter())
        {
            // the ticker's job in tg: the scenario moves the round on itself, so the translated ticker (M10 slice F) is paused
            // (tg's can_fire, master.dm:893) and its pregame countdown sets no round up
            SSticker.can_fire = Dm.FALSE;
            Master.SetRunLevel(RUNLEVEL_GAME);
            _mob = Dm.@new<mob>(locate(MobX, MobY, 1));
            _obj = Dm.@new<obj>(locate(ObjX, ObjY, 1));
            var listener = Dm.@new<ToyListener>();
            listener.Scenario = this;
            listener.RegisterSignal(_mob, COMSIG_MOVABLE_PRE_MOVE, nameof(ToyListener.on_pre_move));
            listener.RegisterSignal(_mob, COMSIG_MOVABLE_MOVED, nameof(ToyListener.on_moved));
            listener.RegisterSignal(_mob, COMSIG_MOVABLE_BUMP, nameof(ToyListener.on_bump));
            listener.RegisterSignal(_mob, COMSIG_DO_AFTER_BEGAN, nameof(ToyListener.on_do_after_began));
            listener.RegisterSignal(_mob, COMSIG_DO_AFTER_ENDED, nameof(ToyListener.on_do_after_ended));
            _frames.Add(new ToyFrame(World.Tick, "start", Rows(), _mob.dir));

            StartTick = World.Tick + 1;
            Add(FirstStepAt, "step EAST", () => Step(Dm.EAST));
            Add(SecondStepAt, "step EAST", () => Step(Dm.EAST));
            Add(WallStepAt, "step SOUTH", () => Step(Dm.SOUTH));
            Add(FirstDoAfterAt, "do_after 1", () => StartDoAfter(ref _first, 1));
            Add(SecondDoAfterAt, "do_after 2", () => StartDoAfter(ref _second, 2));
            Add(CancelStepAt, "step EAST", () => Step(Dm.EAST));
            World.AddPhase("player commands", RunCommands);
        }

        while (World.Tick < StartTick + EndAt)
            World.Step();
        StateHash = World.ComputeStateHash();
    }

    private bool Initialized()
    {
        using (World.Enter())
            return Master != null && Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX);
    }

    // ---- the player ----

    private void Add(int at, string caption, Func<string> command) => _script.Add((StartTick + at, command, caption));

    // The player command phase: whatever the script has for this tick, in script order, then a frame of the room after each.
    private void RunCommands(World world)
    {
        while (_next < _script.Count && _script[_next].At == world.Tick)
        {
            (long _, Func<string> command, string caption) = _script[_next++];
            // The command's line comes before the signals it causes; its result is filled in when it has returned.
            int line = _log.Count;
            Record("command", caption);
            _log[line] = _log[line] with { Detail = command() };
            _frames.Add(new ToyFrame(world.Tick, caption, Rows(), _mob.dir));
        }
    }

    private string Step(float dir)
    {
        object result = Waiting.NoSleep(step(_mob, dir));
        return $"step {DirName(dir)} -> {(Dm.truthy(result) ? "TRUE" : "FALSE")}";
    }

    private string StartDoAfter(ref ToyDoAfter slot, int number)
    {
        slot = new ToyDoAfter(number, World.Tick);
        Waiting.Detach(AwaitDoAfter(slot));
        return $"do_after #{number} of 1 second on the obj";
    }

    // The caller of a do_after, as a verb would be: it waits for the result and notes the tick it came back on.
    private async DmTask AwaitDoAfter(ToyDoAfter record)
    {
        record.Result = await do_after(_mob, 1 * SECONDS, _obj);
        record.EndTick = World.Tick;
        record.Done = true;
        Record("do_after_result", $"#{record.Number} -> {(Dm.truthy(record.Result) ? "TRUE" : "FALSE")} (started t{record.StartTick})");
    }

    // ---- the log ----

    internal void Record(string name, string detail) => _log.Add(new ToyLogEntry(World.Tick, name, detail));

    internal int NextBegan() => ++_began;

    internal int NextEnded() => ++_ended;

    internal static string Where(object value) => value switch
    {
        null => "null",
        turf_closed t => $"wall({Dm.str(t.x)},{Dm.str(t.y)})",
        turf t => $"({Dm.str(t.x)},{Dm.str(t.y)})",
        mob => "mob",
        obj => "obj",
        atom a => a.name,
        _ => Dm.str(value),
    };

    private static string DirName(float dir) => dir switch
    {
        Dm.NORTH => "NORTH",
        Dm.SOUTH => "SOUTH",
        Dm.EAST => "EAST",
        Dm.WEST => "WEST",
        Dm.NORTHEAST => "NORTHEAST",
        Dm.NORTHWEST => "NORTHWEST",
        Dm.SOUTHEAST => "SOUTHEAST",
        Dm.SOUTHWEST => "SOUTHWEST",
        _ => Dm.str(dir),
    };

    internal static string DirText(object dir) => dir is float f ? DirName(f) : Dm.str(dir);

    // ---- the frames ----

    // The room, level 1, north first: a wall is #, a floor ., an obj o and a mob @ (the mob wins a shared tile).
    private static string[] Rows()
    {
        int width = (int)Dm.world.maxx, height = (int)Dm.world.maxy;
        var rows = new string[height];
        var row = new StringBuilder(width);
        for (int y = height; y >= 1; y--)
        {
            row.Clear();
            for (int x = 1; x <= width; x++)
                row.Append(Glyph(locate(x, y, 1)));
            rows[height - y] = row.ToString();
        }
        return rows;
    }

    private static char Glyph(turf t)
    {
        char glyph = t is turf_closed ? '#' : '.';
        foreach (object thing in Dm.each(t.contents))
        {
            if (thing is mob)
                return '@';
            if (thing is obj)
                glyph = 'o';
        }
        return glyph;
    }
}

/// <summary>
/// The listener the scenario registers on its mob for the signals it logs. A plain datum with no DM path of its own (the
/// type registry skips it, as it skips the test fixtures that are a C# convenience only), so it adds no type to tg's tree.
/// </summary>
internal class ToyListener : datum
{
    public ToyScenario Scenario;

    // The arguments are the ones the signals carry (docs/ai/specs/move-chain.md 3a, 3c, 3d, and do_after in timed_actions.dm).
    [SignalHandler]
    public virtual object on_pre_move(datum source = null, object newloc = null)
    {
        Scenario.Record("pre_move", $"{ToyScenario.Where(((atom_movable)source).loc)} -> {ToyScenario.Where(newloc)}");
        return NONE;
    }

    [SignalHandler]
    public virtual object on_moved(datum source = null, object old_loc = null, object movement_dir = null, object forced = null, object old_locs = null, object momentum_change = null)
    {
        Scenario.Record("moved", $"{ToyScenario.Where(old_loc)} -> {ToyScenario.Where(((atom_movable)source).loc)} dir {ToyScenario.DirText(movement_dir)}");
        return NONE;
    }

    [SignalHandler]
    public virtual object on_bump(datum source = null, object bumped = null)
    {
        Scenario.Record("bump", $"{ToyScenario.Where(bumped)} from {ToyScenario.Where(((atom_movable)source).loc)}");
        return NONE;
    }

    [SignalHandler]
    public virtual object on_do_after_began(datum source = null)
    {
        Scenario.Record("do_after_began", $"#{Scenario.NextBegan()}");
        return NONE;
    }

    [SignalHandler]
    public virtual object on_do_after_ended(datum source = null)
    {
        Scenario.Record("do_after_ended", $"#{Scenario.NextEnded()}");
        return NONE;
    }
}
