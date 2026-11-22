using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
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
/// Silicons SI6c (D326) through the real server on MetaStation with headless clients: Alice opens a space piano's InstrumentEditor
/// window, imports a song through tg's text prompt and plays it; Bob beside her hears the notes as pitched samples on reserved channels
/// whose volume the decay updates, and the song stops at its end.
/// </summary>
public class InstrumentGameplayTests(ITestOutputHelper output)
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

    [MetaServerFact]
    public void Alice_plays_a_song_on_a_piano_and_bob_hears_its_notes()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 81, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        w.Run(4 * Second);
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        string Runtimes() => string.Join("\n----\n", runtimes);
        var heard = new List<SoundEvent>();
        bob.Session.Replica.SoundsReceived += events => heard.AddRange(events);

        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        obj_structure_musician_piano piano = null;
        Do(w, () =>
        {
            turf here = get_turf(aliceMob);
            Waiting.NoSleep(bobMob.forceMove(here));
            piano = Dm.@new<obj_structure_musician_piano>(get_step(here, NORTH));
            Waiting.Detach(piano.ui_interact(aliceMob));
        });
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        UiNode Editor()
        {
            w.Run(Second);
            windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
            UiWindowState state = alice.Session.Replica.UiWindows.Values.Single(v => v.Interface == "InstrumentEditor");
            return windows.Get(state.Window).Content;
        }

        // Import a Song: tg's text prompt takes the whole song
        Editor().Walk().Single(n => n.Key == "import").OnClick();
        w.Run(2 * Second);
        UiWindowState prompt = alice.Session.Replica.UiWindows.Values.Single(v => v.Interface == "TextInputModal");
        Assert.True(alice.Session.UiAct(prompt.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of("BPM: 60\nC,E,G,C4")))));
        w.Run(Second);
        Assert.Equal(1, In(w, () => Dm.length(piano.song.lines)));
        Assert.Contains(Editor().Walk(), n => n.Text == "C,E,G,C4");

        heard.Clear();
        Editor().Walk().Single(n => n.Key == "play_music").OnClick();
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(piano.song.playing)), "playing; runtimes: " + Runtimes());
        w.Run(6 * Second);
        List<SoundEvent> notes = heard.Where(e => e.Kind == SoundKind.Play && e.Channel > 0).ToList();
        output.WriteLine(string.Join("\n", notes.Select(n => $"channel {n.Channel} frequency {n.Frequency} volume {n.Volume}")));
        Assert.True(notes.Count >= 4, "Bob heard the four notes: " + heard.Count + " events");
        Assert.Contains(heard, e => e.Kind == SoundKind.Update); // the decay's volume updates
        Assert.True(In(w, () => !Dm.truthy(piano.song.playing)), "the song ended");
        Assert.True(runtimes.Count == 0, Runtimes());
    }
}
