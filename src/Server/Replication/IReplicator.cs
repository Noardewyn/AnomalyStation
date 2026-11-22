using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Owner: the interface is frozen by the M1 contracts step and extended for protocol version 2 by the M2 contracts step; M1
// P4 implemented it, M2 P5 owns this folder (the version-2 members) and M2 P6 (server host) calls it. The mechanism (R3: hints select candidates, shadow copies of the NetField whitelist decide, a
// round-robin sweep, per-field change stamps; or R1 if P4's measurements say so) is P4's, reported in
// docs/ai/specs/replication.md.

/// <summary>
/// The shared, encoded changes of one tick: the body of every connection's <c>Delta</c> (<c>Codec.EncodeDeltaBody</c>),
/// encoded once; the host prefixes each connection's header (<c>Codec.EncodeDelta</c>).
/// </summary>
/// <param name="Tick">The tick.</param>
/// <param name="Body">The encoded body; valid until the next <see cref="IReplicator.BuildDelta"/>.</param>
/// <param name="Spawned">Entities spawned in the tick.</param>
/// <param name="Changed">Entities changed in the tick.</param>
/// <param name="Despawned">Entities despawned in the tick.</param>
public readonly record struct DeltaFrame(long Tick, ReadOnlyMemory<byte> Body, int Spawned, int Changed, int Despawned);

/// <summary>A session's join messages after its <c>Welcome</c>: the encoded <c>MapGrid</c> and <c>Snapshot</c>, in this order on the State channel.</summary>
public readonly record struct JoinMessages(byte[] MapGrid, byte[] Snapshot);

/// <summary>What the last <see cref="IReplicator.BuildDelta"/> did, for the metrics (scope 2i; the host times the call itself).</summary>
/// <param name="Tick">The tick.</param>
/// <param name="Candidates">Objects compared against their shadows (hinted, created, deleted and swept).</param>
/// <param name="Entities">Entities after the tick.</param>
/// <param name="BodyBytes">The encoded body's size.</param>
public readonly record struct ReplicationStats(long Tick, int Candidates, int Entities, int BodyBytes);

/// <summary>
/// What the host knows of one session for its private block (M2, protocol version 2; D40: only this session's bytes
/// carry the result). The host fills it after <c>World.Step</c>: the motion values from the locomotion seam
/// (<c>Anomaly.Core.Tg.LocomotionSeam.TryGetState</c>) and the depth from the session's jitter buffer.
/// </summary>
/// <param name="Session">The session's ordinal (the replicator keeps per-session private tables and the screen shadow under it until <see cref="IReplicator.Forget"/>).</param>
/// <param name="ClientId">The world id of the session's tg <c>/client</c> (whose <c>screen</c> list the replicator reads), 0 when it has none.</param>
/// <param name="OwnEntity">The own mob's id (the header's).</param>
/// <param name="LastInputSeq">The last applied input frame (the header's <c>lastInputSeq</c>).</param>
/// <param name="HasMotion">Whether the own mob is a capsule mob.</param>
/// <param name="Motion">Its authoritative state after <paramref name="LastInputSeq"/>.</param>
/// <param name="Params">Its motion parameters for the next tick.</param>
/// <param name="InputBufferDepth">Frames waiting in the session's jitter buffer.</param>
public readonly record struct PrivateInputs(int Session, long ClientId, long OwnEntity, uint LastInputSeq, bool HasMotion, MotionState Motion, MotionParams Params, byte InputBufferDepth)
{
    /// <summary>Version 43: the connection's Action messages the server has settled (<see cref="OwnBlock.ActionsDone"/>).</summary>
    public uint ActionsDone { get; init; }

    /// <summary>Version 89: tg's ClickOn refuses the own mob's clicks now (<see cref="OwnBlock.HandsBlocked"/>).</summary>
    public bool HandsBlocked { get; init; }

    /// <summary>The entity the client treats as its body, the header's own entity: the exosuit its mob sits in (DV358), else the mob.</summary>
    public long Body { get => _body != 0 ? _body : OwnEntity; init => _body = value; }

    private readonly long _body;

    /// <summary>Version 43: the own mob's access as the host last read it (tg's <c>get_access()</c>), null when it was never read.</summary>
    public IReadOnlyList<string> Access { get; init; }
}

/// <summary>A session's encoded private block for one tick (<c>Codec.EncodeOwnBlock</c>), appended after the shared body (<c>Codec.EncodeDelta</c> with the block).</summary>
/// <param name="Bytes">The encoded block; valid until the next <see cref="IReplicator.BuildPrivate"/> of the same session.</param>
/// <param name="ScreenChanges">Screen entries added, changed or removed.</param>
/// <param name="NewPrivateEntries">Private table entries added (strings, resources, appearances).</param>
public readonly record struct PrivateFrame(ReadOnlyMemory<byte> Bytes, int ScreenChanges, int NewPrivateEntries);

/// <summary>
/// The server's view of the world for the clients (scope 2d). How it reads the world, which is the contract the state hash
/// and the replay depend on:
/// <list type="bullet">
/// <item><description>It runs on the host thread, after <c>World.Step</c> has returned and outside the world: no ambient
/// <c>World</c> (no <c>world.Enter()</c>), so the engine getters (<c>loc</c>, <c>x</c>, <c>y</c>, <c>z</c>) read raw and apply
/// no pending DM contents edit (D95 item 10: such an edit reaches the replica one tick later at worst), and no sink
/// fires. The map is read through <c>Anomaly.Core.Tg.MapView</c>.</description></item>
/// <item><description>It only reads: it writes no tg var, calls no tg proc, creates nothing in the world, touches neither the
/// scheduler nor the RNG. So a world's state hash is the same with and without replication, and a replay without the
/// network gives the hashes of the live run.</description></item>
/// <item><description>It keeps no strong reference to a world object from one tick to the next: shadows hold values and
/// ids, and the hint sink's candidates are cleared by the <see cref="BuildDelta"/> of the same tick. So replication never
/// keeps a datum alive (the registry's weak entries, object-model.md section 6) and never changes the hash.</description></item>
/// <item><description>What it learns from the world during the tick it learns only as hints (presentation-seam.md rule 5:
/// hints select candidates, the shadows decide): the per-world <c>IReplicationHintSink</c> it installs
/// (<c>PresentationSinks.Replication</c>: moved, appearance changed, visibility changed, deleted) and the registry's id
/// watermark for created objects (<c>ObjectRegistry.HighestId</c>), docs/ai/specs/object-model.md section 6.</description></item>
/// <item><description>Only the fields of <c>Anomaly.Protocol.NetField</c> leave the server (D40).</description></item>
/// </list>
/// </summary>
public interface IReplicator
{
    /// <summary>
    /// Once, after the world booted and before the first join: installs the hint sink on <paramref name="world"/> and
    /// builds the shadows of every entity present (a full scan).
    /// </summary>
    void Attach(World world);

    /// <summary>
    /// The changes of <paramref name="tick"/> (the tick <c>World.Step</c> just finished), encoded once for every joined
    /// session. Called once per tick, in tick order, before the joins of the tick are built.
    /// </summary>
    DeltaFrame BuildDelta(long tick);

    /// <summary>
    /// The join messages of a session that joins at <paramref name="tick"/>, after that tick's <see cref="BuildDelta"/>:
    /// the map, and the Snapshot of the tick with the whole type table so far, <paramref name="ownEntity"/> and
    /// <paramref name="lastInputSeq"/> in its header. The session then gets the Deltas from <c>tick + 1</c> on.
    /// </summary>
    JoinMessages BuildJoin(long tick, long ownEntity, uint lastInputSeq);

    /// <summary>A session left: forget what is kept for it (nothing in M1; M10's per-client baselines).</summary>
    void Forget(int session);

    /// <summary>
    /// M3 R4 (added beside the frozen members, with a default that does nothing): re-read the whole map at the next
    /// <see cref="BuildDelta"/>. The host calls it once, when the Master finished its init and joins open: the station loads
    /// in the Master's init, after <see cref="Attach"/>, and a map grid read before would miss what the init changed.
    /// </summary>
    void RereadMap() { }

    /// <summary>
    /// M10 slice H (added beside the frozen members): the replicator of a new round's world, which the host attaches to it when tg's
    /// <c>world/Reboot</c> replaces the world (D15): a fresh one with the same options. The default keeps this one (a replicator that
    /// keeps no world state).
    /// </summary>
    IReplicator ForNewWorld() => this;

    /// <summary>The counters of the last <see cref="BuildDelta"/>.</summary>
    ReplicationStats LastStats { get; }

    // ---- protocol version 2 (M2 contracts; M2 P5 implements them in Replicator, the host calls them, M2 P6) ----
    //
    // The version-2 Delta body (BuildDelta) also carries the public appearance, resource and string tables' new entries,
    // which the replicator interns from the atoms' appearances (Anomaly.Core.DmShim.IDmAppearance: the builtin vars and the
    // overlays and underlays), and the entity fields Pos, Yaw, Posture and Appearance; all under the read rules above:
    // outside the world, read-only, nothing kept across ticks but values and ids, so the state hash is unchanged.

    /// <summary>
    /// Version 2: the private block of one session for the Delta of <paramref name="tick"/>, after that tick's
    /// <see cref="BuildDelta"/>: the own motion state and parameters of <paramref name="inputs"/>, the input buffer depth, the
    /// changes of the client's <c>screen</c> list since the session's last block (each entry's appearance interned into the
    /// session's private tables, so no other session learns it), and the private table entries those need.
    /// </summary>
    PrivateFrame BuildPrivate(long tick, in PrivateInputs inputs) => throw new NotImplementedException("M2 P5");

    /// <summary>
    /// Version 2: the join messages of a session (MapGrid version 2 with the turf appearances and the solidity bits from
    /// <c>Anomaly.Core.Tg.LocomotionSeam.IsSolid</c>, and the Snapshot with every public table and the session's private block,
    /// all of its screen entries as added). It starts the session's private tables and screen shadow afresh.
    /// </summary>
    JoinMessages BuildJoin(long tick, in PrivateInputs inputs) => throw new NotImplementedException("M2 P5");

    /// <summary>
    /// Version 15 (M4c slice 4, DV40): the RadialMenu messages of a session's last private block or join (<see cref="BuildPrivate"/>,
    /// <see cref="BuildJoin(long, in PrivateInputs)"/>): its radial menus that opened, turned their page or closed, whose pictures that
    /// block's private tables carry. The host sends them after that Delta or Snapshot; a second call returns none.
    /// </summary>
    IReadOnlyList<RadialMenuMessage> TakeRadials(int session) => Array.Empty<RadialMenuMessage>();

    /// <summary>
    /// Version 16 (M5 slice 3, DV44): the UiWindow messages of a session's last <see cref="BuildPrivate"/> or join: what tg's tgui windows
    /// wrote into the player's client (client.dm_tgui_output) turned into window updates and closes, without updates that repeat the last
    /// one sent; a join sends every open window again, whole. The host sends them after that Delta or Snapshot; a second call returns none.
    /// </summary>
    IReadOnlyList<UiWindowMessage> TakeUiWindows(int session) => Array.Empty<UiWindowMessage>();

    /// <summary>M10x slice G6 (DV216): the player closed the browse() window <paramref name="window"/>; nothing is sent for it again.</summary>
    void ForgetBrowse(int session, int window) { }

    /// <summary>
    /// Version 19 (DV47): the Tooltip message of a session's last <see cref="BuildPrivate"/> or join, or null: what tg's tooltip datum
    /// wrote into the player's client (client.dm_tooltip_output) when it changes what the client shows; a join shows the shown tooltip
    /// again. The host sends it after that Delta or Snapshot; a second call returns null.
    /// </summary>
    TooltipMessage TakeTooltip(int session) => null;

    /// <summary>
    /// Version 21: the CameraShake message of a session's last <see cref="BuildPrivate"/>, or null: the tick's last animate chain on the
    /// player's client (tg's shake_camera). The host sends it after that Delta; a second call returns null.
    /// </summary>
    CameraShakeMessage TakeCameraShake(int session) => null;

    /// <summary>Version 54: the HeldMotion message of a session's last <see cref="BuildPrivate"/> (its own gun loaded, unloaded or racked), or null.</summary>
    HeldMotionMessage TakeHeldMotion(int session) => null;

    /// <summary>Version 40: the RadioChannels message of a session's last <see cref="BuildPrivate"/> (its worn headset's prefixes changed), or null.</summary>
    RadioChannelsMessage TakeRadioChannels(int session) => null;

    /// <summary>
    /// Version 42: the Motion messages of a session for the last <see cref="BuildDelta"/>, after its <see cref="BuildPrivate"/> (the observer
    /// layer's held entities decide which ghosts it names): the capsule mobs whose position or yaw changed within
    /// <see cref="Limits.MotionResendTicks"/> ticks, split to fit the unreliable channel. <paramref name="into"/> is cleared first; the messages are valid until the next call (reused).
    /// </summary>
    void BuildMotion(int session, List<MotionMessage> into) => into.Clear();
}
