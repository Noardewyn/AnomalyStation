using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Integration;

namespace Anomaly.Server.Tests.Client.Net;

/// <summary>
/// M3 R4 (scope 2j; the owner: "I cannot come back to the game with the same character once the last session ended"): the
/// server takes a known name back only with its token, and the client now keeps the token across launches
/// (<see cref="ReconnectTokens"/>, one per server and name) and sends it again (<see cref="JoinAttempt"/>, the connect
/// screen's join). Each test is a new launch of the client: a new <see cref="JoinAttempt"/> over the same token file.
/// </summary>
public sealed class ReconnectTokenTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "anomaly-r4-tokens-" + Guid.NewGuid().ToString("N"));

    private string StorePath => Path.Combine(_dir, "reconnect-tokens.txt");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static RealHeadlessWorld NewWorld() => new(new LoopbackOptions { Seed = 5, TimeoutSteps = 40 });

    // A launch of the client: a new token store read from the file, a connect screen join, the game scene takes the client.
    private (JoinAttempt Join, FeedPhase Phase) Launch(RealHeadlessWorld world, string name, List<LoopbackClientTransport> transports = null)
    {
        var join = new JoinAttempt(() =>
        {
            LoopbackClientTransport t = world.Network.CreateClient();
            transports?.Add(t);
            return t;
        }, new ReconnectTokens(StorePath), "127.0.0.1", world.Server.Port, name);
        return (join, world.Join(join));
    }

    [Fact]
    public void After_a_crash_the_relaunched_client_takes_its_character_back()
    {
        using var world = NewWorld();
        var transports = new List<LoopbackClientTransport>();
        (JoinAttempt first, FeedPhase phase) = Launch(world, "Crasher", transports);
        Assert.Equal(FeedPhase.Playing, phase);
        Assert.False(first.UsedKeptToken);
        RealHeadlessWorld.Player p = world.Adopt("Crasher", first.Client);
        long body = world.Host.AvatarOf("Crasher");
        Assert.Equal(body, p.Client.OwnEntity);
        Assert.Equal(first.Client.Token, new ReconnectTokens(StorePath).Get("127.0.0.1:" + world.Server.Port, "Crasher"));

        // The crash: no goodbye; the server times the session out and the body stays (SSD).
        transports[0].Drop();
        world.Crash(p);
        world.Run(60);
        Assert.Null(world.Host.LiveSession("Crasher"));

        (JoinAttempt second, phase) = Launch(world, "Crasher");
        Assert.Equal(FeedPhase.Playing, phase);
        Assert.True(second.UsedKeptToken);
        Assert.False(second.Retried);
        Assert.Equal(body, world.Host.AvatarOf("Crasher"));
        Assert.Equal(body, second.Client.OwnEntity);
    }

    [Fact]
    public void A_relaunch_right_after_a_crash_replaces_the_session_that_has_not_timed_out_yet()
    {
        using var world = NewWorld();
        var transports = new List<LoopbackClientTransport>();
        (JoinAttempt first, _) = Launch(world, "Quick", transports);
        RealHeadlessWorld.Player p = world.Adopt("Quick", first.Client);
        long body = world.Host.AvatarOf("Quick");
        transports[0].Drop();
        world.Crash(p);

        (JoinAttempt second, FeedPhase phase) = Launch(world, "Quick");
        Assert.Equal(FeedPhase.Playing, phase);
        Assert.Equal(body, second.Client.OwnEntity);
        Assert.Equal(SessionState.Joined, world.Host.LiveSession("Quick").State);
    }

    [Fact]
    public void After_a_clean_quit_the_relaunched_client_takes_its_character_back()
    {
        using var world = NewWorld();
        (JoinAttempt first, _) = Launch(world, "Quitter");
        RealHeadlessWorld.Player p = world.Adopt("Quitter", first.Client);
        long body = world.Host.AvatarOf("Quitter");
        world.Leave(p);
        Assert.Null(world.Host.LiveSession("Quitter"));

        (JoinAttempt second, FeedPhase phase) = Launch(world, "Quitter");
        Assert.Equal(FeedPhase.Playing, phase);
        Assert.Equal(body, second.Client.OwnEntity);
    }

    [Fact]
    public void Two_launches_at_once_the_newer_takes_the_session_over_and_the_older_is_told_why()
    {
        using var world = NewWorld();
        (JoinAttempt first, _) = Launch(world, "Twin");
        RealHeadlessWorld.Player p = world.Adopt("Twin", first.Client);
        long body = world.Host.AvatarOf("Twin");

        (JoinAttempt second, FeedPhase phase) = Launch(world, "Twin");
        Assert.Equal(FeedPhase.Playing, phase);
        Assert.Equal(body, second.Client.OwnEntity);
        world.Run(5);
        Assert.Equal(FeedPhase.Closed, p.Client.Phase);
        Assert.Equal(DisconnectReason.Replaced, p.Client.CloseCode);
        Assert.Contains("connected again from elsewhere", p.Client.CloseReason);
    }

    [Fact]
    public void A_wrong_kept_token_is_forgotten_and_the_refusal_says_what_to_do()
    {
        using var world = NewWorld();
        (JoinAttempt first, _) = Launch(world, "Owner");
        world.Adopt("Owner", first.Client);

        // Another computer (another token file) has a wrong token for the name: refused, forgotten, tried once without it,
        // refused as NameInUse with the advice.
        string server = "127.0.0.1:" + world.Server.Port;
        string other = Path.Combine(_dir, "other.txt");
        new ReconnectTokens(other).Set(server, "Owner", new SessionToken(1, 2));
        var join = new JoinAttempt(() => world.Network.CreateClient(), new ReconnectTokens(other), "127.0.0.1", world.Server.Port, "Owner");
        Assert.Equal(FeedPhase.Closed, world.Join(join));
        Assert.True(join.Retried);
        Assert.False(join.UsedKeptToken);
        Assert.Equal(DisconnectReason.NameInUse, join.Client.CloseCode);
        Assert.Contains("already has a character called \"Owner\"", join.Advice);
        Assert.Contains("Choose another name", join.Advice);
        Assert.True(new ReconnectTokens(other).Get(server, "Owner").IsNone);
        Assert.Equal(SessionState.Joined, world.Host.LiveSession("Owner").State); // the owner keeps playing
    }

    [Fact]
    public void A_token_kept_from_a_server_that_restarted_is_dropped_and_the_name_joins_anew()
    {
        string server;
        using (var world = NewWorld())
        {
            (JoinAttempt first, _) = Launch(world, "Phoenix");
            world.Adopt("Phoenix", first.Client);
            server = "127.0.0.1:" + world.Server.Port;
        }
        using var restarted = NewWorld();
        Assert.Equal(server, "127.0.0.1:" + restarted.Server.Port); // the loopback's port repeats, as a restarted server's does
        (JoinAttempt join, FeedPhase phase) = Launch(restarted, "Phoenix");
        Assert.Equal(FeedPhase.Playing, phase);
        Assert.True(join.Retried);
        Assert.Equal(restarted.Host.AvatarOf("Phoenix"), join.Client.OwnEntity);
        Assert.Equal(join.Client.Token, new ReconnectTokens(StorePath).Get(server, "Phoenix"));
    }

    [Fact]
    public void The_token_file_keys_by_server_and_ckey_and_survives_junk()
    {
        Directory.CreateDirectory(_dir);
        var store = new ReconnectTokens(StorePath);
        store.Set("Host:7777", "John Smith", new SessionToken(0xabc, 0xdef));
        store.Set("host:7778", "John Smith", new SessionToken(5, 6));
        File.AppendAllText(StorePath, "junk line\nhost:1|x\tzz\t00\n\n");
        var again = new ReconnectTokens(StorePath);
        Assert.Equal(new SessionToken(0xabc, 0xdef), again.Get("host:7777", "johnsmith"));
        Assert.Equal(new SessionToken(5, 6), again.Get("HOST:7778", "John  Smith"));
        Assert.True(again.Get("host:7779", "John Smith").IsNone);
        Assert.Equal(2, again.Count);
        for (int i = 0; i < ReconnectTokens.MaxEntries + 5; i++)
            again.Set("h:" + i, "n", new SessionToken((ulong)i + 1, 1));
        Assert.Equal(ReconnectTokens.MaxEntries, new ReconnectTokens(StorePath).Count);
        Assert.Equal(0, new ReconnectTokens(null).Count);
    }
}
