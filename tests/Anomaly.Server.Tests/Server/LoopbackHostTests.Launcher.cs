using System.Buffers.Text;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

// D335: the server's side of the launcher: the login token of a Hello and the status answer.
public partial class LoopbackHostTests
{
    private const string Address = "play.example.org:7777";

    /// <summary>The hub's key, and tokens made as the hub makes them (the launcher's repository, <c>GameToken.Create</c>).</summary>
    private sealed class Hub : IDisposable
    {
        public readonly ECDsa Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public readonly string KeyFile = Path.GetTempFileName();

        public Hub() => File.WriteAllText(KeyFile, Key.ExportSubjectPublicKeyInfoPem());

        public string Token(string name, string audience = Address, int lifetimeSeconds = 120, ECDsa signer = null, string algorithm = "ES256")
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string header = Base64Url.EncodeToString(Encoding.ASCII.GetBytes("{\"alg\":\"" + algorithm + "\",\"typ\":\"JWT\"}"));
            string payload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
            {
                ["sub"] = "account-of-" + name, ["name"] = name, ["aud"] = audience, ["iat"] = now, ["exp"] = now + lifetimeSeconds,
            }));
            string signed = header + "." + payload;
            return signed + "." + Base64Url.EncodeToString((signer ?? Key).SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256));
        }

        public ServerConfig Config() => new()
        {
            Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 1000,
            HubKeyFile = KeyFile, PublicAddresses = new[] { Address, "10.0.0.5:7777" },
        };

        public void Dispose()
        {
            Key.Dispose();
            File.Delete(KeyFile);
        }
    }

    private static DisconnectReason? Refusal(Rig rig, string name, SessionToken token = default, string login = null)
    {
        Player p = rig.Connect(name, token, login: login);
        for (int i = 0; i < 20 && p.Welcome == null && p.Gone == null; i++) rig.Tick();
        return p.Welcome != null ? null : p.Gone?.Reason;
    }

    [Fact]
    public void AHelloCarriesTheLoginTokenBesideTheReconnectToken()
    {
        string login = new string('t', Limits.MaxLoginTokenBytes);
        foreach (SessionToken token in new[] { SessionToken.None, new SessionToken(1, 2) })
        {
            var hello = new Hello(ProtocolVersion.Current, "Alice", token, login);
            byte[] bytes = Codec.Encode(hello);
            Assert.True(bytes.Length <= Codec.MaxHelloBytes);
            Assert.True(Codec.TryDecode(bytes, out Hello back, out ProtocolError error), error.ToString());
            Assert.Equal(hello, back);
        }
        Assert.True(Codec.TryDecode(Codec.Encode(new Hello(ProtocolVersion.Current, "Alice", SessionToken.None)), out Hello plain, out _));
        Assert.Null(plain.LoginToken);
        Assert.Throws<ProtocolException>(() => Codec.Encode(new Hello(ProtocolVersion.Current, "Alice", SessionToken.None, login + "t")));
    }

    [Fact]
    public void AServerWithTheHubsKeyTakesPlayersThroughTheLauncherOnly()
    {
        using var hub = new Hub();
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var rig = new Rig(config: hub.Config());
        Assert.Contains("players join through the launcher's login", rig.Out.ToString());

        // No login, or one that is not the hub's, not for this server, over, or of another algorithm: refused.
        Assert.Equal(DisconnectReason.LoginRequired, Refusal(rig, "alice"));
        Assert.Equal(DisconnectReason.LoginRequired, Refusal(rig, "alice", login: "not.a.token"));
        Assert.Equal(DisconnectReason.LoginRequired, Refusal(rig, "alice", login: hub.Token("alice", signer: stranger)));
        Assert.Equal(DisconnectReason.LoginRequired, Refusal(rig, "alice", login: hub.Token("alice", audience: "other.example.org:7777")));
        Assert.Equal(DisconnectReason.LoginRequired, Refusal(rig, "alice", login: hub.Token("alice", lifetimeSeconds: -1)));
        Assert.Equal(DisconnectReason.LoginRequired, Refusal(rig, "alice", login: hub.Token("alice", algorithm: "none")));
        string tampered = hub.Token("mallory");
        string[] parts = tampered.Split('.');
        parts[1] = hub.Token("alice").Split('.')[1]; // another payload under mallory's signature
        Assert.Equal(DisconnectReason.LoginRequired, Refusal(rig, "alice", login: string.Join('.', parts)));

        // The login names the player, whatever name the Hello carries; the server's other address is as good.
        string login = hub.Token("alice");
        Player alice = rig.Join("SomeoneElse", login: login);
        Assert.Contains(" alice at tick", rig.Out.ToString());
        Assert.DoesNotContain("SomeoneElse", rig.Out.ToString());
        rig.Join("bob", login: hub.Token("bob", audience: "10.0.0.5:7777"));

        // A login read off the wire joins nobody once its owner is in.
        Assert.Equal(DisconnectReason.LoginRequired, Refusal(rig, "alice", login: login));
        Assert.NotNull(alice.Welcome);
        Assert.Null(alice.Gone);

        // After a drop the session's reconnect token stands in for the login, which is over by then; a wrong one does not.
        Assert.Equal(DisconnectReason.BadToken, Refusal(rig, "alice", new SessionToken(1, 2)));
        Player back = rig.Join("alice", alice.Welcome.Value.Token, login: login);
        Assert.Equal(DisconnectReason.Replaced, alice.Gone?.Reason);

        // The account logs in from another computer (no reconnect token there) and takes its character over.
        Player elsewhere = rig.Join("alice", login: hub.Token("alice"));
        rig.Tick(3);
        Assert.Equal(DisconnectReason.Replaced, back.Gone?.Reason);
        Assert.Null(elsewhere.Gone);
        Assert.Equal(0, rig.Host.World.RuntimeErrorCount);
    }

    [Fact]
    public void TheGamesOwnClientSendsTheLaunchersLogin()
    {
        using var hub = new Hub();
        using var rig = new Rig(config: hub.Config());
        using var client = new Anomaly.Client.Shared.GameClient(rig.Network.CreateClient(), "127.0.0.1", rig.Server.Port, "TypedName", default, hub.Token("alice"));
        client.Connect();
        for (int i = 0; i < 40 && client.Phase != Anomaly.Client.Shared.FeedPhase.Playing; i++)
        {
            rig.Tick();
            client.Poll(i * 0.05);
        }
        Assert.Equal(Anomaly.Client.Shared.FeedPhase.Playing, client.Phase);
        Assert.Contains(" alice at tick", rig.Out.ToString());

        // Without the launcher the same client is told where to go.
        using var typed = new Anomaly.Client.Shared.GameClient(rig.Network.CreateClient(), "127.0.0.1", rig.Server.Port, "TypedName");
        typed.Connect();
        for (int i = 0; i < 40 && typed.Phase != Anomaly.Client.Shared.FeedPhase.Closed; i++)
        {
            rig.Tick();
            typed.Poll(i * 0.05);
        }
        Assert.Equal(DisconnectReason.LoginRequired, typed.CloseCode);
        Assert.Contains("launcher", Anomaly.Client.Shared.JoinAttempt.Explain(typed.CloseReason, typed.CloseCode, "TypedName"));
    }

    [Fact]
    public void AServerWithoutTheHubsKeyTakesTheTypedNameAndDoesNotReadALogin()
    {
        using var hub = new Hub();
        using var rig = new Rig();
        rig.Join("Bob", login: hub.Token("alice"));
        Assert.Contains(" Bob at tick", rig.Out.ToString());
        Assert.DoesNotContain("alice", rig.Out.ToString());
    }

    [Fact]
    public void AKeyThatCannotBeReadStopsTheStart()
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, HubKeyFile = Path.Combine(Path.GetTempPath(), "no-such-hub-key.pem"), PublicAddresses = new[] { Address } };
        Assert.ThrowsAny<IOException>(() => new Rig(config: config));
    }

    [Fact]
    public async Task TheStatusAnswerNamesTheServerItsPlayersTheRoundAndTheClientBuild()
    {
        string build = Path.GetTempFileName();
        File.WriteAllText(build, "{\"version\":\"1.2.3\",\"downloads\":{\"win-x64\":{\"url\":\"https://example.org/c.zip\",\"sha256\":\"00\",\"size\":1,\"executable\":\"win-x64/AnomalyStation.exe\"}}}");
        try
        {
            var config = new ServerConfig
            {
                Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 1000, MaxPlayers = 40,
                Status = true, StatusPort = 0, ServerName = "Станция \"Тест\"", ServerDescription = "Two lines.\nThe second.", ServerMode = "Extended", ClientBuildFile = build,
            };
            using var rig = new Rig(config: config);
            rig.Join("Alice");
            rig.Tick(40);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            string url = "http://127.0.0.1:" + rig.Host.StatusPort;

            using JsonDocument status = JsonDocument.Parse(await http.GetStringAsync(url + "/status"));
            JsonElement root = status.RootElement;
            Assert.Equal("Станция \"Тест\"", root.GetProperty("name").GetString());
            Assert.Equal("Two lines.\nThe second.", root.GetProperty("description").GetString());
            Assert.Equal("Extended", root.GetProperty("mode").GetString());
            Assert.Equal(JsonValueKind.String, root.GetProperty("map").ValueKind); // the test room is no tg map: its name may be empty
            Assert.Equal(1, root.GetProperty("players").GetInt32());
            Assert.Equal(40, root.GetProperty("maxPlayers").GetInt32());
            Assert.InRange(root.GetProperty("roundSeconds").GetInt32(), 0, 60); // no lobby here: the round runs from the start
            Assert.Equal("1.2.3", root.GetProperty("build").GetProperty("version").GetString());

            // Anything else is a 404, and a caller that sends rubbish or nothing does not stop the next one.
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(url + "/statusx")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(url + "/")).StatusCode);
            using (var raw = new TcpClient("127.0.0.1", rig.Host.StatusPort))
                raw.GetStream().Write(new byte[5000]);
            using (var silent = new TcpClient("127.0.0.1", rig.Host.StatusPort))
                Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(url + "/status?x=1")).StatusCode);
        }
        finally
        {
            File.Delete(build);
        }
    }

    [Fact]
    public void AServerGivesNoStatusUnlessAsked()
    {
        using var rig = new Rig();
        Assert.Equal(0, rig.Host.StatusPort);
    }

    [Fact]
    public void TheLauncherKeysOfTheConfig()
    {
        ServerConfig Parse(string file, out string error, params string[] args)
            => ServerConfig.Parse(new[] { "--ticks", "1" }.Concat(args).ToArray(), name => file, out error, () => 1);

        ServerConfig config = Parse("SERVER_NAME My Station\nSERVER_DESCRIPTION Come in.\nSERVER_MODE Roleplay\nSTATUS\nCLIENT_BUILD config/client-build.json\nHUB_KEY config/hub-key.pem\nPUBLIC_ADDRESS Play.Example.org:7777, 10.0.0.5:7777\n", out string error);
        Assert.Null(error);
        Assert.Equal(("My Station", "Come in.", "Roleplay", true), (config.ServerName, config.ServerDescription, config.ServerMode, config.Status));
        Assert.Equal(("config/client-build.json", "config/hub-key.pem"), (config.ClientBuildFile, config.HubKeyFile));
        Assert.Equal(new[] { "play.example.org:7777", "10.0.0.5:7777" }, config.PublicAddresses);

        ServerConfig plain = Parse("", out error);
        Assert.Null(error);
        Assert.False(plain.Status);
        Assert.Null(plain.HubKeyFile);
        Assert.Empty(plain.PublicAddresses);

        // A login is for one address: a server that takes logins must know its own.
        Assert.Null(Parse("", out error, "--hub-key", "hub-key.pem"));
        Assert.Contains("--public-address", error);
        Assert.Null(Parse("", out error, "--public-address", "no-port"));
        Assert.Contains("host:port", error);
        Assert.Null(Parse("", out error, "--public-address", "http://x:1"));
        Assert.NotNull(Parse("", out error, "--hub-key", "hub-key.pem", "--public-address", "x:1"));
    }
}
