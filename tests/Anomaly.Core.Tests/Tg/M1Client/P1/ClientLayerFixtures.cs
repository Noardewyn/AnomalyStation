using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.P1;

/// <summary>
/// A booted toy room (<see cref="TgToyRoom.Boot"/>: the real Master, RUNLEVEL_GAME, SSinput live) with the client seam's
/// host installed and a command phase, as the M1 server runs the world (scope 2b, 2e): what the tests queue with
/// <see cref="Command"/> runs in the phase after the Master fired, one command at a time, as P3 does. The world keeps
/// <c>NotTranslatedThrows</c> on (scope 2j), so a reached <c>Dm.NotTranslated</c> fails the test.
/// </summary>
internal sealed class ClientRoom
{
    public TgToyRoom Room { get; }

    public World World => Room.World;

    public RecordingHost Host { get; } = new RecordingHost();

    private readonly Queue<Action> _commands = new Queue<Action>();

    /// <summary>The room's size: big enough for a few steps in each direction from the middle.</summary>
    public const int Size = 16;

    public ClientRoom(ulong seed = 21)
    {
        Room = TgToyRoom.Boot(Size, Size, seed: seed);
        ClientSeam.SetHost(World, Host);
        World.AddPhase("test player commands", _ =>
        {
            while (_commands.Count > 0)
                _commands.Dequeue()();
        });
    }

    /// <summary>Queues a player command for the command phase of the next <see cref="Step"/>.</summary>
    public void Command(Action command) => _commands.Enqueue(command);

    public void Step(int ticks = 1) => World.Step(ticks);

    public void In(Action body) => Room.In(body);

    public T In<T>(Func<T> body) => Room.In(body);

    public long Tick => World.Tick;

    /// <summary>A plain <c>/mob</c> on the floor at (x, y), made as the host makes an avatar (scope 2b).</summary>
    public mob NewMob(int x, int y) => In(() => Dm.@new<mob>(locate(x, y, 1)));

    /// <summary>The first join of the M1 server (ClientSeam's order): key the avatar, connect, step until client/New returned.</summary>
    public ClientConnection Join(mob avatar, string key)
    {
        ClientConnection connection = null;
        Command(() =>
        {
            ClientSeam.KeyMob(avatar, key);
            connection = ClientSeam.Connect(key);
        });
        Step();
        Assert.NotNull(connection);
        Assert.True(connection.Done, "client/New did not return in the tick it was started");
        return connection;
    }

    public void AssertNoRuntimes() => Assert.True(Room.Runtimes.Count == 0, string.Join("\n", Room.RuntimeText));
}

/// <summary>The server host's side of the client seam (<see cref="IClientHost"/>), recording what the engine tells it.</summary>
internal sealed class RecordingHost : IClientHost
{
    public readonly List<string> Events = new List<string>();

    public readonly List<client> Deleted = new List<client>();

    public void ClientDeleted(client player)
    {
        Deleted.Add(player);
        Events.Add("deleted " + player.key);
    }

    public void ClientMobChanged(client player, mob old_mob, mob new_mob)
        => Events.Add("mob " + player.key + " " + Id(old_mob) + " -> " + Id(new_mob));

    public static string Id(datum d) => d == null ? "null" : d.dm_id.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// A listener that records the movement signals tg sends around a step (move-chain.md section 3a), in the order they arrive
/// (rules 15: a recording harness). A fixture type of the port's own (<c>/datum/test_m1p1_listener</c>).
/// </summary>
[DmPath("/datum/test_m1p1_listener")]
public class datum_test_m1p1_listener : datum
{
    public readonly List<string> Trace = new List<string>();

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    /// <summary>Listens to <paramref name="mover"/> and the turfs around it.</summary>
    public void Watch(mob mover)
    {
        RegisterSignal(mover, COMSIG_MOB_CLIENT_PRE_NON_LIVING_MOVE, nameof(on_client_pre_move));
        RegisterSignal(mover, COMSIG_MOVABLE_ATTEMPTED_MOVE, nameof(on_attempted_move));
        RegisterSignal(mover, COMSIG_ATOM_DIR_CHANGE, nameof(on_dir_change));
        RegisterSignal(mover, COMSIG_MOVABLE_PRE_MOVE, nameof(on_pre_move));
        RegisterSignal(mover, COMSIG_MOVABLE_MOVED, nameof(on_moved));
    }

    public void WatchTurf(turf T)
    {
        RegisterSignal(T, COMSIG_ATOM_EXIT, nameof(on_exit));
        RegisterSignal(T, COMSIG_ATOM_EXITED, nameof(on_exited));
        RegisterSignal(T, COMSIG_ATOM_ENTERED, nameof(on_entered));
    }

    [SignalHandler]
    public virtual void on_client_pre_move(datum source = null, object new_loc = null, object direct = null)
        => Trace.Add("client_pre_move " + At(new_loc) + " " + Dm.str(direct));

    [SignalHandler]
    public virtual void on_attempted_move(datum source = null, object newloc = null, object direction = null)
        => Trace.Add("attempted_move " + At(newloc) + " " + Dm.str(direction));

    [SignalHandler]
    public virtual void on_dir_change(datum source = null, object old_dir = null, object new_dir = null)
        => Trace.Add("dir_change " + Dm.str(old_dir) + " " + Dm.str(new_dir));

    [SignalHandler]
    public virtual void on_pre_move(datum source = null, object newloc = null)
        => Trace.Add("pre_move " + At(newloc));

    [SignalHandler]
    public virtual void on_moved(datum source = null, object old_loc = null, object movement_dir = null)
        => Trace.Add("moved " + At(old_loc) + " " + Dm.str(movement_dir));

    [SignalHandler]
    public virtual void on_exit(datum source = null, object leaving = null, object direction = null)
        => Trace.Add("exit " + At(source));

    [SignalHandler]
    public virtual void on_exited(datum source = null, object gone = null, object direction = null)
        => Trace.Add("exited " + At(source));

    [SignalHandler]
    public virtual void on_entered(datum source = null, object arrived = null, object old_loc = null)
        => Trace.Add("entered " + At(source));

    private static string At(object place) => place is atom a ? Dm.str(a.x) + "," + Dm.str(a.y) : Dm.str(place);
}

/// <summary>
/// A plain mob whose Login sleeps one tick before tg's (the case tg warns about, login.dm:2-5: "Anything that sleeps can
/// result in the client reference being dropped"). A fixture type of the port's own.
/// </summary>
[DmPath("/mob/test_m1p1_slow_login")]
public class mob_test_m1p1_slow_login : mob
{
    public override async DmTask<object> Login()
    {
        await sleep(world.tick_lag);
        return await base.Login();
    }
}

/// <summary>A plain mob that records the <c>usr</c> its Login runs with (BYOND ref <c>/client/proc/New</c>: usr is the mob of the key).</summary>
[DmPath("/mob/test_m1p1_usr_login")]
public class mob_test_m1p1_usr_login : mob
{
    public mob login_usr;

    public override async DmTask<object> Login()
    {
        login_usr = usr;
        return await base.Login();
    }
}
