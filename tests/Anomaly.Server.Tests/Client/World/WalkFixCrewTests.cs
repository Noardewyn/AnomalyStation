using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>M3 R2: the fake feed's crew is tg's humans (the bots were black capsules: icon_state "human", which human.dmi lacks).</summary>
public sealed class WalkFixCrewTests
{
    [Fact]
    public void TheFakeFeedsBotsAreTgsHumansNotAMissingState()
    {
        var feed = new FakeFeed("Tester", seed: 1, bots: 4, oneWayLatencyMs: 0, jitterMs: 0);
        feed.Poll(0);
        feed.Poll(0.1);
        int crew = 0;
        foreach (ReplicaEntity e in feed.Replica.Entities.Where(e => e.TypePath == "/mob/living/carbon/human"))
        {
            crew++;
            Assert.True(feed.Replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry entry));
            // tg's human.dmi has no state "human": the root is "human_basic" with the limbs, eyes and a jumpsuit as overlays (the showcase's composition).
            Assert.Equal("human_basic", feed.Replica.ResolveString(entry.IconState));
            Assert.Equal("icons/mob/human/human.dmi", feed.Replica.ResolveResource(entry.Icon));
            Assert.True(entry.Overlays.Count >= 10, "limbs, eyes and a jumpsuit: " + entry.Overlays.Count);
            Assert.NotEqual(0, entry.AppearanceFlags & AppearanceFlag.KeepTogether);
            foreach (TableRef overlay in entry.Overlays)
            {
                Assert.True(feed.Replica.TryGetAppearance(overlay, out AppearanceEntry part));
                Assert.NotNull(feed.Replica.ResolveString(part.IconState));
            }
        }
        Assert.True(crew >= 5, "the player and four bots: " + crew);
        // The crew differs: not one picture for everybody.
        var pictures = feed.Replica.Entities.Where(e => e.TypePath == "/mob/living/carbon/human").Select(e => e.State.Appearance).Distinct().Count();
        Assert.True(pictures >= 3, "distinct pictures: " + pictures);
    }
}
