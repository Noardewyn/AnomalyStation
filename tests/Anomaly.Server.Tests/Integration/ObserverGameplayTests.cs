using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10x slice B (D281): observing from tg's lobby and dead chat through the real server on MetaStation with the lobby on. Alice presses
/// the lobby's Observe and says Yes in tg's alert: she is a ghost at Meta's observer landmark, started as an observer, and dead chat
/// tells that she has observed. Bob readies and the round starts; Carol stays in the lobby. Alice's speech is dead chat: neither the
/// living Bob nor the lobby's Carol reads it. Bob dies: Alice reads tg's death notice, and Bob, dead in his body, reads her dead chat.
/// After the round's end the lobby reads dead chat too.
/// </summary>
public class ObserverGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

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

    private static void Send(RealHeadlessWorld w, ChatInputModel input, string text)
    {
        input.SetChannel(SayChannels.Say);
        input.Focus();
        input.Input(text, w.World.Tick / (double)Second);
        Assert.True(input.Enter());
        w.Run(Second);
    }

    private static void Act(RealHeadlessWorld.Player p, string @interface, string action, params (string Key, UiValue Value)[] parameters)
    {
        UiWindowState window = Assert.Single(p.Session.Replica.UiWindows.Values, v => v.Interface == @interface);
        Assert.True(p.Session.UiAct(window.Window, action, parameters.Length == 0 ? null : UiValue.ObjectOf(parameters.Select(x => new KeyValuePair<string, UiValue>(x.Key, x.Value)).ToArray())));
    }

    private static bool Has(List<string> texts, string part) => texts.Any(t => t.Contains(part, StringComparison.Ordinal));

    [MetaServerFact]
    public void Alice_observes_from_the_lobby_and_dead_chat_reaches_the_dead_and_after_the_end_the_lobby()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f, Lobby = true, OpenJobs = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 46, TimeoutSteps = 12000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < Defines.GAME_STATE_PREGAME; i++)
            w.Run(1);
        (RealHeadlessWorld.Player alice, List<string> aliceTexts, ChatInputModel aliceInput) = Join(w, "Alice", null);
        (RealHeadlessWorld.Player bob, List<string> bobTexts, ChatInputModel bobInput) = Join(w, "Bob", "10.1.2.3");
        (RealHeadlessWorld.Player carol, List<string> carolTexts, _) = Join(w, "Carol", "10.1.2.4");
        w.Run(4 * Second); // tgui_say.initialize sleeps 3 s, then sends the window's props
        int bootRuntimes = w.World.RuntimeErrorCount;

        // 1. Alice observes: tg's alert, then the ghost at the observer landmark.
        Act(alice, Limits.LobbyUiInterface, "observe");
        w.Run(Second);
        UiWindowState alert = Assert.Single(alice.Session.Replica.UiWindows.Values, v => v.Interface == "AlertModal");
        Assert.True(alice.Session.UiAct(alert.Window, "choose", UiValue.ObjectOf(new[] { new KeyValuePair<string, UiValue>("choice", UiValue.Of("Yes")) })));
        w.Run(2 * Second);
        var ghost = Assert.IsType<mob_dead_observer>(MobOf(w, alice));
        Assert.True(In(w, () => Dm.truthy(ghost.started_as_observer)));
        // set_ghost_appearance: the character's hair and beard (observer.dm:837)
        Assert.Equal(In(w, () => Dm.str(ghost.client.prefs.read_preference(typeof(datum_preference_choiced_hairstyle)))), In(w, () => Dm.str(ghost.hairstyle)));
        Assert.Equal(In(w, () => Dm.str(ghost.client.prefs.read_preference(typeof(datum_preference_choiced_facial_hairstyle)))), In(w, () => Dm.str(ghost.facial_hairstyle)));
        Assert.True(In(w, () => Dm.locate<obj_effect_landmark_observer_start>(GlobalProcs.GLOB.landmarks_list) is { } start && GlobalProcs.get_turf(ghost) == start.loc));
        Assert.True(Has(aliceTexts, "has observed."));
        Assert.DoesNotContain(alice.Session.Replica.UiWindows.Values, v => v.Interface == Limits.LobbyUiInterface);

        // 2. Bob readies and the round starts; Carol stays in the lobby.
        Act(bob, Limits.LobbyUiInterface, "ready_toggle");
        w.Run(Second);
        In(w, () => GlobalProcs.SSticker.start_immediately = Dm.TRUE);
        for (int i = 0; i < 20 * Second && In(w, () => GlobalProcs.SSticker.current_state) != Defines.GAME_STATE_PLAYING; i++)
            w.Run(1);
        w.Run(2 * Second);
        var bobMob = Assert.IsAssignableFrom<mob_living_carbon_human>(MobOf(w, bob));

        // 3. Alice's speech is dead chat: the living and the lobby do not read it.
        Send(w, aliceInput, "boo");
        Assert.True(Has(aliceTexts, "DEAD:"));
        Assert.True(Has(aliceTexts, "boo"));
        Assert.False(Has(bobTexts, "boo"));
        Assert.False(Has(carolTexts, "boo"));

        // 4. Bob dies: tg's death notice reaches Alice; dead in his body, he reads dead chat.
        In(w, () => Waiting.NoSleep(bobMob.death()));
        w.Run(Second);
        Assert.True(Has(aliceTexts, "has died at"));
        Send(w, aliceInput, "welcome to the other side");
        Assert.True(Has(bobTexts, "welcome to the other side"));
        Assert.False(Has(carolTexts, "welcome to the other side"));

        // 5. After the round's end the lobby reads dead chat.
        In(w, () => GlobalProcs.SSticker.force_ending = Dm.TRUE);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) != Defines.GAME_STATE_FINISHED; i++)
            w.Run(1);
        Assert.Equal(Defines.GAME_STATE_FINISHED, In(w, () => GlobalProcs.SSticker.current_state));
        Send(w, aliceInput, "good game");
        Assert.True(Has(carolTexts, "good game"));
        output.WriteLine("Carol read: " + carolTexts.Last(t => t.Contains("good game", StringComparison.Ordinal)));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
