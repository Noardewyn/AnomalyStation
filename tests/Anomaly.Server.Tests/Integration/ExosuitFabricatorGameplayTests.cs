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
/// Silicons SI3 (D326) through the real server on MetaStation with a headless client: Meta's exosuit fabricator is linked to the station's
/// science techweb and to the ore silo; Bob opens its window, finds the cyborg endoskeleton among the designs (and no mech part, DV384;
/// the MOD shell since gaps MO1), presses Build Now and, once the silo has the iron, the fabricator prints the endoskeleton out of its outlet.
/// </summary>
public class ExosuitFabricatorGameplayTests(ITestOutputHelper output)
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

    private static UiNode Shown(RealHeadlessWorld w, TguiWindows windows, RealHeadlessWorld.Player p, string ui)
    {
        w.Run(Second);
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        UiWindowState state = p.Session.Replica.UiWindows.Values.Single(v => v.Interface == ui);
        return windows.Get(state.Window).Content;
    }

    private static turf FreeTileBy(atom a) => Dm.each<turf>(Dm.list(get_step(a, SOUTH), get_step(a, NORTH), get_step(a, EAST), get_step(a, WEST)))
        .First(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each<atom>(t.contents).Any(c => Dm.truthy(c.density)));

    [MetaServerFact]
    public void Bob_prints_a_cyborg_endoskeleton_at_the_exosuit_fabricator()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 63, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };

        // Meta's fabricator: the station's techweb and the ore silo.
        var fab = In(w, () => Dm.each<obj_machinery_mecha_part_fabricator>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_mecha_part_fabricator))).First());
        output.WriteLine("fabricator at " + In(w, () => $"{fab.x},{fab.y},{fab.z}"));
        Assert.IsType<datum_techweb_science>(In(w, () => fab.stored_research));
        Assert.NotNull(In(w, () => fab.rmat.silo));
        turf outlet = In(w, () => FreeTileBy(fab));

        Do(w, () =>
        {
            fab.drop_direction = get_dir(fab, outlet);
            Waiting.NoSleep(bobMob.forceMove(FreeTileBy(fab)));
            Waiting.Detach(fab.ui_interact(bobMob));
        });
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => bob.Session.UiAct(window, action, parameters),
            SendClose = window => bob.Session.UiClose(window),
        };
        UiNode ui = Shown(w, windows, bob, "ExosuitFabricator");
        string suitPath = "/datum/design/borg_suit";
        UiNode recipe = ui.Walk().Single(n => n.Key == "recipe:" + suitPath);
        Assert.Contains(recipe.Walk(), n => n.Key == "name" && n.Text == "Cyborg Endoskeleton");
        Assert.DoesNotContain(ui.Walk(), n => n.Key == "recipe:/datum/design/ripley_chassis");
        Assert.Contains(ui.Walk(), n => n.Key == "recipe:/datum/design/mod_shell"); // the MODsuits are live since gaps MO1
        // the silo is empty at roundstart: the endoskeleton cannot be printed yet
        Assert.True(recipe.Walk().Single(n => n.Key == "buildNow").Disabled);

        // The silo gets the iron; the window sees it and Bob presses Build Now.
        Do(w, () => Waiting.NoSleep(fab.rmat.mat_container.insert_amount_mat(10 * SHEET_MATERIAL_AMOUNT, typeof(datum_material_iron))));
        ui = Shown(w, windows, bob, "ExosuitFabricator");
        recipe = ui.Walk().Single(n => n.Key == "recipe:" + suitPath);
        UiNode buildNow = recipe.Walk().Single(n => n.Key == "buildNow");
        Assert.False(buildNow.Disabled);
        buildNow.OnClick();
        w.Run(2 * Second);
        Assert.IsType<datum_design_borg_suit>(In(w, () => fab.being_built));
        Assert.Contains("fab-active", In(w, () => Dm.each(fab.overlays).Select(o => o is string s ? s : Dm.str((o as DmAppearance)?.icon_state)).ToList()));
        ui = Shown(w, windows, bob, "ExosuitFabricator");
        Assert.Contains(ui.Walk(), n => n.Key == "stop" && n.Text == "Stop");

        w.Run(52 * Second);
        Assert.Null(In(w, () => fab.being_built));
        Assert.Contains(In(w, () => Dm.each<atom>(outlet.contents).ToList()), a => a is obj_item_robot_suit);

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
