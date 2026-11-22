using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>The dev menu's player markers: the players of D315's trackers only, keyed apart when they share a name, with their distance.</summary>
public class PlayerMarkerListTests
{
    [Fact]
    public void OnlyPlayersAreMarkedWithTheirNameAndDistance()
    {
        var mobs = new MinimapMobsMessage { Z = 2 };
        mobs.Mobs.Add(new MinimapMob(13, 14, true, "Alice"));
        mobs.Mobs.Add(new MinimapMob(11, 10, false, "the mouse"));
        mobs.Mobs.Add(new MinimapMob(11, 10, true, "Unknown"));
        mobs.Mobs.Add(new MinimapMob(20, 10, true, "Unknown"));
        mobs.Mobs.Add(new MinimapMob(10, 10, true, ""));

        List<PlayerMarkerList.Mark> marks = PlayerMarkerList.Of(mobs, 10f, 10f);
        Assert.Equal(new[] { "Alice#0", "Unknown#0", "Unknown#1", "a player#0" }, marks.Select(m => m.Key));
        Assert.Equal("Alice\n5 tiles", marks[0].Text);
        Assert.Equal("Unknown\n1 tile", marks[1].Text);
        Assert.Equal((20, 10), (marks[2].X, marks[2].Y));
        Assert.Empty(PlayerMarkerList.Of(null, 0f, 0f));

        var dev = new DevMenuOptions();
        Assert.True(dev.TrySet("player_markers", "true", out _));
        Assert.Contains("player_markers=true", dev.ToText());
    }
}
