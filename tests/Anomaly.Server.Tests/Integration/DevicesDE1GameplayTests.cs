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
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package DE1 (D333) through the real server on MetaStation: a crew member holds a tape recorder, opens tg's radial with the
/// use-in-hand key (Z) and picks Record over the wire; the client hears tg's click and sees the recorder's recording state in his hand
/// slot; he says a line, stops the recorder from the radial and plays it back, and the client gets the recorder's playback of his line.
/// Then he rolls a d20 in his hand (Z): tg's roll message and sound reach the client.
/// </summary>
public class DevicesDE1GameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public DevicesDE1GameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static List<ScreenEntry> HoldersOf(ReplicaWorld replica, long id)
        => replica.Screen.Values.Where(e => e.VisChildren != null && e.VisChildren.Contains(id)).ToList();

    // the state the client draws for that item in its hand slot
    private static string HandState(RealHeadlessWorld w, ReplicaWorld replica, atom item)
    {
        ScreenEntry hand = Assert.Single(HoldersOf(replica, In(w, () => item.dm_id)));
        Assert.True(replica.TryGetAppearance(Assert.Single(hand.VisChildren.Items).Appearance, out AppearanceEntry drawn));
        return replica.ResolveString(drawn.IconState);
    }

    // Z, then the slice of tg's radial the client got
    private static void Pick(RealHeadlessWorld w, RealHeadlessWorld.Player p, string slice, Func<string> diag)
    {
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second / 2);
        RadialMenuMessage menu = p.Session.Replica.TopRadial;
        Assert.True(menu != null, "no radial; " + diag());
        int index = menu.Elements.FindIndex(e => (e.Name ?? "").Contains(slice, StringComparison.OrdinalIgnoreCase));
        Assert.True(index >= 0, "no " + slice + " slice in " + string.Join(", ", menu.Elements.Select(e => e.Name)));
        Assert.True(p.Session.RadialPick(menu.MenuId, index + 1));
        w.Run(Second / 2);
    }

    [MetaServerFact]
    public void A_crew_member_records_himself_plays_the_tape_back_and_rolls_a_die_and_the_client_hears_and_sees_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 37, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Clerk");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        var runtimes = new List<string>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.Message); sink?.Invoke(e); };
        string Diag() => "chat: " + string.Join(" | ", texts.TakeLast(6)) + " runtimes: " + string.Join("\n", runtimes.TakeLast(3));

        // a recorder with its tape in the active hand
        var recorder = In(w, () =>
        {
            foreach (obj_item held in Dm.each_cast<obj_item>(me.held_items.Copy()))
                if (held != null)
                    Waiting.NoSleep(me.dropItemToGround(held));
            var r = Dm.@new<obj_item_taperecorder>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(r))));
            return r;
        });
        w.Run(Second / 2);
        Assert.Equal("taperecorder_idle", HandState(w, replica, recorder));

        // Record: tg's click, the recording state in the hand
        heard.Clear();
        Pick(w, p, "Record", Diag);
        Assert.True(In(w, () => Dm.truthy(recorder.recording)), "recording; " + Diag());
        Assert.Contains(heard, h => h.File == "sound/items/taperecorder/taperecorder_play.ogg");
        w.Run(Second / 2);
        Assert.Equal("taperecorder_recording", HandState(w, replica, recorder));

        // his line on the tape
        p.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "Testing one two", false));
        w.Run(2 * Second);
        Assert.Contains("Testing one two", In(w, () => string.Join("\n", Dm.each(recorder.mytape.storedinfo).Select(Dm.str))));

        // Stop, then Play: the recorder says the line back to him
        Pick(w, p, "Stop", Diag);
        Assert.False(In(w, () => Dm.truthy(recorder.recording)), "stopped; " + Diag());
        Assert.Contains(heard, h => h.File == "sound/items/taperecorder/taperecorder_stop.ogg");
        int before = texts.Count(t => t.Contains("Testing one two", StringComparison.Ordinal));
        Pick(w, p, "Play", Diag);
        w.Run(Second / 2);
        Assert.Equal("taperecorder_playing", HandState(w, replica, recorder));
        w.Run(3 * Second);
        int after = texts.Count(t => t.Contains("Testing one two", StringComparison.Ordinal));
        _output.WriteLine("lines with the words: " + before + " before the playback, " + after + " after");
        Assert.True(after > before, "no playback reached the client; " + Diag());
        w.Run(2 * Second);
        Assert.Equal("taperecorder_idle", HandState(w, replica, recorder));

        // a d20 rolled in the hand
        var die = In(w, () =>
        {
            Waiting.NoSleep(me.dropItemToGround(recorder));
            var d = Dm.@new<obj_item_dice_d20>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(d))));
            return d;
        });
        w.Run(Second / 2);
        heard.Clear();
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second / 2);
        Assert.Contains(texts, t => t.Contains("It lands on", StringComparison.Ordinal));
        Assert.Contains(heard, h => h.File == "sound/items/dice_roll.ogg");
        Assert.Equal(In(w, () => Dm.text("d20-[]", die.result)), In(w, () => Dm.each(die.overlays).Select(o => o is IDmAppearance a ? a.icon_state : Dm.str(o)).Single()));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n", runtimes));
    }
}
