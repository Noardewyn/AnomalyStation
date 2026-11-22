using System.Reflection;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests;

/// <summary>
/// The M1 projects build and load together (the contracts step): the protocol, the transports with LiteNetLib, the bots,
/// the server host and the core. The packages' own tests go into Protocol/, Net/, Server/, Replication/ and Bots/
/// (docs/ai/specs/m1-ownership.md).
/// </summary>
public class ProjectsLoadTests
{
    [Fact]
    public void TheM1ProjectsLoadTogether()
    {
        Assert.True(ProtocolVersion.Current >= 28); // the version of this step or later (BuildInfoTests pins the current one)
        Assert.Equal("ANOM", System.Text.Encoding.ASCII.GetString(ProtocolVersion.Magic));
        // version 85: the Hello has its own cap (the login token); one UDP packet that is never fragmented (1232 bytes of payload on IPv6) still holds it
        Assert.True(Codec.MaxHelloBytes <= 1100);
        Assert.Equal(3, Channels.Count);
        Assert.Contains(typeof(LiteNetServerTransport).Assembly.GetReferencedAssemblies(), a => a.Name == "LiteNetLib");
        Assert.NotNull(Assembly.Load("LiteNetLib").GetType("LiteNetLib.NetManager"));
        Assert.Equal(BotBehaviour.Wander, new BotOptions().Behaviour);
        Assert.Equal(Anomaly.Core.Tg.TestRoom.DefaultSize, new ServerConfig().RoomWidth);
        Assert.NotNull(typeof(Anomaly.Core.Tg.TgState));
    }
}
