using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// The engineering campaign (D287): a live type whose own examine is a deferred live-partial row (D264) keeps tg's base
/// examine, so examining it still shows its description (the generated stub returns base.examine).
/// </summary>
public class DeferredExamineTests
{
    [Fact]
    public void ADeferredExamineStillShowsTheDescription()
    {
        var r = new T2Room(5601);
        var bob = r.Join(12, 12, "Bob");
        var sensor = r.In(() => Dm.@new<obj_machinery_air_sensor>(locate(12, 11, 1))); // its examine is a deferred row (Engineering E5)
        string examine = r.In(() => string.Join("|", Dm.each(Waiting.NoSleep(sensor.examine(bob))).Select(Dm.str)));
        Assert.Contains(Dm.str(sensor.desc), examine);
    }
}
