using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1's world for the client layer (M1): a booted toy room (the real Master, RUNLEVEL_GAME, so SSinput and SSverb_manager
/// fire every tick) with the host's seam installed and a command phase that runs after the Master's fire, as the server host
/// (P3) runs player commands. Written from tg's DM, not from P1's tests: its own fixtures, its own recorders. A player is
/// driven only through tg's verbs (<c>keyDown</c>, <c>keyUp</c>) and the host entry points of <see cref="ClientSeam"/>;
/// what happens is read back from tg's variables and lists.
/// </summary>
internal sealed class T1Room
{
    /// <summary>The room's width and height, rim included: the floor is (2,2) to (15,15).</summary>
    public const int Size = 16;

    public TgToyRoom Room { get; }

    public World World => Room.World;

    public T1Host Host { get; } = new T1Host();

    public T1Chat Chat { get; } = new T1Chat();

    private readonly Queue<Action> _commands = new Queue<Action>();

    public T1Room(ulong seed = 21)
    {
        Room = TgToyRoom.Boot(Size, Size, seed: seed);
        ClientSeam.SetHost(World, Host);
        PresentationSinks.Of(World).Messages = Chat;
        World.AddPhase("t1 player commands", _ =>
        {
            while (_commands.Count > 0)
                _commands.Dequeue()();
        });
    }

    /// <summary>Queues <paramref name="command"/> for the command phase of the next <see cref="Step"/> (after the Master's fire of that tick).</summary>
    public void Act(Action command) => _commands.Enqueue(command);

    public void Step(int ticks = 1) => World.Step(ticks);

    /// <summary>Runs <paramref name="body"/> inside the world's scope (to read, or to set up before a step).</summary>
    public void In(Action body) => Room.In(body);

    public T In<T>(Func<T> body) => Room.In(body);

    /// <summary>The world's clock in deciseconds, inside the scope.</summary>
    public float Time => In(() => world.time);

    /// <summary>A plain <c>/mob</c> on the floor at (x, y), the avatar the host creates (scope 2b).</summary>
    public mob NewMob(int x, int y) => In(() => Dm.@new<mob>(locate(x, y, 1)));

    public T NewMob<T>(int x, int y) where T : mob, new() => In(() => Dm.@new<T>(locate(x, y, 1)));

    /// <summary>
    /// The first join (the host's order, ClientSeam): key the avatar, connect, in one command, then one step. client/New
    /// must have returned within it.
    /// </summary>
    public ClientConnection Join(mob avatar, string key)
    {
        ClientConnection connection = null;
        Act(() =>
        {
            ClientSeam.KeyMob(avatar, key);
            connection = ClientSeam.Connect(key);
        });
        Step();
        Assert.NotNull(connection);
        Assert.True(connection.Done, "client/New did not return in the tick it was started");
        Assert.True(connection.Accepted, "the connection was refused: " + connection.Error);
        return connection;
    }

    /// <summary>A connected player: a new plain mob at (x, y), keyed, connected.</summary>
    public (mob Mob, client Client) JoinNew(string key, int x = 8, int y = 8)
    {
        mob avatar = NewMob(x, y);
        client player = Join(avatar, key).Client;
        return (avatar, player);
    }

    // ---- what a player's machine sends (inside the world's scope: Act or In) ----

    /// <summary>The player's machine sends tg's <c>keyDown</c> verb (BYOND runs a verb with usr the player's mob).</summary>
    public void Down(client player, string key) => Verb(player, () => player.keyDown(key));

    /// <summary>The player's machine sends tg's <c>keyUp</c> verb.</summary>
    public void Up(client player, string key) => Verb(player, () => player.keyUp(key));

    private void Verb(client player, Func<DmTask> verb)
    {
        object caller_usr = World.usr;
        World.usr = player.mob;
        try
        {
            Waiting.Detach(verb());
        }
        finally
        {
            World.usr = caller_usr;
        }
    }

    public turf At(int x, int y) => locate(x, y, 1);

    /// <summary>
    /// A call of tg's <c>client/Move(new_loc, direct)</c> as BYOND makes it (<c>usr</c> the client's mob; inside the world's
    /// scope), returning what it returned.
    /// </summary>
    public object MoveCall(client player, object new_loc, object direct)
    {
        object caller_usr = World.usr;
        World.usr = player.mob;
        try
        {
            return Waiting.NoSleep(player.Move(new_loc, direct));
        }
        finally
        {
            World.usr = caller_usr;
        }
    }

    public void AssertNoRuntimes() => Assert.True(Room.Runtimes.Count == 0, string.Join("\n", Room.RuntimeText));

    /// <summary>The coordinates of a mob's turf as "x,y".</summary>
    public static string Where(atom a) => a.loc is turf t ? Dm.str(t.x) + "," + Dm.str(t.y) : Dm.str(a.loc);
}

/// <summary>BYOND's direction bits as ints (tg's NORTH, SOUTH, EAST, WEST are floats, which cannot be or-ed in C#).</summary>
internal static class Dirs
{
    public const int North = 1, South = 2, East = 4, West = 8;
}

/// <summary>The server host's side of the client seam, recording in order what the engine part tells it.</summary>
internal sealed class T1Host : IClientHost
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

/// <summary>The message sink of the world (tg's to_chat lands here), recording the text sent to each target.</summary>
internal sealed class T1Chat : IMessageSink
{
    public readonly List<(object Target, string Text)> Messages = new List<(object, string)>();

    public void Chat(object target, DmList message)
        => Messages.Add((target, Dm.str(message["text"]) + Dm.str(message["html"]))); // tg's to_chat puts the markup in "html"

    public void Output(object target, string text) => Messages.Add((target, text));

    public void Balloon(object viewer, object atom, string text) { }

    public IEnumerable<string> To(object target) => Messages.Where(m => ReferenceEquals(m.Target, target)).Select(m => m.Text);
}

/// <summary>
/// A listener that records, in the order they arrive, the signals tg sends around a client's life and a step
/// (translation-rules.md 15: a recording harness). A fixture type of T1's own (<c>/datum/test_m1t1_listener</c>).
/// </summary>
[DmPath("/datum/test_m1t1_listener")]
public class datum_test_m1t1_listener : datum
{
    public readonly List<string> Trace = new List<string>();

    /// <summary>When set, the PRE_NON_LIVING_MOVE handler throws (a runtime in a signal handler).</summary>
    public bool ThrowOnPreMove;

    /// <summary>What the PRE_NON_LIVING_MOVE handler returns (COMSIG_MOB_CLIENT_BLOCK_PRE_NON_LIVING_MOVE blocks the move).</summary>
    public int PreNonLivingReturn;

    /// <summary>What the KEYBIND_FACE_DIR handler returns (COMSIG_IGNORE_MOVEMENT_LOCK lets the mob move while locked).</summary>
    public int FaceDirReturn;

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    /// <summary>The signals of a mob's movement around client/Move (move-chain.md 3a) and its dir.</summary>
    public void WatchMove(mob mover)
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

    /// <summary>The signals of a mob's login and logout, and the global ones that go with them.</summary>
    public void WatchMobLife(mob m)
    {
        RegisterSignal(m, COMSIG_MOB_LOGIN, nameof(on_mob_login));
        RegisterSignal(m, COMSIG_MOB_CLIENT_LOGIN, nameof(on_mob_client_login));
        RegisterSignal(m, COMSIG_MOB_LOGOUT, nameof(on_mob_logout));
    }

    public void WatchGlobals()
    {
        RegisterSignal(SSdcs, COMSIG_GLOB_PLAYER_LOGIN, nameof(on_glob_player_login));
        RegisterSignal(SSdcs, COMSIG_GLOB_PLAYER_LOGOUT, nameof(on_glob_player_logout));
        RegisterSignal(SSdcs, COMSIG_GLOB_MOB_LOGGED_IN, nameof(on_glob_mob_logged_in));
        RegisterSignal(SSdcs, COMSIG_GLOB_CLIENT_CONNECT, nameof(on_glob_client_connect));
    }

    public void WatchClient(client c)
    {
        RegisterSignal(c, COMSIG_CLIENT_MOB_LOGIN, nameof(on_client_mob_login));
        RegisterSignal(c, COMSIG_QDELETING, nameof(on_client_qdeleting));
    }

    /// <summary>A client that the PRE_NON_LIVING_MOVE handler qdels once (a move that pokes another player's client: bindings_atom.dm:6-9).</summary>
    public client KillOnPreMove;

    [SignalHandler]
    public virtual object on_client_pre_move(datum source = null, object new_loc = null, object direct = null)
    {
        Trace.Add("client_pre_move " + At(new_loc) + " " + Dm.str(direct));
        if (ThrowOnPreMove)
            throw new InvalidOperationException("t1 boom in a signal handler");
        if (KillOnPreMove != null)
        {
            client victim = KillOnPreMove;
            KillOnPreMove = null;
            qdel(victim);
        }
        return PreNonLivingReturn;
    }

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

    [SignalHandler]
    public virtual void on_mob_login(datum source = null)
        => Trace.Add("MOB_LOGIN");

    [SignalHandler]
    public virtual void on_mob_client_login(datum source = null, object logged = null)
        => Trace.Add("MOB_CLIENT_LOGIN " + (logged is client c ? c.key : Dm.str(logged)));

    [SignalHandler]
    public virtual void on_client_mob_login(datum source = null, object logged = null)
        => Trace.Add("CLIENT_MOB_LOGIN");

    [SignalHandler]
    public virtual void on_mob_logout(datum source = null)
    {
        // What the mob looks like when BYOND says the player has left it (BYOND ref /mob/proc/Logout): its client is gone
        // already, its key is still the player's (a disconnect) or null (the player went to another mob).
        mob m = (mob)source;
        Trace.Add("MOB_LOGOUT key=" + (m.key ?? "null") + " client=" + (m.client == null ? "null" : "set")
            + " listed=" + Dm.str(Dm.truthy(Dm.@in(m, GLOB.player_list)) ? 1 : 0));
    }

    [SignalHandler]
    public virtual void on_glob_player_login(datum source = null, object logged = null)
        => Trace.Add("GLOB_PLAYER_LOGIN");

    [SignalHandler]
    public virtual void on_glob_player_logout(datum source = null, object logged = null)
        => Trace.Add("GLOB_PLAYER_LOGOUT");

    [SignalHandler]
    public virtual void on_glob_mob_logged_in(datum source = null, object logged = null)
        => Trace.Add("GLOB_MOB_LOGGED_IN");

    [SignalHandler]
    public virtual void on_glob_client_connect(datum source = null, object connected = null)
        => Trace.Add("GLOB_CLIENT_CONNECT");

    [SignalHandler]
    public virtual void on_client_qdeleting(datum source = null, object force = null)
        => Trace.Add("CLIENT_QDELETING");

    [SignalHandler]
    public virtual object on_face_dir(datum source = null, object dir = null)
    {
        Trace.Add("face_dir " + Dm.str(dir));
        return FaceDirReturn;
    }

    /// <summary>COMSIG_MOB_RESET_PERSPECTIVE of a mob (mob.dm:544).</summary>
    public void WatchPerspective(mob m) => RegisterSignal(m, COMSIG_MOB_RESET_PERSPECTIVE, nameof(on_reset_perspective));

    [SignalHandler]
    public virtual void on_reset_perspective(datum source = null)
        => Trace.Add("RESET_PERSPECTIVE");

    /// <summary>COMSIG_CLIENT_SET_EYE of a client (client_procs.dm:1025).</summary>
    public void WatchClientEye(client c) => RegisterSignal(c, COMSIG_CLIENT_SET_EYE, nameof(on_set_eye));

    [SignalHandler]
    public virtual void on_set_eye(datum source = null, object old_eye = null, object new_eye = null)
        => Trace.Add("SET_EYE " + Dm.str((old_eye as atom)?.tag) + " -> " + Dm.str((new_eye as atom)?.tag));

    /// <summary>COMSIG_MOB_KEYDOWN of a mob (mob.dm:1613-1615: what /mob/key_down sends, with the key, the client and the full key).</summary>
    public void WatchKeyDown(mob m) => RegisterSignal(m, COMSIG_MOB_KEYDOWN, nameof(on_mob_keydown));

    /// <summary>The <c>usr</c> at the last COMSIG_MOB_KEYDOWN.</summary>
    public object UsrAtKeyDown;

    [SignalHandler]
    public virtual void on_mob_keydown(datum source = null, object key = null, object user = null, object full_key = null)
    {
        UsrAtKeyDown = World.Current.usr;
        Trace.Add("KEYDOWN " + Dm.str(key) + " as " + Dm.str(full_key) + " by " + (user is client c ? c.key : Dm.str(user)) + " on " + Dm.str(((mob)source).tag));
    }

    /// <summary>A callback target: what a persistent client's post-login callback calls.</summary>
    public void ping_login() => Trace.Add("CALLBACK post_login");

    /// <summary>A callback target: what a persistent client's post-logout callback calls.</summary>
    public void ping_logout() => Trace.Add("CALLBACK post_logout");

    public void WatchFaceDir(mob m) => RegisterSignal(m, COMSIG_MOVABLE_KEYBIND_FACE_DIR, nameof(on_face_dir));

    private static string At(object place) => place is atom a ? Dm.str(a.x) + "," + Dm.str(a.y) : Dm.str(place);
}

/// <summary>
/// A plain mob that notes every call of keyLoop (SSinput's loop over GLOB.keyloop_list) and then runs tg's. A fixture type
/// of T1's own. It records in a world-wide list the test passes in, so the order across mobs is visible.
/// </summary>
[DmPath("/mob/test_m1t1_loop_recorder")]
public class mob_test_m1t1_loop_recorder : mob
{
    [NotHashed]
    public List<string> Calls;

    public override object keyLoop(client user = null)
    {
        Calls?.Add(Dm.str(key) + (user == null ? " (no client)" : ""));
        return base.keyLoop(user);
    }
}

/// <summary>
/// A plain mob whose Login sleeps before tg's (tg's warning at login.dm:2-5: "Anything that sleeps can result in the client
/// reference being dropped"). A fixture type of T1's own.
/// </summary>
[DmPath("/mob/test_m1t1_slow_login")]
public class mob_test_m1t1_slow_login : mob
{
    /// <summary>Ticks to sleep before tg's Login.</summary>
    public int ticks = 1;

    /// <summary>Whether client was set when the sleep ended.</summary>
    public bool client_after_sleep;

    public bool login_returned;

    public object login_result;

    public override async DmTask<object> Login()
    {
        await sleep(world.tick_lag * ticks);
        client_after_sleep = client != null;
        login_result = await base.Login();
        login_returned = true;
        return login_result;
    }
}

/// <summary>A plain mob that records the <c>usr</c> of its Login and Logout (BYOND ref /client/proc/New: usr is the mob of the key).</summary>
[DmPath("/mob/test_m1t1_usr_recorder")]
public class mob_test_m1t1_usr_recorder : mob
{
    [NotHashed]
    public List<string> Log = new List<string>();

    public override async DmTask<object> Login()
    {
        Log.Add("Login usr=" + (usr == this ? "self" : usr == null ? "null" : "other"));
        return await base.Login();
    }

    public override async DmTask<object> Logout()
    {
        Log.Add("Logout usr=" + (usr == this ? "self" : usr == null ? "null" : "other"));
        return await base.Logout();
    }
}

/// <summary>
/// A plain mob whose Login first registers the test listener on the connecting client (the client exists only from the
/// connection on, so a listener cannot be put on it before), then runs tg's Login. A fixture type of T1's own.
/// </summary>
[DmPath("/mob/test_m1t1_watched")]
public class mob_test_m1t1_watched : mob
{
    [NotHashed]
    public datum_test_m1t1_listener Listener;

    public override async DmTask<object> Login()
    {
        if (client != null)
            Listener?.WatchClient(client);
        return await base.Login();
    }
}
