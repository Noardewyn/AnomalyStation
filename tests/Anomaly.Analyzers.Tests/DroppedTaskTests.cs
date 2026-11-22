using static Anomaly.Analyzers.Tests.AnalyzerHarness;

namespace Anomaly.Analyzers.Tests;

/// <summary>ANOM0001: a DmTask value that is discarded (every project in scope).</summary>
public class DroppedTaskTests
{
    private const string Procs = """
        public abstract class Proc
        {
            public abstract DmTask Run();
            public virtual DmTask<int> Value() => DmTask<int>.FromResult(1);
            public IDmTask Untyped() => DmTask.CompletedTask;
            public static async DmTask Sleeps() { await sleep(1); }
            public static async DmTask<object> SleepsT() { await sleep(1); return null; }
            public static void Out(out DmTask task) => task = DmTask.CompletedTask;
            public static (DmTask, int) Pair() => (DmTask.CompletedTask, 1);
        }

        """;

    [Fact]
    public void AnExpressionStatementDropsATaskInASynchronousCallerOfANonAsyncDeclaration()
    {
        // CS4014 misses these: the declarations the calls bind to are abstract or expression-bodied.
        Expect(Analyze(Procs + """
            public static class C
            {
                public static void M(Proc p)
                {
                    p.Run();
                    p.Value();
                    p.Untyped();
                }
            }
            """),
            ("ANOM0001", "p.Run()"), ("ANOM0001", "p.Value()"), ("ANOM0001", "p.Untyped()"));
    }

    [Fact]
    public void ADiscardDropsATask()
    {
        Expect(Analyze(Procs + """
            public static class C
            {
                public static async DmTask M(Proc p)
                {
                    _ = p.Run();
                    _ = Proc.SleepsT();
                    (_, int a) = (p.Run(), 1);
                    var (_, b) = Proc.Pair();
                    Proc.Out(out _);
                    await sleep(a + b);
                }

                public static void Local()
                {
                    var _ = Proc.Sleeps(); // a local named '_' that nothing reads
                }
            }
            """),
            ("ANOM0001", "p.Run()"), ("ANOM0001", "Proc.SleepsT()"), ("ANOM0001", "_"), ("ANOM0001", "_"),
            ("ANOM0001", "_"), ("ANOM0001", "Proc.Sleeps()"));
    }

    [Fact]
    public void ACallThroughAnotherAssemblyIsCheckedToo()
    {
        var library = Compile("""
            public static class Lib
            {
                public static async DmTask Sleeps() { await sleep(1); }
            }
            """, assemblyName: "Lib");
        Expect(Analyze("""
            public static class C
            {
                public static void M() { Lib.Sleeps(); }
            }
            """, extra: new[] { AsReference(library) }),
            ("ANOM0001", "Lib.Sleeps()"));
    }

    [Fact]
    public void LambdasLocalFunctionsExpressionBodiesAndConditionalAccessDropTasksToo()
    {
        Expect(Analyze(Procs + """
            public static class C
            {
                public static void Body(Proc p) => p.Run();

                public static void M(Proc p)
                {
                    Action a = () => p.Run();
                    Action b = () => { p.Value(); };
                    Waiting.DetachSync(() => p.Run()); // DetachSync is for synchronous callees: Detach(p.Run()) is meant
                    p?.Run();
                    void Local() { Proc.Sleeps(); }
                    Local();
                }
            }
            """),
            ("ANOM0001", "p.Run()"), ("ANOM0001", "p.Run()"), ("ANOM0001", "p.Value()"), ("ANOM0001", "p.Run()"),
            ("ANOM0001", "p?.Run()"), ("ANOM0001", "Proc.Sleeps()"));
    }

    [Fact]
    public void TheCallFormsAreNotDrops()
    {
        Expect(Analyze(Procs + """
            public static class C
            {
                public static async DmTask<int> M(Proc p)
                {
                    await p.Run();
                    int v = await p.Value();
                    Waiting.Detach(p.Run());
                    object x = Waiting.Detach(Proc.SleepsT());
                    Waiting.Spawn(-1, () => p.Run());
                    Waiting.Spawn(0, async () => { await sleep(1); });
                    Func<DmTask> later = () => p.Run();
                    DmTask stored = p.Run(); // stored, not dropped: ANOM0002 in translated code only
                    await stored;
                    return v;
                }

                public static void Sync(Proc p)
                {
                    Waiting.NoSleep(p.Run());
                    int v = Waiting.NoSleep(p.Value());
                    Waiting.DetachSync(() => Console.WriteLine(v));
                }

                public static DmTask PassThrough(Proc p) => p.Run();
            }
            """));
    }

    [Fact]
    public void TheMessageNamesTheFix()
    {
        var messages = Messages(Procs + """
            public static class C
            {
                public static void M(Proc p) { _ = p.Run(); }
            }
            """);
        Assert.Equal(
            "ANOM0001: The DmTask of 'p.Run()' is dropped: await it, write Waiting.Detach(...) for a detached call "
            + "(INVOKE_ASYNC, ASYNC), or Waiting.NoSleep(...) in a synchronous proc",
            Assert.Single(messages));
    }
}
