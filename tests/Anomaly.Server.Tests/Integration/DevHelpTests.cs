using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Integration;

/// <summary>The owner's report (2026-10-09): "help" in a player's dev console was answered "unknown dev command".</summary>
public class DevHelpTests
{
    [Fact]
    public void Help_from_a_players_console_lists_the_dev_commands()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 400 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        alice.Session.DevCommand("help");
        w.Run(5);
        Assert.Contains(texts, t => t.Contains("give WHO TYPE", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("unknown dev command", StringComparison.Ordinal));
    }
}
