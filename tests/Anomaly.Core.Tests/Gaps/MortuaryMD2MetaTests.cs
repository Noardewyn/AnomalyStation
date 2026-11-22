using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package MD2 on the converted MetaStation: its morgues and its crematorium boot live with their trays inside, the morgues
/// with their own air, the crematorium listed for its button, and with no runtime from their files.
/// </summary>
public class MortuaryMD2MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MortuaryMD2MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    [MetaFact]
    public void Metas_morgues_and_crematorium_boot_live_with_their_trays()
    {
        using (_meta.World.Enter())
        {
            int morgues = 0, crematoriums = 0, inert = 0, trayless = 0, airless = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        if (thing is not obj_structure_bodycontainer container)
                            continue;
                        if (container is obj_structure_bodycontainer_morgue morgue)
                        {
                            morgues++;
                            if (morgue.internal_air == null)
                                airless++;
                        }
                        else
                            crematoriums++;
                        if (container.connected_value == null || !ReferenceEquals(container.connected_value.loc, container))
                            trayless++;
                        if (InertSeam.IsInert(thing))
                            inert++;
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => (e.ToString() ?? "").Contains("morgue", StringComparison.Ordinal)).ToList();
            _output.WriteLine($"Meta: {morgues} morgues ({airless} without air), {crematoriums} crematoriums, {trayless} without their tray inside, {inert} inert; runtimes {runtimes.Count}");
            Assert.True(morgues >= 10, "too few morgues");
            Assert.Equal(1, crematoriums);
            Assert.Equal(0, inert);
            Assert.Equal(0, trayless);
            Assert.Equal(0, airless);
            Assert.Equal(crematoriums, Dm.length(GLOB.crematoriums));
            Assert.Empty(runtimes);
        }
    }
}
