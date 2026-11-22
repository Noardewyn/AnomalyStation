using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.M1Seams;

/// <summary>
/// The engine-to-host half of the client seam (M1 contracts step): a per-world <see cref="IClientHost"/> that the
/// <c>/client</c> engine part (M1 P1) tells when a client is deleted and when its mob changes. Clients and mobs here are
/// constructor-only instances: the seam only passes them on.
/// </summary>
public class ClientHostSeamTests
{
    private sealed class RecordingHost : IClientHost
    {
        public List<string> Calls { get; } = new();

        public void ClientDeleted(client player) => Calls.Add("deleted " + Name(player));

        public void ClientMobChanged(client player, mob old_mob, mob new_mob) => Calls.Add("mob " + Name(player) + " " + Name(old_mob) + " -> " + Name(new_mob));

        private static string Name(object o) => o == null ? "null" : o is client c ? "client:" + c.ckey : "mob:" + ((mob)o).name;
    }

    private static client Client(string ckey) => new client { ckey = ckey };

    private static mob Mob(string name) => new mob { name = name };

    [Fact]
    public void TheHostIsPerWorldAndAbsentByDefault()
    {
        var world = new World();
        var other = new World();
        Assert.Null(ClientSeam.HostOf(world));
        var host = new RecordingHost();
        ClientSeam.SetHost(world, host);
        Assert.Same(host, ClientSeam.HostOf(world));
        Assert.Null(ClientSeam.HostOf(other));
        ClientSeam.SetHost(world, null);
        Assert.Null(ClientSeam.HostOf(world));
    }

    [Fact]
    public void TheEnginePartsCallsReachTheAmbientWorldsHostInOrder()
    {
        var world = new World();
        var host = new RecordingHost();
        ClientSeam.SetHost(world, host);
        client player = Client("alice");
        mob avatar = Mob("Alice");
        mob ghost = Mob("ghost");
        using (world.Enter())
        {
            ClientSeam.NotifyClientMobChanged(player, null, avatar);
            ClientSeam.NotifyClientMobChanged(player, avatar, ghost);
            ClientSeam.NotifyClientDeleted(player);
        }
        Assert.Equal(new[]
        {
            "mob client:alice null -> mob:Alice",
            "mob client:alice mob:Alice -> mob:ghost",
            "deleted client:alice",
        }, host.Calls);
    }

    [Fact]
    public void WithoutAHostOrOutsideAWorldTheCallsDoNothing()
    {
        var world = new World();
        var other = new World();
        var host = new RecordingHost();
        ClientSeam.SetHost(world, host);
        client player = Client("bob");
        ClientSeam.NotifyClientDeleted(player); // no ambient world
        using (other.Enter())
        {
            ClientSeam.NotifyClientDeleted(player); // a world without a host
            ClientSeam.NotifyClientMobChanged(player, null, Mob("Bob"));
        }
        Assert.Empty(host.Calls);
    }

    [Fact]
    public void InstallingAHostDoesNotChangeTheStateHash()
    {
        var plain = new World(5);
        var hosted = new World(5);
        ClientSeam.SetHost(hosted, new RecordingHost());
        plain.Step(3);
        hosted.Step(3);
        Assert.Equal(plain.ComputeStateHash(), hosted.ComputeStateHash());
    }
}
