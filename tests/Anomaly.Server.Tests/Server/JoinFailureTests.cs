using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>A Join whose client/New cannot even start (the client layer throws) refuses the session instead of leaving it waiting.</summary>
public class JoinFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AJoinThatThrowsRefusesTheSession(bool inKeyMob)
    {
        using var server = TestServer.Start();
        if (inKeyMob) server.Clients.ThrowOnKeyMob = true;
        else server.Clients.ThrowOnConnect = true;
        FakeRemote alice = server.Connect("Alice");
        server.Tick(2);
        Assert.Equal(DisconnectReason.Refused, alice.Closed?.Reason);
        Assert.Contains("runtime at tick", server.Err.ToString());
        server.Tick();
        Assert.Equal(SessionState.Closed, server.Host.SessionOf(1).State);
        Assert.Null(alice.Welcome);
    }
}
