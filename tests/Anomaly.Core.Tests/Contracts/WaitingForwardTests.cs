using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// The DM spellings of the waiting primitives forward to the Kernel (docs/ai/specs/waiting-model.md sections 5, 6, 8).
/// </summary>
public class WaitingForwardTests
{
    [Fact]
    public void SleepRoundsLikeTheKernel()
    {
        var w = new World();
        long woke = -1;

        async DmTask Sleeper()
        {
            await Dm.sleep(1);   // 1 ds = 2 ticks (waiting-model.md section 8)
            woke = World.Current.Tick;
        }

        using (w.Enter())
            Waiting.Detach(Sleeper());
        w.Step(3);
        Assert.Equal(2, woke);
    }

    [Fact]
    public void SpawnRunsANegativeDelayNowAndZeroLaterInTheTick()
    {
        var w = new World();
        var order = new List<string>();
        using (w.Enter())
        {
            Dm.spawn(0, async () => order.Add("spawn(0)"));
            Dm.spawn(-1, async () => order.Add("spawn(-1)"));
            order.Add("caller");
        }
        w.Step();
        Assert.Equal(new[] { "spawn(-1)", "caller", "spawn(0)" }, order);
    }

    [Fact]
    public void InputAndAlertAreStubsThatCompleteWithNull()
    {
        DmTask<object> answer = Dm.input("bob", "message", "title");
        Assert.True(answer.IsCompleted);
        Assert.True(answer.TryGetResult(out object value));
        Assert.Null(value);
        Assert.True(Dm.alert("bob", "message").IsCompleted);
    }
}
