using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M9 slice B through the real server on MetaStation with headless clients (protocol version 39): Alice, Bob and Carol stand in a row,
/// Bob 3 tiles from Alice and Carol 10. Alice's wheel waves (Bob sees it in chat and over her, Carol is too far), a second wave inside the
/// cooldown does nothing but tell her so (DV164); her scream plays tg's scream to Bob; "*jump" said is the jump; her custom emote (the say window's Me) reaches
/// Bob; her blush draws tg's blush on her face; "*help" lists the emotes; the wheel's "More..." opens tg's emote panel, whose nod
/// reaches Bob; Dave's death is his deathgasp.
/// </summary>
public class EmoteGameplayTests(ITestOutputHelper output)
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

    // An open floor with normal air (the speech test's row: in low pressure tg's voice carries one tile).
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

    private static int Count(List<string> lines, string text) => lines.Count(t => t.Contains(text, StringComparison.Ordinal));

    [MetaServerFact]
    public void Alice_emotes_from_her_wheel_and_her_panel()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        RealHeadlessWorld.Player dave = w.Join("Dave");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var daveMob = (mob_living_carbon_human)w.MobOf(dave);
        var chat = new Dictionary<string, List<string>> { ["Alice"] = new(), ["Bob"] = new(), ["Carol"] = new(), ["Dave"] = new() };
        var runes = new List<RunechatMessage>();
        var heard = new List<SoundEvent>();
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob, carol, dave })
        {
            string name = p.Name;
            p.Client.ServerTextReceived += t => chat[name].Add(t.Html);
        }
        bob.Client.RunechatReceived += runes.Add;
        bob.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(4 * Second);

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
        string aliceName = In(w, () => aliceMob.name);
        Assert.True(In(w, () => GLOB.emote_list["wave"] is DmList), "the emote list is made at init");

        // 1. The wheel's wave: Bob sees it in chat and over Alice; Carol, 10 tiles off, does not. A second wave inside tg's cooldown is refused with a balloon alert (DV164).
        Assert.True(alice.Session.Emote("wave"));
        Assert.True(alice.Session.Emote("wave"));
        w.Run(Second);
        Assert.Equal(1, Count(chat["Bob"], "waves."));
        Assert.Contains(chat["Bob"], t => t.Contains(aliceName, StringComparison.Ordinal) && t.Contains("waves.", StringComparison.Ordinal));
        Assert.Contains(chat["Alice"], t => t.Contains("waves.", StringComparison.Ordinal));
        Assert.Contains(chat["Alice"], t => t == "emote on cooldown!"); // DV164: the second is refused with a balloon alert
        Assert.DoesNotContain(chat["Carol"], t => t.Contains("waves.", StringComparison.Ordinal));
        Assert.Contains(runes, r => r.Target == aliceMob.dm_id && r.Maptext.Contains("waves.", StringComparison.Ordinal) && r.Maptext.Contains("emote", StringComparison.Ordinal));
        // D319: an emote over the head is not translated
        Assert.False(Anomaly.Client.Shared.Speech.ChatTranslation.TryMaptextWords(runes.Last(r => r.Maptext.Contains("waves.", StringComparison.Ordinal)).Maptext, out _, out _));
        output.WriteLine("Bob saw: " + chat["Bob"].Last(t => t.Contains("waves.", StringComparison.Ordinal)));

        // 2. The scream: its message and tg's human scream sound reach Bob.
        Assert.True(alice.Session.Emote("scream"));
        w.Run(Second);
        Assert.Contains(chat["Bob"], t => t.Contains("screams!", StringComparison.Ordinal));
        Assert.Contains(heard, h => (h.File ?? "").Contains("scream", StringComparison.Ordinal));

        // 3. tg's "*" in a said line is an emote: the jump (it has a sound, so it waits out tg's 2 s shared emote audio cooldown).
        w.Run(2 * Second);
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "*jump"));
        w.Run(Second);
        Assert.Contains(chat["Bob"], t => t.Contains("jumps!", StringComparison.Ordinal));

        // 4. The custom emote: the say window's Me channel.
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Me, "taps her foot to a tune"));
        w.Run(Second);
        Assert.Contains(chat["Bob"], t => t.Contains("taps her foot to a tune", StringComparison.Ordinal));

        // 5. The blush draws tg's blush on Alice's face (emote_visuals.dmi), seen by Bob.
        Assert.True(alice.Session.Emote("blush"));
        w.Run(Second);
        Assert.Contains(chat["Bob"], t => t.Contains("blushes.", StringComparison.Ordinal));
        Assert.True(bob.Session.Replica.TryGet(aliceMob.dm_id, out ReplicaEntity aliceSeen));
        List<(string Icon, string State, float Layer)> looks = CrewOutfitTests.Icons(bob.Session.Replica, aliceSeen.State.Appearance);
        Assert.Contains(looks, l => l.Icon == "icons/mob/human/emote_visuals.dmi" && l.State == "blush");
        w.Run(6 * Second); // tg deletes it after 5.2 s
        Assert.True(bob.Session.Replica.TryGet(aliceMob.dm_id, out aliceSeen));
        Assert.DoesNotContain(CrewOutfitTests.Icons(bob.Session.Replica, aliceSeen.State.Appearance), l => l.State == "blush");

        // 6. "*help" lists what she can run.
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "*help"));
        w.Run(Second);
        Assert.Contains(chat["Alice"], t => t.Contains("Available emotes", StringComparison.Ordinal) && t.Contains("salute", StringComparison.Ordinal));

        // 7. The wheel's "More...": tg's emote panel opens with her emotes; its nod reaches Bob.
        alice.Session.Keybinding(KeybindingCode.VerbEmotePanel);
        w.Run(Second);
        UiWindowState panel = Assert.Single(alice.Session.Replica.UiWindows.Values, v => v.Interface == "EmotePanel");
        IReadOnlyList<UiValue> emotes = panel.Merged.Arr("emotes");
        Assert.Contains(emotes, e => e.Str("key") == "nod");
        Assert.DoesNotContain(emotes, e => e.Str("key") == "me"); // the panel's blacklist
        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        UiNode nod = windows.Get(panel.Window).Content.Walk().First(n => n.Key == "play:nod");
        nod.OnClick();
        w.Run(Second);
        Assert.Contains(chat["Bob"], t => t.Contains("nods.", StringComparison.Ordinal));

        // 8. Dave dies: his deathgasp.
        alice.Session.DevCommand("kill Dave");
        w.Run(2 * Second);
        Assert.Contains(chat["Bob"], t => t.Contains("seizes up and falls limp", StringComparison.Ordinal));

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
