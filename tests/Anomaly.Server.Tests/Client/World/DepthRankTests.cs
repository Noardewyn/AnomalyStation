using Anomaly.Client.Shared.World;
using Xunit;

namespace Anomaly.Server.Tests.Client.World;

public class DepthRankTests
{
    [Fact]
    public void A_mob_ranks_over_every_item_on_its_tile()
    {
        for (long id = 1; id < 500; id++)
            Assert.True(DepthRank.Of(4f, 1) > DepthRank.Of(3f, id) && DepthRank.Of(4f, id) > DepthRank.Of(3.1f, 1));
    }

    [Fact]
    public void Things_of_one_layer_have_distinct_stable_ranks_within_a_tenth_of_a_layer()
    {
        var seen = new HashSet<float>();
        for (long id = 1; id < 64; id++)
        {
            float rank = DepthRank.Of(3f, id);
            Assert.Equal(rank, DepthRank.Of(3f, id));
            Assert.InRange(rank, DepthRank.Of(3f, 0), DepthRank.Of(3f, 0) + 0.1f * DepthRank.LayerStep);
            Assert.True(seen.Add(rank));
        }
    }

    [Fact]
    public void Layers_out_of_range_are_clamped()
    {
        Assert.True(DepthRank.Of(float.PositiveInfinity, 7) <= 4f * DepthRank.LayerStep + DepthRank.TieSpan);
        Assert.InRange(DepthRank.Of(-32767f, 7), 0f, DepthRank.TieSpan);
        Assert.InRange(DepthRank.Of(float.NaN, 7), 0f, DepthRank.TieSpan);
    }
}
