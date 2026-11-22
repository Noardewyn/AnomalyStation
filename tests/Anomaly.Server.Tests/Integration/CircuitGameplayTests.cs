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
/// Gaps CI1 (D339, D341, the circuits' core and editor) through the real server on MetaStation with a headless client: at Meta's science
/// component printer Bob prints a toggle component from its ComponentPrinter window; he puts a circuit with a cell into a compact remote,
/// links the circuit to the printer, opens the IntegratedCircuit editor, prints a toggle into it from the component menu and drags a wire
/// from the remote's signal to the toggle in the node graph; using the remote in hand flips the toggle.
/// </summary>
public class CircuitGameplayTests(ITestOutputHelper output)
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

    // the node of the key; a wrapper that shares its key with the control inside gives the control
    private static UiNode ByKey(UiNode tree, string key)
    {
        UiNode found = tree.Walk().First(n => n.Key == key);
        while (found.Walk().Skip(1).FirstOrDefault(n => n.Key == key) is UiNode inner)
            found = inner;
        return found;
    }

    private static turf FreeTileBy(atom a) => Dm.each<turf>(Dm.list(get_step(a, SOUTH), get_step(a, NORTH), get_step(a, EAST), get_step(a, WEST)))
        .First(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each<atom>(t.contents).Any(c => Dm.truthy(c.density)));

    [MetaServerFact]
    public void Bob_prints_components_wires_a_circuit_and_fires_it_with_a_compact_remote()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 10650, TimeoutSteps = 20000 },
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

        // Meta's component printer, its materials topped up: Bob prints a toggle component from its window.
        var printer = In(w, () => Dm.each<obj_machinery_component_printer>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_component_printer))).First());
        output.WriteLine("component printer at " + In(w, () => $"{printer.x},{printer.y},{printer.z}"));
        Assert.NotNull(In(w, () => printer.techweb));
        Do(w, () =>
        {
            Waiting.NoSleep(printer.materials.mat_container.insert_amount_mat(10 * SHEET_MATERIAL_AMOUNT, typeof(datum_material_iron)));
            Waiting.NoSleep(printer.materials.mat_container.insert_amount_mat(10 * SHEET_MATERIAL_AMOUNT, typeof(datum_material_glass)));
            Waiting.NoSleep(bobMob.forceMove(FreeTileBy(printer)));
            Waiting.Detach(printer.ui_interact(bobMob));
        });
        UiNode ui = Shown(w, windows, bob, "ComponentPrinter");
        UiNode recipe = ByKey(ui, "recipe:/datum/design/component/math/toggle");
        ByKey(recipe, "print").OnClick();
        w.Run(Second);
        Assert.Contains(In(w, () => Dm.each<atom>(get_turf(printer)).ToList()), a => a is obj_item_circuit_component_compare_toggle);

        // A circuit with a cell goes into a compact remote in Bob's hands, linked to the printer.
        var remote = In(w, () => Dm.@new<obj_item_compact_remote>(bobMob.loc));
        var circuit = In(w, () => Dm.@new<obj_item_integrated_circuit>(bobMob.loc));
        Do(w, () =>
        {
            circuit.set_cell(Dm.@new<obj_item_stock_parts_power_store_cell_high>(circuit));
            Waiting.NoSleep(printer.item_interaction(bobMob, circuit));
            Waiting.NoSleep(bobMob.put_in_hands(circuit));
        });
        w.Run(Second);
        Assert.Same(printer, In(w, () => circuit.linked_component_printer?.resolve()));
        Do(w, () => Waiting.NoSleep(remote.base_item_interaction(bobMob, circuit, Dm.list()))); // the shell's COMSIG_ATOM_ITEM_INTERACTION
        w.Run(Second);
        Assert.Same(remote, In(w, () => circuit.shell));
        Do(w, () => Waiting.NoSleep(bobMob.put_in_hands(remote)));

        // The editor: its component menu prints a toggle into the circuit; a wire dragged from the remote's signal to its toggle input.
        Do(w, () => Waiting.Detach(circuit.ui_interact(bobMob)));
        ui = Shown(w, windows, bob, "IntegratedCircuit");
        ByKey(ui, "toggle_component_menu").OnClick();
        ui = Shown(w, windows, bob, "IntegratedCircuit");
        UiNode menu = ui.Walk().First(n => n.Key == "component_menu" && n.Kind == UiKind.Section);
        ByKey(menu, "search").OnInput("Toggle");
        ui = Shown(w, windows, bob, "IntegratedCircuit");
        ByKey(ui.Walk().First(n => n.Key == "/obj/item/circuit_component/compare/toggle"), "print").OnClick();
        w.Run(Second);
        var toggle = In(w, () => Dm.each<obj_item_circuit_component_compare_toggle>(circuit.attached_components).Single());
        ui = Shown(w, windows, bob, "IntegratedCircuit");
        UiNode plane = ui.Walk().Single(n => n.Kind == UiKind.NodeGraph);
        int remoteNode = plane.Children.FindIndex(n => n.Text == "Compact Remote");
        int toggleNode = plane.Children.FindIndex(n => n.Text == "Toggle");
        Assert.True(remoteNode >= 0 && toggleNode >= 0);
        // the remote's outputs: User (0), Signal (1); the toggle's inputs: Set Toggle State (0), Toggle And Compare (1)
        plane.OnConnect(remoteNode, 1, toggleNode, 1);
        ui = Shown(w, windows, bob, "IntegratedCircuit");
        Assert.Contains(ui.Walk().Single(n => n.Kind == UiKind.NodeGraph).GraphLinks, l => l.FromNode == remoteNode && l.FromPort == 1 && l.ToNode == toggleNode && l.ToPort == 1);

        // Used in hand, the remote fires its signal: the toggle flips on.
        Assert.Equal(0f, In(w, () => toggle.toggle_state));
        Do(w, () => Waiting.NoSleep(remote.attack_self(bobMob)));
        w.Run(Second);
        Assert.Equal(1f, In(w, () => toggle.toggle_state));

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n----\n", runtimes));
    }
}
