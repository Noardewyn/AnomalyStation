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
/// Gaps package P3 (D327) through the real server on MetaStation: a geneticist at the genetics lab's DNA console with a second player
/// in the scanner opens the DnaConsole window, sees the subject, opens the sequencer and steps a gene of the subject's first
/// mutation (the gene changes and the subject takes genetic damage), saves the subject's genetic makeup and prints its enzyme
/// injector; the client sees the occupied scanner and the window's data.
/// </summary>
public class GeneticsGameplayTests(ITestOutputHelper output)
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

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf ClearNeighbour(atom a)
        => new[] { SOUTH, NORTH, EAST, WEST }.Select(d => get_step(a, d) as turf)
            .FirstOrDefault(t => t != null && Clear(t) && Dm.truthy(Waiting.NoSleep(a.Adjacent(t))));

    private static UiWindowState Window(ReplicaWorld replica, string iface) => replica.UiWindows.Tgui().LastOrDefault(win => win.Interface == iface);

    [MetaServerFact]
    public void A_geneticist_reads_pulses_and_copies_the_genes_of_a_player_in_the_scanner()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 331, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player gene = w.Join("Geneticist");
        RealHeadlessWorld.Player subject = w.Join("Subject");
        var me = (mob_living_carbon_human)w.MobOf(gene);
        var them = (mob_living_carbon_human)w.MobOf(subject);
        ReplicaWorld replica = gene.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => gene.Session.UiAct(window, action, parameters),
            SendClose = window => gene.Session.UiClose(window),
        };
        w.Run(Second);

        // Meta's genetics lab: a DNA console linked to its scanner
        var console = In(w, () => Dm.each<obj_machinery_computer_dna_console>()
            .Where(c => c.z == 1 && c.connected_scanner != null && ClearNeighbour(c) != null).OrderBy(c => c.dm_id).FirstOrDefault());
        Assert.NotNull(console);
        obj_machinery_dna_scannernew scanner = In(w, () => console.connected_scanner);
        Assert.False(In(w, () => InertSeam.IsInert(console) || InertSeam.IsInert(scanner)));
        output.WriteLine(In(w, () => $"console {console.x},{console.y}; scanner {scanner.x},{scanner.y}; area {(get_area(console) as area)?.name}"));

        // the subject in the scanner, the geneticist at the console (ADVANCEDTOOLUSER as any human); the start-up cooldowns pass
        Do(w, () =>
        {
            Waiting.NoSleep(scanner.open_machine());
            Waiting.NoSleep(scanner.close_machine(them));
            Waiting.NoSleep(me.forceMove(ClearNeighbour(console)));
            console.injector_ready = 0;
            console.enzyme_copy_timer = 0;
        });
        w.Run(Second);
        Assert.Same(scanner, In(w, () => them.loc));
        Assert.True(replica.TryGet(scanner.dm_id, out ReplicaEntity seenScanner));

        // the window: the subject's name and the mode buttons
        Assert.True(gene.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Window(replica, "DnaConsole");
        Assert.NotNull(window);
        UiNode content = Shown(windows, replica, window.Window);
        string name = In(w, () => them.name);
        Assert.Contains(content.Walk(), n => n.Key == "name" && n.Text == name);

        // the sequencer: the first gene of the subject's first mutation steps to the next letter
        content.Walk().First(n => n.Key == "mode:sequencer").OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        UiNode firstPicture = content.Walk().First(n => n.Key != null && n.Key.StartsWith("genome:", StringComparison.Ordinal));
        string alias = firstPicture.Key["genome:".Length..];
        firstPicture.OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        Type path = In(w, () => (Type)GET_MUTATION_TYPE_FROM_ALIAS(alias));
        string before = In(w, () => Dm.str(them.dna.mutation_index[path]));
        UiNode gene1 = content.Walk().FirstOrDefault(n => n.Key == "gene:0" && n.Kind == UiKind.Button);
        Assert.NotNull(gene1);
        gene1.OnClickWith(new UiClick(false, false, false, false));
        w.Run(Second);
        string after = In(w, () => Dm.str(them.dna.mutation_index[path]));
        Assert.NotEqual(before[..1], after[..1]);
        Assert.Equal(before[1..], after[1..]);
        Assert.NotNull(In(w, () => them.has_status_effect(typeof(datum_status_effect_genetic_damage))));

        // the enzymes: slot 1 saves the subject, its enzyme injector prints on the console's tile
        content.Walk().First(n => n.Key == "mode:enzymes").OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "save" && n.Kind == UiKind.Button).OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        UiNode slot = content.Walk().First(n => n.Key == "slot:1");
        Assert.Contains(name, slot.Title);
        UiNode print = slot.Walk().First(n => n.Key == "print:ue" && n.Kind == UiKind.Button);
        print.OnClick();
        w.Run(Second);
        var injector = In(w, () => Dm.locate<obj_item_dnainjector_timed>(console.loc));
        Assert.NotNull(injector);
        Assert.Equal(In(w, () => Dm.str(them.real_name)), In(w, () => Dm.str(injector.stored_dna.real_name)));
        output.WriteLine("runtimes: " + w.World.RuntimeErrorCount);
    }
}
