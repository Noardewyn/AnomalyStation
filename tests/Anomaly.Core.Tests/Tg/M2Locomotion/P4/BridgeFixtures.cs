using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.M1Client.P1;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.P4;

/// <summary>
/// A booted toy room as the M2 server runs it (M2 P4's own tests of the bridge, scope 2b): the client seam's host, a
/// player command phase, then the locomotion phase installed right after it (<c>LocomotionSeam.Install</c>). Humans join
/// with a client and are registered as capsule mobs; frames and actions are queued as commands for the next tick, as the
/// host's jitter buffer gives them. The world keeps <c>NotTranslatedThrows</c> on, so a reached <c>Dm.NotTranslated</c>
/// fails the test.
/// <para>
/// A human's movement speed (<c>cached_multiplicative_slowdown</c>) is tg's: its walk and run movespeed modifiers read
/// tg's configuration, which the room sets to tg's shipped <c>RUN_DELAY</c> 1.5 ds and <c>WALK_DELAY</c> 4 ds
/// (<c>config/game_options.txt</c>), as the server does. The room is 16 by 16 tiles with walls on its rim: the floor is 2..15.
/// </para>
/// </summary>
internal sealed class CapsuleRoom
{
    public const int Size = 16;

    public TgToyRoom Room { get; }

    public World World => Room.World;

    public RecordingHost Host { get; } = new RecordingHost();

    private readonly Queue<Action> _commands = new Queue<Action>();

    public CapsuleRoom(ulong seed = 41)
    {
        Room = TgToyRoom.Boot(Size, Size, seed: seed);
        DmWorldState.Of(World).NotTranslatedThrows = true;
        ClientSeam.SetHost(World, Host);
        World.AddPhase("test player commands", _ =>
        {
            while (_commands.Count > 0)
                _commands.Dequeue()();
        });
        LocomotionSeam.Install(World);
        In(() =>
        {
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_movedelay_run_delay), 1.5f));
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_movedelay_walk_delay), 4f));
        });
    }

    public void Command(Action command) => _commands.Enqueue(command);

    public void Step(int ticks = 1) => World.Step(ticks);

    public void In(Action body) => Room.In(body);

    public T In<T>(Func<T> body) => Room.In(body);

    /// <summary>A human on (x, y), joined by a player of that key and registered as a capsule mob (the host's join).</summary>
    public mob_living_carbon_human Join(int x, int y, string key) => Join<mob_living_carbon_human>(x, y, key);

    public T Join<T>(int x, int y, string key) where T : mob_living
    {
        T body = In(() => Dm.@new<T>(locate(x, y, 1)));
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
        Assert.Same(body.client, connection.Client);
        return body;
    }

    /// <summary>A human with no player (a dummy), registered as a capsule mob as the dev spawn should.</summary>
    public mob_living_carbon_human Dummy(int x, int y)
    {
        mob_living_carbon_human body = In(() => Dm.@new<mob_living_carbon_human>(locate(x, y, 1)));
        Command(() => LocomotionSeam.Register(body));
        Step();
        return body;
    }

    /// <summary>The frame of the next tick for <paramref name="who"/> (the host's Motion command).</summary>
    public void Frame(mob_living who, sbyte moveX, sbyte moveY, ushort yaw = 0)
    {
        uint seq = ++_seq;
        Command(() => LocomotionSeam.SetFrame(who, new MotionInput(seq, moveX, moveY, yaw, MotionInputFlags.None)));
    }

    private uint _seq;

    /// <summary>Steps <paramref name="ticks"/> ticks with the same frame each tick.</summary>
    public void Walk(mob_living who, sbyte moveX, sbyte moveY, int ticks, ushort yaw = 0)
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

    /// <summary>The invariant of the bridge: the capsule's tile (a slide's target while sliding) is the mob's loc.</summary>
    public void AssertOnLoc(mob_living who)
    {
        MotionState s = State(who);
        int tx = s.Slide.Active ? MotionMath.TileOf(s.Slide.TargetX) : s.TileX;
        int ty = s.Slide.Active ? MotionMath.TileOf(s.Slide.TargetY) : s.TileY;
        (int lx, int ly, int lz) = In(() => ((int)who.loc.x, (int)who.loc.y, (int)who.loc.z));
        Assert.Equal((lx, ly, lz), (tx, ty, s.Z));
    }

    public (int X, int Y) Tile(mob_living who) => In(() => ((int)who.loc.x, (int)who.loc.y));

    public void AssertNoRuntimes() => Assert.True(Room.Runtimes.Count == 0, string.Join("\n", Room.RuntimeText));
}

/// <summary>
/// A recording listener for the bridge's tests (rules 15: a recording harness; a fixture type of the port's own,
/// <c>/datum/test_m2p4_listener</c>): the movement, bump and click signals of the mobs and turfs it watches, in order.
/// </summary>
[DmPath("/datum/test_m2p4_listener")]
public class datum_test_m2p4_listener : datum
{
    public readonly List<string> Trace = new List<string>();

    /// <summary>Block the watched mob's next moves (COMSIG_MOVABLE_PRE_MOVE returns the block bit).</summary>
    public bool BlockMoves;

    /// <summary>Block the client's moves (COMSIG_MOB_CLIENT_PRE_MOVE returns the block bit).</summary>
    public bool BlockClientMoves;

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public void Watch(mob who)
    {
        RegisterSignal(who, COMSIG_MOVABLE_PRE_MOVE, nameof(on_pre_move));
        RegisterSignal(who, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        RegisterSignal(who, COMSIG_MOVABLE_BUMP, nameof(on_bump));
        RegisterSignal(who, COMSIG_MOB_CLIENT_MOVED, nameof(on_client_moved));
        RegisterSignal(who, COMSIG_MOB_CLIENT_PRE_MOVE, nameof(on_client_pre_move));
        RegisterSignal(who, COMSIG_LIVING_MOB_BUMP, nameof(on_mob_bump));
        RegisterSignal(who, COMSIG_LIVING_MOB_BUMPED, nameof(on_mob_bumped));
        RegisterSignal(who, COMSIG_ATOM_DIR_CHANGE, nameof(on_dir_change));
        RegisterSignal(who, COMSIG_ATOM_ATTACK_HAND, nameof(on_attack_hand));
        RegisterSignal(who, COMSIG_ATOM_ATTACK_HAND_SECONDARY, nameof(on_attack_hand_secondary));
        RegisterSignal(who, COMSIG_MOB_ATTACK_RANGED_SECONDARY, nameof(on_ranged_secondary));
        RegisterSignal(who, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarmed_attack));
    }

    public int Count(string prefix) => Trace.Count(t => t.StartsWith(prefix, StringComparison.Ordinal));

    [SignalHandler]
    public virtual object on_pre_move(datum source = null, object newloc = null)
    {
        Trace.Add("pre_move " + At(newloc));
        return BlockMoves ? COMPONENT_MOVABLE_BLOCK_PRE_MOVE : NONE;
    }

    [SignalHandler]
    public virtual void on_moved(datum source = null, object old_loc = null, object movement_dir = null, object forced = null)
        => Trace.Add("moved " + At(old_loc) + " " + Dm.str(movement_dir) + " forced=" + Dm.str(forced));

    [SignalHandler]
    public virtual void on_bump(datum source = null, object bumped = null)
        => Trace.Add("bump " + (bumped is turf ? "turf " + At(bumped) : DmTypes.PathOf(bumped.GetType())));

    [SignalHandler]
    public virtual void on_client_moved(datum source = null, object direct = null, object old_dir = null, object old_loc = null)
        => Trace.Add("client_moved " + Dm.str(direct) + " " + Dm.str(old_dir) + " " + At(old_loc));

    [SignalHandler]
    public virtual object on_client_pre_move(datum source = null, object args = null)
    {
        Trace.Add("client_pre_move");
        return BlockClientMoves ? COMSIG_MOB_CLIENT_BLOCK_PRE_MOVE : NONE;
    }

    [SignalHandler]
    public virtual void on_mob_bump(datum source = null, object bumped = null) => Trace.Add("mob_bump " + RefOf(bumped));

    [SignalHandler]
    public virtual void on_mob_bumped(datum source = null, object bumper = null) => Trace.Add("mob_bumped " + RefOf(bumper));

    [SignalHandler]
    public virtual void on_dir_change(datum source = null, object old_dir = null, object new_dir = null)
        => Trace.Add("dir " + Dm.str(old_dir) + " " + Dm.str(new_dir));

    [SignalHandler]
    public virtual void on_attack_hand(datum source = null, object user = null, object modifiers = null)
        => Trace.Add("attack_hand " + RefOf(user) + " " + Mods(modifiers));

    [SignalHandler]
    public virtual void on_attack_hand_secondary(datum source = null, object user = null, object modifiers = null)
        => Trace.Add("attack_hand_secondary " + RefOf(user) + " " + Mods(modifiers));

    [SignalHandler]
    public virtual void on_ranged_secondary(datum source = null, object target = null, object modifiers = null)
        => Trace.Add("ranged_secondary " + RefOf(target));

    [SignalHandler]
    public virtual void on_unarmed_attack(datum source = null, object target = null, object proximity = null, object modifiers = null)
        => Trace.Add("unarmed " + RefOf(target) + " " + Dm.str(proximity));

    private static string At(object place) => place is atom a ? Dm.str(a.x) + "," + Dm.str(a.y) : Dm.str(place);

    private static string RefOf(object thing) => thing is datum d ? "#" + d.dm_id.ToString(System.Globalization.CultureInfo.InvariantCulture) : Dm.str(thing);

    private static string Mods(object modifiers) => modifiers is DmList l && Dm.truthy(l[RIGHT_CLICK]) ? "right" : "left";
}

/// <summary>
/// A human whose active hand is empty (a fixture type of the port's own, <c>/mob/living/carbon/human/test_m2p4_hands</c>):
/// tg's <c>get_active_held_item</c> (<c>inventory.dm:5</c>) is a skeleton until M2 P1 translates the inventory, and
/// <c>ClickOn</c> asks it on every click. The override is tg's own two lines (<c>get_item_for_held_index</c>).
/// </summary>
[DmPath("/mob/living/carbon/human/test_m2p4_hands")]
public class mob_living_carbon_human_test_m2p4_hands : mob_living_carbon_human
{
    public override object get_active_held_item()
    {
        float i = active_hand_index;
        if (i > 0 && i <= held_items.len)
            return held_items[i];
        return null;
    }
}
