using Anomaly.Core.Kernel;
using static Anomaly.Core.Kernel.Sleeping;

namespace Anomaly.Core.Tests.Kernel;

/// <summary>
/// When the innermost proc of a deep awaiting chain returns, every caller resumes in turn. Without a
/// bound this nests on the native stack (about 4,400 levels overflowed a 1 MB thread before the trampoline).
/// </summary>
public class CompletionDepthTests
{
    // Each level suspends before it recurses, so the descent is shallow and only the completion is deep.
    private static async DmTask<int> Chain(int n, List<int> order)
    {
        await Sleep(0);
        if (n == 0) return 0;
        int below = await Chain(n - 1, order);
        order.Add(n);
        return below + 1;
    }

    [Fact]
    public void ADeepCompletionChainFinishesOnASmallStackInCallerOrder()
    {
        const int depth = 50_000;
        int result = -1;
        List<int> order = null;
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var w = new World(1);
                order = new List<int>(depth);
                DmTask<int> task;
                using (w.Enter()) task = Chain(depth, order);
                w.Step(1);
                result = task.IsCompleted ? task.GetAwaiter().GetResult() : -2;
            }
            catch (Exception e)
            {
                failure = e;
            }
        }, maxStackSize: 1024 * 1024);
        thread.Start();
        thread.Join();
        Assert.Null(failure);
        Assert.Equal(depth, result);
        Assert.Equal(Enumerable.Range(1, depth), order); // innermost caller first, exactly like plain returns
    }

    [Fact]
    public void ResumptionsBeyondTheInlineDepthStillRunBeforeTheNextScheduledEntry()
    {
        var w = KernelTestWorld.New(out var errors);
        var log = new List<string>();
        using (w.Enter())
        {
            Waiting.Detach(Top());
            Waiting.Detach(Other());
        }
        w.Step(1);
        Assert.Empty(errors);
        Assert.Equal(new[] { "top resumed", "other" }, log);

        async DmTask Top()
        {
            await Nest(500); // far beyond the inline depth
            log.Add("top resumed");
        }

        static async DmTask Nest(int n)
        {
            if (n == 0) { await Sleep(0.5f); return; }
            await Nest(n - 1);
        }

        async DmTask Other() { await Sleep(0.5f); log.Add("other"); }
    }
}
