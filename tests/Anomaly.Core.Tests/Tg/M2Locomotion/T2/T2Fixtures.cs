using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M1Client.P1;
using Anomaly.Core.Tests.Tg.Wave4.Integration;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// M2 T2's own rig for the bridge tests (the independent tester's: it shares nothing with P4's <c>CapsuleRoom</c>): a booted
/// toy room as the M2 server runs it, 16 by 16 tiles with walls on the rim (the floor is 2..15). A player command phase
/// (the commands the host's jitter buffer gives for a tick) is followed by the locomotion phase, installed the way the host
/// installs it. Humans join with a client and are registered as capsule mobs. The world keeps <c>NotTranslatedThrows</c> on.
/// <para>
/// The configuration tg reads its walk and run speeds from (<c>game_options.txt</c>: <c>RUN_DELAY</c> 1.5, <c>WALK_DELAY</c> 4,
/// which the server host does not load yet, M2 P1's open issue) is set on the world, so a human's own movespeed modifiers give
/// <see cref="Run"/> and <see cref="Walk"/>. A test that wants another speed passes a <c>slowdown</c>, which is written to
/// <c>cached_multiplicative_slowdown</c> after the mob is made (tg's modifiers recompute it when one changes).
/// </para>
/// </summary>
internal sealed class T2Room
{
    /// <summary>tg's RUN_DELAY (__DEFINES/movespeed.dm / combat.dm), deciseconds per tile.</summary>
    public const float Run = 1.5f;

    /// <summary>tg's WALK_DELAY, deciseconds per tile.</summary>
    public const float Walk = 4f;

    public const int Size = 16;

    public const int C = MotionMath.SubunitsPerTile;

    public TgToyRoom Room { get; }

    public World World => Room.World;

    public RecordingHost Host { get; } = new RecordingHost();

    private readonly Queue<Action> _commands = new();
    private uint _seq;

    public T2Room(ulong seed = 91)
    {
        Room = TgToyRoom.Boot(Size, Size, seed: seed);
        DmWorldState.Of(World).NotTranslatedThrows = true;
        ClientSeam.SetHost(World, Host);
        In(() =>
        {
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_movedelay_run_delay), Run));
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_movedelay_walk_delay), Walk));
        });
        World.AddPhase("test player commands", _ =>
        {
            while (_commands.Count > 0)
                _commands.Dequeue()();
        });
        LocomotionSeam.Install(World);
    }

    public void Command(Action command) => _commands.Enqueue(command);

    public void Step(int ticks = 1) => World.Step(ticks);

    public void In(Action body) => Room.In(body);

    public T In<T>(Func<T> body) => Room.In(body);

    /// <summary>A human with a player on (x, y), registered as a capsule mob (the host's join).</summary>
    public mob_living_carbon_human Join(int x, int y, string key, float? slowdown = null, string name = null)
        => Join<mob_living_carbon_human>(x, y, key, slowdown, name);

    public T Join<T>(int x, int y, string key, float? slowdown = null, string name = null) where T : mob_living
    {
        T body = In(() => Dm.@new<T>(locate(x, y, 1)));
        In(() =>
        {
            if (slowdown != null)
                body.cached_multiplicative_slowdown = slowdown.Value;
            if (name != null)
                body.name = name;
        });
        ClientConnection connection = null;
        Command(() =>
        {
            ClientSeam.KeyMob(body, key);
            connection = ClientSeam.Connect(key);
            LocomotionSeam.Register(body);
        });
        Step();
        Assert.NotNull(connection);
        Assert.True(connection.Accepted, "the player did not join");
        return body;
    }

    /// <summary>A human with no player, registered as a capsule mob (what the dev spawn does).</summary>
    public mob_living_carbon_human Dummy(int x, int y, float? slowdown = null, string name = null) => Dummy<mob_living_carbon_human>(x, y, slowdown, name);

    public T Dummy<T>(int x, int y, float? slowdown = null, string name = null) where T : mob_living
    {
        T body = In(() => Dm.@new<T>(locate(x, y, 1)));
        In(() =>
        {
            if (slowdown != null)
                body.cached_multiplicative_slowdown = slowdown.Value;
            if (name != null)
                body.name = name;
        });
        Command(() => LocomotionSeam.Register(body));
        Step();
        return body;
    }

    /// <summary>Sets the frame of the next tick for <paramref name="who"/> (the host's Motion command).</summary>
    public void Frame(mob_living who, sbyte moveX, sbyte moveY, ushort yaw)
    {
        uint seq = ++_seq;
        Command(() => LocomotionSeam.SetFrame(who, new MotionInput(seq, moveX, moveY, yaw, MotionInputFlags.None)));
    }

    /// <summary>Steps <paramref name="ticks"/> ticks with the same frame each tick.</summary>
    public void Go(mob_living who, sbyte moveX, sbyte moveY, ushort yaw, int ticks = 1)
    {
        for (int i = 0; i < ticks; i++)
        {
            Frame(who, moveX, moveY, yaw);
            Step();
        }
    }

    public MotionState State(mob_living who)
    {
        Assert.True(LocomotionSeam.TryGetState(World, who.dm_id, out MotionState state, out _));
        return state;
    }

    public MotionParams Params(mob_living who)
    {
        Assert.True(LocomotionSeam.TryGetState(World, who.dm_id, out _, out MotionParams parameters));
        return parameters;
    }

    public (int X, int Y, int Z) LocOf(mob_living who) => In(() => ((int)who.loc.x, (int)who.loc.y, (int)who.loc.z));

    /// <summary>The bridge's invariant (D105): the capsule's tile (a slide's target while sliding) is the mob's loc.</summary>
    public void AssertOnLoc(mob_living who, string when = "")
    {
        MotionState s = State(who);
        (int tx, int ty) = s.Slide.Active ? (MotionMath.TileOf(s.Slide.TargetX), MotionMath.TileOf(s.Slide.TargetY)) : (s.TileX, s.TileY);
        Assert.True((tx, ty, s.Z) == LocOf(who), $"{when} the capsule of {who.dm_id} is on tile ({tx},{ty},{s.Z}) but the mob's loc is {LocOf(who)}");
    }

    public void AssertNoRuntimes() => Assert.True(Room.Runtimes.Count == 0, string.Join("\n", Room.RuntimeText));

    /// <summary>The probe trace of this world (<see cref="Toy.On"/> writes it).</summary>
    public List<string> Trace => Toy.Trace(World);

    /// <summary>A dense object that blocks a face but not its tile (<c>ON_BORDER_1</c>): the controller's grid does not see it, tg's <c>Enter</c> does.</summary>
    public obj Edge(int x, int y) => In(() =>
    {
        var edge = Dm.@new<obj>(locate(x, y, 1));
        edge.name = "edge";
        edge.density = TRUE;
        edge.anchored = TRUE; // a loose object is pushed by a walker (PushAM, living.dm:334): this one stays like a window
        Dm.combine(ref edge.flags_1, ON_BORDER_1);
        return edge;
    });

    /// <summary>A dense object (a crate): a solid tile for the controller and for tg's <c>Enter</c>.</summary>
    public obj Crate(int x, int y) => In(() =>
    {
        var crate = Dm.@new<obj>(locate(x, y, 1));
        crate.name = "crate";
        crate.density = TRUE;
        crate.anchored = TRUE; // bolted down: a wall for the walker (a loose one is pushed, PushAM)
        return crate;
    });

    /// <summary>
    /// Probes the whole movement chain of move-chain.md section 3a on <paramref name="mover"/> and the two turfs (and their
    /// areas), in the order the chain sends them; every probe writes "label source args..." to <see cref="Trace"/>.
    /// </summary>
    public static void WatchChain(mob mover, turf from, turf to)
    {
        WatchMover(mover);
        WatchTurfs(from, to);
    }

    /// <summary>The probes on the mover itself (each signal once; a second call would write every line twice).</summary>
    public static void WatchMover(mob mover)
    {
        On(mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
        On(mover, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir", 2);
        On(mover, COMSIG_ATOM_DIR_CHANGE, "dir", 2);
        On(mover, COMSIG_ATOM_POST_DIR_CHANGE, "post_dir", 2);
        On(mover, COMSIG_MOVABLE_CROSS_OVER, "cross_over", 1);
        On(mover, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
        On(mover, COMSIG_ATOM_EXITING, "exiting", 2);
        On(mover, COMSIG_ATOM_ENTERING, "entering", 3);
        On(mover, COMSIG_MOVABLE_MOVED, "moved", 5);
        On(mover, COMSIG_MOVABLE_BUMP, "bump", 1);
        On(mover, COMSIG_MOVABLE_EXITED_AREA, "exited_area", 2);
    }

    /// <summary>The probes on two turfs and their areas.</summary>
    public static void WatchTurfs(turf from, turf to)
    {
        On(from, COMSIG_ATOM_EXIT, "exit", 2);
        On(to, COMSIG_ATOM_TRIED_PASS, "tried_pass_turf", 2);
        On(from, COMSIG_ATOM_EXITED, "exited", 2);
        On(to, COMSIG_ATOM_ENTERED, "entered", 3);
        On(from, COMSIG_ATOM_ABSTRACT_EXITED, "abstract_exited", 2);
        On(to, COMSIG_ATOM_ABSTRACT_ENTERED, "abstract_entered", 3);
        var area_from = (area)from.loc;
        var area_to = (area)to.loc;
        On(area_from, COMSIG_AREA_EXITED, "area_exited", 2);
        On(area_to, COMSIG_AREA_ENTERED, "area_entered", 2);
    }

    /// <summary>The compass bearing of a cardinal BYOND direction, the way the view yaw is sent.</summary>
    public static ushort Yaw(float dir) => MotionFacing.YawOf((byte)dir);
}
