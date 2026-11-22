using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>M3 T2: SSmapping.is_planetary (mapping.dm:896) is the map config's `planetary` (map_config.dm), which tg's map tests read.</summary>
public class IsPlanetaryTests
{
    [Fact]
    public void is_planetary_returns_the_current_map_configs_planetary()
    {
        World world = T2Maps.Boot("areas");
        using (world.Enter())
        {
            Assert.False(Dm.truthy(SSmapping.is_planetary()));
            SSmapping.current_map.planetary = 1f;
            Assert.True(Dm.truthy(SSmapping.is_planetary()));
            Assert.True(Dm.eq(SSmapping.is_planetary(), SSmapping.current_map.planetary));
        }
    }
}
