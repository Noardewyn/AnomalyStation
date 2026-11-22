using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Replication.Bench;

/// <summary>
/// A measurement of scope 2d's plan (docs/ai/specs/replication.md section 6): skipped unless <c>ANOMALY_BENCH=1</c>; run in
/// Release with <c>ANOMALY_BENCH=1 dotnet test tests/Anomaly.Server.Tests -c Release --filter Category=Bench</c>.
/// <c>ANOMALY_BENCH_OUT</c>, if set, names a folder for the JSON results.
/// </summary>
internal sealed class BenchFactAttribute : FactAttribute
{
    public BenchFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ANOMALY_BENCH") != "1")
            Skip = "a measurement: ANOMALY_BENCH=1 dotnet test tests/Anomaly.Server.Tests -c Release --filter Category=Bench";
    }
}

/// <summary>Distribution of a sample: mean, median, p99, max.</summary>
internal readonly record struct Dist(double Mean, double P50, double P99, double Max)
{
    public static Dist Of(IReadOnlyCollection<double> values)
    {
        if (values.Count == 0) return default;
        double[] sorted = values.OrderBy(v => v).ToArray();
        double P(double p) => sorted[Math.Min(sorted.Length - 1, (int)(p * sorted.Length))];
        return new Dist(Math.Round(values.Average(), 4), Math.Round(P(0.5), 4), Math.Round(P(0.99), 4), Math.Round(sorted[^1], 4));
    }

    public override string ToString() => $"mean {Mean} p50 {P50} p99 {P99} max {Max}";
}

/// <summary>Collects named results, prints them and writes them as JSON.</summary>
internal sealed class BenchReport
{
    private readonly ITestOutputHelper _output;
    private readonly string _name;
    private readonly SortedDictionary<string, object> _values = new(StringComparer.Ordinal);

    public BenchReport(ITestOutputHelper output, string name)
    {
        _output = output;
        _name = name;
        Add("build", Optimized() ? "Release" : "Debug (numbers are not representative)");
        Add("runtime", Environment.Version.ToString());
        Add("processors", Environment.ProcessorCount);
    }

    public void Add(string key, object value)
    {
        _values[key] = value;
        _output.WriteLine($"{key}: {(value is string ? value : JsonSerializer.Serialize(value))}");
    }

    public void Save()
    {
        string folder = Environment.GetEnvironmentVariable("ANOMALY_BENCH_OUT");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, _name + ".json"), JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool Optimized()
    {
        var debuggable = typeof(Replicator).Assembly.GetCustomAttribute<DebuggableAttribute>();
        return debuggable == null || !debuggable.IsJITOptimizerDisabled;
    }
}

/// <summary>The protocol's codec with the time spent encoding Delta bodies counted (to tell the diff from the encoding).</summary>
internal sealed class TimedEncoder : IReplicationEncoder
{
    private readonly IReplicationEncoder _inner = CodecReplicationEncoder.Instance;

    public long DeltaEncodeTicks { get; set; }

    public byte[] EncodeMapGrid(MapGrid grid) => _inner.EncodeMapGrid(grid);

    public byte[] EncodeSnapshot(Snapshot snapshot) => _inner.EncodeSnapshot(snapshot);

    public int EncodeDeltaBody(Delta delta, ref byte[] buffer)
    {
        long start = Stopwatch.GetTimestamp();
        int length = _inner.EncodeDeltaBody(delta, ref buffer);
        DeltaEncodeTicks += Stopwatch.GetTimestamp() - start;
        return length;
    }
}

/// <summary>
/// R2 over the whitelist (scope 2d option R2): every tick, every live registered atom's whitelisted values (x, y, z,
/// dir, name, and glide_size for movables) compared with a shadow, found by a walk of the registry.
/// </summary>
internal sealed class WhitelistFullScan
{
    private struct Slot
    {
        public int X, Y, Z;
        public float Dir, Glide;
        public string Name;
    }

    private readonly Dictionary<long, int> _index = new();
    private Slot[] _slots = new Slot[1024];
    private int _count;

    public long Objects { get; private set; }

    public long Slots { get; private set; }

    public long Changed { get; private set; }

    public void Diff(World world)
    {
        long objects = 0, slots = 0, changed = 0;
        world.Objects.ForEachLiveInIdOrder((id, obj) =>
        {
            if (obj is not atom a) return;
            objects++;
            bool movable = a is atom_movable;
            slots += movable ? 6 : 5;
            if (!_index.TryGetValue(id, out int i))
            {
                if (_count == _slots.Length) Array.Resize(ref _slots, _count * 2);
                i = _count++;
                _index.Add(id, i);
            }
            ref Slot s = ref _slots[i];
            int x = (int)a.x, y = (int)a.y, z = (int)a.z;
            float dir = a.dir;
            string name = a.name;
            float glide = movable ? ((atom_movable)a).glide_size : 0;
            if (x != s.X) { s.X = x; changed++; }
            if (y != s.Y) { s.Y = y; changed++; }
            if (z != s.Z) { s.Z = z; changed++; }
            if (dir != s.Dir) { s.Dir = dir; changed++; }
            if (!ReferenceEquals(name, s.Name)) { s.Name = name; changed++; }
            if (glide != s.Glide) { s.Glide = glide; changed++; }
        });
        Objects = objects;
        Slots = slots;
        Changed = changed;
    }
}

/// <summary>
/// R2 over every field (the scout's probe, outside the world): every tick, every hashed instance field (object-model.md
/// section 8: declared by a <c>[DmPath]</c> class, not <c>[NotHashed]</c>) of every live registered object compared with a
/// shadow, through a per-type differ compiled from expression trees (a stand-in for generated code).
/// </summary>
internal sealed class AllFieldsFullScan
{
    private sealed class Entry
    {
        public double[] Numbers;
        public object[] References;
    }

    private sealed class Differ
    {
        public int Numbers;
        public int References;
        public Func<object, double[], object[], int> Run;
    }

    private readonly Dictionary<Type, Differ> _differs = new();
    private readonly Dictionary<long, Entry> _entries = new();

    public long Objects { get; private set; }

    public long Slots { get; private set; }

    public long Changed { get; private set; }

    public int TypesCompiled => _differs.Count;

    public void Diff(World world)
    {
        long objects = 0, slots = 0, changed = 0;
        world.Objects.ForEachLiveInIdOrder((id, obj) =>
        {
            Type type = obj.GetType();
            if (!_differs.TryGetValue(type, out Differ differ)) _differs[type] = differ = Build(type);
            if (!_entries.TryGetValue(id, out Entry entry))
                _entries[id] = entry = new Entry { Numbers = new double[differ.Numbers], References = new object[differ.References] };
            objects++;
            slots += differ.Numbers + differ.References;
            changed += differ.Run(obj, entry.Numbers, entry.References);
        });
        Objects = objects;
        Slots = slots;
        Changed = changed;
    }

    /// <summary>The fields the state hasher hashes, up the type's chain.</summary>
    public static List<FieldInfo> HashedFields(Type type)
    {
        var fields = new List<FieldInfo>();
        for (Type level = type; level != null && level != typeof(object); level = level.BaseType)
        {
            if (level.GetCustomAttribute<DmPathAttribute>() == null) continue;
            foreach (FieldInfo field in level.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (field.GetCustomAttribute<NotHashedAttribute>() == null) fields.Add(field);
        }
        return fields;
    }

    private static Differ Build(Type type)
    {
        ParameterExpression o = Expression.Parameter(typeof(object), "o");
        ParameterExpression numbers = Expression.Parameter(typeof(double[]), "numbers");
        ParameterExpression references = Expression.Parameter(typeof(object[]), "references");
        ParameterExpression typed = Expression.Variable(type, "t");
        ParameterExpression changed = Expression.Variable(typeof(int), "changed");
        var body = new List<Expression> { Expression.Assign(typed, Expression.Convert(o, type)), Expression.Assign(changed, Expression.Constant(0)) };
        int n = 0, r = 0;
        foreach (FieldInfo field in HashedFields(type))
        {
            Type ft = field.FieldType;
            Expression read = Expression.Field(typed, field);
            if (ft == typeof(float) || ft == typeof(int) || ft == typeof(long) || ft == typeof(double) || ft == typeof(byte) || ft == typeof(bool) || ft == typeof(float?))
            {
                Expression value = ft == typeof(bool) ? Expression.Condition(read, Expression.Constant(1.0), Expression.Constant(0.0))
                    : ft == typeof(float?) ? Expression.Coalesce(Expression.Convert(read, typeof(double?)), Expression.Constant(double.NaN))
                    : Expression.Convert(read, typeof(double));
                ParameterExpression v = Expression.Variable(typeof(double));
                IndexExpression slot = Expression.ArrayAccess(numbers, Expression.Constant(n++));
                body.Add(Expression.Block(new[] { v }, Expression.Assign(v, value),
                    Expression.IfThen(Expression.NotEqual(v, slot), Expression.Block(Expression.Assign(slot, v), Expression.PreIncrementAssign(changed)))));
            }
            else if (!ft.IsValueType)
            {
                ParameterExpression v = Expression.Variable(typeof(object));
                IndexExpression slot = Expression.ArrayAccess(references, Expression.Constant(r++));
                body.Add(Expression.Block(new[] { v }, Expression.Assign(v, Expression.Convert(read, typeof(object))),
                    Expression.IfThen(Expression.Not(Expression.ReferenceEqual(v, slot)), Expression.Block(Expression.Assign(slot, v), Expression.PreIncrementAssign(changed)))));
            }
        }
        body.Add(changed);
        var lambda = Expression.Lambda<Func<object, double[], object[], int>>(Expression.Block(new[] { typed, changed }, body), o, numbers, references);
        return new Differ { Numbers = n, References = r, Run = lambda.Compile() };
    }
}
