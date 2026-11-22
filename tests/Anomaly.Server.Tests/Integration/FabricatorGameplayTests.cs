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
/// Gaps RD1 (D333, the fabricators) through the real server on MetaStation with a headless client: Meta's science protolathe is linked to
/// the station's techweb and to the ore silo; Bob opens its Fabricator window, finds the basic matter bin, and once the silo has the iron
/// prints two of them. At Meta's autolathe he opens the Autolathe window and prints a crowbar from its iron.
/// </summary>
public class FabricatorGameplayTests(ITestOutputHelper output)
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
    public void Bob_prints_at_Metas_science_protolathe_and_its_autolathe()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 64, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => bob.Session.UiAct(window, action, parameters),
            SendClose = window => bob.Session.UiClose(window),
        };

        // Meta's science protolathe: the station's techweb and the ore silo.
        var lathe = In(w, () => Dm.each<obj_machinery_rnd_production_protolathe_department_science>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_rnd_production_protolathe_department_science))).First());
        output.WriteLine("protolathe at " + In(w, () => $"{lathe.x},{lathe.y},{lathe.z}"));
        Assert.IsType<datum_techweb_science>(In(w, () => lathe.stored_research));
        Assert.NotNull(In(w, () => lathe.materials.silo));
        Do(w, () =>
        {
            Waiting.NoSleep(bobMob.forceMove(FreeTileBy(lathe)));
            Waiting.Detach(lathe.ui_interact(bobMob));
        });
        UiNode ui = Shown(w, windows, bob, "Fabricator");
        UiNode recipe = ui.Walk().Single(n => n.Key == "recipe:/datum/design/basic_matter_bin");
        Assert.Contains(recipe.Walk(), n => n.Key == "name" && n.Text == "Basic Matter Bin");

        // The silo gets the iron; the window sees it and Bob prints two bins.
        Do(w, () => Waiting.NoSleep(lathe.materials.mat_container.insert_amount_mat(10 * SHEET_MATERIAL_AMOUNT, typeof(datum_material_iron))));
        ui = Shown(w, windows, bob, "Fabricator");
        recipe = ui.Walk().Single(n => n.Key == "recipe:/datum/design/basic_matter_bin");
        UiNode twice = recipe.Walk().Single(n => n.Key == "max");
        twice.OnCommit("2");
        for (int i = 0; i < Second && !In(w, () => Dm.truthy(lathe.busy)); i++)
            w.Run(1);
        Assert.Equal("protolathe_n", In(w, () => lathe.icon_state));
        w.Run(3 * Second);
        Assert.Equal("protolathe", In(w, () => lathe.icon_state));
        Assert.Equal(2, In(w, () => Dm.each<obj_item_stock_parts_matter_bin>(get_turf(lathe)).Count()));

        // Meta's autolathe: its iron in, a crowbar out.
        var autolathe = In(w, () => Dm.each<obj_machinery_autolathe>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_autolathe))).First());
        output.WriteLine("autolathe at " + In(w, () => $"{autolathe.x},{autolathe.y},{autolathe.z}"));
        Do(w, () =>
        {
            Waiting.NoSleep(autolathe.materials.insert_amount_mat(10 * SHEET_MATERIAL_AMOUNT, typeof(datum_material_iron)));
            Waiting.NoSleep(bobMob.forceMove(FreeTileBy(autolathe)));
            Waiting.Detach(autolathe.ui_interact(bobMob));
        });
        ui = Shown(w, windows, bob, "Autolathe");
        recipe = ui.Walk().Single(n => n.Key == "recipe:/datum/design/crowbar");
        UiNode print = recipe.Walk().Single(n => n.Key == "print");
        Assert.False(print.Disabled);
        print.OnClick();
        for (int i = 0; i < Second && !In(w, () => Dm.truthy(autolathe.busy)); i++)
            w.Run(1);
        Assert.Equal("autolathe_n", In(w, () => autolathe.icon_state));
        w.Run(3 * Second);
        Assert.Equal("autolathe", In(w, () => autolathe.icon_state));
        Assert.Contains(In(w, () => Dm.each<atom>(get_turf(autolathe)).ToList()), a => a.GetType() == typeof(obj_item_crowbar));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
