using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.Tg.PlayTest;

/// <summary>
/// The owner's play test (2026-10-09) on Meta's boot: a cable draws its links to its neighbours (tg's connect_cable and
/// get_dir_string, not the map's four-way node).
/// </summary>
public class LinkPicturesTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;

    public LinkPicturesTests(MetaWorldFixture meta) => _meta = meta;

    [MetaFact]
    public void CablesDrawTheirLinks()
    {
        using (_meta.World.Enter())
        {
            int cables = 0, linked = 0;
            foreach (obj_structure_cable cable in Dm.each<obj_structure_cable>())
            {
                if (cable is obj_structure_cable_multilayer)
                    continue;
                cables++;
                if (cable.linked_dirs != NONE)
                    linked++;
                Assert.Equal(Dm.str(cable.get_dir_string(cable.linked_dirs, Dm.str(cable.icon_state).EndsWith("-node") ? 1f : 0f)), Dm.str(cable.icon_state));
            }
            Assert.True(cables > 1000, $"Meta's cables: {cables}");
            Assert.True(linked > cables * 0.95, $"linked {linked} of {cables}");
        }
    }
}
