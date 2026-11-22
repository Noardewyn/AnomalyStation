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
/// Silicons SI4 (D326) through the real server on MetaStation with a headless client: Alice is made the AI; her client gets the AI's
/// command verbs (Show Laws among the ones tg grants at run time) and the camera static around her eye, its chunk masks the server's
/// obscured tiles; she runs Toggle Floor Bolts from the verb list; she moves her eye to a holopad a camera sees and right-clicks it, and
/// her hologram stands on the pad in her view; in multicamera mode the add-camera button opens a camera window whose view area reaches
/// her client.
/// </summary>
public class AiGameplayTests(ITestOutputHelper output)
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
    public void Alice_the_AI_gets_her_verbs_static_hologram_and_camera_window()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 78, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var chat = new List<string>();
        alice.Client.ServerTextReceived += t => chat.Add(t.ToString());
        w.Run(4 * Second);
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        string Runtimes() => string.Join("\n----\n", runtimes);

        // tg's admin Make AI: the AI chamber's core
        var human = (mob_living_carbon_human)w.MobOf(alice);
        Do(w, () => Waiting.Detach(human.AIize(human.client)));
        w.Run(4 * Second);
        var ai = w.MobOf(alice) as mob_living_silicon_ai;
        Assert.True(ai != null, "Alice is the AI; runtimes: " + Runtimes());
        output.WriteLine("the AI at " + In(w, () => $"{ai.x},{ai.y},{ai.z}"));

        // the stat panel's AI Commands: the type's verbs and the ones Initialize grants
        IReadOnlyList<VerbEntry> verbs = alice.Session.Replica.Verbs;
        output.WriteLine(string.Join(", ", verbs.Select(v => v.Category + "/" + v.Name)));
        Assert.Contains(verbs, v => v.Category == "AI Commands" && v.Name == "Show Laws");
        Assert.Contains(verbs, v => v.Category == "AI Commands" && v.Name == "Jump To Network");
        VerbEntry bolts = verbs.Single(v => v.Name == "Toggle Floor Bolts");
        float anchored = In(w, () => ai.is_anchored);
        alice.Session.RunVerb(bolts.Path);
        w.Run(Second);
        Assert.True(anchored != In(w, () => ai.is_anchored), "the bolts; chat: " + string.Join(" | ", chat.TakeLast(5)) + "; err: " + w.Err + "; runtimes: " + Runtimes());

        // the camera static: the replica's masks are the eye's chunks' obscured tiles
        Assert.NotEmpty(alice.Session.Replica.StaticChunks);
        Do(w, () =>
        {
            int checkedTiles = 0;
            foreach (datum_camerachunk chunk in Dm.each<datum_camerachunk>(ai.eyeobj.visibleCameraChunks))
            {
                foreach (turf t in Dm.each<turf>(chunk.turfs))
                {
                    Assert.Equal(Dm.@in(t, chunk.obscuredTurfs), alice.Session.Replica.IsObscured((int)t.x, (int)t.y, (int)t.z));
                    checkedTiles++;
                }
            }
            Assert.True(checkedTiles > 0);
        });

        // a holopad a camera sees: the eye goes there, a right click projects the hologram
        var pad = In(w, () => Dm.each<obj_machinery_holopad>(obj_machinery_holopad.holopads)
            .Where(p => p is not obj_machinery_holopad_secure && p.z == ai.z && !Dm.truthy(p.machine_stat) && SScameras.is_visible_by_cameras(get_turf(p)))
            .OrderBy(p => Math.Abs(p.x - ai.x) + Math.Abs(p.y - ai.y)).First());
        output.WriteLine("the holopad at " + In(w, () => $"{pad.x},{pad.y},{pad.z}"));
        Do(w, () => ai.eyeobj.setLoc(get_turf(pad)));
        w.Run(2 * Second);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, pad.dm_id, 0, 0, 0, 16, 16), ClickButton.Right, ClickModifiers.None));
        w.Run(2 * Second);
        var holo = In(w, () => pad.masters?[ai] as obj_effect_overlay_holo_pad_hologram);
        Assert.True(holo != null, "the right click projects Alice's hologram; runtimes: " + Runtimes());
        Assert.Same(In(w, () => get_turf(pad)), In(w, () => holo.loc));
        Assert.Contains(alice.Session.Replica.Entities, e => e.Id == holo.dm_id);

        // the multicamera button: a camera window, its view area on her client
        long button = In(w, () => Dm.each<atom_movable_screen_ai_multicam>(ai.client.screen).Single().dm_id);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.True(In(w, () => Dm.truthy(ai.multicam_on)), "multicam mode; runtimes: " + Runtimes());
        // the add-camera button drops a new window (the mode itself shows the ones there are)
        long add = In(w, () => Dm.each<atom_movable_screen_ai_add_multicam>(ai.client.screen).Single().dm_id);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, add, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.Contains(alice.Session.Replica.ViewAreas, a => a.ScreenId != 0);
        Assert.True(runtimes.Count == 0, Runtimes());
    }
}
