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
/// Gaps package S3 (D298) on MetaStation through the real server and a client's own stack: a crewman takes the pen and a sheet out of one
/// of Meta's paper bins, puts the sheet down and clicks it with the pen; tg's PaperSheet window opens in write mode, he types into its text
/// area and saves, the paper on the server holds the text, turns to its written-on picture on the client, and the client's window shows
/// the text on the page.
/// </summary>
public class PaperworkGameplayTests(ITestOutputHelper output)
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

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf ClearNeighbour(atom a)
        => new[] { SOUTH, NORTH, EAST, WEST }.Select(d => get_step(a, d) as turf)
            .FirstOrDefault(t => t != null && Clear(t) && Dm.truthy(Waiting.NoSleep(a.Adjacent(t))));

    [MetaServerFact]
    public void A_crewman_takes_paper_and_a_pen_from_a_bin_writes_and_the_client_shows_the_paper()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 97, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Crewman");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // a paper bin of the station with its pen (tg's bins take the pen mapped on their tile) and a clear tile beside it
        obj_item_paper_bin bin = In(w, () => Dm.each<obj_item_paper_bin>().Where(b => b.z == 1 && b.GetType() == typeof(obj_item_paper_bin) && b.bin_pen != null
            && get_area(b) is area_station && isturf(b.loc) && ClearNeighbour(b) != null).OrderBy(b => b.dm_id).FirstOrDefault());
        Assert.NotNull(bin);
        output.WriteLine(In(w, () => $"{Dm.each<obj_item_paper_bin>().Count(b => b.z == 1)} paper bins on the station; this one at {bin.x},{bin.y} in {Dm.str(((area)get_area(bin)).name)}"));
        turf beside = In(w, () => ClearNeighbour(bin));
        Do(w, () =>
        {
            Waiting.NoSleep(me.drop_all_held_items());
            Waiting.NoSleep(me.forceMove(beside));
        });
        w.Run(Second);

        // 1. the bin hands out its pen, then a sheet
        Assert.True(p.Session.Click(On(bin), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        obj_item_pen pen = In(w, () => me.get_active_held_item() as obj_item_pen);
        Assert.NotNull(pen);
        Do(w, () => me.swap_hand());
        w.Run(Second);
        Assert.True(p.Session.Click(On(bin), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        obj_item_paper paper = In(w, () => me.get_active_held_item() as obj_item_paper);
        Assert.NotNull(paper);
        Assert.Equal(29f, In(w, () => bin.total_paper));

        // 2. the sheet goes on the floor; a click on it with the pen opens tg's PaperSheet in write mode
        Do(w, () =>
        {
            Waiting.NoSleep(me.dropItemToGround(paper));
            me.swap_hand();
        });
        w.Run(Second);
        Assert.Same(pen, In(w, () => me.get_active_held_item()));
        Assert.True(p.Session.Click(On(paper), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("PaperSheet", window.Interface);
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        UiNode content = windows.Get(window.Window).Content;
        Assert.Equal(700.0, windows.Get(window.Window).Context.WindowHeight); // the write mode's text area

        // 3. typed and saved (Button.Confirm: twice): the paper holds the text, its picture shows words, the window shows the text
        content.Walk().Single(n => n.Kind == UiKind.TextArea).OnInput("Requesting **more** paper.");
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        windows.Get(window.Window).Content.Walk().Single(n => n.Key == "save").OnClick();
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        windows.Get(window.Window).Content.Walk().Single(n => n.Key == "save").OnClick();
        w.Run(Second);
        Assert.Equal("Requesting **more** paper.", In(w, () => (paper.raw_text_inputs?[1] as datum_paper_input)?.raw_text));
        Assert.Equal("paper_words", In(w, () => paper.icon_state));
        Assert.True(replica.TryGet(paper.dm_id, out ReplicaEntity seen));
        Assert.Contains("paper_words", CryoGameplayTests.States(replica, seen.State.Appearance));
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        UiNode page = windows.Get(window.Window).Content.Walk().Single(n => n.Kind == UiKind.PaperPage);
        output.WriteLine("the page: " + page.TextValue);
        Assert.Contains("Requesting <b>more</b> paper.", page.TextValue);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
