using static Anomaly.Analyzers.Tests.AnalyzerHarness;

namespace Anomaly.Analyzers.Tests;

/// <summary>ANOM0003: translated code never reads a task's state or result, nor uses a kernel awaiter directly.</summary>
public class BlockingReadTests
{
    private const string Procs = """
        public abstract class datum
        {
            public abstract DmTask Run();
            public virtual DmTask<object> Value() => DmTask<object>.FromResult(null);
            public IDmTask Untyped() => DmTask.CompletedTask;
        }

        """;

    [Fact]
    public void ReadingATaskOrItsAwaiterIsReported()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                public override DmTask Run() => DmTask.CompletedTask;

                public object M()
                {
                    Run().GetAwaiter().GetResult();
                    object v = Value().GetAwaiter().GetResult();
                    Value().TryGetResult(out object w);
                    bool done = Run().IsCompleted;
                    bool failed = Untyped().IsFaulted;
                    Exception e = Value().Exception;
                    sleep(1).GetAwaiter();
                    return v ?? w;
                }
            }
            """, translated: true),
            ("ANOM0003", "Run().GetAwaiter()"), ("ANOM0003", "Value().GetAwaiter()"), ("ANOM0003", "Value().TryGetResult(out object w)"),
            ("ANOM0003", "Run().IsCompleted"), ("ANOM0003", "Untyped().IsFaulted"), ("ANOM0003", "Value().Exception"),
            ("ANOM0003", "sleep(1).GetAwaiter()"));
    }

    [Fact]
    public void AStoredAwaiterIsReportedAtEachUse()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                public override DmTask Run() => DmTask.CompletedTask;

                public object M()
                {
                    var awaiter = Value().GetAwaiter();
                    if (awaiter.IsCompleted) return awaiter.GetResult();
                    return null;
                }
            }
            """, translated: true),
            ("ANOM0003", "Value().GetAwaiter()"), ("ANOM0003", "awaiter.IsCompleted"), ("ANOM0003", "awaiter.GetResult()"));
    }

    [Fact]
    public void StaticMembersAwaitAndAsObjectTaskAreAllowed()
    {
        Expect(Analyze(Procs + """
            public class thing : datum
            {
                public override DmTask Run() => DmTask.CompletedTask;
                public DmTask<object> ByName() => Untyped().AsObjectTask();

                public async DmTask<object> M()
                {
                    await DmTask.CompletedTask;
                    await DmTask.FromException(new InvalidOperationException()).AsObjectTask();
                    return await DmTask<object>.FromResult(1);
                }
            }
            """, translated: true));
    }

    [Fact]
    public void LambdasLocalFunctionsAndGenericTasksAreCheckedToo()
    {
        Expect(Analyze(Procs + """
            public abstract class holder<T>
            {
                public abstract DmTask<T> Get();

                public bool M()
                {
                    Func<bool> f = () => Get().IsCompleted;
                    return f() && Local();

                    bool Local() => Get().TryGetResult(out _);
                }
            }
            """, translated: true),
            ("ANOM0003", "Get().IsCompleted"), ("ANOM0003", "Get().TryGetResult(out _)"));
    }

    [Fact]
    public void OutsideTranslatedCodeReadsAreNotReported()
    {
        Expect(Analyze(Procs + """
            public static class host
            {
                public static object M(datum d) => d.Value().IsCompleted ? d.Value().GetAwaiter().GetResult() : null;
            }
            """, translated: false));
    }
}
