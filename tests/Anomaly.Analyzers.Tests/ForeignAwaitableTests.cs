using static Anomaly.Analyzers.Tests.AnalyzerHarness;

namespace Anomaly.Analyzers.Tests;

/// <summary>ANOM0004: a DmTask proc awaits only kernel awaitables (awaiters that implement IDmAwaiter).</summary>
public class ForeignAwaitableTests
{
    private const string Awaitables = """
        using System.Runtime.CompilerServices;
        using System.Threading.Tasks;

        public abstract class datum
        {
            public abstract DmTask Run();
            public virtual DmTask<int> Value() => DmTask<int>.FromResult(1);
        }

        public readonly struct Foreign
        {
            public Awaiter GetAwaiter() => default;
            public readonly struct Awaiter : INotifyCompletion
            {
                public bool IsCompleted => true;
                public void OnCompleted(Action continuation) => continuation();
                public void GetResult() { }
            }
        }

        public readonly struct Kernelish
        {
            public Awaiter GetAwaiter() => default;
            public readonly struct Awaiter : INotifyCompletion, IDmAwaiter
            {
                public bool IsCompleted => true;
                public void OnCompleted(Action continuation) => continuation();
                public void GetResult() { }
            }
        }

        """;

    [Fact]
    public void AwaitingTaskValueTaskOrAnotherForeignAwaiterInADmTaskProcIsReported()
    {
        Expect(Analyze(Awaitables + """
            public static class C
            {
                public static async DmTask<int> M()
                {
                    await Task.CompletedTask;
                    await Task.Delay(1);
                    await Task.Yield();
                    int v = await new ValueTask<int>(3);
                    await new Foreign();
                    return v;
                }
            }
            """),
            ("ANOM0004", "await Task.CompletedTask"), ("ANOM0004", "await Task.Delay(1)"), ("ANOM0004", "await Task.Yield()"),
            ("ANOM0004", "await new ValueTask<int>(3)"), ("ANOM0004", "await new Foreign()"));
    }

    [Fact]
    public void KernelAwaitablesAndCallsThroughAbstractDeclarationsAreAllowed()
    {
        Expect(Analyze(Awaitables + """
            public static class C
            {
                public static async DmTask<int> M(datum d)
                {
                    await sleep(1);
                    await Sleeping.SleepTicks(2);
                    await d.Run();
                    await new Kernelish();
                    return await d.Value();
                }
            }
            """));
    }

    [Fact]
    public void LambdasAndLocalFunctionsAreJudgedByTheirOwnReturnType()
    {
        Expect(Analyze(Awaitables + """
            public static class C
            {
                public static async DmTask M()
                {
                    Func<DmTask> detached = async () => { await Task.Yield(); };
                    Func<Task> hosted = async () => { await Task.Yield(); }; // not a DmTask proc
                    Waiting.Spawn(-1, async () => await Task.CompletedTask);
                    await Local();
                    await Hosted(); // a Task awaited by the DmTask proc M

                    async DmTask<int> Local() { await Task.CompletedTask; return 1; }
                    async Task Hosted() { await Task.Delay(1); } // not a DmTask proc
                }

                public static async Task Host(datum d) => await Task.Delay(1); // host code, not a DmTask proc
            }
            """),
            ("ANOM0004", "await Task.Yield()"), ("ANOM0004", "await Task.CompletedTask"), ("ANOM0004", "await Hosted()"),
            ("ANOM0004", "await Task.CompletedTask"));
    }

    [Fact]
    public void AwaitForeachAndAwaitUsingAwaitValueTasks()
    {
        Expect(Analyze(Awaitables + """
            public sealed class Resource : IAsyncDisposable
            {
                public ValueTask DisposeAsync() => default;
            }

            public static class C
            {
                public static async IAsyncEnumerable<int> Items() { yield return 1; await Task.CompletedTask; }

                public static async DmTask M()
                {
                    await foreach (var i in Items()) { }
                    await using (var r = new Resource()) { }
                    await using var s = new Resource();
                }
            }
            """),
            ("ANOM0004", "await"), ("ANOM0004", "await"), ("ANOM0004", "await"));
    }

    [Fact]
    public void ADynamicAwaitCannotBeProvedKernelAndIsReported()
    {
        Expect(Analyze(Awaitables + """
            public static class C
            {
                public static async DmTask M(dynamic x) { await x; }
            }
            """),
            ("ANOM0004", "await x"));
    }

    [Fact]
    public void TheMessageNamesTheAwaitedType()
    {
        var messages = Messages(Awaitables + """
            public static class C
            {
                public static async DmTask M() { await Task.CompletedTask; }
            }
            """);
        Assert.Equal(
            "ANOM0004: 'await Task.CompletedTask' awaits 'Task', whose awaiter does not implement IDmAwaiter: a DmTask proc "
            + "awaits only DmTask, DmTask<T>, sleep and other kernel awaitables",
            Assert.Single(messages));
    }
}
