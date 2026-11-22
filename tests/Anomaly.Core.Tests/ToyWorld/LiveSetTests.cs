using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.ToyWorld;

/// <summary>
/// The objects alive at a state hash must not depend on the build (docs/ai/specs/object-model.md section 8, "Live objects
/// and the build"). The owner's Debug run and the lead's Release run of the toy world once gave different hashes: the
/// Master's stack canary (stack_end_detector.dm), held only by a local of the running Loop, was collected in the
/// optimized build. BYOND keeps a proc's vars alive until the proc returns, so the canary lives while the Loop runs.
/// </summary>
public class LiveSetTests
{
    [Fact]
    public void TheMastersStackCanaryLivesWhileTheLoopRunsEvenAfterAFullCollection()
    {
        var s = ToyScenario.Run();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using (s.World.Enter())
        {
            Assert.NotNull(Master.stack_end_detector);
            Assert.True(Dm.truthy(Master.stack_end_detector.check())); // the Loop's stack is still going
        }
    }
}
