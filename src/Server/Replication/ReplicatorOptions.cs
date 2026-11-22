using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Owner: M1 P4 (replication), M2 P5 (the locomotion view and the version-2 settings).

/// <summary>
/// How the replicator turns its messages into bytes: the protocol's codec in the server (<see cref="CodecReplicationEncoder"/>);
/// the measurement harness wraps it to time the encoding, and a test may plug in its own to look at the messages.
/// </summary>
public interface IReplicationEncoder
{
    /// <summary>The whole <see cref="MapGrid"/> message.</summary>
    byte[] EncodeMapGrid(MapGrid grid);

    /// <summary>The whole <see cref="Snapshot"/> message.</summary>
    byte[] EncodeSnapshot(Snapshot snapshot);

    /// <summary>
    /// The shared body of a <see cref="Delta"/> (from <see cref="Delta.FirstNewType"/> on) into <paramref name="buffer"/>, which
    /// the encoder may replace with a larger one; returns the bytes written (<c>Codec.EncodeDeltaBody(delta, ref buffer)</c>,
    /// the pooled broadcast form: the replicator reuses one buffer for every tick).
    /// </summary>
    int EncodeDeltaBody(Delta delta, ref byte[] buffer);
}

/// <summary>The protocol's codec (<c>Anomaly.Protocol.Codec</c>), the server's encoder.</summary>
public sealed class CodecReplicationEncoder : IReplicationEncoder
{
    /// <summary>The one instance (stateless).</summary>
    public static CodecReplicationEncoder Instance { get; } = new();

    private CodecReplicationEncoder()
    {
    }

    public byte[] EncodeMapGrid(MapGrid grid) => Codec.Encode(grid);

    public byte[] EncodeSnapshot(Snapshot snapshot) => Codec.Encode(snapshot);

    public int EncodeDeltaBody(Delta delta, ref byte[] buffer) => Codec.EncodeDeltaBody(delta, ref buffer);
}

/// <summary>
/// What the replicator reads from the locomotion bridge (M2 P4): which mobs are capsules and where their capsules are, and the
/// solidity predicate of MapGrid version 2 and the cell changes. An interface so that the replicator is tested on its own;
/// the server uses <see cref="LocomotionSeamView"/>. Read-only, outside the world, like the replicator.
/// </summary>
public interface ILocomotionView
{
    /// <summary>The capsule state of the mob <paramref name="mobId"/> after the last step; false when it is no capsule mob.</summary>
    bool TryGetCapsule(World world, long mobId, out MotionState state);

    /// <summary>Whether the tile blocks capsules (<c>LocomotionSeam.IsSolid</c>: the one predicate the controller and the client's prediction use).</summary>
    bool IsSolid(World world, int x, int y, int z);

    /// <summary>The blocked faces of the tile (<c>LocomotionSeam.FacesOf</c>, M3: the one predicate the controller and the client's prediction use); a default member, so a view that only has solidity keeps the seam's.</summary>
    TileEdges FacesOf(World world, int x, int y, int z) => LocomotionSeam.FacesOf(world, x, y, z);

    /// <summary>Version 37 (D239): whether the tile is solid only through <c>climb_walkable</c> structures (<c>LocomotionSeam.IsClimbWalkable</c>, MapGrid's <c>CellFlags.ClimbWalkable</c>); a default member, as <see cref="FacesOf"/>.</summary>
    bool IsClimbWalkable(World world, int x, int y, int z) => LocomotionSeam.IsClimbWalkable(world, x, y, z);

    /// <summary>Version 73 (DV360): whether tg's plastic flaps that refuse a standing mob stand on the tile (<c>LocomotionSeam.IsFlaps</c>, MapGrid's <c>CellFlags.Flaps</c>); a default member, as <see cref="FacesOf"/>.</summary>
    bool IsFlaps(World world, int x, int y, int z) => LocomotionSeam.IsFlaps(world, x, y, z);
}

/// <summary>
/// <see cref="LocomotionSeam"/> as the replicator's view: the seam's capsule states and its solidity predicate (a world
/// whose seam was never installed, the M1 rooms and tests, has no capsules; <c>IsSolid</c> reads the map either way).
/// </summary>
public sealed class LocomotionSeamView : ILocomotionView
{
    /// <inheritdoc/>
    public bool TryGetCapsule(World world, long mobId, out MotionState state) => LocomotionSeam.TryGetState(world, mobId, out state, out _);

    /// <inheritdoc/>
    public bool IsSolid(World world, int x, int y, int z) => LocomotionSeam.IsSolid(world, x, y, z);
}

/// <summary>A view with no capsule mobs and <see cref="DenseSolidity"/> (a server or a test without the locomotion bridge).</summary>
public sealed class NoLocomotionView : ILocomotionView
{
    /// <summary>The one instance (stateless).</summary>
    public static NoLocomotionView Instance { get; } = new();

    private NoLocomotionView()
    {
    }

    /// <inheritdoc/>
    public bool TryGetCapsule(World world, long mobId, out MotionState state)
    {
        state = default;
        return false;
    }

    /// <inheritdoc/>
    public bool IsSolid(World world, int x, int y, int z) => DenseSolidity.IsSolid(world, x, y, z);
}

/// <summary>
/// Scope 2a's solidity predicate in its plain form: outside the map or a dense turf, or a dense movable that is not a mob
/// standing on the tile. It is the predicate <c>LocomotionSeam.IsSolid</c> implements (which also lets an ON_BORDER_1 movable
/// through); this copy serves <see cref="NoLocomotionView"/> and the tests. Reads raw, outside the world.
/// </summary>
public static class DenseSolidity
{
    /// <summary>Whether the tile (x, y, z) blocks capsules.</summary>
    public static bool IsSolid(World world, int x, int y, int z)
    {
        if (x < 1 || y < 1 || z < 1 || x > MapView.Width(world) || y > MapView.Height(world) || z > MapView.Levels(world)) return true;
        turf cell = MapView.TurfAt(world, x, y, z);
        if (cell == null || cell.density > 0) return true;
        DmList contents = cell.contents;
        for (int i = 1; i <= contents.Count; i++)
            if (contents[(float)i] is atom_movable m && m is not mob && m.density > 0 && !Dm.truthy(Dm.bitand(m.flags_1, Defines.ON_BORDER_1))) return true; // an ON_BORDER_1 one blocks a face, not the tile (M3)
        return false;
    }
}

/// <summary>The replicator's settings (docs/ai/specs/replication.md section 4).</summary>
public sealed class ReplicatorOptions
{
    /// <summary>
    /// A map editor's world (docs/ai/specs/map-editor.md 3, DV504): the spawners and the mapping helpers that stand on the map are entities, shown by the
    /// pictures a mapper places them by. In a game they are never seen (<see cref="ReplicatedFields.IsEntity"/>).
    /// </summary>
    public bool MapMarkers { get; init; }

    /// <summary>The encoder; the protocol codec by default.</summary>
    public IReplicationEncoder Encoder { get; init; } = CodecReplicationEncoder.Instance;

    /// <summary>
    /// The sweep visits about 1/<see cref="SweepDivisor"/> of the entity slots per tick (scope 2d: 64), so every entity is
    /// compared with its shadow at least once every <see cref="SweepDivisor"/> ticks whatever the hints said. 1 compares
    /// every entity every tick (a full scan of the entities, for tests).
    /// </summary>
    public int SweepDivisor { get; init; } = 64;

    /// <summary>
    /// The least slots the sweep visits per tick, so that a small world (M1: a few hundred entities) is swept in a few
    /// ticks instead of 64. 0 for exactly 1/<see cref="SweepDivisor"/>.
    /// </summary>
    public int MinSweepPerTick { get; init; } = 32;

    /// <summary>The locomotion bridge as the replicator reads it (capsule states, solidity); <see cref="LocomotionSeamView"/> by default.</summary>
    public ILocomotionView Locomotion { get; init; } = new LocomotionSeamView();

    /// <summary>
    /// The cell sweep visits this many map cells per tick, round robin, and re-reads their solidity and turf appearance, so
    /// a change no hint named (a turf's <c>density</c>, an in-place overlay edit) reaches the clients within a few dozen
    /// ticks (the same safety net as the entity sweep). 0 turns it off.
    /// </summary>
    public int CellSweepPerTick { get; init; } = 16;

    /// <summary>
    /// A session's screen objects are re-read for a changed appearance at least every this many ticks even when no hint
    /// named them (the screen objects' in-place overlay edits), spread over the ticks. 0 turns it off (hints only).
    /// </summary>
    public int ScreenSweepPeriodTicks { get; init; } = 20;

    /// <summary>M10 slice C: interest management (Replicator.Interest.cs); null sends every entity to every session in the shared body.</summary>
    public InterestOptions Interest { get; init; }

    internal void Validate()
    {
        if (Encoder == null) throw new ArgumentException("an encoder is required", nameof(Encoder));
        if (SweepDivisor < 1) throw new ArgumentOutOfRangeException(nameof(SweepDivisor), SweepDivisor, "at least 1");
        if (MinSweepPerTick < 0) throw new ArgumentOutOfRangeException(nameof(MinSweepPerTick), MinSweepPerTick, "not negative");
        if (Locomotion == null) throw new ArgumentException("a locomotion view is required", nameof(Locomotion));
        if (CellSweepPerTick < 0) throw new ArgumentOutOfRangeException(nameof(CellSweepPerTick), CellSweepPerTick, "not negative");
        if (ScreenSweepPeriodTicks < 0) throw new ArgumentOutOfRangeException(nameof(ScreenSweepPeriodTicks), ScreenSweepPeriodTicks, "not negative");
        Interest?.Validate();
    }
}
