using Anomaly.Net;

namespace Anomaly.Server.Tests.Integration;

/// <summary>The owner (2026-11-02): <c>reboot now</c> restarts the world at once (tg's Hard Restart); the regular reboot waits a second at least.</summary>
public class RebootNowTests
{
    [Fact]
    public void RebootNowRestartsTheWorldWithNoCountdown()
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, DevCommands = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 }, config, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(10);
        alice.Session.DevCommand("reboot now");
        w.Run(8);
        Assert.Equal(1, w.Host.Restarts);
    }
}
