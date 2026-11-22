using Anomaly.Client.Shared;
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
/// M9 slice A through the real server on MetaStation with headless clients and tg's say window (protocol version 38): Alice, Bob and Carol
/// stand in a straight open row of air, Bob 3 tiles from Alice and Carol 10. Alice's open window shows her thinking indicator and her typing one;
/// her said line reaches Bob's chat and runechat (and her own), not Carol 10 tiles off (tg's range 7); her shout ("!!") reaches Carol
/// (DV162: 14 tiles); her whisper reaches Bob only as tg's "whispers something" and Dave next to her in full; her OOC reaches everyone;
/// hit while typing she blurts her text out cut short (force_say); a closed door between them keeps Bob from hearing her;
/// Dave's ghost hears her.
/// </summary>
public class SpeechGameplayTests(ITestOutputHelper output)
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

    // An open floor with normal air (in low pressure tg's voice carries one tile: living_say.dm's SOUND_MINIMUM_PRESSURE).
    private static bool Clear(turf t) => t is turf_open_floor && !Dm.truthy(t.density) && !Dm.truthy(t.opacity)
        && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || Dm.truthy(a.opacity))
        && Waiting.NoSleep(t.return_air()) is datum_gas_mixture air && Dm.num(air.return_pressure()) > 90;

    // The first straight row of 15 clear tiles running east on the station level, from its west end.
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

    private static void Say(RealHeadlessWorld.Player p, string type, string channel = null, string entry = null, bool visible = false)
        => p.Session.Say(new SayOut(type, channel, entry, visible));

    [MetaServerFact]
    public void Alice_is_heard_as_far_as_her_voice_carries()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 91, TimeoutSteps = 20000 },
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
        var runes = new Dictionary<string, List<RunechatMessage>> { ["Alice"] = new(), ["Bob"] = new(), ["Carol"] = new(), ["Dave"] = new() };
        var modal = new List<SayModalMessage>();
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob, carol, dave })
        {
            string name = p.Name;
            p.Client.ServerTextReceived += t => chat[name].Add(t.Html);
            p.Client.RunechatReceived += r => runes[name].Add(r);
        }
        alice.Client.SayModalReceived += modal.Add;
        w.Run(4 * Second); // tgui_say.initialize sleeps 3 s, then sends the window's props

        turf row = In(w, Row);
        Assert.NotNull(row);
        output.WriteLine(In(w, () => $"the row starts at {row.x},{row.y}"));
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(row));
            Waiting.NoSleep(daveMob.forceMove(East(row, 1)));
            Waiting.NoSleep(bobMob.forceMove(East(row, 3)));
            Waiting.NoSleep(carolMob.forceMove(East(row, 10)));
        });
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        Assert.Contains(modal, m => m.Command == SayModalCommand.Props && m.MaxLength == Limits.MaxSayEntryChars && !m.LightMode);

        // 1. The window opens: the thinking indicator; a key: the typing one.
        Say(alice, SayTypes.Open, SayChannels.Say);
        w.Run(2);
        Assert.True(In(w, () => HAS_TRAIT(aliceMob, TRAIT_THINKING_IN_CHARACTER)), "an open IC window is thinking");
        Say(alice, SayTypes.Typing);
        w.Run(2);
        Assert.True(In(w, () => Dm.truthy(aliceMob.active_typing_indicator)), "a key shows the typing bubble");
        w.Run(3);
        Assert.True(bob.Session.Replica.TryGet(aliceMob.dm_id, out ReplicaEntity aliceSeen));
        List<(string Icon, string State, float Layer)> looks = CrewOutfitTests.Icons(bob.Session.Replica, aliceSeen.State.Appearance);
        Assert.Contains(looks, l => l.Icon == "icons/mob/effects/talk.dmi" && l.State == "default0"); // tg's typing bubble over Alice

        // 2. Said: Bob (3 tiles) and Alice herself hear it, with runechat over Alice; Carol (10 tiles) does not.
        Say(alice, SayTypes.Entry, SayChannels.Say, "Hello there");
        Say(alice, SayTypes.Close);
        w.Run(Second);
        Assert.False(In(w, () => HAS_TRAIT(aliceMob, TRAIT_THINKING_IN_CHARACTER)));
        Assert.Contains(chat["Bob"], t => t.Contains("Hello there", StringComparison.Ordinal));
        Assert.Contains(chat["Alice"], t => t.Contains("Hello there", StringComparison.Ordinal));
        Assert.DoesNotContain(chat["Carol"], t => t.Contains("Hello there", StringComparison.Ordinal));
        Assert.Contains(runes["Bob"], r => r.Target == aliceMob.dm_id && r.Maptext.Contains("Hello there", StringComparison.Ordinal));
        Assert.Contains(runes["Alice"], r => r.Target == aliceMob.dm_id);
        Assert.DoesNotContain(runes["Carol"], r => r.Maptext.Contains("Hello there", StringComparison.Ordinal));
        // D319: the chat translator finds the words in the line Bob reads and in the text over Alice's head
        ChatEntry heard = new ChatLog().Add(null, chat["Bob"].Last(t => t.Contains("Hello there", StringComparison.Ordinal)), 0);
        Assert.True(Anomaly.Client.Shared.Speech.ChatTranslation.TryWords(heard.Type, heard.Plain, out int at, out int length));
        Assert.StartsWith("Hello there", heard.Plain.Substring(at, length));
        string maptext = runes["Bob"].Last(r => r.Target == aliceMob.dm_id && r.Maptext.Contains("Hello there", StringComparison.Ordinal)).Maptext;
        Assert.True(Anomaly.Client.Shared.Speech.ChatTranslation.TryMaptextWords(maptext, out at, out length));
        Assert.StartsWith("Hello there", HtmlText.ToPlain(maptext.Substring(at, length)));
        output.WriteLine("Bob heard: " + chat["Bob"].Last(t => t.Contains("Hello there", StringComparison.Ordinal)));

        // 3. Shouted ("!!", DV162): Carol 10 tiles off hears it, and its runechat is a yell.
        Say(alice, SayTypes.Entry, SayChannels.Say, "Help me!!");
        w.Run(Second);
        Assert.Contains(chat["Carol"], t => t.Contains("Help me!!", StringComparison.Ordinal));
        Assert.Contains(runes["Carol"], r => r.Maptext.Contains("yell", StringComparison.Ordinal));

        // 4. Whispered: Dave next to her hears the words, Bob 3 tiles off sees her whisper something.
        Say(alice, SayTypes.Entry, SayChannels.Say, "#the code is 1234");
        w.Run(Second);
        Assert.Contains(chat["Dave"], t => t.Contains("whispers, ", StringComparison.Ordinal) && t.Contains("The code is 1234", StringComparison.Ordinal)); // tg capitalizes
        Assert.DoesNotContain(chat["Bob"], t => t.Contains("1234", StringComparison.Ordinal));
        Assert.Contains(chat["Bob"], t => t.Contains("something, but you are too far away to hear", StringComparison.Ordinal));
        Assert.DoesNotContain(chat["Carol"], t => t.Contains("1234", StringComparison.Ordinal));

        // 5. OOC reaches every player, wherever they are.
        Say(alice, SayTypes.Entry, SayChannels.Ooc, "brb");
        w.Run(Second);
        foreach (string name in chat.Keys)
            Assert.Contains(chat[name], t => t.Contains("OOC:", StringComparison.Ordinal) && t.Contains("brb", StringComparison.Ordinal));

        // 6. Hit while typing (tg's force_say): the server asks the window for its text, the window answers with it cut short.
        Say(alice, SayTypes.Open, SayChannels.Say);
        w.Run(2);
        modal.Clear();
        Do(w, () => aliceMob.force_say());
        w.Run(2);
        Assert.Contains(modal, m => m.Command == SayModalCommand.Force);
        Say(alice, SayTypes.Force, SayChannels.Say, "I was about to say something important");
        w.Run(Second);
        Assert.Contains(chat["Bob"], t => t.Contains("I was about to say something", StringComparison.Ordinal) && t.Contains("-", StringComparison.Ordinal)
            && new[] { "GACK!", "GLORF!", "OOF!", "AUGH!", "OW!", "URGH!", "HRNK!" }.Any(p => t.Contains(p, StringComparison.Ordinal)));

        // 7. A closed door between Alice and Bob: an airlock dropped on the tile between them stops her voice.
        Do(w, () => Dm.@new<obj_machinery_door_airlock>(East(row, 2)));
        w.Run(Second);
        Assert.True(In(w, () => Dm.each<obj_machinery_door_airlock>(East(row, 2).contents).Any(d => Dm.truthy(d.density))));
        Say(alice, SayTypes.Entry, SayChannels.Say, "Through the door");
        w.Run(Second);
        Assert.Contains(chat["Dave"], t => t.Contains("Through the door", StringComparison.Ordinal));
        Assert.DoesNotContain(chat["Bob"], t => t.Contains("Through the door", StringComparison.Ordinal));

        // 8. A ghost hears the living: Dave dies (dev), a movement key moves his ghost off the body, and Alice's line reaches him.
        alice.Session.DevCommand("kill Dave");
        w.Run(5);
        dave.Yaw = 0;
        dave.Keys = MoveKeys.Forward;
        w.Run(4);
        dave.Keys = MoveKeys.None;
        w.Run(3);
        Assert.True(dave.Session.Ghosting, "Dave's client drives a ghost");
        long ghost = dave.Client.OwnEntity;
        Say(alice, SayTypes.Entry, SayChannels.Say, "Rest in peace");
        w.Run(Second);
        Assert.Contains(chat["Dave"], t => t.Contains("Rest in peace", StringComparison.Ordinal));
        Assert.Contains(runes["Dave"], r => r.Target == aliceMob.dm_id && r.Maptext.Contains("Rest in peace", StringComparison.Ordinal));
        Assert.NotEqual(daveMob.dm_id, ghost);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
