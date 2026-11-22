using Anomaly.Net;

namespace Anomaly.Server.Tests.Integration;

/// <summary>tg's admin Change Map (<c>changemap MAP</c>): the next round's world is built from MAP; an unknown map changes nothing.</summary>
public class ChangeMapTests
{
    [Fact]
    public void ChangeMapLoadsTheChosenMapAtTheNextRound()
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, DevCommands = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(10);
        Assert.False(w.Host.MapLoaded);

        alice.Session.DevCommand("changemap no_such_map");
        w.Run(4);
        Assert.Null(w.Host.MapName);

        alice.Session.DevCommand("changemap MULTIZ_DEBUG");
        w.Run(4);
        Assert.Equal("multiz_debug", w.Host.MapName);

        alice.Session.DevCommand("reboot now");
        for (int i = 0; i < 2000 && !(w.Host.Restarts == 1 && !w.Host.BuildingNextWorld); i++)
            w.Run(1);
        Assert.Equal(1, w.Host.Restarts);
        Assert.False(w.Host.BuildingNextWorld, w.Out + "\n" + w.Err);
        Assert.True(w.Host.MapLoaded);
    }
}
