using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8y;

/// <summary>
/// M8y slice B on the booted MetaStation: the two stasis beds of the Treatment Center (79,105 and 85,105; one facing south, one with dir 4)
/// are live machines after the map load: their parts, their elevation on their tile, their mattress overlay and their lying angle.
/// </summary>
public class MetaStasisTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;

    public MetaStasisTests(MetaWorldFixture meta) => _meta = meta;

    [MetaFact]
    public void BothBedsAreLiveMachinesWithTheirPartsElevationAndMattress()
    {
        TgTestWorld.RunLevel(_meta.World, RUNLEVEL_GAME);
        for (int t = 0; t < 40; t++)
            _meta.World.Step();
        using (_meta.World.Enter())
        {
            var lyings = new List<float>();
            foreach (int x in new[] { 79, 85 })
            {
                var bed = Dm.locate<obj_machinery_stasis>(locate(x, 105, 1));
                Assert.NotNull(bed);
                Assert.Equal("stasis", bed.icon_state);
                // update_buckle_vars (stasis.dm:83) at Initialize: one bed faces south (90), the other has dir 4, east (270)
                Assert.Equal(Dm.truthy(Dm.bitand(bed.dir, NORTHEAST)) ? 270f : 90f, bed.buckle_lying);
                lyings.Add(bed.buckle_lying);
                Assert.True(HAS_TRAIT(locate(x, 105, 1), TRAIT_ELEVATED_TURF));
                Assert.NotNull(bed.mattress_on);
                Assert.Contains(Dm.each(bed.component_parts).ToList(), p => p is datum_stock_part_servo);
                Assert.Equal(300f, Dm.num(bed.idle_power_usage));
                Assert.Equal(1f, bed.stasis_enabled);
            }
            Assert.Equal(new[] { 90f, 270f }, lyings.OrderBy(l => l).ToArray());
        }
    }
}
