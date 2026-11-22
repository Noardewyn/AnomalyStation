using System.Diagnostics;
using System.Runtime.CompilerServices;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Replication.Bench;

/// <summary>
/// The measurements of scope 2d's plan that decide R1 or R3 (D99), reported in docs/ai/specs/replication.md section 6:
/// (1) write cost per var write; (2) diff cost per object and per slot, whitelist and all fields, at about 1k, 4k and 16k
/// atoms, and the candidate set at 52 and 150 movers; (3) hint coverage against a full-scan oracle over 10k ticks;
/// (4) delta and join sizes and times; (5) shadow memory per entity. The macro check of (1) (the Move chain's vars as
/// generated properties) needs a throwaway copy of the core and runs outside the repository.
/// </summary>
[Trait("Category", "Bench")]
public class ReplicationBench
{
    private readonly ITestOutputHelper _output;

    public ReplicationBench(ITestOutputHelper output)
    {
        _output = output;
    }

    // ---- (1) write cost ----

    [BenchFact]
    public void WriteCostPerVarWrite()
    {
        var report = new BenchReport(_output, "write-cost");
        const int N = 4096, Rounds = 20000;
        var plain = new PlainVars[N];
        var dirty = new DirtyBitVars[N];
        var stamped = new StampedVars[N];
        var context = new StampContext();
        object[] refs = { new object(), new object() };
        for (int i = 0; i < N; i++)
        {
            plain[i] = new PlainVars();
            dirty[i] = new DirtyBitVars();
            stamped[i] = new StampedVars { Context = context };
        }
        var results = new List<(double Plain, double Dirty, double Stamped)>();
        for (int rep = 0; rep < 5; rep++)
        {
            var sw = Stopwatch.StartNew();
            for (int k = 0; k < Rounds; k++)
            {
                float v = k;
                object o = refs[k & 1];
                for (int i = 0; i < N; i++)
                {
                    PlainVars p = plain[i];
                    p.a = v; p.b = v + 1; p.c = 3; p.d = v; p.r = o;
                }
            }
            double tPlain = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            for (int k = 0; k < Rounds; k++)
            {
                float v = k;
                object o = refs[k & 1];
                for (int i = 0; i < N; i++)
                {
                    DirtyBitVars p = dirty[i];
                    p.a = v; p.b = v + 1; p.c = 3; p.d = v; p.r = o;
                }
                if ((k & 63) == 0)
                    for (int i = 0; i < N; i++) dirty[i].DirtyMask = 0;
            }
            double tDirty = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            for (int k = 0; k < Rounds; k++)
            {
                context.Tick = k;
                float v = k;
                object o = refs[k & 1];
                for (int i = 0; i < N; i++)
                {
                    StampedVars p = stamped[i];
                    p.a = v; p.b = v + 1; p.c = 3; p.d = v; p.r = o;
                }
                context.Dirty.Clear();
            }
            double tStamped = sw.Elapsed.TotalMilliseconds;
            double writes = (double)N * Rounds * 5;
            results.Add((tPlain * 1e6 / writes, tDirty * 1e6 / writes, tStamped * 1e6 / writes));
        }
        // The first repetition warms the JIT up; the median of the others.
        var kept = results.Skip(1).ToList();
        report.Add("ns_per_write_plain_field", Math.Round(kept.Select(r => r.Plain).OrderBy(x => x).ElementAt(kept.Count / 2), 3));
        report.Add("ns_per_write_property_dirty_bit", Math.Round(kept.Select(r => r.Dirty).OrderBy(x => x).ElementAt(kept.Count / 2), 3));
        report.Add("ns_per_write_property_stamp_dirty_list", Math.Round(kept.Select(r => r.Stamped).OrderBy(x => x).ElementAt(kept.Count / 2), 3));
        report.Add("note", "4096 objects x 20000 rounds x 5 writes (4 floats, 1 reference; 4 of the 5 change every round); median of 4 repetitions after a warm-up");
        report.Save();
    }

    // ---- (2), (4), (5): costs at three sizes ----

    [BenchFact]
    public void CostsAtThreeSizes()
    {
        var report = new BenchReport(_output, "costs");
        ReplicationWorld big = null;
        foreach (int size in new[] { 32, 64, 128 })
        {
            string at = "room" + size + ".";
            var watch = Stopwatch.StartNew();
            var world = ReplicationWorld.Boot(size: size, movers: 150, seed: 5, mix: ScenarioMix.MoversOnly, containers: 0);
            world.ScenarioOn = false;
            report.Add(at + "boot_s", Math.Round(watch.Elapsed.TotalSeconds, 2));
            CountObjects(world, out int atoms, out int objects, out int movables);
            report.Add(at + "atoms", atoms);
            report.Add(at + "registered_objects", objects);
            report.Add(at + "movables", movables);

            // (5) shadow memory, attach time
            var encoder = new TimedEncoder();
            long before = GC.GetTotalMemory(true);
            watch.Restart();
            var replicator = new Replicator(new ReplicatorOptions { Encoder = encoder });
            replicator.Attach(world.World);
            double attachMs = watch.Elapsed.TotalMilliseconds;
            long after = GC.GetTotalMemory(true);
            report.Add(at + "attach_ms", Math.Round(attachMs, 2));
            report.Add(at + "entities", replicator.EntityCount);
            report.Add(at + "shadow_bytes_per_entity", Math.Round((after - before) / (double)replicator.EntityCount, 1));

            // (4) join: MapGrid and Snapshot
            Join(report, at, replicator, encoder, world);

            // (2), (4) per tick at 52 and 150 movers
            foreach (int movers in new[] { 52, 150 })
                PerTick(report, at + "movers" + movers + ".", world, replicator, encoder, movers, warm: 200, ticks: size == 128 ? 600 : 1000);

            // (2) R2 for comparison: the whitelist of every atom, and every field of every object, each tick
            world.ActiveMovers = 150;
            var whitelist = new WhitelistFullScan();
            var allFields = new AllFieldsFullScan();
            var wl = new List<double>();
            var all = new List<double>();
            watch.Restart();
            allFields.Diff(world.World);
            report.Add(at + "r2_all_fields_first_build_ms", Math.Round(watch.Elapsed.TotalMilliseconds, 1));
            whitelist.Diff(world.World);
            for (int i = 0; i < (size == 128 ? 60 : 200); i++)
            {
                world.Step();
                replicator.BuildDelta(world.World.Tick);
                watch.Restart();
                whitelist.Diff(world.World);
                wl.Add(watch.Elapsed.TotalMilliseconds);
                watch.Restart();
                allFields.Diff(world.World);
                all.Add(watch.Elapsed.TotalMilliseconds);
            }
            Dist wlDist = Dist.Of(wl), allDist = Dist.Of(all);
            report.Add(at + "r2_whitelist_ms_per_tick", wlDist);
            report.Add(at + "r2_whitelist_objects", whitelist.Objects);
            report.Add(at + "r2_whitelist_slots", whitelist.Slots);
            report.Add(at + "r2_whitelist_ns_per_object", Math.Round(wlDist.Mean * 1e6 / whitelist.Objects, 1));
            report.Add(at + "r2_whitelist_ns_per_slot", Math.Round(wlDist.Mean * 1e6 / whitelist.Slots, 2));
            report.Add(at + "r2_all_fields_ms_per_tick", allDist);
            report.Add(at + "r2_all_fields_objects", allFields.Objects);
            report.Add(at + "r2_all_fields_slots", allFields.Slots);
            report.Add(at + "r2_all_fields_ns_per_object", Math.Round(allDist.Mean * 1e6 / allFields.Objects, 1));
            report.Add(at + "r2_all_fields_ns_per_slot", Math.Round(allDist.Mean * 1e6 / allFields.Slots, 2));
            report.Add(at + "r2_all_fields_changed_slots_per_tick", allFields.Changed);
            report.Add(at + "runtimes", world.Runtimes.Count);
            if (size == 128) big = world;
        }

        // Dense: the 128 room with 16k objs on its floor (16k entities): a station's worth of movables.
        big.AddClutter(16000);
        var denseEncoder = new TimedEncoder();
        long denseBefore = GC.GetTotalMemory(true);
        var dense = new Replicator(new ReplicatorOptions { Encoder = denseEncoder });
        var w2 = Stopwatch.StartNew();
        dense.Attach(big.World); // replaces the first replicator's hint sink
        report.Add("dense.attach_ms", Math.Round(w2.Elapsed.TotalMilliseconds, 1));
        long denseAfter = GC.GetTotalMemory(true);
        CountObjects(big, out int denseAtoms, out int denseObjects, out _);
        report.Add("dense.atoms", denseAtoms);
        report.Add("dense.registered_objects", denseObjects);
        report.Add("dense.entities", dense.EntityCount);
        report.Add("dense.shadow_bytes_per_entity", Math.Round((denseAfter - denseBefore) / (double)dense.EntityCount, 1));
        report.Add("dense.sweep_period_ticks", dense.SweepPeriodTicks);
        Join(report, "dense.", dense, denseEncoder, big);
        PerTick(report, "dense.movers150.", big, dense, denseEncoder, 150, warm: 200, ticks: 600);
        var denseWhitelist = new WhitelistFullScan();
        var dwl = new List<double>();
        denseWhitelist.Diff(big.World);
        for (int i = 0; i < 60; i++)
        {
            big.Step();
            dense.BuildDelta(big.World.Tick);
            var watch = Stopwatch.StartNew();
            denseWhitelist.Diff(big.World);
            dwl.Add(watch.Elapsed.TotalMilliseconds);
        }
        report.Add("dense.r2_whitelist_ms_per_tick", Dist.Of(dwl));
        report.Add("dense.runtimes", big.Runtimes.Count);
        report.Save();
    }

    // ---- (3) hint coverage ----

    [BenchFact]
    public void HintCoverageAgainstTheOracle()
    {
        var report = new BenchReport(_output, "hint-coverage");
        foreach ((int divisor, int min) in new[] { (64, 0), (64, 32) })
        {
            string at = $"sweep_1_{divisor}_min{min}.";
            var world = ReplicationWorld.Boot(size: 32, movers: 52, seed: 77, mix: new ScenarioMix());
            var run = new ReplicationRun(world, divisor, min);
            long mismatchTicks = 0, mismatches = 0, sweepCatches = 0, lateBeyondPeriod = 0;
            var lateness = new List<double>();
            var pendingSince = new Dictionary<long, long>();
            var changedByField = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var sweepByField = new SortedDictionary<string, long>(StringComparer.Ordinal);
            long spawned = 0, despawned = 0;
            int maxPeriod = 0;
            for (int i = 0; i < 10000; i++)
            {
                run.Step();
                long tick = world.World.Tick;
                maxPeriod = Math.Max(maxPeriod, run.Replicator.SweepPeriodTicks);
                spawned += run.LastDelta.Spawned.Count;
                despawned += run.LastDelta.Despawned.Count;
                foreach (EntityChange change in run.LastDelta.Changed)
                    foreach (NetField f in Fields(change.Mask)) changedByField[f.ToString()] = changedByField.GetValueOrDefault(f.ToString()) + 1;
                foreach (SweepCatch c in run.Replicator.LastSweepCatches)
                {
                    sweepCatches++;
                    foreach (NetField f in Fields(c.Fields)) sweepByField[f.ToString()] = sweepByField.GetValueOrDefault(f.ToString()) + 1;
                }
                // Hinted fields: exact every tick.
                List<string> differences = ReplicationRun.Differences(run.Client, run.Truth, static e => e with { Name = null });
                if (differences.Count > 0)
                {
                    mismatchTicks++;
                    mismatches += differences.Count;
                    if (mismatchTicks <= 5) _output.WriteLine(ReplicationRun.Describe(tick, differences));
                }
                // Names: how late, against the sweep period. An entity that left the turfs is no longer late (its next
                // spawn record carries its name).
                foreach (long gone in pendingSince.Keys.Where(id => !run.Truth.ContainsKey(id)).ToList()) pendingSince.Remove(gone);
                foreach (EntityView t in run.Truth.Values)
                {
                    run.Client.TryGetView(t.Id, out EntityView e);
                    if (e.Name == t.Name)
                    {
                        if (pendingSince.Remove(t.Id, out long since)) lateness.Add(tick - since);
                        continue;
                    }
                    if (!pendingSince.ContainsKey(t.Id)) pendingSince[t.Id] = tick;
                }
                foreach ((long id, long since) in pendingSince)
                    if (tick - since > run.Replicator.SweepPeriodTicks) lateBeyondPeriod++;
            }
            report.Add(at + "ticks", 10000);
            report.Add(at + "scenario", world.Actions);
            report.Add(at + "steps_tried", world.Steps);
            report.Add(at + "steps_moved", world.StepsMoved);
            report.Add(at + "spawned", spawned);
            report.Add(at + "despawned", despawned);
            report.Add(at + "changed_by_field", changedByField);
            report.Add(at + "mismatch_ticks_hinted_fields", mismatchTicks);
            report.Add(at + "mismatches_hinted_fields", mismatches);
            report.Add(at + "names_later_than_the_sweep_period", lateBeyondPeriod);
            report.Add(at + "sweep_only_catches", sweepCatches);
            report.Add(at + "sweep_only_catches_by_field", sweepByField);
            report.Add(at + "name_lateness_ticks", Dist.Of(lateness));
            report.Add(at + "sweep_period_ticks_max", maxPeriod);
            report.Add(at + "entities_at_end", run.Replicator.EntityCount);
            report.Add(at + "runtimes", world.Runtimes.Count);
        }
        report.Save();
    }

    // ---- helpers ----

    private static void Join(BenchReport report, string at, Replicator replicator, TimedEncoder encoder, ReplicationWorld world)
    {
        var watch = new Stopwatch();
        var gridTimes = new List<double>();
        MapGrid grid = null;
        byte[] gridBytes = null;
        for (int i = 0; i < 7; i++)
        {
            watch.Restart();
            grid = replicator.ReadMapGrid();
            gridBytes = encoder.EncodeMapGrid(grid);
            gridTimes.Add(watch.Elapsed.TotalMilliseconds);
        }
        report.Add(at + "mapgrid_ms_first", Math.Round(gridTimes[0], 3));
        report.Add(at + "mapgrid_ms", Dist.Of(gridTimes.Skip(2).ToList()));
        report.Add(at + "mapgrid_bytes", gridBytes.Length);
        report.Add(at + "mapgrid_runs", grid.Runs.Count);
        var times = new List<double>();
        int bytes = 0;
        for (int i = 0; i < 20; i++)
        {
            watch.Restart();
            Snapshot snapshot = replicator.BuildSnapshot(world.Movers[0].dm_id, 0);
            bytes = encoder.EncodeSnapshot(snapshot).Length;
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        report.Add(at + "snapshot_bytes", bytes);
        report.Add(at + "snapshot_ms_first", Math.Round(times[0], 3));
        report.Add(at + "snapshot_ms", Dist.Of(times.Skip(2).ToList()));
        report.Add(at + "snapshot_bytes_per_entity", Math.Round(bytes / (double)Math.Max(1, replicator.EntityCount), 2));
    }

    private static void PerTick(BenchReport report, string at, ReplicationWorld world, Replicator replicator, TimedEncoder encoder, int movers, int warm, int ticks)
    {
        world.ActiveMovers = movers;
        // Warm up for the ticks given and at least 2 s, so that tiered compilation has promoted the hot code.
        var warming = Stopwatch.StartNew();
        for (int i = 0; i < warm || warming.Elapsed.TotalSeconds < 2; i++)
        {
            world.Step();
            replicator.BuildDelta(world.World.Tick);
        }
        var step = new List<double>();
        var r3 = new List<double>();
        var encode = new List<double>();
        var bytes = new List<double>();
        var candidates = new List<double>();
        var hinted = new List<double>();
        var created = new List<double>();
        var swept = new List<double>();
        var changed = new List<double>();
        long moved0 = world.StepsMoved, tried0 = world.Steps;
        long alloc0 = GC.GetAllocatedBytesForCurrentThread();
        long allocR3 = 0;
        var watch = new Stopwatch();
        for (int i = 0; i < ticks; i++)
        {
            watch.Restart();
            world.Step();
            step.Add(watch.Elapsed.TotalMilliseconds);
            encoder.DeltaEncodeTicks = 0;
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            watch.Restart();
            replicator.BuildDelta(world.World.Tick);
            r3.Add(watch.Elapsed.TotalMilliseconds);
            allocR3 += GC.GetAllocatedBytesForCurrentThread() - a0;
            encode.Add(encoder.DeltaEncodeTicks * 1000.0 / Stopwatch.Frequency);
            ReplicationDetail d = replicator.LastDetail;
            bytes.Add(d.BodyBytes);
            candidates.Add(d.Candidates);
            hinted.Add(d.Hinted);
            created.Add(d.Created);
            swept.Add(d.Swept);
            changed.Add(d.Changed);
        }
        report.Add(at + "tick_step_ms", Dist.Of(step));
        report.Add(at + "r3_build_delta_ms", Dist.Of(r3));
        report.Add(at + "r3_encode_ms", Dist.Of(encode));
        report.Add(at + "r3_diff_ms_mean", Math.Round(r3.Average() - encode.Average(), 4));
        report.Add(at + "r3_alloc_bytes_per_tick", allocR3 / ticks);
        report.Add(at + "delta_body_bytes", Dist.Of(bytes));
        report.Add(at + "candidates", Dist.Of(candidates));
        report.Add(at + "candidates_hinted", Dist.Of(hinted));
        report.Add(at + "candidates_created", Dist.Of(created));
        report.Add(at + "candidates_swept", Dist.Of(swept));
        report.Add(at + "changed_entities", Dist.Of(changed));
        report.Add(at + "steps_per_tick", Math.Round((world.Steps - tried0) / (double)ticks, 2));
        report.Add(at + "steps_moved_per_tick", Math.Round((world.StepsMoved - moved0) / (double)ticks, 2));
        report.Add(at + "alloc_bytes_per_tick_total", (GC.GetAllocatedBytesForCurrentThread() - alloc0) / ticks);
    }

    private static void CountObjects(ReplicationWorld world, out int atoms, out int objects, out int movables)
    {
        int a = 0, o = 0, m = 0;
        world.World.Objects.ForEachLiveInIdOrder((id, obj) =>
        {
            o++;
            if (obj is atom) a++;
            if (obj is atom_movable) m++;
        });
        atoms = a;
        objects = o;
        movables = m;
    }

    private static IEnumerable<NetField> Fields(NetField mask)
    {
        for (int bit = 0; bit < 16; bit++)
            if (((int)mask & (1 << bit)) != 0) yield return (NetField)(1 << bit);
    }

    // ---- (1)'s three forms of a var ----

    private sealed class PlainVars
    {
        public float a, b, c, d;
        public object r;
    }

    private sealed class DirtyBitVars
    {
        public ulong DirtyMask;
        private float _a, _b, _c, _d;
        private object _r;

        public float a { get => _a; set { if (_a != value) { _a = value; DirtyMask |= 1; } } }
        public float b { get => _b; set { if (_b != value) { _b = value; DirtyMask |= 2; } } }
        public float c { get => _c; set { if (_c != value) { _c = value; DirtyMask |= 4; } } }
        public float d { get => _d; set { if (_d != value) { _d = value; DirtyMask |= 8; } } }
        public object r { get => _r; set { if (!ReferenceEquals(_r, value)) { _r = value; DirtyMask |= 16; } } }
    }

    private sealed class StampContext
    {
        public long Tick;
        public readonly List<StampedVars> Dirty = new();
    }

    private sealed class StampedVars
    {
        public StampContext Context;
        private long _lastDirtyTick = -1;
        private readonly long[] _stamps = new long[5];
        private float _a, _b, _c, _d;
        private object _r;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Mark(int field)
        {
            long tick = Context.Tick;
            _stamps[field] = tick;
            if (_lastDirtyTick != tick)
            {
                _lastDirtyTick = tick;
                Context.Dirty.Add(this);
            }
        }

        public float a { get => _a; set { if (_a != value) { _a = value; Mark(0); } } }
        public float b { get => _b; set { if (_b != value) { _b = value; Mark(1); } } }
        public float c { get => _c; set { if (_c != value) { _c = value; Mark(2); } } }
        public float d { get => _d; set { if (_d != value) { _d = value; Mark(3); } } }
        public object r { get => _r; set { if (!ReferenceEquals(_r, value)) { _r = value; Mark(4); } } }
    }
}
