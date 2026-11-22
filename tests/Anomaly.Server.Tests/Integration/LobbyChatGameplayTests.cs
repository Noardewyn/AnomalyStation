using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10x slice A (D281): chat in tg's lobby through the real server on MetaStation with the lobby on. Alice is the host (tg's
/// !localhost! rank, an admin); Bob connects from another address. In the pregame both talk OOC from the chat window's input and
/// both read it, the admin's line in tg's adminooc span; Bob's Say from the lobby reaches no one and raises no runtime (a new player
/// is in nullspace); his Pray reaches the admin. After the round starts Bob, still in the lobby, talks OOC (tg's OOC_DURING_ROUND).
/// </summary>
public class LobbyChatGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static (RealHeadlessWorld.Player Player, List<string> Texts, ChatInputModel Input) Join(RealHeadlessWorld w, string name, string address)
    {
        LoopbackClientTransport transport = w.Network.CreateClient();
        transport.Address = address;
        var client = new GameClient(transport, "127.0.0.1", w.Server.Port, name);
        var texts = new List<string>();
        client.ServerTextReceived += t => texts.Add(t.Html);
        Assert.Equal(FeedPhase.Playing, w.Connect(client));
        RealHeadlessWorld.Player player = w.Adopt(name, client);
        var input = new ChatInputModel(message => player.Session.Say(message));
        client.SayModalReceived += m => input.OnServer(m);
        return (player, texts, input);
    }

    private static void Send(RealHeadlessWorld w, ChatInputModel input, string channel, string text)
    {
        input.SetChannel(channel);
        input.Focus();
        input.Input(text, w.World.Tick / (double)Second);
        Assert.True(input.Enter());
        w.Run(Second);
    }

    [MetaServerFact]
    public void The_lobby_talks_OOC_before_and_after_the_round_start_and_its_Say_reaches_no_one()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f, Lobby = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 45, TimeoutSteps = 8000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < Defines.GAME_STATE_PREGAME; i++)
            w.Run(1);
        (RealHeadlessWorld.Player alice, List<string> aliceTexts, ChatInputModel aliceInput) = Join(w, "Alice", null);
        (RealHeadlessWorld.Player bob, List<string> bobTexts, ChatInputModel bobInput) = Join(w, "Bob", "10.1.2.3");
        w.Run(4 * Second); // tgui_say.initialize sleeps 3 s, then sends the window's props
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.True(In(w, () => Dm.truthy(GlobalProcs.GLOB.ooc_allowed)));

        Send(w, bobInput, SayChannels.Ooc, "anyone here");
        Assert.Contains(aliceTexts, t => t.Contains("anyone here", StringComparison.Ordinal) && t.Contains("Bob", StringComparison.Ordinal));
        Assert.Contains(bobTexts, t => t.Contains("anyone here", StringComparison.Ordinal));

        Send(w, aliceInput, SayChannels.Ooc, "round soon");
        string adminLine = Assert.Single(bobTexts, t => t.Contains("round soon", StringComparison.Ordinal));
        Assert.Contains("adminooc", adminLine, StringComparison.Ordinal); // tg's span_adminooc for an admin with R_ADMIN
        output.WriteLine("the admin's OOC line: " + adminLine);

        int aliceSeen = aliceTexts.Count;
        Send(w, bobInput, SayChannels.Say, "hello lobby");
        Assert.DoesNotContain(aliceTexts.Skip(aliceSeen), t => t.Contains("hello lobby", StringComparison.Ordinal));

        Send(w, bobInput, SayChannels.Pray, "let me be captain");
        Assert.Contains(aliceTexts, t => t.Contains("let me be captain", StringComparison.Ordinal)); // the prayer reaches the admin

        In(w, () => GlobalProcs.SSticker.start_immediately = Dm.TRUE);
        for (int i = 0; i < 20 * Second && In(w, () => GlobalProcs.SSticker.current_state) != Defines.GAME_STATE_PLAYING; i++)
            w.Run(1);
        Assert.Equal(Defines.GAME_STATE_PLAYING, In(w, () => GlobalProcs.SSticker.current_state));
        Assert.True(In(w, () => Dm.truthy(GlobalProcs.GLOB.ooc_allowed))); // OOC_DURING_ROUND
        Send(w, bobInput, SayChannels.Ooc, "still in the lobby");
        Assert.Contains(aliceTexts, t => t.Contains("still in the lobby", StringComparison.Ordinal));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
