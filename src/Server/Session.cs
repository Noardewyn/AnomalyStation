using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server;

// Owner: M1 P3 (server host). Contract by the M1 contracts step; P3 adds what its state machine needs (rate buckets,
// strikes) and owns every transition.

/// <summary>
/// Where a session is in its life (scope 2b, 2e). Transitions happen on the host thread: the network side in step 2 of the
/// tick (poll), the world side in the command phase of <c>World.Step</c>.
/// <code>
///   Hello accepted ──► Joining ──(Join applied: avatar keyed, client/New started)──► LoggingIn
///   LoggingIn ──(client/New returned with a mob: Welcome sent)──► Joined
///   LoggingIn ──(client/New refused or failed)──► Leaving (Refused)
///   Joining ──(connection lost before the Join applied: the Join is dropped)──► Closed
///   LoggingIn | Joined ──(connection lost, kick, strikes, flood, replaced, shutdown, client deleted)──► Leaving
///   Leaving ──(Leave applied: client/Del, or the client was already deleted)──► Closed
/// </code>
/// A reconnect (the same name with the session's token) is a new session with the same key, which attaches to the mob
/// that kept the key (tg's SSD); a live old session is closed with <see cref="DisconnectReason.Replaced"/> first.
/// </summary>
public enum SessionState : byte
{
    /// <summary>The connection request was accepted; its Join command is queued for the next tick.</summary>
    Joining,

    /// <summary>The Join ran: the host keyed the avatar and called <c>ClientSeam.Connect</c>; tg's <c>client/New</c> (and the <c>mob/Login</c> it runs) may still be sleeping. No Welcome yet, and no command but Leave is accepted.</summary>
    LoggingIn,

    /// <summary><c>client/New</c> returned: the Welcome and the join sequence were sent; the session gets a Delta every tick and its commands are queued.</summary>
    Joined,

    /// <summary>The connection dropped or was closed; the Leave command is queued (tg's <c>client/Del</c> runs in the next tick; the mob keeps its key, tg's SSD).</summary>
    Leaving,

    /// <summary>Gone. A reconnect with the token is a new <see cref="Session"/> with the same <see cref="Key"/>.</summary>
    Closed,
}

/// <summary>
/// One connected player as the server host sees it (scope 2b "Mapping": peer, name, token, strikes, rate buckets, state,
/// byte counters). The world never sees a Session: it sees tg's <c>/client</c>, made by <c>ClientSeam.Connect</c>.
/// <para>
/// A replay (<c>--replay</c>) makes sessions without a <see cref="Connection"/>: the command phase works on sessions in
/// both runs, so the world gets the same calls; only the network half is missing.
/// </para>
/// </summary>
public sealed class Session
{
    public Session(int ordinal, string key, SessionToken token, IConnection connection)
    {
        Ordinal = ordinal;
        Key = key;
        Ckey = Anomaly.Core.DmShim.Dm.ckey(key);
        Token = token;
        Connection = connection;
    }

    /// <summary>Assigned in acceptance order from 1, never reused in a run (0 is the console); commands of one tick apply in (ordinal, seq) order.</summary>
    public int Ordinal { get; }

    /// <summary>The player's BYOND key: the Hello's name (tg's <c>client.key</c>; <c>ckey</c> is <c>ckey(Key)</c>).</summary>
    public string Key { get; }

    /// <summary>The canonical key, <c>ckey(Key)</c>: the player's identity (two names with one ckey are one player, as in BYOND).</summary>
    public string Ckey { get; }

    /// <summary>The reconnect token given in Welcome.</summary>
    public SessionToken Token { get; }

    /// <summary>M10 slice I: the connection's IP address (tg's <c>client.address</c>), null for a local one (a loopback test client, a replay).</summary>
    public string Address { get; init; }

    /// <summary>M10 slice I: when the key was last seen before this connection (tg's lastseen), for its unread notes; null for a new key.</summary>
    public string LastSeenBefore { get; init; }

    /// <summary>The transport connection (null in a replay).</summary>
    public IConnection Connection { get; }

    public SessionState State { get; set; }

    /// <summary>
    /// The tg client while the session is in the world (from the Join to the Leave). The host holds it only while
    /// <c>GLOB.clients</c> does too and drops it at <c>IClientHost.ClientDeleted</c>, so it never keeps a deleted client
    /// alive (a host reference would change the registry's live set and the state hash).
    /// </summary>
    public client Client { get; set; }

    /// <summary>The world id of the client's mob (<c>IClientHost.ClientMobChanged</c>), 0 without one; the own entity of the Deltas.</summary>
    public long OwnEntity { get; set; }

    /// <summary>The tick of the Join (the Snapshot's tick).</summary>
    public long JoinedTick { get; set; }

    /// <summary>The highest MotionInput frame seq received (the jitter buffer's newest).</summary>
    public uint LastReceivedInputSeq { get; set; }

    /// <summary>The highest MotionInput frame seq the world applied (the jitter buffer gave it to a tick): the header's <c>lastInputSeq</c> of the Deltas.</summary>
    public uint LastAppliedInputSeq { get; set; }

    /// <summary>Payload bytes the host encoded for this session (its own counter; the transport's is on <see cref="Connection"/>).</summary>
    public long BytesSent { get; set; }

    /// <summary>Payload bytes received from this session.</summary>
    public long BytesReceived { get; set; }

    // ---- added by P3 ----

    /// <summary>The session's jitter buffer of MotionInput frames (M2): one frame is taken per tick.</summary>
    public InputJitterBuffer Jitter { get; } = new();

    /// <summary>Why the host closed or will close the session (<see cref="DisconnectReason.None"/> while it is open or when the transport ended it).</summary>
    public DisconnectReason CloseReason { get; internal set; }

    /// <summary>The text sent with <see cref="CloseReason"/>.</summary>
    public string CloseText { get; internal set; }

    /// <summary>Protocol violations counted (all time).</summary>
    public int Strikes { get; internal set; }

    /// <summary>MotionInput messages and actions dropped by the token buckets (all time).</summary>
    public int RateLimited { get; internal set; }

    /// <summary>Version 53: the bug report being received (its parts so far), or null; true in <see cref="BugReportRefused"/> while a refused one's parts still come.</summary>
    internal System.IO.MemoryStream BugReportBody { get; set; }

    internal int BugReportTotal { get; set; }

    internal bool BugReportRefused { get; set; }

    /// <summary>Actions the world applied (rest, intent, combat mode, clicks, dev commands), all time.</summary>
    public int ActionsApplied { get; internal set; }

    /// <summary>
    /// Clicks and hovers the world dropped because their target was forged or stale (all time): an id that is no object, an entity
    /// the replicator does not show, a turf outside the map, on another level or out of tg's view range, a screen id that is not in
    /// the player's <c>client.screen</c> now (docs/ai/specs/interaction.md section 2). A click that races a despawn is normal.
    /// </summary>
    public int ClicksRejected { get; internal set; }

    /// <summary>Version 4: hovers dropped over <see cref="ActionLimits.Hover"/> (silently: sweeping the view over a crowd is normal) or replaced by a newer one before they ran (all time).</summary>
    public int HoversDropped { get; internal set; }

    /// <summary>Version 4: hovers the world ran (all time; at most one per tick).</summary>
    public int HoversApplied { get; internal set; }

    /// <summary>Version 15: radial picks the world applied (ClientSeam.RadialPick).</summary>
    public int RadialPicksApplied { get; internal set; }

    /// <summary>Version 16: tgui acts the world applied (ClientSeam.TguiMessage "act/...").</summary>
    public int UiActsApplied { get; internal set; }

    /// <summary>Version 16: tgui window closes the world applied (ClientSeam.TguiMessage "close").</summary>
    public int UiClosesApplied { get; internal set; }

    /// <summary>Version 16: UiWindow messages sent (updates and closes).</summary>
    public int UiWindowsSent { get; internal set; }

    /// <summary>Version 16: UiWindow messages over the caps, not sent.</summary>
    public int UiWindowsDropped { get; internal set; }

    /// <summary>Version 17: drops the world applied (ClientSeam.Drop: tg's /client/MouseDrag and /client/MouseDrop).</summary>
    public int DropsApplied { get; internal set; }

    /// <summary>Version 34: MouseDown, MouseDrag and MouseUp actions the world applied (ClientSeam.MouseDown, MouseDrag, MouseUp).</summary>
    public int MouseActionsApplied { get; internal set; }

    /// <summary>Version 34: drags dropped over <see cref="Limits.MouseDragRate"/> or replaced by a newer one before they ran (silently).</summary>
    public int MouseDragsDropped { get; internal set; }

    /// <summary>Version 34 (DV94): aims whose eye was too far from the mob's own position or height and was replaced by the server's.</summary>
    public int AimsCorrected { get; internal set; }

    /// <summary>Version 17: drops dropped in the command phase because the dragged thing or what it was released over is not the player's to see (forged or stale; never a strike).</summary>
    public int DropsRejected { get; internal set; }

    /// <summary>Version 24: chat link clicks the world applied (ClientSeam.Topic, D154).</summary>
    public int TopicsApplied { get; internal set; }

    /// <summary>Version 24: chat link clicks dropped because the server did not send that link to this session (forged, or forgotten among <see cref="ChatLinks.Capacity"/> newer ones; never a strike, DV59).</summary>
    public int TopicsRefused { get; internal set; }

    /// <summary>Version 24 (D154): the hrefs of the chat links sent to this session.</summary>
    public ChatLinks ChatLinks { get; } = new();

    /// <summary>Version 19: Tooltip messages sent (shows and hides).</summary>
    public int TooltipsSent { get; internal set; }

    /// <summary>Version 21: CameraShake messages sent.</summary>
    public int CameraShakesSent { get; internal set; }

    /// <summary>Version 84 (gaps E2a): what the last WeatherMessage to this join's client said (its level and layers), null before the first.</summary>
    public string WeatherKey { get; set; }

    /// <summary>Version 67 (D315): the levels whose minimap this join's client has.</summary>
    public HashSet<int> MinimapLevels { get; } = new();

    /// <summary>D315: the dev command <c>minimapmobs on</c>: the living mobs of the own level go to this session twice a second.</summary>
    public bool MinimapMobs { get; set; }

    /// <summary>Version 70 (D320): the build preview this join's client has (none until one is sent).</summary>
    public BuildPreviewMessage BuildPreview { get; set; } = BuildPreviewMessage.None;

    /// <summary>Version 92: the gun sight the client was last sent (<see cref="GunSightMessage.None"/> after a join).</summary>
    public GunSightMessage GunSight { get; set; } = GunSightMessage.None;

    /// <summary>Version 79 (silicons SI4d): how many of the world's runtime resources (DmWorldState.RuntimeResourceOrder) this client was sent.</summary>
    public int RuntimeResourcesSent { get; set; }

    /// <summary>Version 15: RadialMenu messages sent (opens, new pages and closes).</summary>
    public int RadialsSent { get; internal set; }

    /// <summary>Version 4: keybinding presses and releases the world ran (all time).</summary>
    public int KeybindingsApplied { get; internal set; }

    /// <summary>True while the session is in the world (from its Join to its Leave).</summary>
    public bool InWorld => State is SessionState.LoggingIn or SessionState.Joined;

    // tg's client/New in flight (from the Join until it returned); dropped then, so it keeps nothing alive.
    internal ILoginHandle Login;

    // The ticks of the last strikes (a ring of Limits.MaxStrikes) and of the last rate-limited frames.
    internal readonly long[] StrikeTicks = new long[Limits.MaxStrikes];
    internal int StrikeCursor;
    internal readonly Queue<long> FloodTicks = new();

    // The token buckets (D40): MotionInput messages, all actions, and the per-kind buckets of the actions.
    internal TokenBucket MotionBucket = new(Limits.MotionInputRate);
    internal TokenBucket ActionBucket = new(Limits.ActionRate);
    internal TokenBucket ToggleBucket = new(ActionLimits.Toggle);
    internal TokenBucket ClickBucket = new(ActionLimits.Click);
    internal TokenBucket DevBucket = new(ActionLimits.Dev);

    // Version 5: the dev menu's gas probes (ActionLimits.DevProbe), and whether the session was told the server has no dev tools.
    internal TokenBucket DevProbeBucket = new(ActionLimits.DevProbe);
    internal bool DevProbeRefused;

    /// <summary>D322: what the client was last told about the dev menu; null: nothing yet.</summary>
    internal bool? DevAccessSent;
    internal TokenBucket KeybindingBucket = new(ActionLimits.Keybinding);
    internal TokenBucket HoverBucket = new(ActionLimits.Hover);

    // Version 34: the held button's drags (Limits.MouseDragRate), outside the overall action bucket.
    internal TokenBucket MouseDragBucket = new(Limits.MouseDragRate);

    /// <summary>Version 38: the say window's messages (<see cref="Limits.SayRate"/>).</summary>
    internal TokenBucket SayBucket = new(Limits.SayRate);

    // Actions that wait for the tick of their frame (ServerHost.FormInputs releases them).
    internal readonly List<ActionMessage> PendingActions = new();

    // Version 43: the Action messages decoded from this connection (the client's count of what it sent); those not still pending are
    // settled (applied or dropped), the private block's ActionsDone.
    internal uint ActionsReceived;

    // Version 43: the own mob's access (tg's get_access()) as the host last read it inside the world, null before the first read.
    internal List<string> Access;

    // Version 89: tg's ClickOn refuses the own mob's clicks (incapacitated or TRAIT_HANDS_BLOCKED), read with the access.
    internal bool HandsBlocked;

    // The host's clock when each pending action arrived (Stopwatch ticks), aligned with PendingActions: a click's age when its
    // Delta is sent is the server's part of click_ms (ServerHost.ClickLatency).
    internal readonly List<long> PendingStamps = new();

    /// <summary>Actions waiting for the tick of their frame (at most <see cref="ActionLimits.MaxPendingActions"/>).</summary>
    public int PendingActionCount => PendingActions.Count;

    // Consecutive ticks the jitter buffer repeated the last frame, and the last frame the host queued as a command.
    internal int RepeatedTicks;
    internal MotionInput LastQueuedFrame;
    internal bool HaveQueuedFrame;

    // The seq and yaw of the last frame the world applied as a command (world-half fields: the idle frame of a leaving session
    // keeps them, so a replay, which sees only the commands, makes the same frame). LastAppliedInputSeq is the network half's
    // acknowledgement and also follows the frames that were no command.
    internal uint LastFrameSeq;
    internal ushort LastYaw;

    // Version 95 also (slice T4b, world half): the last hover whose zone was skipped while a do-after ran for the mob, applied once it has
    // none left (ServerHost.CatchUpLooks); set and cleared only by applied commands and the world, so a replay holds the same.
    internal QueuedCommand? FrozenLook;

    // The close the host does after the tick (set inside the world, done outside it).
    internal bool CloseAfterTick;

    // Chat for the player (ServerText), sent once the session is Joined.
    internal readonly Queue<ServerText> Outbox = new();

    // Version 4: the encoded ContextHints for the player (the screentip sink fills it inside the world, SendChat sends it).
    // At most ControlOutboxCap: a slow link keeps the newest.
    internal readonly Queue<byte[]> ControlOutbox = new();
    internal const int ControlOutboxCap = 8;

    // Version 38: the encoded runechat lines and say window messages, sent with the chat (not capped: each is a line of chat).
    internal readonly Queue<byte[]> SpeechOutbox = new();

    // Version 6 (D141): what the session was sent of the air flow around it (ServerHost.SendAirflow).
    internal readonly Replication.AirflowView Airflow = new();

    // Version 13: the session's path table and the sounds it heard in the tick (ServerHost.SendSounds, Replication.SoundFeed).
    internal readonly Replication.SoundView Sounds = new();

    // Byte counters at the start of the current metrics window.
    internal long WindowSentMark;
    internal long WindowReceivedMark;
    internal bool TransportCounted;
}
