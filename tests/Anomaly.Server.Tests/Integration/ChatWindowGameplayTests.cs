using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M9 slice C through the real server on MetaStation with headless clients (D259): Alice talks from the big chat window's input row
/// (<see cref="ChatInputModel"/>) and the others' windows sort what they get into tg's pages (<see cref="ChatLog"/>). Alice, Dave, Bob and
/// Carol stand in a row (Dave 1 tile from Alice, Bob 3, Carol 10). Her said line lands on Bob's Local page; her whisper reaches Dave in full and
/// Bob as "something" (tg leaves that line unsorted: on All, not Local); her shout reaches Carol; her OOC line lands on the OOC page and not on Local; with Bob reading OOC, a said line is
/// counted unread on his All and Local tabs; Dave punching Bob lands on Alice's Combat page.
/// </summary>
public class ChatWindowGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    // An open floor with normal air (the speech test's row).
    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.truthy(t.opacity)
        && !Dm.each<atom>(t.contents).Any(a => Dm.truthy(a.density) || Dm.truthy(a.opacity))
        && Waiting.NoSleep(t.return_air()) is datum_gas_mixture air && Dm.num(air.return_pressure()) > 90;

    private static turf Row()
    {
        for (int y = 1; y <= Dm.num(world.maxy); y++)
        {
            int run = 0;
            for (int x = 1; x <= Dm.num(world.maxx); x++)
            {
                run = Clear(locate(x, y, 1) as turf) ? run + 1 : 0;
                if (run == 15)
                    return locate(x - 14, y, 1) as turf;
            }
        }
        return null;
    }

    private static turf East(turf from, int tiles) => locate(from.x + tiles, from.y, from.z) as turf;

    private static bool Shows(ChatLog log, string text) => log.Visible.Any(m => m.Plain.Contains(text, StringComparison.Ordinal));

    [MetaServerFact]
    public void Alice_talks_from_the_chat_window_and_the_pages_sort_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 95, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        RealHeadlessWorld.Player dave = w.Join("Dave");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var daveMob = (mob_living_carbon_human)w.MobOf(dave);
        var logs = new Dictionary<string, ChatLog> { ["Alice"] = new(), ["Bob"] = new(), ["Carol"] = new(), ["Dave"] = new() };
        double clock = 0;
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob, carol, dave })
        {
            ChatLog log = logs[p.Name];
            p.Client.ServerTextReceived += t =>
            {
                if (t.Type != "balloon")
                    log.Add(t.Type, t.Html, clock += 10); // 10 s apart: no line is counted on an earlier one
            };
        }
        var input = new ChatInputModel(message => alice.Session.Say(message));
        alice.Client.SayModalReceived += m => input.OnServer(m);
        w.Run(4 * Second); // tgui_say.initialize sleeps 3 s, then sends the window's props

        turf row = In(w, Row);
        Assert.NotNull(row);
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(row));
            Waiting.NoSleep(daveMob.forceMove(East(row, 1)));
            Waiting.NoSleep(bobMob.forceMove(East(row, 3)));
            Waiting.NoSleep(carolMob.forceMove(East(row, 10)));
        });
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        string bobName = In(w, () => bobMob.name);
        ChatLog bobLog = logs["Bob"];
        ChatPage all = bobLog.Pages[0], local = bobLog.Pages[1], ooc = bobLog.Pages[4];
        void Send(string channel, string text)
        {
            input.SetChannel(channel);
            input.Focus();
            input.Input(text, w.World.Tick / (double)Second);
            Assert.True(input.Enter());
            w.Run(Second);
        }

        // 1. Said: on Bob's Local page, typed by tg's selectors.
        Send(SayChannels.Say, "Hello there");
        bobLog.ChangePage(local);
        Assert.True(Shows(bobLog, "Hello there"));
        Assert.Equal(ChatLog.TypeLocalChat, bobLog.Visible.Last(m => m.Plain.Contains("Hello there", StringComparison.Ordinal)).Type);

        // 2. Whispered from the window: Dave hears the words, Bob only that she whispers something.
        Send(ChatInputModel.Whisper, "the code is 1234");
        Assert.True(Shows(logs["Dave"], "The code is 1234")); // tg capitalizes
        Assert.False(Shows(bobLog, "1234"));
        bobLog.ChangePage(all);
        Assert.Equal(ChatLog.TypeUnknown, bobLog.Visible.Last(m => m.Plain.Contains("something, but you are too far away to hear", StringComparison.Ordinal)).Type); // as in tg: only a name span
        bobLog.ChangePage(local);

        // 3. Shouted from the window: tg's yell reaches Carol 10 tiles off (DV162).
        Send(ChatInputModel.Shout, "Help me");
        Assert.True(Shows(logs["Carol"], "Help me!!"));

        // 4. OOC: on the OOC page, not on Local.
        Send(SayChannels.Ooc, "brb");
        Assert.False(Shows(bobLog, "brb"));
        bobLog.ChangePage(ooc);
        Assert.True(Shows(bobLog, "brb"));

        // 5. Bob reads OOC: a said line is unread on his All and Local tabs.
        int allUnread = all.UnreadCount; // the OOC line, read while on Local, is unread on All already
        Send(SayChannels.Say, "Anyone there");
        Assert.Equal(allUnread + 1, all.UnreadCount);
        Assert.Equal(1, local.UnreadCount);
        Assert.Equal(0, ooc.UnreadCount);
        bobLog.ChangePage(local);
        Assert.Equal(0, local.UnreadCount);
        Assert.True(Shows(bobLog, "Anyone there"));

        // 6. Dave punches Bob: the bystander's line is tg's .danger, on Alice's Combat page.
        Do(w, () =>
        {
            Waiting.NoSleep(daveMob.forceMove(East(row, 2)));
            daveMob.set_combat_mode(Dm.TRUE);
        });
        w.Run(Second);
        Assert.True(dave.Session.Click(bobMob.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        ChatLog aliceLog = logs["Alice"];
        aliceLog.ChangePage(aliceLog.Pages[3]);
        Assert.Contains(aliceLog.Visible, m => m.Type == ChatLog.TypeCombat && m.Plain.Contains(bobName, StringComparison.Ordinal));
        output.WriteLine("Alice's combat page: " + aliceLog.Visible.Last().Plain);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
