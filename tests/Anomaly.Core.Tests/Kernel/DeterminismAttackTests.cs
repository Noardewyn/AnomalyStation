using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>RNG regression values and state-hash determinism (D31, D64: compared on one platform).</summary>
public class DeterminismAttackTests
{
    // ---- RNG ----

    [Fact]
    public void TheRngCoreIsCanonicalSplitMix64()
    {
        // Reference output of SplitMix64 (Steele, Lea, Flood 2014; Vigna's splitmix64.c) for the seed 1234567.
        var r = new Rng(1234567);
        Assert.Equal(new ulong[] { 6457827717110365317, 3203168211198807973, 9817491932198370423, 4593380528125082431, 16408922859458223821 },
            Enumerable.Range(0, 5).Select(_ => r.NextUInt64()));
    }

    [Fact]
    public void PerTickStreamsArePinnedAndReseededBeforeTheFirstDrain()
    {
        // Values computed independently (Python) from Reseed(seed, tick) = Mix64(seed ^ Mix64(tick + G)) and the
        // SplitMix64 step. A change here changes every golden trace: update it only on purpose.
        var w = new World(42);
        var drawn = new List<ulong>();
        using (w.Enter()) Waiting.Detach(Sleeper());
        w.AddPhase("noise", x => x.Rng.NextUInt64()); // draws after the drain never shift the next tick
        w.Step(2);
        Assert.Equal(new ulong[] { 3722946841873465102, 16082488802077818305, 13938158061835861011 }, drawn);

        var r = new Rng(0);
        r.Reseed(1, 1);
        Assert.Equal(2424468580836592839UL, r.NextUInt64());
        r.Reseed(29051994, 100);
        Assert.Equal(new ulong[] { 7384085609782115760, 1030019741282318112, 9211008771434759311 },
            Enumerable.Range(0, 3).Select(_ => r.NextUInt64()));

        r.Reseed(42, 1);
        Assert.Equal(5, r.NextInt(1, 6));
        Assert.Equal(0.8718334436416626f, r.NextFloat());

        async DmTask Sleeper()
        {
            await Sleep(0.5f); // wakes first in tick 1
            drawn.Add(World.Current.Rng.NextUInt64());
            drawn.Add(World.Current.Rng.NextUInt64());
            await Sleep(0.5f); // first in tick 2
            drawn.Add(World.Current.Rng.NextUInt64());
        }
    }

    [Fact]
    public void ATicksNumbersDoNotDependOnHowManyWereDrawnBefore()
    {
        ulong FirstOfTick3(int drawsBefore)
        {
            var w = new World(7);
            ulong first = 0;
            w.AddPhase("draw", x =>
            {
                if (x.Tick < 3) for (int i = 0; i < drawsBefore; i++) x.Rng.NextUInt64();
                else if (x.Tick == 3) first = x.Rng.NextUInt64();
            });
            w.Step(3);
            return first;
        }
        Assert.Equal(FirstOfTick3(0), FirstOfTick3(1));
        Assert.Equal(FirstOfTick3(0), FirstOfTick3(1000));
    }

    // ---- state hash ----

    // A scenario touching every kernel path: nested awaits, the completion trampoline, detached errors, NoSleep,
    // waitfor wrappers, spawn at every delay class, phases issuing zero-delay work, RNG-driven sleeps, a runaway
    // cut, usr. Returns the hash after every tick.
    private static List<ulong> Scenario(ulong seed, bool perturb, bool withSink)
    {
        var w = new World(seed);
        if (withSink) w.RuntimeErrorSink = _ => { };
        w.Scheduler.MaxZeroDelayHops = 50;
        var trace = new List<string>();
        w.AddStateHasher("trace", (World _, ref StateHasher h) => { foreach (var s in trace) h.Add(s); });
        var hashes = new List<ulong>();

        async DmTask<int> Deep(int n) { if (n == 0) { await Sleep(0.5f); return 0; } return 1 + await Deep(n - 1); }
        async DmTask Worker(int id)
        {
            for (int i = 0; i < 5; i++)
            {
                int ticks = World.Current.Rng.NextInt(0, 3);
                await SleepTicks(ticks);
                trace.Add($"w{id}:{i}:{World.Current.Tick}:{World.Current.usr}");
                if (World.Current.Rng.NextInt(1, 10) == 1) throw new InvalidOperationException("worker " + id);
            }
        }
        async DmTask Runaway() { while (true) await Sleep(0); }
        int Wrapper()
        {
            var dot = new DotBox<int>();
            return Waiting.WaitforFalse(Body(dot), dot);
            async DmTask<int> Body(DotBox<int> d) { d.Value = World.Current.Rng.NextInt(1, 100); await Sleep(1); return 0; }
        }

        using (w.Enter())
        {
            w.usr = "starter";
            for (int i = 0; i < 20; i++) Waiting.Detach(Worker(i));
            Waiting.Detach(Runaway());
            w.usr = null;
        }
        w.AddPhase("commands", x =>
        {
            x.usr = "p" + x.Tick % 3;
            if (perturb && x.Tick == 4) x.Rng.NextUInt64(); // one extra draw
            trace.Add("deep=" + Waiting.Detach(Deep(100)));
            trace.Add("wf=" + Wrapper());
            trace.Add("nosleep=" + Waiting.NoSleep(Deep(0)));
            Waiting.Spawn(-1, async () => { await Sleep(0); trace.Add("spawn-1:" + World.Current.usr); });
            Waiting.Spawn(0, async () => trace.Add("spawn0:" + World.Current.usr));
            Waiting.Spawn(x.Rng.NextFloat() * 3, async () => trace.Add("spawnN:" + World.Current.Tick));
        });
        for (int t = 0; t < 12; t++)
        {
            w.Step();
            hashes.Add(w.ComputeStateHash());
        }
        return hashes;
    }

    [Fact]
    public void TheSameScenarioHashesIdenticallyTickByTick()
    {
        Assert.Equal(Scenario(5, false, true), Scenario(5, false, true));
        Assert.Equal(Scenario(5, false, true), Scenario(5, false, false)); // the sink does not affect the state
    }

    [Fact]
    public void TheScenarioHashIsPinnedSoDebugAndReleaseMustAgree()
    {
        // The suite runs in Debug (class state machines) and Release (struct state machines): both must reach this
        // exact state. Update the value only for an intended change of kernel behaviour or of the scenario.
        Assert.Equal(PinnedScenarioHash, Scenario(5, false, true)[^1]);
    }

    private const ulong PinnedScenarioHash = 16899860203049809635UL;

    [Fact]
    public void ADivergenceIsVisibleFromTheTickItHappens()
    {
        var a = Scenario(5, false, true);
        var b = Scenario(5, true, true);
        Assert.Equal(a.Take(3), b.Take(3));
        Assert.NotEqual(a[3], b[3]);
        Assert.NotEqual(Scenario(5, false, true)[0], Scenario(6, false, true)[0]);
    }
}

/// <summary>waiting-model.md names only APIs that exist (section "Kernel API reference" and every Type.Member).</summary>
public class WaitingSpecConsistencyTests
{
    private static string Spec()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AnomalyStation.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir.FullName, "docs", "ai", "specs", "waiting-model.md"));
    }

    private static readonly Assembly Kernel = typeof(World).Assembly;

    private static Type KernelType(string name, bool generic) =>
        Kernel.GetExportedTypes().FirstOrDefault(t => t.Name == (generic ? name + "`1" : name));

    private static bool HasMember(Type t, string member) =>
        t.GetMember(member, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length > 0
        || (t.IsInterface && t.GetInterfaces().Any(i => HasMember(i, member)))
        || t.GetInterfaces().Any(i => i.GetMember(member).Length > 0);

    [Fact]
    public void EveryTypeAndMemberInTheApiReferenceExists()
    {
        var spec = Spec();
        int at = spec.IndexOf("## Kernel API reference", StringComparison.Ordinal);
        Assert.True(at >= 0);
        var missing = new List<string>();
        int rows = 0;
        foreach (var line in spec[at..].Split('\n').Where(l => l.StartsWith("| `")))
        {
            var cells = line.Split('|');
            var types = Regex.Matches(cells[1], "`([A-Za-z]+)(<T>)?`")
                .Select(m => (m.Value, Type: KernelType(m.Groups[1].Value, m.Groups[2].Success))).ToList();
            foreach (var (text, type) in types) if (type == null) missing.Add(text);
            // Parenthesised remarks outside code spans name other things: "(`DmTask<T>`)", "(the shim's `sleep` ...)".
            var members = Regex.Replace(cells[2], "\\s\\([^)]*\\)", "");
            foreach (Match m in Regex.Matches(members, "`(?:([A-Za-z]+)\\.)?([A-Za-z]+)(?:<T>)?(?:\\([^`]*\\))?`"))
            {
                var member = m.Groups[2].Value;
                if (!m.Groups[1].Success && (member == "async" || KernelType(member, false) != null || KernelType(member, true) != null)) continue;
                bool ok = m.Groups[1].Success
                    ? KernelType(m.Groups[1].Value, false) is { } owner && HasMember(owner, member)
                    : types.Any(t => t.Type != null && HasMember(t.Type, member));
                if (!ok) missing.Add($"{cells[1].Trim()} {m.Value}");
            }
            rows++;
        }
        Assert.True(rows >= 8);
        Assert.Empty(missing);
    }

    [Fact]
    public void EveryQualifiedKernelMemberInTheProseExists()
    {
        // `Type.Member` and `Type.Member(..)` anywhere in the spec, for the kernel's public types.
        var missing = new List<string>();
        foreach (Match m in Regex.Matches(Spec(), "`([A-Z][A-Za-z]+)(<T>)?\\.([A-Za-z]+)"))
        {
            var type = KernelType(m.Groups[1].Value, m.Groups[2].Success) ?? KernelType(m.Groups[1].Value, !m.Groups[2].Success);
            if (type == null || m.Groups[3].Value is "cs" or "md") continue; // not a kernel type, or a file name
            if (!HasMember(type, m.Groups[3].Value)) missing.Add(m.Value);
        }
        Assert.Empty(missing);
    }

    [Fact]
    public void EveryTestTheSpecCitesExists()
    {
        var tests = typeof(WaitingSpecConsistencyTests).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods().Select(mi => (Type: t.Name, Method: mi.Name))).ToList();
        var missing = new List<string>();
        foreach (Match m in Regex.Matches(Spec(), "`(?:([A-Z][A-Za-z]+Tests)\\.)?([A-Z][A-Za-z]{20,})`"))
        {
            if (m.Groups[1].Success && !tests.Any(t => t.Type == m.Groups[1].Value && t.Method == m.Groups[2].Value)) missing.Add(m.Value);
            if (!m.Groups[1].Success && !tests.Any(t => t.Method == m.Groups[2].Value) && KernelType(m.Groups[2].Value, false) == null) missing.Add(m.Value);
        }
        Assert.Empty(missing);
    }
}
