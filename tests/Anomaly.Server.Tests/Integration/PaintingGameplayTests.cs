using Anomaly.Client.Shared.Icons;
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
/// Silicons SI4d (D326) through the real server on MetaStation with a headless client: Alice paints a canvas with a red crayon in its
/// Canvas window (a click on the picture), finalizes it with tg's title and signature prompts, and her client gets the painting's PNG as a
/// runtime resource (protocol 79) whose path the canvas' picture names; she frames it in a library frame, and the round's end archives it.
/// </summary>
public class PaintingGameplayTests(ITestOutputHelper output)
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

    private static void Answer(RealHeadlessWorld w, RealHeadlessWorld.Player p, string ui, string action, string key, string value)
    {
        w.Run(2 * Second);
        UiWindowState input = p.Session.Replica.UiWindows.Values.Single(v => v.Interface == ui);
        Assert.True(p.Session.UiAct(input.Window, action, UiValue.ObjectOf((key, UiValue.Of(value)))));
    }

    [MetaServerFact]
    public void Alice_paints_finalizes_and_frames_a_canvas_and_her_client_gets_its_picture()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 79, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(4 * Second);
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        string Runtimes() => string.Join("\n----\n", runtimes);

        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        obj_item_canvas canvas = null;
        Do(w, () =>
        {
            canvas = Dm.@new<obj_item_canvas>(aliceMob.loc);
            var crayon = Dm.@new<obj_item_toy_crayon_red>(aliceMob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_hands(crayon))));
            Waiting.Detach(canvas.ui_interact(aliceMob));
        });
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        UiNode Canvas()
        {
            w.Run(Second);
            windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
            UiWindowState state = alice.Session.Replica.UiWindows.Values.Single(v => v.Interface == "Canvas");
            return windows.Get(state.Window).Content;
        }

        // a pencil click on the picture's top left cell
        UiNode picture = Canvas().Walk().Single(n => n.Key == "canvas");
        double side = double.Parse(picture.Height.ToString().Replace("px", ""), System.Globalization.CultureInfo.InvariantCulture);
        picture.OnClickAt(1, side - 1);
        w.Run(Second);
        string red = In(w, () => Dm.str(((DmList)canvas.workspace.get_first_layer_pixel_data(SOUTH)[1])[1]));
        string crayonRed = In(w, () => Dm.str(((obj_item_toy_crayon)aliceMob.get_active_held_item()).paint_color)).ToLowerInvariant() + "ff";
        Assert.True(red == crayonRed, "the crayon painted the pixel: " + red + "; runtimes: " + Runtimes());

        // Finalize (a confirm button: two clicks), then tg's title and signature prompts
        Canvas().Walk().Single(n => n.Key == "finalize" && n.OnClick != null).OnClick();
        Canvas().Walk().Single(n => n.Key == "finalize" && n.OnClick != null).OnClick();
        Answer(w, alice, "TextInputModal", "submit", "entry", "Red Dawn");
        Answer(w, alice, "AlertModal", "choose", "choice", "Yes");
        w.Run(2 * Second);
        Assert.True(In(w, () => Dm.truthy(canvas.finalized)), "finalized; runtimes: " + Runtimes());
        Assert.Equal("Red Dawn", In(w, () => Dm.str(canvas.painting_metadata.title)));
        string path = In(w, () => (canvas.generated_icon.dm_file as DmResource)?.Path);
        output.WriteLine("the painting's resource " + path);
        Assert.True(RuntimeImages.TryRead(path, out byte[] png) && png.Length > 0, "the client has the painting's PNG");
        Assert.True(RuntimeImages.TryRead(path[..^4] + ".json", out _));

        // framed in a library frame; the round's end archives it
        Do(w, () =>
        {
            var frame = Dm.@new<obj_structure_sign_painting_library>(aliceMob.loc);
            Waiting.NoSleep(aliceMob.dropItemToGround(aliceMob.get_active_held_item() as obj_item));
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_hands(canvas))));
            Assert.Equal(TRUE, Waiting.NoSleep(frame.frame_canvas(aliceMob, canvas)));
            SSpersistent_paintings.save_paintings();
            Assert.Contains("Red Dawn", DmWorldState.Current.Files.ReadText("data/paintings.json"));
        });
        Assert.True(runtimes.Count == 0, Runtimes());
    }
}
