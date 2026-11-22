using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;

namespace Anomaly.Server.Tests.Replication;

/// <summary>One entity as a client sees it: every field of an entity record, with the type as its DM path.</summary>
internal readonly record struct EntityView(long Id, string Type, int X, int Y, int Z, byte Dir, string Name, float Glide, long LastMoveTick)
{
    public override string ToString() => $"#{Id} {Type} ({X},{Y},{Z}) dir {Dir} '{Name}' glide {Glide} moved t{LastMoveTick}";
}

/// <summary>
/// The tests' wire helpers over the protocol's codec and P2's <see cref="ReplicaWorld"/>: decoding a message (a refused one
/// fails the test), a session's Delta message from the shared body, and a replica's entities as <see cref="EntityView"/>s.
/// </summary>
internal static class Wire
{
    public static MapGrid DecodeMapGrid(byte[] message)
    {
        Assert.True(Codec.TryDecode(message, out MapGrid grid, out ProtocolError error), error.ToString());
        return grid;
    }

    public static Snapshot DecodeSnapshot(byte[] message)
    {
        Assert.True(Codec.TryDecode(message, out Snapshot snapshot, out ProtocolError error), error.ToString());
        return snapshot;
    }

    /// <summary>One session's Delta message: its header (<c>Codec.EncodeDelta</c>) and the shared body.</summary>
    public static byte[] DeltaMessage(DeltaFrame frame, long ownEntity = 0, uint lastInputSeq = 0)
    {
        var buffer = new byte[Codec.MaxDeltaHeaderBytes + frame.Body.Length];
        int written = Codec.EncodeDelta(frame.Tick, lastInputSeq, ownEntity, frame.Body.Span, buffer);
        return buffer.AsSpan(0, written).ToArray();
    }

    public static Delta DecodeDelta(DeltaFrame frame, long ownEntity = 0, uint lastInputSeq = 0)
    {
        Assert.True(Codec.TryDecode(DeltaMessage(frame, ownEntity, lastInputSeq), out Delta delta, out ProtocolError error), error.ToString());
        return delta;
    }

    /// <summary>Applies a State channel message; a refused message fails the test (a desync throws).</summary>
    public static void Apply(ReplicaWorld replica, byte[] message)
    {
        Assert.True(replica.TryApply(message, out _, out ProtocolError error), error.ToString());
        Assert.False(replica.IsDesynced, "the replica desynced");
    }

    public static EntityView View(this ReplicaEntity e)
        => new(e.Id, e.TypePath, e.State.X, e.State.Y, e.State.Z, e.State.Dir, e.State.Name, e.State.GlideSize, e.State.LastMoveTick);

    /// <summary>Every entity of the replica, in id order.</summary>
    public static List<EntityView> Views(this ReplicaWorld replica) => replica.Entities.Select(View).OrderBy(e => e.Id).ToList();

    public static bool TryGetView(this ReplicaWorld replica, long id, out EntityView view)
    {
        if (replica.TryGet(id, out ReplicaEntity entity))
        {
            view = entity.View();
            return true;
        }
        view = default;
        return false;
    }
}

/// <summary>
/// The full-scan oracle (scope 2d measurement 3): every tick it reads every registered object of the world, outside the
/// world, and lists the entities with every field, written independently of the replicator (it reads the movable's own
/// <c>x</c>, <c>y</c>, <c>z</c> getters and walks the registry). <c>LastMoveTick</c> is its own observation too: the tick
/// in which it saw the position change or the entity appear.
/// </summary>
internal sealed class WorldOracle
{
    private readonly Dictionary<long, (int X, int Y, int Z, long Moved)> _last = new();

    public SortedDictionary<long, EntityView> Scan(World world)
    {
        Assert.Null(World.CurrentOrNull);
        long tick = world.Tick;
        var result = new SortedDictionary<long, EntityView>();
        world.Objects.ForEachLiveInIdOrder((id, obj) =>
        {
            // What a living viewer's client holds (BYOND: invisibility up to the viewer's see_invisible, SEE_INVISIBLE_LIVING; the
            // observer layer above it reaches only the sessions that see it, protocol 22, D151).
            if (obj is not atom_movable m || m.loc is not turf || m.invisibility > Defines.SEE_INVISIBLE_LIVING) return;
            int x = (int)m.x, y = (int)m.y, z = (int)m.z;
            long moved = tick;
            if (_last.TryGetValue(id, out var before) && before.X == x && before.Y == y && before.Z == z) moved = before.Moved;
            // The wire forms: dir is one of the eight directions or 0, glide within 0..256 (the codec's ranges).
            // The wire forms: dir is one of the eight directions or 0, glide within 0..Limits.MaxGlideSize (the codec's ranges).
            float glide = m.glide_size;
            float dir = m.dir;
            byte wireDir = dir is 1 or 2 or 4 or 5 or 6 or 8 or 9 or 10 ? (byte)dir : (byte)0;
            result.Add(id, new EntityView(id, PathOf(m.GetType()), x, y, z, wireDir, m.name, glide > 0 ? Math.Min(glide, Limits.MaxGlideSize) : 0f, moved));
        });
        _last.Clear();
        foreach (EntityView e in result.Values) _last[e.Id] = (e.X, e.Y, e.Z, e.LastMoveTick);
        return result;
    }

    private static string PathOf(Type type)
    {
        for (Type level = type; level != null; level = level.BaseType)
        {
            string path = DmTypes.PathOf(level);
            if (path != null) return path;
        }
        return null;
    }
}
