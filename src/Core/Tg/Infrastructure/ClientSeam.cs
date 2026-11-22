using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Owners (docs/ai/specs/m1-ownership.md): the host seam (IClientHost, SetHost, HostOf, NotifyClientDeleted,
// NotifyClientMobChanged) is the M1 contracts step's and complete; the host entry points (KeyMob, Connect,
// SetHeldDirections, Disconnect) and ClientConnection's filling are M1 P1's (the client layer), which P3 (the server host)
// calls. Host code, not tg code: it stands for BYOND's engine side of a player's connection.

/// <summary>
/// The engine-to-host half of the client seam: what the <c>/client</c> engine part (P1, <c>Builtin/client.builtin.cs</c>)
/// tells the server host. Installed per world (<see cref="ClientSeam.SetHost"/>). Calls come synchronously from inside the
/// world, in the order of the simulation; the host only records them and acts after <c>World.Step</c> (it never calls into
/// the world from them). Chat to the player (<c>to_chat</c>) does not come here: it goes through the message sink
/// (<c>IMessageSink</c>, presentation-seam.md section 3).
/// </summary>
public interface IClientHost
{
    /// <summary>
    /// <paramref name="player"/> is gone from the world: tg deleted it (<c>qdel(client)</c>: a kick, tg's keysend flood
    /// autokick; in BYOND deleting a client disconnects the player) or <see cref="ClientSeam.Disconnect"/> ran
    /// <c>client/Del</c>. Called once per client, whichever side ended it. The host closes the session's connection (after
    /// the tick) unless it already did, and drops its reference to the client.
    /// </summary>
    void ClientDeleted(client player);

    /// <summary>
    /// <paramref name="player"/>'s mob changed from <paramref name="old_mob"/> to <paramref name="new_mob"/>: <c>client/New</c>'s
    /// BYOND part attached it, a <c>mob.key</c> or <c>client.mob</c> assignment moved it (Logout on the old mob, Login on the
    /// new), or the mob was deleted (<paramref name="new_mob"/> null). The host updates the session's own entity.
    /// </summary>
    void ClientMobChanged(client player, mob old_mob, mob new_mob);
}

/// <summary>
/// The host's handle on a connecting client (<see cref="ClientSeam.Connect"/>). The <c>/client</c> exists at once;
/// its <c>New</c>, which attaches the mob and runs <c>mob/Login</c>, may sleep (both slots suspend, <c>slots.tsv</c>), so the
/// handle says when it returned (scope risk 11). The host checks it after each <c>World.Step</c>, sends the Welcome once
/// <see cref="Done"/> and <see cref="Accepted"/>, and drops the handle.
/// </summary>
public sealed class ClientConnection
{
    internal ClientConnection(client player)
    {
        Client = player;
    }

    /// <summary>The new client (registered, its BYOND vars set before <c>New</c> ran).</summary>
    public client Client { get; }

    /// <summary>True once <c>client/New</c> has returned (or failed).</summary>
    public bool Done { get; internal set; }

    /// <summary>What <c>client/New</c> returned: the connected mob, or null when it refused the connection (BYOND: "null to disallow the connection").</summary>
    public mob Mob { get; internal set; }

    /// <summary>The runtime error that ended <c>client/New</c>, if any (the world reported it too).</summary>
    public Exception Error { get; internal set; }

    /// <summary>Done, without an error, with a mob: the player is in the world.</summary>
    public bool Accepted => Done && Error == null && Mob != null;
}

/// <summary>
/// The client seam (scope 2b): how the server host creates, drives and ends tg's <c>/client</c> of a session, and how the
/// <c>/client</c> engine part tells the host about it.
/// <para>
/// <b>Host to engine</b> (P1 implements; P3 calls in the command phase of <c>World.Step</c>, ambient world set, one command
/// at a time in (session ordinal, seq) order). Each entry point sets BYOND's <c>usr</c> for what it runs, as BYOND does, and
/// gives the caller's back. The order for a first join (the M10 lobby stand-in, scope 2b "Stub in M1"): the host creates
/// the avatar (<c>new /mob</c> at a spawn point of the <see cref="TestRoom"/>), keys it with <see cref="KeyMob"/>, then calls
/// <see cref="Connect"/>, whose BYOND part finds the mob with the client's key and attaches to it instead of creating a
/// <c>world.mob</c>, as after tg's <c>mind.transfer_to</c>. A reconnect calls <see cref="Connect"/> with the same key: the
/// mob kept it (tg's SSD), so the client attaches to it again.
/// </para>
/// <para>
/// <b>Engine to host</b> (complete): <see cref="SetHost"/> installs the world's <see cref="IClientHost"/>;
/// <see cref="NotifyClientDeleted"/> and <see cref="NotifyClientMobChanged"/> are what the engine part calls. A world without
/// a host ignores them. The slot is per world (a Kernel extension: no static state).
/// </para>
/// </summary>
public static partial class ClientSeam
{
    // ---- engine to host (complete) ----

    /// <summary>Installs <paramref name="host"/> as the client host of <paramref name="world"/> (null removes it).</summary>
    public static void SetHost(World world, IClientHost host)
    {
        ArgumentNullException.ThrowIfNull(world);
        Slot.Of(world).Host = host;
    }

    /// <summary>The client host of <paramref name="world"/>, null when none is installed.</summary>
    public static IClientHost HostOf(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Slot.Of(world).Host;
    }

    /// <summary>For the <c>/client</c> engine part: tells the ambient world's host that <paramref name="player"/> is gone. No ambient world or no host: nothing.</summary>
    public static void NotifyClientDeleted(client player)
    {
        World world = World.CurrentOrNull;
        if (world != null) Slot.Of(world).Host?.ClientDeleted(player);
    }

    /// <summary>For the <c>/client</c> engine part: tells the ambient world's host that <paramref name="player"/>'s mob changed. No ambient world or no host: nothing.</summary>
    public static void NotifyClientMobChanged(client player, mob old_mob, mob new_mob)
    {
        World world = World.CurrentOrNull;
        if (world != null) Slot.Of(world).Host?.ClientMobChanged(player, old_mob, new_mob);
    }

    // ---- host to engine (M1 P1) ----

    /// <summary>
    /// BYOND's <c>mob.key = key</c> (BYOND ref <c>/mob/var/key</c>): sets the mob's <c>key</c> and <c>ckey</c>; a connected
    /// client of that key moves to this mob (Logout on its old mob, Login on this one) and any other mob with that key loses
    /// it. The host keys a new avatar before <see cref="Connect"/>.
    /// </summary>
    public static void KeyMob(mob avatar, string key)
    {
        ArgumentNullException.ThrowIfNull(avatar);
        avatar.key = key;
    }

    /// <summary>
    /// A player connects (BYOND's side of a new connection): creates the <c>/client</c>, sets its BYOND vars
    /// (<c>key</c>, <c>ckey</c>, <c>address</c>, <c>computer_id</c>, <c>connection</c> "seeker", <c>byond_version</c>,
    /// <c>byond_build</c>) and runs tg's <c>client/New</c> detached (with <c>usr</c> the mob of that key, BYOND ref
    /// <c>/client/proc/New</c>): it runs until its first sleep, and the returned handle tells when it is
    /// <see cref="ClientConnection.Done"/>. If the session drops before that, the host calls <see cref="Disconnect"/> on
    /// <see cref="ClientConnection.Client"/> all the same (tg's <c>login.dm:2-5</c> warns about it).
    /// </summary>
    public static ClientConnection Connect(string key, string address = "127.0.0.1", string computer_id = null)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("A connecting player has a key.", nameof(key));
        World world = World.Current;
        client player = client.dm_engine_create(key, address, computer_id);
        var connection = new ClientConnection(player);
        object caller_usr = world.usr;
        try
        {
            // BYOND ref /client/proc/New, Args: "usr: The mob in the world with the same key as the player, if it exists."
            world.usr = client.dm_mob_with_key(player.ckey);
            Waiting.Detach(RunNew(player, connection));
        }
        finally
        {
            world.usr = caller_usr;
        }
        return connection;
    }

    // The engine's call of tg's client/New: a top-level call, so an isolation boundary as Waiting.Detach is (rules 9.2,
    // waiting-model.md 9.2), which also records the error for the host. BYOND ref /client/proc/New: New returns the mob,
    // "or null to disallow the connection", and a connection that is not accepted is closed (BYOND's client/Del).
    private static async DmTask RunNew(client player, ClientConnection connection)
    {
        try
        {
            object returned = await player.New(null);
            // A client tg deleted while New ran (tg's `qdel(src)` then `return`) refused the connection too.
            connection.Mob = player.gc_destroyed == null ? returned as mob : null;
        }
        catch (client.ClientDeletedKill)
        {
            connection.Mob = null; // the player left while Login slept: BYOND killed client/New silently
        }
        catch (Exception e)
        {
            connection.Error = e;
            World.Current.ReportRuntime(e);
        } // isolation boundary (translation-rules.md 9.2): the engine's call of client/New
        connection.Done = true;
        if (!connection.Accepted)
            player.dm_disconnect();
    }

    /// <summary>
    /// The movement keys the player holds now (an InputFrame), as BYOND direction bits (NORTH 1, SOUTH 2, EAST 4, WEST 8;
    /// other bits throw <see cref="ArgumentOutOfRangeException"/>, the host validates first): compared with what the
    /// client holds, tg's <c>keyUp</c> verb runs for each released direction, then <c>keyDown</c> for each pressed one, each
    /// in the order NORTH, SOUTH, EAST, WEST, with the direction's default movement hotkey (tg's
    /// <c>/datum/keybinding/movement</c>), with <c>usr</c> the client's mob. tg's flood rule (<c>MAX_KEYPRESS_AUTOKICK</c>)
    /// runs inside and may delete the client (<see cref="IClientHost.ClientDeleted"/>). <c>SSinput</c> moves the mob in the
    /// next tick.
    /// </summary>
    public static void SetHeldDirections(client player, int dirs)
    {
        ArgumentNullException.ThrowIfNull(player);
        if ((dirs & ~0xF) != 0)
            throw new ArgumentOutOfRangeException(nameof(dirs), dirs, "Only the bits NORTH, SOUTH, EAST and WEST are movement keys.");
        if (!player.dm_connected)
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            // BYOND sends a verb per key event; each runs as its own call (detached), in the order of the events.
            for (int dir = 1; dir <= 8; dir <<= 1) // NORTH, SOUTH, EAST, WEST
            {
                string hotkey = MovementHotkey(dir);
                if ((dirs & dir) == 0 && Dm.@in(hotkey, player.keys_held))
                {
                    if (!StillConnected(player))
                        return;
                    Waiting.Detach(player.keyUp(hotkey));
                }
            }
            for (int dir = 1; dir <= 8; dir <<= 1) // NORTH, SOUTH, EAST, WEST
            {
                string hotkey = MovementHotkey(dir);
                if ((dirs & dir) != 0 && !Dm.@in(hotkey, player.keys_held))
                {
                    if (!StillConnected(player))
                        return;
                    Waiting.Detach(player.keyDown(hotkey));
                }
            }
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// The default movement hotkey of a direction that <see cref="SetHeldDirections"/> sends: the arrow key of tg's
    /// <c>/datum/keybinding/movement/north</c> and its siblings (<c>hotkey_keys = list("W", "North")</c> and so on,
    /// code/datums/keybinding/movement.dm:5-31).
    /// </summary>
    public static string MovementHotkey(int dir) => dir switch
    {
        1 => "North",
        2 => "South",
        4 => "East",
        8 => "West",
        _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, "Not a cardinal direction."),
    };

    // A client deleted by an earlier key event (tg's flood kick) takes no more.
    private static bool StillConnected(client player) => player.dm_connected && player.gc_destroyed == null;

    /// <summary>
    /// The player's connection ended (BYOND's side of a disconnect): runs tg's <c>client/Del</c> detached (<c>Destroy</c>,
    /// <c>become_uncliented</c>, <c>mob/Logout</c>); the mob keeps its key. <see cref="IClientHost.ClientDeleted"/> follows.
    /// </summary>
    public static void Disconnect(client player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.dm_disconnect();
    }

    // The per-world slot of the host (World.GetExtension: one per world, no static state).
    private sealed class Slot
    {
        internal IClientHost Host;

        internal static Slot Of(World world) => world.GetExtension(static _ => new Slot());
    }
}
