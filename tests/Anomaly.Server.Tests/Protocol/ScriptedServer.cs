using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// A scripted stand-in for the server's entity table: seeded random spawns, moves, renames, despawns and respawns of the
/// same id, new types, an own-entity switch, and the Snapshot and Delta messages a replicator would build from it
/// (the diff P4 does against the world). The tests compare a <see cref="ReplicaWorld"/> fed with these messages against
/// <see cref="Table"/>.
/// </summary>
internal sealed class ScriptedServer
{
    public const int Width = 40;
    public const int Height = 40;
    public const int Levels = 2;

    private readonly Random _rng;
    private readonly List<EntityState> _limbo = new();
    private long _nextId = 1;

    public ScriptedServer(int seed)
    {
        _rng = new Random(seed);
        Types.Add("/mob");
        Types.Add("/obj/item");
    }

    /// <summary>The server's entity table, by id.</summary>
    public SortedDictionary<long, EntityState> Table { get; } = new();

    /// <summary>The server-global type table (append-only).</summary>
    public List<string> Types { get; } = new();

    public long Tick { get; private set; }

    public long OwnEntity { get; private set; }

    public uint LastInputSeq { get; private set; }

    /// <summary>How many types the previous Delta had already announced.</summary>
    private int _announced;

    public MapGrid Map() => Msg.FlatMap(Width, Height, Levels);

    /// <summary>A Snapshot of the table now (the type table whole).</summary>
    public Snapshot Snapshot()
    {
        var s = new Snapshot { Tick = Tick, OwnEntity = OwnEntity, LastInputSeq = LastInputSeq };
        s.Types.AddRange(Types);
        s.Entities.AddRange(Table.Values);
        return s;
    }

    public void Populate(int count)
    {
        for (int i = 0; i < count; i++)
            Table.Add(_nextId, NewState(_nextId++));
        OwnEntity = Table.Keys.First();
        _announced = Types.Count;
    }

    private EntityState NewState(long id) => Msg.State(id, _rng.Next(Types.Count), _rng.Next(1, Width + 1), _rng.Next(1, Height + 1), _rng.Next(1, Levels + 1),
        new byte[] { 1, 2, 4, 8, 5, 6, 9, 10 }[_rng.Next(8)], _rng.Next(5) == 0 ? null : "thing " + id, _rng.Next(3) == 0 ? 0f : 32f / _rng.Next(1, 8), Tick);

    /// <summary>Advances one tick of random changes and returns the Delta from the previous state to this one.</summary>
    public Delta Step()
    {
        var before = new Dictionary<long, EntityState>(Table);
        var newTypes = new List<string>();
        Tick++;
        int ops = _rng.Next(0, 14);
        for (int i = 0; i < ops; i++)
        {
            long[] ids = Table.Keys.ToArray();
            switch (_rng.Next(12))
            {
                case 0 or 1: // spawn a new object
                    if (_rng.Next(40) == 0)
                    {
                        Types.Add("/obj/new_type_" + Types.Count);
                        newTypes.Add(Types[^1]);
                    }

                    Table.Add(_nextId, NewState(_nextId++) with { Type = _rng.Next(Types.Count) });
                    break;
                case 2: // respawn a despawned id (a container's content put back on a turf)
                    if (_limbo.Count > 0)
                    {
                        EntityState back = _limbo[^1];
                        _limbo.RemoveAt(_limbo.Count - 1);
                        Table[back.Id] = back with { X = _rng.Next(1, Width + 1), Y = _rng.Next(1, Height + 1), LastMoveTick = Tick };
                    }

                    break;
                case 3: // despawn
                    if (ids.Length > 5)
                    {
                        long id = ids[_rng.Next(ids.Length)];
                        if (id != OwnEntity)
                        {
                            _limbo.Add(Table[id]);
                            Table.Remove(id);
                        }
                    }

                    break;
                case 4: // rename
                    if (ids.Length > 0)
                    {
                        long id = ids[_rng.Next(ids.Length)];
                        Table[id] = Table[id] with { Name = _rng.Next(4) == 0 ? null : "renamed " + Tick };
                    }

                    break;
                case 5: // teleport (also z)
                    if (ids.Length > 0)
                    {
                        long id = ids[_rng.Next(ids.Length)];
                        Table[id] = Table[id] with { X = _rng.Next(1, Width + 1), Y = _rng.Next(1, Height + 1), Z = _rng.Next(1, Levels + 1), LastMoveTick = Tick };
                    }

                    break;
                case 6: // the own mob changes (a mob transfer)
                    if (ids.Length > 0)
                        OwnEntity = _rng.Next(5) == 0 ? 0 : ids[_rng.Next(ids.Length)];
                    break;
                case 7: // a glide size change alone
                    if (ids.Length > 0)
                    {
                        long id = ids[_rng.Next(ids.Length)];
                        Table[id] = Table[id] with { GlideSize = 32f / _rng.Next(1, 33) };
                    }

                    break;
                default: // a step
                    if (ids.Length > 0)
                    {
                        long id = ids[_rng.Next(ids.Length)];
                        EntityState e = Table[id];
                        int dx = _rng.Next(-1, 2), dy = _rng.Next(-1, 2);
                        int x = Math.Clamp(e.X + dx, 1, Width), y = Math.Clamp(e.Y + dy, 1, Height);
                        Table[id] = e with { X = x, Y = y, Dir = DirOf(dx, dy, e.Dir), LastMoveTick = (x != e.X || y != e.Y) ? Tick : e.LastMoveTick };
                    }

                    break;
            }
        }

        // A non-capsule entity's position is its tile's centre (what the codec and the replica derive).
        foreach (long key in Table.Keys.ToArray())
            Table[key] = Table[key] with { PosX = Table[key].X * 256, PosY = Table[key].Y * 256 };
        if (_rng.Next(3) == 0)
            LastInputSeq += (uint)_rng.Next(1, 4);

        var delta = new Delta { Tick = Tick, OwnEntity = OwnEntity, LastInputSeq = LastInputSeq, FirstNewType = _announced };
        delta.NewTypes.AddRange(Types.Skip(_announced));
        _announced = Types.Count;
        foreach (EntityState state in Table.Values)
        {
            if (!before.TryGetValue(state.Id, out EntityState old))
                delta.Spawned.Add(state);
            else if (!old.Equals(state))
                delta.Changed.Add(Diff(old, state));
        }

        foreach (long id in before.Keys.OrderBy(i => i))
            if (!Table.ContainsKey(id))
                delta.Despawned.Add(id);
        return delta;
    }

    private static byte DirOf(int dx, int dy, byte current)
    {
        int dir = (dy > 0 ? 1 : dy < 0 ? 2 : 0) | (dx > 0 ? 4 : dx < 0 ? 8 : 0);
        return dir == 0 ? current : (byte)dir;
    }

    /// <summary>The change from <paramref name="a"/> to <paramref name="b"/> (the same id, at least one field differs).</summary>
    public static EntityChange Diff(EntityState a, EntityState b)
    {
        var c = new EntityChange { Id = b.Id };
        if (a.X != b.X) { c.Mask |= NetField.X; c.X = b.X; }
        if (a.Y != b.Y) { c.Mask |= NetField.Y; c.Y = b.Y; }
        if (a.Z != b.Z) { c.Mask |= NetField.Z; c.Z = b.Z; }
        if (a.Dir != b.Dir) { c.Mask |= NetField.Dir; c.Dir = b.Dir; }
        if (a.Name != b.Name) { c.Mask |= NetField.Name; c.Name = b.Name; }
        if (a.GlideSize != b.GlideSize) { c.Mask |= NetField.GlideSize; c.GlideSize = b.GlideSize; }
        if (a.LastMoveTick != b.LastMoveTick) { c.Mask |= NetField.LastMoveTick; c.LastMoveTick = b.LastMoveTick; }
        return c;
    }

    /// <summary>Encodes a Delta the way the server does: the body once, then a header per connection.</summary>
    public static byte[] EncodeForConnection(Delta delta, byte[] body)
    {
        var buffer = new byte[Codec.MaxDeltaHeaderBytes + body.Length];
        int length = Codec.EncodeDelta(delta.Tick, delta.LastInputSeq, delta.OwnEntity, body, buffer);
        return buffer.AsSpan(0, length).ToArray();
    }
}
